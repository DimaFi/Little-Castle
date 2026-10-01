using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Stateless deterministic 2D value/fractal noise.
    /// It never touches UnityEngine.Random state.
    /// </summary>
    public static class DeterministicNoise
    {
        public static float FractalValueNoise(
            int seed,
            float x,
            float z,
            int octaves,
            float frequency,
            float lacunarity,
            float persistence)
        {
            float total = 0f;
            float amplitude = 1f;
            float amplitudeSum = 0f;
            float currentFrequency = Mathf.Max(0.000001f, frequency);
            int safeOctaves = Mathf.Max(1, octaves);

            for (int octave = 0; octave < safeOctaves; octave++)
            {
                int octaveSeed = Hash(seed, octave, 0x51ED270B);
                total += ValueNoise(octaveSeed, x * currentFrequency, z * currentFrequency) * amplitude;
                amplitudeSum += amplitude;
                amplitude *= persistence;
                currentFrequency *= lacunarity;
            }

            return amplitudeSum > 0f ? total / amplitudeSum : 0f;
        }

        public static float ValueNoise(int seed, float x, float z)
        {
            int x0 = Mathf.FloorToInt(x);
            int z0 = Mathf.FloorToInt(z);
            int x1 = x0 + 1;
            int z1 = z0 + 1;

            float tx = Smooth(x - x0);
            float tz = Smooth(z - z0);

            float a = Hash01(seed, x0, z0);
            float b = Hash01(seed, x1, z0);
            float c = Hash01(seed, x0, z1);
            float d = Hash01(seed, x1, z1);

            return Mathf.LerpUnclamped(
                Mathf.LerpUnclamped(a, b, tx),
                Mathf.LerpUnclamped(c, d, tx),
                tz);
        }

        public static int Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (int)h;
            }
        }

        private static float Hash01(int seed, int x, int z)
        {
            unchecked
            {
                uint h = (uint)Hash(seed, x, z);
                return (h & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
