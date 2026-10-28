using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class BridgeCrossingPresentationTests
    {
        private static WorldBridgeSiteData Site(Vector2 position, float yaw, float elevation)
        {
            return new WorldBridgeSiteData(
                8123, 111, 222,
                FixedBridgeSiteProfile.AssetId,
                position, yaw,
                FixedBridgeSiteProfile.BridgeLength,
                elevation,
                FixedBridgeSiteProfile.ContractVersion,
                true);
        }

        [TestCase(0f)]
        [TestCase(37f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void RotatedWorldSockets_RoundTripAgainstAuthoredOffsets(float yaw)
        {
            var site = Site(new Vector2(-96.5f, -33.25f), yaw, 8.25f);
            Assert.That(BridgeSitePresentationUtility.TryGetWorldAnchors(
                site, out Vector3 root, out Vector3 south,
                out Vector3 north, out Vector3 inRiver,
                out Vector3 outRiver, out float waterY), Is.True);

            Assert.That(root, Is.EqualTo(new Vector3(-96.5f, 8.25f, -33.25f)));
            Assert.That(waterY, Is.EqualTo(7.4f).Within(0.00001f));
            Assert.That(Vector3.Distance(south, north),
                Is.EqualTo(FixedBridgeSiteProfile.BridgeLength).Within(0.0001f));
            Assert.That(Vector3.Distance(inRiver, outRiver),
                Is.EqualTo(2f * FixedBridgeSiteProfile.SupportHalfExtentX)
                    .Within(0.0001f));
            Assert.That(south.y, Is.EqualTo(root.y).Within(0.00001f));
            Assert.That(north.y, Is.EqualTo(root.y).Within(0.00001f));

            Vector2 localSouth = FixedBridgeSiteProfile.WorldToLocal(
                new Vector2(south.x, south.z), site.worldPosition, yaw);
            Vector2 localNorth = FixedBridgeSiteProfile.WorldToLocal(
                new Vector2(north.x, north.z), site.worldPosition, yaw);
            Assert.That(localSouth.y, Is.EqualTo(-5.4f).Within(0.0001f));
            Assert.That(localNorth.y, Is.EqualTo(5.4f).Within(0.0001f));
        }

        [Test]
        public void ProtectedWaterSurface_OnlyCoversBridgeChannel()
        {
            var site = Site(new Vector2(-50f, 17f), 90f, 0f);
            Vector2 wet = FixedBridgeSiteProfile.LocalToWorld(
                new Vector2(3f, 0f), site.worldPosition, site.yawDegrees);
            Vector2 dry = FixedBridgeSiteProfile.LocalToWorld(
                new Vector2(3f, 6f), site.worldPosition, site.yawDegrees);
            Assert.That(
                BridgeSitePresentationUtility.IsProtectedWaterSurface(site, wet),
                Is.True);
            Assert.That(
                BridgeSitePresentationUtility.IsProtectedWaterSurface(site, dry),
                Is.False);
            Assert.That(
                BridgeSitePresentationUtility.IsInsideFixedFootprint(site, dry),
                Is.True);
        }

        [Test]
        public void DeckHeight_FollowsCrownAndStopsOutsideWalkableCorridor()
        {
            var site = Site(new Vector2(128f, -128f), -45f, 2f);
            Assert.That(BridgeSitePresentationUtility.TryGetDeckHeightAt(
                site, site.worldPosition, out float crown), Is.True);
            Assert.That(crown,
                Is.EqualTo(2f + FixedBridgeSiteProfile.DeckCrownHeight)
                    .Within(0.0001f));

            Vector2 end = FixedBridgeSiteProfile.LocalToWorld(
                new Vector2(0f, 5.4f), site.worldPosition, site.yawDegrees);
            Assert.That(BridgeSitePresentationUtility.TryGetDeckHeightAt(
                site, end, out float endpoint), Is.True);
            Assert.That(endpoint, Is.EqualTo(2f).Within(0.0001f));

            Vector2 outside = FixedBridgeSiteProfile.LocalToWorld(
                new Vector2(2f, 0f), site.worldPosition, site.yawDegrees);
            Assert.That(BridgeSitePresentationUtility.TryGetDeckHeightAt(
                site, outside, out _), Is.False);
        }

        [Test]
        public void UnsupportedSite_DoesNotGeneratePhantomWaterSockets()
        {
            var site = Site(new Vector2(0f, 0f), 0f, 1f);
            site.contractVersion = "v001";
            Assert.That(BridgeSitePresentationUtility.TryGetWorldAnchors(
                site, out _, out _, out _, out _, out _, out _),
                Is.False);
            Assert.That(
                BridgeSitePresentationUtility.IsProtectedWaterSurface(
                    site, Vector2.zero),
                Is.False);
        }
    }
}
