using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapEnvironmentPresentationTests
    {
        static Rect Bounds(MapEnvironmentPresentation presentation, Camera camera) =>
            (Rect)typeof(MapEnvironmentPresentation).GetMethod("CalculateResidentBounds",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presentation, new object[] { camera });

        static Rect WeatherBounds(MapEnvironmentPresentation presentation) =>
            (Rect)typeof(MapEnvironmentPresentation).GetMethod("CalculateWeatherBounds",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presentation, null);

        static MapEditSession Session(Vector4 bounds) => new MapEditSession(new MapAuthoringDocument
            { mapId = "environment-presentation", bounds = bounds });

        static Camera Camera(GameObject owner, Vector3 position)
        {
            var camera = owner.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
            camera.fieldOfView = 60f;
            camera.aspect = 1.6f;
            camera.farClipPlane = 10000f;
            return camera;
        }

        static void AssertContainsFootprint(Rect bounds, Camera camera, float groundHeight)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
            for (int corner = 0; corner < 4; corner++)
            {
                Ray ray = camera.ViewportPointToRay(new Vector3(corner & 1, corner >> 1, 0f));
                Assert.That(plane.Raycast(ray, out float distance), Is.True);
                Vector3 point = ray.GetPoint(distance);
                Assert.That(point.x, Is.InRange(bounds.xMin - .01f, bounds.xMax + .01f));
                Assert.That(point.z, Is.InRange(bounds.yMin - .01f, bounds.yMax + .01f));
            }
        }

        [Test]
        public void PerspectiveZoomOutCoversAllGroundCornersAndKeepsWeatherLocal()
        {
            var owner = new GameObject("Environment camera test");
            using (var presentation = new MapEnvironmentPresentation(Session(new Vector4(-4096f, -4096f, 8192f, 8192f))))
            try
            {
                var camera = Camera(owner, new Vector3(17f, 40f, 23f));
                Rect near = Bounds(presentation, camera);
                Assert.That(near.size, Is.EqualTo(new Vector2(256f, 256f)));
                camera.transform.position = new Vector3(17f, 600f, 23f);
                Rect far = Bounds(presentation, camera);
                Assert.That(far.width, Is.GreaterThan(near.width));
                Assert.That(far.height, Is.GreaterThan(near.height));
                AssertContainsFootprint(far, camera, presentation.GroundFocus.y);
                Rect weather = WeatherBounds(presentation);
                Assert.That(weather.width, Is.EqualTo(256f));
                Assert.That(weather.height, Is.EqualTo(256f));
                Assert.That(weather.Contains(new Vector2(presentation.GroundFocus.x, presentation.GroundFocus.z)), Is.True);
                camera.transform.position += new Vector3(.01f, .01f, .01f);
                camera.fieldOfView += .001f;
                Assert.That(Bounds(presentation, camera), Is.EqualTo(far));
                camera.fieldOfView = 90f;
                Rect wide = Bounds(presentation, camera);
                Assert.That(wide.width, Is.GreaterThan(far.width));
                AssertContainsFootprint(wide, camera, presentation.GroundFocus.y);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void SmallNegativeMapAlwaysUsesItsCompleteFiniteBounds()
        {
            var owner = new GameObject("Environment small map test");
            var map = new Vector4(-320f, -240f, 128f, 96f);
            using (var presentation = new MapEnvironmentPresentation(Session(map)))
            try
            {
                var camera = Camera(owner, new Vector3(-260f, 600f, -200f));
                var expected = new Rect(map.x, map.y, map.z, map.w);
                Assert.That(Bounds(presentation, camera), Is.EqualTo(expected));
                Assert.That(WeatherBounds(presentation), Is.EqualTo(expected));
                Assert.That(Bounds(presentation, null), Is.EqualTo(expected));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void NegativeMapEdgeClipsExpandedCoverageAndWeatherToMap()
        {
            var owner = new GameObject("Environment finite edge test");
            var map = new Vector4(-8192f, -8192f, 4096f, 4096f);
            using (var presentation = new MapEnvironmentPresentation(Session(map)))
            try
            {
                var camera = Camera(owner, new Vector3(-8170f, 600f, -8160f));
                Rect bounds = Bounds(presentation, camera);
                Assert.That(bounds.xMin, Is.EqualTo(map.x));
                Assert.That(bounds.yMin, Is.EqualTo(map.y));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(map.x + map.z));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(map.y + map.w));
                Assert.That(bounds.width, Is.GreaterThan(256f));
                Rect weather = WeatherBounds(presentation);
                Assert.That(weather.xMin, Is.EqualTo(map.x));
                Assert.That(weather.yMin, Is.EqualTo(map.y));
                Assert.That(weather.size, Is.EqualTo(new Vector2(256f, 256f)));
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
