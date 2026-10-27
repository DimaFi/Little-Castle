using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class ScatterSpawnRule
    {
        public bool enabled = true;
        [Tooltip("Flowers use exact mesh slope, natural ground and smooth meadow patches.")]
        public bool flowerMeadow;
        [Min(1)] public float clearingRadius = 8;
        [Range(0,1)] public float isolatedDensity = .035f;
        [Range(0,1)] public float clearingDensity = .6f;
        [Min(.5f)] public float speciesGroupRadius = 2.4f;
        public string[] flowerVariants;
        public float[] flowerWeights;
        [Min(0)] public int maximumFlowersPerChunk = 600;
        [Min(0)] public float flowerClearance = .2f;
        public bool allowGrass = true, allowWoodlandFloor = true, allowMud;
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

        public BiomeMask allowedBiomes =
            BiomeMask.All;

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

        public IReadOnlyList<ScatterSpawnRule> Rules => rules;

        [Header("Concept-only natural prop distribution (opt in)")]
        [Tooltip("Avoid placing tree trunks on exposed rock, roads and riverbed " +
                 "cells even if the broad terrain/biome masks would allow them.")]
        [SerializeField] private bool restrictTreeSurfaces;

        [Tooltip("Bias non-resource decorative rock spawn rolls towards actual " +
                 "rock surfaces or hills; never raises density above the " +
                 "authored baseChance. Does not affect resource deposits.")]
        [SerializeField] private bool biasDecorativeRocksToSlopes;

        [Range(0f, 1f)]
        [SerializeField] private float flatRockChanceMultiplier = 0.25f;

        [Range(0f, 90f)]
        [SerializeField] private float rockSlopeBiasStart = 8f;

        [Range(0f, 90f)]
        [SerializeField] private float rockSlopeBiasFull = 30f;

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

                if (rule == null || !rule.enabled || string.IsNullOrWhiteSpace(rule.archetypeId))
                    continue;

                GenerateRule(context, chunk, rule, minX, minZ, maxX, maxZ);
            }
        }

        private void GenerateRule(
            GenerationContext context,
            WorldChunkData chunk,
            ScatterSpawnRule rule,
            float minX,
            float minZ,
            float maxX,
            float maxZ)
        {
            float spacing = Mathf.Max(0.25f, rule.spacing);
            var flowers=rule.flowerMeadow?new List<WorldSpawnData>():null;

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

                    float slope = rule.flowerMeadow ? Vector3.Angle(Vector3.up,
                        WorldChunkSampling.SampleTriangleNormal(chunk,context.Settings,worldX,worldZ)) : WorldChunkSampling.SampleSlope(
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

                    BiomeKind biome =
                        WorldChunkSampling.SampleBiome(
                            chunk,
                            context.Settings,
                            worldX,
                            worldZ);

                    if (!rule.allowedBiomes.Contains(biome))
                        continue;

                    if(rule.flowerMeadow)
                    {
                        SurfaceKind ground=WorldChunkSampling.SampleSurface(chunk,context.Settings,worldX,worldZ);
                        if(!(ground==SurfaceKind.Grass&&rule.allowGrass || ground==SurfaceKind.WoodlandFloor&&rule.allowWoodlandFloor || ground==SurfaceKind.Mud&&rule.allowMud))continue;
                        // A flower root requires both decoration and grass clearance.
                        if((placementBlocks&(PlacementBlockFlags.Decoration|PlacementBlockFlags.Grass))!=0)continue;
                        bool clear=true;
                        for(int edge=0;edge<4;edge++)
                        {
                            float angle=edge*Mathf.PI*.5f;
                            float px=worldX+Mathf.Cos(angle)*rule.flowerClearance,pz=worldZ+Mathf.Sin(angle)*rule.flowerClearance;
                            var blocks=WorldChunkSampling.SamplePlacementBlocks(chunk,context.Settings,px,pz);
                            var edgeSurface=WorldChunkSampling.SampleSurface(chunk,context.Settings,px,pz);
                            if((blocks&(PlacementBlockFlags.Decoration|PlacementBlockFlags.Grass))!=0 ||
                                !(edgeSurface==SurfaceKind.Grass&&rule.allowGrass||edgeSurface==SurfaceKind.WoodlandFloor&&rule.allowWoodlandFloor||edgeSurface==SurfaceKind.Mud&&rule.allowMud)){clear=false;break;}
                        }
                        if(!clear)continue;
                    }

                    // Legacy remains exactly as it was: do not do
                    // additional per-candidate surface sampling unless
                    // an opt-in category actually requires it.
                    bool needTreeSurface = restrictTreeSurfaces &&
                        rule.category == SpawnCategory.Tree;
                    bool needRockSurface = biasDecorativeRocksToSlopes &&
                        rule.category == SpawnCategory.Rock;
                    SurfaceKind surface = SurfaceKind.Unknown;
                    if (needTreeSurface || needRockSurface)
                        surface = WorldChunkSampling.SampleSurface(
                            chunk, context.Settings, worldX, worldZ);

                    if (needTreeSurface && !IsNaturalTreeSurface(surface))
                        continue;

                    float chance = Mathf.Clamp01(rule.baseChance);
                    if(rule.flowerMeadow)
                    {
                        float field=MeadowNoise(context.WorldSeed,worldX/Mathf.Max(1,rule.clearingRadius),worldZ/Mathf.Max(1,rule.clearingRadius),ruleSalt);
                        chance*=Mathf.Lerp(rule.isolatedDensity,rule.clearingDensity,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.75f,field)));
                    }

                    if (needRockSurface)
                    {
                        // No new rocks or increased population: solely
                        // shift existing chance toward true exposed stone
                        // and steep slopes, retaining some flatland props.
                        float rockAffinity =
                            surface == SurfaceKind.Rock
                                ? 1f
                                : Mathf.Lerp(
                                    Mathf.Clamp01(flatRockChanceMultiplier),
                                    1f,
                                    Mathf.SmoothStep(
                                        0f, 1f,
                                        Mathf.InverseLerp(
                                            Mathf.Clamp(
                                                rockSlopeBiasStart, 0f, 90f),
                                            Mathf.Max(
                                                rockSlopeBiasStart + 0.001f,
                                                rockSlopeBiasFull),
                                            slope)));
                        // Exactly zero affinity should never spawn a
                        // decorative prop, including the rare Hash01 == 0
                        // case. This condition exists only in opt-in mode.
                        if (rockAffinity <= 0f)
                            continue;
                        chance *= rockAffinity;
                    }

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

                    if (chance<=0 || roll >= chance)
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

                    var spawn = new WorldSpawnData(
                            stableId,
                            rule.flowerMeadow?FlowerVariant(rule,context.WorldSeed,worldX,worldZ,ruleSalt):rule.archetypeId,
                            rule.category,
                            new Vector3(worldX, height, worldZ),
                            yaw,
                            scale);
                    if(flowers!=null)flowers.Add(spawn);else chunk.AddSpawn(spawn);
                }
            }
            if(flowers!=null)
            {
                flowers.Sort((a,b)=>a.stableId.CompareTo(b.stableId));
                int count=Mathf.Min(flowers.Count,Mathf.Max(0,rule.maximumFlowersPerChunk));
                for(int i=0;i<count;i++)chunk.AddSpawn(flowers[i]);
            }
        }

        static string FlowerVariant(ScatterSpawnRule rule,int seed,float x,float z,int salt)
        {
            if(rule.flowerVariants==null||rule.flowerWeights==null||rule.flowerVariants.Length!=rule.flowerWeights.Length)return rule.archetypeId;
            float sum=0;for(int i=0;i<rule.flowerVariants.Length;i++)if(!string.IsNullOrWhiteSpace(rule.flowerVariants[i]))sum+=Mathf.Max(0,rule.flowerWeights[i]);
            if(sum<=0)return rule.archetypeId;
            float roll=DeterministicHash.Hash01(seed,Mathf.FloorToInt(x/Mathf.Max(.5f,rule.speciesGroupRadius)),Mathf.FloorToInt(z/Mathf.Max(.5f,rule.speciesGroupRadius)),salt^0x79)*sum;
            for(int i=0;i<rule.flowerVariants.Length;i++)
            {if(string.IsNullOrWhiteSpace(rule.flowerVariants[i])||rule.flowerWeights[i]<=0)continue;roll-=rule.flowerWeights[i];if(roll<=0)return rule.flowerVariants[i];}
            return rule.archetypeId;
        }
        static float MeadowNoise(int seed,float x,float z,int salt)
        {
            int ix=Mathf.FloorToInt(x),iz=Mathf.FloorToInt(z);float u=Mathf.SmoothStep(0,1,x-ix),v=Mathf.SmoothStep(0,1,z-iz);
            return Mathf.Lerp(Mathf.Lerp(DeterministicHash.Hash01(seed,ix,iz,salt),DeterministicHash.Hash01(seed,ix+1,iz,salt),u),
                Mathf.Lerp(DeterministicHash.Hash01(seed,ix,iz+1,salt),DeterministicHash.Hash01(seed,ix+1,iz+1,salt),u),v);
        }

        private static bool IsNaturalTreeSurface(SurfaceKind surface)
        {
            return surface == SurfaceKind.Grass ||
                   surface == SurfaceKind.WoodlandFloor ||
                   surface == SurfaceKind.Mud;
        }
    }
}
