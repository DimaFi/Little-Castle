using System.Collections;
using System.Linq;
using System.Reflection;
using LittleCastle.Experimental.MountainBorder.Presentation;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LittleCastle.Tests
{
    public sealed class MapAtmospherePresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator OrdinaryStartupOwnsItsPresentationAndRestoresTheCamera()
        {
            var root = new GameObject("Scene-owned map atmosphere test");
            root.SetActive(false);
            var source = ScriptableObject.CreateInstance<SurfacePaintData>();
            var terrain = new Material(Shader.Find("Little Castle/Terrain/LC Terrain"));
            try
            {
                source.mapId = "atmosphere-test";
                source.bounds = new Vector4(-32, -32, 64, 64);
                string originalMap = JsonUtility.ToJson(source);
                var painter = root.AddComponent<MeadowSurfacePainter>();
                painter.source = source;
                painter.terrainMaterial = terrain;
                painter.brushMaterial = terrain;

                var cameraRoot = new GameObject("Map camera");
                cameraRoot.transform.SetParent(root.transform, false);
                var camera = cameraRoot.AddComponent<Camera>();
                camera.enabled = false;
                camera.farClipPlane = 300f;
                camera.fieldOfView = 44f;
                painter.studyCamera = camera;

                var lighting = root.AddComponent<DayNightLightingController>();
                var presentation = root.AddComponent<MapAtmospherePresentation>();
                SetField(presentation, "painter", painter);
                SetField(presentation, "atmosphere", lighting);
                Assert.That(root.GetComponentInChildren<DistantValleyPreview>(true), Is.Null);

                root.SetActive(true);
                yield return null;
                yield return null;
                Assert.That(painter.Session.MapId, Is.EqualTo(source.mapId));
                Assert.That(root.GetComponentInChildren<NearWeatherCloudPreview>(), Is.Not.Null);
                Assert.That(root.GetComponentInChildren<DistantValleyPreview>(), Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<MeshFilter>().Any(
                    filter => filter.sharedMesh && filter.sharedMesh.name == "Distant valley floor"), Is.True);
                Assert.That(root.GetComponentInChildren<LittleCastleOptionalAtmosphereStack>(), Is.Null);
                Assert.That(camera.farClipPlane, Is.EqualTo(960f));
                Assert.That(camera.fieldOfView, Is.EqualTo(44f));

                presentation.enabled = false;
                yield return null;
                Assert.That(root.GetComponentInChildren<DistantValleyPreview>(true), Is.Null);
                Assert.That(camera.farClipPlane, Is.EqualTo(300f));
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(originalMap));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(terrain);
                Object.Destroy(source);
            }
            yield return null;
        }

        static void SetField(MapAtmospherePresentation presentation, string name, Component value) =>
            typeof(MapAtmospherePresentation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(presentation, value);
    }
}
