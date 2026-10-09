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

        [Header("Concept-only natural placement (opt in)")]
        [Tooltip("Honor the authoritative Grass placement-block bit on roads, " +
                 "bridge supports, rivers and settlement clearings. Legacy off.")]
        [SerializeField] private bool respectGrassPlacementBlocks;

        [Tooltip("Fade grass density on steep slopes without changing terrain " +
                 "height, SurfaceKind, or any resource/road placement.")]
        [SerializeField] private bool fadeGrassOnSteepSlopes;

        [Range(0f, 90f)]
        [SerializeField] private float slopeFadeStart = 20f;

        [Range(0f, 90f)]
        [SerializeField] private float slopeFadeEnd = 36f;

        public override void Generate(
            GenerationContext context,
            WorldChunkData chunk)
        {
            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    // These are *visual ground cover* samples, not
                    // one persistent spawn per blade. Placement clearance
                    // is authoritative and already projected from macro
                    // settlements, roads, rivers and bridge footprints.
                    if (respectGrassPlacementBlocks &&
                        chunk.IsPlacementBlocked(
                            x, z, PlacementBlockFlags.Grass))
                    {
                        chunk.SetGrassDensity(x, z, 0f);
                        continue;
                    }

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

                    if (fadeGrassOnSteepSlopes)
                    {
                        float slope = chunk.GetCellSlope(x, z);
                        float start = Mathf.Clamp(
                            slopeFadeStart, 0f, 90f);
                        float end = Mathf.Max(
                            start + 0.001f, slopeFadeEnd);
                        // Below start retains legacy coverage; above end
                        // becomes bare ground. Rock base surface remains
                        // unconditionally zero in either mode.
                        density *= 1f - Mathf.SmoothStep(
                            0f, 1f, Mathf.InverseLerp(
                                start, end, slope));
                    }

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
