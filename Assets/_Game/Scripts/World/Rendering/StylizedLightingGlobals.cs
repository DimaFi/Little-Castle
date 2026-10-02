using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Bridges the authoritative world atmosphere into one shared shader
    /// contract used by every Little Castle stylized material.
    ///
    /// Keep presentation parameters here. Gameplay systems must never depend
    /// on these shader globals.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class StylizedLightingGlobals : MonoBehaviour
    {
        private static readonly int SunDirectionId =
            Shader.PropertyToID("_LC_SunDirection");

        private static readonly int SunColorId =
            Shader.PropertyToID("_LC_SunColor");

        private static readonly int MoonDirectionId =
            Shader.PropertyToID("_LC_MoonDirection");

        private static readonly int MoonColorId =
            Shader.PropertyToID("_LC_MoonColor");

        private static readonly int AmbientColorId =
            Shader.PropertyToID("_LC_AmbientColor");

        private static readonly int AmbientSkyColorId =
            Shader.PropertyToID("_LC_AmbientSkyColor");

        private static readonly int AmbientEquatorColorId =
            Shader.PropertyToID("_LC_AmbientEquatorColor");

        private static readonly int AmbientGroundColorId =
            Shader.PropertyToID("_LC_AmbientGroundColor");

        private static readonly int ShadowTintId =
            Shader.PropertyToID("_LC_ShadowTint");

        private static readonly int DaylightId =
            Shader.PropertyToID("_LC_Daylight");

        private static readonly int TwilightId =
            Shader.PropertyToID("_LC_Twilight");

        private static readonly int NightAmountId =
            Shader.PropertyToID("_LC_NightAmount");

        private static readonly int WindId =
            Shader.PropertyToID("_LC_Wind");

        private static readonly int GameTimeId =
            Shader.PropertyToID("_LC_GameTime");

        [Header("Sources")]
        [SerializeField]
        private LittleCastle.World.WorldTimeSystem timeSystem;

        [SerializeField]
        private LittleCastle.World.DayNightLightingController atmosphere;

        [SerializeField]
        private Light sun;

        [SerializeField]
        private Light moon;

        [Header("Shared palette")]
        [SerializeField]
        private Color dayShadowTint =
            new Color(
                0.72f,
                0.82f,
                1f,
                1f);

        [SerializeField]
        private Color twilightShadowTint =
            new Color(
                0.48f,
                0.56f,
                0.78f,
                1f);

        [SerializeField]
        private Color nightShadowTint =
            new Color(
                0.34f,
                0.44f,
                0.68f,
                1f);

        [SerializeField]
        [Min(0f)]
        private float ambientMultiplier = 1f;

        [SerializeField]
        [Min(0f)]
        private float sunMultiplier = 1f;

        [SerializeField]
        [Min(0f)]
        private float moonMultiplier = 1f;

        [Header("Shared wind")]
        [SerializeField]
        private Vector2 windDirection =
            new Vector2(
                0.85f,
                0.3f);

        [SerializeField]
        [Range(0f, 2f)]
        private float windStrength = 0.35f;

        [SerializeField]
        [Min(0f)]
        private float windSpeed = 1f;

        public float Daylight { get; private set; }
        public float Twilight { get; private set; }
        public float NightAmount { get; private set; }

        public Vector3 SunDirection { get; private set; }
        public Vector3 MoonDirection { get; private set; }

        public Color CurrentSunColor { get; private set; }
        public Color CurrentMoonColor { get; private set; }
        public Color CurrentAmbientColor { get; private set; }
        public Color CurrentAmbientSkyColor { get; private set; }
        public Color CurrentAmbientEquatorColor { get; private set; }
        public Color CurrentAmbientGroundColor { get; private set; }
        public Color CurrentShadowTint { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            ApplyGlobals();
        }

        private void LateUpdate()
        {
            ApplyGlobals();
        }

        [ContextMenu("Apply Stylized Shader Globals")]
        public void ApplyGlobals()
        {
            ResolveReferences();

            Daylight =
                atmosphere != null
                    ? atmosphere.DaylightFactor
                    : timeSystem != null &&
                      timeSystem.IsDay
                        ? 1f
                        : 0f;

            Twilight =
                atmosphere != null
                    ? atmosphere.TwilightFactor
                    : 0f;

            NightAmount =
                Mathf.Clamp01(
                    1f -
                    Daylight -
                    Twilight * 0.35f);

            SunDirection =
                sun != null
                    ? -sun.transform.forward
                    : Vector3.up;

            MoonDirection =
                moon != null
                    ? -moon.transform.forward
                    : -SunDirection;

            CurrentSunColor =
                sun != null
                    ? sun.color *
                      sun.intensity *
                      sunMultiplier
                    : Color.black;

            CurrentMoonColor =
                moon != null
                    ? moon.color *
                      moon.intensity *
                      moonMultiplier
                    : Color.black;

            CurrentAmbientSkyColor =
                RenderSettings.ambientSkyColor *
                ambientMultiplier;

            CurrentAmbientEquatorColor =
                RenderSettings.ambientEquatorColor *
                ambientMultiplier;

            CurrentAmbientGroundColor =
                RenderSettings.ambientGroundColor *
                ambientMultiplier;

            CurrentAmbientColor =
                EvaluateAmbientColor();

            CurrentShadowTint =
                EvaluateShadowTint(
                    Daylight,
                    Twilight);

            Shader.SetGlobalVector(
                SunDirectionId,
                new Vector4(
                    SunDirection.x,
                    SunDirection.y,
                    SunDirection.z,
                    0f));

            Shader.SetGlobalColor(
                SunColorId,
                CurrentSunColor);

            Shader.SetGlobalVector(
                MoonDirectionId,
                new Vector4(
                    MoonDirection.x,
                    MoonDirection.y,
                    MoonDirection.z,
                    0f));

            Shader.SetGlobalColor(
                MoonColorId,
                CurrentMoonColor);

            Shader.SetGlobalColor(
                AmbientColorId,
                CurrentAmbientColor);

            Shader.SetGlobalColor(
                AmbientSkyColorId,
                CurrentAmbientSkyColor);

            Shader.SetGlobalColor(
                AmbientEquatorColorId,
                CurrentAmbientEquatorColor);

            Shader.SetGlobalColor(
                AmbientGroundColorId,
                CurrentAmbientGroundColor);

            Shader.SetGlobalColor(
                ShadowTintId,
                CurrentShadowTint);

            Shader.SetGlobalFloat(
                DaylightId,
                Daylight);

            Shader.SetGlobalFloat(
                TwilightId,
                Twilight);

            Shader.SetGlobalFloat(
                NightAmountId,
                NightAmount);

            Vector2 normalizedWind =
                windDirection.sqrMagnitude >
                0.0001f
                    ? windDirection.normalized
                    : Vector2.right;

            Shader.SetGlobalVector(
                WindId,
                new Vector4(
                    normalizedWind.x,
                    normalizedWind.y,
                    windStrength,
                    windSpeed));

            float hour =
                timeSystem != null
                    ? timeSystem.CurrentHour
                    : 12f;

            float day =
                timeSystem != null
                    ? timeSystem.CurrentDay
                    : 1f;

            Shader.SetGlobalVector(
                GameTimeId,
                new Vector4(
                    hour / 24f,
                    day,
                    Time.unscaledTime,
                    0f));
        }

        private void ResolveReferences()
        {
            if (atmosphere == null)
            {
                atmosphere =
                    FindFirstObjectByType<
                        LittleCastle.World.DayNightLightingController>();
            }

            if (timeSystem == null)
            {
                timeSystem =
                    atmosphere != null
                        ? atmosphere.TimeSystem
                        : FindFirstObjectByType<
                            LittleCastle.World.WorldTimeSystem>();
            }

            if (sun == null)
            {
                sun =
                    atmosphere != null
                        ? atmosphere.Sun
                        : RenderSettings.sun;
            }

            if (moon == null &&
                atmosphere != null)
            {
                moon =
                    atmosphere.Moon;
            }
        }

        private Color EvaluateAmbientColor()
        {
            return
                CurrentAmbientSkyColor *
                0.50f +
                CurrentAmbientEquatorColor *
                0.35f +
                CurrentAmbientGroundColor *
                0.15f;
        }

        private Color EvaluateShadowTint(
            float daylight,
            float twilight)
        {
            Color tint =
                Color.Lerp(
                    nightShadowTint,
                    dayShadowTint,
                    daylight);

            return Color.Lerp(
                tint,
                twilightShadowTint,
                twilight * 0.75f);
        }

        private void OnValidate()
        {
            ambientMultiplier =
                Mathf.Max(
                    0f,
                    ambientMultiplier);

            sunMultiplier =
                Mathf.Max(
                    0f,
                    sunMultiplier);

            moonMultiplier =
                Mathf.Max(
                    0f,
                    moonMultiplier);

            windStrength =
                Mathf.Clamp(
                    windStrength,
                    0f,
                    2f);

            windSpeed =
                Mathf.Max(
                    0f,
                    windSpeed);
        }
    }
}
