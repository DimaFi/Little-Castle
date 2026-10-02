using UnityEngine;

namespace LittleCastle.Building
{
    /// <summary>
    /// Asset-independent contract for one modular wall type.
    ///
    /// The authored prefab is presentation. Gameplay/networking stores only
    /// definitionId + wall path and derives section transforms locally.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WallPlacementDefinition",
        menuName = "Little Castle/Building/Wall Placement Definition")]
    public sealed class WallPlacementDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string definitionId = "stone_wall_a";

        [Header("Module geometry")]
        [Tooltip(
            "World-space length of one authored wall module along local +Z.")]
        [Min(0.1f)]
        [SerializeField] private float segmentLength = 2f;

        [Tooltip(
            "Small overlap hides cracks on curves. 1 = exact nominal spacing.")]
        [Range(0.75f, 1f)]
        [SerializeField] private float spacingMultiplier = 0.96f;

        [Tooltip(
            "Vertical sink/offset applied after terrain projection.")]
        [SerializeField] private float baseHeightOffset = 0f;

        [Header("Curve")]
        [Tooltip(
            "Distance used while sampling the smoothed centerline before it is " +
            "resampled into fixed-length modules.")]
        [Min(0.05f)]
        [SerializeField] private float curveSampleStep = 0.5f;

        [Tooltip(
            "Maximum allowed yaw change between neighboring modules. Very " +
            "sharp turns should be rejected or require a dedicated corner.")]
        [Range(1f, 90f)]
        [SerializeField] private float maximumTurnDegrees = 28f;

        [Header("Terrain")]
        [Range(0f, 60f)]
        [SerializeField] private float maximumGroundSlopeDegrees = 22f;

        [Min(0.01f)]
        [SerializeField] private float maximumHeightStep = 0.75f;

        [Header("Presentation")]
        [SerializeField] private GameObject segmentPrefab;

        [Tooltip(
            "Optional dedicated endpoint/cap. It is not required by the core " +
            "layout algorithm.")]
        [SerializeField] private GameObject endCapPrefab;

        public string DefinitionId =>
            string.IsNullOrWhiteSpace(definitionId)
                ? "wall"
                : definitionId;

        public float SegmentLength =>
            Mathf.Max(0.1f, segmentLength);

        public float SegmentSpacing =>
            SegmentLength *
            Mathf.Clamp(
                spacingMultiplier,
                0.75f,
                1f);

        public float BaseHeightOffset =>
            baseHeightOffset;

        public float CurveSampleStep =>
            Mathf.Max(
                0.05f,
                curveSampleStep);

        public float MaximumTurnDegrees =>
            Mathf.Clamp(
                maximumTurnDegrees,
                1f,
                90f);

        public float MaximumGroundSlopeDegrees =>
            Mathf.Clamp(
                maximumGroundSlopeDegrees,
                0f,
                60f);

        public float MaximumHeightStep =>
            Mathf.Max(
                0.01f,
                maximumHeightStep);

        public GameObject SegmentPrefab =>
            segmentPrefab;

        public GameObject EndCapPrefab =>
            endCapPrefab;

        private void OnValidate()
        {
            segmentLength =
                Mathf.Max(
                    0.1f,
                    segmentLength);

            curveSampleStep =
                Mathf.Max(
                    0.05f,
                    curveSampleStep);

            maximumHeightStep =
                Mathf.Max(
                    0.01f,
                    maximumHeightStep);
        }
    }
}
