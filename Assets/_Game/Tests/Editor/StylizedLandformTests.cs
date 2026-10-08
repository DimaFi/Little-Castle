using System;
using System.Collections.Generic;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class StylizedLandformTests
    {
        private const float BorderEpsilon = 0f;

        [Test]
        public void PureSampler_IsExactForSameSeed_AndChangesForAnotherSeed()
        {
            var settings = new StylizedLandformSettings();
            var positions = new[]
            {
                new Vector2(-2048.5f, -913.25f),
                new Vector2(-64f, 128f),
                Vector2.zero,
                new Vector2(777.75f, -333.5f),
                new Vector2(4096f, 6144f)
            };

            bool anyChanged = false;
            foreach (Vector2 position in positions)
            {
                float first =
                    StylizedLandformSampler.SampleHeight(
                        12345,
                        position.x,
                        position.y,
                        settings);
                float repeated =
                    StylizedLandformSampler.SampleHeight(
                        12345,
                        position.x,
                        position.y,
                        settings);
                float otherSeed =
                    StylizedLandformSampler.SampleHeight(
                        54321,
                        position.x,
                        position.y,
                        settings);

                Assert.That(repeated, Is.EqualTo(first));
                anyChanged |= otherSeed != first;
            }

            Assert.That(
                anyChanged,
                Is.True,
                "Changing the world seed did not change any sampled height.");
        }

        [Test]
        public void PureSampler_ProducesFiniteHeightsAndMasks()
        {
            var settings = new StylizedLandformSettings();
            float[] coordinates =
            {
                -1000000f,
                -8192.25f,
                -0.001f,
                0f,
                0.001f,
                8192.25f,
                1000000f
            };

            foreach (float x in coordinates)
            {
                foreach (float z in coordinates)
                {
                    StylizedLandformSample sample =
                        StylizedLandformSampler.Sample(
                            int.MinValue + 17,
                            x,
                            z,
                            settings);

                    AssertFinite(sample.Height, "height", x, z);
                    AssertMask(sample.PlainMask, "plain", x, z);
                    AssertMask(sample.HillMask, "hill", x, z);
                    AssertMask(sample.HighlandMask, "highland", x, z);
                    AssertMask(sample.ScarpMask, "scarp", x, z);
                }
            }

            Assert.That(
                StylizedLandformSampler.SampleHeight(
                    1,
                    float.NaN,
                    float.PositiveInfinity,
                    settings),
                Is.EqualTo(settings.BaseHeight));
        }

        [Test]
        public void OptInStage_AgreesAcrossNegativeAndPositiveChunkBorders()
        {
            WorldGenerationSettings generationSettings =
                ScriptableObject.CreateInstance<WorldGenerationSettings>();
            LayeredTerrainStage stage =
                ScriptableObject.CreateInstance<LayeredTerrainStage>();

            try
            {
                var serializedStage = new SerializedObject(stage);
                serializedStage.FindProperty("useStylizedLandforms").boolValue =
                    true;
                serializedStage.ApplyModifiedPropertiesWithoutUndo();

                var context =
                    new GenerationContext(
                        24681357,
                        generationSettings);

                AssertEastWestBorder(
                    stage,
                    context,
                    generationSettings,
                    new ChunkCoordinate(-3, -2));
                AssertNorthSouthBorder(
                    stage,
                    context,
                    generationSettings,
                    new ChunkCoordinate(-2, -4));
                AssertEastWestBorder(
                    stage,
                    context,
                    generationSettings,
                    new ChunkCoordinate(3, 2));
                AssertNorthSouthBorder(
                    stage,
                    context,
                    generationSettings,
                    new ChunkCoordinate(2, 4));
            }
            finally
            {
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(generationSettings);
            }
        }

        [Test]
        public void DisabledMode_PreservesLegacyLayeredTerrainFormula()
        {
            WorldGenerationSettings generationSettings =
                ScriptableObject.CreateInstance<WorldGenerationSettings>();
            LayeredTerrainStage stage =
                ScriptableObject.CreateInstance<LayeredTerrainStage>();

            try
            {
                Assert.That(stage.UseStylizedLandforms, Is.False);

                var coordinate = new ChunkCoordinate(-2, 1);
                var chunk =
                    new WorldChunkData(
                        coordinate,
                        generationSettings.CellsPerSide);
                var context =
                    new GenerationContext(
                        13579,
                        generationSettings);
                stage.Generate(context, chunk);

                float originX =
                    coordinate.x * generationSettings.ChunkWorldSize;
                float originZ =
                    coordinate.z * generationSettings.ChunkWorldSize;

                for (int z = 0; z < chunk.SamplesPerSide; z++)
                {
                    for (int x = 0; x < chunk.SamplesPerSide; x++)
                    {
                        float worldX =
                            originX + x * generationSettings.CellWorldSize;
                        float worldZ =
                            originZ + z * generationSettings.CellWorldSize;

                        Assert.That(
                            chunk.GetHeight(x, z),
                            Is.EqualTo(
                                LegacyDefaultHeight(
                                    13579,
                                    worldX,
                                    worldZ)));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(generationSettings);
            }
        }

        [Test]
        public void DefaultPreset_HasBroadBuildableGroundAndControlledScarps()
        {
            var settings = new StylizedLandformSettings();
            int[] seeds = { 7, 12345, 86420, -170031 };

            double buildableSum = 0d;
            double largestBuildableSum = 0d;
            double steepSum = 0d;
            double plainSum = 0d;
            double highlandSum = 0d;
            double scarpSum = 0d;

            foreach (int seed in seeds)
            {
                LandformMetrics metrics =
                    Measure(seed, settings, 3072f, 97);

                buildableSum += metrics.BuildableFraction;
                largestBuildableSum +=
                    metrics.LargestBuildableFraction;
                steepSum += metrics.SteepFraction;
                plainSum += metrics.PlainFraction;
                highlandSum += metrics.HighlandFraction;
                scarpSum += metrics.ScarpFraction;

                Debug.Log(
                    $"Stylized landforms seed={seed}: {metrics}");
            }

            float divisor = seeds.Length;
            float averageBuildable = (float)(buildableSum / divisor);
            float averageLargest =
                (float)(largestBuildableSum / divisor);
            float averageSteep = (float)(steepSum / divisor);
            float averagePlain = (float)(plainSum / divisor);
            float averageHighland = (float)(highlandSum / divisor);
            float averageScarp = (float)(scarpSum / divisor);

            Debug.Log(
                "Stylized landforms four-seed average: " +
                $"buildable={averageBuildable:P1}, " +
                $"largestBuildable={averageLargest:P1}, " +
                $"plain={averagePlain:P1}, " +
                $"highland={averageHighland:P1}, " +
                $"scarp={averageScarp:P1}, " +
                $"steep={averageSteep:P1}.");

            Assert.That(averageBuildable, Is.GreaterThan(0.52f));
            Assert.That(averageLargest, Is.GreaterThan(0.2f));
            Assert.That(averagePlain, Is.GreaterThan(0.42f));
            Assert.That(averageHighland, Is.InRange(0.005f, 0.3f));
            Assert.That(averageScarp, Is.GreaterThan(0.001f));
            Assert.That(averageSteep, Is.LessThan(0.16f));
        }

        [Test]
        public void DefaultPreset_ReportsFiniteMapScaleImplications()
        {
            var settings = new StylizedLandformSettings();
            var spans = new[]
            {
                new PlayerScale(2, 2048f),
                new PlayerScale(8, 4096f),
                new PlayerScale(16, 6144f)
            };

            foreach (PlayerScale scale in spans)
            {
                LandformMetrics metrics =
                    Measure(12345, settings, scale.Span, 49);
                Debug.Log(
                    $"Stylized landforms {scale.Players}-player " +
                    $"{scale.Span:F0} m sample: {metrics}");

                Assert.That(metrics.AllFinite, Is.True);
                Assert.That(
                    metrics.BuildableFraction,
                    Is.GreaterThan(0.35f));
            }
        }

        private static void AssertEastWestBorder(
            LayeredTerrainStage stage,
            GenerationContext context,
            WorldGenerationSettings settings,
            ChunkCoordinate westCoordinate)
        {
            WorldChunkData west =
                Generate(stage, context, settings, westCoordinate);
            WorldChunkData east =
                Generate(
                    stage,
                    context,
                    settings,
                    new ChunkCoordinate(
                        westCoordinate.x + 1,
                        westCoordinate.z));

            int borderX = west.SamplesPerSide - 1;
            for (int z = 0; z < west.SamplesPerSide; z++)
            {
                Assert.That(
                    west.GetHeight(borderX, z),
                    Is.EqualTo(east.GetHeight(0, z))
                        .Within(BorderEpsilon));
            }
        }

        private static void AssertNorthSouthBorder(
            LayeredTerrainStage stage,
            GenerationContext context,
            WorldGenerationSettings settings,
            ChunkCoordinate southCoordinate)
        {
            WorldChunkData south =
                Generate(stage, context, settings, southCoordinate);
            WorldChunkData north =
                Generate(
                    stage,
                    context,
                    settings,
                    new ChunkCoordinate(
                        southCoordinate.x,
                        southCoordinate.z + 1));

            int borderZ = south.SamplesPerSide - 1;
            for (int x = 0; x < south.SamplesPerSide; x++)
            {
                Assert.That(
                    south.GetHeight(x, borderZ),
                    Is.EqualTo(north.GetHeight(x, 0))
                        .Within(BorderEpsilon));
            }
        }

        private static WorldChunkData Generate(
            LayeredTerrainStage stage,
            GenerationContext context,
            WorldGenerationSettings settings,
            ChunkCoordinate coordinate)
        {
            var chunk =
                new WorldChunkData(
                    coordinate,
                    settings.CellsPerSide);
            stage.Generate(context, chunk);
            return chunk;
        }

        private static float LegacyDefaultHeight(
            int worldSeed,
            float worldX,
            float worldZ)
        {
            int regionSeed =
                DeterministicNoise.Hash(worldSeed, 1001, 0x11);
            int hillSeed =
                DeterministicNoise.Hash(worldSeed, 1002, 0x22);
            int mountainSeed =
                DeterministicNoise.Hash(worldSeed, 1003, 0x33);
            int detailSeed =
                DeterministicNoise.Hash(worldSeed, 1004, 0x44);

            float region =
                DeterministicNoise.FractalValueNoise(
                    regionSeed,
                    worldX,
                    worldZ,
                    3,
                    0.0008f,
                    2f,
                    0.5f);
            float reliefMask =
                Mathf.Clamp01(
                    AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
                        .Evaluate(region));

            float hills =
                DeterministicNoise.FractalValueNoise(
                    hillSeed,
                    worldX,
                    worldZ,
                    4,
                    0.006f,
                    2f,
                    0.5f);
            float hillOffset = (hills - 0.5f) * 2f * 7f;

            float mountainNoise =
                DeterministicNoise.FractalValueNoise(
                    mountainSeed,
                    worldX,
                    worldZ,
                    4,
                    0.0022f,
                    2f,
                    0.5f);
            float ridge =
                1f - Mathf.Abs(mountainNoise * 2f - 1f);
            ridge = Mathf.Pow(Mathf.Clamp01(ridge), 2.2f);
            float mountainOffset = ridge * 34f * reliefMask;

            float detail =
                DeterministicNoise.FractalValueNoise(
                    detailSeed,
                    worldX,
                    worldZ,
                    2,
                    0.028f,
                    2f,
                    0.5f);
            float detailOffset = (detail - 0.5f) * 2f * 1.8f;
            float localHillStrength =
                Mathf.Lerp(0.45f, 1f, reliefMask);

            return
                2f +
                hillOffset * localHillStrength +
                mountainOffset +
                detailOffset;
        }

        private static LandformMetrics Measure(
            int seed,
            StylizedLandformSettings settings,
            float span,
            int samplesPerSide)
        {
            float step = span / (samplesPerSide - 1);
            float halfSpan = span * 0.5f;
            var heights =
                new float[samplesPerSide, samplesPerSide];
            var buildable =
                new bool[samplesPerSide, samplesPerSide];

            int plainCount = 0;
            int highlandCount = 0;
            int scarpCount = 0;
            bool allFinite = true;

            for (int z = 0; z < samplesPerSide; z++)
            {
                for (int x = 0; x < samplesPerSide; x++)
                {
                    float worldX = x * step - halfSpan;
                    float worldZ = z * step - halfSpan;
                    StylizedLandformSample sample =
                        StylizedLandformSampler.Sample(
                            seed,
                            worldX,
                            worldZ,
                            settings);

                    heights[x, z] = sample.Height;
                    allFinite &=
                        !float.IsNaN(sample.Height) &&
                        !float.IsInfinity(sample.Height);
                    if (sample.PlainMask >= 0.7f)
                        plainCount++;
                    if (sample.HighlandMask >= 0.3f)
                        highlandCount++;
                    if (sample.ScarpMask >= 0.25f)
                        scarpCount++;
                }
            }

            int buildableCount = 0;
            int steepCount = 0;
            for (int z = 1; z < samplesPerSide - 1; z++)
            {
                for (int x = 1; x < samplesPerSide - 1; x++)
                {
                    float dx =
                        (heights[x + 1, z] - heights[x - 1, z]) /
                        (2f * step);
                    float dz =
                        (heights[x, z + 1] - heights[x, z - 1]) /
                        (2f * step);
                    float slope =
                        Mathf.Atan(
                            Mathf.Sqrt(dx * dx + dz * dz)) *
                        Mathf.Rad2Deg;

                    bool isBuildable = slope <= 8f;
                    buildable[x, z] = isBuildable;
                    if (isBuildable)
                        buildableCount++;
                    if (slope >= 18f)
                        steepCount++;
                }
            }

            int interiorCount =
                (samplesPerSide - 2) * (samplesPerSide - 2);
            int sampleCount = samplesPerSide * samplesPerSide;

            return new LandformMetrics(
                allFinite,
                (float)buildableCount / interiorCount,
                (float)FindLargestRegion(buildable) / interiorCount,
                (float)steepCount / interiorCount,
                (float)plainCount / sampleCount,
                (float)highlandCount / sampleCount,
                (float)scarpCount / sampleCount);
        }

        private static int FindLargestRegion(bool[,] cells)
        {
            int width = cells.GetLength(0);
            int height = cells.GetLength(1);
            var visited = new bool[width, height];
            int largest = 0;
            var queue = new Queue<Vector2Int>();

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!cells[x, z] || visited[x, z])
                        continue;

                    int count = 0;
                    visited[x, z] = true;
                    queue.Enqueue(new Vector2Int(x, z));

                    while (queue.Count > 0)
                    {
                        Vector2Int current = queue.Dequeue();
                        count++;

                        TryEnqueue(
                            current.x - 1,
                            current.y,
                            cells,
                            visited,
                            queue);
                        TryEnqueue(
                            current.x + 1,
                            current.y,
                            cells,
                            visited,
                            queue);
                        TryEnqueue(
                            current.x,
                            current.y - 1,
                            cells,
                            visited,
                            queue);
                        TryEnqueue(
                            current.x,
                            current.y + 1,
                            cells,
                            visited,
                            queue);
                    }

                    largest = Mathf.Max(largest, count);
                }
            }

            return largest;
        }

        private static void TryEnqueue(
            int x,
            int z,
            bool[,] cells,
            bool[,] visited,
            Queue<Vector2Int> queue)
        {
            if (x < 0 || z < 0 ||
                x >= cells.GetLength(0) ||
                z >= cells.GetLength(1) ||
                visited[x, z] ||
                !cells[x, z])
            {
                return;
            }

            visited[x, z] = true;
            queue.Enqueue(new Vector2Int(x, z));
        }

        private static void AssertFinite(
            float value,
            string label,
            float x,
            float z)
        {
            Assert.That(
                float.IsNaN(value) || float.IsInfinity(value),
                Is.False,
                $"Non-finite {label} at ({x}, {z}).");
        }

        private static void AssertMask(
            float value,
            string label,
            float x,
            float z)
        {
            AssertFinite(value, label, x, z);
            Assert.That(
                value,
                Is.InRange(0f, 1f),
                $"Out-of-range {label} mask at ({x}, {z}).");
        }

        private readonly struct PlayerScale
        {
            public int Players { get; }
            public float Span { get; }

            public PlayerScale(int players, float span)
            {
                Players = players;
                Span = span;
            }
        }

        private readonly struct LandformMetrics
        {
            public bool AllFinite { get; }
            public float BuildableFraction { get; }
            public float LargestBuildableFraction { get; }
            public float SteepFraction { get; }
            public float PlainFraction { get; }
            public float HighlandFraction { get; }
            public float ScarpFraction { get; }

            public LandformMetrics(
                bool allFinite,
                float buildableFraction,
                float largestBuildableFraction,
                float steepFraction,
                float plainFraction,
                float highlandFraction,
                float scarpFraction)
            {
                AllFinite = allFinite;
                BuildableFraction = buildableFraction;
                LargestBuildableFraction = largestBuildableFraction;
                SteepFraction = steepFraction;
                PlainFraction = plainFraction;
                HighlandFraction = highlandFraction;
                ScarpFraction = scarpFraction;
            }

            public override string ToString()
            {
                return
                    $"buildable={BuildableFraction:P1}, " +
                    $"largestBuildable={LargestBuildableFraction:P1}, " +
                    $"plain={PlainFraction:P1}, " +
                    $"highland={HighlandFraction:P1}, " +
                    $"scarp={ScarpFraction:P1}, " +
                    $"steep={SteepFraction:P1}, " +
                    $"finite={AllFinite}";
            }
        }
    }
}
