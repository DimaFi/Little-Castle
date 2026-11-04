using System;
using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapRiverBTests
    {
        [Test] public void GoldenPreBVersion10KeepsItsNativeFingerprint()
        {
            string root=Application.dataPath+"/_Game/Tests/Editor/Fixtures/SoftRiverV10BeforeB/";
            if(!System.IO.File.Exists(root+"candidate-map.json"))Assert.Ignore("Private authored-map golden is supplied by the canonical local workspace.");
            var map=JsonUtility.FromJson<MapAuthoringDocument>(System.IO.File.ReadAllText(root+"candidate-map.json"));
            var save=JsonUtility.FromJson<MapEditSave>(System.IO.File.ReadAllText(root+"candidate-save.json"));
            var s=new MapEditSession(map);Assert.That(s.ExportSave().baseFingerprint,Is.EqualTo(save.baseFingerprint));s.LoadSave(save);Assert.That(s.ExportSave().version,Is.EqualTo(10));
        }
        static MapEditSession Session()=>new MapEditSession(new MapAuthoringDocument{mapId="river-B",bounds=new Vector4(-100,-100,200,200),heightField=new MapHeightFieldData{minimumHeight=-12,maximumHeight=24}});
        static MapRiverRecord River()=>new MapRiverRecord{id=20,layoutVersion=3,sectionProfile=new MapRiverSectionProfile(),nodes=new List<MapRiverNode>{new MapRiverNode{id=1,position=new Vector2(0,-24),waterHeight=0,width=4,depth=1,speed=.2f,leftTransition=1,rightTransition=1},new MapRiverNode{id=2,position=new Vector2(0,0),waterHeight=0,width=6,depth=1.2f,speed=.3f,leftTransition=1,rightTransition=1},new MapRiverNode{id=3,position=new Vector2(0,24),waterHeight=0,width=4,depth=1,speed=.2f,leftTransition=1,rightTransition=1}}};
        [TestCase(1,false,0f)] [TestCase(2,false,0f)] [TestCase(3,false,0f)]
        [TestCase(3,true,0f)] [TestCase(3,false,10000f)] [TestCase(3,false,-10000f)]
        public void NearestPreservesOriginalFrameAndTiesAcrossIndexBorders(int version,bool curved,float translation)
        {
            var recipe=River();recipe.layoutVersion=version;
            // Large-coordinate cases use a straight valid strip: the existing finite-difference
            // tangent validator can reject quantized gentle bends at +/-10000 before any query.
            foreach(var n in recipe.nodes){n.position+=new Vector2(-8+translation,-8+translation);n.width+=4;n.leftTransition=n.rightTransition=2;}
            if(curved)recipe.nodes[1].position.x+=.4f;
            recipe.nodes[1].depth=2;recipe.nodes[1].speed=.7f;
            var path=new MapRiverPath(recipe);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var index=(Dictionary<ChunkCoordinate,List<int>>)typeof(MapRiverPath).GetField("segmentIndex",flags).GetValue(path);
            var blend=typeof(MapRiverPath).GetMethod("Blend",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(float x in new[]{-20f,-16f,-15.9999f,-12f,-8.0001f,-8f,-7.9999f,-4f,0f,4f})
            for(float z=-40;z<=24;z+=2)
            {
                var p=new Vector2(x+translation,z+translation);var expected=default(MapRiverFrame);float signed=0,best=float.PositiveInfinity;bool found=false;
                if(path.bounds.Contains(p)&&index.TryGetValue(new ChunkCoordinate(Mathf.FloorToInt(p.x/8),Mathf.FloorToInt(p.y/8)),out var candidates))
                {
                    // Frozen pre-optimization query: interpolate every improving candidate.
                    foreach(int i in candidates)
                    {
                        var a=path.frames[i];var b=path.frames[i+1];var d=b.center-a.center;
                        float t=Mathf.Clamp01(Vector2.Dot(p-a.center,d)/d.sqrMagnitude);var c=Vector2.Lerp(a.center,b.center,t);float sq=(p-c).sqrMagnitude;
                        if(sq>=best)continue;best=sq;expected=(MapRiverFrame)blend.Invoke(null,new object[]{a,b,t});signed=Vector2.Dot(p-c,expected.Left);
                    }
                    found=best<Mathf.Pow(expected.width*.5f+Mathf.Max(expected.leftTransition,expected.rightTransition)+3,2);
                }
                Assert.That(path.Nearest(p,out var actual,out float distance),Is.EqualTo(found),p.ToString());
                Assert.That(JsonUtility.ToJson(actual),Is.EqualTo(JsonUtility.ToJson(expected)),p.ToString());
                Assert.That(distance,Is.EqualTo(signed),p.ToString());
            }
        }
        [Test] public void ManyPreviewUpdatesCommitOneUndoAndCancelKeepsHistory()
        {
            var s=Session();s.UpsertRiver(River());string before=s.ExportJson();var g=new MapRiverEditGesture(s,s.GetRiver(20));
            MapEditSession resident=null;
            for(int i=0;i<10;i++)
            {
                var d=s.GetRiver(20);d.nodes[1].position.x=i*.1f;Assert.That(g.Update(d),Is.True,g.Message);Assert.That(s.ExportJson(),Is.EqualTo(before));
                if(resident==null)resident=g.PreviewSession;else Assert.That(g.PreviewSession,Is.SameAs(resident));
                Assert.That(g.LastValid.preview,Is.SameAs(resident));
                Assert.That(MapRiverEditSolver.TrySolve(s,d,out var materialized,out string error),Is.True,error);
                Assert.That(JsonUtility.ToJson(g.LastValid.river),Is.EqualTo(JsonUtility.ToJson(materialized.river)),"Baseline-only validation must return the original materialized solver result.");
                Assert.That(JsonUtility.ToJson(g.PreviewSession.ExportRiverNetwork()),Is.EqualTo(JsonUtility.ToJson(materialized.preview.ExportRiverNetwork())));
            }
            Assert.That(g.Complete(),Is.True,g.Message);string after=s.ExportJson();Assert.That(after,Is.Not.EqualTo(before));s.Undo();Assert.That(s.ExportJson(),Is.EqualTo(before));s.Redo();Assert.That(s.ExportJson(),Is.EqualTo(after));
            g=new MapRiverEditGesture(s,s.GetRiver(20));var changed=s.GetRiver(20);changed.nodes[1].depth=2;Assert.That(g.Update(changed),Is.True);g.Cancel();Assert.That(s.ExportJson(),Is.EqualTo(after));
        }
        [Test] public void CandidatePreparationPreservesLiveDataRedoAndNotificationsIncludingFailure()
        {
            var s=Session();var river=River();
            river.rocks.Add(new MapRockInstance{id=91,archetypeId="fixture-rock",catalogVersion=1,position=new Vector2(3,-14),scale=1,station=10,side=1,bankOffset=.3f});
            s.UpsertRiver(river);s.BeginHeightStroke(2,1,1);s.AppendHeightPoint(new Vector2(50,50));s.CommitStroke();s.Undo();
            string before=s.ExportJson();Assert.That(s.CanRedo,Is.True);int notifications=0;s.Changed+=_=>notifications++;
            var candidate=s.GetRiver(20);candidate.nodes[1].position.x=.2f;
            var prepared=s.ValidateRiverEditCandidate(candidate);
            Assert.That(MapRiverEditSolver.TrySolve(s,candidate,out var oldPath,out string error),Is.True,error);
            Assert.That(JsonUtility.ToJson(prepared),Is.EqualTo(JsonUtility.ToJson(oldPath.river)),"Automatic rocks use the unchanged baseline, as before.");
            Assert.That(s.ExportJson(),Is.EqualTo(before));Assert.That(s.CanRedo,Is.True);Assert.That(notifications,Is.Zero);
            prepared.nodes[0].depth=4;Assert.That(s.ExportJson(),Is.EqualTo(before),"Prepared recipes remain detached.");
            candidate.nodes[1].position.x=400;
            Assert.Throws<System.IO.InvalidDataException>(()=>s.ValidateRiverEditCandidate(candidate));
            Assert.That(s.ExportJson(),Is.EqualTo(before));Assert.That(s.CanRedo,Is.True);Assert.That(notifications,Is.Zero);
        }
        [Test] public void BaselineOnlySolverMatchesMaterializedLinkedRouteAndOneExactUndo()
        {
            string root=Application.dataPath+"/_Game/Tests/Editor/Fixtures/SoftRiverV9/";
            var s=new MapEditSession(JsonUtility.FromJson<MapAuthoringDocument>(System.IO.File.ReadAllText(root+"authored-map.json")));
            s.UpgradeRiverProfiles(new long[]{810001,810002},new MapRiverSectionProfile{maximumFill=4});
            string before=s.ExportJson();var gesture=new MapRiverEditGesture(s,s.GetRiver(810001));MapEditSession resident=null;
            for(int i=1;i<=3;i++)
            {
                var candidate=s.GetRiver(810001);candidate.nodes[candidate.nodes.Count-1].width+=i*.01f;
                Assert.That(gesture.Update(candidate),Is.True,gesture.Message);
                if(resident==null)resident=gesture.PreviewSession;else Assert.That(gesture.PreviewSession,Is.SameAs(resident));
                Assert.That(MapRiverEditSolver.TrySolve(s,candidate,out var expected,out string error),Is.True,error);
                Assert.That(JsonUtility.ToJson(gesture.LastValid.river),Is.EqualTo(JsonUtility.ToJson(expected.river)));
                Assert.That(JsonUtility.ToJson(gesture.PreviewSession.ExportRiverNetwork()),Is.EqualTo(JsonUtility.ToJson(expected.preview.ExportRiverNetwork())));
                Assert.That(s.ExportJson(),Is.EqualTo(before));
            }
            Assert.That(gesture.Complete(),Is.True,gesture.Message);string after=s.ExportJson();
            s.Undo();Assert.That(s.ExportJson(),Is.EqualTo(before));s.Redo();Assert.That(s.ExportJson(),Is.EqualTo(after));
        }
        [Test] public void RejectedPreviewAndRevokedAccessNeverChangeMap()
        {
            var s=Session();s.UpsertRiver(River());string before=s.ExportJson();var g=new MapRiverEditGesture(s,s.GetRiver(20));var d=s.GetRiver(20);d.nodes[1].position=new Vector2(400,0);Assert.That(g.Update(d),Is.False);Assert.That(g.Complete(),Is.False);Assert.That(g.Draft.nodes[1].position.x,Is.EqualTo(400));Assert.That(s.ExportJson(),Is.EqualTo(before));
            g=new MapRiverEditGesture(s,s.GetRiver(20));d=s.GetRiver(20);d.nodes[1].depth=2;Assert.That(g.Update(d),Is.True);s.Access=MapEditAccess.None;Assert.That(g.Complete(),Is.False);Assert.That(s.ExportJson(),Is.EqualTo(before));
        }
        [Test] public void InsertionPreservesCubicAndInterpolatesPropertiesWithStableId()
        {
            var r=River();r.nodes[1].position.x=2;MapRiverSpline.SetHandle(r,0,true,new Vector2(1,8));var original=MapRiverNodeEditing.Copy(r);
            int index=MapRiverNodeEditing.Insert(r,0,.5f,99);Assert.That(index,Is.EqualTo(1));Assert.That(r.nodes[1].id,Is.EqualTo(99));Assert.That(r.nodes[1].width,Is.EqualTo(5));Assert.That(r.nodes[1].depth,Is.EqualTo(1.1f).Within(.0001f));
            for(int i=0;i<=40;i++){float t=i/40f;var actual=t<=.5f?MapRiverSpline.Evaluate(r,0,t*2):MapRiverSpline.Evaluate(r,1,(t-.5f)*2);Assert.That(Vector2.Distance(actual,MapRiverSpline.Evaluate(original,0,t)),Is.LessThan(.0001f));}
            MapRiverNodeEditing.Remove(r,1);Assert.That(r.nodes.Exists(n=>n.id==99),Is.False);Assert.That(r.shape.handles.Exists(h=>h.nodeId==99),Is.False);
        }
        [Test] public void SplineSaveV11RoundTripAndOldSaveCannotEraseHandles()
        {
            var s=Session();s.UpsertRiver(River());string legacy=s.ExportJson();Assert.That(s.ExportSave().version,Is.EqualTo(10));
            var d=s.GetRiver(20);MapRiverSpline.SetHandle(d,1,true,new Vector2(.1f,8));s.ApplyRiverEdit(d);var save=s.ExportSave();Assert.That(save.version,Is.EqualTo(11));
            var loaded=Session();loaded.LoadJson(s.ExportJson());Assert.That(loaded.ExportJson(),Is.EqualTo(s.ExportJson()));
            Assert.Throws<System.IO.InvalidDataException>(()=>loaded.LoadJson(legacy));Assert.That(loaded.GetRiver(20).shape.enabled,Is.True);
        }
        [Test] public void SurfacePickingUsesWaterHeightRatherThanDeepGround()
        {
            var r=River();foreach(var n in r.nodes)n.waterHeight=7;
            Assert.That(MapRiverPicking.Pick(new[]{new MapRiverPath(r)},new Ray(new Vector3(0,12,0),Vector3.down),out var id,out var p,out _),Is.True);Assert.That(id,Is.EqualTo(20));Assert.That(p.y,Is.EqualTo(7.018f).Within(.001));
            Assert.That(MapRiverPicking.Pick(new[]{new MapRiverPath(r)},new Ray(new Vector3(40,12,0),Vector3.down),out _,out _,out _),Is.False);
        }
    }
}
