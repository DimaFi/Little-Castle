using System;
using UnityEngine;

namespace LittleCastle.Building
{
    /// <summary>
    /// Thin player-input adapter for WallPlacementController.
    ///
    /// A build-menu button starts this adapter with BeginBuild. The core wall
    /// layout remains input-agnostic and can also be driven by AI/tests/network.
    /// </summary>
    public sealed class WallBuildInputController : MonoBehaviour
    {
        [SerializeField]
        private WallPlacementController placementController;

        [SerializeField]
        private Camera buildCamera;

        [SerializeField]
        private LayerMask groundMask = ~0;

        [SerializeField, Min(1f)]
        private float maximumRayDistance = 5000f;

        [Header("Controls")]
        [SerializeField]
        private KeyCode confirmKey = KeyCode.Return;

        [SerializeField]
        private KeyCode removeLastPointKey = KeyCode.Backspace;

        [SerializeField]
        private KeyCode cancelKey = KeyCode.Escape;

        [SerializeField]
        private KeyCode toggleClosedLoopKey = KeyCode.C;

        [Tooltip(
            "One press toggles the last committed point Smooth <-> Sharp. " +
            "Right Shift is accepted as well.")]
        [SerializeField]
        private KeyCode sharpCornerToggleKey = KeyCode.LeftShift;

        private bool closedLoop;
        private long activeWallId;
        private int activeOwnerPlayerId;

        public bool IsBuilding =>
            placementController != null &&
            placementController.IsPlacing;

        public event Action<WallRuntimeState> WallConfirmed;

        private void Awake()
        {
            if (buildCamera == null)
                buildCamera = Camera.main;

            if (placementController != null)
            {
                placementController.PlacementConfirmed +=
                    OnPlacementConfirmed;
            }
        }

        private void OnDestroy()
        {
            if (placementController != null)
            {
                placementController.PlacementConfirmed -=
                    OnPlacementConfirmed;
            }
        }

        public bool BeginBuild(
            WallPlacementDefinition definition,
            long provisionalWallId,
            int ownerPlayerId,
            bool startClosedLoop = false)
        {
            if (placementController == null ||
                definition == null ||
                provisionalWallId == 0)
            {
                return false;
            }

            activeWallId =
                provisionalWallId;

            activeOwnerPlayerId =
                ownerPlayerId;

            closedLoop =
                startClosedLoop;

            return
                placementController.BeginPlacement(
                    definition,
                    activeWallId,
                    activeOwnerPlayerId,
                    closedLoop);
        }

        public void CancelBuild()
        {
            if (placementController != null)
                placementController.Cancel();
        }

        private void Update()
        {
            if (!IsBuilding)
                return;

            UpdateCursor();

            if (Input.GetMouseButtonDown(0) &&
                TryGetGroundPoint(
                    out Vector3 point))
            {
                placementController.AddControlPoint(
                    point);
            }

            if (Input.GetKeyDown(
                    removeLastPointKey))
            {
                placementController.RemoveLastControlPoint();
            }

            if (Input.GetKeyDown(
                    toggleClosedLoopKey))
            {
                closedLoop =
                    !closedLoop;

                placementController.SetClosedLoop(
                    closedLoop);
            }

            if (Input.GetKeyDown(
                    sharpCornerToggleKey) ||
                Input.GetKeyDown(
                    KeyCode.RightShift))
            {
                placementController.ToggleLastControlPointMode();
            }

            if (Input.GetKeyDown(
                    confirmKey))
            {
                placementController.TryConfirm(
                    out WallRuntimeState ignored);
            }

            if (Input.GetKeyDown(
                    cancelKey))
            {
                placementController.Cancel();
            }
        }

        private void UpdateCursor()
        {
            if (TryGetGroundPoint(
                    out Vector3 point))
            {
                placementController.SetCursorPoint(
                    point);
            }
            else
            {
                placementController.ClearCursorPoint();
            }
        }

        private bool TryGetGroundPoint(
            out Vector3 point)
        {
            point = default;

            if (buildCamera == null)
            {
                buildCamera = Camera.main;

                if (buildCamera == null)
                    return false;
            }

            Ray ray =
                buildCamera.ScreenPointToRay(
                    Input.mousePosition);

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    maximumRayDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            point =
                hit.point;

            return true;
        }

        private void OnPlacementConfirmed(
            WallRuntimeState wall)
        {
            WallConfirmed?.Invoke(
                wall);
        }

        private void OnValidate()
        {
            maximumRayDistance =
                Mathf.Max(
                    1f,
                    maximumRayDistance);
        }
    }
}
