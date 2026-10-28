using LittleCastle.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class NightLightingRuntimeTests
    {
        [Test]
        public void NightLightEmitter_FadesInAndOutWithoutInstantPop()
        {
            var go =
                new GameObject(
                    "NightLightEmitter_Test");

            Light light =
                go.AddComponent<Light>();

            light.type =
                LightType.Point;

            NightLightEmitter emitter =
                go.AddComponent<
                    NightLightEmitter>();

            try
            {
                // EditMode tests do not guarantee MonoBehaviour.Awake runs
                // when AddComponent is called. Explicitly exercise the real
                // initialization path instead of assuming PlayMode lifecycle.
                var awake = typeof(NightLightEmitter).GetMethod(
                    "Awake",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.That(awake, Is.Not.Null);
                awake.Invoke(emitter, null);

                Assert.That(
                    light.enabled,
                    Is.False);

                emitter.SetBudgetActive(
                    true,
                    1f);

                emitter.TickPresentation(
                    0.05f);

                Assert.That(
                    light.enabled,
                    Is.True);

                Assert.That(
                    light.intensity,
                    Is.GreaterThan(0f));

                Assert.That(
                    emitter.CurrentStrength,
                    Is.GreaterThan(0f)
                        .And
                        .LessThan(1f));

                emitter.TickPresentation(
                    1f);

                Assert.That(
                    emitter.CurrentStrength,
                    Is.EqualTo(1f)
                        .Within(0.001f));

                emitter.SetBudgetActive(
                    false,
                    1f);

                emitter.TickPresentation(
                    0.05f);

                Assert.That(
                    light.enabled,
                    Is.True,
                    "Fade-out should not hard-disable the Light immediately.");

                Assert.That(
                    emitter.CurrentStrength,
                    Is.GreaterThan(0f));

                emitter.TickPresentation(
                    1f);

                Assert.That(
                    emitter.CurrentStrength,
                    Is.EqualTo(0f)
                        .Within(0.001f));

                Assert.That(
                    light.enabled,
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(
                    go);
            }
        }

        [Test]
        public void NightLightPoolVisual_UsesPropertyBlockWithoutMaterialClone()
        {
            Shader shader =
                Shader.Find(
                    "Little Castle/Effects/LC Night Light Pool");

            Assert.That(
                shader,
                Is.Not.Null);

            var material =
                new Material(
                    shader);

            var go =
                GameObject.CreatePrimitive(
                    PrimitiveType.Quad);

            MeshRenderer renderer =
                go.GetComponent<
                    MeshRenderer>();

            renderer.sharedMaterial =
                material;

            NightLightPoolVisual visual =
                go.AddComponent<
                    NightLightPoolVisual>();

            try
            {
                Material before =
                    renderer.sharedMaterial;

                visual.ApplyProperties();

                Assert.That(
                    renderer.sharedMaterial,
                    Is.SameAs(
                        before));

                var block =
                    new MaterialPropertyBlock();

                renderer.GetPropertyBlock(
                    block);

                Assert.That(
                    block.GetFloat(
                        Shader.PropertyToID(
                            "_Intensity")),
                    Is.GreaterThan(0f));

                visual.SetPresentationVisible(
                    false);

                Assert.That(
                    renderer.enabled,
                    Is.False);

                visual.SetPresentationVisible(
                    true);

                Assert.That(
                    renderer.enabled,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(
                    go);

                Object.DestroyImmediate(
                    material);
            }
        }
    }
}
