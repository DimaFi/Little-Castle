using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Assigns logical terrain surface IDs for texturing/gameplay.
    /// It does not create materials. Rendering maps SurfaceKind to materials.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TerrainSurfaceStage",
        menuName = "Little Castle/World/Generation/Terrain Surface Stage")]
    public sealed class TerrainSurfaceStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.Surface;

        [Min(0f)]
        [SerializeField] private float roadSurfaceExtraWidth = 0.5f;

        [Min(0f)]
        [SerializeField] private float riverbedExtraWidth = 0.5f;

        [Header("Concept river profile (opt in)")]
        [Tooltip(
            "Use per-point river width profile for riverbed surface, " +
            "matching carved water channels. Disabled preserves legacy " +
            "nominalWidth behavior in existing Main world profiles.")]
        [SerializeField] private bool useVariableRiverWidth;

        public bool UseVariableRiverWidth => useVariableRiverWidth;

        [Header("Concept chunk broad phase (opt in)")]
        [Tooltip(
            "Build chunk-local road/river segment candidates before cell " +
            "surface tests. Disabled preserves the legacy whole-plan scans.")]
        [SerializeField] private bool useSegmentBroadPhase;

        public bool UseSegmentBroadPhase => useSegmentBroadPhase;

        private struct RoadSurfaceSegment
        {
            public Vector2 a;
            public Vector2 b;
            public float halfWidth;
            public RoadKind roadKind;
            public SegmentBounds bounds;
        }

        private struct RiverSurfaceSegment
        {
            public WorldRiverData river;
            public int segmentIndex;
            public Vector2 a;
            public Vector2 b;
            public float legacyHalfWidth;
            public SegmentBounds bounds;
        }

        private struct SegmentBounds
        {
            public float minX;
            public float minZ;
            public float maxX;
            public float maxZ;

            public bool Contains(Vector2 point) =>
                point.x >= minX &&
                point.x <= maxX &&
                point.y >= minZ &&
                point.y <= maxZ;
        }

        public override void Generate(
            GenerationContext context,
            WorldChunkData chunk)
        {
            AssignBaseSurface(chunk);

            MacroWorldPlan plan = context.MacroPlan;

            if (plan == null)
                return;

            if (useSegmentBroadPhase)
            {
                float chunkSize = context.Settings.ChunkWorldSize;
                float minX = chunk.Coordinate.x * chunkSize;
                float minZ = chunk.Coordinate.z * chunkSize;
                float maxX = minX + chunkSize;
                float maxZ = minZ + chunkSize;

                List<RoadSurfaceSegment> roadSegments =
                    BuildRoadSegments(
                        plan,
                        minX,
                        minZ,
                        maxX,
                        maxZ);

                List<RiverSurfaceSegment> riverSegments =
                    BuildRiverSegments(
                        plan,
                        minX,
                        minZ,
                        maxX,
                        maxZ);

                ApplyRoadSurfacesBroadPhase(
                    context,
                    chunk,
                    roadSegments);

                ApplyRiverSurfacesBroadPhase(
                    context,
                    chunk,
                    riverSegments);

                return;
            }

            ApplyRoadSurfaces(context, chunk, plan);
            ApplyRiverSurfaces(context, chunk, plan);
        }

        private static void AssignBaseSurface(
            WorldChunkData chunk)
        {
            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    BiomeKind biome =
                        chunk.GetBiome(x, z);

                    TerrainClass terrain =
                        chunk.GetTerrainClass(x, z);

                    SurfaceKind surface;

                    if (terrain == TerrainClass.Steep ||
                        biome == BiomeKind.RockyHighland)
                    {
                        surface = SurfaceKind.Rock;
                    }
                    else if (biome == BiomeKind.WetLowland)
                    {
                        surface = SurfaceKind.Mud;
                    }
                    else if (biome == BiomeKind.TemperateWoodland)
                    {
                        surface = SurfaceKind.WoodlandFloor;
                    }
                    else
                    {
                        surface = SurfaceKind.Grass;
                    }

                    chunk.SetSurface(
                        x,
                        z,
                        surface);
                }
            }
        }

        private List<RoadSurfaceSegment> BuildRoadSegments(
            MacroWorldPlan plan,
            float minX,
            float minZ,
            float maxX,
            float maxZ)
        {
            var result = new List<RoadSurfaceSegment>();

            // Preserve road-major and segment-major order so the first road
            // intersecting a cell keeps the exact legacy surface priority.
            for (int r = 0; r < plan.Roads.Count; r++)
            {
                WorldRoadData road = plan.Roads[r];
                if (road == null || road.centerline.Count < 2)
                    continue;

                float halfWidth =
                    Mathf.Max(
                        0.1f,
                        road.width * 0.5f +
                        roadSurfaceExtraWidth);

                for (int s = 0; s < road.centerline.Count - 1; s++)
                {
                    Vector2 a = road.centerline[s];
                    Vector2 b = road.centerline[s + 1];
                    if (!TryGetLocalBounds(
                            a,
                            b,
                            halfWidth,
                            minX,
                            minZ,
                            maxX,
                            maxZ,
                            out SegmentBounds bounds))
                    {
                        continue;
                    }

                    result.Add(new RoadSurfaceSegment
                    {
                        a = a,
                        b = b,
                        halfWidth = halfWidth,
                        roadKind = road.roadKind,
                        bounds = bounds
                    });
                }
            }

            return result;
        }

        private List<RiverSurfaceSegment> BuildRiverSegments(
            MacroWorldPlan plan,
            float minX,
            float minZ,
            float maxX,
            float maxZ)
        {
            var result = new List<RiverSurfaceSegment>();
            float profilePadding = Mathf.Max(0f, riverbedExtraWidth);

            for (int r = 0; r < plan.Rivers.Count; r++)
            {
                WorldRiverData river = plan.Rivers[r];
                if (river == null || river.centerline.Count < 2)
                    continue;

                float legacyHalfWidth =
                    Mathf.Max(
                        0.1f,
                        river.nominalWidth * 0.5f +
                        riverbedExtraWidth);

                for (int s = 0; s < river.centerline.Count - 1; s++)
                {
                    Vector2 a = river.centerline[s];
                    Vector2 b = river.centerline[s + 1];
                    float boundsPadding;

                    if (useVariableRiverWidth)
                    {
                        if ((b - a).sqrMagnitude <= 0.000001f)
                            continue;

                        float halfStart =
                            Mathf.Max(
                                0.1f,
                                river.GetWidthAtPoint(s) * 0.5f +
                                profilePadding);

                        float halfEnd =
                            Mathf.Max(
                                0.1f,
                                river.GetWidthAtPoint(s + 1) * 0.5f +
                                profilePadding);

                        boundsPadding =
                            ConservativePadding(
                                halfStart,
                                halfEnd);
                    }
                    else
                    {
                        // Legacy nominal-width mode includes degenerate point
                        // segments through DistancePointSegmentSqr.
                        boundsPadding = legacyHalfWidth;
                    }

                    if (!TryGetLocalBounds(
                            a,
                            b,
                            boundsPadding,
                            minX,
                            minZ,
                            maxX,
                            maxZ,
                            out SegmentBounds bounds))
                    {
                        continue;
                    }

                    result.Add(new RiverSurfaceSegment
                    {
                        river = river,
                        segmentIndex = s,
                        a = a,
                        b = b,
                        legacyHalfWidth = legacyHalfWidth,
                        bounds = bounds
                    });
                }
            }

            return result;
        }

        private void ApplyRoadSurfacesBroadPhase(
            GenerationContext context,
            WorldChunkData chunk,
            IReadOnlyList<RoadSurfaceSegment> segments)
        {
            float cellSize = context.Settings.CellWorldSize;
            float chunkSize = context.Settings.ChunkWorldSize;
            float originX = chunk.Coordinate.x * chunkSize;
            float originZ = chunk.Coordinate.z * chunkSize;

            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    var point = new Vector2(
                        originX + (x + 0.5f) * cellSize,
                        originZ + (z + 0.5f) * cellSize);

                    for (int i = 0; i < segments.Count; i++)
                    {
                        RoadSurfaceSegment segment = segments[i];
                        if (!segment.bounds.Contains(point))
                            continue;

                        float distanceSqr =
                            DistancePointSegmentSqr(
                                point,
                                segment.a,
                                segment.b);

                        if (!RoadSegmentIntersects(
                                distanceSqr,
                                segment.halfWidth))
                            continue;

                        chunk.SetSurface(
                            x,
                            z,
                            GetRoadSurface(segment.roadKind));
                        break;
                    }
                }
            }
        }

        private void ApplyRiverSurfacesBroadPhase(
            GenerationContext context,
            WorldChunkData chunk,
            IReadOnlyList<RiverSurfaceSegment> segments)
        {
            float cellSize = context.Settings.CellWorldSize;
            float chunkSize = context.Settings.ChunkWorldSize;
            float originX = chunk.Coordinate.x * chunkSize;
            float originZ = chunk.Coordinate.z * chunkSize;
            float profilePadding = Mathf.Max(0f, riverbedExtraWidth);

            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    var point = new Vector2(
                        originX + (x + 0.5f) * cellSize,
                        originZ + (z + 0.5f) * cellSize);

                    for (int i = 0; i < segments.Count; i++)
                    {
                        RiverSurfaceSegment segment = segments[i];
                        if (!segment.bounds.Contains(point))
                            continue;

                        bool intersects;

                        if (useVariableRiverWidth)
                        {
                            Vector2 delta = segment.b - segment.a;
                            float squaredLength = delta.sqrMagnitude;
                            float t = Mathf.Clamp01(
                                Vector2.Dot(
                                    point - segment.a,
                                    delta) /
                                squaredLength);
                            Vector2 nearest = segment.a + delta * t;
                            float halfWidth = Mathf.Max(
                                0.1f,
                                segment.river.GetWidthAtSegment(
                                    segment.segmentIndex,
                                    t) * 0.5f +
                                profilePadding);

                            intersects =
                                (point - nearest).sqrMagnitude <=
                                halfWidth * halfWidth;
                        }
                        else
                        {
                            intersects =
                                RiverSegmentIntersects(
                                    DistancePointSegmentSqr(
                                        point,
                                        segment.a,
                                        segment.b),
                                    segment.legacyHalfWidth);
                        }

                        if (!intersects)
                            continue;

                        chunk.SetSurface(
                            x,
                            z,
                            SurfaceKind.Riverbed);
                        break;
                    }
                }
            }
        }

        private static float ConservativePadding(
            float a,
            float b)
        {
            if (!IsFinite(a) || !IsFinite(b))
                return float.PositiveInfinity;

            return Mathf.Max(a, b);
        }

        private static bool RoadSegmentIntersects(
            float distanceSqr,
            float halfWidth)
        {
            float widthSqr = halfWidth * halfWidth;
            if (float.IsNaN(widthSqr) || float.IsPositiveInfinity(widthSqr))
                return true;

            return
                !float.IsNaN(distanceSqr) &&
                distanceSqr <= widthSqr;
        }

        private static bool RiverSegmentIntersects(
            float distanceSqr,
            float halfWidth)
        {
            float widthSqr = halfWidth * halfWidth;
            if (float.IsPositiveInfinity(widthSqr))
                return true;

            return distanceSqr <= widthSqr;
        }

        private static bool TryGetLocalBounds(
            Vector2 a,
            Vector2 b,
            float padding,
            float minX,
            float minZ,
            float maxX,
            float maxZ,
            out SegmentBounds bounds)
        {
            if (!IsFinite(a.x) ||
                !IsFinite(a.y) ||
                !IsFinite(b.x) ||
                !IsFinite(b.y) ||
                !IsFinite(padding))
            {
                bounds = new SegmentBounds
                {
                    minX = minX,
                    minZ = minZ,
                    maxX = maxX,
                    maxZ = maxZ
                };
                return true;
            }

            float segmentMinX = Mathf.Min(a.x, b.x) - padding;
            float segmentMinZ = Mathf.Min(a.y, b.y) - padding;
            float segmentMaxX = Mathf.Max(a.x, b.x) + padding;
            float segmentMaxZ = Mathf.Max(a.y, b.y) + padding;

            if (segmentMaxX < minX || segmentMinX > maxX ||
                segmentMaxZ < minZ || segmentMinZ > maxZ)
            {
                bounds = default(SegmentBounds);
                return false;
            }

            bounds = new SegmentBounds
            {
                minX = Mathf.Max(minX, segmentMinX),
                minZ = Mathf.Max(minZ, segmentMinZ),
                maxX = Mathf.Min(maxX, segmentMaxX),
                maxZ = Mathf.Min(maxZ, segmentMaxZ)
            };
            return true;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) &&
            !float.IsInfinity(value);

        private void ApplyRoadSurfaces(
            GenerationContext context,
            WorldChunkData chunk,
            MacroWorldPlan plan)
        {
            float cellSize =
                context.Settings.CellWorldSize;

            float chunkSize =
                context.Settings.ChunkWorldSize;

            float originX =
                chunk.Coordinate.x *
                chunkSize;

            float originZ =
                chunk.Coordinate.z *
                chunkSize;

            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    var point =
                        new Vector2(
                            originX +
                            (x + 0.5f) * cellSize,
                            originZ +
                            (z + 0.5f) * cellSize);

                    for (int r = 0; r < plan.Roads.Count; r++)
                    {
                        WorldRoadData road =
                            plan.Roads[r];

                        if (road == null ||
                            road.centerline.Count < 2)
                        {
                            continue;
                        }

                        float halfWidth =
                            Mathf.Max(
                                0.1f,
                                road.width * 0.5f +
                                roadSurfaceExtraWidth);

                        if (DistanceToPolylineSqr(
                                point,
                                road.centerline) >
                            halfWidth * halfWidth)
                        {
                            continue;
                        }

                        chunk.SetSurface(
                            x,
                            z,
                            GetRoadSurface(
                                road.roadKind));

                        break;
                    }
                }
            }
        }

        private void ApplyRiverSurfaces(
            GenerationContext context,
            WorldChunkData chunk,
            MacroWorldPlan plan)
        {
            float cellSize =
                context.Settings.CellWorldSize;

            float chunkSize =
                context.Settings.ChunkWorldSize;

            float originX =
                chunk.Coordinate.x *
                chunkSize;

            float originZ =
                chunk.Coordinate.z *
                chunkSize;

            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    var point =
                        new Vector2(
                            originX +
                            (x + 0.5f) * cellSize,
                            originZ +
                            (z + 0.5f) * cellSize);

                    for (int r = 0; r < plan.Rivers.Count; r++)
                    {
                        WorldRiverData river =
                            plan.Rivers[r];

                        if (river == null ||
                            river.centerline.Count < 2)
                        {
                            continue;
                        }

                        bool intersects;

                        if (useVariableRiverWidth)
                        {
                            intersects =
                                IsInsideProfileRiverbed(
                                    point,
                                    river,
                                    riverbedExtraWidth);
                        }
                        else
                        {
                            // Exact legacy-off branch: intentionally uses
                            // constant nominalWidth for older saved worlds.
                            float halfWidth =
                                Mathf.Max(
                                    0.1f,
                                    river.nominalWidth * 0.5f +
                                    riverbedExtraWidth);

                            intersects =
                                DistanceToPolylineSqr(
                                    point,
                                    river.centerline) <=
                                halfWidth * halfWidth;
                        }

                        if (intersects)
                        {
                            chunk.SetSurface(
                                x,
                                z,
                                SurfaceKind.Riverbed);

                            break;
                        }
                    }
                }
            }
        }

        private static bool IsInsideProfileRiverbed(
            Vector2 point,
            WorldRiverData river,
            float extraWidth)
        {
            // Evaluate every segment's interpolated channel width. Choosing
            // only the nearest centerline point can miss the wider side of
            // a sharp bend or confluence transition.
            float padding = Mathf.Max(0f, extraWidth);

            for (int i = 0; i < river.centerline.Count - 1; i++)
            {
                Vector2 a = river.centerline[i];
                Vector2 b = river.centerline[i + 1];
                Vector2 delta = b - a;
                float squaredLength = delta.sqrMagnitude;
                if (squaredLength <= 0.000001f)
                    continue;

                float t = Mathf.Clamp01(
                    Vector2.Dot(point - a, delta) / squaredLength);
                Vector2 nearest = a + delta * t;
                float halfWidth = Mathf.Max(0.1f,
                    river.GetWidthAtSegment(i, t) * 0.5f + padding);

                if ((point - nearest).sqrMagnitude <= halfWidth * halfWidth)
                    return true;
            }
            return false;
        }

        private static SurfaceKind GetRoadSurface(
            RoadKind roadKind)
        {
            switch (roadKind)
            {
                case RoadKind.Trail:
                    return SurfaceKind.Trail;
                case RoadKind.ImprovedRoad:
                    return SurfaceKind.ImprovedRoad;
                case RoadKind.SettlementStreet:
                    return SurfaceKind.SettlementStreet;
                case RoadKind.FortifiedRoute:
                    return SurfaceKind.FortifiedRoad;
                default:
                    return SurfaceKind.DirtRoad;
            }
        }

        private static float DistanceToPolylineSqr(
            Vector2 point,
            IReadOnlyList<Vector2> points)
        {
            float best =
                float.PositiveInfinity;

            for (int i = 0; i < points.Count - 1; i++)
            {
                float distance =
                    DistancePointSegmentSqr(
                        point,
                        points[i],
                        points[i + 1]);

                if (distance < best)
                    best = distance;
            }

            return best;
        }

        private static float DistancePointSegmentSqr(
            Vector2 point,
            Vector2 a,
            Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;

            if (lengthSqr <= 0.000001f)
                return (point - a).sqrMagnitude;

            float t =
                Mathf.Clamp01(
                    Vector2.Dot(
                        point - a,
                        ab) /
                    lengthSqr);

            Vector2 closest =
                a + ab * t;

            return
                (point - closest).sqrMagnitude;
        }
    }
}
