using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Reproducible, data-only map previews for the already opt-in
    /// StylizedLandformSampler. No scene/prefab/terrain asset is rewritten.
    /// A preview PNG is NOT a gameplay screenshot or visual acceptance.
    /// </summary>
    public static class ConceptLandscapePreview
    {
        private const string StagePath =
            "Assets/_Game/Settings/World/ConceptWorld_v001/Stages/01_LayeredTerrain.asset";
        private const int Samples = 192;
        private const float SpanMeters = 3072f;

        [Serializable]
        private sealed class Metrics
        {
            public int seed;
            public int samplesPerSide;
            public float spanMeters;
            public float minimumHeight;
            public float maximumHeight;
            public float meanHeight;
            public float buildableFraction;
            public float largestBuildableConnectedFraction;
            public float plainFraction;
            public float highlandFraction;
            public float scarpFraction;
            public float steepFraction;
            public float maximumSlopeDegrees;
            public string note =
                "Uncarved stylized base-height diagnostic, NOT a full " +
                "river/bridge/road, spawn fairness or Unity visual test.";
        }

        [MenuItem("Little Castle/World/Concept Landscape/Export Seed Preview Maps")]
        public static void ExportSeedPreviewMaps()
        {
            LayeredTerrainStage stage =
                AssetDatabase.LoadAssetAtPath<LayeredTerrainStage>(StagePath);
            if (stage == null || !stage.UseStylizedLandforms)
                throw new InvalidOperationException(
                    "Expected the opt-in concept LayeredTerrainStage with " +
                    "UseStylizedLandforms enabled. Main settings were not changed.");

            string project = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string output = Path.Combine(
                project, "Logs", "GPT6-ConceptLandscape");
            Directory.CreateDirectory(output);

            foreach (int seed in new[] { 12345, 54321, -10101 })
                Export(stage.StylizedLandforms, seed, output);

            Debug.Log("[Concept Landscape] Wrote map PNGs and per-seed JSON to " +
                output + ". These are BASE landforms only, not rendered game screenshots.");
        }

        private static void Export(
            StylizedLandformSettings settings,
            int seed,
            string folder)
        {
            int pixels = Samples * Samples;
            float[] heights = new float[pixels];
            Color32[] landforms = new Color32[pixels];
            bool[] buildable = new bool[pixels];
            var report = new Metrics
            {
                seed = seed,
                samplesPerSide = Samples,
                spanMeters = SpanMeters,
                minimumHeight = float.PositiveInfinity,
                maximumHeight = float.NegativeInfinity
            };

            // Center around the origin: negative and positive world-space
            // coordinates appear in every preview, with repeatable pixels.
            float step = SpanMeters / (Samples - 1);
            int plain = 0, highland = 0, scarp = 0, steep = 0, buildableCount = 0;

            for (int z = 0; z < Samples; z++)
            {
                for (int x = 0; x < Samples; x++)
                {
                    int i = z * Samples + x;
                    float wx = -SpanMeters * 0.5f + step * x;
                    float wz = -SpanMeters * 0.5f + step * z;

                    StylizedLandformSample sample =
                        StylizedLandformSampler.Sample(
                            seed, wx, wz, settings);
                    if (float.IsNaN(sample.Height) ||
                        float.IsInfinity(sample.Height))
                        throw new InvalidOperationException(
                            "Non-finite height for seed " + seed +
                            " at " + wx + "," + wz);

                    heights[i] = sample.Height;
                    report.minimumHeight = Mathf.Min(
                        report.minimumHeight, sample.Height);
                    report.maximumHeight = Mathf.Max(
                        report.maximumHeight, sample.Height);
                    report.meanHeight += sample.Height / pixels;

                    if (sample.PlainMask > 0.5f) plain++;
                    if (sample.HighlandMask > 0.5f) highland++;
                    if (sample.ScarpMask > 0.5f) scarp++;

                    // RGB channels visualize plain/highland/scarp masks,
                    // NOT artist-authored vegetation or final materials.
                    landforms[i] = new Color(
                        Mathf.Clamp01(sample.ScarpMask),
                        Mathf.Clamp01(sample.PlainMask),
                        Mathf.Clamp01(sample.HighlandMask), 1f);
                }
            }

            Color32[] slopeColors = new Color32[pixels];
            Color32[] heightColors = new Color32[pixels];
            float heightRange = Mathf.Max(0.001f,
                report.maximumHeight - report.minimumHeight);

            for (int z = 0; z < Samples; z++)
            {
                for (int x = 0; x < Samples; x++)
                {
                    int i = z * Samples + x;
                    int l = z * Samples + Mathf.Max(0, x - 1);
                    int r = z * Samples + Mathf.Min(Samples - 1, x + 1);
                    int d = Mathf.Max(0, z - 1) * Samples + x;
                    int u = Mathf.Min(Samples - 1, z + 1) * Samples + x;
                    float dx = (heights[r] - heights[l]) /
                        (Mathf.Max(1, Mathf.Min(Samples - 1, x + 1) -
                            Mathf.Max(0, x - 1)) * step);
                    float dz = (heights[u] - heights[d]) /
                        (Mathf.Max(1, Mathf.Min(Samples - 1, z + 1) -
                            Mathf.Max(0, z - 1)) * step);
                    float degrees = Mathf.Atan(
                        Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;

                    report.maximumSlopeDegrees =
                        Mathf.Max(report.maximumSlopeDegrees, degrees);
                    bool lowSlope = degrees <= 8f;
                    buildable[i] = lowSlope;
                    if (lowSlope) buildableCount++;
                    if (degrees >= 18f) steep++;

                    float h = Mathf.Clamp01(
                        (heights[i] - report.minimumHeight) / heightRange);
                    heightColors[i] = Color.Lerp(
                        new Color(0.30f, 0.42f, 0.24f, 1f),
                        new Color(0.84f, 0.80f, 0.70f, 1f), h);
                    slopeColors[i] = lowSlope
                        ? new Color32(100, 180, 108, 255)
                        : degrees < 18f
                            ? new Color32(234, 196, 101, 255)
                            : new Color32(180, 92, 87, 255);
                }
            }

            report.buildableFraction = buildableCount / (float)pixels;
            report.plainFraction = plain / (float)pixels;
            report.highlandFraction = highland / (float)pixels;
            report.scarpFraction = scarp / (float)pixels;
            report.steepFraction = steep / (float)pixels;
            report.largestBuildableConnectedFraction =
                LargestConnectedArea(buildable, Samples) / (float)pixels;

            string prefix = Path.Combine(folder, "seed_" + seed);
            WritePng(prefix + "_height.png", heightColors, Samples);
            WritePng(prefix + "_masks_RGB_scarp_plain_highland.png",
                landforms, Samples);
            WritePng(prefix + "_slope_buildable.png", slopeColors, Samples);
            File.WriteAllText(
                prefix + "_metrics.json",
                JsonUtility.ToJson(report, true),
                System.Text.Encoding.UTF8);

            Debug.Log("[Concept Landscape] seed " + seed +
                ": buildable " + (report.buildableFraction * 100f).ToString("F1") +
                "%, largest connected " +
                (report.largestBuildableConnectedFraction * 100f).ToString("F1") +
                "%, highland " + (report.highlandFraction * 100f).ToString("F1") +
                "%, scarp " + (report.scarpFraction * 100f).ToString("F1") +
                "%. Not post-river gameplay terrain.");
        }

        private static int LargestConnectedArea(bool[] cells, int side)
        {
            var visited = new bool[cells.Length];
            var queue = new Queue<int>();
            int largest = 0;

            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i] || visited[i])
                    continue;

                visited[i] = true;
                queue.Enqueue(i);
                int size = 0;
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    size++;
                    int x = index % side, z = index / side;
                    if (x > 0) Visit(index - 1);
                    if (x + 1 < side) Visit(index + 1);
                    if (z > 0) Visit(index - side);
                    if (z + 1 < side) Visit(index + side);
                }
                largest = Mathf.Max(largest, size);
            }

            return largest;

            void Visit(int neighbor)
            {
                if (!cells[neighbor] || visited[neighbor])
                    return;
                visited[neighbor] = true;
                queue.Enqueue(neighbor);
            }
        }

        private static void WritePng(
            string path, Color32[] pixels, int side)
        {
            var texture = new Texture2D(
                side, side, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
