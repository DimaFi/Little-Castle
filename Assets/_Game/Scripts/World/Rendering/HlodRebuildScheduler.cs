using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Central client-side queue for expensive HLOD proxy rebuilds.
    ///
    /// Multiple dirty notifications for one target are coalesced. The queue is
    /// presentation only and must never own authoritative wall/building state.
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class HlodRebuildScheduler : MonoBehaviour
    {
        private static readonly ProfilerMarker RebuildMarker =
            new ProfilerMarker(
                "World.HLOD.RebuildProxy");

        public static HlodRebuildScheduler Instance
        {
            get;
            private set;
        }

        [SerializeField]
        [Min(1)]
        private int maxRebuildsPerFrame = 1;

        [SerializeField]
        [Min(0.1f)]
        private float softTimeBudgetMilliseconds = 1.5f;

        private readonly Queue<HlodProxyRebuildable> queue =
            new Queue<HlodProxyRebuildable>();

        private readonly HashSet<HlodProxyRebuildable> queued =
            new HashSet<HlodProxyRebuildable>();

        public int PendingCount =>
            queued.Count;

        public int TotalRebuildCount
        {
            get;
            private set;
        }

        public float LastRebuildMilliseconds
        {
            get;
            private set;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
            {
                Debug.LogWarning(
                    "Only one HlodRebuildScheduler should be active. " +
                    "Disabling duplicate component.",
                    this);

                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static HlodRebuildScheduler EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            HlodRebuildScheduler existing =
                FindFirstObjectByType<HlodRebuildScheduler>();

            if (existing != null)
                return existing;

            var root =
                new GameObject(
                    "[Little Castle] HLOD Rebuild Scheduler");

            DontDestroyOnLoad(root);

            return root.AddComponent<HlodRebuildScheduler>();
        }

        public static void Schedule(
            HlodProxyRebuildable target)
        {
            if (target == null ||
                !target.isActiveAndEnabled)
            {
                return;
            }

            EnsureInstance().EnqueueInternal(target);
        }

        public static void Cancel(
            HlodProxyRebuildable target)
        {
            if (target == null)
                return;

            target.IsHlodRebuildQueued = false;

            if (Instance != null)
                Instance.queued.Remove(target);
        }

        private void EnqueueInternal(
            HlodProxyRebuildable target)
        {
            if (target == null ||
                !target.isActiveAndEnabled ||
                !queued.Add(target))
            {
                return;
            }

            target.IsHlodRebuildQueued = true;
            queue.Enqueue(target);
        }

        private void LateUpdate()
        {
            if (queue.Count == 0)
                return;

            double frameStart =
                Time.realtimeSinceStartupAsDouble;

            int processed = 0;
            int visitsRemaining =
                Mathf.Max(
                    32,
                    maxRebuildsPerFrame * 8);

            while (queue.Count > 0 &&
                   processed < maxRebuildsPerFrame &&
                   visitsRemaining-- > 0)
            {
                HlodProxyRebuildable target =
                    queue.Dequeue();

                if (target == null ||
                    !queued.Remove(target))
                {
                    continue;
                }

                target.IsHlodRebuildQueued = false;

                if (!target.isActiveAndEnabled)
                    continue;

                double rebuildStart =
                    Time.realtimeSinceStartupAsDouble;

                try
                {
                    using (RebuildMarker.Auto())
                    {
                        target.RebuildHlodProxy();
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(
                        exception,
                        target);
                }

                LastRebuildMilliseconds =
                    (float)(
                        (Time.realtimeSinceStartupAsDouble -
                         rebuildStart) *
                        1000.0);

                TotalRebuildCount++;
                processed++;

                double elapsedMilliseconds =
                    (Time.realtimeSinceStartupAsDouble -
                     frameStart) *
                    1000.0;

                if (elapsedMilliseconds >=
                    softTimeBudgetMilliseconds)
                {
                    break;
                }
            }
        }

        private void OnValidate()
        {
            maxRebuildsPerFrame =
                Mathf.Max(
                    1,
                    maxRebuildsPerFrame);

            softTimeBudgetMilliseconds =
                Mathf.Max(
                    0.1f,
                    softTimeBudgetMilliseconds);
        }
    }
}
