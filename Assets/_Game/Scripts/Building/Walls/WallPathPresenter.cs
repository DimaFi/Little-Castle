using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Building
{
    /// <summary>
    /// Local presentation of one authoritative wall path.
    ///
    /// The presenter derives rigid module transforms locally. It may project
    /// them to nearby terrain, but the compact wall path remains the saved /
    /// networked source of truth.
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

        private readonly List<GameObject> instances =
            new List<GameObject>();

        public bool LastIsValid { get; private set; }
        public string LastValidationMessage { get; private set; } =
            string.Empty;

        public IReadOnlyList<WallSectionPose> LastSections =>
            sectionBuffer;

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

            WallPathLayoutUtility.BuildSections(
                wall,
                definition.SegmentSpacing,
                definition.CurveSampleStep,
                sectionBuffer);

            if (snapToGround &&
                !ProjectSectionsToGround(
                    definition,
                    out string groundReason))
            {
                LastValidationMessage =
                    groundReason;

                InstantiateSections(
                    definition,
                    wall,
                    preview
                        ? invalidPreviewMaterial
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

            InstantiateSections(
                definition,
                wall,
                previewMaterial);

            return LastIsValid;
        }

        public void Clear()
        {
            sectionBuffer.Clear();
            LastIsValid = false;
            LastValidationMessage = string.Empty;
            ClearInstances();
        }

        private bool ProjectSectionsToGround(
            WallPlacementDefinition definition,
            out string reason)
        {
            for (int i = 0;
                 i < sectionBuffer.Count;
                 i++)
            {
                WallSectionPose pose =
                    sectionBuffer[i];

                Vector3 origin =
                    pose.position +
                    Vector3.up *
                    groundProbeHeight;

                if (!Physics.Raycast(
                        origin,
                        Vector3.down,
                        out RaycastHit hit,
                        groundProbeHeight * 2f,
                        groundMask,
                        QueryTriggerInteraction.Ignore))
                {
                    reason =
                        "No ground was found below wall module " +
                        i +
                        ".";

                    return false;
                }

                float slope =
                    Vector3.Angle(
                        hit.normal,
                        Vector3.up);

                if (slope >
                    definition.MaximumGroundSlopeDegrees)
                {
                    reason =
                        "Ground is too steep for this wall (" +
                        slope.ToString("0.0") +
                        "°).";

                    return false;
                }

                pose.position =
                    hit.point +
                    Vector3.up *
                    definition.BaseHeightOffset;

                sectionBuffer[i] =
                    pose;
            }

            reason = string.Empty;
            return true;
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

            // Fallback for domain reload / serialized scene reconstruction.
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
