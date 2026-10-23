using System;
using System.Collections.Generic;
using System.Text;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public sealed class ReadyModelPreflightReport
    {
        private readonly List<string> errors =
            new List<string>();

        private readonly List<string> warnings =
            new List<string>();

        private readonly List<string> info =
            new List<string>();

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public IReadOnlyList<string> Info => info;
        public bool IsValid => errors.Count == 0;

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                errors.Add(message);
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                warnings.Add(message);
        }

        public void AddInfo(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                info.Add(message);
        }

        public string ToMultilineString()
        {
            var builder =
                new StringBuilder();

            builder.AppendLine(
                IsValid
                    ? "Ready-model preflight passed."
                    : "Ready-model preflight failed.");

            builder.AppendLine(
                "Errors: " +
                errors.Count +
                ", warnings: " +
                warnings.Count +
                ", info: " +
                info.Count);

            Append(
                builder,
                "ERROR",
                errors);

            Append(
                builder,
                "WARN",
                warnings);

            Append(
                builder,
                "INFO",
                info);

            return builder.ToString();
        }

        private static void Append(
            StringBuilder builder,
            string label,
            IReadOnlyList<string> messages)
        {
            for (int i = 0;
                 i < messages.Count;
                 i++)
            {
                builder.Append("[");
                builder.Append(label);
                builder.Append("] ");
                builder.AppendLine(
                    messages[i]);
            }
        }
    }

    /// <summary>
    /// Full folder-level contract check for externally prepared production
    /// models before they are used for procedural world filling.
    /// </summary>
    public static class ReadyModelPreflight
    {
        private const string DefinitionPath =
            "Assets/_Game/Settings/World/MainWorldDefinition.asset";

        [MenuItem(
            "Little Castle/Assets/Run Ready Models Preflight")]
        public static void RunFromMenu()
        {
            ReadyModelPreflightReport report =
                Run(
                    true);

            if (report.IsValid)
            {
                if (report.Warnings.Count > 0)
                    Debug.LogWarning(report.ToMultilineString());
                else
                    Debug.Log(report.ToMultilineString());
            }
            else
            {
                Debug.LogError(
                    report.ToMultilineString());
            }
        }

        public static ReadyModelPreflightReport Run(
            bool syncCatalog)
        {
            var report =
                new ReadyModelPreflightReport();

            if (syncCatalog)
            {
                ReadyModelCatalogSync.Sync();
            }

            WorldDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    WorldDefinition>(
                    DefinitionPath);

            if (definition == null)
            {
                report.AddError(
                    "MainWorldDefinition is missing: " +
                    DefinitionPath);

                return report;
            }

            WorldSpawnCatalog catalog =
                definition.SpawnCatalog;

            if (catalog == null)
            {
                report.AddError(
                    "MainWorldDefinition has no WorldSpawnCatalog.");

                return report;
            }

            ValidateCatalogDuplicates(
                catalog,
                report);

            var referencedArchetypes =
                CollectReferencedArchetypes(
                    definition);

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:GameObject",
                    new[]
                    {
                        ReadyModelCatalogSync.ReadyRoot
                    });

            var paths =
                new List<string>(
                    guids.Length);

            for (int i = 0;
                 i < guids.Length;
                 i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                if (!string.IsNullOrWhiteSpace(path))
                    paths.Add(path);
            }

            paths.Sort(
                StringComparer.Ordinal);

            var variantsByArchetype =
                new Dictionary<string, int>(
                    StringComparer.Ordinal);

            var categoryByArchetype =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);

            int totalAssetErrors = 0;
            int totalAssetWarnings = 0;

            for (int i = 0;
                 i < paths.Count;
                 i++)
            {
                string path =
                    paths[i];

                if (!TryReadReadyPath(
                        path,
                        out string category,
                        out string archetypeId))
                {
                    report.AddError(
                        "Ready model path does not match " +
                        "<Category>/<ArchetypeId>/<Variant>: " +
                        path);

                    continue;
                }

                GameObject model =
                    AssetDatabase.LoadAssetAtPath<
                        GameObject>(
                        path);

                if (model == null)
                {
                    report.AddError(
                        "Ready model could not be loaded: " +
                        path);

                    continue;
                }

                if (!variantsByArchetype.ContainsKey(
                        archetypeId))
                {
                    variantsByArchetype.Add(
                        archetypeId,
                        0);

                    categoryByArchetype.Add(
                        archetypeId,
                        category);
                }

                variantsByArchetype[
                    archetypeId]++;

                ProductionAssetValidator.ValidationResult
                    validation =
                        ProductionAssetValidator.Validate(
                            model,
                            false);

                totalAssetErrors +=
                    validation.Errors;

                totalAssetWarnings +=
                    validation.Warnings;

                if (validation.Errors > 0)
                {
                    report.AddError(
                        path +
                        " failed ProductionAssetValidator with " +
                        validation.Errors +
                        " error(s).");
                }

                if (validation.Warnings > 0)
                {
                    report.AddWarning(
                        path +
                        " has " +
                        validation.Warnings +
                        " production warning(s). Review Console details.");
                }

                if (!referencedArchetypes.Contains(
                        archetypeId))
                {
                    report.AddWarning(
                        "Ready archetype '" +
                        archetypeId +
                        "' is not referenced by current generation rules.");
                }
            }

            foreach (
                KeyValuePair<string, int> pair
                in variantsByArchetype)
            {
                int catalogCount =
                    catalog.GetValidVariantCount(
                        pair.Key);

                if (catalogCount != pair.Value)
                {
                    report.AddError(
                        "Catalog mismatch for '" +
                        pair.Key +
                        "': Ready variants=" +
                        pair.Value +
                        ", catalog variants=" +
                        catalogCount +
                        ". Run Sync Ready Models.");
                }

                if (categoryByArchetype.TryGetValue(
                        pair.Key,
                        out string category) &&
                    IsRepeatableCategory(
                        category) &&
                    pair.Value < 3)
                {
                    report.AddWarning(
                        "Repeatable archetype '" +
                        pair.Key +
                        "' has only " +
                        pair.Value +
                        " visual variant(s). Three or more are recommended " +
                        "before judging repetition in world filling.");
                }
            }

            foreach (
                string archetypeId
                in referencedArchetypes)
            {
                if (!variantsByArchetype.ContainsKey(
                        archetypeId))
                {
                    report.AddWarning(
                        "Generation references '" +
                        archetypeId +
                        "' but there is no production model in Models/Ready yet. " +
                        "A catalog placeholder may still be used for testing.");
                }

                if (!catalog.ContainsArchetype(
                        archetypeId))
                {
                    report.AddError(
                        "Generation references archetype '" +
                        archetypeId +
                        "' but WorldSpawnCatalog cannot resolve it.");
                }
            }

            if (paths.Count == 0)
            {
                report.AddWarning(
                    "Models/Ready contains no GameObject model assets yet.");
            }

            report.AddInfo(
                "Ready model assets checked: " +
                paths.Count +
                ".");

            report.AddInfo(
                "Ready archetypes: " +
                variantsByArchetype.Count +
                ".");

            report.AddInfo(
                "Production validator totals: errors=" +
                totalAssetErrors +
                ", warnings=" +
                totalAssetWarnings +
                ".");

            return report;
        }

        private static HashSet<string>
            CollectReferencedArchetypes(
                WorldDefinition definition)
        {
            var result =
                new HashSet<string>(
                    StringComparer.Ordinal);

            WorldGenerationSettings generation =
                definition.GenerationSettings;

            if (generation != null)
            {
                for (int i = 0;
                     i < generation.Stages.Count;
                     i++)
                {
                    WorldGenerationStage stage =
                        generation.Stages[i];

                    if (stage is ObjectScatterStage scatter)
                    {
                        for (int r = 0;
                             r < scatter.Rules.Count;
                             r++)
                        {
                            ScatterSpawnRule rule =
                                scatter.Rules[r];

                            if (rule != null &&
                                !string.IsNullOrWhiteSpace(
                                    rule.archetypeId))
                            {
                                result.Add(
                                    rule.archetypeId);
                            }
                        }
                    }
                    else if (
                        stage is ResourceDepositStage resources)
                    {
                        for (int r = 0;
                             r < resources.Rules.Count;
                             r++)
                        {
                            ResourceDepositRule rule =
                                resources.Rules[r];

                            if (rule != null &&
                                rule.createVisualSpawn &&
                                !string.IsNullOrWhiteSpace(
                                    rule.visualArchetypeId))
                            {
                                result.Add(
                                    rule.visualArchetypeId);
                            }
                        }
                    }
                }
            }

            MacroWorldPlannerSettings macro =
                definition.MacroPlannerSettings;

            if (macro != null)
            {
                for (int i = 0;
                     i < macro.PointFeatureRules.Count;
                     i++)
                {
                    MacroPointFeatureRule rule =
                        macro.PointFeatureRules[i];

                    if (rule != null &&
                        !string.IsNullOrWhiteSpace(
                            rule.archetypeId))
                    {
                        result.Add(
                            rule.archetypeId);
                    }
                }

                if (macro.Bridges != null &&
                    macro.Bridges.enabled &&
                    !string.IsNullOrWhiteSpace(
                        macro.Bridges.archetypeId))
                {
                    result.Add(
                        macro.Bridges.archetypeId);
                }
            }

            return result;
        }

        private static void ValidateCatalogDuplicates(
            WorldSpawnCatalog catalog,
            ReadyModelPreflightReport report)
        {
            var seen =
                new HashSet<string>(
                    StringComparer.Ordinal);

            for (int i = 0;
                 i < catalog.Entries.Count;
                 i++)
            {
                WorldSpawnCatalogEntry entry =
                    catalog.Entries[i];

                if (entry == null ||
                    string.IsNullOrWhiteSpace(
                        entry.archetypeId))
                {
                    continue;
                }

                if (!seen.Add(
                        entry.archetypeId))
                {
                    report.AddError(
                        "WorldSpawnCatalog contains duplicate archetypeId '" +
                        entry.archetypeId +
                        "'.");
                }
            }
        }

        private static bool TryReadReadyPath(
            string path,
            out string category,
            out string archetypeId)
        {
            category = null;
            archetypeId = null;

            string prefix =
                ReadyModelCatalogSync.ReadyRoot +
                "/";

            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string[] parts =
                path.Substring(
                    prefix.Length).Split('/');

            if (parts.Length < 3)
                return false;

            category =
                parts[0];

            archetypeId =
                parts[1];

            return
                !string.IsNullOrWhiteSpace(
                    category) &&
                !string.IsNullOrWhiteSpace(
                    archetypeId);
        }

        private static bool IsRepeatableCategory(
            string category)
        {
            return
                string.Equals(
                    category,
                    "Trees",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "Rocks",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "Bushes",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "Props",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
