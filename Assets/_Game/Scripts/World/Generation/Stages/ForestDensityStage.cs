using UnityEngine;

namespace LittleCastle.World
{
    [CreateAssetMenu(
        fileName = "ForestDensityStage",
        menuName = "Little Castle/World/Generation/Forest Density Stage")]
    public sealed class ForestDensityStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.EnvironmentFields;

        [Header("Forest regions")]
        [Min(0.000001f)]
        [SerializeField] private float frequency = 0.0018f;

        [Range(1, 8)]
        [SerializeField] private int octaves = 3;

        [Min(1f)]
        [SerializeField] private float lacunarity = 2f;

        [Range(0f, 1f)]
        [SerializeField] private float persistence = 0.55f;

        [SerializeField]
        private AnimationCurve densityCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Terrain suitability")]
        [SerializeField]
        private TerrainClassMask allowedTerrain =
            TerrainClassMask.Plains |
            TerrainClassMask.RollingHills |
            TerrainClassMask.Highlands;

        [SerializeField]
        private BiomeMask allowedBiomes =
            BiomeMask.All;

        [SerializeField] private float minHeight = -1000f;
        [SerializeField] private float maxHeight = 1000f;

        [Range(0f, 90f)]
        [SerializeField] private float maxSlope = 32f;

        [Header("Climate coupling")]
        [SerializeField] private bool useMoisture = true;

        [Range(0f, 1f)]
        [SerializeField] private float minimumMoisture = 0.15f;

        [Range(0f, 1f)]
        [SerializeField] private float moistureInfluence = 0.7f;

        [Header("Density shaping")]
        [Range(0f, 1f)]
        [SerializeField] private float minimumRegionThreshold = 0.2f;

        [Range(0f, 2f)]
        [SerializeField] private float densityMultiplier = 1f;

        public override void Generate(
            GenerationContext context,
            WorldChunkData chunk)
        {
            float chunkSize = context.Settings.ChunkWorldSize;
            float cellSize = context.Settings.CellWorldSize;
            float originX = chunk.Coordinate.x * chunkSize;
            float originZ = chunk.Coordinate.z * chunkSize;

            int seed = DeterministicHash.Hash32(
                context.WorldSeed,
                0x46F04E57,
                0x44534E54,
                0x01);

            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    float worldX =
                        originX +
                        (x + 0.5f) * cellSize;

                    float worldZ =
                        originZ +
                        (z + 0.5f) * cellSize;

                    TerrainClass terrainClass =
                        chunk.GetTerrainClass(x, z);

                    if (!allowedTerrain.Contains(terrainClass))
                    {
                        chunk.SetForestDensity(x, z, 0f);
                        continue;
                    }

                    BiomeKind biome =
                        chunk.GetBiome(x, z);

                    if (!allowedBiomes.Contains(biome))
                    {
                        chunk.SetForestDensity(x, z, 0f);
                        continue;
                    }

                    float height =
                        WorldChunkSampling.SampleHeight(
                            chunk,
                            context.Settings,
                            worldX,
                            worldZ);

                    float slope =
                        chunk.GetCellSlope(x, z);

                    if (height < minHeight ||
                        height > maxHeight ||
                        slope > maxSlope)
                    {
                        chunk.SetForestDensity(x, z, 0f);
                        continue;
                    }

                    float noise =
                        DeterministicNoise.FractalValueNoise(
                            seed,
                            worldX,
                            worldZ,
                            octaves,
                            frequency,
                            lacunarity,
                            persistence);

                    if (noise <= minimumRegionThreshold)
                    {
                        chunk.SetForestDensity(x, z, 0f);
                        continue;
                    }

                    float normalized =
                        Mathf.InverseLerp(
                            minimumRegionThreshold,
                            1f,
                            noise);

                    float shaped =
                        densityCurve != null
                            ? densityCurve.Evaluate(normalized)
                            : normalized;

                    float climateMultiplier = 1f;

                    if (useMoisture)
                    {
                        float moisture =
                            chunk.GetMoisture(x, z);

                        float moisture01 =
                            Mathf.InverseLerp(
                                minimumMoisture,
                                1f,
                                moisture);

                        climateMultiplier =
                            Mathf.Lerp(
                                1f - moistureInfluence,
                                1f,
                                moisture01);
                    }

                    chunk.SetForestDensity(
                        x,
                        z,
                        Mathf.Clamp01(
                            shaped *
                            densityMultiplier *
                            climateMultiplier));
                }
            }
        }
    }
}
