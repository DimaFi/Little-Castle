using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Presentation-only switch between source renderers and one cheap HLOD
    /// proxy root. Source GameObjects stay alive so gameplay/colliders are not
    /// implicitly disabled.
    ///
    /// The proxy root must contain presentation only: no authoritative state,
    /// colliders, interaction scripts or gameplay ownership.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisibilityHlodProxyReceiver :
        MonoBehaviour,
        IVisibilityBudgetReceiver
    {
        [SerializeField]
        private Renderer[] nearRenderers;

        [SerializeField]
        private GameObject farProxyRoot;

        [SerializeField]
        private VisibilityQualityTier proxyStartsAtTier =
            VisibilityQualityTier.Far;

        [SerializeField]
        private bool hideInHiddenTier = true;

        private bool[] authoredNearRendererState;
        private bool stateCaptured;
        private VisibilityBudgetTarget budgetTarget;

        public HlodPresentationMode CurrentMode
        {
            get;
            private set;
        } = HlodPresentationMode.Near;

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
            RestoreNearRenderers();

            if (farProxyRoot != null &&
                !transform.IsChildOf(farProxyRoot.transform))
            {
                farProxyRoot.SetActive(false);
            }
        }

        public void Configure(
            Renderer[] sourceRenderers,
            GameObject proxyRoot,
            VisibilityQualityTier proxyTier =
                VisibilityQualityTier.Far)
        {
            if (stateCaptured)
                RestoreNearRenderers();

            SetProxy(false);

            nearRenderers =
                sourceRenderers;

            farProxyRoot =
                proxyRoot;

            proxyStartsAtTier =
                proxyTier;

            CaptureAuthoredState();
            SyncFromTarget();
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
            authoredNearRendererState =
                new bool[nearRenderers != null
                    ? nearRenderers.Length
                    : 0];

            for (int i = 0;
                 i < authoredNearRendererState.Length;
                 i++)
            {
                authoredNearRendererState[i] =
                    nearRenderers[i] != null &&
                    nearRenderers[i].enabled;
            }

            stateCaptured = true;
        }

        private void ApplyTier(
            VisibilityQualityTier tier)
        {
            CurrentMode =
                VisibilityHlodProxyPolicy.Evaluate(
                    tier,
                    proxyStartsAtTier,
                    hideInHiddenTier);

            switch (CurrentMode)
            {
                case HlodPresentationMode.Near:
                    SetNearRenderers(true);
                    SetProxy(false);
                    break;

                case HlodPresentationMode.Proxy:
                    SetNearRenderers(false);
                    SetProxy(true);
                    break;

                default:
                    SetNearRenderers(false);
                    SetProxy(false);
                    break;
            }
        }

        private void SetNearRenderers(
            bool allowed)
        {
            if (nearRenderers == null)
                return;

            for (int i = 0;
                 i < nearRenderers.Length;
                 i++)
            {
                Renderer renderer =
                    nearRenderers[i];

                if (renderer == null)
                    continue;

                renderer.enabled =
                    allowed &&
                    i < authoredNearRendererState.Length &&
                    authoredNearRendererState[i];
            }
        }

        private void RestoreNearRenderers()
        {
            if (!stateCaptured)
                return;

            SetNearRenderers(true);
        }

        private void SetProxy(
            bool enabled)
        {
            if (farProxyRoot == null ||
                transform.IsChildOf(farProxyRoot.transform))
            {
                return;
            }

            if (farProxyRoot.activeSelf != enabled)
                farProxyRoot.SetActive(enabled);
        }

        private void OnValidate()
        {
            if (farProxyRoot != null &&
                transform.IsChildOf(farProxyRoot.transform))
            {
                Debug.LogWarning(
                    "HLOD proxy root must be a child presentation object, " +
                    "not the receiver GameObject itself.",
                    this);
            }
        }
    }
}
