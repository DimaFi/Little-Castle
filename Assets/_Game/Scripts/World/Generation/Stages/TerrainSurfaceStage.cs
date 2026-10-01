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

                        float halfWidth =
                            Mathf.Max(
                                0.1f,
                                river.nominalWidth * 0.5f +
                                riverbedExtraWidth);

                        if (DistanceToPolylineSqr(
                                point,
                                river.centerline) <=
                            halfWidth * halfWidth)
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
