using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Building
{
    public enum WallControlPointMode : byte
    {
        Smooth = 0,
        Sharp = 1
    }

    /// <summary>
    /// Optional reference from one wall control point to a separately owned
    /// defensive structure socket (for example an archer tower).
    ///
    /// The snapped world position remains in controlPoints so the wall can be
    /// reproduced even before the referenced structure GameObject is loaded.
    /// </summary>
    [Serializable]
    public struct WallSocketAttachmentState
    {
        public int controlPointIndex;
        public long structureId;
        public string socketId;
        public float socketYawDegrees;
        public float clearanceRadius;

        public WallSocketAttachmentState(
            int controlPointIndex,
            long structureId,
            string socketId,
            float socketYawDegrees,
            float clearanceRadius = 0f)
        {
            this.controlPointIndex = controlPointIndex;
            this.structureId = structureId;
            this.socketId = socketId;
            this.socketYawDegrees = socketYawDegrees;
            this.clearanceRadius =
                Mathf.Max(
                    0f,
                    clearanceRadius);
        }
    }

    /// <summary>
    /// Authoritative compact wall state.
    ///
    /// Do not network/save derived per-section or per-small-tower transforms.
    /// They are reproduced deterministically from this path on every client.
    /// </summary>
    [Serializable]
    public sealed class WallRuntimeState
    {
        public long wallId;
        public int ownerPlayerId;
        public string definitionId;
        public bool closedLoop;

        public List<Vector3> controlPoints =
            new List<Vector3>();

        public List<WallControlPointMode> controlPointModes =
            new List<WallControlPointMode>();

        public List<WallSocketAttachmentState> socketAttachments =
            new List<WallSocketAttachmentState>();

        public WallControlPointMode GetControlPointMode(
            int index)
        {
            if (index < 0 ||
                index >= controlPoints.Count ||
                index >= controlPointModes.Count)
            {
                return WallControlPointMode.Smooth;
            }

            return controlPointModes[index];
        }

        public void EnsureControlPointModes()
        {
            while (controlPointModes.Count <
                   controlPoints.Count)
            {
                controlPointModes.Add(
                    WallControlPointMode.Smooth);
            }

            while (controlPointModes.Count >
                   controlPoints.Count)
            {
                controlPointModes.RemoveAt(
                    controlPointModes.Count - 1);
            }
        }

        public void AddControlPoint(
            Vector3 position,
            WallControlPointMode mode =
                WallControlPointMode.Smooth)
        {
            controlPoints.Add(
                position);

            controlPointModes.Add(
                mode);
        }

        public void SetControlPointMode(
            int index,
            WallControlPointMode mode)
        {
            EnsureControlPointModes();

            if (index < 0 ||
                index >= controlPointModes.Count)
            {
                return;
            }

            controlPointModes[index] =
                mode;
        }

        public bool ToggleControlPointMode(
            int index)
        {
            EnsureControlPointModes();

            if (index < 0 ||
                index >= controlPointModes.Count)
            {
                return false;
            }

            controlPointModes[index] =
                controlPointModes[index] ==
                WallControlPointMode.Sharp
                    ? WallControlPointMode.Smooth
                    : WallControlPointMode.Sharp;

            return true;
        }

        public WallRuntimeState Clone()
        {
            var clone =
                new WallRuntimeState
                {
                    wallId = wallId,
                    ownerPlayerId = ownerPlayerId,
                    definitionId = definitionId,
                    closedLoop = closedLoop
                };

            clone.controlPoints.AddRange(
                controlPoints);

            clone.controlPointModes.AddRange(
                controlPointModes);

            clone.socketAttachments.AddRange(
                socketAttachments);

            clone.EnsureControlPointModes();

            return clone;
        }
    }

    [Serializable]
    public struct WallSectionPose
    {
        public long sectionId;
        public int sectionIndex;
        public Vector3 position;
        public float yawDegrees;

        public WallSectionPose(
            long sectionId,
            int sectionIndex,
            Vector3 position,
            float yawDegrees)
        {
            this.sectionId = sectionId;
            this.sectionIndex = sectionIndex;
            this.position = position;
            this.yawDegrees = yawDegrees;
        }

        public Quaternion Rotation =>
            Quaternion.Euler(
                0f,
                yawDegrees,
                0f);
    }

    [Serializable]
    public struct WallTowerPose
    {
        public long towerId;
        public int towerIndex;
        public Vector3 position;
        public float yawDegrees;
        public bool isStartTower;

        public WallTowerPose(
            long towerId,
            int towerIndex,
            Vector3 position,
            float yawDegrees,
            bool isStartTower)
        {
            this.towerId = towerId;
            this.towerIndex = towerIndex;
            this.position = position;
            this.yawDegrees = yawDegrees;
            this.isStartTower = isStartTower;
        }

        public Quaternion Rotation =>
            Quaternion.Euler(
                0f,
                yawDegrees,
                0f);
    }
}
