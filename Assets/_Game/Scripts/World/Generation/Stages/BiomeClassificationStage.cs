using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Converts terrain + climate fields into coarse gameplay biomes.
    /// The classification is intentionally simple and data-oriented.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BiomeClassificationStage",
        menuName = "Little Castle/World/Generation/Biome Classification Stage")]
    public sealed class BiomeClassificationStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.Biome;

        [Range(0f, 1f)]
        [SerializeField] private float woodlandMoisture = 0.48f;

        [Range(0f, 1f)]
        [SerializeField] private float wetLowlandMoisture = 0.72f;

        [Range(0f, 1f)]
        [SerializeField] private float highlandMeadowMoisture = 0.4f;

        [Range(0f, 1f)]
        [SerializeField] private float coldRockyTemperature = 0.32f;

        public override void Generate(
            GenerationContext context,
            WorldChunkData chunk)
        {
            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    TerrainClass terrain =
                        chunk.GetTerrainClass(x, z);

                    float moisture =
                        chunk.GetMoisture(x, z);

                    float temperature =
                        chunk.GetTemperature(x, z);

                    BiomeKind biome;

                    if (terrain == TerrainClass.Steep)
                    {
                        biome = BiomeKind.RockyHighland;
                    }
                    else if (terrain == TerrainClass.Highlands)
                    {
                        biome =
                            moisture >= highlandMeadowMoisture &&
                            temperature > coldRockyTemperature
                                ? BiomeKind.HighlandMeadow
                                : BiomeKind.RockyHighland;
                    }
                    else if (
                        terrain == TerrainClass.Plains &&
                        moisture >= wetLowlandMoisture)
                    {
                        biome = BiomeKind.WetLowland;
                    }
                    else if (moisture >= woodlandMoisture)
                    {
                        biome = BiomeKind.TemperateWoodland;
                    }
                    else
                    {
                        biome = BiomeKind.TemperateGrassland;
                    }

                    chunk.SetBiome(
                        x,
                        z,
                        biome);
                }
            }
        }
    }
}
