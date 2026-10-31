using System.Collections.Generic;
using System.Reflection;
using LittleCastle.Rendering;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittleCastle.Tests
{
    public sealed class NightLightPreviewBudgetTests
    {
        [Test]
        public void ExplicitPreview_SelectsPriorityThenNearestWithoutRuntimeRegistration()
        {
            var fixture = new PreviewFixture();
            try
            {
                fixture.SetBudget(2, 10);
                var priority = fixture.AddEmitter(6, 10);
                var nearest = fixture.AddEmitter(1);
                var middle = fixture.AddEmitter(2);
                var farther = fixture.AddEmitter(4);
                var outside = fixture.AddEmitter(11);
                var noLight = fixture.AddEmitter(3, withLight: false);
                var disabled = fixture.AddEmitter(.5f);
                disabled.enabled = false;
                var registryBefore = new HashSet<NightLightEmitter>(NightLightEmitter.ActiveEmitters);

                fixture.Manager.EvaluatePreviewBudget(new[]
                {
                    farther, outside, middle, noLight, priority, disabled, nearest, null
                });
                fixture.TickEmitters(1);

                Assert.That(fixture.Manager.RegisteredEmitterCount, Is.EqualTo(6));
                Assert.That(fixture.Manager.CandidateCount, Is.EqualTo(4));
                Assert.That(fixture.Manager.ActiveRealtimeLightCount, Is.EqualTo(2));
                Assert.That(priority.BudgetActive, Is.True, "Priority precedes distance.");
                Assert.That(nearest.BudgetActive, Is.True, "Equal-priority candidates use distance.");
                Assert.That(middle.BudgetActive || farther.BudgetActive || outside.BudgetActive ||
                    noLight.BudgetActive || disabled.BudgetActive, Is.False);
                CollectionAssert.AreEquivalent(registryBefore, NightLightEmitter.ActiveEmitters,
                    "An explicit preview must not register its emitters in the runtime registry.");
            }
            finally { fixture.Dispose(); }
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(12, 3)]
        public void ExplicitPreview_RespectsFiniteRealtimeBudget(int limit, int expected)
        {
            var fixture = new PreviewFixture();
            try
            {
                fixture.SetBudget(limit, 10);
                var emitters = new[] { fixture.AddEmitter(3), fixture.AddEmitter(1), fixture.AddEmitter(2) };
                fixture.Manager.EvaluatePreviewBudget(emitters);
                fixture.TickEmitters(1);

                int rendered = 0;
                foreach (var emitter in emitters)
                {
                    if (emitter.TargetLight.enabled) rendered++;
                    Assert.That(emitter.CurrentStrength, Is.InRange(0f, 1f));
                    Assert.That(float.IsNaN(emitter.TargetLight.intensity) ||
                        float.IsInfinity(emitter.TargetLight.intensity), Is.False);
                }
                Assert.That(fixture.Manager.ActiveRealtimeLightCount, Is.EqualTo(expected));
                Assert.That(rendered, Is.EqualTo(expected));
            }
            finally { fixture.Dispose(); }
        }

        [Test]
        public void ExplicitPreview_UsesEmitterRangeAndNightThresholdThenFadesOutByDay()
        {
            var fixture = new PreviewFixture();
            try
            {
                fixture.SetBudget(3, 10);
                var nearby = fixture.AddEmitter(2);
                var beyondEmitterRange = fixture.AddEmitter(4);
                SetField(beyondEmitterRange, "maxDistance", 3f);
                var emitters = new[] { nearby, beyondEmitterRange };

                fixture.SetNightAmount(.1f);
                fixture.Manager.EvaluatePreviewBudget(emitters);
                Assert.That(fixture.Manager.CandidateCount, Is.Zero);

                fixture.SetNightAmount(1);
                fixture.Manager.EvaluatePreviewBudget(emitters);
                fixture.TickEmitters(1);
                Assert.That(fixture.Manager.CandidateCount, Is.EqualTo(1));
                Assert.That(nearby.TargetLight.enabled, Is.True);
                Assert.That(beyondEmitterRange.BudgetActive, Is.False);

                fixture.SetNightAmount(0);
                fixture.Manager.EvaluatePreviewBudget(emitters);
                Assert.That(fixture.Manager.ActiveRealtimeLightCount, Is.Zero);
                Assert.That(nearby.BudgetActive, Is.False);
                Assert.That(nearby.TargetLight.enabled, Is.True, "Deselection preserves the fade-out tier.");
                fixture.TickEmitters(1);
                Assert.That(nearby.TargetLight.enabled, Is.False);
                Assert.That(nearby.CurrentStrength, Is.Zero);
            }
            finally { fixture.Dispose(); }
        }

        [Test]
        public void RestrictedPreview_DoesNotSelectFadeOrHideAnotherSceneEmitter()
        {
            var local = new PreviewFixture();
            PreviewFixture foreign = null;
            try
            {
                foreign = new PreviewFixture();
                local.SetBudget(1, 10);
                SetField(local.Manager, "restrictToOwnScene", true);
                var ownEmitter = local.AddEmitter(2);
                var otherEmitter = foreign.AddEmitter(200, 10, withPool: true);
                otherEmitter.SetBudgetActive(true, 1);
                otherEmitter.TickPresentation(.05f);
                otherEmitter.PoolVisual.SetPresentationVisible(true);
                float strength = otherEmitter.CurrentStrength;
                float intensity = otherEmitter.TargetLight.intensity;

                local.Manager.EvaluatePreviewBudget(new[] { otherEmitter, ownEmitter });
                Invoke(local.Manager, "TickPresentationLights", .1f);

                Assert.That(local.Manager.RegisteredEmitterCount, Is.EqualTo(1));
                Assert.That(local.Manager.CandidateCount, Is.EqualTo(1));
                Assert.That(local.Manager.ActiveRealtimeLightCount, Is.EqualTo(1));
                Assert.That(ownEmitter.BudgetActive, Is.True);
                Assert.That(otherEmitter.BudgetActive, Is.True);
                Assert.That(otherEmitter.CurrentStrength, Is.EqualTo(strength));
                Assert.That(otherEmitter.TargetLight.intensity, Is.EqualTo(intensity));
                Assert.That(otherEmitter.TargetLight.enabled, Is.True);
                Assert.That(otherEmitter.PoolVisible, Is.True,
                    "The local manager must not apply its distance LOD to another additive scene.");
                Assert.That(local.Manager.FadingRealtimeLightCount, Is.Zero);
            }
            finally
            {
                foreign?.Dispose();
                local.Dispose();
            }
        }

        [Test]
        public void DefaultUnrestrictedPreview_PreservesCrossSceneSelection()
        {
            var local = new PreviewFixture();
            PreviewFixture foreign = null;
            try
            {
                foreign = new PreviewFixture();
                local.SetBudget(1, 10);
                var ownEmitter = local.AddEmitter(3);
                var otherEmitter = foreign.AddEmitter(1);

                local.Manager.EvaluatePreviewBudget(new[] { ownEmitter, otherEmitter });

                Assert.That(local.Manager.RegisteredEmitterCount, Is.EqualTo(2));
                Assert.That(local.Manager.CandidateCount, Is.EqualTo(2));
                Assert.That(local.Manager.ActiveRealtimeLightCount, Is.EqualTo(1));
                Assert.That(ownEmitter.BudgetActive, Is.False);
                Assert.That(otherEmitter.BudgetActive, Is.True,
                    "Scene restriction is opt-in; the existing shared runtime budget remains unrestricted.");
            }
            finally
            {
                foreign?.Dispose();
                local.Dispose();
            }
        }

        sealed class PreviewFixture : System.IDisposable
        {
            readonly Scene scene;
            readonly List<NightLightEmitter> emitters = new List<NightLightEmitter>();
            readonly StylizedLightingGlobals globals;
            public NightLightBudgetManager Manager { get; }

            public PreviewFixture()
            {
                // Preview scenes never open/save an authored scene or change its active state.
                scene = EditorSceneManager.NewPreviewScene();
                var root = AddRoot("Night light budget test");
                root.SetActive(false);
                Manager = root.AddComponent<NightLightBudgetManager>();
                globals = root.AddComponent<StylizedLightingGlobals>();
                var camera = root.AddComponent<Camera>();
                camera.enabled = false;
                SetField(Manager, "lightingGlobals", globals);
                SetField(Manager, "targetCamera", camera);
                SetNightAmount(1);
            }

            public void SetBudget(int maximum, float distance)
            {
                SetField(Manager, "maxActiveRealtimeLights", maximum);
                SetField(Manager, "globalMaxDistance", distance);
            }

            public void SetNightAmount(float amount)
            {
                // Exercise budget selection without changing shared shader globals,
                // finding a live atmosphere controller or advancing a real clock.
                var property = typeof(StylizedLightingGlobals).GetProperty(nameof(StylizedLightingGlobals.NightAmount));
                Assert.That(property, Is.Not.Null);
                property.GetSetMethod(true).Invoke(globals, new object[] { amount });
            }

            public NightLightEmitter AddEmitter(float distance, int priority = 0,
                bool withLight = true, bool withPool = false)
            {
                var root = AddRoot("Explicit preview emitter");
                root.transform.position = new Vector3(distance, 0, 0);
                if (withLight) root.AddComponent<Light>().type = LightType.Point;
                var emitter = root.AddComponent<NightLightEmitter>();
                emitters.Add(emitter);
                SetField(emitter, "priority", priority);
                SetField(emitter, "realtimeFlickerStrength", 0f);
                if (withPool)
                {
                    var pool = new GameObject("Owned test pool");
                    pool.transform.SetParent(root.transform);
                    pool.AddComponent<MeshRenderer>();
                    pool.AddComponent<NightLightPoolVisual>();
                }
                // Ordinary EditMode components do not guarantee Awake. Do not invoke
                // OnEnable: preview tests must not populate the static runtime registry.
                Invoke(emitter, "Awake");
                return emitter;
            }

            public void TickEmitters(float seconds)
            {
                foreach (var emitter in emitters) emitter.TickPresentation(seconds);
            }

            GameObject AddRoot(string name)
            {
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, scene);
                return root;
            }

            public void Dispose()
            {
                // Public presentation methods temporarily use the fade registry.
                // Clear every owned emitter even when an assertion interrupts the test.
                foreach (var emitter in emitters)
                    if (emitter != null) emitter.ForceOffImmediate();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing test configuration field: " + name);
            field.SetValue(target, value);
        }

        static void Invoke(object target, string name, params object[] arguments)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing lifecycle method: " + name);
            method.Invoke(target, arguments);
        }
    }
}
