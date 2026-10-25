using LittleCastle.Rendering.Atmosphere;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class AtmosphereRendererTests
    {
        [Test]
        public void Attenuation_MonotonicAndBounded()
        {
            float near = LCAtmosphereMath.Transmittance(0.01f, 10f);
            float far = LCAtmosphereMath.Transmittance(0.01f, 200f);
            Assert.That(near, Is.InRange(0f, 1f));
            Assert.That(far, Is.LessThan(near));
            Assert.That(LCAtmosphereMath.Transmittance(-1f, 100f), Is.EqualTo(1f));
        }

        [Test]
        public void GroundFog_DecreasesWithHeight()
        {
            float valley = LCAtmosphereMath.HeightDensity(0f, 0f, 0.02f, 20f);
            float mountain = LCAtmosphereMath.HeightDensity(80f, 0f, 0.02f, 20f);
            Assert.That(mountain, Is.LessThan(valley));
        }

        [Test]
        public void BiomeZone_FeathersOutsideBounds()
        {
            var go = new GameObject("atmosphere-zone-test");
            try
            {
                float inside = LCAtmosphereMath.ZoneWeight(Vector3.zero, go.transform, new Vector2(100f, 100f), 40f);
                float edge = LCAtmosphereMath.ZoneWeight(new Vector3(70f, 0f, 0f), go.transform, new Vector2(100f, 100f), 40f);
                float outside = LCAtmosphereMath.ZoneWeight(new Vector3(150f, 0f, 0f), go.transform, new Vector2(100f, 100f), 40f);
                Assert.That(inside, Is.EqualTo(1f));
                Assert.That(edge, Is.InRange(0.001f, 0.999f));
                Assert.That(outside, Is.EqualTo(0f));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void BuildMaterial_ReferencesHiddenShader()
        {
            Shader shader = Shader.Find("Hidden/Little Castle/Atmosphere Fog");
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Resources/Atmosphere/LC_AtmosphereFog.mat");
            Assert.That(shader, Is.Not.Null);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.EqualTo(shader));
        }
    }
}
