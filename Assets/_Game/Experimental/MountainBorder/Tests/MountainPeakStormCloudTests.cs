using LittleCastle.Experimental.MountainBorder.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Experimental.MountainBorder.Tests
{
    public sealed class MountainPeakStormCloudTests
    {
        [Test]
        public void RidgeAnchorsOnlyUseElevatedSourceMeshVertices()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-40, 0, 0), new Vector3(-30, 38, 0),
                new Vector3(-20, 64, 5), new Vector3(-10, 17, 0),
                new Vector3(0, 61, 0), new Vector3(10, 34, 4),
                new Vector3(20, 57, 5), new Vector3(30, 25, 0),
                new Vector3(40, 0, 0)
            };
            try
            {
                var anchors = MountainPeakCloudPresentation.PickRidgeAnchors(mesh, 9);
                Assert.That(anchors, Is.Not.Empty);
                Assert.That(anchors.Length, Is.LessThanOrEqualTo(9));
                foreach (var peak in anchors)
                    Assert.That(peak.y, Is.GreaterThanOrEqualTo(31f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void PeakCloudsSkipFlatMeshAndMissingSources()
        {
            Assert.That(MountainPeakCloudPresentation.PickRidgeAnchors(null),
                Is.Empty);
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-30,0,0),new Vector3(30,0,0),
                new Vector3(0,0,30)
            };
            try
            {
                Assert.That(MountainPeakCloudPresentation.PickRidgeAnchors(mesh),
                    Is.Empty);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void OptionalCloudShaderIsAvailableInResourcesForBuilds()
        {
            var shader = Resources.Load<Shader>("MapAuthoring/LC_MountainPeakClouds");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(material.HasProperty("_CloudWidth"), Is.True);
                Assert.That(material.HasProperty("_MinimumDistance"), Is.True);
                Assert.That(material.HasProperty("_FrontOffset"), Is.True);
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void PeakCloudProfileDefaultsKeepGeometryAwayFromGameplayCamera()
        {
            var profile = ScriptableObject.CreateInstance<MountainBorderProfile>();
            try
            {
                Assert.That(profile.enablePeakStormClouds, Is.True);
                Assert.That(profile.peakCloudMinimumDistance, Is.GreaterThan(75f));
                Assert.That(profile.peakCloudCount, Is.InRange(3,16));
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
