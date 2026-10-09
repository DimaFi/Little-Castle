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

        public override void Generate(
            GenerationContext context,
            WorldChunkData chunk)
        {
            AssignBaseSurface(chunk);

            MacroWorldPlan plan = context.MacroPlan;

            if (plan == null)
                return;

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
