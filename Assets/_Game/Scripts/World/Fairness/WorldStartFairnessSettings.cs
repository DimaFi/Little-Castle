using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class StartResourceRequirement
    {
        public ResourceKind resourceKind = ResourceKind.Stone;

        [Min(1f)]
        public float searchRadius = 320f;

        [Min(0)]
        public int minimumEffectiveCapacity = 150;

        [Min(1)]
        public int targetEffectiveCapacity = 500;

        [Min(0f)]
        public float weight = 1f;
    }

    /// <summary>
    /// Balance policy for deterministic player-start selection.
    ///
    /// The planner does not inject resources around players. It evaluates the
    /// already generated random world and accepts only comparable viable starts.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldStartFairnessSettings",
        menuName = "Little Castle/World/World Start Fairness Settings")]
    public sealed class WorldStartFairnessSettings : ScriptableObject
    {
        [Header("Candidate search")]
        [Min(32f)]
        [SerializeField] private float candidateSpacing = 192f;

        [Min(0f)]
        [SerializeField] private float playableEdgeMargin = 160f;

        [Min(8)]
        [SerializeField] private int maximumCandidatesToEvaluate = 512;

        [Header("Buildable start area")]
        [Min(8f)]
        [SerializeField] private float buildAreaRadius = 70f;

        [Range(0f, 45f)]
        [SerializeField] private float maximumCenterSlope = 10f;

        [Range(0f, 45f)]
        [SerializeField] private float maximumBuildAreaSlope = 16f;

        [Min(0f)]
        [SerializeField] private float maximumBuildAreaHeightRange = 12f;

        [Header("Wood access")]
        [Min(16f)]
        [SerializeField] private float forestSampleRadius = 260f;

        [Range(0f, 1f)]
        [SerializeField] private float minimumAverageForestDensity = 0.12f;

        [Header("Strategic resources")]
        [SerializeField]
        private List<StartResourceRequirement> resourceRequirements =
            new List<StartResourceRequirement>
            {
                new StartResourceRequirement
                {
                    resourceKind = ResourceKind.Stone,
                    searchRadius = 360f,
                    minimumEffectiveCapacity = 120,
                    targetEffectiveCapacity = 500,
                    weight = 1f
                },
                new StartResourceRequirement
                {
                    resourceKind = ResourceKind.IronOre,
                    searchRadius = 520f,
                    minimumEffectiveCapacity = 80,
                    targetEffectiveCapacity = 350,
                    weight = 0.8f
                }
            };

        [Header("Start separation")]
        [Tooltip(
            "Minimum start distance is derived from sqrt(playableArea / players) " +
            "and multiplied by this value, so the same policy scales with map size.")]
        [Range(0.2f, 1.5f)]
        [SerializeField] private float minimumSeparationMultiplier = 0.62f;

        [Header("Scoring")]
        [Min(0f)]
        [SerializeField] private float terrainWeight = 1.2f;

        [Min(0f)]
        [SerializeField] private float forestWeight = 1f;

        [Min(0f)]
        [SerializeField] private float resourceWeight = 1.5f;

        [Min(0f)]
        [SerializeField] private float separationWeight = 2f;

        [Min(0f)]
        [SerializeField] private float qualityWeight = 1f;

        [Header("Seed acceptance")]
        [Tooltip(
            "Maximum allowed difference between the best and worst selected " +
            "normalized start score. A failing seed should be rerolled before match start.")]
        [Range(0f, 1f)]
        [SerializeField] private float maximumAcceptedScoreSpread = 0.28f;

        public float CandidateSpacing => Mathf.Max(32f, candidateSpacing);
        public float PlayableEdgeMargin => Mathf.Max(0f, playableEdgeMargin);
        public int MaximumCandidatesToEvaluate => Mathf.Max(8, maximumCandidatesToEvaluate);
        public float BuildAreaRadius => Mathf.Max(8f, buildAreaRadius);
        public float MaximumCenterSlope => Mathf.Clamp(maximumCenterSlope, 0f, 45f);
        public float MaximumBuildAreaSlope => Mathf.Clamp(maximumBuildAreaSlope, 0f, 45f);
        public float MaximumBuildAreaHeightRange => Mathf.Max(0f, maximumBuildAreaHeightRange);
        public float ForestSampleRadius => Mathf.Max(16f, forestSampleRadius);
        public float MinimumAverageForestDensity => Mathf.Clamp01(minimumAverageForestDensity);
        public IReadOnlyList<StartResourceRequirement> ResourceRequirements => resourceRequirements;
        public float MinimumSeparationMultiplier => Mathf.Max(0.2f, minimumSeparationMultiplier);
        public float TerrainWeight => Mathf.Max(0f, terrainWeight);
        public float ForestWeight => Mathf.Max(0f, forestWeight);
        public float ResourceWeight => Mathf.Max(0f, resourceWeight);
        public float SeparationWeight => Mathf.Max(0f, separationWeight);
        public float QualityWeight => Mathf.Max(0f, qualityWeight);
        public float MaximumAcceptedScoreSpread => Mathf.Clamp01(maximumAcceptedScoreSpread);
    }
}
