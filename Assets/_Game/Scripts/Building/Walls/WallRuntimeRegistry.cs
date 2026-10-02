using System;
using System.Collections.Generic;

namespace LittleCastle.Building
{
    /// <summary>
    /// Server/save-side registry of authoritative wall paths.
    ///
    /// Visual sections are derived locally and are intentionally not stored
    /// here.
    /// </summary>
    [Serializable]
    public sealed class WallRuntimeRegistry
    {
        private readonly Dictionary<long, WallRuntimeState> walls =
            new Dictionary<long, WallRuntimeState>();

        public int Count => walls.Count;

        public event Action<WallRuntimeState> WallChanged;
        public event Action<long> WallRemoved;

        public bool Upsert(
            WallRuntimeState wall)
        {
            if (wall == null ||
                wall.wallId == 0 ||
                string.IsNullOrWhiteSpace(
                    wall.definitionId) ||
                wall.controlPoints == null ||
                wall.controlPoints.Count < 2)
            {
                return false;
            }

            WallRuntimeState clone =
                wall.Clone();

            if (walls.TryGetValue(
                    clone.wallId,
                    out WallRuntimeState current) &&
                AreEquivalent(
                    current,
                    clone))
            {
                return false;
            }

            walls[
                clone.wallId] =
                clone;

            WallChanged?.Invoke(
                clone.Clone());

            return true;
        }

        public bool Remove(
            long wallId)
        {
            if (!walls.Remove(
                    wallId))
            {
                return false;
            }

            WallRemoved?.Invoke(
                wallId);

            return true;
        }

        public bool TryGet(
            long wallId,
            out WallRuntimeState wall)
        {
            if (walls.TryGetValue(
                    wallId,
                    out WallRuntimeState current))
            {
                wall =
                    current.Clone();

                return true;
            }

            wall = null;
            return false;
        }

        public void CopyAllTo(
            List<WallRuntimeState> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();

            foreach (
                KeyValuePair<long, WallRuntimeState> pair
                in walls)
            {
                output.Add(
                    pair.Value.Clone());
            }
        }

        private static bool AreEquivalent(
            WallRuntimeState a,
            WallRuntimeState b)
        {
            if (a == null ||
                b == null ||
                a.wallId != b.wallId ||
                a.ownerPlayerId != b.ownerPlayerId ||
                a.definitionId != b.definitionId ||
                a.closedLoop != b.closedLoop ||
                a.controlPoints.Count !=
                    b.controlPoints.Count)
            {
                return false;
            }

            a.EnsureControlPointModes();
            b.EnsureControlPointModes();

            for (int i = 0;
                 i < a.controlPoints.Count;
                 i++)
            {
                if (a.controlPoints[i] !=
                        b.controlPoints[i] ||
                    a.GetControlPointMode(i) !=
                        b.GetControlPointMode(i))
                {
                    return false;
                }
            }

            if (a.socketAttachments.Count !=
                b.socketAttachments.Count)
            {
                return false;
            }

            for (int i = 0;
                 i < a.socketAttachments.Count;
                 i++)
            {
                WallSocketAttachmentState left =
                    a.socketAttachments[i];

                WallSocketAttachmentState right =
                    b.socketAttachments[i];

                if (left.controlPointIndex !=
                        right.controlPointIndex ||
                    left.structureId !=
                        right.structureId ||
                    left.socketId !=
                        right.socketId ||
                    !UnityEngine.Mathf.Approximately(
                        left.socketYawDegrees,
                        right.socketYawDegrees) ||
                    !UnityEngine.Mathf.Approximately(
                        left.clearanceRadius,
                        right.clearanceRadius))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
