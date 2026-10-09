using System;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Pure root-adapter tests; no scene, streamer integration, remote
    /// services or real first-visit timings implied by these checks.
    /// </summary>
    public sealed class StartupCancellationTests
    {
        private sealed class PreparedArea : IWorldStartupAreaPreparation
        {
            public MacroWorldPlan plan;
            public int beginCalls;
            public int completeCalls;
            public int cancelCalls;
            public bool throwOnBegin;
            public bool throwOnComplete;
            public bool throwOnCancel;
            public bool throwOnDataReadiness;
            public float data = 0f;
            public float visible = 0f;
            public bool dataReady;
            public bool visibleReady;

            public void Begin(MacroWorldPlan completedPlan)
            {
                beginCalls++;
                if (throwOnBegin)
                    throw new InvalidOperationException("bad adapter begin");
                plan = completedPlan;
            }

            public void CompletePreparation()
            {
                completeCalls++;
                if (throwOnComplete)
                    throw new InvalidOperationException("priority reset failed");
            }

            public float PreparedDataReadiness01
            {
                get
                {
                    if (throwOnDataReadiness)
                        throw new InvalidOperationException("data source fault");
                    return data;
                }
            }

            public bool IsPreparedDataReady => dataReady;
            public float VisibleAreaReadiness01 => visible;
            public bool IsVisibleAreaReady => visibleReady;

            public void CancelPreparation()
            {
                cancelCalls++;
                plan = null;
                if (throwOnCancel)
                    throw new InvalidOperationException("cleanup failed");
            }
        }

        [Test]
        public void MacroDataVisible_AreDistinctGatesBeforeInputUnlock()
        {
            var area = new PreparedArea();
            var macro = new MacroWorldPlanner(null).BeginPlanning(
                5, new Rect(-16, -16, 32, 32));
            var boot = new WorldStartupBootstrapAdapter(macro, area);

            Assert.That(boot.CanAcceptGameInput, Is.False);
            Assert.That(boot.Phase,
                Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.MacroPlanning));
            Assert.That(boot.StartingDataReadiness01, Is.Zero);

            boot.Tick(); // no-rules macro complete; begin local prewarm
            Assert.That(area.beginCalls, Is.EqualTo(1));
            Assert.That(area.plan, Is.SameAs(macro.Result));
            Assert.That(boot.Phase,
                Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.StartingData));
            Assert.That(boot.CanAcceptGameInput, Is.False);

            area.data = 0.75f;
            area.visible = 1f;
            area.visibleReady = true;
            boot.Tick();
            Assert.That(boot.Phase,
                Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.StartingData),
                "Visible meshes alone must not substitute for prepared data.");

            area.dataReady = true;
            area.data = 1f;
            boot.Tick();
            Assert.That(boot.Phase,
                Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.VisibleReady));
            Assert.That(boot.CanAcceptGameInput, Is.False);

            boot.Tick();
            Assert.That(boot.IsReady, Is.True);
            Assert.That(boot.CanAcceptGameInput, Is.True);
            Assert.That(boot.IsTerminal, Is.True);
            Assert.That(area.beginCalls, Is.EqualTo(1));
            Assert.That(area.completeCalls, Is.EqualTo(1),
                "Release priority once after all gates are ready.");
            Assert.That(area.cancelCalls, Is.Zero,
                "Successful readiness must not cancel the playable world.");
            boot.Tick();
            Assert.That(area.completeCalls, Is.EqualTo(1));
        }

        [Test]
        public void CompletionFailure_FailsClosedAndStillCancels()
        {
            var area = new PreparedArea
            {
                data = 1f,
                dataReady = true,
                visible = 1f,
                visibleReady = true,
                throwOnComplete = true
            };
            var boot = Start(area);
            boot.Tick();
            boot.Tick();
            boot.Tick();
            Assert.That(boot.Phase,
                Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.Failed));
            Assert.That(area.completeCalls, Is.EqualTo(1));
            Assert.That(area.cancelCalls, Is.EqualTo(1));
            Assert.That(boot.CanAcceptGameInput, Is.False);
        }

        [Test]
        public void VisibleReadinessRegressesIfStartingDataBecomesUnavailable()
        {
            var area = new PreparedArea { data = 1f, dataReady = true };
            var boot = Start(area);
            boot.Tick(); // Macro -> StartingData
            boot.Tick(); // StartingData -> VisibleReady
            Assert.That(boot.Phase,
                Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.VisibleReady));

            area.dataReady = false;
            area.data = 0.25f;
            area.visibleReady = true;
            area.visible = 1f;
            boot.Tick();
            Assert.That(boot.Phase,
                Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.StartingData));
            Assert.That(boot.CanAcceptGameInput, Is.False);
        }

        [Test]
        public void CancelDuringMacro_RemovesPartialPlanAndReleasesOnce()
        {
            MacroWorldPlannerSettings settings = MakeManyFeatures();
            try
            {
                var macro = new MacroWorldPlanner(settings).BeginPlanning(
                    500, new Rect(-500, -500, 1000, 1000));
                var area = new PreparedArea();
                int releaseCalls = 0;
                var boot = new WorldStartupBootstrapAdapter(
                    macro, area, () => releaseCalls++, 1, 1d);

                boot.Tick();
                Assert.That(macro.PointCellsExamined, Is.EqualTo(1));
                Assert.That(macro.IsCompleted, Is.False);
                Assert.That(area.beginCalls, Is.Zero);
                Assert.That(boot.CanAcceptGameInput, Is.False);

                boot.Cancel();
                boot.Cancel();
                boot.Tick();

                Assert.That(boot.Phase,
                    Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.Cancelled));
                Assert.That(boot.IsTerminal, Is.True);
                Assert.That(boot.IsReady, Is.False);
                Assert.That(macro.Phase,
                    Is.EqualTo(MacroPlanningSession.PlanningPhase.Cancelled));
                Assert.That(macro.Result, Is.Null);
                Assert.That(area.cancelCalls, Is.EqualTo(1));
                Assert.That(releaseCalls, Is.EqualTo(1));
                Assert.That(area.beginCalls, Is.Zero);
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void CancelAfterMacro_DoesNotMarkMatchReady()
        {
            var area = new PreparedArea();
            int releaseCount = 0;
            var boot = new WorldStartupBootstrapAdapter(
                CompletedMacro(), area, () => releaseCount++);
            boot.Tick();
            Assert.That(area.beginCalls, Is.EqualTo(1));
            boot.Cancel();
            boot.Tick();
            Assert.That(boot.Phase,
                Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.Cancelled));
            Assert.That(area.cancelCalls, Is.EqualTo(1));
            Assert.That(releaseCount, Is.EqualTo(1));
            Assert.That(boot.CanAcceptGameInput, Is.False);
        }

        [Test]
        public void MacroAlreadyCancelled_MustNotStartWarmup()
        {
            MacroWorldPlannerSettings settings = MakeManyFeatures();
            try
            {
                MacroPlanningSession macro =
                    new MacroWorldPlanner(settings).BeginPlanning(
                        5, new Rect(-100, -100, 200, 200));
                macro.Cancel();
                var area = new PreparedArea();
                var boot = new WorldStartupBootstrapAdapter(macro, area);
                boot.Tick();
                Assert.That(boot.Phase,
                    Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.Cancelled));
                Assert.That(area.beginCalls, Is.Zero);
                Assert.That(area.cancelCalls, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void BeginFailure_FailsClosedAndRunsCleanupOnce()
        {
            var area = new PreparedArea { throwOnBegin = true };
            int releases = 0;
            var boot = new WorldStartupBootstrapAdapter(
                CompletedMacro(), area, () => releases++);

            Assert.DoesNotThrow(boot.Tick);
            Assert.That(boot.Phase,
                Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.Failed));
            Assert.That(boot.Failure, Is.TypeOf<InvalidOperationException>());
            Assert.That(boot.CanAcceptGameInput, Is.False);
            Assert.That(area.beginCalls, Is.EqualTo(1));
            Assert.That(area.cancelCalls, Is.EqualTo(1));
            Assert.That(releases, Is.EqualTo(1));

            boot.Tick();
            boot.Cancel();
            Assert.That(releases, Is.EqualTo(1));
        }

        [Test]
        public void ProbeOrStreamerReadinessFailure_FailsClosed()
        {
            var area = new PreparedArea
            {
                throwOnDataReadiness = true,
                dataReady = true
            };
            var boot = Start(area);
            boot.Tick();
            boot.Tick();
            Assert.That(boot.Phase,
                Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.Failed));
            Assert.That(area.cancelCalls, Is.EqualTo(1));
            Assert.That(boot.IsReady, Is.False);
        }

        [Test]
        public void CleanupFailureIsReportedAndCannotReleaseCamera()
        {
            var area = new PreparedArea { throwOnCancel = true };
            var boot = Start(area);
            boot.Tick();
            boot.Cancel();
            Assert.That(boot.Phase,
                Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.Failed));
            Assert.That(boot.Failure, Is.Not.Null);
            Assert.That(boot.CanAcceptGameInput, Is.False);
        }

        [Test]
        public void InvalidProgressIsNotTreatedAsReadyOrGlobalPercent()
        {
            var area = new PreparedArea
            {
                data = float.NaN,
                dataReady = true,
                visible = float.PositiveInfinity,
                visibleReady = true
            };
            var boot = Start(area);
            boot.Tick();
            Assert.That(boot.StartingDataReadiness01, Is.Zero);
            Assert.That(boot.VisibleAreaReadiness01, Is.Zero);
            boot.Tick();
            Assert.That(boot.Phase,
                Is.EqualTo(WorldStartupBootstrapAdapter.StartupPhase.StartingData));
            Assert.That(boot.IsReady, Is.False);

            area.data = 2f;
            Assert.That(boot.StartingDataReadiness01, Is.EqualTo(1f));
        }

        [Test]
        public void InvalidMacroWorkBudget_IsRejectedAtConstruction()
        {
            MacroPlanningSession macro = CompletedMacro();
            var area = new PreparedArea();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldStartupBootstrapAdapter(macro, area, null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldStartupBootstrapAdapter(macro, area, null, 2, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldStartupBootstrapAdapter(macro, area, null, 2,
                    double.NaN));
            Assert.Throws<ArgumentNullException>(() =>
                new WorldStartupBootstrapAdapter(null, area));
            Assert.Throws<ArgumentNullException>(() =>
                new WorldStartupBootstrapAdapter(macro, null));
        }

        [Test]
        public void RootStreamerAdapterRejectsUnboundOrStaleMacroPlan()
        {
            var root = new GameObject("Q02StreamGate");
            try
            {
                var streamer = root.AddComponent<WorldStreamer>();
                var area = new WorldStreamerStartingAreaPreparation(streamer, 2);
                Assert.Throws<InvalidOperationException>(() =>
                    area.Begin(CompletedMacro().Result));
                Assert.That(area.IsPreparedDataReady, Is.False);
                Assert.That(area.IsVisibleAreaReady, Is.False);
                Assert.DoesNotThrow(area.CancelPreparation);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static MacroPlanningSession CompletedMacro() =>
            new MacroWorldPlanner(null).BeginPlanning(
                1, new Rect(0, 0, 100, 100));

        private static WorldStartupBootstrapAdapter Start(
            PreparedArea area) =>
            new WorldStartupBootstrapAdapter(CompletedMacro(), area);

        private static MacroWorldPlannerSettings MakeManyFeatures()
        {
            var settings =
                ScriptableObject.CreateInstance<MacroWorldPlannerSettings>();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("planningHalo").floatValue = 0f;
            var rules = serialized.FindProperty("pointFeatureRules");
            rules.arraySize = 1;
            var rule = rules.GetArrayElementAtIndex(0);
            rule.FindPropertyRelative("ruleId").stringValue = "q02_test";
            rule.FindPropertyRelative("spacing").floatValue = 50f;
            rule.FindPropertyRelative("chance").floatValue = 1f;
            rule.FindPropertyRelative("avoidOtherPointFeatures")
                .boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return settings;
        }
    }
}
