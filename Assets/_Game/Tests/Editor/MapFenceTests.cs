using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LittleCastle.Rendering;
using LittleCastle.World;
using LittleCastle.World.Environment;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class MapFenceTests
    {
        static MapAuthoringDocument Map()=>new MapAuthoringDocument{mapId="fence-test",bounds=new Vector4(-64,-64,128,128)};
        static MapFenceRecord Line(long id=1,string variant="fence.wood01")=>new MapFenceRecord{id=id,variantId=variant,
            points=new List<Vector2>{new Vector2(-4,0),new Vector2(4,0)}};
        [Test] public void StraightInputsStayStraightAndPostsHaveEqualSpacingIncludingTheLastSpan()
        {
            var r=Line();r.points.Insert(1,new Vector2(-1,0));var path=new MapFencePath(r);r.points[0]=Vector2.one;
            Assert.That(path.Length,Is.EqualTo(8).Within(.0001f));
            foreach(var f in path.frames){Assert.That(f.position.y,Is.Zero.Within(.00001f));Assert.That(f.tangent.x,Is.EqualTo(1).Within(.00001f));}
            for(int i=1;i<path.PostCount;i++)Assert.That(Vector2.Distance(path.At((i-1)*path.ActualSpacing).position,path.At(i*path.ActualSpacing).position),Is.EqualTo(path.ActualSpacing).Within(.001f));
            Assert.That(path.At(path.Length).position,Is.EqualTo(new Vector2(4,0)));
        }
        [Test] public void CurvedInputsHaveContinuousTangentsThroughControlPoints()
        {
            var r=Line();r.points=new List<Vector2>{new Vector2(-8,0),new Vector2(-3,0),new Vector2(2,3),new Vector2(8,3)};
            var path=new MapFencePath(r);float turn=0;
            for(int i=1;i<path.frames.Count;i++)turn=Mathf.Max(turn,Vector2.Angle(path.frames[i-1].tangent,path.frames[i].tangent));
            Assert.That(turn,Is.LessThan(5));Assert.That(path.At(path.Length*.5f).position.y,Is.GreaterThan(.2f));
        }
        [Test] public void ClosedStrokeHasOnePostPerSpanAndNoDuplicateClosingPost()
        {
            var s=new MapEditSession(Map());s.BeginFenceStroke("fence.wood01");
            foreach(var p in new[]{new Vector2(-5,-5),new Vector2(5,-5),new Vector2(5,5),new Vector2(-5,5),new Vector2(-5,-5)})s.AppendFencePoint(p);
            s.CommitStroke();var path=new MapFencePath(s.ExportFences()[0]);
            Assert.That(path.record.closed,Is.True);Assert.That(path.PostCount,Is.EqualTo(path.SpanCount));
            Assert.That(Vector2.Distance(path.At(0).position,path.At(path.Length).position),Is.LessThan(.0001f));
            Assert.That(Vector2.Angle(path.At(0).tangent,path.At(path.Length).tangent),Is.LessThan(.01f));
        }
        [Test] public void OneFreehandStrokeHasOneUndoAndRoundTripsThroughMapAndWorldSave()
        {
            var s=new MapEditSession(Map());s.BeginFenceStroke("fence.wood02");
            s.AppendFencePoint(new Vector2(-4,0));s.AppendFencePoint(new Vector2(0,0));s.AppendFencePoint(new Vector2(4,2));
            Assert.That(s.FenceCount,Is.Zero);Assert.That(s.HasPending,Is.True);s.CommitStroke();Assert.That(s.FenceCount,Is.EqualTo(1));
            var recipe=s.ExportFences()[0];s.Undo();Assert.That(s.FenceCount,Is.Zero);Assert.That(s.CanUndo,Is.False);
            s.Redo();Assert.That(s.GetFence(recipe.id).variantId,Is.EqualTo("fence.wood02"));
            var loaded=new MapEditSession(Map());loaded.LoadJson(s.ExportJson());Assert.That(loaded.ExportJson(),Is.EqualTo(s.ExportJson()));
            Assert.That(new MapEditSession(s.ExportMap()).FenceCount,Is.EqualTo(1));
            var save=new WorldSaveState();s.StoreIn(save);loaded.LoadSave(JsonUtility.FromJson<WorldSaveState>(JsonUtility.ToJson(save)).mapEdits);
            Assert.That(loaded.FenceCount,Is.EqualTo(1));
            var baseAsset=ScriptableObject.CreateInstance<SurfacePaintData>();
            try{baseAsset.mapId="fence-test";baseAsset.bounds=Map().bounds;baseAsset.fences=s.ExportFences();Assert.That(new MapEditSession(MapEditSession.FromSurface(baseAsset)).FenceCount,Is.EqualTo(1));}
            finally{Object.DestroyImmediate(baseAsset);}
        }
        [Test] public void ToolControllerDrainsTheFinalPointerPositionAndCancelDoesNotCreateAFence()
        {
            var s=new MapEditSession(Map());var registry=new MapToolRegistry();registry.Register(new MapFenceTool());
            var controller=new MapToolController(s,registry);controller.context.settings.radius=1;controller.Select("fence.draw");
            controller.Begin(new MapToolPointer(default,true,new Vector2(-4,0)),0);controller.Move(new MapToolPointer(default,true,new Vector2(4,0)));controller.Complete();
            Assert.That(s.FenceCount,Is.EqualTo(1));Assert.That(s.ExportFences()[0].points[1].x,Is.EqualTo(4).Within(.001f));
            controller.Begin(new MapToolPointer(default,true,new Vector2(-4,2)),1);controller.Move(new MapToolPointer(default,true,new Vector2(4,2)));controller.Cancel();
            Assert.That(s.FenceCount,Is.EqualTo(1));Assert.That(s.HasPending,Is.False);
        }
        [Test] public void EndpointsSnapExactlyAndVariantChangeAndDeleteAreUndoable()
        {
            var s=new MapEditSession(Map());s.UpsertFence(Line());s.BeginFenceStroke("fence.wood02");
            s.AppendFencePoint(new Vector2(8,3));s.AppendFencePoint(new Vector2(4.2f,0));s.CommitStroke();
            var added=s.ExportFences().Find(r=>r.id!=1);Assert.That(added.points[added.points.Count-1],Is.EqualTo(new Vector2(4,0)));
            var first=s.GetFence(1);first.variantId="fence.wood02";s.UpsertFence(first);s.Undo();Assert.That(s.GetFence(1).variantId,Is.EqualTo("fence.wood01"));
            s.DeleteFence(1);Assert.That(s.HasFence(1),Is.False);s.Undo();Assert.That(s.HasFence(1),Is.True);
            s.ClearEdits();Assert.That(s.FenceCount,Is.Zero);s.Undo();Assert.That(s.FenceCount,Is.EqualTo(2));
        }
        [TestCase(false)] [TestCase(true)] public void ExistingFenceContinuesFromEitherEndInOneUndoAndKeepsItsRecipe(bool prepend)
        {
            var s=new MapEditSession(Map());var original=Line();original.scale=.8f;original.spacing=1.4f;original.smoothing=.7f;s.UpsertFence(original);
            string before=s.ExportJson();var end=original.points[prepend?0:1];var target=end+new Vector2(prepend?-4:4,2);
            s.BeginFenceStroke("fence.wood02",2,.9f,1.2f);s.AppendFencePoint(end+new Vector2(.1f,0));s.AppendFencePoint(target);
            Assert.That(s.GetFence(1).points.Count,Is.EqualTo(2),"A pending extension must not mutate the original.");
            Assert.That(s.FenceDraft.id,Is.EqualTo(1));Assert.That(s.FenceDraft.points.Count,Is.EqualTo(3));s.CommitStroke();
            var extended=s.GetFence(1);Assert.That(s.FenceCount,Is.EqualTo(1));Assert.That(extended.points[prepend?0:2],Is.EqualTo(target));
            Assert.That(extended.variantId,Is.EqualTo(original.variantId));Assert.That(extended.scale,Is.EqualTo(.8f));Assert.That(extended.spacing,Is.EqualTo(1.4f));Assert.That(extended.smoothing,Is.EqualTo(.7f));
            s.Undo();Assert.That(s.ExportJson(),Is.EqualTo(before));s.Redo();Assert.That(s.GetFence(1).points.Count,Is.EqualTo(3));
            var loaded=new MapEditSession(Map());loaded.LoadJson(s.ExportJson());Assert.That(loaded.GetFence(1).scale,Is.EqualTo(.8f));
        }
        [Test] public void CancelOrClickAtExistingEndpointLeavesTheFenceUntouchedAndExtensionCanCloseIt()
        {
            var s=new MapEditSession(Map());s.UpsertFence(Line());string before=s.ExportJson();
            s.BeginFenceStroke("fence.wood02");s.AppendFencePoint(new Vector2(4,0));s.AppendFencePoint(new Vector2(6,3));s.CancelStroke();
            Assert.That(s.ExportJson(),Is.EqualTo(before));s.BeginFenceStroke("fence.wood02");s.AppendFencePoint(new Vector2(4,0));s.CommitStroke();Assert.That(s.ExportJson(),Is.EqualTo(before));
            s.BeginFenceStroke("fence.wood02");foreach(var p in new[]{new Vector2(4,0),new Vector2(4,4),new Vector2(-4,4),new Vector2(-4,.1f)})s.AppendFencePoint(p);s.CommitStroke();
            Assert.That(s.FenceCount,Is.EqualTo(1));Assert.That(s.GetFence(1).closed,Is.True);s.Undo();Assert.That(s.ExportJson(),Is.EqualTo(before));
        }
        [Test] public void ScaleChangesHeightCrossSectionAndSpacingWithoutChangingGroundOrTopology()
        {
            var record=Line();record.scale=.8f;var path=new MapFencePath(record);Assert.That(path.ActualSpacing,Is.LessThanOrEqualTo(1.6f*.8f));
            var source=new MapFenceGeometry.Source(MapFenceCatalog.Default.entries[0].postLOD0);var output=new MapFenceGeometry.Buffer();
            MapFenceGeometry.Post(output,source,path.At(0),2,Vector3.zero,record.scale);
            Assert.That(output.vertices.Max(v=>v.y),Is.EqualTo(2+1.1f*.8f).Within(.001f));Assert.That(output.vertices.Count,Is.EqualTo(source.vertices.Length));
            var rail=new MapFenceGeometry.Source(MapFenceCatalog.Default.entries[0].railLOD0);output=new MapFenceGeometry.Buffer();
            MapFenceGeometry.Rail(output,rail,path,0,path.ActualSpacing,.86f,(x,z)=>2,Vector3.zero);
            Assert.That(output.vertices.Max(v=>v.y),Is.EqualTo(2+(.86f+rail.vertices.Max(v=>v.y))*.8f).Within(.001f));
            var s=new MapEditSession(Map());s.UpsertFence(record);var bad=s.ExportSave();bad.fences[0].scale=0;
            Assert.Throws<InvalidDataException>(()=>s.LoadSave(bad));Assert.That(s.GetFence(1).scale,Is.EqualTo(.8f));
        }
        [Test] public void InvalidSaveIsAtomicAndDifferentAuthoredFenceBasesCannotBeMixed()
        {
            var s=new MapEditSession(Map());s.UpsertFence(Line());string before=s.ExportJson();
            var bad=s.ExportSave();bad.fences[0].points[0]=new Vector2(float.NaN,0);Assert.Throws<InvalidDataException>(()=>s.LoadSave(bad));
            Assert.That(s.ExportJson(),Is.EqualTo(before));Assert.That(s.CanUndo,Is.True);
            bad=s.ExportSave();bad.fences.Add(bad.fences[0]);Assert.Throws<InvalidDataException>(()=>s.LoadSave(bad));
            var other=Map();other.fences.Add(Line(2));Assert.Throws<InvalidDataException>(()=>new MapEditSession(other).LoadJson(before));
        }
        [Test] public void PlayerHasNoFenceToolPermissionAndHistoryRechecksRole()
        {
            var s=new MapEditSession(Map(),MapEditAccess.Player);Assert.Throws<UnauthorizedAccessException>(()=>s.BeginFenceStroke("fence.wood01"));
            s.Access=MapEditAccess.Fences;s.UpsertFence(Line());s.Access=MapEditAccess.Player;
            Assert.Throws<UnauthorizedAccessException>(()=>s.Undo());Assert.That(s.FenceCount,Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)] public void ActualV6FingerprintAndSavesRemainCompatible(bool environment)
        {
            var map=Map();if(environment)map.environment=new MapEnvironmentData();
            // Hash the old serialized schema, independently of the compatibility DTO in production.
            string json=MapLegacyTestDocument.V6Json(map);
            if(!environment)json=Regex.Replace(json,@",""environment"":null","");
            ulong h=14695981039346656037UL;foreach(char c in json){h^=c;h=unchecked(h*1099511628211UL);}
            var original=new MapEditSession(map);var save=original.ExportSave();save.version=6;save.baseFingerprint=h.ToString("x16");
            var restored=new MapEditSession(map);Assert.DoesNotThrow(()=>restored.LoadSave(save));Assert.That(restored.FenceCount,Is.Zero);
        }
        [Test] public void SelectedRealMeshesAndBothMaterialsAreInstalledAndRemainLightweight()
        {
            var catalog=MapFenceCatalog.Default;Assert.That(catalog,Is.Not.Null,"Run Prepare Selected Fences first.");
            Assert.That(catalog.entries.Length,Is.EqualTo(2));var a=catalog.Resolve("fence.wood01");var b=catalog.Resolve("fence.wood02");
            Assert.That(a.Complete&&b.Complete,Is.True);Assert.That(a.postLOD0,Is.SameAs(b.postLOD0));Assert.That(a.railLOD0,Is.SameAs(b.railLOD0));
            Assert.That(a.material,Is.Not.SameAs(b.material));Assert.That(a.material.GetTexture("_MainTex"),Is.Not.SameAs(b.material.GetTexture("_MainTex")));
            Assert.That(a.postLOD0.triangles.Length/3+a.railLOD0.triangles.Length/3*2,Is.EqualTo(300));
            Assert.That(a.postLOD1.triangles.Length/3+a.railLOD1.triangles.Length/3*2,Is.EqualTo(68));
        }
        [Test] public void RealRailsDeformAlongCurveAndSlopeWithoutChangingTheirTopology()
        {
            var catalog=MapFenceCatalog.Default;Assert.That(catalog,Is.Not.Null);var source=new MapFenceGeometry.Source(catalog.entries[0].railLOD0);
            var record=Line();record.points=new List<Vector2>{new Vector2(-6,0),new Vector2(0,2),new Vector2(6,6)};var path=new MapFencePath(record);
            var buffer=new MapFenceGeometry.Buffer();float end=path.ActualSpacing;
            MapFenceGeometry.Rail(buffer,source,path,0,end,.41f,(x,z)=>x*.2f,Vector3.zero);
            Assert.That(buffer.vertices.Count,Is.EqualTo(source.vertices.Length));Assert.That(buffer.triangles.Count,Is.EqualTo(source.triangles.Length));
            for(int i=0;i<source.vertices.Length;i++)
            {
                var original=source.vertices[i];float station=original.x/1.6f*end;var frame=path.At(station);
                var side=new Vector2(-frame.tangent.y,frame.tangent.x);var expected=frame.position+side*original.z;
                Assert.That(Vector2.Distance(new Vector2(buffer.vertices[i].x,buffer.vertices[i].z),expected),Is.LessThan(.0001f));
                Assert.That(buffer.normals[i].magnitude,Is.EqualTo(1).Within(.0001f));
            }
        }
        [Test] public void PresentationBatchesMeshesSharesEndpointPostAndTracksHeightUndoAndDisposal()
        {
            var catalog=MapFenceCatalog.Default;Assert.That(catalog,Is.Not.Null);var s=new MapEditSession(Map());
            var a=Line();a.points=new List<Vector2>{Vector2.zero,new Vector2(3.2f,0)};
            var b=Line(2,"fence.wood02");b.points=new List<Vector2>{new Vector2(3.2f,0),new Vector2(6.4f,0)};
            s.UpsertFence(a);s.UpsertFence(b);
            var before=new HashSet<Mesh>(Resources.FindObjectsOfTypeAll<Mesh>());
            var view=new MapFencePresentation(s,catalog);
            try
            {
                view.Tick(Vector3.zero);Assert.That(view.VisibleSpans,Is.EqualTo(4));Assert.That(view.VisibleBatches,Is.EqualTo(2));
                var created=Resources.FindObjectsOfTypeAll<Mesh>().Where(m=>!before.Contains(m)&&m.name.StartsWith("Derived fence chunk")&&m.name.EndsWith("LOD0")).ToArray();
                Assert.That(created.Sum(m=>m.triangles.Length/3),Is.EqualTo(84*5+108*8),"The shared endpoint needs one post.");
                s.BeginHeightStroke(4,.8f);s.AppendHeightPoint(new Vector2(2,0));s.CommitStroke();view.Tick(Vector3.zero);
                s.Undo();view.Tick(Vector3.zero);s.DeleteFence(1);view.Tick(Vector3.zero);Assert.That(view.VisibleSpans,Is.EqualTo(2));
            }
            finally{view.Dispose();}
            Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Any(m=>!before.Contains(m)&&m.name.StartsWith("Derived fence chunk")),Is.False);
        }
    }
}
