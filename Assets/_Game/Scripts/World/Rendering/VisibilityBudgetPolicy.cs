namespace LittleCastle.Rendering
{
    public struct VisibilityBudgetEvaluationInput
    {
        public bool cullingStateKnown;
        public bool cullingVisible;
        public bool proactiveVisible;
        public bool insideView;
        public bool insidePrewarm;
        public float secondsSinceVisible;
        public float viewportEdge;
        public float surfaceDistance;
        public bool forceFullQuality;
    }

    public static class VisibilityBudgetPolicy
    {
        public static VisibilityQualityTier Evaluate(
            VisibilityBudgetSettings rawSettings,
            VisibilityBudgetEvaluationInput input)
        {
            VisibilityBudgetSettings settings =
                rawSettings.Sanitized();

            bool visible =
                input.insideView &&
                (!input.cullingStateKnown ||
                 input.cullingVisible ||
                 input.proactiveVisible ||
                 input.secondsSinceVisible <=
                    settings.occlusionGraceSeconds);

            if (visible)
            {
                if (input.forceFullQuality)
                    return VisibilityQualityTier.Full;

                if (input.viewportEdge <=
                        settings.fullQualityViewportRadius &&
                    input.surfaceDistance <=
                        settings.fullQualityDistance)
                {
                    return VisibilityQualityTier.Full;
                }

                if (input.surfaceDistance <=
                    settings.balancedDistance)
                {
                    return VisibilityQualityTier.Balanced;
                }

                return VisibilityQualityTier.Far;
            }

            if (input.insidePrewarm &&
                input.surfaceDistance <=
                    settings.prewarmDistance)
            {
                if (input.surfaceDistance <=
                    settings.fullQualityDistance)
                {
                    return VisibilityQualityTier.Balanced;
                }

                return VisibilityQualityTier.Far;
            }

            return VisibilityQualityTier.Hidden;
        }
    }
}
