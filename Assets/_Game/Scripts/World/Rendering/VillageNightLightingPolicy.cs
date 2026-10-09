using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    public enum VillageNightMarkerKind : byte
    {
        PathEntrance = 0,
        CourtyardWell = 1,
        WindowEmissive = 2
    }

    /// <summary>
    /// View presentation IDs are derived from the approved Q11 village
    /// layout; they are not added to the authoritative spawn or save schema.
    /// </summary>
    public readonly struct VillageNightMarker
    {
        public readonly long stableId;
        public readonly VillageNightMarkerKind kind;
        public readonly Vector2 worldXZ;

        public VillageNightMarker(
            long stableId,
            VillageNightMarkerKind kind,
            Vector2 worldXZ)
        {
            this.stableId = stableId;
            this.kind = kind;
            this.worldXZ = worldXZ;
        }
    }

    public readonly struct VillageNightPresentation
    {
        public readonly long stableId;
        public readonly VillageNightMarkerKind kind;
        public readonly bool emissiveVisible;
        public readonly bool groundPoolVisible;
        public readonly bool requestRealtimeLight;

        public VillageNightPresentation(
            long stableId,
            VillageNightMarkerKind kind,
            bool emissiveVisible,
            bool groundPoolVisible,
            bool requestRealtimeLight)
        {
            this.stableId = stableId;
            this.kind = kind;
            this.emissiveVisible = emissiveVisible;
            this.groundPoolVisible = groundPoolVisible;
            this.requestRealtimeLight = requestRealtimeLight;
        }
    }

    public readonly struct VillageNightBudgetMetrics
    {
        public readonly int markers;
        public readonly int emissive;
        public readonly int groundPools;
        public readonly int realtimeRequests;
        public readonly int realtimeCandidatesDenied;

        public VillageNightBudgetMetrics(
            int markers,
            int emissive,
            int groundPools,
            int realtimeRequests,
            int realtimeCandidatesDenied)
        {
            this.markers = markers;
            this.emissive = emissive;
            this.groundPools = groundPools;
            this.realtimeRequests = realtimeRequests;
            this.realtimeCandidatesDenied = realtimeCandidatesDenied;
        }
    }

    /// <summary>
    /// Deterministic, unambiguous cheap/near presentation tier screening.
    /// The existing NightLightBudgetManager does the actual global lighting
    /// budget and fading, not this planner.
    ///
    /// No GameObjects/Light creation, per-frame allocations after warming
    /// the instance scratch buffers, or changes to gameplay/world state.
    /// </summary>
    public sealed class VillageNightLightingPolicy
    {
        private struct Ranked
        {
            public VillageNightMarker marker;
            public double squaredDistance;
            public bool emissive;
            public bool pool;
            public bool realtime;
        }

        private sealed class PriorityComparer : IComparer<Ranked>
        {
            internal static readonly PriorityComparer Instance =
                new PriorityComparer();

            public int Compare(Ranked a, Ranked b)
            {
                int priorityA = Priority(a.marker.kind);
                int priorityB = Priority(b.marker.kind);
                int byPriority = priorityA.CompareTo(priorityB);
                if (byPriority != 0)
                    return byPriority;
                int distance =
                    a.squaredDistance.CompareTo(b.squaredDistance);
                if (distance != 0)
                    return distance;
                return a.marker.stableId.CompareTo(b.marker.stableId);
            }

            private static int Priority(VillageNightMarkerKind kind) =>
                kind == VillageNightMarkerKind.PathEntrance ? 0 :
                kind == VillageNightMarkerKind.CourtyardWell ? 1 : 2;
        }

        private sealed class StableIdComparer : IComparer<Ranked>
        {
            internal static readonly StableIdComparer Instance =
                new StableIdComparer();
            public int Compare(Ranked a, Ranked b) =>
                a.marker.stableId.CompareTo(b.marker.stableId);
        }

        private readonly List<Ranked> scratch = new List<Ranked>(8);
        private readonly HashSet<long> ids = new HashSet<long>();

        /// <summary>
        /// Creates markers only for an *already accepted* Q11 layout.
        /// No proposed house/window position is inferred from its art pivot.
        /// Visual well and path glows can be positioned/offset vertically by
        /// the root presenter after sampling actual terrain geometry.
        /// </summary>
        public static void BuildMarkers(
            VillageLayoutData layout,
            List<VillageNightMarker> output)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();
            output.Add(new VillageNightMarker(
                DeterministicHash.StablePairId(
                    layout.WorldSeed, layout.VillageStableId, -2L,
                    0x4E4C574C),
                VillageNightMarkerKind.CourtyardWell, layout.WellPosition));

            for (int i = 0; i < layout.Approaches.Count; i++)
            {
                VillageApproachPath path = layout.Approaches[i];
                output.Add(new VillageNightMarker(
                    DeterministicHash.StablePairId(
                        layout.WorldSeed, layout.VillageStableId,
                        path.houseStableId, 0x4E4C454E),
                    VillageNightMarkerKind.PathEntrance, path.doorEdgeWorld));
            }
        }

        /// <summary>
        /// globalFreeSlots is a root-supplied advisory number, NOT a
        /// replacement for NightLightBudgetManager. It must be set to
        /// zero when the owner cannot prove current global availability.
        /// </summary>
        public VillageNightBudgetMetrics Evaluate(
            IReadOnlyList<VillageNightMarker> input,
            VillageNightLightingBudget config,
            Vector2 cameraXZ,
            float nightAmount,
            int globalFreeSlots,
            List<VillageNightPresentation> output)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (!config.IsValid ||
                !Finite(cameraXZ.x) || !Finite(cameraXZ.y) ||
                !Finite(nightAmount) ||
                nightAmount < 0f || nightAmount > 1f ||
                globalFreeSlots < 0)
                throw new ArgumentException(
                    "Invalid village night budget, camera/night or global slots.");

            scratch.Clear();
            ids.Clear();

            for (int i = 0; i < input.Count; i++)
            {
                VillageNightMarker marker = input[i];
                if (marker.kind < VillageNightMarkerKind.PathEntrance ||
                    marker.kind > VillageNightMarkerKind.WindowEmissive ||
                    !Finite(marker.worldXZ.x) ||
                    !Finite(marker.worldXZ.y) ||
                    !ids.Add(marker.stableId))
                    throw new ArgumentException(
                        "Invalid marker position/kind or duplicate stable ID.");

                double dx = (double)cameraXZ.x - marker.worldXZ.x;
                double dz = (double)cameraXZ.y - marker.worldXZ.y;
                scratch.Add(new Ranked
                {
                    marker = marker,
                    squaredDistance = dx * dx + dz * dz
                });
            }

            scratch.Sort(PriorityComparer.Instance);
            bool night = nightAmount >= config.minimumNightAmount;
            double poolSq = (double)config.groundPoolDistance *
                config.groundPoolDistance;
            double nearSq = (double)config.realtimeDistance *
                config.realtimeDistance;
            double emissiveSq = (double)config.emissiveDistance *
                config.emissiveDistance;
            int pools = 0;
            int realtime = 0;
            int emissive = 0;
            int denied = 0;
            int effectiveRealtimeLimit = Mathf.Min(
                config.maxVillageRealtimeRequests, globalFreeSlots);

            for (int i = 0; i < scratch.Count; i++)
            {
                Ranked value = scratch[i];
                bool visible = night && value.squaredDistance <= emissiveSq;
                bool pool = visible &&
                    value.marker.kind != VillageNightMarkerKind.WindowEmissive &&
                    value.squaredDistance <= poolSq &&
                    pools < config.maxVillageGroundPools;

                // Strictly no Point/Spot requests for far objects or
                // window-only emissive markers.
                bool eligibleLight = pool &&
                    value.squaredDistance <= nearSq;
                bool request = eligibleLight &&
                    realtime < effectiveRealtimeLimit;

                if (visible)
                    emissive++;
                if (pool)
                    pools++;
                if (request)
                    realtime++;
                else if (eligibleLight)
                    denied++;

                value.emissive = visible;
                value.pool = pool;
                value.realtime = request;
                scratch[i] = value;
            }

            scratch.Sort(StableIdComparer.Instance);
            output.Clear();
            if (output.Capacity < scratch.Count)
                output.Capacity = scratch.Count;
            for (int i = 0; i < scratch.Count; i++)
            {
                Ranked value = scratch[i];
                output.Add(new VillageNightPresentation(
                    value.marker.stableId, value.marker.kind,
                    value.emissive, value.pool, value.realtime));
            }

            return new VillageNightBudgetMetrics(
                scratch.Count, emissive, pools, realtime, denied);
        }

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
