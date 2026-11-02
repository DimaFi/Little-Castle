using LittleCastle.World.Weather;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class RainShelterVolumeTests
    {
        private static RainShelterVolume.Shape Roof(float edge = 2f, float protection = .98f)
        {
            return new RainShelterVolume.Shape
            {
                center = Vector2.zero, right = Vector2.right, forward = Vector2.up,
                halfSize = new Vector2(5f, 3f), roofY = 6f, edgeFade = edge,
                verticalFade = .9f, protection = protection, irregularity = 0f
            };
        }

        [Test]
        public void DeepUnderRoofIsDryAndAboveRoofStillGetsRain()
        {
            var roof = Roof();
            Assert.AreEqual(.98f, RainShelterVolume.CoverageAt(new Vector3(0, 0, 0), roof), .0001f);
            Assert.AreEqual(0f, RainShelterVolume.CoverageAt(new Vector3(0, 8, 0), roof), .0001f);
            Assert.Greater(RainShelterVolume.CoverageAt(new Vector3(0, 5.8f, 0), roof), 0f);
            Assert.Less(RainShelterVolume.CoverageAt(new Vector3(0, 5.8f, 0), roof), .98f);
        }

        [Test]
        public void RoofEdgeIsSmoothNotAnAbruptBinaryLine()
        {
            var roof = Roof(2f);
            float deep = RainShelterVolume.HorizontalCoverage(0f, 0f, roof);
            float inner = RainShelterVolume.HorizontalCoverage(4f, 0f, roof);
            float outside = RainShelterVolume.HorizontalCoverage(5.2f, 0f, roof);
            Assert.AreEqual(.98f, deep, .0001f);
            Assert.Greater(inner, .05f);
            Assert.Less(inner, deep);
            Assert.AreEqual(0f, outside, .0001f);
        }

        [Test]
        public void RoofRotationUsesItsLocalFootprint()
        {
            var roof = Roof();
            roof.right = Vector2.up;
            roof.forward = Vector2.left;
            Assert.Greater(RainShelterVolume.HorizontalCoverage(0, 4, roof), 0);
            Assert.AreEqual(0f, RainShelterVolume.HorizontalCoverage(4f, 0, roof), .0001f);
        }

        [Test]
        public void ProtectionAllowsSomeRainUnderAwnings()
        {
            var roof = Roof(protection: .72f);
            Assert.AreEqual(.72f, RainShelterVolume.CoverageAt(new Vector3(0, 0, 0), roof), .0001f);
        }

        [Test]
        public void PrefabMarkerTransformCanMoveWithoutSceneWideController()
        {
            var root = new GameObject("Rain shelter test prefab");
            try
            {
                var marker = root.AddComponent<RainShelterVolume>();
                marker.halfWidth = 4f;
                marker.halfDepth = 2f;
                marker.transform.position = new Vector3(10, 7, 4);
                Assert.Greater(RainShelterVolume.CoverageAt(new Vector3(10, 0, 4)), .9f);
                Assert.AreEqual(0f, RainShelterVolume.CoverageAt(new Vector3(100, 0, 4), marker.WorldShape));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
