using System;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class VillageLayoutTests
    {
        private const BuildingSiteFlags Ground =
            BuildingSiteFlags.Ready |
            BuildingSiteFlags.Playable |
            BuildingSiteFlags.Buildable |
            BuildingSiteFlags.Walkable;

        private static VillageLayoutSettings Settings(int houses = 3)
        {
            return new VillageLayoutSettings
            {
                houseCount = houses,
                houseFootprint = new BuildingFootprintDefinition(
                    new Vector2(1.5f, 1.8f),
                    5, 0.35f, 12f, 2f, 2.5f, 0.45f),
                wellRadius = 1f,
                courtyardRadius = 4f,
                minimumBuildingClearance = 0.5f,
                approachHalfWidth = 0.75f,
                pathSampleInterval = 0.75f,
                maximumPathStep = 0.5f,
                maximumCourtyardSlope = 12f,
                maximumWellHeightSpread = 0.35f
            };
        }

        private static BuildingSiteSample Flat(Vector2 position) =>
            new BuildingSiteSample(0f, 0f, Ground);

        private static VillageLayoutData RequireFlatVillage(
            int houses, int seed = 14, long stableId = 42L,
            Vector2 center = default)
        {
            Assert.That(VillageLayoutPlanner.TryPlan(
                seed, stableId, center, Settings(houses), Flat,
                out VillageLayoutData layout,
                out VillageLayoutFailure rejected), Is.True,
                "Flat fixture must work. Failure: " + rejected.reason +
                " lastCandidate=" + rejected.lastCandidateReason +
                " slot=" + rejected.failedSlot);
            Assert.That(layout, Is.Not.Null);
            return layout;
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void FlatVillage_HasAllHousesWellAndWalkableApproaches(int count)
        {
            VillageLayoutData village = RequireFlatVillage(count);
            Assert.That(village.Houses.Count, Is.EqualTo(count));
            Assert.That(village.Approaches.Count, Is.EqualTo(count));
            Assert.That(village.VillageStableId, Is.EqualTo(42L));
            Assert.That(village.WellPosition, Is.EqualTo(Vector2.zero));
            Assert.That(village.WellHeight, Is.Zero);

            for (int i = 0; i < count; i++)
            {
                VillageHousePlacement house = village.Houses[i];
                VillageApproachPath path = village.Approaches[i];
                Assert.That(house.slot, Is.EqualTo(i));
                Assert.That(path.houseStableId, Is.EqualTo(house.stableId));
                Assert.That(path.doorEdgeWorld, Is.Not.EqualTo(
                    path.courtyardEdgeWorld));
                Assert.That(Vector2.Distance(
                    village.Center, path.courtyardEdgeWorld),
                    Is.EqualTo(village.CourtyardRadius).Within(0.0002f));

                Vector2 inward = (village.Center - house.worldCenter).normalized;
                Vector2 door = new Vector2(
                    Mathf.Sin(house.yawDegrees * Mathf.Deg2Rad),
                    Mathf.Cos(house.yawDegrees * Mathf.Deg2Rad));
                Assert.That(Vector2.Dot(door, inward),
                    Is.GreaterThan(0.9999f),
                    "Entrance local +Z must face communal courtyard.");
            }

            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                {
                    Assert.That(village.Houses[i].stableId,
                        Is.Not.EqualTo(village.Houses[j].stableId));
                    Assert.That(Vector2.Distance(
                        village.Houses[i].worldCenter,
                        village.Houses[j].worldCenter),
                        Is.GreaterThan(5f));
                }
        }

        [Test]
        public void RepeatSameSeedAndNegativeOrigin_PreservesStableData()
        {
            var origin = new Vector2(-960f, -224f);
            VillageLayoutData first = RequireFlatVillage(
                5, -10101, 88990011L, origin);
            VillageLayoutData second = RequireFlatVillage(
                5, -10101, 88990011L, origin);

            Assert.That(first.WellStableId, Is.EqualTo(second.WellStableId));
            Assert.That(first.WellPosition, Is.EqualTo(second.WellPosition));
            for (int i = 0; i < first.Houses.Count; i++)
            {
                Assert.That(second.Houses[i].stableId,
                    Is.EqualTo(first.Houses[i].stableId));
                Assert.That(second.Houses[i].worldCenter,
                    Is.EqualTo(first.Houses[i].worldCenter));
                Assert.That(second.Houses[i].yawDegrees,
                    Is.EqualTo(first.Houses[i].yawDegrees));
                Assert.That(second.Approaches[i].stableId,
                    Is.EqualTo(first.Approaches[i].stableId));
                Assert.That(second.Approaches[i].doorEdgeWorld,
                    Is.EqualTo(first.Approaches[i].doorEdgeWorld));
                Assert.That(second.Approaches[i].courtyardEdgeWorld,
                    Is.EqualTo(first.Approaches[i].courtyardEdgeWorld));
            }
        }

        [Test]
        public void WorldSeedAndVillageId_AffectStableHouseIds()
        {
            VillageLayoutData one = RequireFlatVillage(3, 14, 42L);
            VillageLayoutData two = RequireFlatVillage(3, 15, 42L);
            VillageLayoutData three = RequireFlatVillage(3, 14, 43L);

            Assert.That(one.Houses[0].stableId,
                Is.Not.EqualTo(two.Houses[0].stableId));
            Assert.That(one.Houses[0].stableId,
                Is.Not.EqualTo(three.Houses[0].stableId));
            Assert.That(one.VillageStableId, Is.EqualTo(42L));
            Assert.That(three.VillageStableId, Is.EqualTo(43L));
        }

        [Test]
        public void WellOrCourtyardUnderWater_ReturnsNoPartialLayout()
        {
            Func<Vector2, BuildingSiteSample> sample = position =>
                new BuildingSiteSample(0f, 0f,
                    position == Vector2.zero
                        ? Ground | BuildingSiteFlags.Water : Ground);
            bool success = VillageLayoutPlanner.TryPlan(
                14, 42L, Vector2.zero, Settings(), sample,
                out VillageLayoutData layout,
                out VillageLayoutFailure failure);

            Assert.That(success, Is.False);
            Assert.That(layout, Is.Null);
            Assert.That(failure.reason,
                Is.EqualTo(VillageLayoutRejection.WellOrCourtyardBlocked));
            Assert.That(failure.failedSlot, Is.EqualTo(-1));
            Assert.That(failure.candidatesAttempted, Is.Zero);
        }

        [Test]
        public void MissingPeripheralTerrain_ExhaustsFiniteCandidates()
        {
            int samples = 0;
            Func<Vector2, BuildingSiteSample> sample = point =>
            {
                samples++;
                return new BuildingSiteSample(0f, 0f,
                    point.magnitude <= 4.001f
                        ? Ground : BuildingSiteFlags.None);
            };

            bool success = VillageLayoutPlanner.TryPlan(
                14, 42L, Vector2.zero, Settings(), sample,
                out VillageLayoutData layout,
                out VillageLayoutFailure failure);

            Assert.That(success, Is.False);
            Assert.That(layout, Is.Null);
            Assert.That(failure.reason,
                Is.EqualTo(VillageLayoutRejection.ExhaustedAlternatives));
            Assert.That(failure.lastCandidateReason,
                Is.EqualTo(VillageLayoutRejection.UnsupportedHouseFootprint));
            Assert.That(failure.footprintRejection,
                Is.EqualTo(BuildingFootprintRejection.SampleUnavailable));
            Assert.That(failure.failedSlot, Is.EqualTo(0));
            Assert.That(failure.candidatesAttempted,
                Is.EqualTo(VillageLayoutPlanner.MaximumAlternativesPerHouse));
            Assert.That(samples, Is.LessThan(1000));
        }

        [Test]
        public void BlockFirstHouseCenter_UsesBoundedDeterministicAlternative()
        {
            VillageLayoutData primary = RequireFlatVillage(3);
            Vector2 blocked = primary.Houses[0].worldCenter;
            int sampled = 0;
            Func<Vector2, BuildingSiteSample> terrain = point =>
            {
                sampled++;
                return new BuildingSiteSample(0f, 0f,
                    Vector2.Distance(point, blocked) < 0.002f
                        ? Ground | BuildingSiteFlags.Water : Ground);
            };

            bool success = VillageLayoutPlanner.TryPlan(
                14, 42L, Vector2.zero, Settings(), terrain,
                out VillageLayoutData fallback,
                out VillageLayoutFailure failure);

            Assert.That(success, Is.True,
                "A single blocked original candidate should have alternatives.");
            Assert.That(fallback.Houses.Count, Is.EqualTo(3));
            Assert.That(fallback.Houses[0].worldCenter,
                Is.Not.EqualTo(blocked));
            Assert.That(fallback.Houses[0].stableId,
                Is.EqualTo(primary.Houses[0].stableId),
                "Retry must not change the logical house ID.");
            Assert.That(sampled, Is.GreaterThan(25 + 34 * 3));
        }

        [Test]
        public void NarrowBlockedApproach_UsesAlternativeWithoutFlattening()
        {
            VillageLayoutData primary = RequireFlatVillage(3);
            VillageApproachPath oldPath = primary.Approaches[0];
            Vector2 obstacle = Vector2.Lerp(
                oldPath.doorEdgeWorld, oldPath.courtyardEdgeWorld, 0.5f);
            Func<Vector2, BuildingSiteSample> terrain = point =>
                new BuildingSiteSample(0f, 0f,
                    Vector2.Distance(point, obstacle) < 0.025f
                        ? Ground | BuildingSiteFlags.Occupied : Ground);

            bool success = VillageLayoutPlanner.TryPlan(
                14, 42L, Vector2.zero, Settings(), terrain,
                out VillageLayoutData alternative,
                out VillageLayoutFailure failure);

            Assert.That(success, Is.True,
                "A single tiny path obstruction should shift the local slot.");
            Assert.That(alternative.Houses[0].worldCenter,
                Is.Not.EqualTo(primary.Houses[0].worldCenter));
            Assert.That(alternative.Houses[0].stableId,
                Is.EqualTo(primary.Houses[0].stableId));
        }

        [Test]
        public void InvalidSettingsAndMissingSampler_DoNotQueryTerrain()
        {
            var invalid = Settings();
            invalid.houseCount = 6;
            int calls = 0;
            Func<Vector2, BuildingSiteSample> sample = p =>
            {
                calls++;
                return Flat(p);
            };

            Assert.That(VillageLayoutPlanner.TryPlan(
                1, 22, Vector2.zero, invalid, sample,
                out var bad, out var rejection), Is.False);
            Assert.That(bad, Is.Null);
            Assert.That(rejection.reason,
                Is.EqualTo(VillageLayoutRejection.InvalidRequest));
            Assert.That(calls, Is.Zero);

            Assert.That(VillageLayoutPlanner.TryPlan(
                1, 22, Vector2.zero, Settings(), null,
                out _, out _), Is.False);
            Assert.That(VillageLayoutPlanner.TryPlan(
                1, 22, new Vector2(float.NaN, 0f), Settings(), sample,
                out _, out _), Is.False);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void ExcessiveWellHeightSpread_RejectsCourtyard()
        {
            Func<Vector2, BuildingSiteSample> sample = p =>
                new BuildingSiteSample(p.x > 0.5f ? 1f : 0f, 0f, Ground);

            Assert.That(VillageLayoutPlanner.TryPlan(
                1, 22, Vector2.zero, Settings(), sample,
                out VillageLayoutData data,
                out VillageLayoutFailure reason), Is.False);
            Assert.That(data, Is.Null);
            Assert.That(reason.reason,
                Is.EqualTo(VillageLayoutRejection.WellOrCourtyardBlocked));
        }

        [Test]
        public void PathSamplingHasExplicitUpperBoundAndIsFinite()
        {
            VillageLayoutSettings huge = Settings(3);
            huge.houseFootprint.entranceDepth = 150f;
            int calls = 0;
            Func<Vector2, BuildingSiteSample> sample = p =>
            {
                calls++;
                return Flat(p);
            };

            Assert.That(VillageLayoutPlanner.TryPlan(
                14, 42L, Vector2.zero, huge, sample,
                out VillageLayoutData plan,
                out VillageLayoutFailure reason), Is.False);
            Assert.That(plan, Is.Null);
            Assert.That(reason.reason,
                Is.EqualTo(VillageLayoutRejection.ExhaustedAlternatives));
            Assert.That(reason.lastCandidateReason,
                Is.EqualTo(VillageLayoutRejection.ApproachBlocked));
            Assert.That(calls, Is.LessThan(1000),
                "Path length must be refused before unbounded probe requests.");
        }

        [Test]
        public void BlockedCourtyardSlope_DoesNotBecomeBuildableViaRoadMask()
        {
            Func<Vector2, BuildingSiteSample> sample = p =>
                new BuildingSiteSample(0f, p == Vector2.zero ? 30f : 0f,
                    Ground | BuildingSiteFlags.Road);

            Assert.That(VillageLayoutPlanner.TryPlan(
                14, 42, Vector2.zero, Settings(), sample,
                out var plan, out var failure), Is.False);
            Assert.That(plan, Is.Null);
            Assert.That(failure.reason,
                Is.EqualTo(VillageLayoutRejection.WellOrCourtyardBlocked));
        }
    }
}
