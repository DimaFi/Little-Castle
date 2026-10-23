using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Validates cross-asset world-generation contracts that Unity's inspector
    /// cannot enforce by itself.
    /// </summary>
    public static class WorldGenerationConfigurationValidator
    {
        public static WorldConfigurationValidationReport Validate(
            WorldDefinition definition)
        {
            if (definition == null)
            {
                var missing =
                    new WorldConfigurationValidationReport();

                missing.AddError(
                    "WorldDefinition is missing.");

                return missing;
            }

            WorldConfigurationValidationReport report =
                Validate(
                    definition.GenerationSettings,
                    definition.MacroPlannerSettings,
                    definition.SpawnCatalog);

            ValidateStreamingSettings(
                definition.StreamingSettings,
                report);

            ValidateMapRules(
                definition.MapRules,
                report);

            ValidateStartFairnessSettings(
                definition.StartFairnessSettings,
                report);

            report.AddInfo(
                "World definition: '" +
                definition.WorldId +
                "'.");

            return report;
        }

        public static WorldConfigurationValidationReport Validate(
            WorldGenerationSettings generationSettings,
            MacroWorldPlannerSettings macroSettings,
            WorldSpawnCatalog spawnCatalog)
        {
            var report =
                new WorldConfigurationValidationReport();

            if (generationSettings == null)
            {
                report.AddError(
                    "WorldGenerationSettings is missing.");

                return report;
            }

            ValidateGenerationSettings(
                generationSettings,
                macroSettings,
                report);

            var referencedArchetypes =
                new HashSet<string>();

            var stableRuleIds =
                new HashSet<string>();

            ValidateGenerationRules(
                generationSettings,
                stableRuleIds,
                referencedArchetypes,
                report);

            ValidateMacroRules(
                macroSettings,
                stableRuleIds,
                referencedArchetypes,
                report);

            ValidateSpawnCatalog(
                spawnCatalog,
                referencedArchetypes,
                report);

            report.AddInfo(
                "Generation profile: '" +
                generationSettings.ProfileId +
                "', version " +
                generationSettings.GenerationVersion +
                ".");

            return report;
        }

        private static void ValidateGenerationSettings(
            WorldGenerationSettings settings,
            MacroWorldPlannerSettings macroSettings,
            WorldConfigurationValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(settings.ProfileId))
            {
                report.AddError(
                    "Generation profile ID is empty.");
            }

            if (settings.CellsPerSide < 4)
            {
                report.AddWarning(
                    "CellsPerSide is very low (" +
                    settings.CellsPerSide +
                    "). This is acceptable for testing but coarse for terrain.");
            }

            if (settings.Stages.Count == 0)
            {
                report.AddError(
                    "Generation pipeline has no stages.");

                return;
            }

            bool hasTerrainBase = false;
            bool hasTerrainAnalysis = false;
            bool hasMacroProjection = false;
            bool hasClimate = false;
            bool hasBiome = false;
            bool hasForestField = false;
            bool hasObjectScatter = false;
            bool hasRiverCarving = false;
            int terrainBaseWriters = 0;

            bool hasPrevious = false;
            WorldGenerationStagePhase previous =
                WorldGenerationStagePhase.TerrainBase;

            for (int i = 0; i < settings.Stages.Count; i++)
            {
                WorldGenerationStage stage =
                    settings.Stages[i];

                if (stage == null)
                {
                    report.AddWarning(
                        "Pipeline stage #" + i +
                        " is null.");

                    continue;
                }

                if (hasPrevious &&
                    stage.Phase < previous)
                {
                    report.AddError(
                        "Stage '" +
                        stage.name +
                        "' (" +
                        stage.Phase +
                        ") appears after later phase " +
                        previous +
                        ".");
                }

                previous = stage.Phase;
                hasPrevious = true;

                if (stage.Phase ==
                    WorldGenerationStagePhase.TerrainBase)
                {
                    hasTerrainBase = true;
                    terrainBaseWriters++;
                }

                if (stage is TerrainClassificationStage)
                    hasTerrainAnalysis = true;

                if (stage is MacroFeatureProjectionStage)
                    hasMacroProjection = true;

                if (stage is ClimateStage)
                    hasClimate = true;

                if (stage is BiomeClassificationStage)
                    hasBiome = true;

                if (stage is ForestDensityStage)
                    hasForestField = true;

                if (stage is ObjectScatterStage)
                    hasObjectScatter = true;

                if (stage is RiverTerrainCarvingStage)
                    hasRiverCarving = true;
            }

            if (!hasTerrainBase)
            {
                report.AddError(
                    "Pipeline has no TerrainBase stage.");
            }

            if (terrainBaseWriters > 1)
            {
                report.AddWarning(
                    "Pipeline contains " +
                    terrainBaseWriters +
                    " TerrainBase stages. Multiple height writers may overwrite " +
                    "each other unless this is intentional.");
            }

            if (!hasTerrainAnalysis)
            {
                report.AddError(
                    "TerrainClassificationStage is missing. " +
                    "Slope/terrain filters used by macro and scatter rules will not work correctly.");
            }

            if (macroSettings != null &&
                !hasMacroProjection)
            {
                report.AddWarning(
                    "MacroWorldPlannerSettings is assigned, but " +
                    "MacroFeatureProjectionStage is missing from the chunk pipeline.");
            }

            if (hasRiverCarving &&
                macroSettings == null)
            {
                report.AddWarning(
                    "RiverTerrainCarvingStage exists but no macro planner settings are supplied; " +
                    "it will have no rivers to carve.");
            }

            if (hasBiome && !hasClimate)
            {
                report.AddWarning(
                    "BiomeClassificationStage exists without ClimateStage. " +
                    "Moisture/temperature will stay at default values.");
            }

            if (hasForestField && !hasClimate)
            {
                report.AddWarning(
                    "ForestDensityStage exists without ClimateStage. " +
                    "The current forest stage is designed to use moisture.");
            }

            if (hasObjectScatter &&
                !hasForestField)
            {
                report.AddWarning(
                    "ObjectScatterStage exists without ForestDensityStage. " +
                    "Rules coupled to forest density will produce no objects.");
            }
        }

        private static void ValidateGenerationRules(
            WorldGenerationSettings settings,
            HashSet<string> stableRuleIds,
            HashSet<string> referencedArchetypes,
            WorldConfigurationValidationReport report)
        {
            for (int stageIndex = 0;
                 stageIndex < settings.Stages.Count;
                 stageIndex++)
            {
                WorldGenerationStage stage =
                    settings.Stages[stageIndex];

                if (stage is ObjectScatterStage scatter)
                {
                    for (int i = 0; i < scatter.Rules.Count; i++)
                    {
                        ScatterSpawnRule rule =
                            scatter.Rules[i];

                        if (rule == null)
                        {
                            report.AddWarning(
                                "ObjectScatterStage has null rule #" + i + ".");

                            continue;
                        }

                        string context =
                            "Scatter rule #" + i;

                        ValidateStableRuleId(
                            rule.ruleId,
                            context,
                            stableRuleIds,
                            report);

                        if (string.IsNullOrWhiteSpace(
                            rule.archetypeId))
                        {
                            report.AddError(
                                context +
                                " has empty archetypeId.");
                        }
                        else
                        {
                            referencedArchetypes.Add(
                                rule.archetypeId);
                        }

                        if (rule.spacing < 0.25f)
                        {
                            report.AddWarning(
                                context +
                                " has extremely small spacing; this can create huge spawn counts.");
                        }

                        if (rule.minHeight >
                            rule.maxHeight)
                        {
                            report.AddError(
                                context +
                                " has minHeight greater than maxHeight.");
                        }

                        if (rule.allowedBiomes == BiomeMask.None)
                        {
                            report.AddWarning(
                                context +
                                " has BiomeMask.None and can never spawn.");
                        }

                        if (rule.minScale <= 0f ||
                            rule.maxScale <= 0f)
                        {
                            report.AddError(
                                context +
                                " has non-positive scale.");
                        }
                    }
                }
                else if (stage is ResourceDepositStage resources)
                {
                    for (int i = 0;
                         i < resources.Rules.Count;
                         i++)
                    {
                        ResourceDepositRule rule =
                            resources.Rules[i];

                        if (rule == null)
                        {
                            report.AddWarning(
                                "ResourceDepositStage has null rule #" + i + ".");

                            continue;
                        }

                        string context =
                            "Resource rule #" + i;

                        ValidateStableRuleId(
                            rule.ruleId,
                            context,
                            stableRuleIds,
                            report);

                        if (rule.resourceKind ==
                            ResourceKind.Unknown)
                        {
                            report.AddWarning(
                                context +
                                " uses ResourceKind.Unknown.");
                        }

                        if (rule.createVisualSpawn &&
                            !string.IsNullOrWhiteSpace(
                                rule.visualArchetypeId))
                        {
                            referencedArchetypes.Add(
                                rule.visualArchetypeId);
                        }

                        if (rule.minHeight >
                            rule.maxHeight)
                        {
                            report.AddError(
                                context +
                                " has minHeight greater than maxHeight.");
                        }

                        if (rule.allowedBiomes == BiomeMask.None)
                        {
                            report.AddWarning(
                                context +
                                " has BiomeMask.None and can never generate.");
                        }

                        if (rule.minCapacity >
                            rule.maxCapacity)
                        {
                            report.AddWarning(
                                context +
                                " has minCapacity > maxCapacity. Runtime generation will swap them, " +
                                "but the asset should be cleaned up.");
                        }
                    }
                }
            }
        }

        private static void ValidateMacroRules(
            MacroWorldPlannerSettings settings,
            HashSet<string> stableRuleIds,
            HashSet<string> referencedArchetypes,
            WorldConfigurationValidationReport report)
        {
            if (settings == null)
            {
                report.AddInfo(
                    "No MacroWorldPlannerSettings assigned.");

                return;
            }

            for (int i = 0;
                 i < settings.PointFeatureRules.Count;
                 i++)
            {
                MacroPointFeatureRule rule =
                    settings.PointFeatureRules[i];

                if (rule == null)
                {
                    report.AddWarning(
                        "Macro point rule #" + i +
                        " is null.");

                    continue;
                }

                string context =
                    "Macro point rule #" + i +
                    " (" + rule.kind + ")";

                ValidateStableRuleId(
                    rule.ruleId,
                    context,
                    stableRuleIds,
                    report);

                if (!string.IsNullOrWhiteSpace(
                    rule.archetypeId))
                {
                    referencedArchetypes.Add(
                        rule.archetypeId);
                }

                if (rule.minHeight >
                    rule.maxHeight)
                {
                    report.AddError(
                        context +
                        " has minHeight greater than maxHeight.");
                }

                if (rule.spacing <=
                    rule.influenceRadius * 2f)
                {
                    report.AddWarning(
                        context +
                        " spacing is not much larger than its influence diameter; " +
                        "many candidates may be rejected by separation.");
                }
            }

            if (settings.Bridges != null &&
                settings.Bridges.enabled &&
                !string.IsNullOrWhiteSpace(
                    settings.Bridges.archetypeId))
            {
                referencedArchetypes.Add(
                    settings.Bridges.archetypeId);
            }

            if (settings.PlanningHalo <
                settings.MaxPointInfluenceRadius)
            {
                report.AddWarning(
                    "Macro planning halo is smaller than point-feature influence requirements.");
            }

            if (settings.RoadNetwork != null &&
                settings.RoadNetwork.enabled &&
                settings.RoadNetwork.nearestConnectionsPerSettlement <= 0)
            {
                report.AddWarning(
                    "Road network is enabled but nearestConnectionsPerSettlement <= 0.");
            }

            if (settings.RoadPaths != null &&
                settings.RoadPaths.enabled &&
                settings.RoadPaths.maxExpandedNodes < 100)
            {
                report.AddWarning(
                    "Road path solver maxExpandedNodes is very low.");
            }
        }

        private static void ValidateStreamingSettings(
            WorldStreamingSettings settings,
            WorldConfigurationValidationReport report)
        {
            if (settings == null)
            {
                report.AddWarning(
                    "No WorldStreamingSettings assigned. Preview generation can work, " +
                    "but WorldStreamer cannot initialize.");

                return;
            }

            if (settings.MacroPlanRadiusChunks <=
                settings.UnloadRadiusChunks)
            {
                report.AddError(
                    "WorldStreamingSettings macroPlanRadiusChunks must be larger " +
                    "than the unload radius.");
            }

            int warningMargin =
                settings.MacroPlanRadiusChunks -
                settings.UnloadRadiusChunks;

            if (warningMargin <=
                settings.MacroEdgeWarningChunks)
            {
                report.AddWarning(
                    "World streaming macro edge warning margin leaves little room " +
                    "outside the unload radius.");
            }

            if (settings.MaxCachedChunks > 0)
            {
                int radius =
                    settings.PrefetchRadiusChunks;

                int expectedPrefetchEntries = 0;

                for (int z = -radius;
                     z <= radius;
                     z++)
                {
                    for (int x = -radius;
                         x <= radius;
                         x++)
                    {
                        if (settings.CircularLoading &&
                            x * x + z * z >
                            radius * radius)
                        {
                            continue;
                        }

                        expectedPrefetchEntries++;
                    }
                }

                if (settings.MaxCachedChunks <
                    expectedPrefetchEntries)
                {
                    report.AddWarning(
                        "WorldStreamingSettings maxCachedChunks (" +
                        settings.MaxCachedChunks +
                        ") is smaller than the configured data-prefetch target (" +
                        expectedPrefetchEntries +
                        " chunks). Prefetched data may be evicted before the " +
                        "camera reaches it.");
                }
            }

            if (settings.MaxChunkLoadsPerFrame > 8)
            {
                report.AddWarning(
                    "WorldStreamingSettings maxChunkLoadsPerFrame is high (" +
                    settings.MaxChunkLoadsPerFrame +
                    "). Presentation is cheap compared with generation, but " +
                    "large bursts can still create GameObject/Mesh spikes.");
            }

            if (settings.UrgentGenerationStagesPerFrame > 4)
            {
                report.AddWarning(
                    "urgentGenerationStagesPerFrame is high (" +
                    settings.UrgentGenerationStagesPerFrame +
                    "). Cooperative generation works best when heavy stages " +
                    "are spread across multiple frames.");
            }

            if (settings.AddMeshCollider &&
                settings.ColliderRadiusChunks >=
                settings.LoadRadiusChunks &&
                settings.LoadRadiusChunks > 1)
            {
                report.AddWarning(
                    "Terrain MeshCollider radius reaches the full visible " +
                    "streaming radius. Distant visible chunks should normally " +
                    "remain render-only for lower Physics cost.");
            }

            if (settings.GeneratedObjectColliderRadiusChunks >=
                    settings.LoadRadiusChunks &&
                settings.LoadRadiusChunks > 1)
            {
                report.AddWarning(
                    "Generated-object collider radius reaches the full visible " +
                    "streaming radius. Trees, rocks and props should normally " +
                    "keep Physics only near the gameplay focus.");
            }
        }

        private static void ValidateMapRules(
            WorldMapRules mapRules,
            WorldConfigurationValidationReport report)
        {
            if (mapRules == null)
            {
                // Optional during migration/testing. Production host-created
                // sessions should assign this asset.
                return;
            }

            var ids =
                new HashSet<string>();

            if (mapRules.Presets.Count == 0)
            {
                report.AddWarning(
                    "WorldMapRules has no map size presets.");

                return;
            }

            for (int i = 0;
                 i < mapRules.Presets.Count;
                 i++)
            {
                WorldMapSizePreset preset =
                    mapRules.Presets[i];

                if (preset == null)
                {
                    report.AddError(
                        "WorldMapRules preset #" +
                        i +
                        " is null.");

                    continue;
                }

                string context =
                    "Map preset #" +
                    i;

                if (string.IsNullOrWhiteSpace(
                        preset.presetId))
                {
                    report.AddError(
                        context +
                        " has empty presetId.");
                }
                else if (!ids.Add(
                    preset.presetId))
                {
                    report.AddError(
                        "Duplicate map preset ID '" +
                        preset.presetId +
                        "'.");
                }

                if (preset.minimumPlayers < 1)
                {
                    report.AddError(
                        context +
                        " minimumPlayers must be at least 1.");
                }

                if (preset.maximumPlayers > 0 &&
                    preset.maximumPlayers <
                        preset.minimumPlayers)
                {
                    report.AddError(
                        context +
                        " maximumPlayers is smaller than minimumPlayers.");
                }

                if (preset.widthChunks < 4 ||
                    preset.heightChunks < 4)
                {
                    report.AddError(
                        context +
                        " is too small. Finite session maps require at least " +
                        "4x4 playable chunks.");
                }

                if (preset.visualPaddingChunks < 0)
                {
                    report.AddError(
                        context +
                        " visualPaddingChunks cannot be negative.");
                }
            }
        }

        private static void ValidateStartFairnessSettings(
            WorldStartFairnessSettings settings,
            WorldConfigurationValidationReport report)
        {
            if (settings == null)
                return;

            if (settings.ResourceRequirements == null ||
                settings.ResourceRequirements.Count == 0)
            {
                report.AddWarning(
                    "WorldStartFairnessSettings has no strategic resource requirements.");
            }
            else
            {
                var kinds =
                    new HashSet<ResourceKind>();

                for (int i = 0;
                     i < settings.ResourceRequirements.Count;
                     i++)
                {
                    StartResourceRequirement requirement =
                        settings.ResourceRequirements[i];

                    if (requirement == null)
                    {
                        report.AddError(
                            "Start fairness resource requirement #" +
                            i +
                            " is null.");

                        continue;
                    }

                    if (requirement.resourceKind ==
                        ResourceKind.Unknown)
                    {
                        report.AddWarning(
                            "Start fairness resource requirement #" +
                            i +
                            " uses ResourceKind.Unknown.");
                    }

                    if (!kinds.Add(
                            requirement.resourceKind))
                    {
                        report.AddWarning(
                            "Start fairness contains duplicate requirement for " +
                            requirement.resourceKind +
                            ".");
                    }

                    if (requirement.minimumEffectiveCapacity >
                        requirement.targetEffectiveCapacity)
                    {
                        report.AddWarning(
                            "Start fairness requirement for " +
                            requirement.resourceKind +
                            " has minimum capacity above target capacity.");
                    }
                }
            }

            if (settings.MaximumAcceptedScoreSpread > 0.5f)
            {
                report.AddWarning(
                    "Start fairness maximumAcceptedScoreSpread is loose (" +
                    settings.MaximumAcceptedScoreSpread.ToString("F2") +
                    "). Large start-quality differences may be accepted.");
            }

            if (settings.MaximumCandidatesToEvaluate < 32)
            {
                report.AddWarning(
                    "Start fairness evaluates fewer than 32 candidates; " +
                    "large/high-player maps may reject otherwise valid seeds.");
            }
        }

        private static void ValidateSpawnCatalog(
            WorldSpawnCatalog catalog,
            HashSet<string> referencedArchetypes,
            WorldConfigurationValidationReport report)
        {
            if (catalog == null)
            {
                if (referencedArchetypes.Count > 0)
                {
                    report.AddWarning(
                        "No WorldSpawnCatalog assigned; generated spawn data is valid, " +
                        "but referenced archetypes cannot be rendered as prefabs yet.");
                }

                return;
            }

            var catalogIds =
                new HashSet<string>();

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                WorldSpawnCatalogEntry entry =
                    catalog.Entries[i];

                if (entry == null)
                {
                    report.AddWarning(
                        "Spawn catalog entry #" + i +
                        " is null.");

                    continue;
                }

                if (string.IsNullOrWhiteSpace(
                    entry.archetypeId))
                {
                    report.AddError(
                        "Spawn catalog entry #" + i +
                        " has empty archetypeId.");

                    continue;
                }

                if (!catalogIds.Add(
                    entry.archetypeId))
                {
                    report.AddError(
                        "Duplicate WorldSpawnCatalog archetypeId: '" +
                        entry.archetypeId +
                        "'.");
                }

                if (entry.prefabs == null ||
                    entry.prefabs.Length == 0)
                {
                    report.AddWarning(
                        "Spawn catalog archetype '" +
                        entry.archetypeId +
                        "' has no prefab variants.");
                }
                else
                {
                    bool anyValid = false;

                    for (int p = 0;
                         p < entry.prefabs.Length;
                         p++)
                    {
                        if (entry.prefabs[p] != null)
                        {
                            anyValid = true;
                            break;
                        }
                    }

                    if (!anyValid)
                    {
                        report.AddWarning(
                            "Spawn catalog archetype '" +
                            entry.archetypeId +
                            "' contains only null prefabs.");
                    }
                }
            }

            foreach (string archetypeId in referencedArchetypes)
            {
                if (!catalogIds.Contains(archetypeId))
                {
                    report.AddWarning(
                        "Generated archetype '" +
                        archetypeId +
                        "' is referenced by rules but missing from WorldSpawnCatalog.");
                }
            }
        }

        private static void ValidateStableRuleId(
            string ruleId,
            string context,
            HashSet<string> stableRuleIds,
            WorldConfigurationValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                report.AddWarning(
                    context +
                    " has empty ruleId. Generation will fall back to another value, " +
                    "but persistent IDs should use an explicit stable ruleId.");

                return;
            }

            if (!stableRuleIds.Add(ruleId))
            {
                report.AddError(
                    "Duplicate stable ruleId '" +
                    ruleId +
                    "' detected at " +
                    context +
                    ". ruleId values must be globally unique across world-generation rules.");
            }
        }
    }
}
