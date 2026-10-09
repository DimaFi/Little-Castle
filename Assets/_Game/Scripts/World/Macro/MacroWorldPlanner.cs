using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// One deterministic macro-planning implementation for both synchronous
    /// legacy callers and frame-budgeted cooperative bootstraps.
    /// Unity-dependent planners remain on the calling main thread.
    /// </summary>
    public sealed class MacroWorldPlanner
    {
        private readonly MacroWorldPlannerSettings settings;

        public MacroWorldPlanner(MacroWorldPlannerSettings settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// Start a fresh planning transaction. A partially built plan must not
        /// be used for gameplay, saves or player spawn assignments.
        /// </summary>
        public MacroPlanningSession BeginPlanning(
            int worldSeed,
            Rect worldBounds,
            WorldTerrainProbe terrainProbe = null)
        {
            return new MacroPlanningSession(
                settings, worldSeed, worldBounds, terrainProbe);
        }

        /// <summary>
        /// Preserves the existing synchronous API. The same session engine
        /// is driven to completion so sync/stepped paths cannot drift.
        /// Errors and bridge-aware routing failures still propagate.
        /// </summary>
        public MacroWorldPlan GenerateForBounds(
            int worldSeed,
            Rect worldBounds,
            WorldTerrainProbe terrainProbe = null)
        {
            return BeginPlanning(
                worldSeed, worldBounds, terrainProbe).RunToCompletion();
        }
    }
}
