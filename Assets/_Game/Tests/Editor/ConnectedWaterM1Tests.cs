using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class ConnectedWaterM1Tests
    {
        static MapAuthoringDocument Map() => new MapAuthoringDocument { mapId = "m1-contact-test", bounds = new Vector4(-64, -64, 128, 128),
            heightField = new MapHeightFieldData { minimumHeight = -8, maximumHeight = 32 } };
        static MapRiverRecord River(long id, float from, float to, float level) => new MapRiverRecord { id = id, nodes = new List<MapRiverNode> {
            new MapRiverNode { id=1, position=new Vector2(0,from), width=4, depth=.2f, waterHeight=level, leftTransition=2, rightTransition=2 },
            new MapRiverNode { id=2, position=new Vector2(0,to), width=4, depth=.2f, waterHeight=level, leftTransition=2, rightTransition=2 } } };
        static MapEditSession Session()
        {
            var s = new MapEditSession(Map()); s.UpsertRiver(River(10,-30,0,4.2f)); s.UpsertRiver(River(20,12,42,.2f)); return s;
        }
        static MapWaterfallSupport Surface(bool gap = false, bool reverse = false, bool obstacle = false)
        {
            var result = new MapWaterfallSupport(); var triangles = new List<(string key, int id, Vector3 a, Vector3 b, Vector3 c)>();
            float Height(float z) => z<=2?4:z<=4?4-(z-2):z<=6?2:z<=10?2-(z-6)*.5f:0;
            for (int z=0;z<12;z++) for(int x=-3;x<3;x++)
            {
                if(gap&&z>=2&&z<8)continue;
                float h0=gap?(z<2?4:0):Height(z),h1=gap?(z<2?4:0):Height(z+1);
                var a=new Vector3(x,h0,z);var b=new Vector3(x+1,h0,z);var c=new Vector3(x,h1,z+1);var d=new Vector3(x+1,h1,z+1);
                string key="fixture:"+x+":"+z; triangles.Add((key,0,a,c,b));triangles.Add((key,1,b,c,d));
            }
            if(obstacle){triangles.Add(("blocked",0,new Vector3(-3,6,5),new Vector3(-3,6,7),new Vector3(3,6,5)));
                triangles.Add(("blocked",1,new Vector3(3,6,5),new Vector3(-3,6,7),new Vector3(3,6,7)));}
            if(reverse)triangles.Reverse(); foreach(var t in triangles)result.Add(t.key,t.id,t.a,t.b,t.c);return result;
        }
        static MapWaterfallRecord Record(MapEditSession s, MapWaterfallSupport support = null)
        {
            var r = new MapWaterfallRecord { id=101, upstreamRiverId=10, upstreamNodeId=2, downstreamRiverId=20, downstreamNodeId=1 };
            r.route=MapWaterfallRouteBuilder.Build(s.RiverNetwork.Endpoint(10,false),s.RiverNetwork.Endpoint(20,true),support??Surface());return r;
        }

        internal static MapEditSession CreateRoutedSession(out MapWaterfallRecord waterfall)
        {
            var session = Session();
            waterfall = Record(session);
            session.CreateWaterfall(waterfall);
            return session;
        }
        [Test] public void SupportUsesActualTrianglesAndStableOrder()
        {
            var a=Surface();var b=Surface(reverse:true);Assert.That(a.Revision,Is.EqualTo(b.Revision));
            Assert.That(a.Height(new Vector2(0,3),20,out var hit),Is.True);Assert.That(hit.point.y,Is.EqualTo(3).Within(.0001f));
            Assert.That(b.Height(new Vector2(0,3),20,out var other),Is.True);Assert.That(other.key,Is.EqualTo(hit.key));
            Assert.That(other.triangle,Is.EqualTo(hit.triangle));Assert.That(other.normal,Is.EqualTo(hit.normal));
        }
        [Test] public void ContactFollowsTwoExistingLedgesWithoutEditingSupport()
        {
            var s=Session();var support=Surface();string hash=support.Revision;var record=Record(s,support);
            Assert.That(support.Revision,Is.EqualTo(hash));Assert.That(record.route.sections.Count,Is.GreaterThan(8));
            Assert.That(record.route.sections.Count(r=>r.kind==MapWaterfallSectionKind.Contact),Is.GreaterThan(4));
            Assert.That(record.route.sections.Any(r=>r.points.Any(p=>p.surfaceKey!=null&&p.surfaceKey.StartsWith("fixture:"))),Is.True);
            float previous=record.route.sections[0].FrontCenter.y;
            foreach(var row in record.route.sections){Assert.That(row.FrontCenter.y,Is.LessThanOrEqualTo(previous+.02f));previous=row.FrontCenter.y;}
            Assert.That(record.route.sections[record.route.sections.Count-1].FrontCenter.y,Is.EqualTo(.218f).Within(.001f));
        }
        [Test] public void MissingSupportProducesBoundedFreeFallAndLanding()
        {
            var record=Record(Session(),Surface(gap:true));
            Assert.That(record.route.sections.Any(r=>r.kind==MapWaterfallSectionKind.FreeFall),Is.True);
            Assert.That(record.route.sections.Last().kind,Is.EqualTo(MapWaterfallSectionKind.Impact));
        }
        [Test] public void ConvexLedgeReleasesWaterAboveExistingContinuousSupport()
        {
            var support=Surface();string before=support.Revision;var route=Record(Session(),support).route;
            Assert.That(route.sections.Any(r=>r.kind==MapWaterfallSectionKind.FreeFall),Is.True);
            Assert.That(support.Revision,Is.EqualTo(before));
            Assert.DoesNotThrow(()=>MapWaterfallRouteBuilder.ValidateClearance(route,support));
            Assert.That(route.sections.Last().FrontCenter.y,Is.EqualTo(.218f).Within(.001f));
        }
        [Test] public void ObstacleAboveWaterIsRejectedBeforeAnySessionEdit()
        {
            var s=Session();string before=s.ExportJson();
            Assert.Throws<InvalidDataException>(()=>Record(s,Surface(obstacle:true)));Assert.That(s.ExportJson(),Is.EqualTo(before));
        }
        [Test] public void SavedRouteIsDeepOwnedAndHistoryRestoresExactGeometry()
        {
            var s=Session();var r=Record(s);string before=s.ExportJson();s.CreateWaterfall(r);string after=s.ExportJson();
            r.route.sections[3].points[0].position+=Vector3.up*2;
            Assert.That(s.ExportJson(),Is.EqualTo(after));s.Undo();Assert.That(s.ExportJson(),Is.EqualTo(before));
            s.Redo();Assert.That(s.ExportJson(),Is.EqualTo(after));
        }
        [Test] public void CompleteChainIsOneUndoAndMalformedChainPreservesPending()
        {
            var template=Session();var waterfall=Record(template);
            var s=new MapEditSession(Map());string before=s.ExportJson();
            s.CreateConnectedWaterChain(template.GetRiver(10),template.GetRiver(20),waterfall);string after=s.ExportJson();
            Assert.That(s.RiverCount,Is.EqualTo(2));Assert.That(s.WaterfallCount,Is.EqualTo(1));
            s.Undo();Assert.That(s.ExportJson(),Is.EqualTo(before));s.Redo();Assert.That(s.ExportJson(),Is.EqualTo(after));
            s.BeginHeightStroke(2,1);s.AppendHeightPoint(new Vector2(35,35));
            Assert.Throws<InvalidDataException>(()=>s.CreateConnectedWaterChain(template.GetRiver(10),template.GetRiver(20),waterfall));
            Assert.That(s.HasPending,Is.True);s.CancelStroke();Assert.That(s.ExportJson(),Is.EqualTo(after));
        }
        [Test] public void V9RoundTripKeepsRouteAnchorsThicknessAndFlowOffset()
        {
            var s=Session();s.CreateWaterfall(Record(s));var save=s.ExportSave();
            Assert.That(save.version,Is.EqualTo(10));Assert.That(save.riverNetwork.version,Is.EqualTo(2));
            var copy=new MapEditSession(Map());copy.LoadJson(s.ExportJson());Assert.That(copy.ExportJson(),Is.EqualTo(s.ExportJson()));
            var upper=new MapRiverPath(copy.GetRiver(10));
            Assert.That(copy.RiverFlowTimeOffset(20),Is.EqualTo(upper.TravelTime(upper.Length)+MapEditSession.WaterfallTravelTime(copy.GetWaterfall(101).route)).Within(.001f));
            Assert.That(copy.GetWaterfall(101).route.supportRevision,Is.EqualTo(s.GetWaterfall(101).route.supportRevision));
        }
        [Test] public void InvalidRouteAndChangedEndpointsPreservePendingAndHistory()
        {
            var s=Session();var r=Record(s);s.CreateWaterfall(r);string before=s.ExportJson();
            s.BeginHeightStroke(2,1);s.AppendHeightPoint(new Vector2(35,35));
            var bad=r.Copy();bad.route.sections[3].points[0].normal=new Vector3(float.NaN,0,0);
            Assert.Throws<InvalidDataException>(()=>s.CreateWaterfall(bad));Assert.That(s.HasPending,Is.True);s.CancelStroke();
            var river=s.GetRiver(20);foreach(var n in river.nodes)n.waterHeight+=1;
            Assert.Throws<InvalidDataException>(()=>s.UpsertRiver(river));Assert.That(s.ExportJson(),Is.EqualTo(before));
        }
        [Test] public void RouteVolumeQueryAndResidentMeshesFollowDeletionAndUndo()
        {
            var s=CreateRoutedSession(out var r);
            var section=r.route.sections[4];var p=section.points[section.points.Count/2];
            Assert.That(s.QueryWaterfallVolume(p.Front(section.thickness)-p.normal*.04f,out long id),Is.True);Assert.That(id,Is.EqualTo(101));
            Assert.That(s.QueryWaterfallVolume(new Vector3(40,20,40),out _),Is.False);
            var profile=ScriptableObject.CreateInstance<MapWaterfallVisualProfile>();
            profile.body=new Material(Shader.Find("Little Castle/Experimental/Waterfall Volume V2"));profile.foam=new Material(profile.body);
            try
            {
                using(var view=new MapWaterfallPresentation(s,profile))
                {
                    view.Tick(new Vector3(0,8,5),true);Assert.That(view.LastError,Is.Null);Assert.That(view.ResidentCount,Is.EqualTo(1));
                    s.DeleteWaterfall(101);Assert.That(view.ResidentCount,Is.Zero);s.Undo();view.Tick(new Vector3(0,8,5),true);Assert.That(view.ResidentCount,Is.EqualTo(1));
                }
            }
            finally{Object.DestroyImmediate(profile.body);Object.DestroyImmediate(profile.foam);Object.DestroyImmediate(profile);}
        }
        [Test] public void ClosedVolumeHasPositiveThicknessAndNoUnmatchedMeshEdges()
        {
            var route=Record(Session()).route;var mesh=MapWaterfallMeshBuilder.Build(route);var impact=MapWaterfallMeshBuilder.Impact(route);
            try
            {
                var edges=new Dictionary<(int,int),int>();var indices=mesh.triangles;
                for(int i=0;i<indices.Length;i+=3)for(int j=0;j<3;j++){int a=indices[i+j],b=indices[i+(j+1)%3];var key=(Mathf.Min(a,b),Mathf.Max(a,b));edges[key]=edges.TryGetValue(key,out int count)?count+1:1;}
                Assert.That(edges.Values.All(v=>v==2),Is.True);Assert.That(indices.Length/3,Is.LessThanOrEqualTo(11200));
                Assert.That(mesh.bounds.size.y,Is.GreaterThan(3));Assert.That(impact.triangles.Length/3,Is.LessThanOrEqualTo(512));
            }
            finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(impact);}
        }
        [Test] public void NativeV8AuthoredGoldenStillLoadsWithFrozenFingerprint()
        {
            string root=Application.dataPath+"/_Game/Tests/Editor/Fixtures/ConnectedWaterM1/";
            var authored=JsonUtility.FromJson<MapAuthoringDocument>(File.ReadAllText(root+"v8-authored-map.json"));
            var session=new MapEditSession(authored);session.LoadJson(File.ReadAllText(root+"v8-authored-save.json"));
            Assert.That(session.WaterfallCount,Is.EqualTo(1));Assert.That(session.GetWaterfall(101).route,Is.Null);
            Assert.That(session.ExportSave().version,Is.EqualTo(10));
        }
        [Test] public void RoutedTerminalsOpenTheirClosingBankAndUndoRestoresIt()
        {
            var s=Session();var point=new Vector2(0,1);
            float closed=s.SampleHeight(point.x,point.y),expected=s.SampleWaterfallSupportHeight(point.x,point.y,10,20);
            Assert.That(closed,Is.GreaterThan(expected+.1f));
            s.CreateWaterfall(Record(s));Assert.That(s.SampleHeight(point.x,point.y),Is.EqualTo(expected).Within(.0001f));
            Assert.That(s.QueryWater(point,out _,out _,out _),Is.False);
            s.Undo();Assert.That(s.SampleHeight(point.x,point.y),Is.EqualTo(closed).Within(.0001f));
            s.Redo();Assert.That(s.SampleHeight(point.x,point.y),Is.EqualTo(expected).Within(.0001f));
        }
        [Test] public void ClearanceUsesTheRenderedDiagonalOnNonCoplanarSupport()
        {
            var support=new MapWaterfallSupport();
            for(int z=0;z<2;z++)
            {
                float h=z==0?0:1;
                var a=new Vector3(0,h,z);var b=new Vector3(1,1-h,z);var c=new Vector3(0,1-h,z+1);var d=new Vector3(1,h,z+1);
                support.Add("saddle:"+z,0,a,c,b);support.Add("saddle:"+z,1,b,c,d);
            }
            var route=new MapWaterfallRouteData{supportRevision=support.Revision};
            for(int x=0;x<=1;x++)
            {
                var section=new MapWaterfallSection();
                for(int z=0;z<=2;z++)section.points.Add(new MapWaterfallContactPoint{position=new Vector3(x,((x+z)%2)+.025f,z),normal=Vector3.up});
                route.sections.Add(section);
            }
            Assert.DoesNotThrow(()=>MapWaterfallRouteBuilder.ValidateClearance(route,support));
        }
        [Test] public void RoutedAuthoredBaseCannotBeErasedWithV8Envelope()
        {
            var s=Session();s.CreateWaterfall(Record(s));var authored=new MapEditSession(s.ExportMap());string before=authored.ExportJson();
            var old=authored.ExportSave();old.version=8;
            Assert.Throws<InvalidDataException>(()=>authored.LoadSave(old));Assert.That(authored.ExportJson(),Is.EqualTo(before));
        }
    }
}
