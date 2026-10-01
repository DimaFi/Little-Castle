using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Generates broad normalized temperature/moisture fields.
    /// Values are gameplay inputs, not final weather simulation.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ClimateStage",
        menuName = "Little Castle/World/Generation/Climate Stage")]
    public sealed class ClimateStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.Climate;

        [Header("Temperature")]
        [Min(0.000001f)]
        [SerializeField] private float temperatureFrequency = 0.00065f;

        [Range(1, 6)]
        [SerializeField] private int temperatureOctaves = 3;

        [Range(0f, 1f)]
        [SerializeField] private float baseTemperature = 0.62f;

        [SerializeField] private float altitudeCoolingStart = 12f;

        [Min(0f)]
        [SerializeField] private float coolingPerHeightUnit = 0.008f;

        [Header("Moisture")]
        [Min(0.000001f)]
        [SerializeField] private float moistureFrequency = 0.00085f;

        [Range(1, 6)]
        [SerializeField] private int moistureOctaves = 3;

        [Range(0f, 1f)]
        [SerializeField] private float moistureBias = 0.5f;

        [Header("Fractal")]
        [Min(1f)]
        [SerializeField] private float lacunarity = 2f;

        [Range(0f, 1f)]
        [SerializeField] private float persistence = 0.5f;

        public override void Generate(
            GenerationContext context,
            WorldChunkData chunk)
        {
            float cellSize = context.Settings.CellWorldSize;
            float chunkSize = context.Settings.ChunkWorldSize;
            float originX = chunk.Coordinate.x * chunkSize;
            float originZ = chunk.Coordinate.z * chunkSize;

            int temperatureSeed =
                DeterministicHash.Hash32(
                    context.WorldSeed,
                    0x54454D50,
                    0x0001,
                    0x11);

            int moistureSeed =
                DeterministicHash.Hash32(
                    context.WorldSeed,
                    0x4D4F4953,
                    0x0002,
                    0x22);

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

                    float rawTemperature =
                        DeterministicNoise.FractalValueNoise(
                            temperatureSeed,
                            worldX,
                            worldZ,
                            temperatureOctaves,
                            temperatureFrequency,
                            lacunarity,
                            persistence);

                    float temperature =
                        Mathf.Clamp01(
                            baseTemperature +
                            (rawTemperature - 0.5f) * 0.7f);

                    float height =
                        WorldChunkSampling.SampleHeight(
                            chunk,
                            context.Settings,
                            worldX,
                            worldZ);

                    if (height > altitudeCoolingStart)
                    {
                        temperature -=
                            (height - altitudeCoolingStart) *
                            coolingPerHeightUnit;
                    }

                    float rawMoisture =
                        DeterministicNoise.FractalValueNoise(
                            moistureSeed,
                            worldX,
                            worldZ,
                            moistureOctaves,
                            moistureFrequency,
                            lacunarity,
                            persistence);

                    float moisture =
                        Mathf.Clamp01(
                            moistureBias +
                            (rawMoisture - 0.5f));

                    chunk.SetTemperature(
                        x,
                        z,
                        Mathf.Clamp01(temperature));

                    chunk.SetMoisture(
                        x,
                        z,
                        moisture);
                }
            }
        }
    }
}
