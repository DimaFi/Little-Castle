using UnityEngine;

namespace LittleCastle.World
{
    [CreateAssetMenu(
        fileName = "MacroFeatureProjectionStage",
        menuName = "Little Castle/World/Generation/Macro Feature Projection Stage")]
    public sealed class MacroFeatureProjectionStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.MacroProjection;

        [Header("Point features")]
        [SerializeField]
        private PlacementBlockFlags settlementBlocks =
            PlacementBlockFlags.All;

        [SerializeField]
        private PlacementBlockFlags ruinBlocks =
            PlacementBlockFlags.Trees |
            PlacementBlockFlags.Resources |
            PlacementBlockFlags.LargeObjects;

        [SerializeField]
        private PlacementBlockFlags landmarkBlocks =
            PlacementBlockFlags.Trees |
            PlacementBlockFlags.LargeObjects;

        [Header("Paths")]
        [Min(0f)]
        [SerializeField] private float roadClearance = 2f;

        [Min(0f)]
        [SerializeField] private float riverClearance = 3f;

        [Header("Bridges")]
        [Min(0f)]
        [SerializeField] private float bridgeHeightOffset = 0.15f;

        public override void Generate(
            GenerationContext context,
            WorldChunkData chunk)
        {
            MacroWorldPlan plan = context.MacroPlan;

            if (plan == null)
                return;

            ProjectPointFeatures(context, chunk, plan);
            ProjectRoadMasks(context, chunk, plan);
            ProjectRiverMasks(context, chunk, plan);
            ProjectBridgeSites(context, chunk, plan);
        }

        private void ProjectPointFeatures(
            GenerationContext context,
            WorldChunkData chunk,
            MacroWorldPlan plan)
        {
            float chunkSize = context.Settings.ChunkWorldSize;
            float minX = chunk.Coordinate.x * chunkSize;
            float minZ = chunk.Coordinate.z * chunkSize;
            float maxX = minX + chunkSize;
            float maxZ = minZ + chunkSize;

            for (int i = 0; i < plan.PointFeatures.Count; i++)
            {
                WorldPointFeatureData feature =
                    plan.PointFeatures[i];

                if (!CircleOverlapsRect(
                    feature.worldPosition,
                    feature.influenceRadius,
                    minX,
                    minZ,
                    maxX,
                    maxZ))
                {
                    continue;
                }

                PlacementBlockFlags blockFlags =
                    GetPointFeatureBlockFlags(feature.kind);

                if (blockFlags != PlacementBlockFlags.None)
                {
                    WorldPlacementMaskUtility.BlockCircle(
                        chunk,
                        context.Settings,
                        feature.worldPosition,
                        feature.influenceRadius,
                        blockFlags);
                }

                if (!PointBelongsToChunk(
                    feature.worldPosition,
                    minX,
                    minZ,
                    maxX,
                    maxZ) ||
                    string.IsNullOrWhiteSpace(
                        feature.archetypeId))
                {
                    continue;
                }

                float height = WorldChunkSampling.SampleHeight(
                    chunk,
                    context.Settings,
                    feature.worldPosition.x,
                    feature.worldPosition.y);

                chunk.AddSpawn(
                    new WorldSpawnData(
                        feature.stableId,
                        feature.archetypeId,
                        GetSpawnCategory(feature.kind),
                        new Vector3(
                            feature.worldPosition.x,
                            height,
                            feature.worldPosition.y),
                        StableYaw(feature.stableId),
                        1f));
            }
        }

        private void ProjectRoadMasks(
            GenerationContext context,
            WorldChunkData chunk,
            MacroWorldPlan plan)
        {
            for (int i = 0; i < plan.Roads.Count; i++)
            {
                WorldRoadData road = plan.Roads[i];

                if (road == null || road.centerline.Count < 2)
                    continue;

                WorldPlacementMaskUtility.BlockPolyline(
                    chunk,
                    context.Settings,
                    road.centerline,
                    Mathf.Max(
                        0f,
                        road.width * 0.5f + roadClearance),
                    PlacementBlockFlags.Trees |
                    PlacementBlockFlags.LargeObjects |
                    PlacementBlockFlags.Resources);
            }
        }

