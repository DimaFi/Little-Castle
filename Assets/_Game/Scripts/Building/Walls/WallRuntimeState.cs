using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Building
{
    /// <summary>
    /// Authoritative compact wall state.
    ///
    /// Do not network/save derived per-section transforms. They are reproduced
    /// deterministically from this path on every client.
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
}
