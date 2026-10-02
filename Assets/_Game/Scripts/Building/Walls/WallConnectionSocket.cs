using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Building
{
    /// <summary>
    /// Authored connection point on a separate defensive structure.
    ///
    /// Place one or more socket child objects on an archer tower/gate/bastion.
    /// The socket transform position is the exact wall centerline connection,
    /// and local +Z is the preferred outgoing wall direction.
    /// </summary>
    public sealed class WallConnectionSocket : MonoBehaviour
    {
        [SerializeField] private string socketId = "wall_socket";

        [Tooltip(
            "Cursor distance at which this exact socket may be considered.")]
        [Min(0.1f)]
        [SerializeField] private float snapRadius = 2.5f;

        [Tooltip(
            "Ordinary wall modules inside this radius are omitted so the " +
            "defensive structure can physically replace that part of wall.")]
        [Min(0f)]
        [SerializeField] private float wallClearanceRadius = 1.5f;

        public string SocketId =>
            string.IsNullOrWhiteSpace(
                socketId)
                ? gameObject.name
                : socketId;

        public float SnapRadius =>
            Mathf.Max(
                0.1f,
                snapRadius);

        public float WallClearanceRadius =>
            Mathf.Max(
                0f,
                wallClearanceRadius);

        public Vector3 ConnectionPoint =>
            transform.position;

        public Vector3 Forward =>
            transform.forward;

        public float YawDegrees =>
            Mathf.Atan2(
                transform.forward.x,
                transform.forward.z) *
            Mathf.Rad2Deg;

        public bool TryGetStructureId(
            out long structureId)
        {
            GeneratedWorldObject handle =
                GetComponentInParent<
                    GeneratedWorldObject>();

            if (handle != null &&
                handle.StableId != 0)
            {
                structureId =
                    handle.StableId;

                return true;
            }

            structureId = 0;
            return false;
        }

        public bool TryCreateAttachment(
            int controlPointIndex,
            out WallSocketAttachmentState attachment)
        {
            if (!TryGetStructureId(
                    out long structureId))
            {
                attachment = default;
                return false;
            }

            attachment =
                new WallSocketAttachmentState(
                    controlPointIndex,
                    structureId,
                    SocketId,
                    YawDegrees,
                    WallClearanceRadius);

            return true;
        }

        private void OnValidate()
        {
            snapRadius =
                Mathf.Max(
                    0.1f,
                    snapRadius);

            wallClearanceRadius =
                Mathf.Max(
                    0f,
                    wallClearanceRadius);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(
                transform.position,
                SnapRadius);

            Gizmos.DrawLine(
                transform.position,
                transform.position +
                transform.forward *
                Mathf.Max(
                    1f,
                    SnapRadius));
        }
#endif
    }

    public static class WallSocketSnapUtility
    {
        private static readonly Collider[] OverlapBuffer =
            new Collider[32];

        public static bool TryFindNearestSocket(
            Vector3 worldPoint,
            float maximumDistance,
            LayerMask layerMask,
            out WallConnectionSocket socket)
        {
            socket = null;

            float safeDistance =
                Mathf.Max(
                    0.1f,
                    maximumDistance);

            int count =
                Physics.OverlapSphereNonAlloc(
                    worldPoint,
                    safeDistance,
                    OverlapBuffer,
                    layerMask,
                    QueryTriggerInteraction.Collide);

            float bestDistanceSqr =
                float.PositiveInfinity;

            for (int i = 0;
                 i < count;
                 i++)
            {
                Collider collider =
                    OverlapBuffer[i];

                if (collider == null)
                    continue;

                WallConnectionSocket candidate =
                    collider.GetComponent<
                        WallConnectionSocket>();

                if (candidate == null)
                {
                    candidate =
                        collider.GetComponentInParent<
                            WallConnectionSocket>();
                }

                if (candidate == null)
                    continue;

                float allowed =
                    Mathf.Min(
                        safeDistance,
                        candidate.SnapRadius);

                Vector3 delta =
                    candidate.ConnectionPoint -
                    worldPoint;

                float distanceSqr =
                    delta.sqrMagnitude;

                if (distanceSqr >
                        allowed * allowed ||
                    distanceSqr >=
                        bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr =
                    distanceSqr;

                socket =
                    candidate;
            }

            return socket != null;
        }
    }
}
