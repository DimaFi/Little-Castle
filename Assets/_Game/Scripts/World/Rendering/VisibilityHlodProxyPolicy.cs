namespace LittleCastle.Rendering
{
    public enum HlodPresentationMode : byte
    {
        Near = 0,
        Proxy = 1,
        Hidden = 2
    }

    /// <summary>
    /// Pure policy used by HLOD presentation receivers and tests.
    /// It contains no authoritative gameplay state.
    /// </summary>
    public static class VisibilityHlodProxyPolicy
    {
        public static HlodPresentationMode Evaluate(
            VisibilityQualityTier tier,
            VisibilityQualityTier proxyStartsAtTier,
            bool hideInHiddenTier)
        {
            if (hideInHiddenTier &&
                tier == VisibilityQualityTier.Hidden)
            {
                return HlodPresentationMode.Hidden;
            }

            if ((byte)tier >=
                (byte)proxyStartsAtTier)
            {
                return HlodPresentationMode.Proxy;
            }

            return HlodPresentationMode.Near;
        }
    }
}
