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

        public override void Generate(
            GenerationContext context,
            WorldChunkData chunk)
        {
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
