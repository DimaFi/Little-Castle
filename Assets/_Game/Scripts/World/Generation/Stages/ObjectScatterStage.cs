using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class ScatterSpawnRule
    {
        public string ruleId = "tree_oak_common";
        public string archetypeId = "tree_oak_01";
        public SpawnCategory category = SpawnCategory.Tree;

        [Header("Distribution")]
        [Min(0.25f)]
        public float spacing = 5f;

        [Range(0f, 1f)]
        public float baseChance = 0.8f;

        [Range(0f, 0.49f)]
        public float borderJitter = 0.12f;

        [Header("Terrain filters")]
        public TerrainClassMask allowedTerrain =
            TerrainClassMask.Plains |
            TerrainClassMask.RollingHills;

        public float minHeight = -1000f;
        public float maxHeight = 1000f;

        [Range(0f, 90f)]
        public float maxSlope = 28f;

        [Header("Forest coupling")]
        public bool multiplyByForestDensity = true;

        [Range(0.1f, 4f)]
        public float forestDensityExponent = 1f;

        [Header("Transform")]
        [Min(0.01f)]
        public float minScale = 0.9f;

        [Min(0.01f)]
        public float maxScale = 1.1f;
    }

    [CreateAssetMenu(
        fileName = "ObjectScatterStage",
        menuName = "Little Castle/World/Generation/Object Scatter Stage")]
    public sealed class ObjectScatterStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.LocalSpawns;
        [SerializeField]
        private List<ScatterSpawnRule> rules = new List<ScatterSpawnRule>();

        public override void Generate(GenerationContext context, WorldChunkData chunk)
        {
            float chunkSize = context.Settings.ChunkWorldSize;
            float minX = chunk.Coordinate.x * chunkSize;
            float minZ = chunk.Coordinate.z * chunkSize;
            float maxX = minX + chunkSize;
            float maxZ = minZ + chunkSize;

            for (int i = 0; i < rules.Count; i++)
            {
                ScatterSpawnRule rule = rules[i];

                if (rule == null || string.IsNullOrWhiteSpace(rule.archetypeId))
                    continue;

                GenerateRule(context, chunk, rule, minX, minZ, maxX, maxZ);
            }
        }

        private static void GenerateRule(
            GenerationContext context,
            WorldChunkData chunk,
            ScatterSpawnRule rule,
            float minX,
            float minZ,
            float maxX,
            float maxZ)
        {
            float spacing = Mathf.Max(0.25f, rule.spacing);

            int minGridX = Mathf.FloorToInt(minX / spacing) - 1;
            int maxGridX = Mathf.FloorToInt(maxX / spacing) + 1;
            int minGridZ = Mathf.FloorToInt(minZ / spacing) - 1;
            int maxGridZ = Mathf.FloorToInt(maxZ / spacing) + 1;

            string stableRuleId = string.IsNullOrWhiteSpace(rule.ruleId)
                ? rule.archetypeId
                : rule.ruleId;

            int ruleSalt = DeterministicHash.String32(stableRuleId);
            float jitterMargin = Mathf.Clamp(rule.borderJitter, 0f, 0.49f);

            for (int gridZ = minGridZ; gridZ <= maxGridZ; gridZ++)
            {
                for (int gridX = minGridX; gridX <= maxGridX; gridX++)
                {
                    float jitterX = Mathf.Lerp(
                        jitterMargin,
                        1f - jitterMargin,
                        DeterministicHash.Hash01(
                            context.WorldSeed,
                            gridX,
                            gridZ,
                            ruleSalt ^ 0x11));

                    float jitterZ = Mathf.Lerp(
                        jitterMargin,
                        1f - jitterMargin,
                        DeterministicHash.Hash01(
                            context.WorldSeed,
                            gridX,
                            gridZ,
                            ruleSalt ^ 0x22));

                    float worldX = (gridX + jitterX) * spacing;
                    float worldZ = (gridZ + jitterZ) * spacing;

                    if (worldX < minX || worldX >= maxX ||
                        worldZ < minZ || worldZ >= maxZ)
                    {
                        continue;
                    }

                    PlacementBlockFlags categoryBlock =
                        PlacementBlockFlagsExtensions.ForCategory(
                            rule.category);

                    PlacementBlockFlags placementBlocks =
                        WorldChunkSampling.SamplePlacementBlocks(
                            chunk,
                            context.Settings,
                            worldX,
                            worldZ);

                    if ((placementBlocks & categoryBlock) != 0)
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

                    float chance = Mathf.Clamp01(rule.baseChance);

                    if (rule.multiplyByForestDensity)
                    {
                        float forestDensity =
                            WorldChunkSampling.SampleForestDensity(
                                chunk,
                                context.Settings,
                                worldX,
                                worldZ);

                        chance *= Mathf.Pow(
                            Mathf.Clamp01(forestDensity),
                            Mathf.Max(0.1f, rule.forestDensityExponent));
                    }

                    float roll = DeterministicHash.Hash01(
                        context.WorldSeed,
                        gridX,
                        gridZ,
                        ruleSalt ^ 0x33);

                    if (roll > chance)
                        continue;

                    float yaw = DeterministicHash.Hash01(
                        context.WorldSeed,
                        gridX,
                        gridZ,
                        ruleSalt ^ 0x44) * 360f;

                    float scaleT = DeterministicHash.Hash01(
                        context.WorldSeed,
                        gridX,
                        gridZ,
                        ruleSalt ^ 0x55);

                    float minScale = Mathf.Min(rule.minScale, rule.maxScale);
                    float maxScale = Mathf.Max(rule.minScale, rule.maxScale);
                    float scale = Mathf.Lerp(minScale, maxScale, scaleT);

                    long stableId = DeterministicHash.StableId(
                        context.WorldSeed,
                        gridX,
                        gridZ,
                        ruleSalt);

                    chunk.AddSpawn(
                        new WorldSpawnData(
                            stableId,
                            rule.archetypeId,
                            rule.category,
                            new Vector3(worldX, height, worldZ),
                            yaw,
                            scale));
                }
            }
        }
    }
}
