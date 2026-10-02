using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Marks one local Point/Spot light as budget-managed atmosphere lighting.
    ///
    /// The visible emissive material is intentionally independent from this
    /// component. Only the expensive realtime light contribution is selected
    /// by NightLightBudgetManager.
    ///
    /// Fade/flicker presentation is ticked only while the light is active or
    /// fading, so thousands of dormant lanterns do not own Update calls.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NightLightEmitter : MonoBehaviour
    {
        private const float StrengthEpsilon = 0.001f;

        private static readonly HashSet<NightLightEmitter> Registry =
            new HashSet<NightLightEmitter>();

        private static readonly HashSet<NightLightEmitter>
            PresentationRegistry =
                new HashSet<NightLightEmitter>();

        [Header("Realtime light")]
        [SerializeField]
        private Light targetLight;

        [SerializeField]
        [Min(0f)]
        private float baseIntensity = 1.5f;

        [SerializeField]
        [Range(0f, 1f)]
        private float minimumNightAmount = 0.18f;

        [Tooltip(
            "Maximum camera distance at which this emitter may consume one " +
            "realtime-light budget slot.")]
        [SerializeField]
        [Min(1f)]
        private float maxDistance = 70f;

        [SerializeField]
        private int priority = 0;

        [SerializeField]
        private bool allowRealtimeShadows = false;

        [Header("Soft transition")]
        [SerializeField]
        [Min(0.01f)]
        private float fadeInSeconds = 0.22f;

        [SerializeField]
        [Min(0.01f)]
        private float fadeOutSeconds = 0.35f;

        [Header("Subtle light flicker")]
        [SerializeField]
        [Range(0f, 0.25f)]
        private float realtimeFlickerStrength = 0.045f;

        [SerializeField]
        [Min(0f)]
        private float realtimeFlickerSpeed = 3.4f;

        [Header("Cheap ground glow")]
        [SerializeField]
        private NightLightPoolVisual poolVisual;

        [Tooltip(
            "Cheap additive ground glow may remain visible farther away than " +
            "the expensive Point/Spot light.")]
        [SerializeField]
        [Min(1f)]
        private float poolMaxDistance = 115f;

        private bool budgetActive;
        private float targetStrength;
        private float currentStrength;
        private float flickerPhase;

        public static IEnumerable<NightLightEmitter> ActiveEmitters =>
            Registry;

        internal static IEnumerable<NightLightEmitter>
            ActivePresentationEmitters =>
                PresentationRegistry;

        public Light TargetLight =>
            targetLight;

        public NightLightPoolVisual PoolVisual =>
            poolVisual;

        public float BaseIntensity =>
            baseIntensity;

        public float MinimumNightAmount =>
            minimumNightAmount;

        public float MaxDistance =>
            maxDistance;

        public float PoolMaxDistance =>
            poolMaxDistance;

        public int Priority =>
            priority;

        public Vector3 WorldPosition =>
            targetLight != null
                ? targetLight.transform.position
                : transform.position;

        public bool BudgetActive =>
            budgetActive;

        public float CurrentStrength =>
            currentStrength;

        public bool PoolVisible =>
            poolVisual != null &&
            poolVisual.IsPresentationVisible;

        private void Awake()
        {
            ResolveLight();
            ResolvePoolVisual();
            CalculateFlickerPhase();
            ApplyStaticLightRules();
            ForceOffImmediate();
        }

        private void OnEnable()
        {
            Registry.Add(this);

            ResolveLight();
            ResolvePoolVisual();
            CalculateFlickerPhase();
            ApplyStaticLightRules();
        }

        private void OnDisable()
        {
            Registry.Remove(this);
            PresentationRegistry.Remove(this);

            ForceOffImmediate();

            if (poolVisual != null)
            {
                poolVisual.SetPresentationVisible(
                    false);
            }
        }

        private void OnDestroy()
        {
            Registry.Remove(this);
            PresentationRegistry.Remove(this);
        }

        /// <summary>
        /// Sets desired realtime-light participation.
        ///
        /// Actual intensity transitions smoothly in TickPresentation.
        /// </summary>
        public void SetBudgetActive(
            bool active,
            float nightAmount)
        {
            ResolveLight();

            if (targetLight == null)
            {
                budgetActive = false;
                targetStrength = 0f;
                return;
            }

            float nightStrength =
                Mathf.InverseLerp(
                    minimumNightAmount,
                    1f,
                    nightAmount);

            targetStrength =
                active
                    ? Mathf.SmoothStep(
                        0f,
                        1f,
                        nightStrength)
                    : 0f;

            budgetActive =
                active &&
                targetStrength >
                StrengthEpsilon;

            if (budgetActive ||
                currentStrength >
                StrengthEpsilon)
            {
                PresentationRegistry.Add(
                    this);
            }
            else
            {
                targetLight.enabled = false;
            }
        }

        /// <summary>
        /// Cheap visual LOD for the additive ground pool.
        ///
        /// The shader itself handles smooth day/night visibility. This only
        /// prevents distant invisible quads from remaining render candidates.
        /// </summary>
        public void SetDistancePresentation(
            float distance,
            float nightAmount)
        {
            ResolvePoolVisual();

            if (poolVisual == null)
                return;

            bool visible =
                nightAmount >
                    minimumNightAmount * 0.35f &&
                distance <=
                    poolMaxDistance;

            poolVisual.SetPresentationVisible(
                visible);
        }

        /// <summary>
        /// Called by NightLightBudgetManager only for lights that are active or
        /// currently fading.
        /// </summary>
        public void TickPresentation(
            float unscaledDeltaTime)
        {
            ResolveLight();

            if (targetLight == null)
            {
                currentStrength = 0f;
                targetStrength = 0f;
                budgetActive = false;
                PresentationRegistry.Remove(this);
                return;
            }

            float duration =
                targetStrength >
                currentStrength
                    ? fadeInSeconds
                    : fadeOutSeconds;

            float maxDelta =
                unscaledDeltaTime /
                Mathf.Max(
                    0.01f,
                    duration);

            currentStrength =
                Mathf.MoveTowards(
                    currentStrength,
                    targetStrength,
                    maxDelta);

            bool shouldRender =
                currentStrength >
                    StrengthEpsilon ||
                targetStrength >
                    StrengthEpsilon;

            if (!shouldRender)
            {
                currentStrength = 0f;
                targetLight.intensity = 0f;
                targetLight.enabled = false;
                PresentationRegistry.Remove(this);
                return;
            }

            if (!targetLight.enabled)
                targetLight.enabled = true;

            float smoothStrength =
                currentStrength *
                currentStrength *
                (3f -
                 2f * currentStrength);

            float flicker =
                EvaluateRealtimeFlicker();

            targetLight.intensity =
                baseIntensity *
                smoothStrength *
                flicker;
        }

        public void ForceOffImmediate()
        {
            budgetActive = false;
            targetStrength = 0f;
            currentStrength = 0f;

            if (targetLight != null)
            {
                targetLight.intensity = 0f;
                targetLight.enabled = false;
            }

            PresentationRegistry.Remove(this);
        }

        private float EvaluateRealtimeFlicker()
        {
            if (realtimeFlickerStrength <= 0f ||
                realtimeFlickerSpeed <= 0f)
            {
                return 1f;
            }

            float time =
                Time.unscaledTime *
                realtimeFlickerSpeed +
                flickerPhase;

            // Two low-cost sine waves avoid the mechanical single-sine look.
            float wave =
                Mathf.Sin(time) *
                    0.68f +
                Mathf.Sin(
                    time * 1.73f +
                    1.91f) *
                    0.32f;

            return
                Mathf.Max(
                    0f,
                    1f +
                    wave *
                    realtimeFlickerStrength);
        }

        private void ResolveLight()
        {
            if (targetLight == null)
            {
                targetLight =
                    GetComponent<Light>();
            }
        }

        private void ResolvePoolVisual()
        {
            if (poolVisual == null)
            {
                poolVisual =
                    GetComponentInChildren<
                        NightLightPoolVisual>(true);
            }
        }

        private void CalculateFlickerPhase()
        {
            Vector3 p =
                transform.position;

            float value =
                p.x * 0.731f +
                p.y * 1.173f +
                p.z * 1.937f;

            flickerPhase =
                Mathf.Repeat(
                    value,
                    Mathf.PI * 2f);
        }

        private void ApplyStaticLightRules()
        {
            if (targetLight == null)
                return;

            if (!allowRealtimeShadows)
            {
                targetLight.shadows =
                    LightShadows.None;
            }

            // Local atmospheric lights are always realtime presentation.
            targetLight.lightmapBakeType =
                LightmapBakeType.Realtime;
        }

        private void OnValidate()
        {
            baseIntensity =
                Mathf.Max(
                    0f,
                    baseIntensity);

            minimumNightAmount =
                Mathf.Clamp01(
                    minimumNightAmount);

            maxDistance =
                Mathf.Max(
                    1f,
                    maxDistance);

            poolMaxDistance =
                Mathf.Max(
                    maxDistance,
                    poolMaxDistance);

            fadeInSeconds =
                Mathf.Max(
                    0.01f,
                    fadeInSeconds);

            fadeOutSeconds =
                Mathf.Max(
                    0.01f,
                    fadeOutSeconds);

            realtimeFlickerStrength =
                Mathf.Clamp(
                    realtimeFlickerStrength,
                    0f,
                    0.25f);

            realtimeFlickerSpeed =
                Mathf.Max(
                    0f,
                    realtimeFlickerSpeed);

            ResolveLight();
            ResolvePoolVisual();
            ApplyStaticLightRules();
        }
    }
}
