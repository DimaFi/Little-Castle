using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Read-only geometry/placement contract for the authored stone crossing.
    /// Presentation systems use these world-space anchors rather than
    /// duplicating offsets or altering authoritative bridge data.
    /// Does not create meshes, terrain, water or scene objects.
    /// </summary>
    public static class BridgeSitePresentationUtility
    {
        public static bool TryGetWorldAnchors(
            WorldBridgeSiteData site,
            out Vector3 root,
            out Vector3 southRoadSocket,
            out Vector3 northRoadSocket,
            out Vector3 riverIn,
            out Vector3 riverOut,
            out float waterY)
        {
            root = default;
            southRoadSocket = default;
            northRoadSocket = default;
            riverIn = default;
            riverOut = default;
            waterY = 0f;

            if (!FixedBridgeSiteProfile.IsSupported(site) ||
                !IsFinite(site.worldPosition.x) ||
                !IsFinite(site.worldPosition.y) ||
                !IsFinite(site.baseElevation) ||
                !IsFinite(site.yawDegrees))
                return false;

            root = new Vector3(
                site.worldPosition.x, site.baseElevation,
                site.worldPosition.y);
            waterY = site.baseElevation +
                FixedBridgeSiteProfile.WaterHeightOffset;

            southRoadSocket = ToWorld(
                site, new Vector2(0f, -FixedBridgeSiteProfile.HalfLength),
                site.baseElevation);
            northRoadSocket = ToWorld(
                site, new Vector2(0f, FixedBridgeSiteProfile.HalfLength),
                site.baseElevation);

            riverIn = ToWorld(
                site, new Vector2(-FixedBridgeSiteProfile.SupportHalfExtentX, 0f),
                waterY);
            riverOut = ToWorld(
                site, new Vector2(FixedBridgeSiteProfile.SupportHalfExtentX, 0f),
                waterY);
            return true;
        }

        public static Vector3 ToWorld(
            WorldBridgeSiteData site,
            Vector2 localXZ,
            float worldY)
        {
            Vector2 mapped = FixedBridgeSiteProfile.LocalToWorld(
                localXZ, site.worldPosition, site.yawDegrees);
            return new Vector3(mapped.x, worldY, mapped.y);
        }

        public static bool IsProtectedWaterSurface(
            WorldBridgeSiteData site,
            Vector2 worldXZ,
            float seamPadding = 0f)
        {
            if (!FixedBridgeSiteProfile.IsSupported(site))
                return false;

            Vector2 local = FixedBridgeSiteProfile.WorldToLocal(
                worldXZ, site.worldPosition, site.yawDegrees);

            float padding = Mathf.Clamp(seamPadding, 0f, 1f);
            return
                Mathf.Abs(local.x) <= FixedBridgeSiteProfile.SupportHalfExtentX + padding &&
                Mathf.Abs(local.y) <=
                    FixedBridgeSiteProfile.ChannelHalfWidth(local.x) + padding;
        }

        public static bool IsInsideFixedFootprint(
            WorldBridgeSiteData site,
            Vector2 worldXZ)
        {
            if (!FixedBridgeSiteProfile.IsSupported(site))
                return false;
            Vector2 local = FixedBridgeSiteProfile.WorldToLocal(
                worldXZ, site.worldPosition, site.yawDegrees);
            return
                Mathf.Abs(local.x) <= FixedBridgeSiteProfile.SupportHalfExtentX &&
                Mathf.Abs(local.y) <= FixedBridgeSiteProfile.SupportHalfExtentZ;
        }

        public static bool TryGetDeckHeightAt(
            WorldBridgeSiteData site, Vector2 worldXZ,
            out float deckWorldY)
        {
            deckWorldY = 0f;
            if (!FixedBridgeSiteProfile.IsSupported(site))
                return false;

            Vector2 local = FixedBridgeSiteProfile.WorldToLocal(
                worldXZ, site.worldPosition, site.yawDegrees);
            if (Mathf.Abs(local.x) >
                    FixedBridgeSiteProfile.ClearPathWidth * 0.5f ||
                Mathf.Abs(local.y) > FixedBridgeSiteProfile.HalfLength)
                return false;

            deckWorldY = site.baseElevation +
                FixedBridgeSiteProfile.DeckHeight(local.y);
            return IsFinite(deckWorldY);
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }
}
