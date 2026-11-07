using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapRiverNetworkSessionTests
    {
        static MapAuthoringDocument Map() => new MapAuthoringDocument { mapId = "network-session", bounds = new Vector4(-128,-128,256,256),
            heightField = new MapHeightFieldData { minimumHeight=-8, maximumHeight=32 }, trees = new List<MapTreeInstance> {
                new MapTreeInstance { stableId = 777, archetypeId = "oak.a", position = new Vector2(80,80) } },
            fences = new List<MapFenceRecord> { new MapFenceRecord { id = 999, variantId = "fence.wood01",
                points = new List<Vector2> { new Vector2(-70,-70), new Vector2(-20,-70) } } } };
        static MapRiverRecord River(long id, float start, float end, float height) => new MapRiverRecord { id = id,
            nodes = new List<MapRiverNode> { new MapRiverNode { id = 1, position = new Vector2(-8,start), waterHeight = height, width = 6 },
                new MapRiverNode { id = 2, position = new Vector2(-8,end), waterHeight = height, width = 6 } } };
        static MapEditSession Session(MapAuthoringDocument map = null)
        {
            var session = new MapEditSession(map ?? Map());
            session.UpsertRiver(River(10,-65,-35,20)); session.UpsertRiver(River(20,-5,25,5));
            return session;
        }
        static MapWaterfallRecord Link(long id = 101, long upper = 10, long lower = 20) => new MapWaterfallRecord {
            id = id, upstreamRiverId = upper, upstreamNodeId = 2, downstreamRiverId = lower, downstreamNodeId = 1 };

        [Test] public void ConnectionUsesSharedUndoRedoAndIndependentIds()
        {
            var s = Session(); string before = s.ExportJson(); long revision = s.RiverRevision;
            var preview = s.RiverNetwork; s.CreateWaterfall(Link(),preview);
            Assert.That(s.WaterfallCount,Is.EqualTo(1)); Assert.That(s.RiverCount,Is.EqualTo(2));
            Assert.That(s.RiverNetwork.NetworkId(20),Is.EqualTo(10)); Assert.That(s.GetRiver(20).id,Is.EqualTo(20));
            Assert.That(s.GetWaterfall(101).id,Is.EqualTo(101)); Assert.That(s.RiverRevision,Is.EqualTo(revision+1));
            string after = s.ExportJson(); s.CreateWaterfall(Link()); Assert.That(s.RiverRevision,Is.EqualTo(revision+1));
            s.Undo(); Assert.That(s.ExportJson(),Is.EqualTo(before)); s.Redo(); Assert.That(s.ExportJson(),Is.EqualTo(after));
            Assert.Throws<InvalidOperationException>(() => s.CreateWaterfall(Link(102),preview));
        }

        [Test] public void DeleteConnectionKeepsAllocatedSplitIdentityAcrossRedo()
        {
            var s=Session(); s.CreateWaterfall(Link()); string joined=s.ExportJson();
            s.DeleteWaterfall(101); long split=s.RiverNetwork.NetworkId(20); string separated=s.ExportJson();
            Assert.That(split,Is.Not.EqualTo(10)); Assert.That(split,Is.Not.EqualTo(0)); Assert.That(s.RiverNetwork.NetworkId(10),Is.EqualTo(10));
            s.Undo(); Assert.That(s.ExportJson(),Is.EqualTo(joined)); s.Redo(); Assert.That(s.ExportJson(),Is.EqualTo(separated));
            Assert.That(s.RiverNetwork.NetworkId(20),Is.EqualTo(split));
        }

        [Test] public void DeleteSectionRequiresExplicitCascadeAndRestoresTwoLinksInOneUndo()
        {
            var s=Session(); s.UpsertRiver(River(30,55,85,0)); s.CreateWaterfall(Link()); s.CreateWaterfall(Link(102,20,30));
            string before=s.ExportJson(); Assert.Throws<InvalidDataException>(()=>s.DeleteRiver(20)); Assert.That(s.ExportJson(),Is.EqualTo(before));
            s.DeleteRiver(20,true); Assert.That(s.RiverCount,Is.EqualTo(2)); Assert.That(s.WaterfallCount,Is.Zero);
            long split=s.RiverNetwork.NetworkId(30); string after=s.ExportJson();
            s.Undo(); Assert.That(s.ExportJson(),Is.EqualTo(before)); s.Redo(); Assert.That(s.ExportJson(),Is.EqualTo(after));
            Assert.That(s.RiverNetwork.NetworkId(30),Is.EqualTo(split));
        }

        [Test] public void InvalidConnectedRiverEditCannotPartiallyReplaceStateOrHistory()
        {
            var s=Session(); s.CreateWaterfall(Link()); string before=s.ExportJson(); long revision=s.RiverRevision;
            var river=s.GetRiver(20); foreach(var n in river.nodes)n.waterHeight=21;
            Assert.Throws<InvalidDataException>(()=>s.UpsertRiver(river)); Assert.That(s.ExportJson(),Is.EqualTo(before));
            Assert.That(s.RiverRevision,Is.EqualTo(revision)); s.Undo(); Assert.That(s.WaterfallCount,Is.Zero);
        }

        [Test] public void PermissionFailurePreservesPendingTerrainAndGraphHistory()
        {
            var s=Session(); s.Access=MapEditAccess.Terrain; s.BeginHeightStroke(4,1); s.AppendHeightPoint(new Vector2(80,80));
            Assert.Throws<UnauthorizedAccessException>(()=>s.CreateWaterfall(Link())); Assert.That(s.HasPending,Is.True);
            Assert.That(s.WaterfallCount,Is.Zero); s.CancelStroke(); s.Access=MapEditAccess.All; s.CreateWaterfall(Link());
            s.Access=MapEditAccess.Player; Assert.Throws<UnauthorizedAccessException>(()=>s.DeleteWaterfall(101));
            Assert.Throws<UnauthorizedAccessException>(()=>s.Undo()); Assert.That(s.WaterfallCount,Is.EqualTo(1));
        }

        [Test] public void V9RoundTripPreservesRoadsTreesFencesEnvironmentAndLinkIdentity()
        {
            var s=Session(); s.AddStroke(new SurfacePaintStroke { id="network-road", road=true, radius=2,
                points=new List<Vector2> { new Vector2(75,-60),new Vector2(75,60) } });
            s.CreateWaterfall(Link()); var save=s.ExportSave(); Assert.That(save.version,Is.EqualTo(10));
            string json=s.ExportJson(); var fresh=Session(); fresh.LoadJson(json); Assert.That(fresh.ExportJson(),Is.EqualTo(json));
            Assert.That(fresh.TreeCount,Is.EqualTo(1)); Assert.That(fresh.FenceCount,Is.EqualTo(1));
            Assert.That(fresh.ExportEdits().strokes[0].id,Is.EqualTo("network-road"));
            Assert.That(JsonUtility.ToJson(fresh.ExportEnvironment()),Is.EqualTo(JsonUtility.ToJson(s.ExportEnvironment())));
            Assert.That(fresh.GetWaterfall(101).downstreamNodeId,Is.EqualTo(1));
        }

        [Test] public void ExportMapAndSurfaceAssetRetainAuthoredNetworkAndReset()
        {
            var s=Session(); s.CreateWaterfall(Link()); var map=s.ExportMap(); var copy=new MapEditSession(map);
            Assert.That(copy.WaterfallCount,Is.EqualTo(1)); copy.DeleteWaterfall(101); copy.ClearEdits(); Assert.That(copy.WaterfallCount,Is.EqualTo(1));
            copy.Undo(); Assert.That(copy.WaterfallCount,Is.Zero);
            var data=ScriptableObject.CreateInstance<SurfacePaintData>();
            try
            {
                data.mapId=map.mapId;data.bounds=map.bounds;data.revision=map.revision;data.authored=map.surface;
                data.hasHeightBase=true;data.heightField=map.heightField;data.hasTreeBase=true;data.treeInstances=map.trees;
                data.rivers=map.rivers;data.riverNetwork=map.riverNetwork;data.rockSections=map.rockSections;data.fences=map.fences;
                var restored=new MapEditSession(MapEditSession.FromSurface(data)); Assert.That(restored.WaterfallCount,Is.EqualTo(1));
                Assert.That(restored.RiverNetwork.NetworkId(20),Is.EqualTo(10));
            }
            finally { UnityEngine.Object.DestroyImmediate(data); }
        }

        [Test] public void MalformedV8LoadIsAtomicIncludingPendingTerrain()
        {
            var s=Session(); s.CreateWaterfall(Link()); var bad=s.ExportSave(); string before=s.ExportJson();
            bad.riverNetwork.waterfalls[0].downstreamNodeId=999;
            s.BeginHeightStroke(4,1);s.AppendHeightPoint(new Vector2(80,80));
            Assert.Throws<InvalidDataException>(()=>s.LoadSave(bad)); Assert.That(s.HasPending,Is.True); s.CancelStroke();
            Assert.That(s.ExportJson(),Is.EqualTo(before)); Assert.That(s.CanUndo,Is.True);
            bad=s.ExportSave();bad.riverNetwork=null; Assert.Throws<InvalidDataException>(()=>s.LoadSave(bad));
            bad=s.ExportSave();bad.riverNetwork.members.Clear();bad.riverNetwork.waterfalls.Clear();
            Assert.Throws<InvalidDataException>(()=>s.LoadSave(bad));
            Assert.That(s.ExportJson(),Is.EqualTo(before));
        }

        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void HistoricalReadersKeepTheirFingerprintAndDoNotInferLinks(int version)
        {
            var map=Map(); var s=Session(map); var save=s.ExportSave();
            string[] methods={"LegacyFingerprint","LegacyHeightFingerprint","LegacyTreeFingerprint","LegacyFeatureFingerprint","EnvironmentBaseFingerprint","FenceBaseFingerprint"};
            save.version=version;save.baseFingerprint=(string)typeof(MapEditSession).GetMethod(methods[version-2],BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);
            save.rivers[0].nodes[1].connection=MapRiverConnection.Drop;
            // Deliberately present a new field in an old-version envelope: it must not invent a v7 link.
            save.riverNetwork=new MapRiverNetwork(save.rivers).WithWaterfall(Link()).Snapshot();
            var restored=new MapEditSession(map);restored.LoadSave(save); Assert.That(restored.WaterfallCount,Is.Zero);
            Assert.That(restored.RiverCount,Is.EqualTo(version>=5?2:0));
        }

        [Test] public void NetworkBaseFingerprintRejectsAnotherAuthoredLinkBeforeReplacement()
        {
            var s=Session();s.CreateWaterfall(Link());var map=s.ExportMap();var first=new MapEditSession(map);var save=first.ExportSave();
            map.riverNetwork.waterfalls[0].id=888;var second=new MapEditSession(map);
            Assert.Throws<InvalidDataException>(()=>second.LoadSave(save));Assert.That(second.HasWaterfall(888),Is.True);
            save=second.ExportSave();save.version=7;
            save.baseFingerprint=(string)typeof(MapEditSession).GetMethod("FenceBaseFingerprint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(second,null);
            Assert.Throws<InvalidDataException>(()=>second.LoadSave(save));Assert.That(second.HasWaterfall(888),Is.True);
        }

        [Test] public void ProtectedGapCannotBeBypassedByLoadingNetworkJson()
        {
            var map=Map();map.heightField.protectedAreas.Add(new Rect(-12,-22,8,2));var s=Session(map);string before=s.ExportJson();
            Assert.Throws<InvalidDataException>(()=>s.CreateWaterfall(Link()));
            var save=s.ExportSave();save.riverNetwork=s.RiverNetwork.WithWaterfall(Link()).Snapshot();
            Assert.Throws<InvalidDataException>(()=>s.LoadSave(save));Assert.That(s.ExportJson(),Is.EqualTo(before));
        }

        [Test] public void ConnectionInvalidatesBoundedRegionAndResetIsOneSharedCommand()
        {
            var s=Session();MapRegionChange last=default;int notifications=0; s.Changed+=c=>{last=c;notifications++;};
            s.SampleHeight(-8,-35);long heightRevision=s.HeightRevision; s.CreateWaterfall(Link());
            Assert.That(notifications,Is.EqualTo(1));Assert.That((last.kind&MapChangeKind.Rivers)!=0,Is.True);
            Assert.That(last.bounds.Contains(new Vector2(-8,-20)),Is.True);Assert.That(last.bounds.height,Is.LessThan(80));
            Assert.That(s.HeightRevision,Is.EqualTo(heightRevision+1)); string before=s.ExportJson();
            s.ClearEdits();Assert.That(s.WaterfallCount,Is.Zero);Assert.That(s.RiverCount,Is.Zero);
            s.Undo();Assert.That(s.ExportJson(),Is.EqualTo(before));Assert.That(s.FenceCount,Is.EqualTo(1));
        }

        [Test] public void EmptyCanonicalGraphRetainsTheHistoricalV7BaseIdentity()
        {
            var s=new MapEditSession(Map());
            string previous=(string)typeof(MapEditSession).GetMethod("FenceBaseFingerprint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);
            Assert.That(s.ExportSave().baseFingerprint,Is.EqualTo(previous));
            var map=Map();map.rivers.Add(River(10,-65,-35,20));map.rivers.Add(River(20,-5,25,5));
            var isolated=new MapEditSession(map);Assert.That(isolated.WaterfallCount,Is.Zero);
            previous=(string)typeof(MapEditSession).GetMethod("FenceBaseFingerprint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(isolated,null);
            Assert.That(isolated.ExportSave().baseFingerprint,Is.EqualTo(previous));
        }
    }
}
