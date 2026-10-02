using System;
using UnityEngine;

namespace LittleCastle.Building
{
    /// <summary>
    /// Input-agnostic placement session for curved walls.
    ///
    /// UI/mouse/build-mode code feeds world points into this controller.
    /// It deliberately does not own global input bindings.
    /// </summary>
    public sealed class WallPlacementController : MonoBehaviour
    {
        [SerializeField]
        private WallPathPresenter previewPresenter;

        private WallPlacementDefinition activeDefinition;
        private WallRuntimeState activeWall;

        private bool hasCursorPoint;
        private Vector3 cursorPoint;

        public bool IsPlacing =>
            activeDefinition != null &&
            activeWall != null;

        public WallPlacementDefinition ActiveDefinition =>
            activeDefinition;

        public WallRuntimeState ActiveWall =>
            activeWall;

        public bool PreviewIsValid =>
            previewPresenter != null &&
            previewPresenter.LastIsValid;

        public string PreviewValidationMessage =>
            previewPresenter != null
                ? previewPresenter.LastValidationMessage
                : string.Empty;

        public event Action<WallRuntimeState> PlacementConfirmed;
        public event Action PlacementCancelled;

        public bool BeginPlacement(
            WallPlacementDefinition definition,
            long wallId,
            int ownerPlayerId,
            bool closedLoop = false)
        {
            if (definition == null ||
                wallId == 0)
            {
                return false;
            }

            activeDefinition =
                definition;

            activeWall =
                new WallRuntimeState
                {
                    wallId = wallId,
                    ownerPlayerId = ownerPlayerId,
                    definitionId =
                        definition.DefinitionId,
                    closedLoop =
                        closedLoop
                };

            hasCursorPoint = false;

            RefreshPreview();

            return true;
        }

        public bool AddControlPoint(
            Vector3 worldPoint)
        {
            if (!IsPlacing)
                return false;

            activeWall.AddControlPoint(
                worldPoint,
                WallControlPointMode.Smooth);

            hasCursorPoint = false;

            RefreshPreview();

            return true;
        }

        public bool AddControlPointAtSocket(
            WallConnectionSocket socket)
        {
            if (!IsPlacing ||
                socket == null)
            {
                return false;
            }

            int pointIndex =
                activeWall.controlPoints.Count;

            activeWall.AddControlPoint(
                socket.ConnectionPoint,
                WallControlPointMode.Smooth);

            hasCursorPoint = false;

            if (socket.TryCreateAttachment(
                    pointIndex,
                    out WallSocketAttachmentState attachment))
            {
                activeWall.socketAttachments.Add(
                    attachment);
            }

            RefreshPreview();

            return true;
        }

        public bool RemoveLastControlPoint()
        {
            if (!IsPlacing ||
                activeWall.controlPoints.Count == 0)
            {
                return false;
            }

            int removedIndex =
                activeWall.controlPoints.Count - 1;

            activeWall.controlPoints.RemoveAt(
                removedIndex);

            if (removedIndex <
                activeWall.controlPointModes.Count)
            {
                activeWall.controlPointModes.RemoveAt(
                    removedIndex);
            }

            for (int i =
                     activeWall.socketAttachments.Count - 1;
                 i >= 0;
                 i--)
            {
                if (activeWall.socketAttachments[i]
                        .controlPointIndex >=
                    removedIndex)
                {
                    activeWall.socketAttachments.RemoveAt(
                        i);
                }
            }

            RefreshPreview();

            return true;
        }

        public bool ToggleLastControlPointMode()
        {
            if (!IsPlacing ||
                activeWall.controlPoints.Count == 0)
            {
                return false;
            }

            bool changed =
                activeWall.ToggleControlPointMode(
                    activeWall.controlPoints.Count - 1);

            if (changed)
                RefreshPreview();

            return changed;
        }

        public WallControlPointMode GetLastControlPointMode()
        {
            if (!IsPlacing ||
                activeWall.controlPoints.Count == 0)
            {
                return WallControlPointMode.Smooth;
            }

            return
                activeWall.GetControlPointMode(
                    activeWall.controlPoints.Count - 1);
        }

        public bool AttachLastControlPointToSocket(
            long structureId,
            string socketId,
            float socketYawDegrees,
            float clearanceRadius)
        {
            if (!IsPlacing ||
                activeWall.controlPoints.Count == 0 ||
                structureId == 0 ||
                string.IsNullOrWhiteSpace(
                    socketId))
            {
                return false;
            }

            int pointIndex =
                activeWall.controlPoints.Count - 1;

            for (int i =
                     activeWall.socketAttachments.Count - 1;
                 i >= 0;
                 i--)
            {
                if (activeWall.socketAttachments[i]
                        .controlPointIndex ==
                    pointIndex)
                {
                    activeWall.socketAttachments.RemoveAt(
                        i);
                }
            }

            activeWall.socketAttachments.Add(
                new WallSocketAttachmentState(
                    pointIndex,
                    structureId,
                    socketId,
                    socketYawDegrees,
                    clearanceRadius));

            RefreshPreview();

            return true;
        }

        public void SetCursorPoint(
            Vector3 worldPoint)
        {
            if (!IsPlacing)
                return;

            cursorPoint =
                worldPoint;

            hasCursorPoint = true;

            RefreshPreview();
        }

        public void ClearCursorPoint()
        {
            hasCursorPoint = false;
            RefreshPreview();
        }

        public void SetClosedLoop(
            bool closedLoop)
        {
            if (!IsPlacing)
                return;

            activeWall.closedLoop =
                closedLoop;

            RefreshPreview();
        }

        public bool TryConfirm(
            out WallRuntimeState confirmed)
        {
            confirmed = null;

            if (!IsPlacing)
                return false;

            hasCursorPoint = false;
            RefreshPreview();

            if (previewPresenter != null &&
                !previewPresenter.LastIsValid)
            {
                return false;
            }

            var sections =
                new System.Collections.Generic.List<
                    WallSectionPose>();

            WallPathLayoutUtility.BuildSections(
                activeWall,
                activeDefinition,
                sections);

            if (!WallPlacementValidator.Validate(
                    activeWall,
                    activeDefinition,
                    sections,
                    out string ignoredReason))
            {
                return false;
            }

            confirmed =
                activeWall.Clone();

            PlacementConfirmed?.Invoke(
                confirmed);

            EndPlacement(
                false);

            return true;
        }

        public void Cancel()
        {
            if (!IsPlacing)
                return;

            EndPlacement(
                true);
        }

        private void RefreshPreview()
        {
            if (previewPresenter == null)
                return;

            if (!IsPlacing)
            {
                previewPresenter.Clear();
                return;
            }

            WallRuntimeState preview =
                activeWall.Clone();

            if (hasCursorPoint &&
                !preview.closedLoop &&
                preview.controlPoints.Count > 0)
            {
                preview.controlPoints.Add(
                    cursorPoint);
            }

            previewPresenter.Rebuild(
                activeDefinition,
                preview,
                true);
        }

        private void EndPlacement(
            bool cancelled)
        {
            activeDefinition = null;
            activeWall = null;
            hasCursorPoint = false;

            if (previewPresenter != null)
                previewPresenter.Clear();

            if (cancelled)
                PlacementCancelled?.Invoke();
        }
    }
}
