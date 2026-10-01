using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Carves simple river channels into base terrain using macro river lines.
    ///
    /// Run after the base height stage and before TerrainClassificationStage.
    /// This is an early geometric trench, not final erosion/hydrology.
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

        public override void Generate(
            GenerationContext context,
            WorldChunkData chunk)
        {
            MacroWorldPlan plan = context.MacroPlan;

            if (plan == null || plan.Rivers.Count == 0)
                return;

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

            for (int z = 0; z < chunk.SamplesPerSide; z++)
            {
                for (int x = 0; x < chunk.SamplesPerSide; x++)
                {
                    var worldPoint =
                        new Vector2(
                            originX + x * cellSize,
                            originZ + z * cellSize);

                    float maxCarve = 0f;

                    for (int r = 0; r < plan.Rivers.Count; r++)
                    {
                        WorldRiverData river =
                            plan.Rivers[r];

                        if (river == null ||
                            river.centerline.Count < 2)
                        {
                            continue;
                        }

                        float influenceRadius =
                            Mathf.Max(
                                0.5f,
                                river.nominalWidth * 0.5f +
                                bankFalloff);

                        float distance =
                            DistanceToPolyline(
                                worldPoint,
                                river.centerline);

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
                                river.nominalDepth) *
                            Mathf.Max(
                                0f,
                                depthMultiplier) *
                            profile;

                        maxCarve =
                            Mathf.Max(
                                maxCarve,
                                carve);
                    }

                    if (maxCarve > 0f)
                    {
                        chunk.SetHeight(
                            x,
                            z,
                            chunk.GetHeight(x, z) -
                            maxCarve);
                    }
                }
            }
        }

        private static float DistanceToPolyline(
            Vector2 point,
            IReadOnlyList<Vector2> points)
        {
            float bestSqr =
                float.PositiveInfinity;

            for (int i = 0; i < points.Count - 1; i++)
            {
                float distanceSqr =
                    DistancePointSegmentSqr(
                        point,
                        points[i],
                        points[i + 1]);

                if (distanceSqr < bestSqr)
                    bestSqr = distanceSqr;
            }

            return Mathf.Sqrt(bestSqr);
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
