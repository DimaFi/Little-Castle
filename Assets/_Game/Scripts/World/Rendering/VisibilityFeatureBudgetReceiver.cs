using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Optional presentation-only tier receiver for expensive visual features
    /// that are not already handled by VisibilityBudgetTarget itself.
    ///
    /// Do not assign authoritative/gameplay behaviours here. Repeated local
    /// night lights should still use NightLightEmitter/NightLightBudgetManager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisibilityFeatureBudgetReceiver :
        MonoBehaviour,
        IVisibilityBudgetReceiver
    {
        [Header("Visual-only detail objects")]
        [SerializeField]
        private GameObject[] detailedVisuals;

        [SerializeField]
        private VisibilityQualityTier maxTierForDetailedVisuals =
            VisibilityQualityTier.Balanced;

        [Header("Optional local lights")]
        [SerializeField]
        private Light[] localLights;

        [SerializeField]
        private VisibilityQualityTier maxTierForLocalLights =
            VisibilityQualityTier.Balanced;

        [Header("Particles")]
        [SerializeField]
        private ParticleSystem[] particleSystems;

        [SerializeField]
        private VisibilityQualityTier maxTierForParticles =
            VisibilityQualityTier.Balanced;

        [Header("Expensive visual behaviours only")]
        [SerializeField]
        private Behaviour[] visualBehaviours;

        [SerializeField]
        private VisibilityQualityTier maxTierForBehaviours =
            VisibilityQualityTier.Balanced;

        private bool[] authoredDetailedVisualState;
        private bool[] authoredLightState;
        private bool[] authoredParticlePlayingState;
        private bool[] authoredBehaviourState;

        private bool stateCaptured;
        private VisibilityBudgetTarget budgetTarget;

        private void Awake()
        {
            CaptureAuthoredState();
            ResolveTarget();
        }

        private void OnEnable()
        {
            if (!stateCaptured)
                CaptureAuthoredState();

            ResolveTarget();
            SyncFromTarget();
        }

        private void OnDisable()
        {
            RestoreAuthoredState();
        }

        public void OnVisibilityQualityTierChanged(
            VisibilityQualityTier tier,
            bool cameraVisible)
        {
            ApplyTier(tier);
        }

        private void ResolveTarget()
        {
            if (budgetTarget == null)
            {
                budgetTarget =
                    GetComponentInParent<VisibilityBudgetTarget>();
            }
        }

        private void SyncFromTarget()
        {
            VisibilityQualityTier tier =
                budgetTarget != null
                    ? budgetTarget.CurrentTier
                    : VisibilityQualityTier.Full;

            ApplyTier(tier);
        }

        private void CaptureAuthoredState()
        {
            authoredDetailedVisualState =
                new bool[detailedVisuals != null
                    ? detailedVisuals.Length
                    : 0];

            for (int i = 0;
                 i < authoredDetailedVisualState.Length;
                 i++)
            {
                authoredDetailedVisualState[i] =
                    detailedVisuals[i] != null &&
                    detailedVisuals[i].activeSelf;
            }

            authoredLightState =
                new bool[localLights != null
                    ? localLights.Length
                    : 0];

            for (int i = 0;
                 i < authoredLightState.Length;
                 i++)
            {
                authoredLightState[i] =
                    localLights[i] != null &&
                    localLights[i].enabled;
            }

            authoredParticlePlayingState =
                new bool[particleSystems != null
                    ? particleSystems.Length
                    : 0];

            for (int i = 0;
                 i < authoredParticlePlayingState.Length;
                 i++)
            {
                ParticleSystem particleSystem =
                    particleSystems[i];

                if (particleSystem == null)
                    continue;

                authoredParticlePlayingState[i] =
                    particleSystem.isPlaying ||
                    particleSystem.main.playOnAwake;
            }

            authoredBehaviourState =
                new bool[visualBehaviours != null
                    ? visualBehaviours.Length
                    : 0];

            for (int i = 0;
                 i < authoredBehaviourState.Length;
                 i++)
            {
                Behaviour behaviour =
                    visualBehaviours[i];

                authoredBehaviourState[i] =
                    behaviour != null &&
                    behaviour != this &&
                    behaviour.enabled;
            }

            stateCaptured = true;
        }

        private void ApplyTier(
            VisibilityQualityTier tier)
        {
            SetDetailedVisuals(
                IsWithinTier(
                    tier,
                    maxTierForDetailedVisuals));

            SetLights(
                IsWithinTier(
                    tier,
                    maxTierForLocalLights));

            SetParticles(
                IsWithinTier(
                    tier,
                    maxTierForParticles));

            SetBehaviours(
                IsWithinTier(
                    tier,
                    maxTierForBehaviours));
        }

        private void SetDetailedVisuals(
            bool allowed)
        {
            if (detailedVisuals == null)
                return;

            for (int i = 0;
                 i < detailedVisuals.Length;
                 i++)
            {
                GameObject target =
                    detailedVisuals[i];

                if (target == null ||
                    transform.IsChildOf(target.transform))
                {
                    continue;
                }

                bool desired =
                    allowed &&
                    i < authoredDetailedVisualState.Length &&
                    authoredDetailedVisualState[i];

                if (target.activeSelf != desired)
                    target.SetActive(desired);
            }
        }

        private void SetLights(
            bool allowed)
        {
            if (localLights == null)
                return;

            for (int i = 0;
                 i < localLights.Length;
                 i++)
            {
                Light light =
                    localLights[i];

                if (light == null)
                    continue;

                light.enabled =
                    allowed &&
                    i < authoredLightState.Length &&
                    authoredLightState[i];
            }
        }

        private void SetParticles(
            bool allowed)
        {
            if (particleSystems == null)
                return;

            for (int i = 0;
                 i < particleSystems.Length;
                 i++)
            {
                ParticleSystem particleSystem =
                    particleSystems[i];

                if (particleSystem == null)
                    continue;

                bool shouldPlay =
                    allowed &&
                    i < authoredParticlePlayingState.Length &&
                    authoredParticlePlayingState[i] &&
                    particleSystem.gameObject.activeInHierarchy;

                if (shouldPlay)
                {
                    if (!particleSystem.isPlaying)
                        particleSystem.Play(true);
                }
                else if (particleSystem.isPlaying)
                {
                    particleSystem.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void SetBehaviours(
            bool allowed)
        {
            if (visualBehaviours == null)
                return;

            for (int i = 0;
                 i < visualBehaviours.Length;
                 i++)
            {
                Behaviour behaviour =
                    visualBehaviours[i];

                if (behaviour == null ||
                    behaviour == this)
                {
                    continue;
                }

                behaviour.enabled =
                    allowed &&
                    i < authoredBehaviourState.Length &&
                    authoredBehaviourState[i];
            }
        }

        private void RestoreAuthoredState()
        {
            if (!stateCaptured)
                return;

            SetDetailedVisuals(true);
            SetLights(true);
            SetParticles(true);
            SetBehaviours(true);
        }

        private static bool IsWithinTier(
            VisibilityQualityTier current,
            VisibilityQualityTier maximum)
        {
            return (byte)current <=
                   (byte)maximum;
        }
    }
}
