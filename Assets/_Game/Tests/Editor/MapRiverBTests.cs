using System;
using System.Collections.Generic;
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
        [Test] public void ManyPreviewUpdatesCommitOneUndoAndCancelKeepsHistory()
        {
            var s=Session();s.UpsertRiver(River());string before=s.ExportJson();var g=new MapRiverEditGesture(s,s.GetRiver(20));
            for(int i=0;i<10;i++){var d=s.GetRiver(20);d.nodes[1].position.x=i*.1f;Assert.That(g.Update(d),Is.True,g.Message);Assert.That(s.ExportJson(),Is.EqualTo(before));}
            Assert.That(g.Complete(),Is.True,g.Message);string after=s.ExportJson();Assert.That(after,Is.Not.EqualTo(before));s.Undo();Assert.That(s.ExportJson(),Is.EqualTo(before));s.Redo();Assert.That(s.ExportJson(),Is.EqualTo(after));
            g=new MapRiverEditGesture(s,s.GetRiver(20));var changed=s.GetRiver(20);changed.nodes[1].depth=2;Assert.That(g.Update(changed),Is.True);g.Cancel();Assert.That(s.ExportJson(),Is.EqualTo(after));
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
