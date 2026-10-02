using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    [DisallowMultipleComponent]
    public sealed class VisibilityBudgetTarget : MonoBehaviour
    {
        [SerializeField]
        private VisibilityBudgetProfile profile;

        [SerializeField]
        private bool forceFullQuality;

        [Min(0.1f)]
        [SerializeField]
        private float fallbackRadius = 1f;

        private Renderer[] renderers;
        private Animator[] animators;
        private LODGroup lodGroup;
        private IVisibilityBudgetReceiver[] receivers;

        private bool[] shadowBudgetApplied;
        private ShadowCastingMode[] shadowModeBeforeBudget;
        private bool[] receiveShadowsBeforeBudget;
        private bool[] animatorDisabledByBudget;

        private Vector3 localSphereCenter;
        private float localSphereRadius = 1f;

        // Managed components begin in their authored/full state. The manager
        // must perform a real downgrade before the logical tier becomes Hidden.
        private VisibilityQualityTier currentTier =
            VisibilityQualityTier.Full;

        private VisibilityQualityTier pendingTier =
            VisibilityQualityTier.Full;

        private float pendingSince;
        private bool hasPendingTier;
        private bool cameraVisible;

        private int registrationIndex = -1;

        private bool cullingStateKnown;
        private bool cullingVisible;
        private float lastCullingVisibleTime =
            float.NegativeInfinity;

        private bool lastApproxInsideView;

        public VisibilityBudgetProfile Profile => profile;
        public VisibilityQualityTier CurrentTier => currentTier;
        public bool CameraVisible => cameraVisible;
        public bool ForceFullQuality => forceFullQuality;

        internal int RegistrationIndex => registrationIndex;
        internal bool CullingStateKnown => cullingStateKnown;
        internal bool CullingVisible => cullingVisible;
        internal float LastCullingVisibleTime => lastCullingVisibleTime;

        internal bool LastApproxInsideView
        {
            get => lastApproxInsideView;
            set => lastApproxInsideView = value;
        }

        private void Awake()
        {
            CacheComponents();
            RebuildBounds();
        }

        private void OnEnable()
        {
            if (renderers == null)
            {
                CacheComponents();
                RebuildBounds();
            }

            VisibilityBudgetManager.RegisterTarget(
                this);
        }

        private void Start()
        {
            if (registrationIndex < 0 &&
                isActiveAndEnabled)
            {
                VisibilityBudgetManager
                    .EnsureInstance();
            }
        }

        private void OnDisable()
        {
            VisibilityBudgetManager.UnregisterTarget(
                this);

            RestoreManagedState();
        }

        public void SetProfile(
            VisibilityBudgetProfile newProfile)
        {
            profile =
                newProfile;

            VisibilityBudgetManager.NotifyTargetChanged(
                this);
        }

        public void SetForceFullQuality(
            bool value)
        {
            forceFullQuality =
                value;
        }

        public void RebuildBounds()
        {
            if (renderers == null)
                CacheComponents();

            bool hasBounds = false;
            Bounds combined =
                new Bounds(
                    transform.position,
                    Vector3.zero);

            if (renderers != null)
            {
                for (int i = 0;
                     i < renderers.Length;
                     i++)
                {
                    Renderer renderer =
                        renderers[i];

                    if (renderer == null)
                        continue;

                    if (!hasBounds)
                    {
                        combined =
                            renderer.bounds;

                        hasBounds = true;
                    }
                    else
                    {
                        combined.Encapsulate(
                            renderer.bounds);
                    }
                }
            }

            Vector3 worldCenter =
                hasBounds
                    ? combined.center
                    : transform.position;

            float worldRadius =
                hasBounds
                    ? combined.extents.magnitude
                    : Mathf.Max(
                        0.1f,
                        fallbackRadius);

            float maxScale =
                GetMaximumScale(
                    transform.lossyScale);

            localSphereCenter =
                transform.InverseTransformPoint(
                    worldCenter);

            localSphereRadius =
                Mathf.Max(
                    0.05f,
                    worldRadius /
                    Mathf.Max(
                        0.0001f,
                        maxScale));

            VisibilityBudgetManager.NotifyTargetChanged(
                this);
        }

        internal VisibilityBudgetSettings ResolveSettings(
            VisibilityBudgetSettings fallback)
        {
            return
                profile != null
                    ? profile.Settings
                    : fallback.Sanitized();
        }

        internal BoundingSphere GetBoundingSphere(
            float padding)
        {
            Vector3 scale =
                transform.lossyScale;

            return new BoundingSphere(
                transform.TransformPoint(
                    localSphereCenter),
                localSphereRadius *
                GetMaximumScale(scale) *
                Mathf.Max(
                    1f,
                    padding));
        }

        internal void SetRegistrationIndex(
            int index)
        {
            registrationIndex =
                index;
        }

        internal void PrepareForBudget(
            VisibilityBudgetSettings settings)
        {
            if (settings.configureLodCrossFade &&
                lodGroup != null &&
                lodGroup.fadeMode !=
                    LODFadeMode.SpeedTree)
            {
                lodGroup.fadeMode =
                    LODFadeMode.CrossFade;

                lodGroup.animateCrossFading =
                    true;
            }
        }

        internal void ResetCullingState()
        {
            cullingStateKnown = false;
            cullingVisible = false;
            lastCullingVisibleTime =
                float.NegativeInfinity;

            lastApproxInsideView = false;
        }

        internal void NotifyCullingVisibility(
            bool visible,
            float now)
        {
            cullingStateKnown = true;
            cullingVisible = visible;

            if (visible)
            {
                lastCullingVisibleTime =
                    now;
            }
        }

        internal void RequestTier(
            VisibilityQualityTier desired,
            float now,
            VisibilityBudgetSettings settings,
            bool isCameraVisible)
        {
            cameraVisible =
                isCameraVisible;

            if (desired ==
                currentTier)
            {
                hasPendingTier = false;
                return;
            }

            if (desired <
                currentTier)
            {
                ApplyTier(
                    desired,
                    settings);

                hasPendingTier = false;
                return;
            }

            float delay =
                desired ==
                    VisibilityQualityTier.Hidden
                    ? settings.hiddenDelaySeconds
                    : settings.downgradeDelaySeconds;

            if (!hasPendingTier ||
                pendingTier != desired)
            {
                pendingTier = desired;
                pendingSince = now;
                hasPendingTier = true;

                if (delay > 0f)
                    return;
            }

            if (now - pendingSince <
                delay)
            {
                return;
            }

            ApplyTier(
                desired,
                settings);

            hasPendingTier = false;
        }

        private void ApplyTier(
            VisibilityQualityTier tier,
            VisibilityBudgetSettings settings)
        {
            currentTier =
                tier;

            bool reducedShadowTier =
                tier ==
                    VisibilityQualityTier.Far ||
                tier ==
                    VisibilityQualityTier.Hidden;

            ApplyShadowBudget(
                reducedShadowTier,
                settings);

            ApplyAnimatorBudget(
                tier ==
                    VisibilityQualityTier.Hidden &&
                settings.disableAnimatorsWhenHidden);

            if (receivers == null)
                return;

            for (int i = 0;
                 i < receivers.Length;
                 i++)
            {
                IVisibilityBudgetReceiver receiver =
                    receivers[i];

                if (receiver == null)
                    continue;

                receiver.OnVisibilityQualityTierChanged(
                    tier,
                    cameraVisible);
            }
        }

        private void CacheComponents()
        {
            renderers =
                GetComponentsInChildren<
                    Renderer>(
                    true);

            animators =
                GetComponentsInChildren<
                    Animator>(
                    true);

            lodGroup =
                GetComponentInChildren<
                    LODGroup>(
                    true);

            shadowBudgetApplied =
                new bool[
                    renderers != null
                        ? renderers.Length
                        : 0];

            shadowModeBeforeBudget =
                new ShadowCastingMode[
                    shadowBudgetApplied.Length];

            receiveShadowsBeforeBudget =
                new bool[
                    shadowBudgetApplied.Length];

            animatorDisabledByBudget =
                new bool[
                    animators != null
                        ? animators.Length
                        : 0];

            MonoBehaviour[] behaviours =
                GetComponentsInChildren<
                    MonoBehaviour>(
                    true);

            var foundReceivers =
                new List<
                    IVisibilityBudgetReceiver>(
                    behaviours.Length);

            for (int i = 0;
                 i < behaviours.Length;
                 i++)
            {
                if (behaviours[i] is
                    IVisibilityBudgetReceiver receiver)
                {
                    foundReceivers.Add(
                        receiver);
                }
            }

            receivers =
                foundReceivers.ToArray();
        }

        private void ApplyShadowBudget(
            bool reducedTier,
            VisibilityBudgetSettings settings)
        {
            bool shouldReduce =
                reducedTier &&
                (settings.disableFarShadows ||
                 settings.disableFarReceiveShadows);

            if (renderers == null)
                return;

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null)
                    continue;

                if (!shouldReduce)
                {
                    if (!shadowBudgetApplied[i])
                        continue;

                    renderer.shadowCastingMode =
                        shadowModeBeforeBudget[i];

                    renderer.receiveShadows =
                        receiveShadowsBeforeBudget[i];

                    shadowBudgetApplied[i] =
                        false;

                    continue;
                }

                if (!shadowBudgetApplied[i])
                {
                    shadowModeBeforeBudget[i] =
                        renderer.shadowCastingMode;

                    receiveShadowsBeforeBudget[i] =
                        renderer.receiveShadows;

                    shadowBudgetApplied[i] =
                        true;
                }

                if (settings.disableFarShadows)
                {
                    renderer.shadowCastingMode =
                        ShadowCastingMode.Off;
                }

                if (settings.disableFarReceiveShadows)
                {
                    renderer.receiveShadows =
                        false;
                }
            }
        }

        private void ApplyAnimatorBudget(
            bool disable)
        {
            if (animators == null)
                return;

            for (int i = 0;
                 i < animators.Length;
                 i++)
            {
                Animator animator =
                    animators[i];

                if (animator == null)
                    continue;

                if (disable)
                {
                    if (!animatorDisabledByBudget[i] &&
                        animator.enabled)
                    {
                        animator.enabled = false;
                        animatorDisabledByBudget[i] = true;
                    }

                    continue;
                }

                if (!animatorDisabledByBudget[i])
                    continue;

                animator.enabled = true;
                animatorDisabledByBudget[i] = false;
            }
        }

        private void RestoreManagedState()
        {
            ApplyShadowBudget(
                false,
                VisibilityBudgetSettings.Default);

            ApplyAnimatorBudget(
                false);

            hasPendingTier = false;
            currentTier =
                VisibilityQualityTier.Full;
            pendingTier =
                VisibilityQualityTier.Full;
        }

        private static float GetMaximumScale(
            Vector3 scale)
        {
            return Mathf.Max(
                Mathf.Abs(
                    scale.x),
                Mathf.Max(
                    Mathf.Abs(
                        scale.y),
                    Mathf.Abs(
                        scale.z)));
        }
    }
}
