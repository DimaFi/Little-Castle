using UnityEngine;

namespace LittleCastle.Rendering.ChimneySmoke
{
    /// <summary>Deterministic, stateless, smooth changes. Samples represent older emitted smoke toward the tip.</summary>
    public static class ChimneySmokeMath
    {
        public static float Hash01(int seed, int salt)
        {
            unchecked
            {
                uint x = (uint)(seed ^ (salt * 1640531527));
                x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
                return (x & 0x00ffffff) / 16777215f;
            }
        }

        public static Vector3 Evaluate(float age01, float now, int seed, ChimneySmokeProfile profile, Vector4 wind)
        {
            float t = Mathf.Clamp01(age01);
            float height = Mathf.Max(0.2f, profile.height);
            float speed = Mathf.Max(0.05f, profile.riseSpeed);
            // A point at height y is smoke emitted t * lifetime seconds ago.
            float birth = now - t * height / speed;
            float phase = Hash01(seed, 3) * 200f;
            float frequency = Mathf.Max(0.01f, profile.curlFrequency);
            float x = (Mathf.PerlinNoise(phase + birth * frequency * 0.33f, 8f + t * 1.5f) - 0.5f) * 2f;
            float z = (Mathf.PerlinNoise(19f + t * 1.7f, phase + birth * frequency * 0.29f) - 0.5f) * 2f;
            float spreading = profile.curlAmplitude * (0.15f + t * 0.85f);
            Vector2 direction = new Vector2(wind.x, wind.y);
            if (direction.sqrMagnitude > 0.0001f) direction.Normalize();
            // The shader contract is _LC_Wind.xy = horizontal direction, z = strength, w = speed.
            float drift = t * height / speed * Mathf.Max(0f, wind.z) *
                          profile.windInfluence * Mathf.Max(0f, wind.w) * profile.windSpeed * 0.25f;
            return new Vector3(x * spreading + direction.x * drift, t * height, z * spreading + direction.y * drift);
        }

        public static float ShapeAlpha(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.09f)) *
                   (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.62f) / 0.38f)));
        }

        public static float Width(float t, ChimneySmokeProfile profile) =>
            Mathf.Lerp(profile.baseWidth, profile.topWidth, Mathf.Pow(Mathf.Clamp01(t), 0.72f));
    }
}
