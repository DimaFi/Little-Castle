using System;
using LittleCastle.World;

namespace LittleCastle.World
{
    /// <summary>
    /// A starting-area host is owned by the session/root integrator.
    /// It must use the SAME approved macro plan as the bootstrap transaction.
    /// Prepared data and visible mesh readiness are intentionally separate.
    /// </summary>
    public interface IWorldStartupAreaPreparation
    {
        void Begin(MacroWorldPlan completedPlan);
        float PreparedDataReadiness01 { get; }
        bool IsPreparedDataReady { get; }
        float VisibleAreaReadiness01 { get; }
        bool IsVisibleAreaReady { get; }
        void CancelPreparation();
    }

    /// <summary>
    /// Main-thread, stage-driven bootstrap gate. No fake total percentage:
    /// only phase-specific data/visible readiness and macro work counters.
    /// The plan is never published to the preparation adapter until the
    /// entire Q01 transaction completes, including route diagnostics.
    /// </summary>
    public sealed class WorldStartupBootstrapAdapter
    {
        public enum StartupPhase
        {
            MacroPlanning,
            StartingData,
            VisibleReady,
            Ready,
            Cancelled,
            Failed
        }

        private readonly MacroPlanningSession macro;
        private readonly IWorldStartupAreaPreparation preparation;
        private readonly Action releaseOwnedResources;
        private readonly int macroUnitsPerTick;
        private readonly double macroMillisecondsPerTick;
        private bool areaStarted;
        private bool cleaned;
        private Exception failure;

        public StartupPhase Phase { get; private set; }
        public bool IsReady => Phase == StartupPhase.Ready;
        public bool IsTerminal =>
            IsReady || Phase == StartupPhase.Cancelled ||
            Phase == StartupPhase.Failed;
        public bool CanAcceptGameInput => IsReady;
        public Exception Failure => failure;
        public MacroPlanningSession.PlanningPhase MacroPhase => macro.Phase;
        public long MacroPointCellsExamined => macro.PointCellsExamined;
        public int MacroWorkUnitsCompleted => macro.WorkUnitsCompleted;

        /// <summary>
        /// Only meaningful AFTER macro completes and data preparation begins.
        /// This must not be presented as a total bootstrap percentage.
        /// </summary>
        public float StartingDataReadiness01 =>
            areaStarted ? ClampReported(preparation.PreparedDataReadiness01) : 0f;

        public float VisibleAreaReadiness01 =>
            areaStarted ? ClampReported(preparation.VisibleAreaReadiness01) : 0f;

        public WorldStartupBootstrapAdapter(
            MacroPlanningSession macro,
            IWorldStartupAreaPreparation preparation,
            Action releaseOwnedResources = null,
            int macroUnitsPerTick = 64,
            double macroMillisecondsPerTick = 2d)
        {
            if (macro == null)
                throw new ArgumentNullException(nameof(macro));
            if (preparation == null)
                throw new ArgumentNullException(nameof(preparation));
            if (macroUnitsPerTick < 1 ||
                double.IsNaN(macroMillisecondsPerTick) ||
                macroMillisecondsPerTick <= 0d)
                throw new ArgumentOutOfRangeException(nameof(macroUnitsPerTick));

            this.macro = macro;
            this.preparation = preparation;
            this.releaseOwnedResources = releaseOwnedResources;
            this.macroUnitsPerTick = macroUnitsPerTick;
            this.macroMillisecondsPerTick = macroMillisecondsPerTick;
            Phase = StartupPhase.MacroPlanning;
        }

