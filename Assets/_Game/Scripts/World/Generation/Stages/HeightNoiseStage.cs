using UnityEngine;

namespace LittleCastle.World
{
    [CreateAssetMenu(
        fileName = "HeightNoiseStage",
        menuName = "Little Castle/World/Generation/Height Noise Stage")]
    public sealed class HeightNoiseStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.TerrainBase;
        [Min(0f)]
        [SerializeField] private float heightScale = 12f;

        [Min(0.000001f)]
        [SerializeField] private float frequency = 0.0125f;

        [Range(1, 10)]
        [SerializeField] private int octaves = 4;

        [Min(1f)]
        [SerializeField] private float lacunarity = 2f;

        [Range(0f, 1f)]
        [SerializeField] private float persistence = 0.5f;

        [SerializeField]
        private AnimationCurve heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public override void Generate(GenerationContext context, WorldChunkData chunk)
        {
            float cellSize = context.Settings.CellWorldSize;
            float chunkSize = context.Settings.ChunkWorldSize;
            float originX = chunk.Coordinate.x * chunkSize;
            float originZ = chunk.Coordinate.z * chunkSize;

            for (int z = 0; z < chunk.SamplesPerSide; z++)
            {
                for (int x = 0; x < chunk.SamplesPerSide; x++)
                {
                    float worldX = originX + x * cellSize;
                    float worldZ = originZ + z * cellSize;

                    float noise = DeterministicNoise.FractalValueNoise(
                        context.WorldSeed,
                        worldX,
                        worldZ,
                        octaves,
                        frequency,
                        lacunarity,
                        persistence);

                    float shaped = heightCurve != null ? heightCurve.Evaluate(noise) : noise;
                    chunk.SetHeight(x, z, shaped * heightScale);
                }
            }
        }
    }
}
