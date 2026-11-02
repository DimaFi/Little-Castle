using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed partial class MapEditSession
    {
        MapRiverNetwork riverNetwork, authoredRiverNetwork;
        public MapRiverNetwork RiverNetwork => riverNetwork;
        public int WaterfallCount => riverNetwork.WaterfallCount;
        void InitializeRiverNetwork()
        {
            var data = authored.riverNetwork;
            // Legacy authoring assets have no graph. Unity can materialize that
            // optional inline field as an empty object during JSON cloning.
            // Only the authoring base migrates this way; v8 saves stay strict.
            if (data != null && (data.version == 1 || data.version == 2) && data.members != null && data.members.Count == 0
                && data.waterfalls != null && data.waterfalls.Count == 0) data = null;
            riverNetwork = new MapRiverNetwork(ExportRivers(), data);
            ValidateRiverNetworkPlacement(riverNetwork);
            authoredRiverNetwork = riverNetwork;
        }
        public MapRiverNetworkData ExportRiverNetwork() => riverNetwork.Snapshot();
        public bool HasWaterfall(long id) => ExportRiverNetwork().waterfalls.Exists(w => w.id == id);
        public MapWaterfallRecord GetWaterfall(long id) => ExportRiverNetwork().waterfalls.Find(w => w.id == id)
            ?? throw new InvalidOperationException("Водопад не найден.");
        public List<MapWaterfallRecord> QueryWaterfalls(Rect area)
        {
            var result = new List<MapWaterfallRecord>();
            foreach (var record in ExportRiverNetwork().waterfalls) if (WaterfallBounds(record).Overlaps(area)) result.Add(record);
            return result;
        }
        public Rect WaterfallBounds(MapWaterfallRecord record)
            => WaterfallBounds(record, riverNetwork);
        static Rect WaterfallBounds(MapWaterfallRecord record, MapRiverNetwork graph)
        {
            if (record.route != null && !record.route.IsEmpty)
            {
                var bounds = record.route.Bounds;
                var lowerPoint = graph.Endpoint(record.downstreamRiverId, true); float impact = lowerPoint.width * 1.5f;
                bounds.Encapsulate(new Vector3(lowerPoint.position.x - impact, lowerPoint.waterHeight, lowerPoint.position.y - impact));
                bounds.Encapsulate(new Vector3(lowerPoint.position.x + impact, lowerPoint.waterHeight, lowerPoint.position.y + impact));
                return Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
            }
            var upper = graph.Endpoint(record.upstreamRiverId, false); var lower = graph.Endpoint(record.downstreamRiverId, true);
            float pad = Mathf.Max(upper.width, lower.width) * .6f + 2;
            return Rect.MinMaxRect(Mathf.Min(upper.position.x, lower.position.x) - pad, Mathf.Min(upper.position.y, lower.position.y) - pad,
                Mathf.Max(upper.position.x, lower.position.x) + pad, Mathf.Max(upper.position.y, lower.position.y) + pad);
        }
        public void CreateWaterfall(MapWaterfallRecord record, MapRiverNetwork expectedPreview = null)
        {
            RequireRiverAccess();
            if (expectedPreview != null && !ReferenceEquals(expectedPreview, riverNetwork))
                throw new InvalidOperationException("Сеть изменилась. Обновите предпросмотр.");
            var next = riverNetwork.WithWaterfall(record);
            if (ReferenceEquals(next, riverNetwork)) return;
            var area = WaterfallBounds(record, next); ValidateRiverNetworkPlacement(next);
            if (HasWaterfall(record.id)) area = Union(area, WaterfallBounds(GetWaterfall(record.id)));
            CommitStroke();
            var command = new Command { networkBefore = riverNetwork, networkAfter = next, bounds = area, kind = RiverKinds };
            Execute(command);
        }
        /// <summary>Add a complete new chain as one validated, reversible authoring command.</summary>
        public void CreateConnectedWaterChain(MapRiverRecord upper, MapRiverRecord lower, MapWaterfallRecord waterfall)
        {
            RequireRiverAccess();
            if (upper == null || lower == null || waterfall == null || upper.id == lower.id
                || rivers.ContainsKey(upper.id) || rivers.ContainsKey(lower.id)
                || waterfall.upstreamRiverId != upper.id || waterfall.downstreamRiverId != lower.id)
                throw new InvalidDataException("Новая цепочка должна содержать две независимые реки и соединяющий их водопад.");
            var a = Clone(upper); var b = Clone(lower); var proposed = ExportRivers(); proposed.Add(a); proposed.Add(b);
            ValidateRiverState(proposed, ExportRockSections());
            var next = riverNetwork.WithRivers(proposed, false, MapObjectIds.New).WithWaterfall(waterfall);
            ValidateRiverNetworkPlacement(next);
            Rect area = Union(new MapRiverPath(a).bounds, new MapRiverPath(b).bounds);
            area = Union(area, WaterfallBounds(waterfall, next));
            var command = new Command { bounds = Pad(area, HeightSpacing * 2), kind = RiverKinds };
            command.rivers.Add(new RiverChange { after = a }); command.rivers.Add(new RiverChange { after = b });
            AttachRiverNetwork(command, next);
            CommitStroke(); Execute(command);
        }
        public void DeleteWaterfall(long id)
        {
            RequireRiverAccess(); if (!HasWaterfall(id)) return;
            var area = WaterfallBounds(GetWaterfall(id));
            var next = riverNetwork.WithoutWaterfall(id, MapObjectIds.New);
            CommitStroke(); Execute(new Command { networkBefore = riverNetwork, networkAfter = next, bounds = area, kind = RiverKinds });
        }
        void ValidateWaterfallArea(Rect area, MapWaterfallRecord record)
        {
            var upper = riverNetwork.Endpoint(record.upstreamRiverId, false); var lower = riverNetwork.Endpoint(record.downstreamRiverId, true);
            ValidatePoint(upper.position); ValidatePoint(lower.position);
            foreach (var protectedArea in heightField.Definition.protectedAreas)
                if (protectedArea.Overlaps(area)) throw new InvalidDataException("Водопад задевает защищённый рельеф.");
        }
        void ValidateRiverNetworkPlacement(MapRiverNetwork graph)
        {
            foreach (var record in graph.Snapshot().waterfalls)
            {
                var upper = graph.Endpoint(record.upstreamRiverId, false); var lower = graph.Endpoint(record.downstreamRiverId, true);
                ValidatePoint(upper.position); ValidatePoint(lower.position);
                var area = WaterfallBounds(record, graph);
                if (record.route != null && !record.route.IsEmpty)
                    foreach (var section in record.route.sections) foreach (var point in section.points)
                    {
                        var front = point.Front(section.thickness);
                        ValidatePoint(new Vector2(front.x, front.z));
                        if (front.y < MinimumHeight || front.y > MaximumHeight + .025f)
                            throw new InvalidDataException("Маршрут выходит за высоты карты.");
                    }
                foreach (var protectedArea in heightField.Definition.protectedAreas)
                    if (protectedArea.Overlaps(area)) throw new InvalidDataException("Водопад задевает защищённый рельеф.");
            }
        }
        void AttachRiverNetwork(Command command, MapRiverNetwork next)
        { command.networkBefore = riverNetwork; command.networkAfter = next; }
        static Rect ConnectedWaterfallBounds(long riverId, MapRiverNetwork graph)
        {
            Rect area = default;
            foreach (var record in graph.Snapshot().waterfalls)
                if (record.upstreamRiverId == riverId || record.downstreamRiverId == riverId) area = Union(area, WaterfallBounds(record, graph));
            return area;
        }
        bool HasRiverNetworkEdits => Fingerprint(riverNetwork.Snapshot()) != Fingerprint(authoredRiverNetwork.Snapshot());
        bool HasNonLegacyAuthoredNetwork => authoredRiverNetwork.WaterfallCount > 0
            || authoredRiverNetwork.Snapshot().members.Exists(m => m.networkId != m.riverId);
        void AddRiverNetworkReset(Command command)
        {
            if (!HasRiverNetworkEdits && command.rivers.Count == 0) return;
            AttachRiverNetwork(command, authoredRiverNetwork); command.bounds = Union(command.bounds, AllRiverBounds()); command.kind |= RiverKinds;
        }
        // Frozen v8 schema. Never serialize expanded route-bearing DTOs for its fingerprint.
        [Serializable] sealed class LegacyWaterfall { public long id, upstreamRiverId, upstreamNodeId, downstreamRiverId, downstreamNodeId; }
        [Serializable] sealed class LegacyNetwork
        {
            public int version = 1;
            public List<LegacyWaterfall> waterfalls = new List<LegacyWaterfall>();
            public List<MapRiverNetworkMember> members;
        }
        [Serializable] sealed class NetworkBase { public string fenceFingerprint; public LegacyNetwork network; }
        [Serializable] sealed class RoutedNetworkBase { public string previousFingerprint; public MapRiverNetworkData network; }
        LegacyNetwork LegacyNetworkSnapshot()
        {
            var snapshot = authoredRiverNetwork.Snapshot(); var data = new LegacyNetwork { members = snapshot.members };
            foreach (var w in snapshot.waterfalls) data.waterfalls.Add(new LegacyWaterfall { id = w.id, upstreamRiverId = w.upstreamRiverId,
                upstreamNodeId = w.upstreamNodeId, downstreamRiverId = w.downstreamRiverId, downstreamNodeId = w.downstreamNodeId });
            return data;
        }
        // A canonical isolated graph adds no authored information to a v7 base.
        // Unity may materialize an empty inline class while cloning/serializing it.
        string RiverNetworkBaseFingerprint() => !HasNonLegacyAuthoredNetwork ? FenceBaseFingerprint()
            : Fingerprint(new NetworkBase { fenceFingerprint = FenceBaseFingerprint(), network = LegacyNetworkSnapshot() });
        bool HasAuthoredWaterfallRoutes => authoredRiverNetwork.Snapshot().waterfalls.Exists(w => w.route != null && !w.route.IsEmpty);
        string WaterfallRouteBaseFingerprint() => !HasAuthoredWaterfallRoutes ? RiverNetworkBaseFingerprint()
            : Fingerprint(new RoutedNetworkBase { previousFingerprint = RiverNetworkBaseFingerprint(), network = authoredRiverNetwork.Snapshot() });
        [Serializable] sealed class SoftRiverBase { public string previousFingerprint; public List<MapRiverLegacyV10> rivers; }
        string SoftRiverBaseFingerprint()=>!authored.rivers.Exists(r=>r.layoutVersion==3)?WaterfallRouteBaseFingerprint():
            Fingerprint(new SoftRiverBase{previousFingerprint=WaterfallRouteBaseFingerprint(),rivers=MapRiverLegacyV10.Copy(authored.rivers)});
        [Serializable] sealed class SplineShapeBase {public string previousFingerprint;public List<MapRiverShape> shapes;}
        bool HasSplineAuthoring=>authored.rivers.Exists(MapRiverSpline.HasShape)||ExportRivers().Exists(MapRiverSpline.HasShape);
        string SplineRiverBaseFingerprint()
        {
            if(!authored.rivers.Exists(MapRiverSpline.HasShape))return SoftRiverBaseFingerprint();
            var shapes=new List<MapRiverShape>();foreach(var river in authored.rivers)shapes.Add(river.shape);
            return Fingerprint(new SplineShapeBase{previousFingerprint=SoftRiverBaseFingerprint(),shapes=shapes});
        }
        static MapRiverNetworkData LegacyLoadedNetwork(MapRiverNetworkData data)
        {
            if (data == null || data.version != 1) throw new InvalidDataException("Неверная сеть в сохранении v8.");
            var copy = JsonUtility.FromJson<MapRiverNetworkData>(JsonUtility.ToJson(data));
            if (copy.waterfalls != null) foreach (var w in copy.waterfalls) if (w != null) w.route = null;
            return copy;
        }
        public bool QueryWaterfallVolume(Vector3 point, out long waterfallId, float padding = 0)
        {
            if (!MapWaterfallRouteData.Finite(point) || !float.IsFinite(padding) || padding < 0 || padding > 8)
                throw new ArgumentOutOfRangeException(nameof(point));
            return riverNetwork.QueryWaterfallVolume(point, out waterfallId, padding);
        }
        public bool IsRoutedRiver(long riverId) => riverNetwork.HasRoutedRiver(riverId);
        long flowOffsetRevision = -1;
        readonly Dictionary<long, float> flowOffsets = new Dictionary<long, float>();
        public float RiverFlowTimeOffset(long riverId)
        {
            if (flowOffsetRevision != RiverRevision) { flowOffsets.Clear(); flowOffsetRevision = RiverRevision; }
            if (flowOffsets.TryGetValue(riverId, out var cached)) return cached;
            var chain = new List<MapWaterfallRecord>(); long current = riverId;
            var links = ExportRiverNetwork().waterfalls;
            for (int i = 0; i < links.Count; i++)
            {
                var parent = links.Find(w => w.downstreamRiverId == current && w.route != null && !w.route.IsEmpty);
                if (parent == null) break; chain.Add(parent); current = parent.upstreamRiverId;
            }
            float offset = 0;
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                var w = chain[i]; var path = rivers[w.upstreamRiverId];
                offset += path.TravelTime(path.Length) + WaterfallTravelTime(w.route);
            }
            flowOffsets[riverId] = offset; return offset;
        }
        public static float WaterfallTravelTime(MapWaterfallRouteData route)
        {
            float value = 0;
            for (int i = 1; i < route.sections.Count; i++) value += Vector3.Distance(route.sections[i - 1].FrontCenter, route.sections[i].FrontCenter)
                * .5f * (1 / route.sections[i - 1].speed + 1 / route.sections[i].speed);
            return value;
        }
    }
}
