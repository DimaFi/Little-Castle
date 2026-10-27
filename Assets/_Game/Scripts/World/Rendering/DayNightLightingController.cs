using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.World
{
    /// <summary>
    /// Presentation-only atmosphere driven by WorldTimeSystem.
    ///
    /// The simulation clock stays authoritative; this component only turns
    /// time into sun/moon rotation, ambient light, fog and skybox parameters.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class DayNightLightingController : MonoBehaviour
    {
        private static readonly int ZenithColorId =
            Shader.PropertyToID("_ZenithColor");

        private static readonly int HorizonColorId =
            Shader.PropertyToID("_HorizonColor");

        private static readonly int LowerSkyColorId =
            Shader.PropertyToID("_LowerSkyColor");

        private static readonly int SunColorId =
            Shader.PropertyToID("_SunColor");

        private static readonly int MoonColorId =
            Shader.PropertyToID("_MoonColor");

        private static readonly int SunDirectionId =
            Shader.PropertyToID("_SunDirection");

        private static readonly int MoonDirectionId =
            Shader.PropertyToID("_MoonDirection");

        private static readonly int SunVisibilityId =
            Shader.PropertyToID("_SunVisibility");

        private static readonly int MoonVisibilityId =
            Shader.PropertyToID("_MoonVisibility");

        private static readonly int StarVisibilityId =
            Shader.PropertyToID("_StarVisibility");

        private static readonly int TwilightStrengthId =
            Shader.PropertyToID("_TwilightStrength");

        private static readonly int DaylightId =
            Shader.PropertyToID("_Daylight");

        [Header("Clock")]
        [SerializeField]
        private WorldTimeSystem timeSystem;

        public WorldTimeSystem TimeSystem =>
            timeSystem;

        public Light Sun =>
            sun;

        public Light Moon =>
            moon;

        [Header("Optional authored palette")]
        [SerializeField] private LightingPaletteProfile palette;
        public LightingPaletteProfile Palette { get => palette; set => palette = value; }

        public float DaylightFactor
        {
            get;
            private set;
        }

        public float TwilightFactor
        {
            get;
            private set;
        }

        [Header("Directional lights")]
        [SerializeField]
        private Light sun;

        [SerializeField]
        private Light moon;

        [SerializeField]
        private float sunAzimuth = -30f;

        [SerializeField]
        [Min(0f)]
        private float maxSunIntensity = 1.1f;

        [SerializeField]
        [Min(0f)]
        private float maxMoonIntensity = 0.14f;

        [SerializeField]
        private Color sunDayColor =
            new Color(1f, 0.96f, 0.84f, 1f);

        [SerializeField]
        private Color sunHorizonColor =
            new Color(1f, 0.56f, 0.31f, 1f);

        [SerializeField]
        private Color moonLightColor =
            new Color(0.58f, 0.68f, 1f, 1f);

        [Header("Transitions")]
        [SerializeField]
        [Min(0.05f)]
        private float dawnTransitionGameHours = 2f;

        [SerializeField]
        [Min(0.05f)]
        private float duskTransitionGameHours = 2f;

        [SerializeField]
        [Min(0.05f)]
        private float twilightGameHours = 1.25f;

        [Header("Skybox")]
        [SerializeField]
        private Material skyboxMaterial;

        private Material runtimeSkyboxMaterial;
        private Material runtimeSkyboxSource;
        private Material previousSkyboxMaterial;

        [SerializeField]
        private Color dayZenith =
            new Color(0.24f, 0.52f, 0.82f, 1f);

        [SerializeField]
        private Color dayHorizon =
            new Color(0.79f, 0.88f, 0.94f, 1f);

        [SerializeField]
        private Color dayLowerSky =
            new Color(0.54f, 0.67f, 0.73f, 1f);

        [SerializeField]
        private Color nightZenith =
            new Color(0.035f, 0.065f, 0.15f, 1f);

        [SerializeField]
        private Color nightHorizon =
            new Color(0.17f, 0.22f, 0.34f, 1f);

        [SerializeField]
        private Color nightLowerSky =
            new Color(0.11f, 0.14f, 0.22f, 1f);

        [SerializeField]
        private Color twilightZenith =
            new Color(0.24f, 0.20f, 0.38f, 1f);

        [SerializeField]
        private Color twilightHorizon =
            new Color(1f, 0.52f, 0.31f, 1f);

        [SerializeField]
        private Color twilightLowerSky =
            new Color(0.48f, 0.32f, 0.38f, 1f);

        [SerializeField]
        [ColorUsage(true, true)]
        private Color skySunColor =
            new Color(1.85f, 1.32f, 0.76f, 1f);

        [SerializeField]
        [ColorUsage(true, true)]
        private Color skyMoonColor =
            new Color(0.83f, 0.92f, 1.25f, 1f);

        [Header("Ambient")]
        [SerializeField]
        private Color dayAmbientSky =
            new Color(0.78f, 0.82f, 0.86f, 1f);

        [SerializeField]
        private Color dayAmbientEquator =
            new Color(0.62f, 0.66f, 0.70f, 1f);

        [SerializeField]
        private Color dayAmbientGround =
            new Color(0.42f, 0.44f, 0.40f, 1f);

        [SerializeField]
        private Color nightAmbientSky =
            new Color(0.25f, 0.31f, 0.46f, 1f);

        [SerializeField]
        private Color nightAmbientEquator =
            new Color(0.20f, 0.25f, 0.36f, 1f);

        [SerializeField]
        private Color nightAmbientGround =
            new Color(0.13f, 0.16f, 0.24f, 1f);

        [Header("Atmospheric fog")]
        [SerializeField]
        private bool controlFog = true;

        [SerializeField]
        private Color dayFogColor =
            new Color(0.70f, 0.79f, 0.84f, 1f);

        [SerializeField]
        private Color nightFogColor =
            new Color(0.16f, 0.21f, 0.31f, 1f);

        [SerializeField]
        private Color twilightFogColor =
            new Color(0.52f, 0.34f, 0.38f, 1f);

        [SerializeField]
        [Min(0f)]
        private float dayFogStart = 230f;

        [SerializeField]
        [Min(1f)]
        private float dayFogEnd = 900f;

        [SerializeField]
        [Min(0f)]
        private float nightFogStart = 170f;

        [SerializeField]
        [Min(1f)]
        private float nightFogEnd = 720f;

        private void Awake()
        {
            if (timeSystem == null)
            {
                timeSystem =
                    FindAnyObjectByType<
                        WorldTimeSystem>();
            }

            if (sun == null)
                sun = RenderSettings.sun;

            if (sun != null)
                RenderSettings.sun = sun;

            Material sky = GetPresentationSkybox();
            if (sky != null)
                RenderSettings.skybox = sky;

            // WorldTimeSystem initializes earlier. Apply once here so shared
            // shader globals do not start one frame behind the atmosphere.
            ApplyLighting();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            Material sky = GetPresentationSkybox();
            if (sky != null)
                RenderSettings.skybox = sky;

            ApplyLighting();
        }

        private void OnDisable() => ReleaseRuntimeSkybox();

        private void OnDestroy() => ReleaseRuntimeSkybox();

        private Material GetPresentationSkybox()
        {
            // Explicit Edit Mode authoring keeps using the serialized material.
            // Play Mode must never animate properties on that shared source asset.
            if (!Application.isPlaying)
                return skyboxMaterial;

            if (runtimeSkyboxMaterial != null && runtimeSkyboxSource == skyboxMaterial)
                return runtimeSkyboxMaterial;

            ReleaseRuntimeSkybox();
            if (skyboxMaterial == null)
                return null;

            previousSkyboxMaterial = RenderSettings.skybox;
            runtimeSkyboxSource = skyboxMaterial;
            runtimeSkyboxMaterial = new Material(skyboxMaterial)
            {
                name = skyboxMaterial.name + " (Day Night Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            return runtimeSkyboxMaterial;
        }

        private void ReleaseRuntimeSkybox()
        {
            if (runtimeSkyboxMaterial != null)
            {
                if (RenderSettings.skybox == runtimeSkyboxMaterial)
                    RenderSettings.skybox = previousSkyboxMaterial;

                if (Application.isPlaying)
                    Destroy(runtimeSkyboxMaterial);
                else
                    DestroyImmediate(runtimeSkyboxMaterial);
            }

            runtimeSkyboxMaterial = null;
            runtimeSkyboxSource = null;
            previousSkyboxMaterial = null;
        }

        private void Start()
        {
            ApplyLighting();
        }

        private void LateUpdate()
        {
            ApplyLighting();
        }

        public void ApplyLighting()
        {
            if (timeSystem == null ||
                timeSystem.Clock == null ||
                timeSystem.Settings == null)
            {
                return;
            }

            float hour =
                timeSystem.CurrentHour;

            float sunrise =
                timeSystem.Settings.DayStartHour;

            float sunset =
                timeSystem.Settings.NightStartHour;

            float daylight =
                GetDaylight(
                    hour,
                    sunrise,
                    sunset);

            float twilight =
                GetTwilight(
                    hour,
                    sunrise,
                    sunset);

            DaylightFactor =
                daylight;

            TwilightFactor =
                twilight;

            float solarAngle =
                GetSolarAngle(
                    hour,
                    sunrise,
                    sunset);

            UpdateSun(
                daylight,
                solarAngle);

            UpdateMoon(
                daylight,
                solarAngle);

            UpdateAmbient(
                daylight,
                twilight);

            UpdateSky(
                daylight,
                twilight);

            UpdateFog(
                daylight,
                twilight);
        }

        private void UpdateSun(
            float daylight,
            float solarAngle)
        {
            if (sun == null)
                return;

            sun.transform.rotation =
                Quaternion.Euler(
                    solarAngle,
                    sunAzimuth,
                    0f);

            sun.intensity =
                (palette != null ? palette.sunIntensity : maxSunIntensity) *
                daylight;

            float highSun =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    daylight);
            if (palette != null)
            {
                float elevation = Mathf.Asin(Mathf.Clamp(-sun.transform.forward.y, -1, 1)) * Mathf.Rad2Deg;
                highSun = palette.SunColorBlend(daylight, elevation);
            }

            sun.color =
                Color.Lerp(
                    palette != null ? palette.sunHorizon : sunHorizonColor,
                    palette != null ? palette.sunDay : sunDayColor,
                    highSun);
        }

        private void UpdateMoon(
            float daylight,
            float solarAngle)
        {
            if (moon == null)
                return;

            moon.transform.rotation =
                Quaternion.Euler(
                    Mathf.Repeat(
                        solarAngle + 180f,
                        360f),
                    sunAzimuth,
                    0f);

            moon.intensity =
                (palette != null ? palette.moonIntensity : maxMoonIntensity) *
                Mathf.Pow(
                    1f - daylight,
                    1.35f);

            moon.color =
                palette != null ? palette.moon : moonLightColor;
        }

        private void UpdateAmbient(
            float daylight,
            float twilight)
        {
            float environmentDay =
                Mathf.Clamp01(
                    daylight +
                    twilight * 0.28f);

            RenderSettings.ambientMode =
                AmbientMode.Trilight;

            RenderSettings.ambientSkyColor =
                Color.Lerp(
                    palette != null ? palette.nightSky : nightAmbientSky,
                    palette != null ? palette.daySky : dayAmbientSky,
                    environmentDay);

            RenderSettings.ambientEquatorColor =
                Color.Lerp(
                    palette != null ? palette.nightEquator : nightAmbientEquator,
                    palette != null ? palette.dayEquator : dayAmbientEquator,
                    environmentDay);

            RenderSettings.ambientGroundColor =
                Color.Lerp(
                    palette != null ? palette.nightGround : nightAmbientGround,
                    palette != null ? palette.dayGround : dayAmbientGround,
                    environmentDay);
        }

        private void UpdateSky(
            float daylight,
            float twilight)
        {
            Material sky = GetPresentationSkybox();
            if (sky == null)
                return;

            if (RenderSettings.skybox !=
                sky)
            {
                RenderSettings.skybox =
                    sky;
            }

            float baseDay =
                Mathf.Clamp01(
                    daylight +
                    twilight * 0.16f);

            Color zenith =
                Color.Lerp(
                    nightZenith,
                    dayZenith,
                    baseDay);

            Color horizon =
                Color.Lerp(
                    nightHorizon,
                    dayHorizon,
                    baseDay);

            Color lower =
                Color.Lerp(
                    nightLowerSky,
                    dayLowerSky,
                    baseDay);

            sky.SetColor(
                ZenithColorId,
                zenith);

            sky.SetColor(
                HorizonColorId,
                horizon);

            sky.SetColor(
                LowerSkyColorId,
                lower);

            sky.SetColor(
                SunColorId,
                skySunColor);

            sky.SetColor(
                MoonColorId,
                skyMoonColor);

            if (sun != null)
            {
                Vector3 visibleSunDirection =
                    -sun.transform.forward;

                sky.SetVector(
                    SunDirectionId,
                    new Vector4(
                        visibleSunDirection.x,
                        visibleSunDirection.y,
                        visibleSunDirection.z,
                        0f));
            }

            if (moon != null)
            {
                Vector3 visibleMoonDirection =
                    -moon.transform.forward;

                sky.SetVector(
                    MoonDirectionId,
                    new Vector4(
                        visibleMoonDirection.x,
                        visibleMoonDirection.y,
                        visibleMoonDirection.z,
                        0f));
            }

            float darkness =
                Mathf.Clamp01(
                    1f -
                    daylight -
                    twilight * 0.58f);

            sky.SetFloat(
                SunVisibilityId,
                Mathf.Clamp01(
                    daylight +
                    twilight * 0.92f));

            sky.SetFloat(
                TwilightStrengthId,
                twilight);

            sky.SetFloat(
                DaylightId,
                daylight);

            sky.SetFloat(
                MoonVisibilityId,
                Mathf.SmoothStep(
                    0.08f,
                    0.72f,
                    darkness));

            sky.SetFloat(
                StarVisibilityId,
                Mathf.Pow(
                    darkness,
                    1.55f));
        }

        private void UpdateFog(
            float daylight,
            float twilight)
        {
            if (!controlFog)
                return;

            RenderSettings.fog = true;
            RenderSettings.fogMode =
                FogMode.Linear;

            float environmentDay =
                Mathf.Clamp01(
                    daylight +
                    twilight * 0.2f);

            Color fog =
                Color.Lerp(
                    nightFogColor,
                    dayFogColor,
                    environmentDay);

            fog =
                Color.Lerp(
                    fog,
                    twilightFogColor,
                    twilight * 0.20f);

            RenderSettings.fogColor =
                fog;

            RenderSettings.fogStartDistance =
                Mathf.Lerp(
                    nightFogStart,
                    dayFogStart,
                    environmentDay);

            RenderSettings.fogEndDistance =
                Mathf.Lerp(
                    nightFogEnd,
                    dayFogEnd,
                    environmentDay);
        }

        private float GetDaylight(
            float hour,
            float sunrise,
            float sunset)
        {
            if (hour < sunrise ||
                hour >= sunset)
            {
                return 0f;
            }

            float fullDay =
                Mathf.Min(
                    sunset,
                    sunrise +
                    dawnTransitionGameHours);

            float duskStart =
                Mathf.Max(
                    sunrise,
                    sunset -
                    duskTransitionGameHours);

            if (hour < fullDay)
            {
                return Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        sunrise,
                        fullDay,
                        hour));
            }

            if (hour > duskStart)
            {
                return Mathf.SmoothStep(
                    0f,
                    1f,
                    1f -
                    Mathf.InverseLerp(
                        duskStart,
                        sunset,
                        hour));
            }

            return 1f;
        }

        private float GetTwilight(
            float hour,
            float sunrise,
            float sunset)
        {
            float sunriseDistance =
                CircularHourDistance(
                    hour,
                    sunrise);

            float sunsetDistance =
                CircularHourDistance(
                    hour,
                    sunset);

            float distance =
                Mathf.Min(
                    sunriseDistance,
                    sunsetDistance);

            return 1f -
                Mathf.SmoothStep(
                    0f,
                    twilightGameHours,
                    distance);
        }

        private static float CircularHourDistance(
            float a,
            float b)
        {
            float difference =
                Mathf.Abs(a - b);

            return Mathf.Min(
                difference,
                24f - difference);
        }

        private static float GetSolarAngle(
            float hour,
            float sunrise,
            float sunset)
        {
            if (hour >= sunrise &&
                hour < sunset)
            {
                return Mathf.Lerp(
                    0f,
                    180f,
                    Mathf.InverseLerp(
                        sunrise,
                        sunset,
                        hour));
            }

            float nightHours =
                24f -
                sunset +
                sunrise;

            float afterSunset =
                hour >= sunset
                    ? hour - sunset
                    : 24f -
                      sunset +
                      hour;

            return Mathf.Lerp(
                180f,
                360f,
                afterSunset /
                nightHours);
        }

        private void OnValidate()
        {
            maxSunIntensity =
                Mathf.Max(
                    0f,
                    maxSunIntensity);

            maxMoonIntensity =
                Mathf.Max(
                    0f,
                    maxMoonIntensity);

            dawnTransitionGameHours =
                Mathf.Max(
                    0.05f,
                    dawnTransitionGameHours);

            duskTransitionGameHours =
                Mathf.Max(
                    0.05f,
                    duskTransitionGameHours);

            twilightGameHours =
                Mathf.Max(
                    0.05f,
                    twilightGameHours);

            dayFogEnd =
                Mathf.Max(
                    dayFogStart + 1f,
                    dayFogEnd);

            nightFogEnd =
                Mathf.Max(
                    nightFogStart + 1f,
                    nightFogEnd);
        }
    }
}
