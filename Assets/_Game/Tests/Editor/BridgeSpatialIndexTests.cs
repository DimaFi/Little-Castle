using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class BridgeSpatialIndexTests
    {
        [Test]
        public void SpatialBridgePlanner_FindsNegativeZeroAndCellBoundaryCrossings()
        {
            const int seed = 24680;

            var plan =
                new MacroWorldPlan(
                    seed);

            var road =
                new WorldRoadData
                {
                    stableId = 100,
                    roadKind = RoadKind.DirtRoad,
                    width = 5f
                };

            road.centerline.Add(
                new Vector2(
                    -150f,
                    0f));

            road.centerline.Add(
                new Vector2(
                    150f,
                    0f));

            Assert.That(
                plan.AddRoad(
                    road),
                Is.True);

            AddVerticalRiver(
                plan,
                201,
                -64f);

            AddVerticalRiver(
                plan,
                202,
                0f);

            AddVerticalRiver(
                plan,
                203,
                64f);

            var settings =
                new BridgePlannerSettings
                {
                    enabled = true,
                    archetypeId =
                        "bridge_wood_small",
                    standardizeCrossings = true,
                    standardRiverCrossingWidth = 8f,
                    standardBridgeSpan = 12f,
                    crossingWidthBlendPointRadius = 1
                };

            BridgeSitePlanner.BuildBridgeSites(
                seed,
                plan,
                settings);

            Assert.That(
                plan.BridgeSites.Count,
                Is.EqualTo(3));

            Assert.That(
                plan.BridgeSites[0].worldPosition.x,
                Is.EqualTo(-64f)
                    .Within(0.0001f));

            Assert.That(
                plan.BridgeSites[1].worldPosition.x,
                Is.EqualTo(0f)
                    .Within(0.0001f));

            Assert.That(
                plan.BridgeSites[2].worldPosition.x,
                Is.EqualTo(64f)
                    .Within(0.0001f));

            for (int i = 0;
                 i < plan.BridgeSites.Count;
                 i++)
            {
                Assert.That(
                    plan.BridgeSites[i].requiredSpan,
                    Is.EqualTo(12f)
                        .Within(0.0001f));
            }

            for (int i = 0;
                 i < plan.Rivers.Count;
                 i++)
            {
                WorldRiverData river =
                    plan.Rivers[i];

                Assert.That(
                    river.widths.Count,
                    Is.EqualTo(
                        river.centerline.Count));

                Assert.That(
                    river.widths[0],
                    Is.EqualTo(8f)
                        .Within(0.0001f));

                Assert.That(
                    river.widths[1],
                    Is.EqualTo(8f)
                        .Within(0.0001f));
            }
        }

        [Test]
        public void SpatialBridgePlanner_DoesNotCreateNearMissBridge()
        {
            const int seed = 13579;

            var plan =
                new MacroWorldPlan(
                    seed);

            var road =
                new WorldRoadData
                {
                    stableId = 500,
                    roadKind = RoadKind.DirtRoad,
                    width = 5f
                };

            road.centerline.Add(
                new Vector2(
                    -100f,
                    0f));

            road.centerline.Add(
                new Vector2(
                    100f,
                    0f));

            plan.AddRoad(
                road);

            var river =
                new WorldRiverData
                {
                    stableId = 600,
                    nominalWidth = 8f,
                    nominalDepth = 1f
                };

            river.centerline.Add(
                new Vector2(
                    64f,
                    1f));

            river.centerline.Add(
                new Vector2(
                    64f,
                    100f));

            river.widths.Add(8f);
            river.widths.Add(8f);

            plan.AddRiver(
                river);

            BridgeSitePlanner.BuildBridgeSites(
                seed,
                plan,
                new BridgePlannerSettings());

            Assert.That(
                plan.BridgeSites.Count,
                Is.EqualTo(0));
        }

        private static void AddVerticalRiver(
            MacroWorldPlan plan,
            long stableId,
            float x)
        {
            var river =
                new WorldRiverData
                {
                    stableId = stableId,
                    nominalWidth = 6f,
                    nominalDepth = 1f
                };

            river.centerline.Add(
                new Vector2(
                    x,
                    -100f));

            river.centerline.Add(
                new Vector2(
                    x,
                    100f));

            river.widths.Add(6f);
            river.widths.Add(6f);

            river.depths.Add(1f);
            river.depths.Add(1f);

            river.flow.Add(1f);
            river.flow.Add(1f);

            Assert.That(
                plan.AddRiver(
                    river),
                Is.True);
        }
    }
}
