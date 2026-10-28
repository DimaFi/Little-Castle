using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Serializable, pure-data controls for the optional concept-world
    /// landform sampler. Frequencies are derived from one regional scale so
    /// the authored controls describe silhouettes instead of noise internals.
    /// </summary>
    [Serializable]
    public sealed class StylizedLandformSettings
    {
        [Header("World silhouette")]
        [Tooltip("Average elevation of the broad meadow floor in metres.")]
        [SerializeField] private float baseHeight = 3f;

        [Tooltip(
            "Approximate width of a broad meadow, hill belt or highland " +
            "region. Larger values produce longer uninterrupted landforms.")]
        [Min(256f)]
        [SerializeField] private float regionSize = 960f;

        [Tooltip(
            "How much of the world prefers calm meadow. This changes region " +
            "selection; it does not flatten individual building footprints.")]
        [Range(0.35f, 0.8f)]
        [SerializeField] private float plainCoverage = 0.62f;

        [Header("Meadows and hills")]
        [Tooltip("Small, long-wave height variation retained in meadow regions.")]
        [Range(0f, 4f)]
        [SerializeField] private float meadowUndulation = 1.35f;

        [Tooltip("Height of the rounded middle-distance hill belts.")]
        [Range(0f, 24f)]
        [SerializeField] private float hillHeight = 9.5f;

        [Header("Selected highlands")]
        [Tooltip(
            "Approximate share of regional selectors eligible for highlands. " +
            "Highlands remain clustered instead of appearing everywhere.")]
        [Range(0.08f, 0.35f)]
        [SerializeField] private float highlandCoverage = 0.18f;

        [Tooltip("Maximum broad uplift contributed by selected highlands.")]
        [Range(8f, 70f)]
        [SerializeField] private float highlandHeight = 36f;

        [Header("Rock terraces")]
        [Tooltip(
            "Share of selected highland shoulders that can become stepped " +
            "scarps. Zero still leaves smooth highlands.")]
        [Range(0f, 0.8f)]
        [SerializeField] private float scarpCoverage = 0.42f;

        [Tooltip(
            "Blend from smooth highlands to discrete, smoothly bounded rock " +
            "terraces in selected scarp regions.")]
        [Range(0f, 1f)]
        [SerializeField] private float terraceStrength = 0.82f;

        [Tooltip("Vertical spacing between major highland terrace shelves.")]
        [Range(2f, 12f)]
        [SerializeField] private float terraceStepHeight = 5.5f;

        [Header("Surface calmness")]
        [Tooltip(
            "Small surface breakup. It is strongly suppressed on plains and " +
            "only reaches this amplitude in rough regions.")]
        [Range(0f, 2f)]
        [SerializeField] private float detailAmplitude = 0.55f;

        public float BaseHeight =>
            IsFinite(baseHeight) ? baseHeight : 3f;

        public float RegionSize =>
            Mathf.Max(256f, IsFinite(regionSize) ? regionSize : 960f);

        public float PlainCoverage =>
            Mathf.Clamp(
                IsFinite(plainCoverage) ? plainCoverage : 0.62f,
                0.35f,
                0.8f);

        public float MeadowUndulation =>
            Mathf.Clamp(
                IsFinite(meadowUndulation) ? meadowUndulation : 1.35f,
                0f,
                4f);

        public float HillHeight =>
            Mathf.Clamp(
                IsFinite(hillHeight) ? hillHeight : 9.5f,
                0f,
                24f);

        public float HighlandCoverage =>
            Mathf.Clamp(
                IsFinite(highlandCoverage) ? highlandCoverage : 0.18f,
                0.08f,
                0.35f);

        public float HighlandHeight =>
            Mathf.Clamp(
                IsFinite(highlandHeight) ? highlandHeight : 36f,
                8f,
                70f);

        public float ScarpCoverage =>
            Mathf.Clamp(
                IsFinite(scarpCoverage) ? scarpCoverage : 0.42f,
                0f,
                0.8f);

        public float TerraceStrength =>
            Mathf.Clamp01(
                IsFinite(terraceStrength) ? terraceStrength : 0.82f);

        public float TerraceStepHeight =>
            Mathf.Clamp(
                IsFinite(terraceStepHeight) ? terraceStepHeight : 5.5f,
                2f,
                12f);

        public float DetailAmplitude =>
            Mathf.Clamp(
                IsFinite(detailAmplitude) ? detailAmplitude : 0.55f,
                0f,
                2f);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
