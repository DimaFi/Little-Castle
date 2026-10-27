using System;
using System.Collections.Generic;
using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapToolTests
    {
        static MapAuthoringDocument Map()=>new MapAuthoringDocument{mapId="shared-tools",bounds=new Vector4(-256,-256,512,512),heightField=new MapHeightFieldData{minimumHeight=-4,maximumHeight=12}};
        static MapToolPointer Pointer(float x,float z)=>new MapToolPointer(new Ray(new Vector3(x,500,z),Vector3.down),true,new Vector2(x,z));
        static MapToolController Controller(MapEditSession s,string id)
        {var c=new MapToolController(s,MapToolRegistry.ExistingBrushes());c.Select(id);return c;}
        [TestCase("surface.road")][TestCase("surface.meadow")][TestCase("surface.erase")]
        public void MapTools_SurfaceDragUsesOneCommandAndPreservesSoftnessOnReload(string id)
        {
            var s=new MapEditSession(Map());var c=Controller(s,id);c.context.settings.radius=6;c.context.settings.softness=.8f;c.context.settings.strength=.4f;
            c.Begin(Pointer(-2,-2),0);c.Move(Pointer(8,8));c.Tick(1);c.Release();
            var strokes=s.ExportEdits().strokes;Assert.That(strokes.Count,Is.EqualTo(1));Assert.That(strokes[0].points.Count,Is.GreaterThan(2));
            Assert.That(strokes[0].brushSoftness,Is.EqualTo(.801f).Within(.00001f));
            var other=new MapEditSession(Map());other.LoadJson(s.ExportJson());Assert.That(other.ExportEdits().strokes[0].strength,Is.EqualTo(.4f));
            s.Undo();Assert.That(s.ExportEdits().strokes,Is.Empty);Assert.That(s.CanUndo,Is.False);s.Redo();Assert.That(s.ExportEdits().strokes.Count,Is.EqualTo(1));
        }
        [Test] public void MapTools_FrozenStrokeSettingsAndCancelDoNotLeakChanges()
        {
            var s=new MapEditSession(Map());var c=Controller(s,"terrain.raise");var settings=c.context.settings;
            settings.radius=6;c.Begin(Pointer(-1,-1),0);settings.radius=30;c.Tick(1);
            Assert.That(c.context.settings.radius,Is.EqualTo(6));Assert.That(s.SampleHeight(-1,-1),Is.GreaterThan(0));
            c.Cancel();Assert.That(s.EditedHeightSamples,Is.Zero);Assert.That(c.context.settings.radius,Is.EqualTo(30));Assert.That(s.CanUndo,Is.False);
        }
        [Test] public void MapTools_HoldRaisesTerrainAndSwitchCommitsPendingTransaction()
        {
            var s=new MapEditSession(Map());var c=Controller(s,"terrain.raise");c.context.settings.radius=8;
            c.Begin(Pointer(0,0),0);float first=s.SampleHeight(0,0);c.Tick(1);Assert.That(s.SampleHeight(0,0),Is.GreaterThan(first));
            c.Select("surface.meadow");Assert.That(s.EditedHeightSamples,Is.GreaterThan(0));Assert.That(s.HasPending,Is.False);s.Undo();Assert.That(s.EditedHeightSamples,Is.Zero);
        }
        [Test] public void MapTools_ZeroStrengthHeightCreatesNoHistory()
        {
            var s=new MapEditSession(Map());var c=Controller(s,"terrain.raise");c.context.settings.strength=0;
            c.Begin(Pointer(0,0),0);c.Tick(1);c.Release();Assert.That(s.EditedHeightSamples,Is.Zero);Assert.That(s.CanUndo,Is.False);
        }
        [Test] public void MapTools_LongDragDrainsWithinBudgetWithoutGapsOrLosingEndpoint()
        {
            var s=new MapEditSession(Map());var c=Controller(s,"surface.road");c.context.settings.radius=1;
            c.Begin(Pointer(-100,0),0);c.Move(Pointer(100,0));c.Release();
            c.Tick(1);Assert.That(c.Active,Is.True);Assert.That(s.QuerySurface(new Rect(-256,-1,512,2))[0].points.Count,Is.InRange(2,33));
            for(int i=2;i<1000&&c.Active;i++)c.Tick(i);
            Assert.That(c.Active,Is.False);var points=s.ExportEdits().strokes[0].points;
            Assert.That(points[points.Count-1],Is.EqualTo(new Vector2(100,0)));for(int i=1;i<points.Count;i++)Assert.That(Vector2.Distance(points[i-1],points[i]),Is.LessThanOrEqualTo(.251f));
        }
        [Test] public void MapTools_ClickAndControlPointsUseSingleSelectionOwner()
        {
            var s=new MapEditSession(Map());var registry=MapToolRegistry.ExistingBrushes();int count=0;
            registry.Register(new MapClickTool("test.path","Path","Water",p=>count++,true,MapToolGesture.ControlPoints));
            Assert.Throws<ArgumentException>(()=>registry.Register(new MapClickTool("test.path","Duplicate","Water",p=>{})));
            var c=new MapToolController(s,registry);c.Select("test.path");c.Begin(Pointer(1,1),0);c.Tick(1);c.Release();Assert.That(count,Is.EqualTo(1));Assert.That(c.Active,Is.False);
        }
        [Test] public void MapTools_GroveUsesExistingStableRecordsAndSingleUndo()
        {
            var s=new MapEditSession(Map());s.RegisterTreeCatalog(new[]{new MapTreeDefinition{archetypeId="oak.a"}},new[]{new MapTreePack{id="oak",entries=new List<MapTreePackEntry>{new MapTreePackEntry{archetypeId="oak.a"}}}});
            var c=Controller(s,"vegetation.grove");c.context.treePackId="oak";c.context.settings.radius=18;c.context.settings.softness=0;
            c.Begin(Pointer(-10,-10),0);c.Move(Pointer(10,10));c.Tick(1);c.Release();Assert.That(s.TreeCount,Is.GreaterThan(3));
            var records=s.ExportTrees();s.Undo();Assert.That(s.TreeCount,Is.Zero);s.Redo();Assert.That(s.ExportTrees()[0].stableId,Is.EqualTo(records[0].stableId));
            var other=new MapEditSession(Map());other.LoadJson(s.ExportJson());Assert.That(other.ExportTrees()[0].stableId,Is.EqualTo(records[0].stableId));
        }
        [Test] public void MapTools_BatchCoalescesPerChunkAndKeepsDistantAreasSeparate()
        {
            var s=new MapEditSession(Map());var changes=new List<MapRegionChange>();s.Changed+=changes.Add;
            using(s.BatchNotifications())
            {
                for(int i=0;i<10;i++)s.AddStroke(new SurfacePaintStroke{id="stroke"+i,radius=2,points=new List<Vector2>{new Vector2(-1,-1)}});
                s.AddStroke(new SurfacePaintStroke{id="far",radius=2,points=new List<Vector2>{new Vector2(200,200)}});
                Assert.That(changes,Is.Empty);
            }
            Assert.That(changes.Count,Is.EqualTo(5));foreach(var change in changes){Assert.That(change.bounds.width,Is.LessThanOrEqualTo(32));Assert.That(change.bounds.Contains(new Vector2(100,100)),Is.False);Assert.That(change.preview,Is.False);}
        }
        [Test] public void MapTools_RevokedAccessRollsBackHeldStroke()
        {
            var s=new MapEditSession(Map());var c=Controller(s,"terrain.raise");c.Begin(Pointer(0,0),0);s.Access=MapEditAccess.None;
            Assert.Throws<UnauthorizedAccessException>(()=>c.Tick(1));Assert.That(s.EditedHeightSamples,Is.Zero);Assert.That(c.Active,Is.False);
        }
        [TestCase(5f)][TestCase(500f)][TestCase(50000f)]
        public void MapTools_SurfaceQueryFindsSameSlopeAtAnyCameraDistance(float distance)
        {
            var map=Map();for(int z=-8;z<=8;z++)for(int x=-8;x<=8;x++)MapHeightField.SetBase(map.heightField,x,z,x*.1f+z*.05f);
            var s=new MapEditSession(map);var target=new Vector3(-2,s.SampleHeight(-2,-3),-3);var direction=new Vector3(.1f,-1,.15f).normalized;
            Assert.That(MapSurfaceQuery.Raycast(s,new Ray(target-direction*distance,direction),out var hit),Is.True);
            Assert.That(Vector3.Distance(hit.point,target),Is.LessThan(.02f));Assert.That(hit.normal.y,Is.LessThan(1).And.GreaterThan(.9f));
        }
        [Test] public void MapTools_RaycastUsesPendingHeightsBeforeColliderUpdateAndRejectsOutsideMap()
        {
            var s=new MapEditSession(Map());s.BeginHeightStroke(4,1);s.AppendHeightPoint(new Vector2(-32,-32));
            Assert.That(MapSurfaceQuery.Raycast(s,new Ray(new Vector3(-32,500,-32),Vector3.down),out var hit),Is.True);Assert.That(hit.point.y,Is.EqualTo(1).Within(.001f));
            Assert.That(MapSurfaceQuery.Raycast(s,new Ray(new Vector3(-300,100,-300),Vector3.down),out _),Is.False);
        }
        [Test] public void MapTools_DefaultSurfaceFalloffIsPixelCompatibleWithLegacy()
        {
            var stroke=new SurfacePaintStroke{radius=8};for(float d=0;d<9;d+=.01f)
                Assert.That(MapBrushFalloff.SurfaceWeight(stroke,d),Is.EqualTo(ChunkTerrainVertexMasks.SoftCorridor(d,8*.62f,8*.38f)).Within(.000002f));
            Assert.That(MapBrushFalloff.Weight(7,8,0),Is.EqualTo(1));Assert.That(MapBrushFalloff.Weight(7,8,1),Is.LessThan(.1f));
        }
        [Test] public void MapTools_OldBaseFingerprintRemainsAcceptedWithMissingOptionalSoftness()
        {
            var map=Map();map.surface.strokes.Add(new SurfacePaintStroke{id="old",points=new List<Vector2>{Vector2.zero}});
            string raw=JsonUtility.ToJson(map).Replace(",\"brushSoftness\":0.0","").Replace(",\"brushSoftness\":0","");ulong hash=14695981039346656037UL;
            foreach(char ch in raw){hash^=ch;hash=unchecked(hash*1099511628211UL);}
            var s=new MapEditSession(map);var save=s.ExportSave();save.baseFingerprint=hash.ToString("x16");
            Assert.DoesNotThrow(()=>s.LoadJson(JsonUtility.ToJson(save)));
        }
        [Test] public void MapTools_LargeSurfaceOnlyInvalidatesResidentTilesAndPreservesFarTile()
        {
            var s=new MapEditSession(Map());using var cache=new MapSurfaceTileCache(s,16);
            var local=cache.Get(new ChunkCoordinate(-1,-1));var far=cache.Get(new ChunkCoordinate(7,7));int revision=far.revision;
            var c=Controller(s,"surface.road");c.context.settings.radius=64;c.Begin(Pointer(-1,-1),0);c.Release();
            Assert.That(cache.ResidentCount,Is.EqualTo(2));Assert.That(local.revision,Is.GreaterThan(1));Assert.That(far.revision,Is.EqualTo(revision));
        }
        [Test] public void MapTools_PresetIsPlainReusableSettingsAndRuntimeHasNoEditorDependency()
        {
            var preset=ScriptableObject.CreateInstance<MapBrushPreset>();try{preset.settings.radius=20;var json=JsonUtility.ToJson(preset);Assert.That(json,Does.Contain("20"));}finally{UnityEngine.Object.DestroyImmediate(preset);}
            foreach(var reference in typeof(MapToolController).Assembly.GetReferencedAssemblies())Assert.That(reference.Name,Does.Not.StartWith("UnityEditor"));
        }
    }
}
