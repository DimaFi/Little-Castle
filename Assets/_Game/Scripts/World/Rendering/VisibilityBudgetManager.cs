using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace LittleCastle.Rendering
{
    [DefaultExecutionOrder(-100)]
    public sealed class VisibilityBudgetManager : MonoBehaviour
    {
        private static readonly ProfilerMarker EvaluateMarker =
            new ProfilerMarker(
                "World.VisibilityBudget.Evaluate");

        private static readonly ProfilerMarker CullingEventMarker =
            new ProfilerMarker(
                "World.VisibilityBudget.CullingEvent");

        private static readonly List<
            VisibilityBudgetTarget> PendingTargets =
                new List<
                    VisibilityBudgetTarget>(128);

        public static VisibilityBudgetManager Instance
        {
            get;
            private set;
        }

        [SerializeField]
        private Camera targetCamera;

        [SerializeField]
        private VisibilityBudgetSettings defaultSettings =
            VisibilityBudgetSettings.Default;

        [Min(0.05f)]
        [SerializeField]
        private float crossFadeDurationSeconds = 0.12f;

        [Min(16)]
        [SerializeField]
        private int initialCapacity = 256;

        private VisibilityBudgetTarget[] targets;
        private BoundingSphere[] spheres;
        private int targetCount;

        private CullingGroup cullingGroup;
        private Camera cullingCamera;

        private int fullCount;
        private int balancedCount;
        private int farCount;
        private int hiddenCount;
        private int approximateCameraVisibleCount;
        private int cullingVisibleCount;

        public int RegisteredTargetCount => targetCount;
        public int FullCount => fullCount;
        public int BalancedCount => balancedCount;
        public int FarCount => farCount;
        public int HiddenCount => hiddenCount;

        public int ApproximateCameraVisibleCount =>
            approximateCameraVisibleCount;

        public int CullingVisibleCount =>
            cullingVisibleCount;

        public Camera TargetCamera =>
            targetCamera;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            PendingTargets.Clear();
        }

        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
            {
                Destroy(
                    gameObject);

                return;
            }

            Instance = this;

            defaultSettings =
                defaultSettings.Sanitized();

            EnsureCapacity(
                Mathf.Max(
                    16,
                    initialCapacity));

            ApplyCrossFadeDuration();
            FlushPendingTargets();
            ResolveCamera();
        }

        private void OnEnable()
        {
            if (Instance != this)
                return;

            Camera.onPreCull +=
                HandleCameraPreCull;
        }

        private void OnDisable()
        {
            if (Instance != this)
                return;

            Camera.onPreCull -=
                HandleCameraPreCull;

            DisposeCullingGroup();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static VisibilityBudgetManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            VisibilityBudgetManager existing =
                FindFirstObjectByType<
                    VisibilityBudgetManager>();

            if (existing != null)
                return existing;

            var gameObject =
                new GameObject(
                    "[Little Castle] Visibility Budget");

            DontDestroyOnLoad(
                gameObject);

            return gameObject.AddComponent<
                VisibilityBudgetManager>();
        }

        public static void RegisterTarget(
            VisibilityBudgetTarget target)
        {
            if (target == null)
                return;

            if (Instance != null)
            {
                Instance.RegisterInternal(
                    target);

                return;
            }

            if (!PendingTargets.Contains(
                    target))
            {
                PendingTargets.Add(
                    target);
            }
        }

        public static void UnregisterTarget(
            VisibilityBudgetTarget target)
        {
            if (target == null)
                return;

            PendingTargets.Remove(
                target);

            if (Instance != null)
            {
                Instance.UnregisterInternal(
                    target);
            }
        }

        public static void NotifyTargetChanged(
            VisibilityBudgetTarget target)
        {
            if (target == null ||
                Instance == null)
            {
                return;
            }

            Instance.RefreshTargetInternal(
                target);
        }

        private void FlushPendingTargets()
        {
            for (int i = 0;
                 i < PendingTargets.Count;
                 i++)
            {
                VisibilityBudgetTarget target =
                    PendingTargets[i];

                if (target != null &&
                    target.isActiveAndEnabled)
                {
                    RegisterInternal(
                        target);
                }
            }

            PendingTargets.Clear();
        }

        private void RegisterInternal(
            VisibilityBudgetTarget target)
        {
            if (target == null ||
                target.RegistrationIndex >= 0)
            {
                return;
            }

            EnsureCapacity(
                targetCount + 1);

            VisibilityBudgetSettings settings =
                target.ResolveSettings(
                    defaultSettings);

            target.PrepareForBudget(
                settings);

            int index =
                targetCount++;

            targets[index] =
                target;

            spheres[index] =
                target.GetBoundingSphere(
                    settings.boundsPadding);

            target.SetRegistrationIndex(
                index);

            target.ResetCullingState();

            if (cullingGroup != null)
            {
                cullingGroup.SetBoundingSphereCount(
                    targetCount);
            }
        }

        private void UnregisterInternal(
            VisibilityBudgetTarget target)
        {
            int index =
                target != null
                    ? target.RegistrationIndex
                    : -1;

            if (index < 0 ||
                index >= targetCount ||
                targets[index] != target)
            {
                return;
            }

            int lastIndex =
                targetCount - 1;

            if (index !=
                lastIndex)
            {
                VisibilityBudgetTarget moved =
                    targets[lastIndex];

                targets[index] =
                    moved;

                spheres[index] =
                    spheres[lastIndex];

                if (moved != null)
                {
                    moved.SetRegistrationIndex(
                        index);
                }
            }

            targets[lastIndex] = null;
            spheres[lastIndex] =
                default(BoundingSphere);

            targetCount--;
            target.SetRegistrationIndex(
                -1);

            if (cullingGroup != null)
            {
                cullingGroup.SetBoundingSphereCount(
                    targetCount);
            }
        }

        private void RefreshTargetInternal(
            VisibilityBudgetTarget target)
        {
            if (target == null)
                return;

            int index =
                target.RegistrationIndex;

            if (index < 0 ||
                index >= targetCount ||
                targets[index] != target)
            {
                return;
            }

            VisibilityBudgetSettings settings =
                target.ResolveSettings(
                    defaultSettings);

            target.PrepareForBudget(
                settings);

            spheres[index] =
                target.GetBoundingSphere(
                    settings.boundsPadding);
        }

        private void EnsureCapacity(
            int required)
        {
            if (targets != null &&
                required <= targets.Length)
            {
                return;
            }

            int oldCapacity =
                targets != null
                    ? targets.Length
                    : 0;

            int newCapacity =
                Mathf.Max(
                    Mathf.Max(
                        16,
                        initialCapacity),
                    oldCapacity > 0
                        ? oldCapacity * 2
                        : 16);

            while (newCapacity <
                   required)
            {
                newCapacity *= 2;
            }

            var newTargets =
                new VisibilityBudgetTarget[
                    newCapacity];

            var newSpheres =
                new BoundingSphere[
                    newCapacity];

            if (targets != null &&
                targetCount > 0)
            {
                System.Array.Copy(
                    targets,
                    newTargets,
                    targetCount);

                System.Array.Copy(
                    spheres,
                    newSpheres,
                    targetCount);
            }

            targets =
                newTargets;

            spheres =
                newSpheres;

            if (cullingGroup != null)
            {
                cullingGroup.SetBoundingSpheres(
                    spheres);

                cullingGroup.SetBoundingSphereCount(
                    targetCount);
            }
        }

        private void ResolveCamera(
            Camera candidate = null)
        {
            Camera resolved =
                targetCamera;

            if (resolved == null)
            {
                resolved =
                    Camera.main;
            }

            if (resolved == null &&
                candidate != null &&
                candidate.cameraType ==
                    CameraType.Game)
            {
                resolved =
                    candidate;
            }

            if (resolved ==
                targetCamera &&
                cullingGroup != null &&
                cullingCamera == resolved)
            {
                return;
            }

            targetCamera =
                resolved;

            RebuildCullingGroup();
        }

        private void RebuildCullingGroup()
        {
            DisposeCullingGroup();

            if (targetCamera == null)
                return;

            cullingGroup =
                new CullingGroup();

            cullingCamera =
                targetCamera;

            cullingGroup.targetCamera =
                targetCamera;

            cullingGroup.SetBoundingSpheres(
                spheres);

            cullingGroup.SetBoundingSphereCount(
                targetCount);

            cullingGroup.onStateChanged =
                HandleCullingStateChanged;

            for (int i = 0;
                 i < targetCount;
                 i++)
            {
                if (targets[i] != null)
                {
                    targets[i]
                        .ResetCullingState();
                }
            }
        }

        private void DisposeCullingGroup()
        {
            if (cullingGroup == null)
                return;

            cullingGroup.onStateChanged = null;
            cullingGroup.Dispose();
            cullingGroup = null;
            cullingCamera = null;
        }

        private void HandleCameraPreCull(
            Camera camera)
        {
            if (!isActiveAndEnabled ||
                camera == null ||
                camera.cameraType !=
                    CameraType.Game)
            {
                return;
            }

            if (targetCamera == null ||
                cullingGroup == null ||
                cullingCamera !=
                    targetCamera)
            {
                ResolveCamera(
                    camera);
            }

            if (camera !=
                targetCamera)
            {
                return;
            }

            EvaluateTargets(
                camera);
        }

        private void EvaluateTargets(
            Camera camera)
        {
            using (EvaluateMarker.Auto())
            {
                float now =
                    Time.unscaledTime;

                fullCount = 0;
                balancedCount = 0;
                farCount = 0;
                hiddenCount = 0;
                approximateCameraVisibleCount = 0;
                cullingVisibleCount = 0;

                CameraProjection projection =
                    CameraProjection.Create(
                        camera);

                for (int i = 0;
                     i < targetCount;
                     i++)
                {
                    VisibilityBudgetTarget target =
                        targets[i];

                    if (target == null)
                        continue;

                    VisibilityBudgetSettings settings =
                        target.ResolveSettings(
                            defaultSettings);

                    BoundingSphere sphere =
                        target.GetBoundingSphere(
                            settings.boundsPadding);

                    spheres[i] =
                        sphere;

                    ViewMetrics metrics =
                        ViewMetrics.Calculate(
                            projection,
                            sphere,
                            settings.prewarmViewportRadius);

                    bool enteredApproxView =
                        metrics.insideView &&
                        !target.LastApproxInsideView;

                    float secondsSinceVisible =
                        target.CullingStateKnown
                            ? now -
                              target.LastCullingVisibleTime
                            : float.PositiveInfinity;

                    var input =
                        new VisibilityBudgetEvaluationInput
                        {
                            cullingStateKnown =
                                target.CullingStateKnown,
                            cullingVisible =
                                target.CullingVisible,
                            proactiveVisible =
                                enteredApproxView,
                            insideView =
                                metrics.insideView,
                            insidePrewarm =
                                metrics.insidePrewarm,
                            secondsSinceVisible =
                                secondsSinceVisible,
                            viewportEdge =
                                metrics.viewportEdge,
                            surfaceDistance =
                                metrics.surfaceDistance,
                            forceFullQuality =
                                target.ForceFullQuality
                        };

                    VisibilityQualityTier desired =
                        VisibilityBudgetPolicy.Evaluate(
                            settings,
                            input);

                    target.RequestTier(
                        desired,
                        now,
                        settings,
                        metrics.insideView);

                    target.LastApproxInsideView =
                        metrics.insideView;

                    if (metrics.insideView)
                        approximateCameraVisibleCount++;

                    if (target.CullingVisible)
                        cullingVisibleCount++;

                    switch (target.CurrentTier)
                    {
                        case VisibilityQualityTier.Full:
                            fullCount++;
                            break;

                        case VisibilityQualityTier.Balanced:
                            balancedCount++;
                            break;

                        case VisibilityQualityTier.Far:
                            farCount++;
                            break;

                        default:
                            hiddenCount++;
                            break;
                    }
                }
            }
        }

        private void HandleCullingStateChanged(
            CullingGroupEvent state)
        {
            using (CullingEventMarker.Auto())
            {
                int index =
                    state.index;

                if (index < 0 ||
                    index >= targetCount)
                {
                    return;
                }

                VisibilityBudgetTarget target =
                    targets[index];

                if (target == null)
                    return;

                float now =
                    Time.unscaledTime;

                target.NotifyCullingVisibility(
                    state.isVisible,
                    now);

                if (!state.isVisible)
                    return;

                VisibilityBudgetSettings settings =
                    target.ResolveSettings(
                        defaultSettings);

                target.RequestTier(
                    VisibilityQualityTier.Balanced,
                    now,
                    settings,
                    true);
            }
        }

        private void ApplyCrossFadeDuration()
        {
            LODGroup.crossFadeAnimationDuration =
                Mathf.Clamp(
                    crossFadeDurationSeconds,
                    0.05f,
                    0.5f);
        }

        private void OnValidate()
        {
            initialCapacity =
                Mathf.Max(
                    16,
                    initialCapacity);

            crossFadeDurationSeconds =
                Mathf.Clamp(
                    crossFadeDurationSeconds,
                    0.05f,
                    0.5f);

            defaultSettings =
                defaultSettings.Sanitized();

            if (Application.isPlaying)
            {
                ApplyCrossFadeDuration();
            }
        }

        private struct CameraProjection
        {
            public Vector3 position;
            public Vector3 forward;
            public Vector3 right;
            public Vector3 up;

            public bool orthographic;
            public float halfVertical;
            public float halfHorizontal;
            public float tanHalfVerticalFov;
            public float aspect;
            public float nearClip;
            public float farClip;

            public static CameraProjection Create(
                Camera camera)
            {
                Transform transform =
                    camera.transform;

                float aspect =
                    Mathf.Max(
                        0.01f,
                        camera.aspect);

                return new CameraProjection
                {
                    position =
                        transform.position,
                    forward =
                        transform.forward,
                    right =
                        transform.right,
                    up =
                        transform.up,
                    orthographic =
                        camera.orthographic,
                    halfVertical =
                        Mathf.Max(
                            0.01f,
                            camera.orthographicSize),
                    halfHorizontal =
                        Mathf.Max(
                            0.01f,
                            camera.orthographicSize *
                            aspect),
                    tanHalfVerticalFov =
                        Mathf.Tan(
                            Mathf.Deg2Rad *
                            camera.fieldOfView *
                            0.5f),
                    aspect =
                        aspect,
                    nearClip =
                        Mathf.Max(
                            0.001f,
                            camera.nearClipPlane),
                    farClip =
                        Mathf.Max(
                            camera.nearClipPlane +
                            0.01f,
                            camera.farClipPlane)
                };
            }
        }

        private struct ViewMetrics
        {
            public bool insideView;
            public bool insidePrewarm;
            public float viewportEdge;
            public float surfaceDistance;

            public static ViewMetrics Calculate(
                CameraProjection camera,
                BoundingSphere sphere,
                float prewarmRadius)
            {
                Vector3 delta =
                    sphere.position -
                    camera.position;

                float radius =
                    Mathf.Max(
                        0.001f,
                        sphere.radius);

                float depth =
                    Vector3.Dot(
                        camera.forward,
                        delta);

                float horizontal =
                    Mathf.Abs(
                        Vector3.Dot(
                            camera.right,
                            delta));

                float vertical =
                    Mathf.Abs(
                        Vector3.Dot(
                            camera.up,
                            delta));

                float halfHorizontal;
                float halfVertical;

                if (camera.orthographic)
                {
                    halfHorizontal =
                        camera.halfHorizontal;

                    halfVertical =
                        camera.halfVertical;
                }
                else
                {
                    float safeDepth =
                        Mathf.Max(
                            camera.nearClip,
                            depth);

                    halfVertical =
                        Mathf.Max(
                            0.001f,
                            safeDepth *
                            camera.tanHalfVerticalFov);

                    halfHorizontal =
                        Mathf.Max(
                            0.001f,
                            halfVertical *
                            camera.aspect);
                }

                float horizontalEdge =
                    Mathf.Max(
                        0f,
                        horizontal /
                            halfHorizontal -
                        radius /
                            halfHorizontal);

                float verticalEdge =
                    Mathf.Max(
                        0f,
                        vertical /
                            halfVertical -
                        radius /
                            halfVertical);

                float viewportEdge =
                    Mathf.Max(
                        horizontalEdge,
                        verticalEdge);

                bool depthIntersects =
                    depth + radius >=
                        camera.nearClip &&
                    depth - radius <=
                        camera.farClip;

                bool insideView =
                    depthIntersects &&
                    viewportEdge <= 1f;

                bool insidePrewarm =
                    !insideView &&
                    depthIntersects &&
                    viewportEdge <=
                        Mathf.Max(
                            1f,
                            prewarmRadius);

                float surfaceDistance =
                    Mathf.Max(
                        0f,
                        delta.magnitude -
                        radius);

                return new ViewMetrics
                {
                    insideView =
                        insideView,
                    insidePrewarm =
                        insidePrewarm,
                    viewportEdge =
                        viewportEdge,
                    surfaceDistance =
                        surfaceDistance
                };
            }
        }
    }
}
