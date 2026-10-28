using System.Collections;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Presentation-only layout for the first neutral-village prefab.
    /// Structures are created only after their complete footprints can be
    /// verified against currently streamed, playable terrain colliders.
    /// </summary>
    public sealed class ConceptVillageFootprintPresenter : MonoBehaviour
    {
        private const int HouseSlotCount = 3;
        private const int FootprintSampleCount = 5;
        private const int MaximumSamplingAttempts = 60;
        private const int RaycastHitCapacity = 32;
        private const float SamplingRetrySeconds = 0.5f;
        private const float RayOriginOffset = 512f;
        private const float RayDistance = 1024f;
        private const float StructureSink = 0.05f;

        private static readonly Vector3[] HouseSlots =
        {
            new Vector3(-7f, 0f, -7f),
            new Vector3(7f, 0f, -7f),
            new Vector3(0f, 0f, 8f)
        };

        private static readonly Vector2[] CornerSigns =
        {
            new Vector2(-1f, -1f),
            new Vector2(-1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, -1f)
        };

        [Header("Approved structure prefabs")]
        [SerializeField] public GameObject housePrefab;
        [SerializeField] public GameObject wellPrefab;

        [Header("House terrain support")]
        [SerializeField]
        public Vector2 houseHalfExtents = new Vector2(2.5f, 2.5f);

        [Min(0f)]
        [SerializeField] public float maximumHeightSpread = 0.35f;

        [Range(0f, 90f)]
        [SerializeField] public float maximumSlopeDegrees = 12f;

        private readonly RaycastHit[] raycastHits =
            new RaycastHit[RaycastHitCapacity];

        private readonly float[][] houseHeights =
        {
            new float[FootprintSampleCount],
            new float[FootprintSampleCount],
            new float[FootprintSampleCount]
        };

        private readonly FootprintResult[] houseResults =
            new FootprintResult[HouseSlotCount];

        public int PlacedHouseCount { get; private set; }
        public bool PlacementFinished { get; private set; }

        /// <summary>
        /// Returns true only for exactly five finite terrain heights whose
        /// inclusive range is no greater than the supplied finite limit.
        /// </summary>
        public static bool IsSupportedFootprint(
            float[] heights,
            float maxSpread)
        {
            if (heights == null ||
                heights.Length != FootprintSampleCount ||
                !IsFinite(maxSpread) ||
                maxSpread < 0f)
            {
                return false;
            }

            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;

            for (int i = 0; i < heights.Length; i++)
            {
                float height = heights[i];
                if (!IsFinite(height))
                    return false;

                minimum = Mathf.Min(minimum, height);
                maximum = Mathf.Max(maximum, height);
            }

            return maximum - minimum <= maxSpread;
        }

        private IEnumerator Start()
        {
            var retryDelay =
                new WaitForSecondsRealtime(SamplingRetrySeconds);

            for (int attempt = 0;
                 attempt < MaximumSamplingAttempts;
                 attempt++)
            {
                if (TryCollectAllSupport(
                        out bool wellSupported,
                        out float wellHeight))
                {
                    PlaceSupportedStructures(
                        wellSupported,
                        wellHeight);
                    PlacementFinished = true;
                    yield break;
                }

                if (attempt + 1 < MaximumSamplingAttempts)
                    yield return retryDelay;
            }

            // Missing or ambiguous terrain support never creates structures.
            PlacementFinished = true;
        }

        private bool TryCollectAllSupport(
            out bool wellSupported,
            out float wellHeight)
        {
            bool allSamplesReady = true;

            for (int i = 0; i < HouseSlotCount; i++)
            {
                if (housePrefab == null)
                {
                    houseResults[i] = default;
                    continue;
                }

                houseResults[i] = SampleHouseFootprint(
                    HouseSlots[i],
                    GetHouseYaw(HouseSlots[i]),
                    houseHeights[i]);

                if (!houseResults[i].Ready)
                    allSamplesReady = false;
            }

            wellSupported = false;
            wellHeight = 0f;

            if (wellPrefab != null)
            {
                Vector3 centerWorld =
                    transform.TransformPoint(Vector3.zero);

                bool wellReady = TrySampleTerrain(
                    centerWorld,
                    out wellHeight,
                    out Vector3 wellNormal);

                if (!wellReady)
                {
                    allSamplesReady = false;
                }
                else
                {
                    wellSupported = IsSupportedSlope(wellNormal);
                }
            }

            return allSamplesReady;
        }

        private FootprintResult SampleHouseFootprint(
            Vector3 localCenter,
            float yawDegrees,
            float[] heights)
        {
            if (!IsFinite(houseHalfExtents.x) ||
                !IsFinite(houseHalfExtents.y) ||
                houseHalfExtents.x < 0f ||
                houseHalfExtents.y < 0f)
            {
                return new FootprintResult(true, false, 0f);
            }

            bool allSamplesReady = true;
            bool allSlopesSupported = true;
            Quaternion footprintRotation =
                Quaternion.Euler(0f, yawDegrees, 0f);

            for (int sampleIndex = 0;
                 sampleIndex < FootprintSampleCount;
                 sampleIndex++)
            {
                Vector3 localPoint = localCenter;

                if (sampleIndex > 0)
                {
                    Vector2 signs = CornerSigns[sampleIndex - 1];
                    Vector3 cornerOffset = new Vector3(
                        signs.x * houseHalfExtents.x,
                        0f,
                        signs.y * houseHalfExtents.y);

                    localPoint += footprintRotation * cornerOffset;
                }

                bool sampleReady = TrySampleTerrain(
                    transform.TransformPoint(localPoint),
                    out float height,
                    out Vector3 normal);

                heights[sampleIndex] =
                    sampleReady ? height : float.NaN;

                if (!sampleReady)
                {
                    allSamplesReady = false;
                }
                else if (!IsSupportedSlope(normal))
                {
                    allSlopesSupported = false;
                }
            }

            if (!allSamplesReady)
                return new FootprintResult(false, false, 0f);

            bool supported =
                allSlopesSupported &&
                IsSupportedFootprint(
                    heights,
                    maximumHeightSpread);

            float minimumHeight = heights[0];
            for (int i = 1; i < heights.Length; i++)
                minimumHeight = Mathf.Min(minimumHeight, heights[i]);

            return new FootprintResult(
                true,
                supported,
                minimumHeight);
        }

        private bool TrySampleTerrain(
            Vector3 worldPoint,
            out float height,
            out Vector3 normal)
        {
            height = 0f;
            normal = Vector3.zero;

            Vector3 rayOrigin = new Vector3(
                worldPoint.x,
                worldPoint.y + RayOriginOffset,
                worldPoint.z);

            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                Vector3.down,
                raycastHits,
                RayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            // RaycastNonAlloc cannot distinguish exactly-full from overflow.
            // Treat either as ambiguous rather than accepting partial results.
            if (hitCount <= 0 || hitCount >= raycastHits.Length)
                return false;

            float nearestDistance = float.PositiveInfinity;
            bool foundTerrain = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = raycastHits[i];
                if (!(hit.collider is MeshCollider meshCollider))
                    continue;

                GameObject hitObject = meshCollider.gameObject;
                StreamedChunkView chunkView =
                    hitObject.GetComponent<StreamedChunkView>();

                if (chunkView == null ||
                    !chunkView.IsPlayableChunk ||
                    !chunkView.TerrainColliderEnabled ||
                    hitObject.GetComponent<MeshCollider>() != meshCollider ||
                    !IsFinite(hit.distance) ||
                    hit.distance >= nearestDistance)
                {
                    continue;
                }

                foundTerrain = true;
                nearestDistance = hit.distance;
                height = hit.point.y;
                normal = hit.normal;
            }

            return foundTerrain &&
                   IsFinite(height) &&
                   IsFinite(normal.x) &&
                   IsFinite(normal.y) &&
                   IsFinite(normal.z);
        }

        private bool IsSupportedSlope(Vector3 normal)
        {
            if (!IsFinite(maximumSlopeDegrees) ||
                maximumSlopeDegrees < 0f ||
                !IsFinite(normal.sqrMagnitude) ||
                normal.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            return Vector3.Angle(normal, Vector3.up) <=
                   maximumSlopeDegrees;
        }

        private void PlaceSupportedStructures(
            bool wellSupported,
            float wellHeight)
        {
            if (housePrefab != null)
            {
                for (int i = 0; i < HouseSlotCount; i++)
                {
                    FootprintResult result = houseResults[i];
                    if (!result.Supported)
                        continue;

                    CreateStructure(
                        housePrefab,
                        "ConceptVillageHouse_" + i,
                        HouseSlots[i],
                        GetHouseYaw(HouseSlots[i]),
                        result.MinimumHeight);

                    PlacedHouseCount++;
                }
            }

            if (wellPrefab != null && wellSupported)
            {
                CreateStructure(
                    wellPrefab,
                    "ConceptVillageWell",
                    Vector3.zero,
                    0f,
                    wellHeight);
            }
        }

        private void CreateStructure(
            GameObject prefab,
            string instanceName,
            Vector3 localPosition,
            float localYaw,
            float terrainHeight)
        {
            GameObject instance = Object.Instantiate(
                prefab,
                transform,
                false);

            instance.name = instanceName;

            Vector3 worldPosition =
                transform.TransformPoint(localPosition);
            worldPosition.y = terrainHeight - StructureSink;

            instance.transform.localPosition =
                transform.InverseTransformPoint(worldPosition);
            instance.transform.localRotation =
                Quaternion.Euler(0f, localYaw, 0f);

            // Intentionally do not assign localScale: retain the prefab scale.
        }

        private static float GetHouseYaw(Vector3 localSlot)
        {
            Vector3 towardCenter = -localSlot;
            return Mathf.Atan2(
                       towardCenter.x,
                       towardCenter.z) *
                   Mathf.Rad2Deg;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private readonly struct FootprintResult
        {
            public FootprintResult(
                bool ready,
                bool supported,
                float minimumHeight)
            {
                Ready = ready;
                Supported = supported;
                MinimumHeight = minimumHeight;
            }

            public bool Ready { get; }
            public bool Supported { get; }
            public float MinimumHeight { get; }
        }
    }
}
