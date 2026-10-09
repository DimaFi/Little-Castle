using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Carves river channels into base terrain using macro river lines.
    ///
    /// Run after the base height stage and before TerrainClassificationStage.
    /// Local river width/depth profiles are respected so headwaters stay
    /// smaller while downstream/confluence sections become broader/deeper.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RiverTerrainCarvingStage",
        menuName = "Little Castle/World/Generation/River Terrain Carving Stage")]
    public sealed class RiverTerrainCarvingStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.TerrainModification;

        [Min(0f)]
        [SerializeField] private float bankFalloff = 5f;

        [Range(0f, 2f)]
        [SerializeField] private float depthMultiplier = 1f;

        [SerializeField]
        private AnimationCurve crossSection =
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Concept river profile (opt in)")]
        [Tooltip(
            "Evaluate every valid river segment and use its maximum carve " +
            "contribution. This keeps carving aligned with variable-width " +
            "riverbed and wetness envelopes around sharp bends. Disabled " +
            "preserves legacy closest-segment behavior.")]
        [SerializeField] private bool useAnySegmentCarveEnvelope;

        public bool UseAnySegmentCarveEnvelope =>
            useAnySegmentCarveEnvelope;

        [Header("Concept chunk-local river broad phase (opt in)")]
        [Tooltip(
            "Cull river segments against this chunk's expanded bounds once " +
            "before sampling height vertices. Only active when the " +
            "any-segment carve envelope is enabled; legacy Main is unchanged.")]
        [SerializeField] private bool useChunkLocalCarveBroadPhase;

        public bool UseChunkLocalCarveBroadPhase =>
            useChunkLocalCarveBroadPhase;

        // Diagnostics for the most recent Generate call; not authoritative
        // generation state. Excluded segments require no per-vertex samples.
        public int LastCandidateSegments { get; private set; }
        public int LastScannedSegments { get; private set; }

        private readonly struct CarveSegment
        {
            public readonly WorldRiverData river;
            public readonly int index;
            public readonly Vector2 a;
            public readonly Vector2 b;

            public CarveSegment(
                WorldRiverData river, int index, Vector2 a, Vector2 b)
            {
                this.river = river;
                this.index = index;
                this.a = a;
                this.b = b;
            }
        }

        public override void Generate(
            GenerationContext context,
            WorldChunkData chunk)
        {
            LastCandidateSegments = 0;
            LastScannedSegments = 0;
            MacroWorldPlan plan = context.MacroPlan;

            if (plan == null ||
                plan.Rivers.Count == 0)
            {
                return;
            }

            float chunkSize =
                context.Settings.ChunkWorldSize;

            float cellSize =
                context.Settings.CellWorldSize;

            float originX =
                chunk.Coordinate.x *
                chunkSize;

            float originZ =
                chunk.Coordinate.z *
                chunkSize;

            // Opt-in only for the any-segment carve policy; the nearest-only
            // legacy path must still inspect the whole centerline to choose
            // its unique closest segment even if that segment contributes 0.
            List<CarveSegment> localSegments =
                useAnySegmentCarveEnvelope &&
                useChunkLocalCarveBroadPhase
                    ? CollectChunkCandidates(
                        plan, originX, originZ, chunkSize)
                    : null;

            for (int z = 0;
                 z < chunk.SamplesPerSide;
                 z++)
            {
                for (int x = 0;
                     x < chunk.SamplesPerSide;
                     x++)
                {
                    var worldPoint =
                        new Vector2(
                            originX + x * cellSize,
                            originZ + z * cellSize);

                    float maxCarve = 0f;

                    if (localSegments != null)
                    {
                        // Stable river/segment order matches the previous
                        // nested iteration and Mathf.Max accumulation.
                        for (int c = 0; c < localSegments.Count; c++)
                        {
                            CarveSegment candidate = localSegments[c];
                            maxCarve = Mathf.Max(
                                maxCarve,
                                GetSegmentCarve(
                                    worldPoint,
                                    candidate.river,
                                    candidate.index,
                                    candidate.a,
                                    candidate.b));
                        }

                        if (maxCarve > 0f)
                            chunk.SetHeight(
                                x, z, chunk.GetHeight(x, z) - maxCarve);
                        continue;
                    }

                    for (int r = 0;
                         r < plan.Rivers.Count;
                         r++)
                    {
                        WorldRiverData river =
                            plan.Rivers[r];

                        if (river == null ||
                            river.centerline == null ||
                            river.centerline.Count < 2)
                        {
                            continue;
                        }

                        if (useAnySegmentCarveEnvelope)
                        {
                            maxCarve =
                                Mathf.Max(
                                    maxCarve,
                                    GetMaxSegmentCarve(
                                        worldPoint,
                                        river));

                            continue;
                        }

                        // Exact legacy-off branch: select only the closest
                        // centerline segment before evaluating its profile.
                        if (!TryGetClosestSegment(
                                worldPoint,
                                river.centerline,
                                out float distance,
                                out int segmentIndex,
                                out float segmentT))
                        {
                            continue;
                        }

                        float localWidth =
                            river.GetWidthAtSegment(
                                segmentIndex,
                                segmentT);

                        float localDepth =
                            river.GetDepthAtSegment(
                                segmentIndex,
                                segmentT);

                        float influenceRadius =
                            Mathf.Max(
                                0.5f,
                                localWidth * 0.5f +
                                bankFalloff);

                        if (distance > influenceRadius)
                            continue;

                        float normalized =
                            Mathf.Clamp01(
                                distance /
                                influenceRadius);

                        float profile =
                            crossSection != null
                                ? Mathf.Clamp01(
                                    crossSection.Evaluate(
                                        normalized))
                                : 1f - normalized;

                        float carve =
                            Mathf.Max(
                                0f,
                                localDepth) *
                            Mathf.Max(
                                0f,
                                depthMultiplier) *
                            profile;

                        maxCarve =
                            Mathf.Max(
                                maxCarve,
                                carve);
                    }

                    if (maxCarve <= 0f)
                        continue;

                    chunk.SetHeight(
                        x,
                        z,
                        chunk.GetHeight(x, z) -
                        maxCarve);
                }
            }
        }

        private List<CarveSegment> CollectChunkCandidates(
            MacroWorldPlan plan,
            float originX,
            float originZ,
            float chunkSize)
        {
            var result = new List<CarveSegment>(16);
            float maxX = originX + chunkSize;
            float maxZ = originZ + chunkSize;

            for (int r = 0; r < plan.Rivers.Count; r++)
            {
                WorldRiverData river = plan.Rivers[r];
                if (river == null || river.centerline == null ||
                    river.centerline.Count < 2)
                    continue;

                for (int segmentIndex = 0;
                     segmentIndex < river.centerline.Count - 1;
                     segmentIndex++)
                {
                    LastScannedSegments++;
                    if (!RiverEnvelopeUtility.TryCreateSegment(
                            river, segmentIndex,
                            out RiverEnvelopeUtility.Segment segment))
                        continue;

                    // Conservative bound: Q04's minimum half-width is
                    // 0.1m vs carving's >=0.05m, and the added margin
                    // dominates max(0.5m, halfWidth + bankFalloff).
                    // This may include extra segments but must NEVER omit
                    // a segment that can affect any boundary vertex.
                    if (!RiverEnvelopeUtility.OverlapsChunk(
                            segment,
                            Mathf.Max(0.5f, bankFalloff),
                            originX, originZ, maxX, maxZ))
                        continue;

                    result.Add(new CarveSegment(
                        river, segmentIndex, segment.a, segment.b));
                }
            }

            LastCandidateSegments = result.Count;
            return result;
        }

        private float GetMaxSegmentCarve(
            Vector2 point,
            WorldRiverData river)
        {
            float maxCarve = 0f;

            for (int i = 0; i < river.centerline.Count - 1; i++)
            {
                Vector2 a = river.centerline[i];
                Vector2 b = river.centerline[i + 1];
                if ((b - a).sqrMagnitude <= 0.000001f)
                    continue;

                maxCarve = Mathf.Max(
                    maxCarve, GetSegmentCarve(point, river, i, a, b));
            }

            return maxCarve;
        }

        /// <summary>
        /// Shared exact per-segment carving math for both the original full
        /// scan and the chunk-local candidate scan. Never substitute Q04's
        /// wetness mask radius/curve: carving has its own depth profile.
        /// </summary>
        private float GetSegmentCarve(
            Vector2 point,
            WorldRiverData river,
            int segmentIndex,
            Vector2 a,
            Vector2 b)
        {
            float distanceSqr =
                DistancePointSegmentSqr(
                    point, a, b, out float segmentT);

            float localWidth =
                river.GetWidthAtSegment(
                    segmentIndex, segmentT);

            float influenceRadius =
                Mathf.Max(
                    0.5f,
                    localWidth * 0.5f +
                    bankFalloff);

            if (distanceSqr >
                influenceRadius * influenceRadius)
                return 0f;

            float normalized =
                Mathf.Clamp01(
                    Mathf.Sqrt(distanceSqr) /
                    influenceRadius);

            float profile =
                crossSection != null
                    ? Mathf.Clamp01(
                        crossSection.Evaluate(normalized))
                    : 1f - normalized;

            float carve =
                Mathf.Max(
                    0f,
                    river.GetDepthAtSegment(
                        segmentIndex, segmentT)) *
                Mathf.Max(
                    0f,
                    depthMultiplier) *
                profile;

            return carve;
        }

        private static bool TryGetClosestSegment(
            Vector2 point,
            IReadOnlyList<Vector2> points,
            out float distance,
            out int segmentIndex,
            out float segmentT)
        {
            distance = float.PositiveInfinity;
            segmentIndex = -1;
            segmentT = 0f;

            if (points == null ||
                points.Count < 2)
            {
                return false;
            }

            float bestSqr =
                float.PositiveInfinity;

            for (int i = 0;
                 i < points.Count - 1;
                 i++)
            {
                float distanceSqr =
                    DistancePointSegmentSqr(
                        point,
                        points[i],
                        points[i + 1],
                        out float t);

                if (distanceSqr >= bestSqr)
                    continue;

                bestSqr = distanceSqr;
                segmentIndex = i;
                segmentT = t;
            }

            if (segmentIndex < 0)
                return false;

            distance =
                Mathf.Sqrt(bestSqr);

            return true;
        }

        private static float DistancePointSegmentSqr(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            out float segmentT)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;

            if (lengthSqr <= 0.000001f)
            {
                segmentT = 0f;
                return (point - a).sqrMagnitude;
            }

            segmentT =
                Mathf.Clamp01(
                    Vector2.Dot(
                        point - a,
                        ab) /
                    lengthSqr);

            Vector2 closest =
                a +
                ab *
                segmentT;

            return
                (point - closest).sqrMagnitude;
        }
    }
}
