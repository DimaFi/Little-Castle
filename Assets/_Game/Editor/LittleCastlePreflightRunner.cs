using System.Text;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>
    /// One-click gate before production model/world-filling tests.
    ///
    /// It intentionally does not auto-fix art. It synchronizes the explicit
    /// Ready folder, validates the resulting catalog and then exercises the
    /// deterministic world generator without requiring a scene to be open.
    /// </summary>
    public static class LittleCastlePreflightRunner
    {
        private const string DefinitionPath =
            "Assets/_Game/Settings/World/MainWorldDefinition.asset";

        [MenuItem(
            "Little Castle/Preflight/Run Full Model + World Preflight")]
        public static void RunFullPreflight()
        {
            var output =
                new StringBuilder();

            output.AppendLine(
                "LITTLE CASTLE FULL PREFLIGHT");
            output.AppendLine(
                "============================");

            ReadyModelCatalogSync.Sync();

            ReadyModelPreflightReport modelReport =
                ReadyModelPreflight.Run(
                    false);

            output.AppendLine();
            output.AppendLine(
                modelReport.ToMultilineString());

            WorldDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    WorldDefinition>(
                    DefinitionPath);

            WorldConfigurationValidationReport
                configuration =
                    WorldGenerationConfigurationValidator.Validate(
                        definition);

            output.AppendLine();
            output.AppendLine(
                configuration.ToMultilineString());

            WorldGenerationStressReport generationReport =
                WorldGenerationStressPreflight.Run();

            output.AppendLine();
            output.AppendLine(
                generationReport.ToMultilineString());

            bool passed =
                modelReport.IsValid &&
                configuration.IsValid &&
                generationReport.IsValid;

            output.AppendLine();
            output.AppendLine(
                passed
                    ? "FULL PREFLIGHT: PASS"
                    : "FULL PREFLIGHT: FAIL");

            string text =
                output.ToString();

            if (passed)
            {
                if (modelReport.Warnings.Count > 0 ||
                    configuration.Warnings.Count > 0 ||
                    generationReport.Warnings.Count > 0)
                {
                    Debug.LogWarning(text);
                }
                else
                {
                    Debug.Log(text);
                }
            }
            else
            {
                Debug.LogError(text);
            }

            EditorUtility.DisplayDialog(
                "Little Castle Preflight",
                passed
                    ? "PASS. See Console for metrics and warnings."
                    : "FAIL. See Console for exact errors.",
                "OK");
        }
    }
}
