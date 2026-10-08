using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>Isolated prototype. Never rewrites the user's Main* assets or scene.</summary>
    public static class ConceptWorldProfileBuilder
    {
        public const string OutputRoot = "Assets/_Game/Settings/World/ConceptWorld_v001";
        public const string DefinitionPath = OutputRoot + "/ConceptWorldDefinition.asset";
        private const string SourcePath = "Assets/_Game/Settings/World/MainWorldDefinition.asset";

        [MenuItem("Little Castle/World/Create Isolated Concept World Profile")]
        public static void Create()
        {
            if (AssetDatabase.IsValidFolder(OutputRoot))
                throw new InvalidOperationException("ConceptWorld_v001 already exists; refusing to overwrite an edited profile.");
            var source = AssetDatabase.LoadAssetAtPath<WorldDefinition>(SourcePath);
            if (source == null || source.GenerationSettings == null || source.MacroPlannerSettings == null)
                throw new InvalidOperationException("Main world definition and generation/macro settings are required.");

            AssetDatabase.CreateFolder("Assets/_Game/Settings/World", "ConceptWorld_v001");
            AssetDatabase.CreateFolder(OutputRoot, "Stages");
            var definition = Clone(source, "ConceptWorldDefinition");
            var generation = Clone(source.GenerationSettings, "ConceptWorldGenerationSettings");
            var macro = Clone(source.MacroPlannerSettings, "ConceptMacroWorldPlannerSettings");
            var stages = new List<WorldGenerationStage>();
            bool hasTerrain = false;
            foreach (var original in source.GenerationSettings.Stages)
            {
                if (original == null) continue;
                var copy = UnityEngine.Object.Instantiate(original);
                copy.name = original.name;
                AssetDatabase.CreateAsset(copy, OutputRoot + "/Stages/" + original.name + ".asset");
                if (copy is LayeredTerrainStage)
                {
                    var data = new SerializedObject(copy);
                    data.FindProperty("useStylizedLandforms").boolValue = true;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    hasTerrain = true;
                }
                stages.Add(copy);
            }
            if (!hasTerrain) throw new InvalidOperationException("Source pipeline has no LayeredTerrainStage.");

            var bridgeStage = ScriptableObject.CreateInstance<FixedBridgeTerrainStage>();
            bridgeStage.name = "02b_FixedBridgeTerrain";
            AssetDatabase.CreateAsset(bridgeStage, OutputRoot + "/Stages/" + bridgeStage.name + ".asset");
            // Same phase as river carving, but AFTER it and BEFORE slope/classification.
            int insertion = stages.FindIndex(s => s.Phase > WorldGenerationStagePhase.TerrainModification);
            stages.Insert(insertion < 0 ? stages.Count : insertion, bridgeStage);

            var generationData = new SerializedObject(generation);
            generationData.FindProperty("profileId").stringValue = "concept_world_v001";
            generationData.FindProperty("generationVersion").intValue = source.GenerationSettings.GenerationVersion + 1;
            // A 0.5 m grid resolves the 2.86 m bridge walkable corridor and shoreline.
            // Smaller streamed chunks bound per-chunk work; this is not a production budget claim.
            generationData.FindProperty("cellsPerSide").intValue = 64;
            generationData.FindProperty("chunkWorldSize").floatValue = 32f;
            var list = generationData.FindProperty("stages");
            list.arraySize = stages.Count;
            for (int i = 0; i < stages.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            generationData.ApplyModifiedPropertiesWithoutUndo();

            macro.Bridges.enabled = true;
            macro.Bridges.archetypeId = FixedBridgeSiteProfile.AssetId;
            macro.Bridges.useFixedStoneBridgeSites = true;
            macro.Bridges.minimumCompatibleRiverWidth = 1.5f;
            macro.Bridges.maximumCompatibleRiverWidth = 6.5f;
            macro.Bridges.maximumCrossingAngleDeviation = 20f;
            macro.Bridges.maximumApproachGrade = 0.20f;
            macro.Bridges.maximumApproachSlope = 28f;
            macro.Bridges.maximumRiverGrade = 0.20f;
            macro.Bridges.fixedSiteSeparationPadding = 1f;
            // Channels may vary elsewhere; accepted sites use one authored local profile.
            macro.Rivers.nominalWidth = 5.7f;
            macro.Rivers.nominalDepth = 1.22f;
            macro.Rivers.sourceSpacing = 480f;
            macro.Rivers.sourceChance = 0.65f;
            macro.Rivers.minSourceHeight = 8f;
            macro.Rivers.maxSourceSlope = 32f;
            macro.Rivers.sourceTerrain = TerrainClassMask.RollingHills | TerrainClassMask.Highlands;
            macro.RoadPaths.trailWidth = 2.3f;
            macro.RoadPaths.dirtRoadWidth = 2.6f;
            macro.RoadPaths.improvedRoadWidth = 2.6f;
            macro.RoadPaths.settlementStreetWidth = 2.6f;
            macro.RoadPaths.fortifiedRouteWidth = 2.6f;
            // The art is NOT a recipe for auto-spawning a complete player-owned village.
            foreach (var rule in macro.PointFeatureRules)
            {
                if (rule == null) continue;
                rule.maxSlope = 12f;
                rule.allowedTerrain = TerrainClassMask.Plains | TerrainClassMask.RollingHills;
            }
            EditorUtility.SetDirty(macro);

            var definitionData = new SerializedObject(definition);
            definitionData.FindProperty("worldId").stringValue = "little_castle_concept_v001";
            definitionData.FindProperty("generationSettings").objectReferenceValue = generation;
            definitionData.FindProperty("macroPlannerSettings").objectReferenceValue = macro;
            foreach (string field in new[] { "mapRules", "startFairnessSettings", "timeSettings", "streamingSettings", "spawnCatalog" })
            {
                var original = definitionData.FindProperty(field).objectReferenceValue as ScriptableObject;
                if (original != null) definitionData.FindProperty(field).objectReferenceValue = Clone(original, "Concept_" + original.name);
            }
            if (definitionData.FindProperty("mapRules").objectReferenceValue == null)
            {
                var rules = ScriptableObject.CreateInstance<WorldMapRules>();
                rules.name = "ConceptWorldMapRules";
                AssetDatabase.CreateAsset(rules, OutputRoot + "/ConceptWorldMapRules.asset");
                var rulesData = new SerializedObject(rules);
                var presets = rulesData.FindProperty("presets");
                presets.arraySize = 1;
                var preset = presets.GetArrayElementAtIndex(0);
                preset.FindPropertyRelative("presetId").stringValue = "concept_preview";
                preset.FindPropertyRelative("displayName").stringValue = "Concept preview (not balanced)";
                preset.FindPropertyRelative("minimumPlayers").intValue = 1;
                preset.FindPropertyRelative("maximumPlayers").intValue = 16;
                preset.FindPropertyRelative("widthChunks").intValue = 96;
                preset.FindPropertyRelative("heightChunks").intValue = 96;
                preset.FindPropertyRelative("visualPaddingChunks").intValue = 6;
                rulesData.ApplyModifiedPropertiesWithoutUndo();
                definitionData.FindProperty("mapRules").objectReferenceValue = rules;
            }
            definitionData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("Created isolated concept profile: " + DefinitionPath +
                ". Existing scene is unchanged. Reviewed bridge prefab must be registered before visual use.");
        }

        private static T Clone<T>(T source, string name) where T : ScriptableObject
        {
            var copy = UnityEngine.Object.Instantiate(source);
            copy.name = name;
            AssetDatabase.CreateAsset(copy, OutputRoot + "/" + name + ".asset");
            return copy;
        }
    }
}
