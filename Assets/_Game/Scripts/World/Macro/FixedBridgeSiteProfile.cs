using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// C# mirror of the immutable ENV_Bridge_Stone_A v002 source-art contract.
    /// Local +Z is road, local +X is river, and Y=0 is the authored root.
    /// Translation and yaw are the only supported placement transforms.
    /// </summary>
    public static class FixedBridgeSiteProfile
    {
        public const string AssetId = "ENV_Bridge_Stone_A";
        public const string ContractVersion = "v002";

        public const float BridgeLength = 10.8f;
        public const float HalfLength = 5.4f;
        public const float StructuralWidth = 3.6f;
        public const float StructuralHalfWidth = 1.8f;
        public const float CapWidth = 3.78f;
        public const float ClearPathWidth = 2.86f;
        public const float ArchOpeningWidth = 6.5f;
        public const float DeckCrownHeight = 1.05f;
        public const float WaterHeightOffset = -0.85f;

        public const float SupportHalfExtentX = 7f;
        public const float SupportHalfExtentZ = 8f;
        public const float ProtectedCoreHalfExtentX = 5f;
        public const float ProtectedCoreHalfExtentZ = 6.5f;

        public const float SouthRightBannerX = 1.64f;
        public const float SouthRightBannerZ = -5.12f;
        public const float NorthRightBannerX = -1.64f;
        public const float NorthRightBannerZ = 5.12f;

        public static bool IsSupported(WorldBridgeSiteData site)
        {
            return
                site.isFixedSite &&
                site.archetypeId == AssetId &&
                site.contractVersion == ContractVersion;
        }

        public static float DeckHeight(float localZ)
        {
            float clamped =
                Mathf.Min(HalfLength, Mathf.Abs(localZ));

            float value =
                Mathf.Cos(
                    Mathf.PI * clamped /
                    (2f * HalfLength));

            return DeckCrownHeight * value * value;
        }

        public static float ChannelHalfWidth(float localX)
        {
            return
                2.83f +
                0.17f *
                Smooth(2f, 5.5f, Mathf.Abs(localX)) *
                Mathf.Sin(localX * 1.1f + 0.4f);
        }

        public static float TerrainHeight(float localX, float localZ)
        {
            float distance = Mathf.Abs(localZ);
            float shore = ChannelHalfWidth(localX);

            float bed =
                -1.22f +
                0.035f *
                Mathf.Cos(localX * 0.7f) *
                Mathf.Cos(localZ * 1.4f);

            float mound =
                0.09f *
                Mathf.Sin(localX * 1.3f + localZ * 0.8f) *
                Mathf.Sin(localZ * 1.1f) *
                Smooth(3.5f, 4.8f, distance);

            float bank = -0.17f + mound;
            float bankBlend =
                Smooth(
                    shore - 0.28f,
                    shore + 1.25f,
                    distance);

            float height =
                bed * (1f - bankBlend) +
                bank * bankBlend;

            float road =
                (1f - Smooth(
                    1.4f,
                    2.05f,
                    Mathf.Abs(localX))) *
                Smooth(4.3f, 5.4f, distance);

            return height * (1f - road);
        }

        public static float StampWeight(float localX, float localZ)
        {
            return
                (1f - Smooth(
                    ProtectedCoreHalfExtentX,
                    SupportHalfExtentX,
                    Mathf.Abs(localX))) *
                (1f - Smooth(
                    ProtectedCoreHalfExtentZ,
                    SupportHalfExtentZ,
                    Mathf.Abs(localZ)));
        }

        public static Vector2 WorldToLocal(
            Vector2 worldPosition,
            Vector2 siteOrigin,
            float yawDegrees)
        {
            float radians = yawDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            Vector2 delta = worldPosition - siteOrigin;

            return
                new Vector2(
                    cosine * delta.x - sine * delta.y,
                    sine * delta.x + cosine * delta.y);
        }

        public static Vector2 LocalToWorld(
            Vector2 localPosition,
            Vector2 siteOrigin,
            float yawDegrees)
        {
            float radians = yawDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);

            return
                siteOrigin +
                new Vector2(
                    cosine * localPosition.x +
                    sine * localPosition.y,
                    -sine * localPosition.x +
                    cosine * localPosition.y);
        }

        public static void SampleWorld(
            float worldX,
            float worldZ,
            Vector2 siteOrigin,
            float yawDegrees,
            float baseElevation,
            out float targetHeight,
            out float weight)
        {
            Vector2 local =
                WorldToLocal(
                    new Vector2(worldX, worldZ),
                    siteOrigin,
                    yawDegrees);

            targetHeight =
                baseElevation +
                TerrainHeight(local.x, local.y);

            weight = StampWeight(local.x, local.y);
        }

        public static void GetWorldAabbHalfExtents(
            float yawDegrees,
            out float halfExtentX,
            out float halfExtentZ)
        {
            float radians = yawDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Abs(Mathf.Cos(radians));
            float sine = Mathf.Abs(Mathf.Sin(radians));

            halfExtentX =
                cosine * SupportHalfExtentX +
                sine * SupportHalfExtentZ;

            halfExtentZ =
                sine * SupportHalfExtentX +
                cosine * SupportHalfExtentZ;
        }

        private static float Smooth(float minimum, float maximum, float value)
        {
            float t =
                Mathf.Clamp01(
                    (value - minimum) /
                    (maximum - minimum));

            return t * t * (3f - 2f * t);
        }
    }
}
