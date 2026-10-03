using System.Collections;
using LittleCastle.CameraSystem;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Building
{
    /// <summary>
    /// Lightweight development harness used by the ignored local sandbox scene.
    /// It keeps the real world streamer running, exposes the existing wall build
    /// pipeline to the player, preserves confirmed walls for the session, and
    /// places one representative house on nearby flat terrain for immediate QA.
    /// </summary>
    public sealed class LocalSandboxController : MonoBehaviour
    {
        [Header("Wall building")]
        [SerializeField] private WallBuildInputController wallInput;
        [SerializeField] private WallPlacementController wallPlacement;
        [SerializeField] private WallPlacementDefinition wallDefinition;
        [SerializeField] private StrategyCameraController strategyCamera;
        [SerializeField] private KeyCode wallBuildKey = KeyCode.B;

        [Header("Showcase building")]
        [SerializeField] private GameObject showcaseHousePrefab;
        [SerializeField] private Vector2 preferredHouseXZ = new Vector2(28f, 24f);
        [SerializeField, Min(0f)] private float flatGroundSearchRadius = 48f;
        [SerializeField, Min(1f)] private float flatGroundSearchStep = 6f;
        [SerializeField, Range(0f, 45f)] private float maximumHouseSlope = 12f;

        [Header("HUD")]
        [SerializeField] private bool showHud = true;
        [SerializeField] private KeyCode toggleHudKey = KeyCode.F7;

        private Transform confirmedWallsRoot;
        private GameObject showcaseHouseInstance;
        private long nextWallId = 2000000;

        private void Awake()
        {
            if (strategyCamera == null)
                strategyCamera = FindFirstObjectByType<StrategyCameraController>();

            if (wallInput != null)
                wallInput.WallConfirmed += OnWallConfirmed;
        }

        private void Start()
        {
            var root = new GameObject("Confirmed Walls");
            root.transform.SetParent(transform, false);
            confirmedWallsRoot = root.transform;

            if (showcaseHousePrefab != null)
                StartCoroutine(PlaceShowcaseHouseWhenTerrainIsReady());
        }

        private void OnDestroy()
        {
            if (wallInput != null)
                wallInput.WallConfirmed -= OnWallConfirmed;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleHudKey))
                showHud = !showHud;

            if (Input.GetKeyDown(wallBuildKey))
                ToggleWallBuild();
        }

        public void ToggleWallBuild()
        {
            if (wallInput == null || wallDefinition == null)
                return;

            if (wallInput.IsBuilding)
            {
                wallInput.CancelBuild();
                return;
            }

            nextWallId++;

            wallInput.BeginBuild(
                wallDefinition,
                nextWallId,
                1,
                false);
        }

        private void OnWallConfirmed(
            WallRuntimeState wall)
        {
            if (wall == null || wallDefinition == null)
                return;

            var root = new GameObject("Wall_" + wall.wallId);

            if (confirmedWallsRoot != null)
                root.transform.SetParent(confirmedWallsRoot, false);
            else
                root.transform.SetParent(transform, false);

            WallPathPresenter presenter =
                root.AddComponent<WallPathPresenter>();

            presenter.Rebuild(
                wallDefinition,
                wall,
                false);
        }

        private IEnumerator PlaceShowcaseHouseWhenTerrainIsReady()
        {
            // Streaming and terrain colliders are created progressively. Wait for
            // a nearby generated collider instead of racing WorldStreamer.Start.
            const int maxFrames = 360;

            for (int frame = 0; frame < maxFrames; frame++)
            {
                if (TryFindFlatGround(
                        out Vector3 position,
                        out Vector3 normal))
                {
                    showcaseHouseInstance =
                        Instantiate(
                            showcaseHousePrefab,
                            transform);

                    showcaseHouseInstance.name =
                        "Sandbox_ShowcaseHouse";

                    showcaseHouseInstance.transform.SetPositionAndRotation(
                        position,
                        Quaternion.FromToRotation(
                            Vector3.up,
                            normal) *
                        Quaternion.Euler(0f, 25f, 0f));

                    GroundInstance(
                        showcaseHouseInstance,
                        position.y);

                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning(
                "[Little Castle Sandbox] Could not find loaded flat terrain " +
                "for the showcase house within the startup wait.");
        }

        private bool TryFindFlatGround(
            out Vector3 bestPoint,
            out Vector3 bestNormal)
        {
            bestPoint = default;
            bestNormal = Vector3.up;

            float bestSlope = float.MaxValue;
            bool found = false;

            float radius =
                Mathf.Max(
                    0f,
                    flatGroundSearchRadius);

            float step =
                Mathf.Max(
                    1f,
                    flatGroundSearchStep);

            int rings =
                Mathf.CeilToInt(
                    radius / step);

            for (int z = -rings; z <= rings; z++)
            {
                for (int x = -rings; x <= rings; x++)
                {
                    Vector2 offset =
                        new Vector2(
                            x * step,
                            z * step);

                    if (offset.magnitude > radius)
                        continue;

                    Vector3 origin =
                        new Vector3(
                            preferredHouseXZ.x + offset.x,
                            1200f,
                            preferredHouseXZ.y + offset.y);

                    RaycastHit[] hits =
                        Physics.RaycastAll(
                            origin,
                            Vector3.down,
                            2400f,
                            ~0,
                            QueryTriggerInteraction.Ignore);

                    if (hits == null ||
                        hits.Length == 0)
                    {
                        continue;
                    }

                    System.Array.Sort(
                        hits,
                        (a, b) =>
                            a.distance.CompareTo(
                                b.distance));

                    bool hasTerrainHit = false;
                    RaycastHit terrainHit = default;

                    for (int h = 0; h < hits.Length; h++)
                    {
                        StreamedChunkView chunk =
                            hits[h].collider != null
                                ? hits[h].collider.GetComponentInParent<
                                    StreamedChunkView>()
                                : null;

                        if (chunk == null ||
                            !chunk.IsPlayableChunk)
                        {
                            continue;
                        }

                        terrainHit = hits[h];
                        hasTerrainHit = true;
                        break;
                    }

                    if (!hasTerrainHit)
                        continue;

                    float slope =
                        Vector3.Angle(
                            terrainHit.normal,
                            Vector3.up);

                    if (slope < bestSlope)
                    {
                        bestSlope = slope;
                        bestPoint = terrainHit.point;
                        bestNormal = terrainHit.normal;
                        found = true;
                    }

                    if (slope <= maximumHouseSlope)
                        return true;
                }
            }

            return found &&
                   bestSlope <=
                   Mathf.Max(
                       maximumHouseSlope,
                       18f);
        }

        private static void GroundInstance(
            GameObject instance,
            float targetGroundY)
        {
            Renderer[] renderers =
                instance.GetComponentsInChildren<Renderer>(
                    true);

            bool hasBounds = false;
            Bounds bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null ||
                    !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }

            if (!hasBounds)
                return;

            float correction =
                targetGroundY -
                bounds.min.y;

            instance.transform.position +=
                Vector3.up *
                correction;
        }

        private void OnGUI()
        {
            if (!showHud)
                return;

            const float width = 390f;
            Rect area =
                new Rect(
                    Screen.width - width - 16f,
                    16f,
                    width,
                    214f);

            GUILayout.BeginArea(
                area,
                GUI.skin.box);

            GUILayout.Label(
                "LITTLE CASTLE — LOCAL SANDBOX");

            GUILayout.Label(
                "B — начать / отменить строительство стены");

            GUILayout.Label(
                "ЛКМ — поставить точку   Shift — Sharp/Smooth");

            GUILayout.Label(
                "Backspace — убрать точку   C — замкнуть контур");

            GUILayout.Label(
                "Enter — подтвердить   Esc — отменить");

            if (wallPlacement != null &&
                wallInput != null &&
                wallInput.IsBuilding)
            {
                string mode =
                    wallPlacement.GetLastControlPointMode()
                        .ToString();

                string status =
                    string.IsNullOrEmpty(
                        wallPlacement.PreviewValidationMessage)
                        ? "готово"
                        : wallPlacement.PreviewValidationMessage;

                GUILayout.Space(4f);
                GUILayout.Label(
                    "Режим стены: " +
                    mode +
                    " | " +
                    status);
            }

            GUILayout.Space(4f);

            if (GUILayout.Button(
                    wallInput != null &&
                    wallInput.IsBuilding
                        ? "Отменить стену [B]"
                        : "Рисовать стену [B]"))
            {
                ToggleWallBuild();
            }

            GUILayout.Label(
                "F7 — скрыть подсказку   F8 — FPS/streaming");

            GUILayout.EndArea();
        }
    }
}
