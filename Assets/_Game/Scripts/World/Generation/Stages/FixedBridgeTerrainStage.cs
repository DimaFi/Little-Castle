using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Applies authored fixed-site terrain after river carving and before
    /// terrain analysis. Every sample is evaluated in absolute world space,
    /// so neighboring and negative chunks agree at shared borders.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FixedBridgeTerrainStage",
        menuName = "Little Castle/World/Generation/Fixed Bridge Terrain Stage")]
    public sealed class FixedBridgeTerrainStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.TerrainModification;

        public override void Generate(
            GenerationContext context,
            WorldChunkData chunk)
        {
            MacroWorldPlan plan = context.MacroPlan;

            if (plan == null ||
                plan.BridgeSites.Count == 0)
            {
                return;
            }

            float chunkSize = context.Settings.ChunkWorldSize;
            float cellSize = context.Settings.CellWorldSize;
            float originX = chunk.Coordinate.x * chunkSize;
            float originZ = chunk.Coordinate.z * chunkSize;
            float maxX = originX + chunkSize;
            float maxZ = originZ + chunkSize;

            for (int i = 0; i < plan.BridgeSites.Count; i++)
            {
                WorldBridgeSiteData bridge = plan.BridgeSites[i];

                if (!FixedBridgeSiteProfile.IsSupported(bridge))
                    continue;

                FixedBridgeSiteProfile.GetWorldAabbHalfExtents(
                    bridge.yawDegrees,
                    out float halfExtentX,
                    out float halfExtentZ);

                if (bridge.worldPosition.x + halfExtentX < originX ||
                    bridge.worldPosition.x - halfExtentX > maxX ||
                    bridge.worldPosition.y + halfExtentZ < originZ ||
                    bridge.worldPosition.y - halfExtentZ > maxZ)
                {
                    continue;
                }

                int minSampleX =
                    Mathf.Clamp(
                        Mathf.FloorToInt(
                            (bridge.worldPosition.x - halfExtentX - originX) /
                            cellSize),
                        0,
                        chunk.CellsPerSide);

                int maxSampleX =
                    Mathf.Clamp(
                        Mathf.CeilToInt(
                            (bridge.worldPosition.x + halfExtentX - originX) /
                            cellSize),
                        0,
                        chunk.CellsPerSide);

                int minSampleZ =
                    Mathf.Clamp(
                        Mathf.FloorToInt(
                            (bridge.worldPosition.y - halfExtentZ - originZ) /
                            cellSize),
                        0,
                        chunk.CellsPerSide);

                int maxSampleZ =
                    Mathf.Clamp(
                        Mathf.CeilToInt(
                            (bridge.worldPosition.y + halfExtentZ - originZ) /
                            cellSize),
                        0,
                        chunk.CellsPerSide);

                for (int z = minSampleZ;
                     z <= maxSampleZ;
                     z++)
                {
                    float worldZ = originZ + z * cellSize;

                    for (int x = minSampleX;
                         x <= maxSampleX;
                         x++)
                    {
                        float worldX = originX + x * cellSize;

                        FixedBridgeSiteProfile.SampleWorld(
                            worldX,
                            worldZ,
                            bridge.worldPosition,
                            bridge.yawDegrees,
                            bridge.baseElevation,
                            out float targetHeight,
                            out float weight);

                        if (weight <= 0f)
                            continue;

                        chunk.SetHeight(
                            x,
                            z,
                            Mathf.Lerp(
                                chunk.GetHeight(x, z),
                                targetHeight,
                                weight));
                    }
                }
            }
        }
    }
}
