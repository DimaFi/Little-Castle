using UnityEngine;

namespace LittleCastle.Concept
{
    /// <summary>
    /// Presentation-only near-camera budget for the immutable bridge collision.
    /// Colliders never represent world authority; physics is local presentation.
    /// This component is intentionally self-contained so the concept importer
    /// can attach it to a generated prefab without changing WorldStreamer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BridgeStoneNearColliderBudget : MonoBehaviour
    {
        [SerializeField, Min(1f)]
        private float nearDistanceMeters = 80f;

        [SerializeField, Min(0.1f)]
        private float refreshIntervalSeconds = 0.35f;

        private MeshCollider[] ownedColliders;
        private float nextRefreshTime;

        private void Awake()
        {
            ownedColliders = GetComponentsInChildren<MeshCollider>(true);
            SetEnabled(false);
        }

        private void OnEnable()
        {
            nextRefreshTime = 0f;
        }

        private void OnDisable()
        {
            SetEnabled(false);
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime +
                Mathf.Max(0.1f, refreshIntervalSeconds);

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                SetEnabled(false);
                return;
            }

            float distanceSquared =
                (mainCamera.transform.position - transform.position).sqrMagnitude;

            ApplyDistanceSquared(distanceSquared);
        }

        /// <summary>
        /// Test-friendly, allocation-free near/far decision.
        /// </summary>
        public void ApplyDistanceSquared(float distanceSquared)
        {
            float threshold = Mathf.Max(1f, nearDistanceMeters);
            SetEnabled(distanceSquared <= threshold * threshold);
        }

        private void SetEnabled(bool enabled)
        {
            if (ownedColliders == null)
                ownedColliders = GetComponentsInChildren<MeshCollider>(true);

            for (int i = 0; i < ownedColliders.Length; i++)
                if (ownedColliders[i] != null)
                    ownedColliders[i].enabled = enabled;
        }
    }
}
