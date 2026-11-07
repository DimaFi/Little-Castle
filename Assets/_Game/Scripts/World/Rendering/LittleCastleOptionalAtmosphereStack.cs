using UnityEngine;
using UnityEngine.Rendering;
using LittleCastle.World;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Opt-in presentation bridge for the pinned MIT sky/fog UPM packages.
    /// Creates only an in-memory Play Mode Volume. Does not touch the world,
    /// existing map, shared profiles, prefab sources or renderer assets.
    /// This requires isolated Renderer Data with BOTH third-party features.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class LittleCastleOptionalAtmosphereStack : MonoBehaviour
    {
        public enum SkySource { ExistingStylized, PhysicallyBased }

        [Header("Experimental (OFF by default)")]
        [SerializeField] private bool enableExperimentalStack;
        [SerializeField] private SkySource skySource = SkySource.ExistingStylized;
        [SerializeField] private bool keepStylizedStormClouds = true;
        [SerializeField] private DayNightLightingController atmosphere;

        [Header("Physically Based Sky")]
        [SerializeField] private bool enablePhysicalScattering = true;

        [Header("Cristian Qiu volumetric lighting")]
        [SerializeField] private bool enableVolumetricFog;
        [SerializeField] private bool followWeatherForFog = true;
        [SerializeField, Range(0f, 2f)] private float clearFogMultiplier = .55f;
        [SerializeField, Range(0f, 2f)] private float stormFogMultiplier = 1.25f;
        [SerializeField, Range(0f, 1f)] private float fogDensity = .08f;
        [SerializeField, Range(0f, 512f)] private float fogDistance = 120f;
        [SerializeField] private float fogBaseHeight = 0f;
        [SerializeField] private float fogUpperHeight = 45f;
        [SerializeField, Range(8, 256)] private int fogSteps = 32;
        [SerializeField] private bool enableMainLightContribution = true;
        [SerializeField] private bool enableSelectedAdditionalLights;

        private GameObject runtimeOwner;
        private VolumeProfile runtimeProfile;
        private VisualEnvironment visualEnvironment;
        private PhysicallyBasedSky physicalSky;
        private VolumetricFogVolumeComponent volumetricFog;
        private Material cachedStylizedSky;
        private Material savedSky;
        private Texture savedCustomReflection;
        private DefaultReflectionMode savedReflectionMode;
        private bool savedLegacyFog;
        private bool hadFogSnapshot;
        private bool physicalActive;

        public bool IsIntegrationRequested => enableExperimentalStack;
        public bool IsPhysicalSkyActive => physicalActive;

        public void Configure(DayNightLightingController lighting, SkySource source, bool fog)
        {
            atmosphere = lighting;
            skySource = source;
            enableVolumetricFog = fog;
            enableExperimentalStack = fog || source == SkySource.PhysicallyBased;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (!enableExperimentalStack)
            {
                if (runtimeOwner) Release();
                return;
            }

            if (!atmosphere)
                atmosphere = FindAnyObjectByType<DayNightLightingController>();
            if (!runtimeOwner) CreateRuntimeProfile();

            bool wantPhysical = skySource == SkySource.PhysicallyBased;
            // The stylized sky owns the cloud volume during rain and storms.
            if (keepStylizedStormClouds && atmosphere &&
                (atmosphere.RequestedStormStrength > .05f ||
                 atmosphere.RequestedPrecipitation > .09f))
                wantPhysical = false;

            if (atmosphere && atmosphere.ExternalSkyPresenterActive != wantPhysical)
            {
                atmosphere.ExternalSkyPresenterActive = wantPhysical;
                if (!wantPhysical) atmosphere.ApplyLighting();
            }

            if (!wantPhysical)
            {
                Material current = RenderSettings.skybox;
                if (current && current.shader &&
                    current.shader.name == "Little Castle/Sky/Stylized Day Night")
                    cachedStylizedSky = current;
                if (cachedStylizedSky)
                    visualEnvironment.customSkyMaterial.Override(cachedStylizedSky);
            }

            visualEnvironment.skyType.Override((int)(wantPhysical
                ? VisualEnvironment.SkyType.PhysicallyBased
                : VisualEnvironment.SkyType.Custom));
            // Preserve Little Castle's existing Trilight ambient authority.
            visualEnvironment.skyAmbientMode.Override(VisualEnvironment.SkyAmbientMode.Static);
            physicalSky.atmosphericScattering.Override(
                wantPhysical && enablePhysicalScattering);

            volumetricFog.enabled.Override(enableVolumetricFog);
            float weather = atmosphere ? Mathf.Max(
                atmosphere.RequestedPrecipitation, atmosphere.RequestedStormStrength) : 0f;
            float density = followWeatherForFog
                ? EvaluateWeatherFogDensity(fogDensity, weather,
                    clearFogMultiplier, stormFogMultiplier)
                : Mathf.Clamp01(fogDensity);
            volumetricFog.density.Override(density);
            volumetricFog.distance.Override(Mathf.Clamp(fogDistance, 0f, 512f));
            volumetricFog.baseHeight.Override(fogBaseHeight);
            volumetricFog.maximumHeight.Override(
                Mathf.Max(fogBaseHeight + .01f, fogUpperHeight));
            volumetricFog.maxSteps.Override(Mathf.Clamp(fogSteps, 8, 256));
            volumetricFog.enableMainLightContribution.Override(
                enableVolumetricFog && enableMainLightContribution);
            volumetricFog.enableAdditionalLightsContribution.Override(
                enableVolumetricFog && enableSelectedAdditionalLights);

            physicalActive = wantPhysical;
            // Without this, the existing linear fog compounds the new fog.
            if (enableVolumetricFog || (wantPhysical && enablePhysicalScattering))
                RenderSettings.fog = false;
            else if (!atmosphere && hadFogSnapshot)
                RenderSettings.fog = savedLegacyFog;
        }

        private void CreateRuntimeProfile()
        {
            savedLegacyFog = RenderSettings.fog;
            savedSky = RenderSettings.skybox;
            savedCustomReflection = RenderSettings.customReflectionTexture;
            savedReflectionMode = RenderSettings.defaultReflectionMode;
            hadFogSnapshot = true;

            runtimeOwner = new GameObject("LC Optional Atmosphere (runtime only)")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            runtimeOwner.transform.SetParent(transform, false);
            var volume = runtimeOwner.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 400f;
            volume.weight = 1f;
            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
            runtimeProfile.name = "LC Optional Atmosphere Runtime Profile";
            volume.sharedProfile = runtimeProfile;
            visualEnvironment = runtimeProfile.Add<VisualEnvironment>(true);
            physicalSky = runtimeProfile.Add<PhysicallyBasedSky>(true);
            volumetricFog = runtimeProfile.Add<VolumetricFogVolumeComponent>(true);
            volumetricFog.enabled.Override(false);

            if (RenderSettings.skybox && RenderSettings.skybox.shader &&
                RenderSettings.skybox.shader.name == "Little Castle/Sky/Stylized Day Night")
                cachedStylizedSky = RenderSettings.skybox;
        }

        private void OnDisable() { if (runtimeOwner) Release(); }
        private void OnDestroy() { if (runtimeOwner) Release(); }

        private void Release()
        {
            if (atmosphere && atmosphere.ExternalSkyPresenterActive)
            {
                atmosphere.ExternalSkyPresenterActive = false;
                atmosphere.ApplyLighting();
            }
            if (hadFogSnapshot)
            {
                RenderSettings.fog = savedLegacyFog;
                RenderSettings.defaultReflectionMode = savedReflectionMode;
                RenderSettings.customReflectionTexture = savedCustomReflection;
                if (physicalActive && !atmosphere) RenderSettings.skybox = savedSky;
            }

            DestroyRuntimeObject(runtimeOwner);
            DestroyRuntimeObject(runtimeProfile);
            runtimeOwner = null;
            runtimeProfile = null;
            visualEnvironment = null;
            physicalSky = null;
            volumetricFog = null;
            cachedStylizedSky = null;
            savedSky = null;
            savedCustomReflection = null;
            physicalActive = false;
            hadFogSnapshot = false;
        }

        private static void DestroyRuntimeObject(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        public static float EvaluateWeatherFogDensity(float baseDensity,
            float weatherStrength, float clearMultiplier, float stormMultiplier)
        {
            return Mathf.Clamp01(Mathf.Clamp01(baseDensity) * Mathf.Lerp(
                Mathf.Clamp(clearMultiplier, 0f, 2f),
                Mathf.Clamp(stormMultiplier, 0f, 2f),
                Mathf.Clamp01(weatherStrength)));
        }

        private void OnValidate()
        {
            clearFogMultiplier = Mathf.Clamp(clearFogMultiplier, 0f, 2f);
            stormFogMultiplier = Mathf.Clamp(stormFogMultiplier, 0f, 2f);
            fogDensity = Mathf.Clamp01(fogDensity);
            fogDistance = Mathf.Clamp(fogDistance, 0f, 512f);
            fogSteps = Mathf.Clamp(fogSteps, 8, 256);
            fogUpperHeight = Mathf.Max(fogBaseHeight + .01f, fogUpperHeight);
        }
    }
}