        private void ProjectRiverMasks(
            GenerationContext context,
            WorldChunkData chunk,
            MacroWorldPlan plan)
        {
            for (int i = 0; i < plan.Rivers.Count; i++)
            {
                WorldRiverData river = plan.Rivers[i];

                if (river == null || river.centerline.Count < 2)
                    continue;

                if (river.widths != null &&
                    river.widths.Count == river.centerline.Count)
                {
                    WorldPlacementMaskUtility.BlockVariableWidthPolyline(
                        chunk,
                        context.Settings,
                        river.centerline,
                        river.widths,
                        riverClearance,
                        PlacementBlockFlags.All);
                }
                else
                {
                    WorldPlacementMaskUtility.BlockPolyline(
                        chunk,
                        context.Settings,
                        river.centerline,
                        Mathf.Max(
                            0f,
                            river.nominalWidth * 0.5f +
                            riverClearance),
                        PlacementBlockFlags.All);
                }
            }
        }

        private void ProjectBridgeSites(
            GenerationContext context,
            WorldChunkData chunk,
            MacroWorldPlan plan)
        {
            float chunkSize = context.Settings.ChunkWorldSize;
            float minX = chunk.Coordinate.x * chunkSize;
            float minZ = chunk.Coordinate.z * chunkSize;
            float maxX = minX + chunkSize;
            float maxZ = minZ + chunkSize;

            for (int i = 0; i < plan.BridgeSites.Count; i++)
            {
                WorldBridgeSiteData bridge =
                    plan.BridgeSites[i];

                if (string.IsNullOrWhiteSpace(
                        bridge.archetypeId) ||
                    !PointBelongsToChunk(
                        bridge.worldPosition,
                        minX,
                        minZ,
                        maxX,
                        maxZ))
                {
                    continue;
                }

                float height = WorldChunkSampling.SampleHeight(
                    chunk,
                    context.Settings,
                    bridge.worldPosition.x,
                    bridge.worldPosition.y);

                chunk.AddSpawn(
                    new WorldSpawnData(
                        bridge.stableId,
                        bridge.archetypeId,
                        SpawnCategory.Structure,
                        new Vector3(
                            bridge.worldPosition.x,
                            height + bridgeHeightOffset,
                            bridge.worldPosition.y),
                        bridge.yawDegrees,
                        1f));
            }
        }

        private PlacementBlockFlags GetPointFeatureBlockFlags(
            WorldFeatureKind kind)
        {
            switch (kind)
            {
                case WorldFeatureKind.NeutralSettlement:
                    return settlementBlocks;
                case WorldFeatureKind.Ruin:
                    return ruinBlocks;
                case WorldFeatureKind.Landmark:
                    return landmarkBlocks;
                default:
                    return PlacementBlockFlags.None;
            }
        }

        private static SpawnCategory GetSpawnCategory(
            WorldFeatureKind kind)
        {
            switch (kind)
            {
                case WorldFeatureKind.NeutralSettlement:
                    return SpawnCategory.Structure;
                case WorldFeatureKind.Ruin:
                    return SpawnCategory.RuinProp;
                case WorldFeatureKind.Signpost:
                    return SpawnCategory.Sign;
                case WorldFeatureKind.Landmark:
                    return SpawnCategory.Landmark;
                default:
                    return SpawnCategory.Decoration;
            }
        }

        private static float StableYaw(long stableId)
        {
            ulong value = unchecked((ulong)stableId);
            return (value % 36000UL) / 100f;
        }

        private static bool PointBelongsToChunk(
            Vector2 point,
            float minX,
            float minZ,
            float maxX,
            float maxZ)
        {
            return
                point.x >= minX &&
                point.x < maxX &&
                point.y >= minZ &&
                point.y < maxZ;
        }

        private static bool CircleOverlapsRect(
            Vector2 center,
            float radius,
            float minX,
            float minZ,
            float maxX,
            float maxZ)
        {
            float closestX =
                Mathf.Clamp(center.x, minX, maxX);

            float closestZ =
                Mathf.Clamp(center.y, minZ, maxZ);

            float dx = center.x - closestX;
            float dz = center.y - closestZ;
            float safeRadius = Mathf.Max(0f, radius);

            return
                dx * dx + dz * dz <=
                safeRadius * safeRadius;
        }
    }
}
