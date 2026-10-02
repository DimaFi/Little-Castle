using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Selects a small nearby subset of NightLightEmitter instances that are
    /// allowed to own realtime Point/Spot lights.
    ///
    /// The visible emissive material is not budgeted here; only expensive
    /// realtime light contribution is.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class NightLightBudgetManager : MonoBehaviour
    {
        private readonly List<Candidate> candidates =
            new List<Candidate>(128);

        private readonly HashSet<NightLightEmitter> selected =
            new HashSet<NightLightEmitter>();

        [SerializeField]
        private StylizedLightingGlobals lightingGlobals;

        [SerializeField]
        private Camera targetCamera;

        [SerializeField]
        [Min(0)]
        private int maxActiveRealtimeLights = 24;

        [SerializeField]
        [Min(1f)]
        private float globalMaxDistance = 95f;

        [SerializeField]
        [Min(0.02f)]
        private float evaluationInterval = 0.20f;

        private float nextEvaluationTime;

        private struct Candidate
        {
            public NightLightEmitter emitter;
            public float distanceSquared;
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

                return
                    a.distanceSquared.CompareTo(
                        b.distanceSquared);
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime <
                nextEvaluationTime)
            {
                return;
            }

            nextEvaluationTime =
                Time.unscaledTime +
                evaluationInterval;

            EvaluateBudget();
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

            Vector3 cameraPosition =
                targetCamera != null
                    ? targetCamera.transform.position
                    : Vector3.zero;

            float managerDistanceSquared =
                globalMaxDistance *
                globalMaxDistance;

            foreach (
                NightLightEmitter emitter
                in NightLightEmitter.ActiveEmitters)
            {
                if (emitter == null ||
                    !emitter.isActiveAndEnabled ||
                    emitter.TargetLight == null)
                {
                    continue;
                }

                Vector3 delta =
                    emitter.WorldPosition -
                    cameraPosition;

                float distanceSquared =
                    delta.sqrMagnitude;

                float emitterDistance =
                    Mathf.Min(
                        globalMaxDistance,
                        emitter.MaxDistance);

                float allowedDistanceSquared =
                    Mathf.Min(
                        managerDistanceSquared,
                        emitterDistance *
                        emitterDistance);

                if (nightAmount >=
                        emitter.MinimumNightAmount &&
                    distanceSquared <=
                        allowedDistanceSquared)
                {
                    candidates.Add(
                        new Candidate
                        {
                            emitter = emitter,
                            distanceSquared =
                                distanceSquared
                        });
                }
            }

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
        }

        private void ResolveReferences()
        {
            if (lightingGlobals == null)
            {
                lightingGlobals =
                    FindFirstObjectByType<
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
                if (emitter != null)
                {
                    emitter.SetBudgetActive(
                        false,
                        0f);
                }
            }
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
        }
    }
}
