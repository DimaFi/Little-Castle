using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Recommended terrain-height stage for the early project.
    ///
    /// Produces broad regions (plains -> hills -> mountainous areas) from
    /// absolute world coordinates, then adds medium and small detail.
    /// It intentionally does not generate rivers, roads, forests or settlements.
    /// Those belong to separate systems.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LayeredTerrainStage",
        menuName = "Little Castle/World/Generation/Layered Terrain Stage")]
    public sealed class LayeredTerrainStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.TerrainBase;

        [Header("Optional concept-world landforms")]
        [Tooltip(
            "Uses broad buildable meadows, rounded hill belts and selected " +
            "terraced highlands. Disabled preserves the original generator.")]
        [SerializeField] private bool useStylizedLandforms;

        [SerializeField]
        private StylizedLandformSettings stylizedLandforms =
            new StylizedLandformSettings();

        public bool UseStylizedLandforms => useStylizedLandforms;

        public StylizedLandformSettings StylizedLandforms =>
            stylizedLandforms;

        [Header("Base")]
        [SerializeField] private float baseHeight = 2f;

        [Header("Regional relief mask")]
        [Min(0.000001f)]
        [SerializeField] private float regionFrequency = 0.0008f;

        [Range(1, 8)]
        [SerializeField] private int regionOctaves = 3;

        [SerializeField]
        private AnimationCurve regionReliefCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Rolling terrain")]
        [Min(0.000001f)]
        [SerializeField] private float hillFrequency = 0.006f;

        [Min(0f)]
        [SerializeField] private float hillAmplitude = 7f;

        [Range(1, 8)]
        [SerializeField] private int hillOctaves = 4;

        [Header("Mountain ridges")]
        [Min(0.000001f)]
        [SerializeField] private float mountainFrequency = 0.0022f;

        [Min(0f)]
        [SerializeField] private float mountainAmplitude = 34f;

        [Range(1, 8)]
        [SerializeField] private int mountainOctaves = 4;

        [Range(0.5f, 6f)]
        [SerializeField] private float ridgeSharpness = 2.2f;

        [Header("Fine detail")]
        [Min(0.000001f)]
        [SerializeField] private float detailFrequency = 0.028f;

        [Min(0f)]
        [SerializeField] private float detailAmplitude = 1.8f;

        [Range(1, 6)]
        [SerializeField] private int detailOctaves = 2;

        [Header("Common fractal settings")]
        [Min(1f)]
        [SerializeField] private float lacunarity = 2f;

        [Range(0f, 1f)]
        [SerializeField] private float persistence = 0.5f;

        public override void Generate(GenerationContext context, WorldChunkData chunk)
        {
            float cellSize = context.Settings.CellWorldSize;
            float chunkSize = context.Settings.ChunkWorldSize;
            float originX = chunk.Coordinate.x * chunkSize;
            float originZ = chunk.Coordinate.z * chunkSize;

            if (useStylizedLandforms)
            {
                GenerateStylized(
                    context,
                    chunk,
                    cellSize,
                    originX,
                    originZ);
                return;
            }

            int regionSeed = DeterministicNoise.Hash(context.WorldSeed, 1001, 0x11);
            int hillSeed = DeterministicNoise.Hash(context.WorldSeed, 1002, 0x22);
            int mountainSeed = DeterministicNoise.Hash(context.WorldSeed, 1003, 0x33);
            int detailSeed = DeterministicNoise.Hash(context.WorldSeed, 1004, 0x44);

            for (int z = 0; z < chunk.SamplesPerSide; z++)
            {
                for (int x = 0; x < chunk.SamplesPerSide; x++)
                {
                    float worldX = originX + x * cellSize;
                    float worldZ = originZ + z * cellSize;

                    float region = DeterministicNoise.FractalValueNoise(
                        regionSeed,
                        worldX,
                        worldZ,
                        regionOctaves,
                        regionFrequency,
                        lacunarity,
                        persistence);

                    float reliefMask = regionReliefCurve != null
                        ? Mathf.Clamp01(regionReliefCurve.Evaluate(region))
                        : Mathf.Clamp01(region);

                    float hills = DeterministicNoise.FractalValueNoise(
                        hillSeed,
                        worldX,
                        worldZ,
                        hillOctaves,
                        hillFrequency,
                        lacunarity,
                        persistence);

                    // Center around zero so hills can raise and lower the local surface.
                    float hillOffset = (hills - 0.5f) * 2f * hillAmplitude;

                    float mountainNoise = DeterministicNoise.FractalValueNoise(
                        mountainSeed,
                        worldX,
                        worldZ,
                        mountainOctaves,
                        mountainFrequency,
                        lacunarity,
                        persistence);

                    // Ridge transform: 0 at valleys, 1 at ridge centers.
                    float ridge = 1f - Mathf.Abs(mountainNoise * 2f - 1f);
                    ridge = Mathf.Pow(Mathf.Clamp01(ridge), ridgeSharpness);

                    float mountainOffset = ridge * mountainAmplitude * reliefMask;

                    float detail = DeterministicNoise.FractalValueNoise(
                        detailSeed,
                        worldX,
                        worldZ,
                        detailOctaves,
                        detailFrequency,
                        lacunarity,
                        persistence);

                    float detailOffset = (detail - 0.5f) * 2f * detailAmplitude;

                    // Keep broad plains calmer while allowing more variation in rough regions.
                    float localHillStrength = Mathf.Lerp(0.45f, 1f, reliefMask);
                    float height =
                        baseHeight +
                        hillOffset * localHillStrength +
                        mountainOffset +
                        detailOffset;

                    chunk.SetHeight(x, z, height);
                }
            }
        }

        private void GenerateStylized(
            GenerationContext context,
            WorldChunkData chunk,
            float cellSize,
            float originX,
            float originZ)
        {
            StylizedLandformSettings resolvedSettings =
                stylizedLandforms ?? new StylizedLandformSettings();

            for (int z = 0; z < chunk.SamplesPerSide; z++)
            {
                for (int x = 0; x < chunk.SamplesPerSide; x++)
                {
                    float worldX = originX + x * cellSize;
                    float worldZ = originZ + z * cellSize;

                    chunk.SetHeight(
                        x,
                        z,
                        StylizedLandformSampler.SampleHeight(
                            context.WorldSeed,
                            worldX,
                            worldZ,
                            resolvedSettings));
                }
            }
        }
    }
}
