using LittleCastle.Rendering;
using NUnit.Framework;

namespace LittleCastle.Tests
{
    public sealed class VisibilityHlodProxyPolicyTests
    {
        [Test]
        public void FullAndBalanced_UseNearPresentation()
        {
            Assert.That(
                VisibilityHlodProxyPolicy.Evaluate(
                    VisibilityQualityTier.Full,
                    VisibilityQualityTier.Far,
                    true),
                Is.EqualTo(HlodPresentationMode.Near));

            Assert.That(
                VisibilityHlodProxyPolicy.Evaluate(
                    VisibilityQualityTier.Balanced,
                    VisibilityQualityTier.Far,
                    true),
                Is.EqualTo(HlodPresentationMode.Near));
        }

        [Test]
        public void Far_UsesProxyPresentation()
        {
            Assert.That(
                VisibilityHlodProxyPolicy.Evaluate(
                    VisibilityQualityTier.Far,
                    VisibilityQualityTier.Far,
                    true),
                Is.EqualTo(HlodPresentationMode.Proxy));
        }

        [Test]
        public void Hidden_CanDisableBothPresentations()
        {
            Assert.That(
                VisibilityHlodProxyPolicy.Evaluate(
                    VisibilityQualityTier.Hidden,
                    VisibilityQualityTier.Far,
                    true),
                Is.EqualTo(HlodPresentationMode.Hidden));
        }

        [Test]
        public void Hidden_CanKeepProxyWhenExplicitlyRequested()
        {
            Assert.That(
                VisibilityHlodProxyPolicy.Evaluate(
                    VisibilityQualityTier.Hidden,
                    VisibilityQualityTier.Far,
                    false),
                Is.EqualTo(HlodPresentationMode.Proxy));
        }
    }
}
