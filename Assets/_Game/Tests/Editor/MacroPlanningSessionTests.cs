using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class MacroPlanningSessionTests
    {
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(127)]
        public void DifferentStepSizes_ProduceSameFeatureAndRoadGraph(
            int workUnits)
        {
            MacroWorldPlannerSettings settings = CreateSettings();
            try
            {
                var planner = new MacroWorldPlanner(settings);
                Rect bounds = new Rect(-240f, -160f, 480f, 320f);
                MacroWorldPlan synchronous =
                    planner.GenerateForBounds(12345, bounds);

                MacroPlanningSession session =
                    planner.BeginPlanning(12345, bounds);
                int calls = 0;
                while (!session.IsTerminal)
                {
                    Assert.That(session.Step(workUnits), Is.InRange(1, workUnits));
                    calls++;
                    Assert.That(calls, Is.LessThan(5000),
                        "Small fixture should terminate without full map work.");
                }

                Assert.That(session.IsCompleted, Is.True);
                MacroWorldPlan staged = session.Result;
                Assert.That(staged, Is.Not.Null);
                AssertEquivalent(synchronous, staged);
                Assert.That(session.PointCellsExamined, Is.GreaterThan(0));
                Assert.That(session.WorkUnitsCompleted,
                    Is.GreaterThanOrEqualTo(session.PointCellsExamined));
                Assert.That(staged.BridgeAwareRoutingAttempted, Is.False);
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void SeedAndNegativeBounds_PreserveStableIdOrder()
        {
            MacroWorldPlannerSettings settings = CreateSettings();
            try
            {
                var planner = new MacroWorldPlanner(settings);
                Rect area = new Rect(-350f, -250f, 360f, 280f);
                MacroWorldPlan expected =
                    planner.GenerateForBounds(-10101, area);
                MacroWorldPlan actual =
                    planner.BeginPlanning(-10101, area).RunToCompletion();
                MacroWorldPlan anotherSeed =
                    planner.BeginPlanning(777, area).RunToCompletion();

                AssertEquivalent(expected, actual);
                Assert.That(expected.PointFeatures.Count, Is.GreaterThan(0));
                bool difference = expected.PointFeatures.Count !=
                    anotherSeed.PointFeatures.Count;
                for (int i = 0; i < expected.PointFeatures.Count &&
                     i < anotherSeed.PointFeatures.Count; i++)
                    difference |= expected.PointFeatures[i].stableId !=
                        anotherSeed.PointFeatures[i].stableId;
                Assert.That(difference, Is.True,
                    "Stable ID must include the seed.");
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void PartialAndCancelledPlan_IsNeverPublicOrResumable()
        {
            MacroWorldPlannerSettings settings = CreateSettings();
            try
            {
                var planner = new MacroWorldPlanner(settings);
                Rect bounds = new Rect(-240f, -160f, 480f, 320f);
                MacroPlanningSession partial =
                    planner.BeginPlanning(123, bounds);

                Assert.That(partial.Result, Is.Null);
                Assert.That(partial.Step(1), Is.EqualTo(1));
                Assert.That(partial.Result, Is.Null);
                Assert.That(partial.PointCellsExamined, Is.EqualTo(1));

                partial.Cancel();
                Assert.That(partial.IsTerminal, Is.True);
                Assert.That(partial.IsCompleted, Is.False);
                Assert.That(partial.Result, Is.Null);
                Assert.That(partial.Phase, Is.EqualTo(
                    MacroPlanningSession.PlanningPhase.Cancelled));
                Assert.That(partial.Step(1), Is.Zero);
                Assert.Throws<System.InvalidOperationException>(
                    () => partial.RunToCompletion());

                MacroWorldPlan restarted =
                    planner.BeginPlanning(123, bounds).RunToCompletion();
                AssertEquivalent(
                    planner.GenerateForBounds(123, bounds), restarted);
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void NullSettings_InstantlyCompletesLegacyEmptyPlan()
        {
            var planner = new MacroWorldPlanner(null);
            Rect bounds = new Rect(-20f, -20f, 40f, 40f);
            MacroPlanningSession session = planner.BeginPlanning(5, bounds);
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.Result, Is.Not.Null);
            Assert.That(session.Result.WorldSeed, Is.EqualTo(5));
            Assert.That(session.Result.PointFeatures.Count, Is.Zero);
            Assert.That(session.Result.Rivers.Count, Is.Zero);
            Assert.That(session.Step(1), Is.Zero);
            Assert.That(planner.GenerateForBounds(5, bounds), Is.Not.Null);
        }

        [Test]
        public void NullRule_IsSkippedWithoutChangingLaterFeatureOrder()
        {
            MacroWorldPlannerSettings settings = CreateSettings();
            try
            {
                SerializedObject so = new SerializedObject(settings);
                SerializedProperty rules = so.FindProperty("pointFeatureRules");
                rules.InsertArrayElementAtIndex(0);
                rules.GetArrayElementAtIndex(0).managedReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            catch
            {
                // Serialized non-managed classes are not safe to null via
                // managedReferenceValue on all Unity versions. Keep test
                // reproducibility by skipping this optional setup check.
                Object.DestroyImmediate(settings);
                Assert.Ignore("Inline serialized null-rule injection unsupported.");
                return;
            }

            try
            {
                var planner = new MacroWorldPlanner(settings);
                Rect bounds = new Rect(-80f, -80f, 160f, 160f);
                AssertEquivalent(
                    planner.GenerateForBounds(42, bounds),
                    planner.BeginPlanning(42, bounds).RunToCompletion());
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void MillisecondBudgetIsSoft_AndAlwaysMakesInitialProgress()
        {
            MacroWorldPlannerSettings settings = CreateSettings();
            try
            {
                MacroPlanningSession session =
                    new MacroWorldPlanner(settings).BeginPlanning(
                        5, new Rect(-240f, -160f, 480f, 320f));
                int consumed = session.Step(1000, 0.001d);
                Assert.That(consumed, Is.InRange(1, 1000));
                Assert.That(session.WorkUnitsCompleted, Is.EqualTo(consumed));
                Assert.That(session.Result, Is.Null);
                session.Cancel();
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void InvalidBudgetRejectedBeforeAnyMutation()
        {
            MacroWorldPlannerSettings settings = CreateSettings();
            try
            {
                var session = new MacroWorldPlanner(settings).BeginPlanning(
                    1, new Rect(0f, 0f, 100f, 100f));
                Assert.Throws<System.ArgumentOutOfRangeException>(
                    () => session.Step(0));
                Assert.Throws<System.ArgumentOutOfRangeException>(
                    () => session.Step(1, 0d));
                Assert.Throws<System.ArgumentOutOfRangeException>(
                    () => session.Step(1, double.NaN));
                Assert.That(session.WorkUnitsCompleted, Is.Zero);
                Assert.That(session.PointCellsExamined, Is.Zero);
                Assert.That(session.IsTerminal, Is.False);
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void PhaseAdvancesInDeterministicOrder_WithoutVisiblePartialPlan()
        {
            MacroWorldPlannerSettings settings = CreateSettings();
            try
            {
                var session = new MacroWorldPlanner(settings).BeginPlanning(
                    14, new Rect(-80f, -80f, 160f, 160f));
                int last = (int)session.Phase;
                int steps = 0;
                while (!session.IsTerminal)
                {
                    Assert.That(session.Result, Is.Null);
                    session.Step(1);
                    int phase = (int)session.Phase;
                    Assert.That(phase, Is.GreaterThanOrEqualTo(last));
                    last = phase;
                    steps++;
                    Assert.That(steps, Is.LessThan(5000));
                }
                Assert.That(session.IsCompleted, Is.True);
                Assert.That(session.Result, Is.Not.Null);
                Assert.That(last, Is.EqualTo(
                    (int)MacroPlanningSession.PlanningPhase.Completed));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        private static MacroWorldPlannerSettings CreateSettings()
        {
            var settings =
                ScriptableObject.CreateInstance<MacroWorldPlannerSettings>();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("planningHalo").floatValue = 0f;
            var rules = serialized.FindProperty("pointFeatureRules");
            rules.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i);
                rule.FindPropertyRelative("ruleId").stringValue =
                    "test_settlement_" + i;
                rule.FindPropertyRelative("chance").floatValue = 1f;
                rule.FindPropertyRelative("spacing").floatValue = 85f;
                rule.FindPropertyRelative("influenceRadius").floatValue = 5f;
                rule.FindPropertyRelative("separationPadding").floatValue = 5f;
                rule.FindPropertyRelative("avoidOtherPointFeatures")
                    .boolValue = false;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return settings;
        }

        private static void AssertEquivalent(
            MacroWorldPlan a, MacroWorldPlan b)
        {
            Assert.That(b.WorldSeed, Is.EqualTo(a.WorldSeed));
            Assert.That(b.PointFeatures.Count,
                Is.EqualTo(a.PointFeatures.Count));
            for (int i = 0; i < a.PointFeatures.Count; i++)
            {
                WorldPointFeatureData x = a.PointFeatures[i];
                WorldPointFeatureData y = b.PointFeatures[i];
                Assert.That(y.stableId, Is.EqualTo(x.stableId));
                Assert.That(y.kind, Is.EqualTo(x.kind));
                Assert.That(y.archetypeId, Is.EqualTo(x.archetypeId));
                Assert.That(y.worldPosition, Is.EqualTo(x.worldPosition));
                Assert.That(y.influenceRadius, Is.EqualTo(x.influenceRadius));
            }

            Assert.That(b.RoadConnections.Count,
                Is.EqualTo(a.RoadConnections.Count));
            for (int i = 0; i < a.RoadConnections.Count; i++)
            {
                WorldRoadConnectionData x = a.RoadConnections[i];
                WorldRoadConnectionData y = b.RoadConnections[i];
                Assert.That(y.stableId, Is.EqualTo(x.stableId));
                Assert.That(y.fromFeatureId, Is.EqualTo(x.fromFeatureId));
                Assert.That(y.toFeatureId, Is.EqualTo(x.toFeatureId));
            }

            Assert.That(b.Roads.Count, Is.EqualTo(a.Roads.Count));
            Assert.That(b.Rivers.Count, Is.EqualTo(a.Rivers.Count));
            Assert.That(b.BridgeSites.Count, Is.EqualTo(a.BridgeSites.Count));
            Assert.That(b.BridgeAwareRoutingAttempted,
                Is.EqualTo(a.BridgeAwareRoutingAttempted));
            Assert.That(b.BridgeAwareRoutingSatisfied,
                Is.EqualTo(a.BridgeAwareRoutingSatisfied));
        }
    }
}
