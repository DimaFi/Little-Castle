using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable] public sealed class MapWaterfallRecord
    {
        public long id, upstreamRiverId, upstreamNodeId, downstreamRiverId, downstreamNodeId;
        public MapWaterfallRouteData route;
        public MapWaterfallRecord Copy()
        {
            var copy = (MapWaterfallRecord)MemberwiseClone();
            copy.route = route == null || route.IsEmpty ? null : route.Copy(); return copy;
        }
    }

    [Serializable] public sealed class MapRiverNetworkMember
    {
        public long riverId, networkId;
    }

    /// <summary>Independent IDs for river sections and falls. No implicit links from proximity or Drop markers.</summary>
    [Serializable] public sealed class MapRiverNetworkData
    {
        public int version = 2;
        public List<MapWaterfallRecord> waterfalls = new List<MapWaterfallRecord>();
        public List<MapRiverNetworkMember> members = new List<MapRiverNetworkMember>();
    }

    [Serializable] public sealed class MapWaterfallSnapSettings
    {
        public float radius = 3f, minimumDrop = .25f, maximumDrop = 200f, maximumSpan = 128f;
        public float maximumWidthRatio = 2f, minimumDirectionDot = 0f, backwardsTolerance = .05f;
        public void Validate()
        {
            if (!Range(radius, 0, 8) || !Range(minimumDrop, .001f, 200) || !Range(maximumDrop, minimumDrop, 1000)
                || !Range(maximumSpan, .001f, 4096) || !Range(maximumWidthRatio, 1, 10)
                || !Range(minimumDirectionDot, -1, 1) || !Range(backwardsTolerance, 0, .25f))
                throw new InvalidDataException("Неверные настройки привязки водопада.");
        }
        static bool Range(float value, float min, float max) => float.IsFinite(value) && value >= min && value <= max;
        internal MapWaterfallSnapSettings Copy() => (MapWaterfallSnapSettings)MemberwiseClone();
    }

    public readonly struct MapRiverEndpoint : IEquatable<MapRiverEndpoint>
    {
        public readonly long riverId, nodeId;
        public readonly bool upstream;
        public readonly Vector2 position, tangent;
        public readonly float waterHeight, width;
        public readonly int layoutVersion;
        public readonly float surfaceSlope,speed,depth,maximumCut,maximumFill;
        internal MapRiverEndpoint(MapRiverPath path, bool upstream)
        {
            this.upstream = upstream; riverId = path.record.id;
            nodeId = path.record.nodes[upstream ? 0 : path.record.nodes.Count - 1].id;
            var frame = path.frames[upstream ? 0 : path.frames.Count - 1];
            position = frame.center; tangent = frame.tangent; waterHeight = frame.water; width = frame.width;
            layoutVersion=path.record.layoutVersion;surfaceSlope=path.SurfaceSlope(frame.station);speed=frame.speed;depth=Mathf.Lerp(frame.depth,frame.shallowDepth,frame.ford);
            maximumCut=frame.sectionProfile?.maximumCut??10;maximumFill=frame.sectionProfile?.maximumFill??2;
        }
        public bool Equals(MapRiverEndpoint other) => riverId == other.riverId && nodeId == other.nodeId && upstream == other.upstream;
        public override bool Equals(object obj) => obj is MapRiverEndpoint other && Equals(other);
        public override int GetHashCode() => unchecked((riverId.GetHashCode() * 397 ^ nodeId.GetHashCode()) * 397 ^ upstream.GetHashCode());
    }

    public enum MapWaterfallRejection { None, MissingEndpoint, SameRiver, OccupiedEndpoint, InvalidDrop, IncompatibleWidth, OpposedFlow, BackwardsSpan, TooFar, Cycle }

    public readonly struct MapWaterfallCandidate
    {
        public readonly MapRiverEndpoint upper, lower;
        public readonly float squaredDistance;
        internal MapWaterfallCandidate(MapRiverEndpoint upper, MapRiverEndpoint lower, float squaredDistance)
        { this.upper = upper; this.lower = lower; this.squaredDistance = squaredDistance; }
        public MapWaterfallRecord Record(long id) => new MapWaterfallRecord { id = id, upstreamRiverId = upper.riverId,
            upstreamNodeId = upper.nodeId, downstreamRiverId = lower.riverId, downstreamNodeId = lower.nodeId };
    }

    /// <summary>
    /// Immutable graph/endpoint snapshot of actual MapRiverRecords. Build on a river revision, never per frame.
    /// Operations return a complete validated replacement; caller owns the session transaction/persistence.
    /// Only structural endpoints are supported: station splitting must preserve the spline separately.
    /// </summary>
    public sealed class MapRiverNetwork
    {
        const float CellSize = 8f;
        readonly SortedDictionary<long, MapRiverPath> paths = new SortedDictionary<long, MapRiverPath>();
        readonly SortedDictionary<long, MapWaterfallRecord> links = new SortedDictionary<long, MapWaterfallRecord>();
        readonly Dictionary<long, long> membership = new Dictionary<long, long>();
        readonly Dictionary<long, long> downstream = new Dictionary<long, long>();
        readonly Dictionary<ChunkCoordinate, List<MapRiverEndpoint>> endpointIndex = new Dictionary<ChunkCoordinate, List<MapRiverEndpoint>>();
        readonly HashSet<MapRiverEndpoint> occupied = new HashSet<MapRiverEndpoint>();
        readonly MapWaterfallSnapSettings settings;
        public int RiverCount => paths.Count;
        public int WaterfallCount => links.Count;
        public bool HasRoutedRiver(long riverId)
        {
            foreach (var w in links.Values) if (w.route != null && !w.route.IsEmpty && (w.upstreamRiverId == riverId || w.downstreamRiverId == riverId)) return true;
            return false;
        }
        public bool HasRoutedEndpoint(long riverId, bool upstream)
        {
            foreach (var w in links.Values) if (w.route != null && !w.route.IsEmpty
                && (upstream ? w.downstreamRiverId == riverId : w.upstreamRiverId == riverId)) return true;
            return false;
        }
        public bool QueryWaterfallVolume(Vector3 point, out long waterfallId, float padding = 0)
        {
            foreach (var w in links.Values) if (w.route != null && !w.route.IsEmpty && w.route.Contains(point, padding)) { waterfallId = w.id; return true; }
            waterfallId = 0; return false;
        }

        public MapRiverNetwork(IEnumerable<MapRiverRecord> records, MapRiverNetworkData data = null, MapWaterfallSnapSettings settings = null)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            this.settings = (settings ?? new MapWaterfallSnapSettings()).Copy(); this.settings.Validate();
            foreach (var record in records)
            {
                if (record == null || paths.Count >= 4096 || paths.ContainsKey(record.id)) throw new InvalidDataException("Повтор ID или лимит участков сети.");
                // The graph owns the recipe and does not expose its mutable path/record.
                var path = new MapRiverPath(CopyRiver(record)); paths.Add(record.id, path);
                AddEndpoint(new MapRiverEndpoint(path, true)); AddEndpoint(new MapRiverEndpoint(path, false));
            }
            LoadGraph(data);
        }

        // Link-only edits reuse the immutable geometry/index. No Catmull-Rom or
        // river mesh reconstruction is needed merely to merge/split membership.
        MapRiverNetwork(MapRiverNetwork source, MapRiverNetworkData data)
        {
            paths = source.paths; endpointIndex = source.endpointIndex; settings = source.settings;
            LoadGraph(data);
        }

        void LoadGraph(MapRiverNetworkData data)
        {
            if (data == null)
            {
                foreach (long id in paths.Keys) membership.Add(id, id);
                return;
            }
            if ((data.version != 1 && data.version != 2) || data.waterfalls == null || data.members == null || data.waterfalls.Count > 4096 || data.members.Count != paths.Count)
                throw new InvalidDataException("Неверный формат речной сети.");
            foreach (var member in data.members)
            {
                if (member == null || member.networkId == 0 || !paths.ContainsKey(member.riverId) || membership.ContainsKey(member.riverId))
                    throw new InvalidDataException("Неверная принадлежность речной сети.");
                membership.Add(member.riverId, member.networkId);
            }
            var ordered = new List<MapWaterfallRecord>(data.waterfalls);
            if (ordered.Exists(r => r == null)) throw new InvalidDataException("Отсутствует запись водопада.");
            ordered.Sort((a, b) => a.id.CompareTo(b.id));
            foreach (var record in ordered)
            {
                if (record.id == 0 || links.ContainsKey(record.id)) throw new InvalidDataException("Повтор ID водопада.");
                var reason = ValidateConnection(record.upstreamRiverId, record.upstreamNodeId, record.downstreamRiverId, record.downstreamNodeId);
                if (reason != MapWaterfallRejection.None) throw new InvalidDataException("Неверная связь водопада: " + reason);
                if (record.route != null && !record.route.IsEmpty)
                {
                    if (data.version == 1) throw new InvalidDataException("Маршрут требует новую версию речной сети.");
                    record.route.Validate(Endpoint(record.upstreamRiverId, false), Endpoint(record.downstreamRiverId, true));
                }
                links.Add(record.id, record.Copy());
                downstream.Add(record.upstreamRiverId, record.downstreamRiverId);
                occupied.Add(Endpoint(record.upstreamRiverId, false)); occupied.Add(Endpoint(record.downstreamRiverId, true));
            }
            var usedNetworks = new HashSet<long>();
            foreach (var component in Components())
            {
                long network = membership[component[0]];
                if (!usedNetworks.Add(network)) throw new InvalidDataException("Раздельные компоненты используют один ID сети.");
                foreach (long id in component) if (membership[id] != network) throw new InvalidDataException("Связанные участки имеют разные ID сети.");
            }
        }

        public long NetworkId(long riverId) => membership.TryGetValue(riverId, out var id) ? id : throw new KeyNotFoundException("Участок реки не найден.");
        public MapRiverEndpoint Endpoint(long riverId, bool upstream) => paths.TryGetValue(riverId, out var path)
            ? new MapRiverEndpoint(path, upstream) : throw new KeyNotFoundException("Участок реки не найден.");
        public MapRiverNetworkData Snapshot()
        {
            var result = new MapRiverNetworkData();
            foreach (var record in links.Values) result.waterfalls.Add(record.Copy());
            foreach (long id in paths.Keys) result.members.Add(new MapRiverNetworkMember { riverId = id, networkId = membership[id] });
            return result;
        }

        public MapWaterfallRejection ValidateConnection(long upperRiver, long upperNode, long lowerRiver, long lowerNode)
        {
            if (!paths.ContainsKey(upperRiver) || !paths.ContainsKey(lowerRiver)) return MapWaterfallRejection.MissingEndpoint;
            if (upperRiver == lowerRiver) return MapWaterfallRejection.SameRiver;
            var upper = Endpoint(upperRiver, false); var lower = Endpoint(lowerRiver, true);
            if (upper.nodeId != upperNode || lower.nodeId != lowerNode) return MapWaterfallRejection.MissingEndpoint;
            if (occupied.Contains(upper) || occupied.Contains(lower)) return MapWaterfallRejection.OccupiedEndpoint;
            float drop = upper.waterHeight - lower.waterHeight;
            if (drop < settings.minimumDrop || drop > settings.maximumDrop) return MapWaterfallRejection.InvalidDrop;
            if (Mathf.Max(upper.width, lower.width) / Mathf.Min(upper.width, lower.width) > settings.maximumWidthRatio)
                return MapWaterfallRejection.IncompatibleWidth;
            if (Vector2.Dot(upper.tangent, lower.tangent) < settings.minimumDirectionDot) return MapWaterfallRejection.OpposedFlow;
            var span = lower.position - upper.position; float distance = span.magnitude;
            if (distance > settings.maximumSpan) return MapWaterfallRejection.TooFar;
            // A vertical fall has no XZ direction. Never normalize its zero span.
            if (distance > .001f && (Vector2.Dot(span / distance, upper.tangent) < -settings.backwardsTolerance
                || Vector2.Dot(span / distance, lower.tangent) < -settings.backwardsTolerance)) return MapWaterfallRejection.BackwardsSpan;
            if (Reaches(lowerRiver, upperRiver)) return MapWaterfallRejection.Cycle;
            return MapWaterfallRejection.None;
        }

        /// <summary>Only valid lower endpoints, sorted by XZ distance then stable river/node IDs. Multiple results remain explicit alternatives.</summary>
        public List<MapWaterfallCandidate> FindLowerCandidates(long upperRiverId, Vector2 pointer)
            => FindLowerCandidates(upperRiverId, pointer, settings.radius);
        public List<MapWaterfallCandidate> FindLowerCandidates(long upperRiverId, Vector2 pointer, float radius)
        {
            if (!float.IsFinite(radius) || radius < 0 || radius > 8) throw new ArgumentOutOfRangeException(nameof(radius));
            RequireFinite(pointer); var result = new List<MapWaterfallCandidate>();
            if (!paths.ContainsKey(upperRiverId)) return result;
            var upper = Endpoint(upperRiverId, false);
            foreach (var endpoint in EndpointsNear(pointer, radius))
            {
                if (!endpoint.upstream || ValidateConnection(upper.riverId, upper.nodeId, endpoint.riverId, endpoint.nodeId) != MapWaterfallRejection.None) continue;
                result.Add(new MapWaterfallCandidate(upper, endpoint, (endpoint.position - pointer).sqrMagnitude));
            }
            result.Sort((a, b) => { int c = a.squaredDistance.CompareTo(b.squaredDistance); if (c != 0) return c;
                c = a.lower.riverId.CompareTo(b.lower.riverId); return c != 0 ? c : a.lower.nodeId.CompareTo(b.lower.nodeId); });
            return result;
        }

        public List<MapRiverEndpoint> FindUpperEndpoints(Vector2 pointer)
            => FindUpperEndpoints(pointer, settings.radius);
        public List<MapRiverEndpoint> FindUpperEndpoints(Vector2 pointer, float radius)
        {
            if (!float.IsFinite(radius) || radius < 0 || radius > 8) throw new ArgumentOutOfRangeException(nameof(radius));
            RequireFinite(pointer); var result = new List<MapRiverEndpoint>();
            foreach (var endpoint in EndpointsNear(pointer, radius)) if (!endpoint.upstream && !occupied.Contains(endpoint)) result.Add(endpoint);
            result.Sort((a, b) => { int c = (a.position - pointer).sqrMagnitude.CompareTo((b.position - pointer).sqrMagnitude);
                if (c != 0) return c; c = a.riverId.CompareTo(b.riverId); return c != 0 ? c : a.nodeId.CompareTo(b.nodeId); });
            return result;
        }

        public MapRiverNetwork WithWaterfall(MapWaterfallRecord record)
        {
            if (record == null || record.id == 0) throw new InvalidDataException("Нужен стабильный ID водопада.");
            if (links.TryGetValue(record.id, out var previous))
            {
                if (previous.upstreamRiverId == record.upstreamRiverId && previous.upstreamNodeId == record.upstreamNodeId
                    && previous.downstreamRiverId == record.downstreamRiverId && previous.downstreamNodeId == record.downstreamNodeId)
                {
                    if (record.route == null || record.route.IsEmpty || JsonUtility.ToJson(previous.route) == JsonUtility.ToJson(record.route)) return this;
                    return WithWaterfallRoute(record.id, record.route);
                }
                throw new InvalidDataException("ID водопада уже занят другой связью.");
            }
            var reason = ValidateConnection(record.upstreamRiverId, record.upstreamNodeId, record.downstreamRiverId, record.downstreamNodeId);
            if (reason != MapWaterfallRejection.None) throw new InvalidDataException("Водопад не создан: " + reason);
            var next = Snapshot(); next.waterfalls.Add(record.Copy());
            long a = NetworkId(record.upstreamRiverId), b = NetworkId(record.downstreamRiverId), merged = Math.Min(a, b);
            foreach (var member in next.members) if (member.networkId == a || member.networkId == b) member.networkId = merged;
            return Rebuild(next);
        }

        public MapRiverNetwork WithWaterfallRoute(long waterfallId, MapWaterfallRouteData route)
        {
            if (!links.ContainsKey(waterfallId)) throw new InvalidDataException("Водопад не найден.");
            var next = Snapshot(); next.waterfalls.Find(w => w.id == waterfallId).route = route?.Copy();
            return Rebuild(next);
        }

        public MapRiverNetwork WithoutWaterfall(long id, Func<long> allocateNetworkId)
        {
            if (!links.TryGetValue(id, out var removed)) return this;
            if (allocateNetworkId == null) throw new ArgumentNullException(nameof(allocateNetworkId));
            var next = Snapshot(); next.waterfalls.RemoveAll(r => r.id == id);
            // Forest invariant (one input/output per endpoint, no cycles) means removing
            // this directed edge separates its component. Retain identity on its smallest river ID.
            var components = Components(id); var claimed = new HashSet<long>(membership.Values);
            long previousNetwork = membership[removed.upstreamRiverId]; bool retained = false;
            foreach (var component in components)
            {
                if (membership[component[0]] != previousNetwork) continue;
                long network = previousNetwork;
                if (retained)
                {
                    network = allocateNetworkId();
                    if (network == 0 || !claimed.Add(network)) throw new InvalidDataException("Выделенный ID сети уже занят.");
                }
                retained = true;
                var members = new HashSet<long>(component);
                foreach (var member in next.members) if (members.Contains(member.riverId)) member.networkId = network;
            }
            return Rebuild(next);
        }

        /// <summary>Rebase against an atomic river edit. Dependent removals must be explicitly requested.</summary>
        public MapRiverNetwork WithRivers(IEnumerable<MapRiverRecord> records, bool removeDependencies, Func<long> allocateNetworkId)
        {
            if (records == null || allocateNetworkId == null) throw new ArgumentNullException();
            var values = new List<MapRiverRecord>(records); var ids = new SortedSet<long>();
            foreach (var record in values)
                if (record == null || record.id == 0 || !ids.Add(record.id)) throw new InvalidDataException("Неверные участки речной сети.");
            var next = Snapshot();
            if (!removeDependencies && next.waterfalls.Exists(w => !ids.Contains(w.upstreamRiverId) || !ids.Contains(w.downstreamRiverId)))
                throw new InvalidDataException("Участок связан с водопадом. Подтвердите удаление зависимых связей.");
            next.waterfalls.RemoveAll(w => !ids.Contains(w.upstreamRiverId) || !ids.Contains(w.downstreamRiverId));
            var adjacent = new Dictionary<long, List<long>>();
            foreach (long id in ids) adjacent.Add(id, new List<long>());
            foreach (var link in next.waterfalls)
            { adjacent[link.upstreamRiverId].Add(link.downstreamRiverId); adjacent[link.downstreamRiverId].Add(link.upstreamRiverId); }
            var seen = new HashSet<long>(); var claimed = new HashSet<long>(); var reserved = new HashSet<long>(membership.Values);
            next.members.Clear();
            foreach (long id in ids)
            {
                if (!seen.Add(id)) continue; var component = new List<long>(); var pending = new Stack<long>(); pending.Push(id);
                long network = 0;
                while (pending.Count > 0)
                {
                    long current = pending.Pop(); component.Add(current);
                    if (membership.TryGetValue(current, out long previous) && (network == 0 || previous < network)) network = previous;
                    foreach (long neighbour in adjacent[current]) if (seen.Add(neighbour)) pending.Push(neighbour);
                }
                if (network == 0 && !reserved.Contains(id)) network = id;
                if (network == 0 || claimed.Contains(network))
                {
                    network = allocateNetworkId();
                    if (network == 0 || reserved.Contains(network) || claimed.Contains(network)) throw new InvalidDataException("Выделенный ID сети уже занят.");
                }
                claimed.Add(network); reserved.Add(network);
                foreach (long river in component) next.members.Add(new MapRiverNetworkMember { riverId = river, networkId = network });
            }
            return new MapRiverNetwork(values, next, settings);
        }

        MapRiverNetwork Rebuild(MapRiverNetworkData data) => new MapRiverNetwork(this, data);
        bool Reaches(long start, long target)
        {
            var visited = new HashSet<long>(); long current = start;
            while (visited.Add(current))
            {
                if (current == target) return true;
                if (!downstream.TryGetValue(current, out current)) return false;
            }
            return false;
        }
        List<List<long>> Components(long exceptLink = 0)
        {
            var adjacent = new Dictionary<long, List<long>>(); foreach (long id in paths.Keys) adjacent.Add(id, new List<long>());
            foreach (var link in links.Values) if (link.id != exceptLink)
            { adjacent[link.upstreamRiverId].Add(link.downstreamRiverId); adjacent[link.downstreamRiverId].Add(link.upstreamRiverId); }
            var seen = new HashSet<long>(); var result = new List<List<long>>();
            foreach (long id in paths.Keys)
            {
                if (!seen.Add(id)) continue; var component = new List<long>(); var pending = new Stack<long>(); pending.Push(id);
                while (pending.Count > 0) { long current = pending.Pop(); component.Add(current); foreach (long neighbour in adjacent[current]) if (seen.Add(neighbour)) pending.Push(neighbour); }
                component.Sort(); result.Add(component);
            }
            return result;
        }
        void AddEndpoint(MapRiverEndpoint endpoint)
        { var key = Cell(endpoint.position); if (!endpointIndex.TryGetValue(key, out var list)) endpointIndex.Add(key, list = new List<MapRiverEndpoint>()); list.Add(endpoint); }
        IEnumerable<MapRiverEndpoint> EndpointsNear(Vector2 pointer, float radius)
        {
            var lo = Cell(pointer - Vector2.one * radius); var hi = Cell(pointer + Vector2.one * radius);
            for (int z = lo.z; z <= hi.z; z++) for (int x = lo.x; x <= hi.x; x++)
                if (endpointIndex.TryGetValue(new ChunkCoordinate(x, z), out var list)) foreach (var endpoint in list)
                    if ((endpoint.position - pointer).sqrMagnitude <= radius * radius) yield return endpoint;
        }
        static ChunkCoordinate Cell(Vector2 point) => new ChunkCoordinate(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.y / CellSize));
        static void RequireFinite(Vector2 point)
        {
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || Math.Abs((double)point.x / CellSize) > int.MaxValue - 16d
                || Math.Abs((double)point.y / CellSize) > int.MaxValue - 16d) throw new ArgumentOutOfRangeException(nameof(point));
        }
        static MapRiverRecord CopyRiver(MapRiverRecord source)
        {
            var result = new MapRiverRecord { id = source.id, seed = source.seed, layoutVersion = source.layoutVersion, sectionProfile=source.sectionProfile==null?null:JsonUtility.FromJson<MapRiverSectionProfile>(JsonUtility.ToJson(source.sectionProfile)) };
            if (source.nodes == null || source.patches == null || source.rocks == null) throw new InvalidDataException("Неверный профиль реки.");
            foreach (var n in source.nodes)
            {
                if (n == null) throw new InvalidDataException("Отсутствует точка реки.");
                RequireFinite(n.position);
                result.nodes.Add(new MapRiverNode { id = n.id, position = n.position, width = n.width, depth = n.depth, waterHeight = n.waterHeight,
                    speed = n.speed, leftTransition = n.leftTransition, rightTransition = n.rightTransition, leftBank = n.leftBank, rightBank = n.rightBank, connection = n.connection });
            }
            foreach (var p in source.patches)
            {
                if (p == null) throw new InvalidDataException("Отсутствует участок берега.");
                result.patches.Add(new MapBankPatch { id = p.id, station = p.station, radius = p.radius, shallowDepth = p.shallowDepth, left = p.left, right = p.right, crossChannel = p.crossChannel });
            }
            // Rocks are irrelevant to the graph, but the input must remain valid and untouched.
            result.rocks.AddRange(source.rocks);
            return result;
        }
    }
}
