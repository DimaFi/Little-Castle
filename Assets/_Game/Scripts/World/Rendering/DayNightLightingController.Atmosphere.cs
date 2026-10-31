using UnityEngine;
using LittleCastle.Rendering;

namespace LittleCastle.World
{
    public sealed partial class DayNightLightingController
    {
        [Header("Unified atmosphere (optional; null preserves scene lighting)")]
        [SerializeField] private AtmosphereProfile atmosphereProfile;
        [SerializeField, Range(0, 1)] private float overcast;
        [SerializeField, Range(0, 1)] private float precipitation;
        [SerializeField] private TerrainStarterPreviewControls.PreviewQuality atmosphereQuality = TerrainStarterPreviewControls.PreviewQuality.Medium;
        public AtmosphereProfile UnifiedProfile { get => atmosphereProfile; set => atmosphereProfile = value; }
        public TerrainStarterPreviewControls.PreviewQuality AtmosphereQuality => atmosphereQuality;
        public bool HasAtmosphereQualityOverride { get; private set; }
        public void SetAtmosphereQuality(TerrainStarterPreviewControls.PreviewQuality quality)
        {
            atmosphereQuality = quality;
            HasAtmosphereQualityOverride = true;
        }
        public void ClearAtmosphereQualityOverride()
        {
            HasAtmosphereQualityOverride = false;
            observedQualityLevel = -1;
        }

        float localSnow, localSnowfall, smoothSnow, smoothOvercast;
        int atmosphereFrame = -1;
        int observedQualityLevel = -1;
        Vector2 cloudDrift;
        bool atmosphereInitialized, previewAtmosphere;
        bool atmosphereSkyApplied;
        Light shadowControlledSun;
        LightShadows previousSunShadows;
        float previewHour, previewSnow, previewOvercast;
        static readonly int TransmissionId = Shader.PropertyToID("_LC_AtmosphereTransmission");
        static readonly int CloudShadowId = Shader.PropertyToID("_LC_CloudShadow");
        static readonly int CloudShadowDriftId = Shader.PropertyToID("_LC_CloudShadowDrift");
        static readonly int GroundBounceId = Shader.PropertyToID("_LC_AtmosphereGroundBounce");
        static readonly int GradeId = Shader.PropertyToID("_LC_AtmosphereGrade");

        /// <summary>Visual adapter for a weather provider; never advances a clock or modifies the map.</summary>
        public void SetWeatherPresentation(float cloudiness, float rainOrSnow)
        {
            overcast = AtmosphereProfile.SanitizeUnit(cloudiness);
            precipitation = AtmosphereProfile.SanitizeUnit(rainOrSnow);
        }

        public void SetLocalEnvironmentPresentation(float snowCoverage, float snowfall)
        {
            localSnow = AtmosphereProfile.SanitizeUnit(snowCoverage);
            localSnowfall = AtmosphereProfile.SanitizeUnit(snowfall);
        }

        /// <summary>Local Play Mode preview, deliberately does not call RestoreState on the world clock.</summary>
        public void PreviewAtmosphere(float hour, float snow, float cloudiness)
        {
            if (!Application.isPlaying) return;
            previewAtmosphere = true;
            previewHour = Mathf.Repeat(hour, 24);
            previewSnow = AtmosphereProfile.SanitizeUnit(snow);
            previewOvercast = AtmosphereProfile.SanitizeUnit(cloudiness);
            ApplyLighting();
        }

        public void EndAtmospherePreview()
        {
            previewAtmosphere = false;
            atmosphereInitialized = false;
            ApplyLighting();
        }

