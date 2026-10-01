using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Generates high-density ground-cover fields.
    ///
    /// Grass density is authoritative visual/environment data, not one spawn
    /// record per blade. Rendering should use batching/instancing/terrain details.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GroundCoverStage",
        menuName = "Little Castle/World/Generation/Ground Cover Stage")]
    public sealed class GroundCoverStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.GroundCover;

        [Range(0f, 2f)]
        [SerializeField] private float densityMultiplier = 1f;

        [Range(0f, 1f)]
        [SerializeField] private float forestSuppression = 0.55f;

        [Range(0f, 1f)]
        [SerializeField] private float moistureInfluence = 0.25f;

        public override void Generate(
            GenerationContext context,
            WorldChunkData chunk)
        {
            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    SurfaceKind surface =
                        chunk.GetSurface(x, z);

                    float density =
                        GetBaseDensity(
                            chunk.GetBiome(x, z),
                            surface);

                    if (density <= 0f)
                    {
                        chunk.SetGrassDensity(x, z, 0f);
                        continue;
                    }

                    float moisture =
                        chunk.GetMoisture(x, z);

                    float moistureFactor =
                        Mathf.Lerp(
                            1f - moistureInfluence,
                            1f + moistureInfluence,
                            moisture);

                    float forestDensity =
                        chunk.GetForestDensity(x, z);

                    float forestFactor =
                        1f -
                        Mathf.Clamp01(forestDensity) *
                        forestSuppression;

                    density *=
                        moistureFactor *
                        forestFactor *
                        densityMultiplier;

                    chunk.SetGrassDensity(
                        x,
                        z,
                        Mathf.Clamp01(density));
                }
            }
        }

        private static float GetBaseDensity(
            BiomeKind biome,
            SurfaceKind surface)
        {
            switch (surface)
            {
                case SurfaceKind.Rock:
                case SurfaceKind.Riverbed:
                case SurfaceKind.Trail:
                case SurfaceKind.DirtRoad:
                case SurfaceKind.ImprovedRoad:
                case SurfaceKind.SettlementStreet:
                case SurfaceKind.FortifiedRoad:
                    return 0f;
            }

            switch (biome)
            {
                case BiomeKind.TemperateGrassland:
                    return 1f;
                case BiomeKind.TemperateWoodland:
                    return 0.6f;
                case BiomeKind.WetLowland:
                    return 0.75f;
                case BiomeKind.HighlandMeadow:
                    return 0.7f;
                case BiomeKind.RockyHighland:
                    return 0.12f;
                default:
                    return 0.45f;
            }
        }
    }
}