        /// <summary>Call at most once per Unity Update from the root/controller.</summary>
        public void Tick()
        {
            if (IsTerminal)
                return;

            try
            {
                switch (Phase)
                {
                    case StartupPhase.MacroPlanning:
                        if (macro.Phase == MacroPlanningSession.PlanningPhase.Cancelled)
                        {
                            Cancel();
                            return;
                        }
                        if (macro.Phase == MacroPlanningSession.PlanningPhase.Faulted)
                        {
                            Fail(macro.Failure ?? new InvalidOperationException(
                                "Macro plan faulted before bootstrap update."));
                            return;
                        }
                        if (!macro.IsCompleted)
                            macro.Step(macroUnitsPerTick, macroMillisecondsPerTick);

                        if (macro.IsCompleted)
                        {
                            MacroWorldPlan complete = macro.Result;
                            if (complete == null)
                                throw new InvalidOperationException(
                                    "Completed macro planning produced no valid result.");
                            // Begin must either succeed or the entire bootstrap
                            // enters a failure state and cancels the preparation.
                            areaStarted = true;
                            preparation.Begin(complete);
                            Phase = StartupPhase.StartingData;
                        }
                        break;
                    case StartupPhase.StartingData:
                        if (preparation.IsPreparedDataReady &&
                            ReportedReady(preparation.PreparedDataReadiness01))
                            Phase = StartupPhase.VisibleReady;
                        break;
                    case StartupPhase.VisibleReady:
                        if (!preparation.IsPreparedDataReady ||
                            !ReportedReady(preparation.PreparedDataReadiness01))
                        {
                            Phase = StartupPhase.StartingData;
                        }
                        else if (preparation.IsVisibleAreaReady &&
                                 ReportedReady(preparation.VisibleAreaReadiness01))
                        {
                            Phase = StartupPhase.Ready;
                        }
                        break;
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Cancel is idempotent. Never resurrect a half-built plan; the owner
        /// must release terrain probe caches and any independent start chunks.
        /// Cannot cancel a completed/handed-off playable session.
        /// </summary>
        public void Cancel()
        {
            if (IsTerminal)
                return;

            Phase = StartupPhase.Cancelled;
            if (!macro.IsTerminal)
                macro.Cancel();
            Cleanup();
        }

        private void Fail(Exception exception)
        {
            if (IsTerminal)
                return;

            failure = exception;
            Phase = StartupPhase.Failed;
            if (!macro.IsTerminal)
                macro.Cancel();
            Cleanup();
        }

        private void Cleanup()
        {
            if (cleaned)
                return;
            cleaned = true;

            try
            {
                preparation.CancelPreparation();
            }
            catch (Exception exception)
            {
                if (failure == null)
                    failure = exception;
                Phase = StartupPhase.Failed;
            }

            try
            {
                releaseOwnedResources?.Invoke();
            }
            catch (Exception exception)
            {
                if (failure == null)
                    failure = exception;
                Phase = StartupPhase.Failed;
            }
        }

        private static bool ReportedReady(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) &&
            value >= 0.9999f;

        private static float ClampReported(float value) =>
            float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : UnityEngine.Mathf.Clamp01(value);
    }

    /// <summary>
    /// Compatibility adapter for current WorldStreamer read-only readiness.
    /// Does NOT inject a macro plan into WorldStreamer (root-owned).
    /// Explicit identity check prevents a stale/full synchronous macro plan
    /// being mistaken for the completed cooperative transaction.
    /// </summary>
    public sealed class WorldStreamerStartingAreaPreparation :
        IWorldStartupAreaPreparation
    {
        private readonly WorldStreamer streamer;
        private readonly int preparedRadius;
        private bool started;

        public WorldStreamerStartingAreaPreparation(
            WorldStreamer streamer, int preparedRadius)
        {
            if (streamer == null)
                throw new ArgumentNullException(nameof(streamer));
            this.streamer = streamer;
            this.preparedRadius = UnityEngine.Mathf.Max(0, preparedRadius);
        }

        public void Begin(MacroWorldPlan completedPlan)
        {
            if (completedPlan == null ||
                !ReferenceEquals(streamer.MacroPlan, completedPlan))
                throw new InvalidOperationException(
                    "WorldStreamer must accept the exact validated macro plan " +
                    "before starting the starting-area readiness gate.");

            streamer.SetPriorityPreparationRadius(preparedRadius);
            started = true;
        }

        public float PreparedDataReadiness01 =>
            started ? streamer.GetPreparedDataReadiness01(preparedRadius) : 0f;

        public bool IsPreparedDataReady =>
            started && PreparedDataReadiness01 >= 0.9999f;

        public float VisibleAreaReadiness01 =>
            started ? streamer.VisibleReadiness01 : 0f;

        public bool IsVisibleAreaReady =>
            started && streamer.IsVisibleAreaReady;

        public void CancelPreparation()
        {
            if (!started)
                return;
            started = false;
            streamer.ClearPriorityPreparationRadius();
        }
    }
}
