using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Diagnostic description of one pure landform sample. Masks are exposed
    /// so tests and later presentation can reason about the authored regions
    /// without reimplementing the height algorithm.
    /// </summary>
    public readonly struct StylizedLandformSample
    {
        public float Height { get; }
        public float PlainMask { get; }
        public float HillMask { get; }
        public float HighlandMask { get; }
        public float ScarpMask { get; }

        public StylizedLandformSample(
            float height,
            float plainMask,
            float hillMask,
            float highlandMask,
            float scarpMask)
        {
            Height = height;
            PlainMask = plainMask;
            HillMask = hillMask;
            HighlandMask = highlandMask;
            ScarpMask = scarpMask;
        }
    }

    /// <summary>
    /// Stateless world-space height sampler for broad cozy meadows, rounded
    /// hill belts and occasional terraced highlands.
    /// </summary>
    public static class StylizedLandformSampler
    {
        private const float MinimumFrequency = 0.000001f;

        /// <summary>
        /// Samples only height for consumers that do not need region masks.
        /// </summary>
        public static float SampleHeight(
            int worldSeed,
            float worldX,
            float worldZ,
            StylizedLandformSettings settings)
        {
            return Sample(worldSeed, worldX, worldZ, settings).Height;
        }

        /// <summary>
        /// Samples height and the smooth landform-region masks from absolute
        /// world coordinates. No chunk coordinate or mutable state is used.
        /// </summary>
        public static StylizedLandformSample Sample(
            int worldSeed,
            float worldX,
            float worldZ,
            StylizedLandformSettings settings)
        {
            if (settings == null)
                settings = new StylizedLandformSettings();

            if (!IsFinite(worldX) || !IsFinite(worldZ))
            {
                return new StylizedLandformSample(
                    settings.BaseHeight,
                    1f,
                    0f,
                    0f,
                    0f);
            }

            float regionSize = settings.RegionSize;
            float regionFrequency =
                Mathf.Max(MinimumFrequency, 1f / regionSize);

            int warpSeed =
                DeterministicNoise.Hash(worldSeed, 2101, 0x4C31);
            int regionSeed =
                DeterministicNoise.Hash(worldSeed, 2102, 0x52A7);
            int meadowSeed =
                DeterministicNoise.Hash(worldSeed, 2103, 0x1D93);
            int hillSeed =
                DeterministicNoise.Hash(worldSeed, 2104, 0x68E5);
            int highlandSeed =
                DeterministicNoise.Hash(worldSeed, 2105, 0x37B9);
            int highlandShapeSeed =
                DeterministicNoise.Hash(worldSeed, 2106, 0x729D);
            int scarpSeed =
                DeterministicNoise.Hash(worldSeed, 2107, 0x2F41);
            int detailSeed =
                DeterministicNoise.Hash(worldSeed, 2108, 0x5B17);

            // A low-frequency domain bend avoids long grid-like region edges
            // while keeping all decisions stable in absolute world space.
            float warpX =
                CenteredNoise(
                    warpSeed,
                    worldX,
                    worldZ,
                    2,
                    regionFrequency * 0.72f) *
                regionSize * 0.14f;
            float warpZ =
                CenteredNoise(
                    DeterministicNoise.Hash(warpSeed, 1, 0x16F1),
                    worldX,
                    worldZ,
                    2,
                    regionFrequency * 0.72f) *
                regionSize * 0.14f;

            float shapedX = worldX + warpX;
            float shapedZ = worldZ + warpZ;

            float region =
                DeterministicNoise.FractalValueNoise(
                    regionSeed,
                    shapedX,
                    shapedZ,
                    3,
                    regionFrequency,
                    2f,
                    0.5f);

            // Higher requested plain coverage moves the relief onset upward.
            // The wide transition creates naturally blended hill margins.
            float normalizedPlainCoverage =
                Mathf.InverseLerp(
                    0.35f,
                    0.8f,
                    settings.PlainCoverage);
            float reliefStart =
                Mathf.Lerp(0.43f, 0.57f, normalizedPlainCoverage);
            float reliefMask =
                SmoothStep(reliefStart, reliefStart + 0.16f, region);
            float plainMask = 1f - reliefMask;

            float highlandSelector =
                DeterministicNoise.FractalValueNoise(
                    highlandSeed,
                    shapedX,
                    shapedZ,
                    2,
                    regionFrequency * 0.78f,
                    2f,
                    0.48f);
            highlandSelector =
                Mathf.Lerp(highlandSelector, region, 0.38f);

            float normalizedHighlandCoverage =
                Mathf.InverseLerp(
                    0.08f,
                    0.35f,
                    settings.HighlandCoverage);
            float highlandStart =
                Mathf.Lerp(
                    0.64f,
                    0.50f,
                    normalizedHighlandCoverage);
            float highlandMask =
                reliefMask *
                SmoothStep(
                    highlandStart,
                    highlandStart + 0.105f,
                    highlandSelector);

            float hillMask =
                reliefMask * (1f - highlandMask * 0.82f);

            float meadow =
                CenteredNoise(
                    meadowSeed,
                    shapedX,
                    shapedZ,
                    2,
                    regionFrequency * 3.1f) *
                settings.MeadowUndulation;

            float hills =
                CenteredNoise(
                    hillSeed,
                    shapedX,
                    shapedZ,
                    3,
                    regionFrequency * 6.2f);
            // Rounded hills have a broad wave plus a smaller companion shape,
            // rather than accumulating unrestricted fractal detail.
            float roundedHills =
                hills * settings.HillHeight * hillMask;

            float highlandShape =
                DeterministicNoise.FractalValueNoise(
                    highlandShapeSeed,
                    shapedX,
                    shapedZ,
                    2,
                    regionFrequency * 2.35f,
                    2f,
                    0.45f);
            float smoothUplift =
                settings.HighlandHeight *
                highlandMask *
                Mathf.Lerp(0.62f, 1f, highlandShape);

            float scarpSelector =
                DeterministicNoise.FractalValueNoise(
                    scarpSeed,
                    shapedX,
                    shapedZ,
                    2,
                    regionFrequency * 3.35f,
                    2f,
                    0.48f);
            float normalizedScarpCoverage =
                Mathf.InverseLerp(
                    0f,
                    0.8f,
                    settings.ScarpCoverage);
            float scarpStart =
                Mathf.Lerp(
                    0.66f,
                    0.45f,
                    normalizedScarpCoverage);
            float scarpMask =
                highlandMask *
                SmoothStep(
                    scarpStart,
                    scarpStart + 0.13f,
                    scarpSelector);

            float terracedUplift =
                SmoothTerrace(
                    smoothUplift,
                    settings.TerraceStepHeight,
                    0.24f);
            float finalUplift =
                Mathf.Lerp(
                    smoothUplift,
                    terracedUplift,
                    scarpMask * settings.TerraceStrength);

            float detail =
                CenteredNoise(
                    detailSeed,
                    worldX,
                    worldZ,
                    2,
                    regionFrequency * 22f);
            float detailStrength =
                settings.DetailAmplitude *
                Mathf.Lerp(0.08f, 1f, reliefMask);

            float height =
                settings.BaseHeight +
                meadow +
                roundedHills +
                finalUplift +
                detail * detailStrength;

            if (!IsFinite(height))
                height = settings.BaseHeight;

            return new StylizedLandformSample(
                height,
                plainMask,
                hillMask,
                highlandMask,
                scarpMask);
        }

        private static float CenteredNoise(
            int seed,
            float worldX,
            float worldZ,
            int octaves,
            float frequency)
        {
            return
                (DeterministicNoise.FractalValueNoise(
                    seed,
                    worldX,
                    worldZ,
                    octaves,
                    frequency,
                    2f,
                    0.5f) -
                 0.5f) *
                2f;
        }

        private static float SmoothTerrace(
            float height,
            float stepHeight,
            float transitionFraction)
        {
            float safeStep = Mathf.Max(0.001f, stepHeight);
            float scaled = height / safeStep;
            float level = Mathf.Floor(scaled);
            float fraction = scaled - level;

            // Shelves occupy most of each height interval; SmoothStep keeps
            // the shorter connecting face finite and heightfield-friendly.
            float transitionStart =
                Mathf.Clamp01(1f - transitionFraction);
            float riser =
                SmoothStep(transitionStart, 1f, fraction);
            return (level + riser) * safeStep;
        }

        private static float SmoothStep(
            float edge0,
            float edge1,
            float value)
        {
            float width = Mathf.Max(0.000001f, edge1 - edge0);
            float t = Mathf.Clamp01((value - edge0) / width);
            return t * t * (3f - 2f * t);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
