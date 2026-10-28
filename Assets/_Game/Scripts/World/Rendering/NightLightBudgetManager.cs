using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Selects a small nearby subset of NightLightEmitter instances that may
    /// consume realtime Point/Spot light cost.
    ///
    /// Emission and cheap ground-pool visuals are separate, cheaper LOD tiers.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class NightLightBudgetManager : MonoBehaviour
    {
        private readonly List<Candidate> candidates =
            new List<Candidate>(128);

        private readonly HashSet<NightLightEmitter> selected =
            new HashSet<NightLightEmitter>();

        private readonly List<NightLightEmitter> presentationScratch =
            new List<NightLightEmitter>(64);

        [SerializeField]
        private StylizedLightingGlobals lightingGlobals;

        [SerializeField]
        private Camera targetCamera;

        [Header("Realtime light budget")]
        [SerializeField]
        [Min(0)]
        private int maxActiveRealtimeLights = 12;

        [SerializeField]
        [Min(1f)]
        private float globalMaxDistance = 78f;

        [SerializeField]
        [Min(0.02f)]
        private float evaluationInterval = 0.18f;

        [Tooltip(
            "Already selected lights receive a small distance advantage so " +
            "the budget does not visibly chatter while the camera moves.")]
        [SerializeField]
        [Range(0.5f, 1f)]
        private float selectionRetentionDistanceMultiplier = 0.84f;

        private float nextEvaluationTime;

        public int RegisteredEmitterCount { get; private set; }
        public int CandidateCount { get; private set; }
        public int ActiveRealtimeLightCount { get; private set; }
        public int FadingRealtimeLightCount { get; private set; }
        public int VisiblePoolCount { get; private set; }

        private struct Candidate
        {
            public NightLightEmitter emitter;
            public float effectiveDistanceSquared;
            public float actualDistanceSquared;
        }

        private sealed class CandidateComparer :
            IComparer<Candidate>
        {
            public static readonly CandidateComparer Instance =
                new CandidateComparer();

            public int Compare(
                Candidate a,
                Candidate b)
            {
                int priority =
                    b.emitter.Priority.CompareTo(
                        a.emitter.Priority);

                if (priority != 0)
                    return priority;

                int distance =
                    a.effectiveDistanceSquared.CompareTo(
                        b.effectiveDistanceSquared);

                if (distance != 0)
                    return distance;

                return
                    a.actualDistanceSquared.CompareTo(
                        b.actualDistanceSquared);
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >=
                nextEvaluationTime)
            {
                nextEvaluationTime =
                    Time.unscaledTime +
                    evaluationInterval;

                EvaluateBudget();
            }

            TickPresentationLights(
                Time.unscaledDeltaTime);
        }

        [ContextMenu("Evaluate Night Light Budget")]
        public void EvaluateBudget()
        {
            ResolveReferences();

            float nightAmount =
                lightingGlobals != null
                    ? lightingGlobals.NightAmount
                    : 0f;

            candidates.Clear();
            selected.Clear();

            RegisteredEmitterCount = 0;
            CandidateCount = 0;
            VisiblePoolCount = 0;

            Vector3 cameraPosition =
                targetCamera != null
                    ? targetCamera.transform.position
                    : Vector3.zero;

            float managerDistanceSquared =
                globalMaxDistance *
                globalMaxDistance;

            float retentionMultiplierSquared =
                selectionRetentionDistanceMultiplier *
                selectionRetentionDistanceMultiplier;

            foreach (
                NightLightEmitter emitter
                in NightLightEmitter.ActiveEmitters)
            {
                if (emitter == null ||
                    !emitter.isActiveAndEnabled)
                {
                    continue;
                }

                RegisteredEmitterCount++;

                Vector3 delta =
                    emitter.WorldPosition -
                    cameraPosition;

                float distanceSquared =
                    delta.sqrMagnitude;

                float distance =
                    Mathf.Sqrt(
                        distanceSquared);

                emitter.SetDistancePresentation(
                    distance,
                    nightAmount);

                if (emitter.PoolVisible)
                    VisiblePoolCount++;

                if (emitter.TargetLight == null)
                    continue;

                float emitterDistance =
                    Mathf.Min(
                        globalMaxDistance,
                        emitter.MaxDistance);

                float allowedDistanceSquared =
                    Mathf.Min(
                        managerDistanceSquared,
                        emitterDistance *
                        emitterDistance);

                if (nightAmount <
                        emitter.MinimumNightAmount ||
                    distanceSquared >
                        allowedDistanceSquared)
                {
                    continue;
                }

                float effectiveDistanceSquared =
                    distanceSquared;

                if (emitter.BudgetActive)
                {
                    effectiveDistanceSquared *=
                        retentionMultiplierSquared;
                }

                candidates.Add(
                    new Candidate
                    {
                        emitter = emitter,
                        effectiveDistanceSquared =
                            effectiveDistanceSquared,
                        actualDistanceSquared =
                            distanceSquared
                    });
            }

            CandidateCount =
                candidates.Count;

            candidates.Sort(
                CandidateComparer.Instance);

            int activeCount =
                Mathf.Min(
                    maxActiveRealtimeLights,
                    candidates.Count);

            for (int i = 0;
                 i < activeCount;
                 i++)
            {
                selected.Add(
                    candidates[i].emitter);
            }

            foreach (
                NightLightEmitter emitter
                in NightLightEmitter.ActiveEmitters)
            {
                if (emitter == null)
                    continue;

                emitter.SetBudgetActive(
                    selected.Contains(
                        emitter),
                    nightAmount);
            }

            ActiveRealtimeLightCount =
                selected.Count;
        }

        private void TickPresentationLights(
            float unscaledDeltaTime)
        {
            presentationScratch.Clear();

            foreach (
                NightLightEmitter emitter
                in NightLightEmitter.ActivePresentationEmitters)
            {
                if (emitter != null)
                {
                    presentationScratch.Add(
                        emitter);
                }
            }

            int fading = 0;

            for (int i = 0;
                 i < presentationScratch.Count;
                 i++)
            {
                NightLightEmitter emitter =
                    presentationScratch[i];

                if (emitter == null)
                    continue;

                emitter.TickPresentation(
                    unscaledDeltaTime);

                if (!emitter.BudgetActive &&
                    emitter.CurrentStrength > 0.001f)
                {
                    fading++;
                }
            }

            FadingRealtimeLightCount =
                fading;
        }

        private void ResolveReferences()
        {
            if (lightingGlobals == null)
            {
                lightingGlobals =
                    FindAnyObjectByType<
                        StylizedLightingGlobals>();
            }

            if (targetCamera == null)
            {
                targetCamera =
                    Camera.main;
            }
        }

        private void OnDisable()
        {
            foreach (
                NightLightEmitter emitter
                in NightLightEmitter.ActiveEmitters)
            {
                if (emitter == null)
                    continue;

                emitter.ForceOffImmediate();

                if (emitter.PoolVisual != null)
                {
                    emitter.PoolVisual.SetPresentationVisible(
                        false);
                }
            }

            selected.Clear();
            presentationScratch.Clear();

            ActiveRealtimeLightCount = 0;
            FadingRealtimeLightCount = 0;
            VisiblePoolCount = 0;
        }

        private void OnValidate()
        {
            maxActiveRealtimeLights =
                Mathf.Max(
                    0,
                    maxActiveRealtimeLights);

            globalMaxDistance =
                Mathf.Max(
                    1f,
                    globalMaxDistance);

            evaluationInterval =
                Mathf.Max(
                    0.02f,
                    evaluationInterval);

            selectionRetentionDistanceMultiplier =
                Mathf.Clamp(
                    selectionRetentionDistanceMultiplier,
                    0.5f,
                    1f);
        }
    }
}
