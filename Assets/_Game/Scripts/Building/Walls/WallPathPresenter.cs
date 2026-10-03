using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Building
{
    /// <summary>
    /// Local presentation of one authoritative wall path.
    ///
    /// Small structural wall towers and rigid wall modules are derived locally.
    /// Separate defensive buildings attach through WallConnectionSocket data.
    /// </summary>
    public sealed class WallPathPresenter : MonoBehaviour
    {
        [Header("Ground projection")]
        [SerializeField] private bool snapToGround = true;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(1f)] private float groundProbeHeight = 250f;

        [Header("Preview")]
        [SerializeField] private Material validPreviewMaterial;
        [SerializeField] private Material invalidPreviewMaterial;

        private readonly List<WallSectionPose> sectionBuffer =
            new List<WallSectionPose>();

        private readonly List<WallTowerPose> towerBuffer =
            new List<WallTowerPose>();

        private readonly List<GameObject> instances =
            new List<GameObject>();

        public bool LastIsValid { get; private set; }

        public string LastValidationMessage { get; private set; } =
            string.Empty;

        public IReadOnlyList<WallSectionPose> LastSections =>
            sectionBuffer;

        public IReadOnlyList<WallTowerPose> LastTowers =>
            towerBuffer;

        public bool Rebuild(
            WallPlacementDefinition definition,
            WallRuntimeState wall,
            bool preview)
        {
            ClearInstances();

            LastIsValid = false;
            LastValidationMessage = string.Empty;

            if (definition == null ||
                wall == null)
            {
                LastValidationMessage =
                    "Wall definition or state is missing.";

                return false;
            }

            WallPathLayoutUtility.BuildTowerPoses(
                wall,
                definition,
                towerBuffer);

            WallPathLayoutUtility.BuildSections(
                wall,
                definition,
                sectionBuffer);

            if (snapToGround &&
                !ProjectLayoutToGround(
                    definition,
                    out string groundReason))
            {
                LastValidationMessage =
                    groundReason;

                InstantiateLayout(
                    definition,
                    wall,
                    preview
                        ? invalidPreviewMaterial
                        : null);

                return false;
            }

            // One committed point intentionally shows only the start tower.
            // Confirmation still requires a real wall path.
            if (wall.controlPoints.Count < 2)
            {
                LastValidationMessage =
                    "Add another wall point.";

                InstantiateLayout(
                    definition,
                    wall,
                    preview
                        ? validPreviewMaterial
                        : null);

                return false;
            }

            LastIsValid =
                WallPlacementValidator.Validate(
                    wall,
                    definition,
                    sectionBuffer,
                    out string validationReason);

            LastValidationMessage =
                validationReason;

            Material previewMaterial =
                preview
                    ? (LastIsValid
                        ? validPreviewMaterial
                        : invalidPreviewMaterial)
                    : null;

            InstantiateLayout(
                definition,
                wall,
                previewMaterial);

            return LastIsValid;
        }

        public void Clear()
        {
            sectionBuffer.Clear();
            towerBuffer.Clear();
            LastIsValid = false;
            LastValidationMessage = string.Empty;
            ClearInstances();
        }

        private bool ProjectLayoutToGround(
            WallPlacementDefinition definition,
            out string reason)
        {
            for (int i = 0;
                 i < towerBuffer.Count;
                 i++)
            {
                WallTowerPose pose =
                    towerBuffer[i];

                if (!TryProjectPointToGround(
                        pose.position,
                        definition,
                        out Vector3 projected,
                        out reason))
                {
                    reason =
                        "Wall tower " +
                        i +
                        ": " +
                        reason;

                    return false;
                }

                pose.position =
                    projected;

                towerBuffer[i] =
                    pose;
            }

            for (int i = 0;
                 i < sectionBuffer.Count;
                 i++)
            {
                WallSectionPose pose =
                    sectionBuffer[i];

                if (!TryProjectPointToGround(
                        pose.position,
                        definition,
                        out Vector3 projected,
                        out reason))
                {
                    reason =
                        "Wall module " +
                        i +
                        ": " +
                        reason;

                    return false;
                }

                pose.position =
                    projected;

                sectionBuffer[i] =
                    pose;
            }

            reason = string.Empty;
            return true;
        }

        private bool TryProjectPointToGround(
            Vector3 point,
            WallPlacementDefinition definition,
            out Vector3 projected,
            out string reason)
        {
            Vector3 origin =
                point +
                Vector3.up *
                groundProbeHeight;

            RaycastHit[] hits =
                Physics.RaycastAll(
                    origin,
                    Vector3.down,
                    groundProbeHeight * 2f,
                    groundMask,
                    QueryTriggerInteraction.Ignore);

            if (hits == null ||
                hits.Length == 0)
            {
                projected = point;
                reason =
                    "No ground was found.";

                return false;
            }

            Array.Sort(
                hits,
                (a, b) =>
                    a.distance.CompareTo(
                        b.distance));

            bool foundTerrain = false;
            RaycastHit hit = default;

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

                hit = hits[i];
                foundTerrain = true;
                break;
            }

            if (!foundTerrain)
            {
                // Preserve editor/manual-scene usefulness when generated
                // StreamedChunkView terrain is not present.
                hit = hits[0];
            }

            float slope =
                Vector3.Angle(
                    hit.normal,
                    Vector3.up);

            if (slope >
                definition.MaximumGroundSlopeDegrees)
            {
                projected = point;
                reason =
                    "Ground is too steep (" +
                    slope.ToString("0.0") +
                    "°).";

                return false;
            }

            projected =
                hit.point +
                Vector3.up *
                definition.BaseHeightOffset;

            reason = string.Empty;
            return true;
        }

        private void InstantiateLayout(
            WallPlacementDefinition definition,
            WallRuntimeState wall,
            Material overrideMaterial)
        {
            InstantiateTowers(
                definition,
                wall,
                overrideMaterial);

            InstantiateSections(
                definition,
                wall,
                overrideMaterial);
        }

        private void InstantiateTowers(
            WallPlacementDefinition definition,
            WallRuntimeState wall,
            Material overrideMaterial)
        {
            for (int i = 0;
                 i < towerBuffer.Count;
                 i++)
            {
                WallTowerPose pose =
                    towerBuffer[i];

                GameObject prefab =
                    pose.isStartTower
                        ? definition.StartTowerPrefab
                        : definition.RepeatTowerPrefab;

                if (prefab == null)
                    continue;

                GameObject instance =
                    Instantiate(
                        prefab,
                        pose.position,
                        pose.Rotation,
                        transform);

                instance.name =
                    "Wall_" +
                    wall.wallId +
                    "_Tower_" +
                    pose.towerIndex;

                WallTowerHandle handle =
                    instance.GetComponent<
                        WallTowerHandle>();

                if (handle == null)
                {
                    handle =
                        instance.AddComponent<
                            WallTowerHandle>();
                }

                handle.Initialize(
                    wall.wallId,
                    pose.towerId,
                    pose.towerIndex,
                    pose.isStartTower);

                if (overrideMaterial != null)
                {
                    ApplyMaterialOverride(
                        instance,
                        overrideMaterial);
                }

                instances.Add(
                    instance);
            }
        }

        private void InstantiateSections(
            WallPlacementDefinition definition,
            WallRuntimeState wall,
            Material overrideMaterial)
        {
            GameObject prefab =
                definition.SegmentPrefab;

            if (prefab == null)
                return;

            for (int i = 0;
                 i < sectionBuffer.Count;
                 i++)
            {
                WallSectionPose pose =
                    sectionBuffer[i];

                GameObject instance =
                    Instantiate(
                        prefab,
                        pose.position,
                        pose.Rotation,
                        transform);

                instance.name =
                    "Wall_" +
                    wall.wallId +
                    "_Section_" +
                    pose.sectionIndex;

                WallSectionHandle handle =
                    instance.GetComponent<
                        WallSectionHandle>();

                if (handle == null)
                {
                    handle =
                        instance.AddComponent<
                            WallSectionHandle>();
                }

                handle.Initialize(
                    wall.wallId,
                    pose.sectionId,
                    pose.sectionIndex);

                if (overrideMaterial != null)
                {
                    ApplyMaterialOverride(
                        instance,
                        overrideMaterial);
                }

                instances.Add(
                    instance);
            }
        }

        private static void ApplyMaterialOverride(
            GameObject root,
            Material material)
        {
            Renderer[] renderers =
                root.GetComponentsInChildren<
                    Renderer>(true);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Material[] current =
                    renderers[i].sharedMaterials;

                if (current == null ||
                    current.Length == 0)
                {
                    renderers[i].sharedMaterial =
                        material;

                    continue;
                }

                var replacement =
                    new Material[
                        current.Length];

                for (int m = 0;
                     m < replacement.Length;
                     m++)
                {
                    replacement[m] =
                        material;
                }

                renderers[i].sharedMaterials =
                    replacement;
            }
        }

        private void ClearInstances()
        {
            if (instances.Count > 0)
            {
                for (int i = 0;
                     i < instances.Count;
                     i++)
                {
                    GameObject instance =
                        instances[i];

                    if (instance == null)
                        continue;

                    if (Application.isPlaying)
                    {
                        instance.SetActive(false);

                        Destroy(
                            instance);
                    }
                    else
                    {
                        DestroyImmediate(
                            instance);
                    }
                }

                instances.Clear();
                return;
            }

            for (int i = transform.childCount - 1;
                 i >= 0;
                 i--)
            {
                Transform child =
                    transform.GetChild(i);

                if (child == null)
                    continue;

                if (Application.isPlaying)
                {
                    child.gameObject.SetActive(false);

                    Destroy(
                        child.gameObject);
                }
                else
                {
                    DestroyImmediate(
                        child.gameObject);
                }
            }
        }

        private void OnDestroy()
        {
            instances.Clear();
        }

        private void OnValidate()
        {
            groundProbeHeight =
                Mathf.Max(
                    1f,
                    groundProbeHeight);
        }
    }
}
