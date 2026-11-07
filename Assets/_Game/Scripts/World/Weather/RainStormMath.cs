using UnityEngine;

namespace LittleCastle.World.Weather
{
    /// <summary>Pure deterministic weather helpers; visual randomness never changes authoritative world state.</summary>
    public static class RainStormMath
    {
        public static float Intensity(RainMode mode)
        {
            switch (mode)
            {
                case RainMode.Drizzle: return 0.18f;
                case RainMode.Rain: return 0.48f;
                case RainMode.Downpour: return 0.82f;
                case RainMode.Storm: return 1f;
                default: return 0f;
            }
        }

        public static uint Hash(uint input)
        {
            input ^= input >> 16;
            input *= 0x7feb352du;
            input ^= input >> 15;
            input *= 0x846ca68bu;
            return input ^ (input >> 16);
        }

        public static float Unit(uint seed) => (Hash(seed) & 0x00ffffffu) / 16777215f;

        public static float Approach(float from, float to, float seconds, float delta)
        {
            if (delta <= 0f) return from;
            if (seconds <= 0f) return to;
            return Mathf.Lerp(from, to, 1f - Mathf.Exp(-Mathf.Min(delta, 1f) / seconds));
        }

        public static float Wetness(float current, float rain, float delta, float wetSeconds, float drySeconds)
        {
            rain = Mathf.Clamp01(rain);
            float target = rain > 0.001f ? rain : 0f;
            return Mathf.Clamp01(Approach(current, target, target > current ? wetSeconds : drySeconds, delta));
        }

        public static int Cell(float worldCoordinate, float cellSize) =>
            Mathf.FloorToInt(worldCoordinate / Mathf.Max(0.1f, cellSize));
    }
}
