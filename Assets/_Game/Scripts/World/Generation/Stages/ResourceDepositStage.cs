using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class ResourceDepositRule
    {
        public string ruleId = "stone_surface_common";
        public ResourceKind resourceKind = ResourceKind.Stone;
        public string visualArchetypeId = "deposit_stone_01";

        [Header("Distribution")]
        [Min(2f)]
        public float spacing = 90f;

        [Range(0f, 1f)]
        public float chance = 0.3f;

        public TerrainClassMask allowedTerrain =
            TerrainClassMask.RollingHills |
            TerrainClassMask.Steep |
            TerrainClassMask.Highlands;

        public float minHeight = -1000f;
        public float maxHeight = 1000f;

        [Range(0f, 90f)]
        public float maxSlope = 38f;

        [Header("Deposit")]
        [Min(0.1f)]
        public float minRadius = 4f;

        [Min(0.1f)]
        public float maxRadius = 12f;

        [Range(0.01f, 1f)]
        public float minRichness = 0.25f;

        [Range(0.01f, 1f)]
        public float maxRichness = 1f;

        [Min(1)]
        public int minCapacity = 100;

        [Min(1)]
        public int maxCapacity = 1000;

        public bool createVisualSpawn = true;
    }

    [CreateAssetMenu(
        fileName = "ResourceDepositStage",
        menuName = "Little Castle/World/Generation/Resource Deposit Stage")]
    public sealed class ResourceDepositStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.Resources;
        [SerializeField]
        private List<ResourceDepositRule> rules =
            new List<ResourceDepositRule>();

        public override void Generate(GenerationContext context, WorldChunkData chunk)
        {
            float chunkSize = context.Settings.ChunkWorldSize;
            float minX = chunk.Coordinate.x * chunkSize;
            float minZ = chunk.Coordinate.z * chunkSize;
            float maxX = minX + chunkSize;
            float maxZ = minZ + chunkSize;

            for (int i = 0; i < rules.Count; i++)
            {
                ResourceDepositRule rule = rules[i];

                if (rule == null)
                    continue;

                GenerateRule(context, chunk, rule, minX, minZ, maxX, maxZ);
            }
        }

        private static void GenerateRule(
            GenerationContext context,
            WorldChunkData chunk,
            ResourceDepositRule rule,
            float minX,
            float minZ,
            float maxX,
            float maxZ)
        {
            float spacing = Mathf.Max(2f, rule.spacing);

            int minGridX = Mathf.FloorToInt(minX / spacing) - 1;
            int maxGridX = Mathf.FloorToInt(maxX / spacing) + 1;
            int minGridZ = Mathf.FloorToInt(minZ / spacing) - 1;
            int maxGridZ = Mathf.FloorToInt(maxZ / spacing) + 1;

            string stableRuleId = string.IsNullOrWhiteSpace(rule.ruleId)
                ? rule.resourceKind.ToString()
                : rule.ruleId;

            int salt = DeterministicHash.String32(stableRuleId);

            for (int gridZ = minGridZ; gridZ <= maxGridZ; gridZ++)
            {
                for (int gridX = minGridX; gridX <= maxGridX; gridX++)
                {
                    float worldX = (
                        gridX + DeterministicHash.Hash01(
                            context.WorldSeed,
                            gridX,
                            gridZ,
                            salt ^ 0x71)) * spacing;

                    float worldZ = (
                        gridZ + DeterministicHash.Hash01(
                            context.WorldSeed,
                            gridX,
                            gridZ,
                            salt ^ 0x72)) * spacing;

                    if (worldX < minX || worldX >= maxX ||
                        worldZ < minZ || worldZ >= maxZ)
                    {
                        continue;
                    }

                    float roll = DeterministicHash.Hash01(
                        context.WorldSeed,
                        gridX,
                        gridZ,
                        salt ^ 0x73);

                    if (roll > rule.chance)
                        continue;

                    PlacementBlockFlags placementBlocks =
                        WorldChunkSampling.SamplePlacementBlocks(
                            chunk,
                            context.Settings,
                            worldX,
                            worldZ);

                    if ((placementBlocks & PlacementBlockFlags.Resources) != 0)
                        continue;

                    float height = WorldChunkSampling.SampleHeight(
                        chunk,
                        context.Settings,
                        worldX,
                        worldZ);

                    if (height < rule.minHeight || height > rule.maxHeight)
                        continue;

                    float slope = WorldChunkSampling.SampleSlope(
                        chunk,
                        context.Settings,
                        worldX,
                        worldZ);

                    if (slope > rule.maxSlope)
                        continue;

                    TerrainClass terrainClass =
                        WorldChunkSampling.SampleTerrainClass(
                            chunk,
                            context.Settings,
                            worldX,
                            worldZ);

                    if (!rule.allowedTerrain.Contains(terrainClass))
                        continue;

                    float radiusT = DeterministicHash.Hash01(
                        context.WorldSeed,
                        gridX,
                        gridZ,
                        salt ^ 0x74);

                    float richnessT = DeterministicHash.Hash01(
                        context.WorldSeed,
                        gridX,
                        gridZ,
                        salt ^ 0x75);

                    float capacityT = DeterministicHash.Hash01(
                        context.WorldSeed,
                        gridX,
                        gridZ,
                        salt ^ 0x76);

                    float radius = Mathf.Lerp(
                        Mathf.Min(rule.minRadius, rule.maxRadius),
                        Mathf.Max(rule.minRadius, rule.maxRadius),
                        radiusT);

                    float richness = Mathf.Lerp(
                        Mathf.Min(rule.minRichness, rule.maxRichness),
                        Mathf.Max(rule.minRichness, rule.maxRichness),
                        richnessT);

                    int minCapacity = Mathf.Min(rule.minCapacity, rule.maxCapacity);
                    int maxCapacity = Mathf.Max(rule.minCapacity, rule.maxCapacity);

                    int capacity = Mathf.RoundToInt(
                        Mathf.Lerp(minCapacity, maxCapacity, capacityT));

                    long stableId = DeterministicHash.StableId(
                        context.WorldSeed,
                        gridX,
                        gridZ,
                        salt);

                    var position = new Vector3(worldX, height, worldZ);

                    chunk.AddResourceDeposit(
                        new WorldResourceDepositData(
                            stableId,
                            rule.resourceKind,
                            rule.visualArchetypeId,
                            position,
                            radius,
                            richness,
                            capacity));

                    if (rule.createVisualSpawn &&
                        !string.IsNullOrWhiteSpace(rule.visualArchetypeId))
                    {
                        float yaw = DeterministicHash.Hash01(
                            context.WorldSeed,
                            gridX,
                            gridZ,
                            salt ^ 0x77) * 360f;

                        chunk.AddSpawn(
                            new WorldSpawnData(
                                stableId,
                                rule.visualArchetypeId,
                                SpawnCategory.ResourceVisual,
                                position,
                                yaw,
                                1f));
                    }
                }
            }
        }
    }
}
