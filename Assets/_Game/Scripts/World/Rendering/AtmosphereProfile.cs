using UnityEngine;

namespace LittleCastle.World
{
    public enum AtmosphereCloudShape { Puffy, Stretched, HighThin, Overcast }

    [CreateAssetMenu(menuName = "Little Castle/Rendering/Atmosphere Cycle")]
    public sealed class AtmosphereProfile : ScriptableObject
    {
        public AtmosphereLookProfile sunnyDay, goldenHour, blueNight, snowyMoonlight;
        [Header("Cloud layers (no ray marching)")]
        public AtmosphereCloudShape cloudShape;
        [Range(0, 1)] public float cloudCoverage = .43f;
        [Range(0, 1)] public float cloudOpacity = .88f;
        [Range(.2f, 3)] public float cloudScale = 1;
        [Range(.15f, 2)] public float cloudAltitude = .65f;
        [Range(0, 1)] public float highCloudCoverage = .22f;
        [Range(0, 1)] public float cloudShadowStrength = .16f;
        [Range(0, 1)] public float overcastSunAttenuation = .58f;
        [Header("Moon presentation (same world clock; no camera tracking)")]
        public bool artDirectedMoonOrbit;
        [Tooltip("Minimum daytime / maximum nighttime elevation in degrees. The moon crosses the horizon at sunset and sunrise.")]
        public Vector2 moonElevationRange = new Vector2(-12, 8);
        [Tooltip("Offset from the sun's authored azimuth at the midpoint of the night. The moon traverses 180 degrees between sunset and sunrise.")]
        [Range(-360, 360)] public float moonAzimuthOffset = 180;
        [Tooltip("Outer angular diameter in degrees. Zero preserves the sky material's legacy Moon Size.")]
        [Range(0, 6)] public float moonAngularDiameter;
        [Range(0, 1)] public float moonPhase = .24f;
        [Range(-180, 180)] public float moonPhaseRotation;
        [Min(.1f)] public float weatherBlendSeconds = 4;

        public bool IsComplete => sunnyDay && goldenHour && blueNight && snowyMoonlight;

        public float SanitizedMoonPhase => SanitizeUnit(moonPhase);
        public float MoonAngularDiameter
        {
            get
            {
                float diameter = Mathf.Clamp(FiniteOr(moonAngularDiameter, 0), 0, 6);
                return diameter > 0 ? Mathf.Max(.1f, diameter) : 0;
            }
        }
        public float MoonPhaseRotation => Mathf.Clamp(FiniteOr(moonPhaseRotation, 0), -180, 180);

        /// <summary>Light rotation, also used for the visible disk. Solar angle comes from the existing clock.</summary>
        public Quaternion EvaluateMoonRotation(float solarAngle, float sunAzimuth)
        {
            float lunarAngle = Mathf.Repeat(Mathf.Clamp(FiniteOr(solarAngle, 180), 0, 360) + 180, 360);
            float height = Mathf.Sin(lunarAngle * Mathf.Deg2Rad);
            float minimum = Mathf.Clamp(FiniteOr(moonElevationRange.x, -12), -89, 0);
            float maximum = Mathf.Clamp(FiniteOr(moonElevationRange.y, 8), 0, 89);
            float elevation = height >= 0 ? height * maximum : -height * minimum;
            float azimuth = Mathf.Clamp(FiniteOr(sunAzimuth, 0), -360, 360)
                + Mathf.Clamp(FiniteOr(moonAzimuthOffset, 180), -360, 360) + lunarAngle - 90;
            // A directional light points toward the ground; its negative forward is the visible moon.
            return Quaternion.Euler(elevation, Mathf.Repeat(azimuth, 360), 0);
        }

        private static float FiniteOr(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;

        public AtmosphereLook Evaluate(float daylight, float twilight, float sunElevation, float snow)
        {
            if (!IsComplete) return default;
            return BlendLooks(sunnyDay.look, goldenHour.look, blueNight.look, snowyMoonlight.look,
                daylight, twilight, sunElevation, snow);
        }

        public static AtmosphereLook BlendLooks(AtmosphereLook sunny, AtmosphereLook goldenHour,
            AtmosphereLook blue, AtmosphereLook snowy, float daylight, float twilight, float sunElevation, float snow)
        {
            daylight = SanitizeUnit(daylight);
            twilight = SanitizeUnit(twilight);
            // Elevation makes the golden hour visible before dusk's intensity ramp.
            float golden = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(10, 40, sunElevation));
            golden = Mathf.Clamp01(Mathf.Max(golden, Mathf.Clamp01(twilight) * .78f));
            var day = AtmosphereLook.Lerp(sunny, goldenHour, golden);
            var night = AtmosphereLook.Lerp(blue, snowy, SanitizeUnit(snow));
            var result = AtmosphereLook.Lerp(night, day, daylight);
            // Solar hue follows elevation even when intensity is almost zero at dawn/dusk.
            result.direct = day.direct;
            // Twilight horizon stays coloured while the last direct sunlight disappears.
            result.zenith = Color.Lerp(result.zenith, goldenHour.zenith, twilight * .30f);
            result.horizon = Color.Lerp(result.horizon, goldenHour.horizon, twilight * .64f);
            result.fog = Color.Lerp(result.fog, goldenHour.fog, twilight * .24f);
            return result;
        }

        public static float SanitizeUnit(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp01(value);

        public static float TwilightWeight(float hour, float sunrise, float sunset, float transitionHours)
        {
            float dawnDistance = Mathf.Abs(Mathf.Repeat(hour - sunrise + 12, 24) - 12);
            float duskDistance = Mathf.Abs(Mathf.Repeat(hour - sunset + 12, 24) - 12);
            float distance = Mathf.Min(dawnDistance, duskDistance);
            return 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance / Mathf.Max(.05f, transitionHours)));
        }
    }
}
