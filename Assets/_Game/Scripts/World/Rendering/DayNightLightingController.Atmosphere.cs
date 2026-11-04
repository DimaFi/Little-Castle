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
        [SerializeField, Range(0, 1)] private float stormStrength;
        [Header("Legacy scene storm bank (when no complete AtmosphereProfile exists)")]
        [SerializeField, Range(.01f, .35f)] private float legacyStormBankLower = .065f;
        [SerializeField, Range(.35f, 1f)] private float legacyStormBankReach = .78f;
        [SerializeField, Range(0f, 1f)] private float legacyStormBankDensity = .84f;
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

        float localSnow, localSnowfall, smoothSnow, smoothOvercast, smoothStorm;
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

        /// <summary>Explicit storm look: this is weather, not a second clock or sky system.</summary>
        public void SetStormPresentation(float strength)
        {
            stormStrength = AtmosphereProfile.SanitizeUnit(strength);
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
            float stormBlend = 1f - Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime, .1f) / 2.5f);
            smoothStorm = Mathf.Lerp(smoothStorm, stormStrength, stormBlend);
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
                ApplyLegacyStormLook();
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
            float directAttenuation = (1 - smoothOvercast * profile.overcastSunAttenuation)
                * Mathf.Lerp(1f, .58f, smoothStorm);
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
            RenderSettings.ambientSkyColor = look.ambientSky * Mathf.Lerp(1f, .65f, smoothStorm);
            RenderSettings.ambientEquatorColor = look.ambientEquator * Mathf.Lerp(1f, .68f, smoothStorm);
            RenderSettings.ambientGroundColor = look.ambientGround * Mathf.Lerp(1f, .72f, smoothStorm);
            if (controlFog)
            {
                RenderSettings.fogColor = Color.Lerp(
                    Color.Lerp(look.fog, look.cloudShade, smoothOvercast * .24f),
                    new Color(.24f,.30f,.36f,1f), smoothStorm * .45f);
                RenderSettings.fogStartDistance = look.fogStart * Mathf.Lerp(1, .60f, smoothOvercast);
                RenderSettings.fogEndDistance = Mathf.Max(RenderSettings.fogStartDistance + 1,
                    look.fogEnd * Mathf.Lerp(1, .64f, smoothOvercast) * Mathf.Lerp(1f, .76f, smoothStorm));
            }
            Shader.SetGlobalFloat(TransmissionId, look.transmission * directAttenuation);
            Shader.SetGlobalColor(GroundBounceId, StylizedLightingGlobals.ToWorkingColor(look.groundBounce) *
                (look.groundBounceStrength * daylight * directAttenuation));
            Shader.SetGlobalVector(GradeId, new Vector4(look.toneMapping, look.exposure, 0, 0));
            Material sky = GetPresentationSkybox();
            if (!sky) { Shader.SetGlobalVector(CloudShadowId, Vector4.zero); return; }
            atmosphereSkyApplied = true;
            sky.SetColor("_ZenithColor", Color.Lerp(
                Color.Lerp(look.zenith, look.cloudShade, smoothOvercast * .48f),
                new Color(.17f,.22f,.28f,1f), smoothStorm * .72f));
            sky.SetColor("_HorizonColor", Color.Lerp(
                Color.Lerp(look.horizon, look.cloudLight, smoothOvercast * .32f),
                new Color(.27f,.31f,.36f,1f), smoothStorm * .54f));
            sky.SetColor("_LowerSkyColor", look.lowerSky);
            sky.SetColor("_TwilightColor", profile.goldenHour.look.horizon);
            sky.SetColor("_SunColor", look.direct * 1.45f);
            sky.SetColor("_MoonColor", Color.Lerp(profile.blueNight.look.direct, profile.snowyMoonlight.look.direct, smoothSnow) * 1.15f);
            sky.SetColor("_CloudLightColor", Color.Lerp(look.cloudLight, new Color(.38f,.45f,.52f,1f), smoothStorm));
            sky.SetColor("_CloudShadeColor", Color.Lerp(look.cloudShade, new Color(.13f,.18f,.24f,1f), smoothStorm));
            sky.SetFloat("_CloudCoverage", Mathf.Lerp(profile.cloudCoverage, .96f, smoothOvercast));
            sky.SetFloat("_CloudStrength", Mathf.Lerp(profile.cloudOpacity, 1f, smoothStorm));
            sky.SetFloat("_CloudScale", profile.cloudScale);
            sky.SetFloat("_CloudAltitude", profile.cloudAltitude);
            sky.SetFloat("_CloudShape", smoothStorm > .45f ? 3f : (float)profile.cloudShape);
            sky.SetFloat("_HighCloudCoverage", Mathf.Lerp(profile.highCloudCoverage, .76f, smoothStorm));
            sky.SetFloat("_StormCloudMass", smoothStorm);
            sky.SetFloat("_StormCloudBase", profile.stormBankLower);
            sky.SetFloat("_StormCloudReach", profile.stormBankReach);
            sky.SetFloat("_StormCloudDensity", profile.stormBankDensity);
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

        // A complete atmosphere profile is optional in existing user-authored scenes.
        // Legacy mode uses the controller's OWN runtime sky clone and restores source
        // cloud properties when the storm stops; never modifies the authored material.
        private void ApplyLegacyStormLook()
        {
            if (sun) sun.intensity *= Mathf.Lerp(1f, .40f, smoothStorm);
            float ambient = Mathf.Lerp(1f, .58f, smoothStorm);
            RenderSettings.ambientSkyColor *= ambient;
            RenderSettings.ambientEquatorColor *= ambient;
            RenderSettings.ambientGroundColor *= Mathf.Lerp(1f, .70f, smoothStorm);
            if (controlFog)
            {
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor,
                    new Color(.23f,.29f,.35f,1f), smoothStorm * .4f);
                RenderSettings.fogEndDistance *= Mathf.Lerp(1f, .68f, smoothStorm);
            }

            Material sky = GetPresentationSkybox();
            if (!sky) return;
            Material source = skyboxMaterial;
            if (!source || !source.shader ||
                source.shader.name != "Little Castle/Sky/Stylized Day Night") return;
            sky.SetFloat("_CloudWindDriven", Mathf.Lerp(source.GetFloat("_CloudWindDriven"), 1f, smoothStorm));
            sky.SetFloat("_CloudShape", smoothStorm > .45f ? 3f : source.GetFloat("_CloudShape"));
            sky.SetFloat("_CloudStrength", Mathf.Lerp(source.GetFloat("_CloudStrength"), 1f, smoothStorm));
            sky.SetFloat("_CloudCoverage", Mathf.Lerp(source.GetFloat("_CloudCoverage"), .98f, smoothStorm));
            sky.SetFloat("_HighCloudCoverage", Mathf.Lerp(source.GetFloat("_HighCloudCoverage"), .72f, smoothStorm));
            sky.SetFloat("_StormCloudMass", smoothStorm);
            sky.SetFloat("_StormCloudBase", legacyStormBankLower);
            sky.SetFloat("_StormCloudReach", legacyStormBankReach);
            sky.SetFloat("_StormCloudDensity", legacyStormBankDensity);
            sky.SetColor("_CloudShadeColor", Color.Lerp(source.GetColor("_CloudShadeColor"),
                new Color(.14f,.19f,.26f,1f), smoothStorm));
            sky.SetColor("_CloudLightColor", Color.Lerp(source.GetColor("_CloudLightColor"),
                new Color(.39f,.45f,.51f,1f), smoothStorm));
            sky.SetColor("_ZenithColor", Color.Lerp(sky.GetColor("_ZenithColor"),
                new Color(.17f,.21f,.28f,1f), smoothStorm * .8f));
            sky.SetColor("_HorizonColor", Color.Lerp(sky.GetColor("_HorizonColor"),
                new Color(.27f,.31f,.38f,1f), smoothStorm * .7f));
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
