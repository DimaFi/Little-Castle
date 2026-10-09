using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Cooperative, main-thread macro planning transaction. Each Step consumes
    /// a bounded number of point-grid cells / downstream pipeline stages.
    /// Completed plans are exposed only after routing validation.
    ///
    /// WARNING: legacy River/Road/Bridge planners are still atomic work units,
    /// and can exceed a per-frame millisecond budget. This session is a
    /// safe stepping foundation; it does not pretend to make those stages
    /// interruptible before their owners supply incremental variants.
    /// </summary>
    public sealed class MacroPlanningSession
    {
        public enum PlanningPhase
        {
            PointFeatures,
            Rivers,
            RoadConnections,
            ConnectivityBackbone,
            RoadPaths,
            CaptureDesiredConnections,
            BridgeSites,
            RecoverRejectedConnections,
            RepairRoadConnectivity,
            ValidateRouting,
            Completed,
            Cancelled,
            Faulted
        }

        private readonly MacroWorldPlannerSettings settings;
        private readonly WorldTerrainProbe terrainProbe;
        private readonly Rect planningBounds;
        private readonly int worldSeed;
        private MacroWorldPlan workingPlan;
        private readonly bool bridgeAware;
        private readonly Stopwatch elapsed = new Stopwatch();

        private MacroPointFeatureRule currentRule;
        private int ruleIndex;
        private int minGridX;
        private int maxGridX;
        private int maxGridZ;
        private int nextGridX;
        private int nextGridZ;
        private float spacing;
        private float jitterMargin;
        private int ruleSalt;

        private List<WorldRoadConnectionData> desiredConnections;
        private BridgeAwareRoutingPlanner.RecoveryResult recovery;
        private RealizedRoadConnectivityRepair.Result repair;
        private Exception failure;

        public PlanningPhase Phase { get; private set; }
        public int WorkUnitsCompleted { get; private set; }
        public long PointCellsExamined { get; private set; }
        public double ElapsedMilliseconds => elapsed.Elapsed.TotalMilliseconds;
        public Exception Failure => failure;
        public bool IsCompleted => Phase == PlanningPhase.Completed;
        public bool IsTerminal =>
            IsCompleted ||
            Phase == PlanningPhase.Cancelled ||
            Phase == PlanningPhase.Faulted;

        /// <summary>
        /// Never exposes partially routed/validated macro data to consumers.
        /// </summary>
        public MacroWorldPlan Result =>
            IsCompleted ? workingPlan : null;

        internal MacroPlanningSession(
            MacroWorldPlannerSettings settings,
            int worldSeed,
            Rect bounds,
            WorldTerrainProbe terrainProbe)
        {
            this.settings = settings;
            this.worldSeed = worldSeed;
            this.terrainProbe = terrainProbe;
            workingPlan = new MacroWorldPlan(worldSeed);
            planningBounds = settings != null
                ? ExpandRect(bounds, settings.PlanningHalo)
                : bounds;

            bridgeAware =
                settings != null &&
                terrainProbe != null &&
                settings.RoadPaths != null &&
                settings.RoadPaths.enabled &&
                settings.Bridges != null &&
                settings.Bridges.enabled &&
                settings.Bridges.useFixedStoneBridgeSites &&
                settings.Bridges.enableBridgeAwareRouting;

            Phase = settings == null
                ? PlanningPhase.Completed
                : PlanningPhase.PointFeatures;
        }

        /// <summary>
        /// Advances work on the caller's Unity main thread. Budget is soft:
        /// an existing non-incremental planner stage remains atomic. At
        /// least one unit is attempted when nonterminal, even if budget is
        /// smaller than that unit. Do not invoke concurrently or mutate
        /// settings/probe while this session is running.
        /// </summary>
        public int Step(
            int maxWorkUnits = 1,
            double maxMilliseconds = double.PositiveInfinity)
        {
            if (maxWorkUnits <= 0 ||
                double.IsNaN(maxMilliseconds) ||
                maxMilliseconds <= 0d)
                throw new ArgumentOutOfRangeException(
                    nameof(maxWorkUnits),
                    "Work units and time budget must be positive.");

            if (IsTerminal)
                return 0;

            var budgetClock = Stopwatch.StartNew();
            if (!elapsed.IsRunning)
                elapsed.Start();

            int executed = 0;
            try
            {
                while (!IsTerminal && executed < maxWorkUnits)
                {
                    if (executed > 0 &&
                        budgetClock.Elapsed.TotalMilliseconds >= maxMilliseconds)
                        break;

                    ExecuteOneUnit();
                    executed++;
                    WorkUnitsCompleted++;
                }
            }
            catch (Exception exception)
            {
                failure = exception;
                workingPlan = null;
                desiredConnections = null;
                Phase = PlanningPhase.Faulted;
                elapsed.Stop();
                throw;
            }

            if (IsTerminal)
                elapsed.Stop();
            return executed;
        }

        public MacroWorldPlan RunToCompletion()
        {
            if (Phase == PlanningPhase.Cancelled ||
                Phase == PlanningPhase.Faulted)
                throw new InvalidOperationException(
                    "Cannot complete a cancelled or faulted planning session.",
                    failure);

            while (!IsTerminal)
                Step(4096, double.PositiveInfinity);

            return Result;
        }

        public void Cancel()
        {
            if (IsTerminal)
                return;

            // No transaction commit: a cancelled partially built world is
            // never returned as a valid, playable strategic world plan.
            workingPlan = null;
            desiredConnections = null;
            currentRule = null;
            Phase = PlanningPhase.Cancelled;
            elapsed.Stop();

            // The optional probe is caller owned. Its cache lifecycle and
            // disposal belong to the root bootstrap coordinator.
        }

        private void ExecuteOneUnit()
        {
            switch (Phase)
            {
                case PlanningPhase.PointFeatures:
                    StepPointFeatures();
                    break;
                case PlanningPhase.Rivers:
                    if (terrainProbe != null)
                        RiverNetworkPlanner.BuildRivers(
                            worldSeed, planningBounds, terrainProbe,
                            settings.Rivers, workingPlan);
                    Phase = PlanningPhase.RoadConnections;
                    break;
                case PlanningPhase.RoadConnections:
                    RoadNetworkPlanner.BuildConnections(
                        worldSeed, workingPlan, settings.RoadNetwork);
                    Phase = PlanningPhase.ConnectivityBackbone;
                    break;
                case PlanningPhase.ConnectivityBackbone:
                    if (bridgeAware &&
                        settings.Bridges.requireConnectedFeatureGraph)
                        RoadNetworkPlanner.BuildConnectivityBackbone(
                            worldSeed, workingPlan, settings.RoadNetwork);
                    Phase = PlanningPhase.RoadPaths;
                    break;
                case PlanningPhase.RoadPaths:
                    if (terrainProbe != null)
                        TerrainRoadPathPlanner.BuildRoadPaths(
                            workingPlan, terrainProbe, settings.RoadPaths);
                    Phase = PlanningPhase.CaptureDesiredConnections;
                    break;
                case PlanningPhase.CaptureDesiredConnections:
                    desiredConnections = bridgeAware
                        ? new List<WorldRoadConnectionData>(
                            workingPlan.RoadConnections)
                        : null;
                    Phase = PlanningPhase.BridgeSites;
                    break;
                case PlanningPhase.BridgeSites:
                    BridgeSitePlanner.BuildBridgeSites(
                        worldSeed, workingPlan, settings.Bridges, terrainProbe);
                    Phase = PlanningPhase.RecoverRejectedConnections;
                    break;
                case PlanningPhase.RecoverRejectedConnections:
                    if (bridgeAware)
                        recovery =
                            BridgeAwareRoutingPlanner.RecoverRejectedConnections(
                                worldSeed, workingPlan, desiredConnections,
                                terrainProbe, settings.RoadPaths,
                                settings.Bridges);
                    Phase = PlanningPhase.RepairRoadConnectivity;
                    break;
                case PlanningPhase.RepairRoadConnectivity:
                    if (bridgeAware)
                        repair = RealizedRoadConnectivityRepair.Repair(
                            worldSeed, workingPlan, terrainProbe,
                            settings.RoadNetwork, settings.RoadPaths,
                            settings.Bridges, recovery.failedIds);
                    Phase = PlanningPhase.ValidateRouting;
                    break;
                case PlanningPhase.ValidateRouting:
                    ValidateRouting();
                    Phase = PlanningPhase.Completed;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Cannot execute planning phase " + Phase);
            }
        }

        private void ValidateRouting()
        {
            if (!bridgeAware)
                return;

            var diagnostics = WorldRouteConnectivityValidator.Validate(
                workingPlan,
                settings.Bridges.minimumFixedBridgeCount,
                settings.Bridges.requireConnectedFeatureGraph);

            diagnostics.originalRejectedConnections =
                recovery.failedConnections;
            diagnostics.repairInitialComponents =
                repair.initialComponents;
            diagnostics.repairAttemptedCandidates =
                repair.candidatesConsidered;
            diagnostics.repairAcceptedConnections =
                repair.acceptedConnections;
            diagnostics.repairPathAttempts =
                repair.routeAttempts;

            if (recovery.failedConnections > 0 &&
                (!settings.Bridges.requireConnectedFeatureGraph ||
                 diagnostics.components > 1))
                diagnostics.Fail(
                    "Unable to realize " + recovery.failedConnections +
                    " requested road connections after " +
                    recovery.pathAttempts + " bounded retries; " +
                    "alternate repair: " + repair.Summary);

            workingPlan.RouteDiagnostics = diagnostics;
            workingPlan.BridgeAwareRoutingAttempted = true;
            workingPlan.BridgeAwareRoutingSatisfied = diagnostics.IsValid;

            if (!diagnostics.IsValid)
            {
                string message =
                    "Bridge-aware macro routing failed: " +
                    recovery.Summary + "; " +
                    "repair=" + repair.Summary + "; " +
                    diagnostics.Summary + "; " +
                    string.Join("; ", diagnostics.errors);

                if (settings.Bridges.failOnRoutingError)
                    throw new InvalidOperationException(message);

                UnityEngine.Debug.LogWarning(message);
            }
        }

        private void StepPointFeatures()
        {
            IReadOnlyList<MacroPointFeatureRule> rules =
                settings.PointFeatureRules;
            if (ruleIndex >= rules.Count)
            {
                Phase = PlanningPhase.Rivers;
                return;
            }

            if (currentRule == null)
            {
                MacroPointFeatureRule rule = rules[ruleIndex];
                if (rule == null)
                {
                    ruleIndex++;
                    return;
                }

                currentRule = rule;
                spacing = Mathf.Max(50f, rule.spacing);
                ruleSalt = DeterministicHash.String32(
                    string.IsNullOrWhiteSpace(rule.ruleId)
                        ? rule.kind.ToString()
                        : rule.ruleId);
                minGridX =
                    Mathf.FloorToInt(planningBounds.xMin / spacing) - 1;
                maxGridX =
                    Mathf.FloorToInt(planningBounds.xMax / spacing) + 1;
                nextGridZ =
                    Mathf.FloorToInt(planningBounds.yMin / spacing) - 1;
                maxGridZ =
                    Mathf.FloorToInt(planningBounds.yMax / spacing) + 1;
                nextGridX = minGridX;
                jitterMargin = Mathf.Clamp(rule.borderJitter, 0f, 0.45f);
            }

            AppendCandidate(nextGridX, nextGridZ);
            PointCellsExamined++;

            if (nextGridX < maxGridX)
            {
                nextGridX++;
            }
            else
            {
                nextGridX = minGridX;
                if (nextGridZ < maxGridZ)
                    nextGridZ++;
                else
                {
                    currentRule = null;
                    ruleIndex++;
                }
            }
        }

        private void AppendCandidate(int gx, int gz)
        {
            MacroPointFeatureRule rule = currentRule;
            float roll = DeterministicHash.Hash01(
                worldSeed, gx, gz, ruleSalt ^ 0x101);
            if (roll > rule.chance)
                return;

            float jx = Mathf.Lerp(
                jitterMargin, 1f - jitterMargin,
                DeterministicHash.Hash01(
                    worldSeed, gx, gz, ruleSalt ^ 0x102));
            float jz = Mathf.Lerp(
                jitterMargin, 1f - jitterMargin,
                DeterministicHash.Hash01(
                    worldSeed, gx, gz, ruleSalt ^ 0x103));
            var position = new Vector2(
                (gx + jx) * spacing,
                (gz + jz) * spacing);

            if (!planningBounds.Contains(position))
                return;

            if (terrainProbe != null)
            {
                WorldTerrainSample sample = terrainProbe.Sample(position);
                if (!rule.allowedTerrain.Contains(sample.terrainClass) ||
                    sample.height < rule.minHeight ||
                    sample.height > rule.maxHeight ||
                    sample.slope > rule.maxSlope)
                    return;
            }

            if (rule.avoidOtherPointFeatures &&
                !HasSeparation(workingPlan, position,
                    rule.influenceRadius, rule.separationPadding))
                return;

            long id = DeterministicHash.StableId(
                worldSeed, gx, gz, ruleSalt);

            workingPlan.AddPointFeature(
                new WorldPointFeatureData(
                    id, rule.kind, rule.archetypeId,
                    position, rule.influenceRadius));
        }

        private static bool HasSeparation(
            MacroWorldPlan plan,
            Vector2 position,
            float influenceRadius,
            float padding)
        {
            for (int i = 0; i < plan.PointFeatures.Count; i++)
            {
                WorldPointFeatureData existing = plan.PointFeatures[i];
                float required =
                    Mathf.Max(0f, influenceRadius) +
                    Mathf.Max(0f, existing.influenceRadius) +
                    Mathf.Max(0f, padding);
                if ((existing.worldPosition - position).sqrMagnitude <
                    required * required)
                    return false;
            }
            return true;
        }

        private static Rect ExpandRect(Rect rect, float padding)
        {
            float safe = Mathf.Max(0f, padding);
            rect.xMin -= safe;
            rect.xMax += safe;
            rect.yMin -= safe;
            rect.yMax += safe;
            return rect;
        }
    }
}
