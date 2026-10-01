using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Derives gameplay-friendly terrain metadata from generated heights.
    ///
    /// Run this after the height-producing stage.
    /// Classification is stored per terrain cell rather than per mesh vertex.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TerrainClassificationStage",
        menuName = "Little Castle/World/Generation/Terrain Classification Stage")]
    public sealed class TerrainClassificationStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.TerrainAnalysis;
        [Header("Slope thresholds (degrees)")]
        [Range(0f, 45f)]
        [SerializeField] private float rollingSlope = 5f;

        [Range(0f, 60f)]
        [SerializeField] private float steepSlope = 18f;

        [Header("Highland threshold")]
        [SerializeField] private float highlandHeight = 22f;

        public override void Generate(GenerationContext context, WorldChunkData chunk)
        {
            float cellSize = context.Settings.CellWorldSize;

            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    float h00 = chunk.GetHeight(x, z);
                    float h10 = chunk.GetHeight(x + 1, z);
                    float h01 = chunk.GetHeight(x, z + 1);
                    float h11 = chunk.GetHeight(x + 1, z + 1);

                    float left = (h00 + h01) * 0.5f;
                    float right = (h10 + h11) * 0.5f;
                    float bottom = (h00 + h10) * 0.5f;
                    float top = (h01 + h11) * 0.5f;

                    float dx = (right - left) / cellSize;
                    float dz = (top - bottom) / cellSize;
                    float slopeDegrees =
                        Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;

                    float averageHeight = (h00 + h10 + h01 + h11) * 0.25f;

                    TerrainClass terrainClass;

                    if (averageHeight >= highlandHeight)
                        terrainClass = TerrainClass.Highlands;
                    else if (slopeDegrees >= steepSlope)
                        terrainClass = TerrainClass.Steep;
                    else if (slopeDegrees >= rollingSlope)
                        terrainClass = TerrainClass.RollingHills;
                    else
                        terrainClass = TerrainClass.Plains;

                    chunk.SetCellSlope(x, z, slopeDegrees);
                    chunk.SetTerrainClass(x, z, terrainClass);
                }
            }
        }
    }
}