        private void ApplyAtmosphereProfile(float daylight, float twilight, float solarAngle)
        {
            var profile = atmosphereProfile;
            if (!profile || !profile.IsComplete)
            {
                ResetAtmosphereGlobals();
                RestoreAtmosphereSun();
                if (atmosphereSkyApplied)
                {
                    // Discard only our runtime clone, then reconstruct the legacy sky from its source.
                    ReleaseRuntimeSkybox();
                    UpdateSky(daylight, twilight);
                    atmosphereSkyApplied = false;
                }
                return;
            }
            int qualityLevel = QualitySettings.GetQualityLevel();
            if (!HasAtmosphereQualityOverride && observedQualityLevel != qualityLevel)
            {
                observedQualityLevel = qualityLevel;
                string[] names = QualitySettings.names;
                if (qualityLevel >= 0 && qualityLevel < names.Length &&
                    System.Enum.TryParse(names[qualityLevel], true, out TerrainStarterPreviewControls.PreviewQuality projectQuality))
                    atmosphereQuality = projectQuality;
            }
            float targetSnow = previewAtmosphere ? previewSnow : localSnow;
            float targetOvercast = previewAtmosphere ? previewOvercast :
                Mathf.Max(overcast, Mathf.Max(precipitation, localSnowfall) * .88f);
            if (profile.cloudShape == AtmosphereCloudShape.Overcast) targetOvercast = Mathf.Max(.85f, targetOvercast);
            // Multiple explicit refreshes within one frame must not integrate drift more than once.
            if (!atmosphereInitialized || previewAtmosphere)
            {
                smoothSnow = targetSnow;
                smoothOvercast = targetOvercast;
                atmosphereInitialized = true;
            }
            if (atmosphereFrame != Time.frameCount)
            {
                atmosphereFrame = Time.frameCount;
                float blend = 1 - Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime, .1f) / Mathf.Max(.1f, profile.weatherBlendSeconds));
                smoothSnow = Mathf.Lerp(smoothSnow, targetSnow, blend);
                smoothOvercast = Mathf.Lerp(smoothOvercast, targetOvercast, blend);
                Vector4 wind = Shader.GetGlobalVector("_LC_Wind");
                float elapsed = timeSystem != null ? (float)timeSystem.GameHoursAdvancedLastTick : 0;
                cloudDrift += new Vector2(wind.x, wind.y) * wind.w * wind.z * elapsed * .12f;
                // Value noise repeats at 256 cells, so wrapping never visibly jumps.
                cloudDrift.x = Mathf.Repeat(cloudDrift.x, 256);
                cloudDrift.y = Mathf.Repeat(cloudDrift.y, 256);
            }
            float elevation = Mathf.Asin(Mathf.Sin(solarAngle * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
            AtmosphereLook look = profile.Evaluate(daylight, twilight, elevation, smoothSnow);
            float directAttenuation = 1 - smoothOvercast * profile.overcastSunAttenuation;
            if (shadowControlledSun != sun) RestoreAtmosphereSun();
            if (sun)
            {
                if (shadowControlledSun != sun)
                {
                    RestoreAtmosphereSun();
                    shadowControlledSun = sun;
                    previousSunShadows = sun.shadows;
                }
                sun.color = look.direct;
                sun.intensity = look.sunIntensity * daylight * directAttenuation;
                sun.shadows = atmosphereQuality == TerrainStarterPreviewControls.PreviewQuality.Low ? LightShadows.Hard : LightShadows.Soft;
            }
            if (moon)
            {
                // UpdateMoon already reconstructed the legacy orbit this frame. Only a complete,
                // explicitly opted-in cycle overrides it, so disabling the option restores it immediately.
                if (profile.artDirectedMoonOrbit)
                    moon.transform.rotation = profile.EvaluateMoonRotation(solarAngle, sunAzimuth);
                moon.color = Color.Lerp(profile.blueNight.look.direct, profile.snowyMoonlight.look.direct, smoothSnow);
                moon.intensity = look.moonIntensity * Mathf.Pow(1 - daylight, 1.35f) * Mathf.Lerp(1, .65f, smoothOvercast);
                if (profile.artDirectedMoonOrbit)
                    moon.intensity *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.025f, .015f, -moon.transform.forward.y));
            }
            RenderSettings.ambientSkyColor = look.ambientSky;
            RenderSettings.ambientEquatorColor = look.ambientEquator;
            RenderSettings.ambientGroundColor = look.ambientGround;
            if (controlFog)
            {
                RenderSettings.fogColor = Color.Lerp(look.fog, look.cloudShade, smoothOvercast * .24f);
                RenderSettings.fogStartDistance = look.fogStart * Mathf.Lerp(1, .60f, smoothOvercast);
                RenderSettings.fogEndDistance = Mathf.Max(RenderSettings.fogStartDistance + 1,
                    look.fogEnd * Mathf.Lerp(1, .64f, smoothOvercast));
            }
            Shader.SetGlobalFloat(TransmissionId, look.transmission * directAttenuation);
            Shader.SetGlobalColor(GroundBounceId, StylizedLightingGlobals.ToWorkingColor(look.groundBounce) *
                (look.groundBounceStrength * daylight * directAttenuation));
            Shader.SetGlobalVector(GradeId, new Vector4(look.toneMapping, look.exposure, 0, 0));
            Material sky = GetPresentationSkybox();
            if (!sky) { Shader.SetGlobalVector(CloudShadowId, Vector4.zero); return; }
            atmosphereSkyApplied = true;
            sky.SetColor("_ZenithColor", Color.Lerp(look.zenith, look.cloudShade, smoothOvercast * .48f));
            sky.SetColor("_HorizonColor", Color.Lerp(look.horizon, look.cloudLight, smoothOvercast * .32f));
            sky.SetColor("_LowerSkyColor", look.lowerSky);
            sky.SetColor("_TwilightColor", profile.goldenHour.look.horizon);
            sky.SetColor("_SunColor", look.direct * 1.45f);
            sky.SetColor("_MoonColor", Color.Lerp(profile.blueNight.look.direct, profile.snowyMoonlight.look.direct, smoothSnow) * 1.15f);
            sky.SetColor("_CloudLightColor", look.cloudLight);
            sky.SetColor("_CloudShadeColor", look.cloudShade);
            sky.SetFloat("_CloudCoverage", Mathf.Lerp(profile.cloudCoverage, .96f, smoothOvercast));
            sky.SetFloat("_CloudStrength", profile.cloudOpacity);
            sky.SetFloat("_CloudScale", profile.cloudScale);
            sky.SetFloat("_CloudAltitude", profile.cloudAltitude);
            sky.SetFloat("_CloudShape", (float)profile.cloudShape);
            sky.SetFloat("_HighCloudCoverage", profile.highCloudCoverage);
            sky.SetFloat("_CloudQuality", (int)atmosphereQuality);
            sky.SetFloat("_CloudWindDriven", 1);
            sky.SetVector("_CloudDrift", new Vector4(cloudDrift.x, cloudDrift.y, 0, 0));
            // UpdateSky ran before the profile; synchronize its owned clone after rotating the real light.
            if (moon)
            {
                Vector3 direction = -moon.transform.forward;
                sky.SetVector(MoonDirectionId, new Vector4(direction.x, direction.y, direction.z, 0));
            }
            sky.SetFloat("_MoonPhase", profile.SanitizedMoonPhase);
            sky.SetFloat("_MoonAngularDiameter", profile.MoonAngularDiameter);
            sky.SetFloat("_MoonPhaseRotation", profile.MoonPhaseRotation);
            sky.SetFloat("_StarVisibility", Mathf.Pow(Mathf.Clamp01(1 - daylight - twilight * .58f), 1.55f) * look.starBrightness);
            float shadowStrength = atmosphereQuality >= TerrainStarterPreviewControls.PreviewQuality.High
                && profile.cloudShape != AtmosphereCloudShape.HighThin
                ? profile.cloudShadowStrength * profile.cloudOpacity * daylight * (1 - smoothOvercast) : 0;
            Shader.SetGlobalVector(CloudShadowId, new Vector4(shadowStrength, .012f * profile.cloudScale,
                profile.cloudCoverage, profile.cloudAltitude));
            Shader.SetGlobalVector(CloudShadowDriftId, new Vector4(cloudDrift.x, cloudDrift.y, 0, 0));
        }

        private static void ResetAtmosphereGlobals()
        {
            Shader.SetGlobalFloat(TransmissionId, 0);
            Shader.SetGlobalColor(GroundBounceId, Color.clear);
            Shader.SetGlobalVector(GradeId, Vector4.zero);
            Shader.SetGlobalVector(CloudShadowId, Vector4.zero);
        }

        private void RestoreAtmosphereSun()
        {
            if (shadowControlledSun) shadowControlledSun.shadows = previousSunShadows;
            shadowControlledSun = null;
        }
    }
}
