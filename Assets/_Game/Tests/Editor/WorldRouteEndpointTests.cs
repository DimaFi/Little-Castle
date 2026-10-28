using System.Collections.Generic;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class WorldRouteEndpointTests
    {
        private static readonly Vector2 From = new Vector2(-32f, -16f);
        private static readonly Vector2 To = new Vector2(-12f, -16f);

        [TestCase(false)]
        [TestCase(true)]
        public void MatchingEndpoints_AreConnectedInEitherCenterlineDirection(bool reverse)
        {
            var plan = Plan(reverse ? To : From, reverse ? From : To);
            var report = WorldRouteConnectivityValidator.Validate(plan, 0, true);
            Assert.That(report.IsValid, Is.True, string.Join("; ", report.errors));
            Assert.That(report.components, Is.EqualTo(1));
        }

        [Test]
        public void MatchingIdOnUnrelatedGeometry_IsNotConnectivity()
        {
            var plan = Plan(new Vector2(100f, 100f), new Vector2(120f, 100f));
            var report = WorldRouteConnectivityValidator.Validate(plan, 0, true);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.missingRealizedConnections, Is.EqualTo(1));
            Assert.That(report.components, Is.EqualTo(2));
        }

        [TestCase(0.05f, true)]
        [TestCase(0.2f, false)]
        public void EndpointTolerance_AllowsNumericDriftButNotARealGap(float gap, bool expected)
        {
            var plan = Plan(From + new Vector2(gap, 0f), To);
            var report = WorldRouteConnectivityValidator.Validate(plan, 0, true);
            Assert.That(report.IsValid, Is.EqualTo(expected));
        }

        [Test]
        public void NonFiniteInteriorPoint_CannotProduceValidConnectivity()
        {
            var plan = Plan(From, To);
            plan.Roads[0].centerline.Insert(1, new Vector2(float.NaN, 0f));
            var report = WorldRouteConnectivityValidator.Validate(plan, 0, true);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.components, Is.EqualTo(2));
        }

        private static MacroWorldPlan Plan(Vector2 start, Vector2 end)
        {
            var plan = new MacroWorldPlan(-10101);
            plan.AddPointFeature(new WorldPointFeatureData(11, WorldFeatureKind.NeutralSettlement,
                "v", From, 0f));
            plan.AddPointFeature(new WorldPointFeatureData(22, WorldFeatureKind.Ruin,
                "r", To, 0f));
            plan.AddRoadConnection(new WorldRoadConnectionData(101, 11, 22, RoadKind.DirtRoad));
            plan.AddRoad(new WorldRoadData
            {
                stableId = 101, roadKind = RoadKind.DirtRoad, width = 3f,
                centerline = new List<Vector2> { start, end }
            });
            return plan;
        }
    }
}
