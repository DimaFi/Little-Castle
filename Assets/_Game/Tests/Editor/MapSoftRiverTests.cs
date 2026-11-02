using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LittleCastle.Research;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapSoftRiverTests
    {
        static MapAuthoringDocument Map()=>new MapAuthoringDocument{mapId="soft-river",bounds=new Vector4(-128,-128,256,256),heightField=new MapHeightFieldData{minimumHeight=-12,maximumHeight=20}};
        static MapRiverRecord River(float width=4)=>new MapRiverRecord{id=10,layoutVersion=3,sectionProfile=new MapRiverSectionProfile(),nodes=new List<MapRiverNode>{
            new MapRiverNode{id=1,position=new Vector2(0,-20),waterHeight=0,width=width,depth=1,speed=.2f,leftTransition=2,rightTransition=2},
            new MapRiverNode{id=2,position=new Vector2(0,20),waterHeight=0,width=width,depth=1,speed=.3f,leftTransition=2,rightTransition=2}}};
        static MapRiverToolState Draft(MapEditSession s)
        {var t=new MapRiverToolState();t.ConfigureNewRiverProfile(true);t.StartRiver();t.draft=River();t.Refresh(s);return t;}
        [TestCase(2)][TestCase(4)][TestCase(10)][TestCase(40)][TestCase(60)]
        public void SharedSectionKeepsWidthBedBankAndDryTerrain(float width)
        {
            var s=new MapEditSession(Map());s.UpsertRiver(River(width));
            Assert.That(s.QueryWater(Vector2.zero,out var level,out var depth,out _),Is.True);
            Assert.That(level,Is.Zero);Assert.That(depth,Is.EqualTo(1).Within(.001));
            Assert.That(s.SampleHeight(width*.5f+.6f,0),Is.GreaterThanOrEqualTo(.10f));
            Assert.That(s.QueryWater(new Vector2(width*.5f+.2f,0),out _,out _,out _),Is.False);
            Assert.That(s.SampleHeight(width*.5f+3,0),Is.Zero);
            Assert.That(s.SampleBaseHeight(0,0),Is.Zero);
            Assert.That(s.RiverTerrainStep,Is.LessThanOrEqualTo(.25f));
        }
        [Test] public void ReadyDraftAutoAppliesOnceAndUndoRestoresTerrain()
        {
            var s=new MapEditSession(Map());var t=Draft(s);string before=s.ExportJson();
            Assert.That(t.PrepareForSave(s),Is.EqualTo(MapRiverToolState.SavePreparation.Applied));
            string after=s.ExportJson();Assert.That(t.HasUnappliedDraft(s),Is.False);
            Assert.That(t.PrepareForSave(s),Is.EqualTo(MapRiverToolState.SavePreparation.Unchanged));
            s.Undo();Assert.That(s.ExportJson(),Is.EqualTo(before));s.Redo();Assert.That(s.ExportJson(),Is.EqualTo(after));
        }
        [Test] public void OnePointAndInvalidDraftRetainExactAuthoringDataAndHistory()
        {
            var s=new MapEditSession(Map());var t=Draft(s);t.draft.nodes.RemoveAt(1);string map=s.ExportJson(),draft=t.ExportDraftJson();
            Assert.That(t.PrepareForSave(s),Is.EqualTo(MapRiverToolState.SavePreparation.Incomplete));
            Assert.That(t.ExportDraftJson(),Is.EqualTo(draft));Assert.That(s.ExportJson(),Is.EqualTo(map));
            t.draft=River();t.draft.nodes[1].position=new Vector2(150,0);draft=t.ExportDraftJson();
            Assert.That(t.PrepareForSave(s),Is.EqualTo(MapRiverToolState.SavePreparation.Invalid));
            Assert.That(t.ExportDraftJson(),Is.EqualTo(draft));Assert.That(s.ExportJson(),Is.EqualTo(map));Assert.That(s.CanUndo,Is.False);
        }
        [Test] public void EmptyInlineUnityDraftIsNotAnUnappliedRiver()
        {
            var s=new MapEditSession(Map());var t=new MapRiverToolState();
            Assert.That(t.RestoreDraftJson("{\"river\":{\"id\":0,\"nodes\":[]},\"mode\":0}"),Is.True);t.Refresh(s);
            Assert.That(t.HasUnappliedDraft(s),Is.False);
            string snapshot=t.ExportDraftJson();var reopened=new MapRiverToolState();reopened.RestoreDraftJson(snapshot);reopened.Refresh(s);
            Assert.That(reopened.ExportDraftJson(),Is.EqualTo(snapshot));
        }
        [Test] public void RecoveryPairRoundTripsIncompleteDraftAndRejectsAnotherMap()
        {
            string folder=Path.Combine(Path.GetTempPath(),"lc-soft-river-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                var s=new MapEditSession(Map());var t=Draft(s);t.draft.nodes.RemoveAt(1);string draft=t.ExportDraftJson();var map=s.ExportMap();
                string path=Path.Combine(folder,"map.json.recovery.json");MapAuthoringRecovery.Write(path,map,draft);
                var recovered=MapAuthoringRecovery.Read(path,map);Assert.That(recovered.riverDraft,Is.EqualTo(draft));
                var state=new MapRiverToolState();Assert.That(state.RestoreDraftJson(recovered.riverDraft),Is.True);state.Refresh(new MapEditSession(recovered.map));
                Assert.That(state.draft.nodes[0].id,Is.EqualTo(1));Assert.That(state.draft.nodes.Count,Is.EqualTo(1));
                map.mapId="another-map";Assert.Throws<InvalidDataException>(()=>MapAuthoringRecovery.Read(path,map));
            }
            finally{Directory.Delete(folder,true);}
        }
        [Test] public void NewProfileRoundTripsAndLegacySaveCannotEraseAuthoredSection()
        {
            var s=new MapEditSession(Map());s.UpsertRiver(River());var authored=new MapEditSession(s.ExportMap());string json=authored.ExportJson();
            Assert.That(authored.ExportSave().version,Is.EqualTo(10));authored.LoadJson(json);Assert.That(authored.ExportJson(),Is.EqualTo(json));
            var old=authored.ExportSave();old.version=9;Assert.Throws<InvalidDataException>(()=>authored.LoadSave(old));Assert.That(authored.ExportJson(),Is.EqualTo(json));
        }
        [Test] public void NativeV9GoldenAndCurrentRouteFingerprintStillLoad()
        {
            string root=Application.dataPath+"/_Game/Tests/Editor/Fixtures/SoftRiverV9/";
            var map=JsonUtility.FromJson<MapAuthoringDocument>(File.ReadAllText(root+"authored-map.json"));var s=new MapEditSession(map);
            var saved=JsonUtility.FromJson<MapEditSave>(File.ReadAllText(root+"authored-save.json"));Assert.That(saved.version,Is.EqualTo(9));
            Assert.DoesNotThrow(()=>s.LoadSave(saved));Assert.That(s.GetWaterfall(810003).route.sections.Count,Is.GreaterThan(2));
        }
        [Test] public void MigrationKeepsChainIdentityEndpointGeometryAndOneUndo()
        {
            string root=Application.dataPath+"/_Game/Tests/Editor/Fixtures/SoftRiverV9/";
            var s=new MapEditSession(JsonUtility.FromJson<MapAuthoringDocument>(File.ReadAllText(root+"authored-map.json")));
            string before=s.ExportJson();float width=s.RiverNetwork.Endpoint(810001,false).width;
            s.UpgradeRiverProfiles(new long[]{810001,810002},new MapRiverSectionProfile{maximumFill=4});Assert.That(s.GetRiver(810001).layoutVersion,Is.EqualTo(3));
            Assert.That(s.RiverNetwork.Endpoint(810001,false).width,Is.EqualTo(width).Within(.00001));
            Assert.That(s.GetWaterfall(810003).id,Is.EqualTo(810003));string after=s.ExportJson();
            s.Undo();Assert.That(s.ExportJson(),Is.EqualTo(before));s.Redo();Assert.That(s.ExportJson(),Is.EqualTo(after));
        }
        [Test] public void ExcessiveEarthworkFailsWithoutCommittingPendingOrClearingRedo()
        {
            var s=new MapEditSession(Map());s.UpsertRiver(River());s.Undo();var r=River();r.nodes.ForEach(n=>n.waterHeight=10);string before=s.ExportJson();
            Assert.Throws<InvalidDataException>(()=>s.UpsertRiver(r));Assert.That(s.ExportJson(),Is.EqualTo(before));Assert.That(s.CanRedo,Is.True);
        }
        [Test] public void WetFallCorridorExcludesGrassAndUndoRestoresOriginalState()
        {
            string root=Application.dataPath+"/_Game/Tests/Editor/Fixtures/SoftRiverV9/";
            var s=new MapEditSession(JsonUtility.FromJson<MapAuthoringDocument>(File.ReadAllText(root+"authored-map.json")));
            var route=s.GetWaterfall(810003).route;var p=route.sections[route.sections.Count/2].FrontCenter;
            Assert.That(s.VegetationAllowed(p.x,p.z,0),Is.False);s.DeleteWaterfall(810003);
            Assert.That(s.VegetationAllowed(p.x,p.z,0),Is.True);s.Undo();Assert.That(s.VegetationAllowed(p.x,p.z,0),Is.False);
        }
        [Test] public void SpraySourcesFindIntermediateLedgeAndShareTierBudget()
        {
            var route=new MapWaterfallRouteData();
            for(int i=0;i<5;i++)
            {
                var section=new MapWaterfallSection{kind=i==2?MapWaterfallSectionKind.Impact:MapWaterfallSectionKind.Contact};
                for(int j=-1;j<=1;j++)section.points.Add(new MapWaterfallContactPoint{position=new Vector3(j,10-i*2,i*2),normal=Vector3.up,detached=i==1});
                route.sections.Add(section);
            }
            Assert.That(MapWaterfallSpraySources.Resolve(route).Any(p=>p.row==2),Is.True);
            Assert.That(MapWaterfallSpraySources.Budget(RiverEffectsQuality.Low),Is.EqualTo(40));
            Assert.That(MapWaterfallSpraySources.Budget(RiverEffectsQuality.Medium),Is.EqualTo(96));
            Assert.That(MapWaterfallSpraySources.Budget(RiverEffectsQuality.High),Is.EqualTo(176));
        }
        [TestCase(false)] [TestCase(true)] public void RefinedColliderMatchesTheNarrowBedAndUndoRestoresDryGround(bool budgeted)
        {
            var root=new GameObject("Soft river terrain collision test");var mesh=new Mesh();MapTerrainPresentation view=null;
            try
            {
                var vertices=new Vector3[17*17];var triangles=new List<int>();
                for(int z=0;z<=16;z++)for(int x=0;x<=16;x++)
                {
                    int i=z*17+x;vertices[i]=new Vector3(x-8,0,z-8);
                    if(x<16&&z<16){triangles.AddRange(new[]{i,i+17,i+1,i+1,i+17,i+18});}
                }
                mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();
                root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>();
                var session=new MapEditSession(Map());view=new MapTerrainPresentation(session,root.GetComponent<MeshFilter>(),projectSceneTrees:false);
                view.BudgetedUpdates=budgeted;session.UpsertRiver(River(2));
                if(budgeted)
                {
                    view.FlushPreviewBudget(3,double.PositiveInfinity);view.FlushColliderBudget(int.MaxValue);Physics.SyncTransforms();
                    Assert.That(view.Raycast(new Ray(new Vector3(0,5,0),Vector3.down),out var pending),Is.True);Assert.That(pending.point.y,Is.Zero.Within(.001f),"Refinement must retain the complete dry collider while vertices are pending.");
                    while(view.HasPendingPreview)view.FlushPreviewBudget(128,double.PositiveInfinity);
                }
                view.FlushColliderBudget(int.MaxValue);Physics.SyncTransforms();
                Assert.That(view.Raycast(new Ray(new Vector3(0,5,0),Vector3.down),out var wet),Is.True);
                Assert.That(wet.point.y,Is.EqualTo(session.SampleHeight(0,0)).Within(.001f));Assert.That(wet.point.y,Is.EqualTo(-1).Within(.001f));
                Assert.That(session.SampleHeight(7,7),Is.Zero);
                session.Undo();if(budgeted)while(view.HasPendingPreview)view.FlushPreviewBudget(128,double.PositiveInfinity);view.FlushColliderBudget(int.MaxValue);Physics.SyncTransforms();
                Assert.That(view.Raycast(new Ray(new Vector3(0,5,0),Vector3.down),out var dry),Is.True);Assert.That(dry.point.y,Is.Zero.Within(.001f));
            }
            finally{view?.Dispose();UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test] public void PointRoadStampHasFiniteClearanceDistance()
        {Assert.That(MapRiverPath.SegmentDistance(Vector2.zero,Vector2.zero,new Vector2(3,4),new Vector2(3,4)),Is.EqualTo(5).Within(.0001f));}
        [Test] public void GraduallyFlatteningLedgeGetsItsOwnSpraySource()
        {
            var route=new MapWaterfallRouteData();var levels=new[]{12f,11,10,9,8,7.2f,6.6f,6.2f,6,5.9f,5.8f,5.7f,5.6f};
            for(int i=0;i<levels.Length;i++)
            {
                var section=new MapWaterfallSection();for(int j=-1;j<=1;j++)section.points.Add(new MapWaterfallContactPoint{position=new Vector3(i*.5f,levels[i],j),normal=Vector3.up});route.sections.Add(section);
            }
            Assert.That(MapWaterfallSpraySources.Resolve(route).Any(p=>p.row>=4&&p.row<=9),Is.True);
        }
        static MapEditSession LandingSession()
        {
            string root=Application.dataPath+"/_Game/Tests/Editor/Fixtures/SoftRiverV9/";
            var s=new MapEditSession(JsonUtility.FromJson<MapAuthoringDocument>(File.ReadAllText(root+"authored-map.json")));
            s.UpgradeRiverProfiles(new long[]{810001,810002},new MapRiverSectionProfile{maximumFill=4});return s;
        }
        [Test] public void LandingMeetsTheRiverAcrossItsFullWidthAndSavedDepth()
        {
            var s=LandingSession();var route=s.GetWaterfall(810003).route;var lower=s.RiverNetwork.Endpoint(810002,true);
            var end=route.sections[route.sections.Count-1];var prior=route.sections[route.sections.Count-2];
            float distance=Vector2.Distance(new Vector2(end.FrontCenter.x,end.FrontCenter.z),new Vector2(prior.FrontCenter.x,prior.FrontCenter.z));
            foreach(var p in end.points)
            {
                Assert.That(p.Front(end.thickness).y,Is.EqualTo(lower.waterHeight+.018f).Within(.00001f));
                float fraction=p.across>=0?p.across:end.points.IndexOf(p)/(float)(end.points.Count-1);
                float depth=lower.depth*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,1,Mathf.Abs(fraction*2-1))));
                Assert.That(p.position.y,Is.EqualTo(lower.waterHeight-depth).Within(.00002f));
            }
            Assert.That(distance,Is.LessThanOrEqualTo(.03f));
            Assert.That((end.FrontCenter.y-prior.FrontCenter.y)/distance,Is.EqualTo(lower.surfaceSlope).Within(.06f));
            var loaded=new MapEditSession(s.ExportMap());Assert.That(JsonUtility.ToJson(loaded.GetWaterfall(810003).route),Is.EqualTo(JsonUtility.ToJson(route)));
        }
        [Test] public void LandingMeshNormalsAndFoamFollowTheReceivingSurface()
        {
            var s=LandingSession();var route=s.GetWaterfall(810003).route;var lower=new MapRiverPath(s.GetRiver(810002));
            var body=MapWaterfallMeshBuilder.Build(route,receivingRiver:lower);var foam=MapWaterfallMeshBuilder.Impact(route,receivingRiver:lower);
            try
            {
                int columns=route.sections[0].points.Count,first=(route.sections.Count-1)*columns;
                for(int c=0;c<columns;c++){Assert.That(Vector3.Distance(body.normals[first+c],lower.SurfaceNormal(0)),Is.LessThan(.00001f));Assert.That(body.colors[first+c].a,Is.EqualTo(1).Within(.00001f));}
                var vertices=foam.vertices;var colors=foam.colors;
                var uv=foam.uv;
                for(int i=0;i<vertices.Length;i++)if(colors[i].r>.001f)
                {
                    Assert.That(lower.Nearest(new Vector2(vertices[i].x,vertices[i].z),out var f,out float signed),Is.True);
                    Assert.That(vertices[i].y,Is.EqualTo(f.water+.022f).Within(.00001f));
                    Assert.That(uv[i].x,Is.EqualTo(.5f+signed/f.width).Within(.00001f));
                }
                for(int c=1;c<=32;c++)Assert.That(Vector2.Distance(uv[c],uv[0]),Is.LessThan(.00001f),"Coincident foam center vertices must share UVs, without a radial fan.");
                Assert.That(body.triangles.Length/3,Is.LessThanOrEqualTo(11200));
            }
            finally{UnityEngine.Object.DestroyImmediate(body);UnityEngine.Object.DestroyImmediate(foam);}
        }
    }
}
