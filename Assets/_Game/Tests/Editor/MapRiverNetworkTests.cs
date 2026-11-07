using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapRiverNetworkTests
    {
        static MapRiverRecord River(long id, Vector2 start, Vector2 end, float height, float width = 6f)
            => new MapRiverRecord { id = id, nodes = new List<MapRiverNode> {
                new MapRiverNode { id = 1, position = start, waterHeight = height, width = width },
                new MapRiverNode { id = 2, position = end, waterHeight = height, width = width } } };
        static List<MapRiverRecord> Rivers() => new List<MapRiverRecord> {
            River(10, new Vector2(-8,-24), new Vector2(-8,-8), 20),
            River(20, new Vector2(-8,-5), new Vector2(-8,12), 5),
            River(30, new Vector2(-8,16), new Vector2(-8,32), 0) };
        static MapWaterfallRecord Link(long id, long upper, long lower) => new MapWaterfallRecord {
            id = id, upstreamRiverId = upper, upstreamNodeId = 2, downstreamRiverId = lower, downstreamNodeId = 1 };

        [Test] public void LegacyDropMarkersAndProximityNeverCreateLinks()
        {
            var rivers = Rivers(); rivers[0].nodes[1].connection = MapRiverConnection.Drop;
            var graph = new MapRiverNetwork(rivers);
            Assert.That(graph.WaterfallCount, Is.Zero);
            Assert.That(graph.NetworkId(10), Is.Not.EqualTo(graph.NetworkId(20)));
            Assert.That(graph.FindLowerCandidates(10, new Vector2(-8,-5)).Count, Is.EqualTo(1));
            Assert.That(graph.WaterfallCount, Is.Zero);
        }

        [Test] public void MergeSplitAndRestoreKeepIndependentStableIds()
        {
            var graph = new MapRiverNetwork(Rivers());
            var joined = graph.WithWaterfall(Link(101, 10, 20)).WithWaterfall(Link(102, 20, 30));
            Assert.That(graph.WaterfallCount, Is.Zero);
            Assert.That(joined.NetworkId(30), Is.EqualTo(10));
            Assert.That(joined.Snapshot().waterfalls[0].id, Is.EqualTo(101));
            int allocations = 0;
            var split = joined.WithoutWaterfall(101, () => { allocations++; return 999; });
            Assert.That(allocations, Is.EqualTo(1));
            Assert.That(split.NetworkId(10), Is.EqualTo(10));
            Assert.That(split.NetworkId(20), Is.EqualTo(999));
            Assert.That(split.NetworkId(30), Is.EqualTo(999));
            // Session undo/redo can retain these complete snapshots without allocating IDs again.
            var restored = new MapRiverNetwork(Rivers(), joined.Snapshot());
            Assert.That(restored.NetworkId(30), Is.EqualTo(10));
            var redone = new MapRiverNetwork(Rivers(), split.Snapshot());
            Assert.That(redone.NetworkId(30), Is.EqualTo(999));
            Assert.That(restored.WaterfallCount, Is.EqualTo(2));
        }

        [Test] public void RepeatedCreationIsIdempotentButIdRebindingAndOccupiedEndsFail()
        {
            var graph = new MapRiverNetwork(Rivers()).WithWaterfall(Link(101, 10, 20));
            Assert.That(graph.WithWaterfall(Link(101, 10, 20)), Is.SameAs(graph));
            Assert.Throws<InvalidDataException>(() => graph.WithWaterfall(Link(101, 10, 30)));
            Assert.Throws<InvalidDataException>(() => graph.WithWaterfall(Link(103, 10, 30)));
            Assert.That(graph.WaterfallCount, Is.EqualTo(1));
            Assert.That(graph.FindUpperEndpoints(new Vector2(-8,-8)), Is.Empty);
            Assert.That(graph.FindLowerCandidates(30, new Vector2(-8,-5)), Is.Empty);
        }

        [Test] public void CandidateOrderIsIndependentOfLoadOrderAndCrossesNegativeCells()
        {
            var rivers = Rivers(); rivers.Add(River(40, new Vector2(-7,-5), new Vector2(-7,12), 5));
            rivers[1].nodes[0].position = new Vector2(-9,-5); rivers[1].nodes[1].position = new Vector2(-9,12);
            var a = new MapRiverNetwork(rivers); rivers.Reverse(); var b = new MapRiverNetwork(rivers);
            var first = a.FindLowerCandidates(10, new Vector2(-8,-5)); var second = b.FindLowerCandidates(10, new Vector2(-8,-5));
            Assert.That(first.Count, Is.EqualTo(2));
            Assert.That(first[0].lower.riverId, Is.EqualTo(20));
            Assert.That(second[0].lower.riverId, Is.EqualTo(20));
            Assert.That(first[1].lower.riverId, Is.EqualTo(second[1].lower.riverId));
            Assert.That(first[0].squaredDistance, Is.EqualTo(1));
        }

        [Test] public void SnapRadiusUsesXZAndCanBeZero()
        {
            var settings = new MapWaterfallSnapSettings { radius = 0 };
            var graph = new MapRiverNetwork(Rivers(), settings: settings);
            Assert.That(graph.FindLowerCandidates(10, new Vector2(-8,-5)).Count, Is.EqualTo(1));
            Assert.That(graph.FindLowerCandidates(10, new Vector2(-8.01f,-5)), Is.Empty);
            settings.radius = 8; // The snapshot owns its settings.
            Assert.That(graph.FindLowerCandidates(10, new Vector2(-8.01f,-5)), Is.Empty);
        }

        [TestCase(.249f, MapWaterfallRejection.InvalidDrop)]
        [TestCase(.25f, MapWaterfallRejection.None)]
        [TestCase(200f, MapWaterfallRejection.None)]
        [TestCase(201f, MapWaterfallRejection.InvalidDrop)]
        [TestCase(-1f, MapWaterfallRejection.InvalidDrop)]
        public void DropLimitsAreSeparateFromSnapDistance(float drop, MapWaterfallRejection expected)
        {
            var rivers = Rivers(); foreach (var n in rivers[1].nodes) n.waterHeight = 20-drop;
            var graph = new MapRiverNetwork(rivers);
            Assert.That(graph.ValidateConnection(10,2,20,1), Is.EqualTo(expected));
        }

        [Test] public void VerticalCascadeDoesNotNormalizeZeroAndReversedFlowIsRejected()
        {
            var rivers = Rivers(); foreach (var n in rivers[1].nodes) n.position -= new Vector2(0,3);
            var graph = new MapRiverNetwork(rivers);
            Assert.That(graph.ValidateConnection(10,2,20,1), Is.EqualTo(MapWaterfallRejection.None));
            rivers[1].nodes[1].position = new Vector2(-8,-25);
            graph = new MapRiverNetwork(rivers);
            Assert.That(graph.ValidateConnection(10,2,20,1), Is.EqualTo(MapWaterfallRejection.OpposedFlow));
        }

        [Test] public void BackwardsSpanAndWrongNodeAndExcessiveWidthAreRejected()
        {
            var rivers = Rivers(); foreach (var n in rivers[1].nodes) n.position -= new Vector2(0,10);
            var graph = new MapRiverNetwork(rivers);
            Assert.That(graph.ValidateConnection(10,2,20,1), Is.EqualTo(MapWaterfallRejection.BackwardsSpan));
            Assert.That(graph.ValidateConnection(10,1,20,1), Is.EqualTo(MapWaterfallRejection.MissingEndpoint));
            Assert.That(graph.ValidateConnection(10,2,10,1), Is.EqualTo(MapWaterfallRejection.SameRiver));
            rivers = Rivers(); foreach (var n in rivers[1].nodes) n.width = 12.01f;
            graph = new MapRiverNetwork(rivers);
            Assert.That(graph.ValidateConnection(10,2,20,1), Is.EqualTo(MapWaterfallRejection.IncompatibleWidth));
            foreach (var n in rivers[1].nodes) n.width = 12;
            Assert.That(new MapRiverNetwork(rivers).ValidateConnection(10,2,20,1), Is.EqualTo(MapWaterfallRejection.None));
        }

        [Test] public void GraphAndSnapshotsOwnTheirInputsAndBadSplitIsAtomic()
        {
            var rivers = Rivers(); var graph = new MapRiverNetwork(rivers).WithWaterfall(Link(101,10,20));
            rivers[0].nodes[1].position = Vector2.one * 80;
            Assert.That(graph.Endpoint(10,false).position, Is.EqualTo(new Vector2(-8,-8)));
            var snapshot = graph.Snapshot(); snapshot.waterfalls[0].upstreamRiverId = 30; snapshot.members.Clear();
            Assert.That(graph.NetworkId(20), Is.EqualTo(10));
            Assert.Throws<InvalidDataException>(() => graph.WithoutWaterfall(101, () => 10));
            Assert.That(graph.WaterfallCount, Is.EqualTo(1));
            Assert.That(graph.NetworkId(20), Is.EqualTo(10));
        }

        [Test] public void DanglingDuplicateAndContradictoryMembershipAreRejectedBeforeReplacement()
        {
            var graph = new MapRiverNetwork(Rivers()).WithWaterfall(Link(101,10,20));
            var data = graph.Snapshot(); data.waterfalls[0].downstreamNodeId = 999;
            Assert.Throws<InvalidDataException>(() => new MapRiverNetwork(Rivers(), data));
            data = graph.Snapshot(); data.waterfalls.Add(data.waterfalls[0]);
            Assert.Throws<InvalidDataException>(() => new MapRiverNetwork(Rivers(), data));
            data = graph.Snapshot(); data.members[1].networkId = 777;
            Assert.Throws<InvalidDataException>(() => new MapRiverNetwork(Rivers(), data));
            data = graph.Snapshot(); data.members[2].networkId = 10;
            Assert.Throws<InvalidDataException>(() => new MapRiverNetwork(Rivers(), data));
            Assert.That(graph.WaterfallCount, Is.EqualTo(1));
        }

        [Test] public void InvalidSettingsAndNonfinitePointerFailWithoutSearchingCells()
        {
            Assert.Throws<InvalidDataException>(() => new MapRiverNetwork(Rivers(), settings: new MapWaterfallSnapSettings { radius = float.NaN }));
            Assert.Throws<InvalidDataException>(() => new MapRiverNetwork(Rivers(), settings: new MapWaterfallSnapSettings { radius = 8.01f }));
            var graph = new MapRiverNetwork(Rivers());
            Assert.Throws<ArgumentOutOfRangeException>(() => graph.FindUpperEndpoints(new Vector2(float.PositiveInfinity,0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => graph.FindLowerCandidates(10, new Vector2(float.NaN,0)));
        }

        [Test] public void DraftCancelAndStalePreviewCannotCommitAnotherNetwork()
        {
            var graph = new MapRiverNetwork(Rivers()); var draft = new MapWaterfallDraft();
            draft.Begin(graph,10); draft.Preview(graph,new Vector2(-8,-5));
            Assert.That(draft.Candidates.Count, Is.EqualTo(1));
            Assert.That(graph.WaterfallCount, Is.Zero);
            var next = draft.PrepareCommit(graph,0,101);
            Assert.That(next.WaterfallCount, Is.EqualTo(1));
            Assert.That(graph.WaterfallCount, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => draft.PrepareCommit(next,0,102));
            draft.Cancel();
            Assert.That(draft.HasUpper, Is.False);
            Assert.That(draft.Candidates, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => draft.PrepareCommit(graph,0,103));
        }

        [Test] public void DraftKeepsAmbiguousChoicesExplicitAndRequiresOpenUpper()
        {
            var rivers = Rivers(); rivers.Add(River(40,new Vector2(-7,-5),new Vector2(-7,12),5));
            var graph = new MapRiverNetwork(rivers); var draft = new MapWaterfallDraft();
            draft.Begin(graph,10); draft.Preview(graph,new Vector2(-8,-5));
            Assert.That(draft.IsAmbiguous, Is.True);
            var chosen = draft.PrepareCommit(graph,1,101);
            Assert.That(chosen.Snapshot().waterfalls[0].downstreamRiverId, Is.EqualTo(40));
            Assert.Throws<InvalidOperationException>(() => draft.Begin(chosen,10));
            Assert.That(chosen.NetworkId(20), Is.Not.EqualTo(chosen.NetworkId(40)));
        }

        [Test] public void UnrelatedNetworkIdentitySurvivesSplitAndInvalidAllocatedId()
        {
            var graph = new MapRiverNetwork(Rivers()).WithWaterfall(Link(101,10,20));
            Assert.Throws<InvalidDataException>(() => graph.WithoutWaterfall(101,() => 30));
            var split = graph.WithoutWaterfall(101,() => 999);
            Assert.That(split.NetworkId(30), Is.EqualTo(30));
            Assert.That(split.WithoutWaterfall(9999,null), Is.SameAs(split));
        }

        [Test] public void MaximumSpanAndCrossingDirectionsDoNotCreateAutomaticLinks()
        {
            var rivers = Rivers(); foreach (var n in rivers[1].nodes) n.position += new Vector2(0,200);
            var graph = new MapRiverNetwork(rivers);
            Assert.That(graph.ValidateConnection(10,2,20,1), Is.EqualTo(MapWaterfallRejection.TooFar));
            Assert.That(graph.FindLowerCandidates(10,rivers[1].nodes[0].position), Is.Empty);
            Assert.That(graph.WaterfallCount, Is.Zero);
        }
    }
}
