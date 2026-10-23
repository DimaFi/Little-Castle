using LittleCastle.Rendering;
using NUnit.Framework;

namespace LittleCastle.Tests
{
    public sealed class VisibilityBudgetPolicyTests
    {
        [Test]
        public void NearCenterTarget_UsesFullQuality()
        {
            Assert.That(
                Evaluate(true, false, 0.15f, 20f),
                Is.EqualTo(VisibilityQualityTier.Full));
        }

        [Test]
        public void NearPeripheralTarget_UsesBalancedQuality()
        {
            Assert.That(
                Evaluate(true, false, 0.88f, 20f),
                Is.EqualTo(VisibilityQualityTier.Balanced));
        }

        [Test]
        public void FarVisibleTarget_UsesFarQuality()
        {
            Assert.That(
                Evaluate(true, false, 0.1f, 200f),
                Is.EqualTo(VisibilityQualityTier.Far));
        }

        [Test]
        public void NearTargetJustOutsideView_IsPrewarmed()
        {
            Assert.That(
                Evaluate(false, true, 1.08f, 30f),
                Is.EqualTo(VisibilityQualityTier.Balanced));
        }

        [Test]
        public void OffscreenTargetOutsidePrewarm_IsHidden()
        {
            Assert.That(
                Evaluate(false, false, 1.8f, 30f),
                Is.EqualTo(VisibilityQualityTier.Hidden));
        }

        [Test]
        public void RecentlyVisibleOccludedTarget_KeepsQualityDuringGrace()
        {
            VisibilityBudgetSettings settings =
                VisibilityBudgetSettings.Default;

            var input =
                new VisibilityBudgetEvaluationInput
                {
                    cullingStateKnown = true,
                    cullingVisible = false,
                    insideView = true,
                    secondsSinceVisible =
                        settings.occlusionGraceSeconds * 0.5f,
                    viewportEdge = 0.15f,
                    surfaceDistance = 20f
                };

            Assert.That(
                VisibilityBudgetPolicy.Evaluate(settings, input),
                Is.EqualTo(VisibilityQualityTier.Full));
        }

        [Test]
        public void LongOccludedTarget_CanBecomeHidden()
        {
            VisibilityBudgetSettings settings =
                VisibilityBudgetSettings.Default;

            var input =
                new VisibilityBudgetEvaluationInput
                {
                    cullingStateKnown = true,
                    cullingVisible = false,
                    insideView = true,
                    secondsSinceVisible =
                        settings.occlusionGraceSeconds + 1f,
                    viewportEdge = 0.15f,
                    surfaceDistance = 20f
                };

            Assert.That(
                VisibilityBudgetPolicy.Evaluate(settings, input),
                Is.EqualTo(VisibilityQualityTier.Hidden));
        }

        [Test]
        public void ForceFullQuality_OnlyAffectsVisibleTarget()
        {
            VisibilityBudgetSettings settings =
                VisibilityBudgetSettings.Default;

            var visible =
                new VisibilityBudgetEvaluationInput
                {
                    insideView = true,
                    viewportEdge = 0.95f,
                    surfaceDistance = 80f,
                    forceFullQuality = true
                };

            var hidden =
                new VisibilityBudgetEvaluationInput
                {
                    insideView = false,
                    insidePrewarm = false,
                    viewportEdge = 2f,
                    surfaceDistance = 80f,
                    forceFullQuality = true
                };

            Assert.That(
                VisibilityBudgetPolicy.Evaluate(settings, visible),
                Is.EqualTo(VisibilityQualityTier.Full));

            Assert.That(
                VisibilityBudgetPolicy.Evaluate(settings, hidden),
                Is.EqualTo(VisibilityQualityTier.Hidden));
        }

        private static VisibilityQualityTier Evaluate(
            bool insideView,
            bool insidePrewarm,
            float viewportEdge,
            float surfaceDistance)
        {
            var input =
                new VisibilityBudgetEvaluationInput
                {
                    insideView = insideView,
                    insidePrewarm = insidePrewarm,
                    secondsSinceVisible = float.PositiveInfinity,
                    viewportEdge = viewportEdge,
                    surfaceDistance = surfaceDistance
                };

            return
                VisibilityBudgetPolicy.Evaluate(
                    VisibilityBudgetSettings.Default,
                    input);
        }
    }
}
