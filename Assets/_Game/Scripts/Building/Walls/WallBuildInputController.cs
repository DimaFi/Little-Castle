using System;
using LittleCastle.World;
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

        [SerializeField]
        private bool enableStructureSocketSnapping = true;

        [Tooltip(
            "Physics layers containing tower/gate colliders with " +
            "WallConnectionSocket children.")]
        [SerializeField]
        private LayerMask structureSocketMask = ~0;

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
                TryGetBuildPoint(
                    out Vector3 point,
                    out WallConnectionSocket socket))
            {
                if (socket != null)
                {
                    placementController.AddControlPointAtSocket(
                        socket);
                }
                else
                {
                    placementController.AddControlPoint(
                        point);
                }
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
            if (TryGetBuildPoint(
                    out Vector3 point,
                    out WallConnectionSocket ignoredSocket))
            {
                placementController.SetCursorPoint(
                    point);
            }
            else
            {
                placementController.ClearCursorPoint();
            }
        }

        private bool TryGetBuildPoint(
            out Vector3 point,
            out WallConnectionSocket socket)
        {
            socket = null;

            if (!TryGetGroundPoint(
                    out point))
            {
                return false;
            }

            if (!enableStructureSocketSnapping ||
                placementController == null ||
                placementController.ActiveDefinition == null)
            {
                return true;
            }

            float snapDistance =
                placementController.ActiveDefinition
                    .StructureSocketSnapDistance;

            if (WallSocketSnapUtility.TryFindNearestSocket(
                    point,
                    snapDistance,
                    structureSocketMask,
                    out socket))
            {
                point =
                    socket.ConnectionPoint;
            }

            return true;
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

            RaycastHit[] hits =
                Physics.RaycastAll(
                    ray,
                    maximumRayDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore);

            if (hits == null ||
                hits.Length == 0)
            {
                return false;
            }

            Array.Sort(
                hits,
                (a, b) =>
                    a.distance.CompareTo(
                        b.distance));

            // The build cursor belongs on generated terrain, not on the top
            // of a nearby tree/house collider. Structure snapping is handled
            // separately through WallConnectionSocket.
            for (int i = 0; i < hits.Length; i++)
            {
                StreamedChunkView chunk =
                    hits[i].collider != null
                        ? hits[i].collider.GetComponentInParent<
                            StreamedChunkView>()
                        : null;

                if (chunk == null ||
                    !chunk.IsPlayableChunk)
                {
                    continue;
                }

                point =
                    hits[i].point;

                return true;
            }

            // Editor previews or future terrain implementations may not use
            // StreamedChunkView yet. Keep the nearest-hit fallback rather than
            // making the input adapter unusable.
            point =
                hits[0].point;

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
