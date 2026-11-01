using LittleCastle.Rendering.ChimneySmoke;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class ChimneySmokeMathTests
    {
        private ChimneySmokeProfile profile;

        [SetUp]
        public void SetUp() => profile = ScriptableObject.CreateInstance<ChimneySmokeProfile>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(profile);

        [Test]
        public void SameSeedAndTimeAreDeterministic()
        {
            var wind = new Vector4(1, 0, .6f, 1);
            Assert.AreEqual(ChimneySmokeMath.Evaluate(.5f, 12f, 21, profile, wind),
                            ChimneySmokeMath.Evaluate(.5f, 12f, 21, profile, wind));
        }

        [Test]
        public void DifferentSeedsChangeTheCurve()
        {
            Vector3 a = ChimneySmokeMath.Evaluate(.65f, 10f, 1, profile, Vector4.zero);
            Vector3 b = ChimneySmokeMath.Evaluate(.65f, 10f, 2, profile, Vector4.zero);
            Assert.Greater(Vector3.Distance(a, b), .00001f);
        }

        [Test]
        public void HigherSamplesRiseAndDriftWithWind()
        {
            Vector4 wind = new Vector4(1f, 0f, 1f, 1f);
            Vector3 root = ChimneySmokeMath.Evaluate(0f, 10f, 77, profile, wind);
            Vector3 tip = ChimneySmokeMath.Evaluate(1f, 10f, 77, profile, wind);
            Assert.Greater(tip.y, root.y);
            Assert.That(tip.x, Is.GreaterThan(root.x));
        }

        [Test]
        public void AlphaFadesAtRootAndTip()
        {
            Assert.AreEqual(0f, ChimneySmokeMath.ShapeAlpha(0f), .00001f);
            Assert.AreEqual(0f, ChimneySmokeMath.ShapeAlpha(1f), .00001f);
            Assert.Greater(ChimneySmokeMath.ShapeAlpha(.4f), .9f);
        }
    }
}
