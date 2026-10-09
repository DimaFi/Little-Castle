using System;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Pure data tests. These deliberately do not use Physics, prefabs,
    /// WorldStreamer or loading order as a proxy for placement support.
    /// </summary>
    public sealed class FootprintValidatorTests
    {
        private const BuildingSiteFlags Ground =
            BuildingSiteFlags.Ready |
            BuildingSiteFlags.Playable |
            BuildingSiteFlags.Buildable |
            BuildingSiteFlags.Walkable;

        private static BuildingFootprintDefinition Definition(
            int grid = 5, float spread = 0.5f)
        {
            return new BuildingFootprintDefinition(
                new Vector2(2f, 2f), grid, spread, 12f,
                2f, 3f, 0.4f);
        }

        private static BuildingSiteSample Flat(Vector2 point)
        {
            return new BuildingSiteSample(0f, 0f, Ground);
        }

        private static BuildingFootprintResult Check(
            Func<Vector2, BuildingSiteSample> sample,
            float yaw = 0f)
        {
            return BuildingFootprintValidator.Validate(
                Vector2.zero, yaw, Definition(), sample);
        }

        [Test]
        public void FlatGround_AcceptsBoundedSupportAndEntranceQueries()
        {
            int calls = 0;
            var result = Check(point =>
            {
                calls++;
                return Flat(point);
            });

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.rejection,
                Is.EqualTo(BuildingFootprintRejection.None));
            Assert.That(result.samplesChecked, Is.EqualTo(34),
                "5x5 foundation plus 3x3 forward entrance");
            Assert.That(calls, Is.EqualTo(34));
            Assert.That(result.minimumHeight, Is.EqualTo(0f));
            Assert.That(result.maximumHeight, Is.EqualTo(0f));
        }

        [Test]
        public void InteriorRidge_MissedByCenterAndCorners_IsRejected()
        {
            var result = Check(point =>
                new BuildingSiteSample(
                    Mathf.Abs(point.x) < 0.1f &&
                    Mathf.Abs(point.y - 1f) < 0.1f ? 2f : 0f,
                    0f, Ground));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.rejection,
                Is.EqualTo(BuildingFootprintRejection.HeightSpreadExceeded));
            Assert.That(result.samplesChecked, Is.EqualTo(25));
            Assert.That(result.maximumHeight, Is.EqualTo(2f));
        }

        [Test]
        public void OrientedEntrance_TracksYawAndBlocksOccupiedCorridor()
        {
            Func<Vector2, BuildingSiteSample> sample = point =>
                new BuildingSiteSample(0f, 0f,
                    point.x > 2.9f ? Ground | BuildingSiteFlags.Occupied
                                   : Ground);

            var forward = Check(sample, 0f);
            var rotated = Check(sample, 90f);

            Assert.That(forward.IsValid, Is.True);
            Assert.That(rotated.rejection,
                Is.EqualTo(BuildingFootprintRejection.Occupied));
            Assert.That(rotated.rejectedAt.x, Is.GreaterThan(2.9f));
            Assert.That(rotated.rejectedAt.y, Is.InRange(-1.1f, 1.1f));
        }

        [Test]
        public void FoundationRoadRejected_ButWalkableApproachRoadAllowed()
        {
            var roadFoundation = Check(point =>
                new BuildingSiteSample(0f, 0f,
                    point.x == 0f && point.y == 0f
                        ? Ground | BuildingSiteFlags.Road
                        : Ground));
            var approachRoad = Check(point =>
                new BuildingSiteSample(0f, 0f,
                    point.y > 2f
                        ? Ground | BuildingSiteFlags.Road
                        : Ground));

            Assert.That(roadFoundation.rejection,
                Is.EqualTo(BuildingFootprintRejection.RoadUnderFoundation));
            Assert.That(approachRoad.IsValid, Is.True,
                "An approach may meet a real road.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Water_IsRejectedUnderFoundationAndEntrance(bool entrance)
        {
            var result = Check(point =>
                new BuildingSiteSample(0f, 0f,
                    (entrance ? point.y > 2.5f :
                        Mathf.Abs(point.x) < 0.1f &&
                        Mathf.Abs(point.y) < 0.1f)
                        ? Ground | BuildingSiteFlags.Water
                        : Ground));

            Assert.That(result.rejection,
                Is.EqualTo(BuildingFootprintRejection.Water));
        }

        [Test]
        public void UnbuildableSupportAndUnwalkableEntrance_FailDifferently()
        {
            var badFoundation = Check(point =>
                new BuildingSiteSample(0f, 0f,
                    Mathf.Abs(point.x - 1f) < 0.1f &&
                    Mathf.Abs(point.y - 1f) < 0.1f
                        ? Ground & ~BuildingSiteFlags.Buildable : Ground));
            var badEntrance = Check(point =>
                new BuildingSiteSample(0f, 0f,
                    point.y > 2.9f
                        ? Ground & ~BuildingSiteFlags.Walkable : Ground));

            Assert.That(badFoundation.rejection,
                Is.EqualTo(BuildingFootprintRejection.UnbuildableTerrain));
            Assert.That(badEntrance.rejection,
                Is.EqualTo(BuildingFootprintRejection.EntranceNotWalkable));
        }

        [Test]
        public void SteepSlopeOrAbruptDoorStep_FailsClosed()
        {
            var slope = Check(point =>
                new BuildingSiteSample(0f,
                    point.x == 1f && point.y == 1f ? 13f : 0f,
                    Ground));
            var step = Check(point =>
                new BuildingSiteSample(point.y > 2.5f ? 0.9f : 0f,
                    0f, Ground));

            Assert.That(slope.rejection,
                Is.EqualTo(BuildingFootprintRejection.SlopeTooSteep));
            Assert.That(step.rejection,
                Is.EqualTo(BuildingFootprintRejection.EntranceStepExceeded));
            Assert.That(step.samplesChecked, Is.GreaterThan(25));
        }

        [Test]
        public void MissingSampleAndOutOfBounds_AreNotAcceptedAsFlatTerrain()
        {
            var missing = BuildingFootprintValidator.Validate(
                new Vector2(-32f, -32f), 0f, Definition(), point =>
                    new BuildingSiteSample(0f, 0f,
                        point.x < -33f ? BuildingSiteFlags.None : Ground));
            var bounds = BuildingFootprintValidator.Validate(
                new Vector2(-32f, -32f), 0f, Definition(), point =>
                    new BuildingSiteSample(0f, 0f,
                        point.y < -33f
                            ? Ground & ~BuildingSiteFlags.Playable : Ground));

            Assert.That(missing.rejection,
                Is.EqualTo(BuildingFootprintRejection.SampleUnavailable));
            Assert.That(bounds.rejection,
                Is.EqualTo(BuildingFootprintRejection.OutsidePlayableBounds));
        }

        [Test]
        public void NonFiniteOrNotReadySamples_AreExplicitFailures()
        {
            var nan = Check(point =>
                new BuildingSiteSample(float.NaN, 0f, Ground));
            var infinity = Check(point =>
                new BuildingSiteSample(0f, float.PositiveInfinity, Ground));
            var unavailable = Check(point =>
                new BuildingSiteSample(0f, 0f, Ground & ~BuildingSiteFlags.Ready));

            Assert.That(nan.rejection,
                Is.EqualTo(BuildingFootprintRejection.SampleUnavailable));
            Assert.That(infinity.rejection,
                Is.EqualTo(BuildingFootprintRejection.SampleUnavailable));
            Assert.That(unavailable.rejection,
                Is.EqualTo(BuildingFootprintRejection.SampleUnavailable));
        }

        [Test]
        public void NonFiniteRequestAndInvalidFootprint_DoNotInvokeSampler()
        {
            int calls = 0;
            Func<Vector2, BuildingSiteSample> sampler = point =>
            {
                calls++;
                return Flat(point);
            };
            var badYaw = BuildingFootprintValidator.Validate(
                Vector2.zero, float.NaN, Definition(), sampler);
            var badGrid = BuildingFootprintValidator.Validate(
                Vector2.zero, 0f, Definition(12), sampler);
            var badExtents = new BuildingFootprintDefinition(
                Vector2.zero, 5, 0.5f, 12f, 2f, 3f, 0.4f);
            var badExtent = BuildingFootprintValidator.Validate(
                Vector2.zero, 0f, badExtents, sampler);
            var badSampler = BuildingFootprintValidator.Validate(
                Vector2.zero, 0f, Definition(), null);

            Assert.That(badYaw.rejection,
                Is.EqualTo(BuildingFootprintRejection.InvalidRequest));
            Assert.That(badGrid.rejection,
                Is.EqualTo(BuildingFootprintRejection.InvalidRequest));
            Assert.That(badExtent.rejection,
                Is.EqualTo(BuildingFootprintRejection.InvalidRequest));
            Assert.That(badSampler.rejection,
                Is.EqualTo(BuildingFootprintRejection.InvalidRequest));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void MaximumGridAndNegativeWorldPositions_KeepDeterministicBounds()
        {
            int calls = 0;
            Func<Vector2, BuildingSiteSample> sample = point =>
            {
                calls++;
                Assert.That(point.x, Is.InRange(-1004f, -996f));
                Assert.That(point.y, Is.InRange(-1004f, -993f));
                return Flat(point);
            };

            var a = BuildingFootprintValidator.Validate(
                new Vector2(-1000f, -1000f), 0f, Definition(9), sample);
            var b = BuildingFootprintValidator.Validate(
                new Vector2(-1000f, -1000f), 0f, Definition(9), sample);

            Assert.That(a.IsValid, Is.True);
            Assert.That(b.IsValid, Is.True);
            Assert.That(a.samplesChecked, Is.EqualTo(90));
            Assert.That(b.samplesChecked, Is.EqualTo(90));
            Assert.That(calls, Is.EqualTo(180));
        }

        [Test]
        public void BoundaryHeightSpread_IsInclusive()
        {
            var accepted = BuildingFootprintValidator.Validate(
                Vector2.zero, 0f, Definition(5, 0.5f), point =>
                    new BuildingSiteSample(
                        point.x < -1.5f ? 0.5f : 0f, 0f, Ground));

            Assert.That(accepted.IsValid, Is.True,
                "Exactly at the inclusive terrain-support tolerance");
        }

        [Test]
        public void OccupiedFoundationCannotBeRecoveredByValidEntrance()
        {
            var result = Check(point =>
                new BuildingSiteSample(0f, 0f,
                    point.x == 0f && point.y == 0f
                        ? Ground | BuildingSiteFlags.Occupied : Ground));

            Assert.That(result.rejection,
                Is.EqualTo(BuildingFootprintRejection.Occupied));
            Assert.That(result.samplesChecked, Is.LessThan(25));
        }
    }
}
