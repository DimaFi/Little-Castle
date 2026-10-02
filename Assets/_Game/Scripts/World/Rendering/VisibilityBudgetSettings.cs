using System;
using UnityEngine;

namespace LittleCastle.Rendering
{
    public enum VisibilityQualityTier : byte
    {
        Full = 0,
        Balanced = 1,
        Far = 2,
        Hidden = 3
    }

    public interface IVisibilityBudgetReceiver
    {
        void OnVisibilityQualityTierChanged(
            VisibilityQualityTier tier,
            bool cameraVisible);
    }

    [Serializable]
    public struct VisibilityBudgetSettings
    {
        [Header("Full quality")]
        [Range(0.05f, 1f)]
        public float fullQualityViewportRadius;

        [Min(0.01f)]
        public float fullQualityDistance;

        [Header("Distance quality")]
        [Min(0.01f)]
        public float balancedDistance;

        [Min(0.01f)]
        public float farDistance;

        [Header("Camera prewarm")]
        [Min(1f)]
        public float prewarmViewportRadius;

        [Min(0.01f)]
        public float prewarmDistance;

        [Header("Transition stability")]
        [Min(0f)]
        public float downgradeDelaySeconds;

        [Min(0f)]
        public float hiddenDelaySeconds;

        [Min(0f)]
        public float occlusionGraceSeconds;

        [Header("Bounds")]
        [Min(1f)]
        public float boundsPadding;

        [Header("Tier actions")]
        public bool disableAnimatorsWhenHidden;
        public bool disableFarShadows;
        public bool disableFarReceiveShadows;
        public bool configureLodCrossFade;

        public static VisibilityBudgetSettings Default
        {
            get
            {
                return new VisibilityBudgetSettings
                {
                    fullQualityViewportRadius = 0.58f,
                    fullQualityDistance = 58f,
                    balancedDistance = 135f,
                    farDistance = 320f,
                    prewarmViewportRadius = 1.24f,
                    prewarmDistance = 165f,
                    downgradeDelaySeconds = 0.18f,
                    hiddenDelaySeconds = 0.35f,
                    occlusionGraceSeconds = 0.22f,
                    boundsPadding = 1.12f,
                    disableAnimatorsWhenHidden = true,
                    disableFarShadows = true,
                    disableFarReceiveShadows = true,
                    configureLodCrossFade = true
                };
            }
        }

        public VisibilityBudgetSettings Sanitized()
        {
            if (fullQualityDistance <= 0f &&
                balancedDistance <= 0f &&
                farDistance <= 0f &&
                prewarmDistance <= 0f)
            {
                return Default;
            }

            VisibilityBudgetSettings result = this;

            result.fullQualityViewportRadius =
                Mathf.Clamp(
                    result.fullQualityViewportRadius,
                    0.05f,
                    1f);

            result.fullQualityDistance =
                Mathf.Max(
                    0.01f,
                    result.fullQualityDistance);

            result.balancedDistance =
                Mathf.Max(
                    result.fullQualityDistance,
                    result.balancedDistance);

            result.farDistance =
                Mathf.Max(
                    result.balancedDistance,
                    result.farDistance);

            result.prewarmViewportRadius =
                Mathf.Max(
                    1f,
                    result.prewarmViewportRadius);

            result.prewarmDistance =
                Mathf.Clamp(
                    result.prewarmDistance,
                    result.fullQualityDistance,
                    result.farDistance);

            result.downgradeDelaySeconds =
                Mathf.Max(
                    0f,
                    result.downgradeDelaySeconds);

            result.hiddenDelaySeconds =
                Mathf.Max(
                    result.downgradeDelaySeconds,
                    result.hiddenDelaySeconds);

            result.occlusionGraceSeconds =
                Mathf.Max(
                    0f,
                    result.occlusionGraceSeconds);

            result.boundsPadding =
                Mathf.Max(
                    1f,
                    result.boundsPadding);

            return result;
        }
    }
}
