using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Marks a local realtime light as budget-managed atmosphere lighting.
    ///
    /// Emissive materials should carry most of the visual glow. Only a limited
    /// number of nearby emitters are allowed to enable realtime Lights.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NightLightEmitter : MonoBehaviour
    {
        private static readonly HashSet<NightLightEmitter> Registry =
            new HashSet<NightLightEmitter>();

        [SerializeField]
        private Light targetLight;

        [SerializeField]
        [Min(0f)]
        private float baseIntensity = 1.5f;

        [SerializeField]
        [Range(0f, 1f)]
        private float minimumNightAmount = 0.18f;

        [SerializeField]
        [Min(1f)]
        private float maxDistance = 80f;

        [SerializeField]
        private int priority = 0;

        [SerializeField]
        private bool allowRealtimeShadows = false;

        private bool budgetActive;

        public static IEnumerable<NightLightEmitter> ActiveEmitters =>
            Registry;

        public Light TargetLight =>
            targetLight;

        public float BaseIntensity =>
            baseIntensity;

        public float MinimumNightAmount =>
            minimumNightAmount;

        public float MaxDistance =>
            maxDistance;

        public int Priority =>
            priority;

        public Vector3 WorldPosition =>
            targetLight != null
                ? targetLight.transform.position
                : transform.position;

        public bool BudgetActive =>
            budgetActive;

        private void Awake()
        {
            ResolveLight();
            ApplyStaticLightRules();
            SetBudgetActive(
                false,
                0f);
        }

        private void OnEnable()
        {
            Registry.Add(this);

            ResolveLight();
            ApplyStaticLightRules();
        }

        private void OnDisable()
        {
            Registry.Remove(this);

            SetBudgetActive(
                false,
                0f);
        }

        private void OnDestroy()
        {
            Registry.Remove(this);
        }

        public void SetBudgetActive(
            bool active,
            float nightAmount)
        {
            ResolveLight();

            if (targetLight == null)
            {
                budgetActive = false;
                return;
            }

            float strength =
                Mathf.InverseLerp(
                    minimumNightAmount,
                    1f,
                    nightAmount);

            bool shouldEnable =
                active &&
                strength > 0f;

            if (targetLight.enabled !=
                shouldEnable)
            {
                targetLight.enabled =
                    shouldEnable;
            }

            if (shouldEnable)
            {
                targetLight.intensity =
                    baseIntensity *
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        strength);
            }

            budgetActive =
                shouldEnable;
        }

        private void ResolveLight()
        {
            if (targetLight == null)
            {
                targetLight =
                    GetComponent<Light>();
            }
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

            ResolveLight();
            ApplyStaticLightRules();
        }
    }
}
