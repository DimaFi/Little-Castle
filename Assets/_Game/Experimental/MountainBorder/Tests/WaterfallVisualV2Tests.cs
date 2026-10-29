using LittleCastle.Experimental.MountainBorder.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder.Tests
{
    public sealed class WaterfallVisualV2Tests
    {
        [TestCase(WaterfallVisualQuality.Low)]
        [TestCase(WaterfallVisualQuality.Medium)]
        [TestCase(WaterfallVisualQuality.High)]
        public void FoundationCoversTheWholeChannelAtEveryStationAndForegroundSheetsOverlap(WaterfallVisualQuality quality)
        {
            using (var source = WaterfallGeometryV1.Build())
            {
                var settings = new WaterfallVisualSettings { quality = quality };
                Mesh mesh = WaterfallVisualEffects.BuildVolumeMesh(source.fall, settings, 728);
                try
                {
                    var uv = mesh.uv;
                    int columns = settings.Budget.radialSegments + 1;
                    int sheetSize = columns * (settings.Budget.rows + 1);
                    for (int r = 0; r <= settings.Budget.rows; r++)
                    {
                        float min = 1f, max = 0f;
                        for (int c = 0; c < columns; c++)
                        {
                            float u = uv[r * columns + c].x;
                            min = Mathf.Min(min, u); max = Mathf.Max(max, u);
                        }
                        Assert.That(min, Is.EqualTo(0f).Within(.0001f));
                        Assert.That(max, Is.EqualTo(1f).Within(.0001f));
                        // The front layers must overlap, not leave separate equally spaced hoses.
                        float previousMax = 0f;
                        for (int sheet = 1; sheet < settings.Budget.strands; sheet++)
                        {
                            min = 1f; max = 0f;
                            for (int c = 0; c < columns; c++)
                            {
                                float u = uv[sheet * sheetSize + r * columns + c].x;
                                min = Mathf.Min(min, u); max = Mathf.Max(max, u);
                            }
                            if (sheet > 1) Assert.That(min, Is.LessThan(previousMax));
                            previousMax = max;
                        }
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        [Test]
        public void RotatingAnExplicitLinkKeepsFoamFootprintsAndSprayTint()
        {
            var template = new Material(Shader.Find("Little Castle/Experimental/Waterfall Volume V2"));
            var mist = new Material(Shader.Find("Little Castle/Experimental/Water Mist V2"));
            using (var source = WaterfallGeometryV1.Build())
            using (var rotated = WaterfallGeometryV1.Between(new Vector3(12f,24.15f,20f), new Vector3(28f,1f,20f)))
            {
                try
                {
                    using (var a = new WaterfallVisualEffects(null, source, new WaterfallVisualSettings(), template, mist))
                    using (var b = new WaterfallVisualEffects(null, rotated, new WaterfallVisualSettings(), template, mist))
                    {
                        Assert.That(b.FoamMesh.triangles.Length, Is.EqualTo(a.FoamMesh.triangles.Length));
                        Assert.That(b.FoamMesh.triangles.Length, Is.GreaterThan(0));
                        foreach (var system in b.Root.GetComponentsInChildren<ParticleSystem>())
                        {
                            Color color = system.main.startColor.color;
                            Assert.That(color.r, Is.EqualTo(1f));
                            Assert.That(color.g, Is.EqualTo(1f));
                            Assert.That(color.b, Is.EqualTo(1f));
                            Assert.That(Vector3.Dot(system.transform.forward, Vector3.up), Is.GreaterThan(.999f));
                        }
                    }
                }
                finally { Object.DestroyImmediate(template); Object.DestroyImmediate(mist); }
            }
        }

        [TestCase(WaterfallVisualQuality.Low)]
        [TestCase(WaterfallVisualQuality.Medium)]
        [TestCase(WaterfallVisualQuality.High)]
        public void CurvedStrandsStayWithinActualWidthAndHaveExposedThickness(WaterfallVisualQuality quality)
        {
            using (var source = WaterfallGeometryV1.Build())
            {
                var settings = new WaterfallVisualSettings { quality = quality, thickness = .48f };
                Mesh mesh = WaterfallVisualEffects.BuildVolumeMesh(source.fall, settings, 728);
                try
                {
                    Assert.That(mesh.triangles.Length / 3, Is.EqualTo(settings.Budget.MaxTriangles));
                    var points = mesh.vertices; var uv = mesh.uv; var stations = mesh.uv2;
                    float minFront = float.MaxValue, maxFront = float.MinValue;
                    for (int i = 0; i < points.Length; i++)
                    {
                        Assert.That(uv[i].x, Is.InRange(-.0001f, 1.0001f), "A strand spilled outside the carved water width.");
                        Assert.That(float.IsNaN(points[i].x) || float.IsNaN(points[i].y) || float.IsNaN(points[i].z), Is.False);
                        // At the midpoint of the vertical curve, its exposed normal points toward -Z.
                        if (Mathf.Abs(stations[i].y - .5f) > .0001f) continue;
                        Vector3 center = new Vector3(-58f, 12.575f, 16f);
                        Vector3 along = new Vector3(0f, -34.725f, -18f).normalized;
                        Vector3 normal = Vector3.Cross(Vector3.right, along).normalized;
                        float front = Vector3.Dot(points[i] - center, normal);
                        minFront = Mathf.Min(minFront, front); maxFront = Mathf.Max(maxFront, front);
                    }
                    Assert.That(minFront, Is.GreaterThan(.05f), "Back of ribbon penetrates source terrain clearance.");
                    Assert.That(maxFront - minFront, Is.GreaterThan(.25f), "Side view requires actual section thickness.");
                    Assert.That(mesh.bounds.min.x, Is.GreaterThanOrEqualTo(source.fall.bounds.min.x - .001f));
                    Assert.That(mesh.bounds.max.x, Is.LessThanOrEqualTo(source.fall.bounds.max.x + .001f));
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        [Test]
        public void GenerationIsSeededWithoutTouchingUnityRandom()
        {
            using (var source = WaterfallGeometryV1.Build())
            {
                var original = source.fall.vertices;
                var state = Random.state;
                var settings = new WaterfallVisualSettings();
                Mesh a = null, b = null, c = null;
                try
                {
                    a = WaterfallVisualEffects.BuildVolumeMesh(source.fall, settings, 81);
                    b = WaterfallVisualEffects.BuildVolumeMesh(source.fall, settings, 81);
                    c = WaterfallVisualEffects.BuildVolumeMesh(source.fall, settings, 82);
                    CollectionAssert.AreEqual(a.vertices, b.vertices);
                    CollectionAssert.AreEqual(a.triangles, b.triangles);
                    CollectionAssert.AreNotEqual(a.vertices, c.vertices);
                    CollectionAssert.AreEqual(original, source.fall.vertices);
                    Assert.That(Random.state, Is.EqualTo(state));
                }
                finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(c); }
            }
        }

        [Test]
        public void QualityHasBoundedGeometryAndParticlesAndOffBuildsNothing()
        {
            using (var geometry = WaterfallGeometryV1.Build())
            {
                int previousTriangles = -1;
                foreach (WaterfallVisualQuality quality in System.Enum.GetValues(typeof(WaterfallVisualQuality)))
                {
                    var settings = new WaterfallVisualSettings { quality = quality };
                    var budget = settings.Budget;
                    Assert.That(budget.MaxTriangles, Is.LessThanOrEqualTo(11200));
                    Assert.That(budget.MaxParticles, Is.LessThanOrEqualTo(176));
                    Assert.That(budget.MaxTriangles, Is.GreaterThan(previousTriangles));
                    previousTriangles = budget.MaxTriangles;
                }
                using (var effects = new WaterfallVisualEffects(null, geometry,
                    new WaterfallVisualSettings { quality = WaterfallVisualQuality.Off }, null, null))
                {
                    Assert.That(effects.VolumeMesh, Is.Null);
                    Assert.That(effects.Root.GetComponentsInChildren<ParticleSystem>().Length, Is.Zero);
                    Assert.That(effects.QueryWetness(geometry.landing), Is.Zero);
                }
            }
        }

        [Test]
        public void OwnedResourcesDisposeAndDistanceStopsParticlesWithoutDestroyingInputs()
        {
            Shader shader = Shader.Find("Little Castle/Experimental/Waterfall Volume V2");
            Shader mistShader = Shader.Find("Little Castle/Experimental/Water Mist V2");
            Assert.That(shader, Is.Not.Null); Assert.That(mistShader, Is.Not.Null);
            var template = new Material(shader); var mist = new Material(mistShader);
            var owner = new GameObject("Waterfall test owner"); var cameraObject = new GameObject("Waterfall test camera");
            owner.transform.SetPositionAndRotation(new Vector3(0f, 0f, 155f), Quaternion.Euler(0f, 27f, 0f));
            WaterfallVisualEffects effects = null;
            using (var geometry = WaterfallGeometryV1.Build())
            {
                try
                {
                    effects = new WaterfallVisualEffects(owner.transform, geometry, new WaterfallVisualSettings(), template, mist);
                    Mesh ownedMesh = effects.VolumeMesh;
                    GameObject ownedRoot = effects.Root;
                    Assert.That(ownedRoot.transform.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(ownedRoot.transform.localRotation, Is.EqualTo(Quaternion.identity));
                    Assert.That(ownedRoot.transform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(Vector3.Distance(ownedRoot.transform.TransformPoint(geometry.landing),
                        owner.transform.TransformPoint(geometry.landing)), Is.LessThan(.0001f));
                    var systems = ownedRoot.GetComponentsInChildren<ParticleSystem>();
                    Assert.That(systems.Length, Is.EqualTo(3));
                    int count = 0;
                    foreach (var system in systems)
                    {
                        count += system.main.maxParticles;
                        Assert.That(system.useAutoRandomSeed, Is.False);
                        Assert.That(system.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.Local));
                        Assert.That(system.collision.enabled, Is.False);
                    }
                    Assert.That(count, Is.EqualTo(114));
                    var camera = cameraObject.AddComponent<Camera>();
                    camera.transform.position = owner.transform.TransformPoint(Vector3.Lerp(geometry.lip,geometry.landing,.5f))
                        + Vector3.forward * 100f;
                    effects.UpdatePresentation(camera, 2f);
                    foreach (var system in systems)
                    {
                        Assert.That(system.emission.rateOverTime.constant, Is.GreaterThan(0f), "Middle distance must retain bounded impact mist.");
                        system.Simulate(.5f, false, true, false);
                    }
                    effects.UpdatePresentation(camera, 3f);
                    foreach (var system in systems)
                        Assert.That(system.isPlaying, Is.True, "Editor simulation must resume even within the same distance tier.");
                    camera.transform.position = Vector3.one * 2000f;
                    effects.UpdatePresentation(camera, 4f);
                    foreach (var system in systems) Assert.That(system.isPlaying, Is.False);
                    Vector3 worldLanding = owner.transform.TransformPoint(geometry.landing);
                    Assert.That(effects.QueryWetness(worldLanding), Is.GreaterThan(.9f));
                    Assert.That(effects.QueryWetness(worldLanding + Vector3.right * 100), Is.Zero);
                    owner.transform.position += Vector3.right * 20f;
                    worldLanding = owner.transform.TransformPoint(geometry.landing);
                    Assert.That(effects.QueryWetness(worldLanding), Is.GreaterThan(.9f));
                    Assert.That(Vector3.Distance(new Vector3(effects.WetnessLanding.x, effects.WetnessLanding.y,
                        effects.WetnessLanding.z), worldLanding), Is.LessThan(.001f));
                    effects.Dispose(); effects.Dispose();
                    Assert.That(effects.IsDisposed, Is.True);
                    Assert.That(!ownedRoot, Is.True); Assert.That(!ownedMesh, Is.True);
                    Assert.That(template && mist && geometry.fall, Is.True);
                }
                finally
                {
                    effects?.Dispose(); Object.DestroyImmediate(template); Object.DestroyImmediate(mist);
                    Object.DestroyImmediate(owner); Object.DestroyImmediate(cameraObject);
                }
            }
        }
    }
}
