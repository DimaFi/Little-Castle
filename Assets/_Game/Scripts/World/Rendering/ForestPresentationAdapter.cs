using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Reviewed asset-level permission to replace ONLY a decorative tree's
    /// presentation. Defaults to gameplay-protected if unconfigured.
    /// A resource tree MUST use RequiresGameplayPresence = true; there is
    /// no implicit "all trees are cheap decoration" shortcut.
    /// </summary>
    public readonly struct ForestAssetPresentationEligibility
    {
        public readonly bool requiresGameplayPresence;
        public readonly bool hasApprovedSilhouette;
        public readonly bool hasConfirmedForestCluster;

        public ForestAssetPresentationEligibility(
            bool requiresGameplayPresence,
            bool hasApprovedSilhouette,
            bool hasConfirmedForestCluster)
        {
            this.requiresGameplayPresence = requiresGameplayPresence;
            this.hasApprovedSilhouette = hasApprovedSilhouette;
            this.hasConfirmedForestCluster = hasConfirmedForestCluster;
        }

        public static ForestAssetPresentationEligibility SafeDefault =>
            new ForestAssetPresentationEligibility(true, false, false);
    }

    /// <summary>
    /// Read-only spawn-data bridge. Root presenter still owns actual prefab
    /// pooling, LODGroup, collider activation and cluster lifetimes.
    /// The adapter does not query/destroy/instantiate any GameObject.
    /// Evaluate a complete active-interest set, never "one frame's newly
    /// arrived chunk" for a purported global instance/collider quota.
    /// </summary>
    public sealed class ForestPresentationAdapter
    {
        private readonly List<ForestPresentationCandidate> scratch =
            new List<ForestPresentationCandidate>(256);
        private readonly ForestPresentationBudget budget =
            new ForestPresentationBudget();

        /// <summary>
        /// Only tree-category generated spawns are included. Deleted trees
        /// remain absent across regeneration because runtime deltas are
        /// filtered before any presentation decision is calculated.
        ///
        /// A null eligibility callback fails open to authored prefab +
        /// gameplay collider for every tree.
        /// </summary>
        public ForestPresentationMetrics EvaluateActiveChunks(
            IEnumerable<WorldChunkData> activeChunks,
            WorldRuntimeDeltaState runtimeDelta,
            Func<WorldSpawnData, ForestAssetPresentationEligibility>
                approvedEligibility,
            Vector2 viewpointXZ,
            int visualSeed,
            ForestPresentationSettings settings,
            List<ForestPresentationChoice> output)
        {
            if (activeChunks == null)
                throw new ArgumentNullException(nameof(activeChunks));

            scratch.Clear();
            foreach (WorldChunkData chunk in activeChunks)
            {
                if (chunk == null)
                    throw new ArgumentException(
                        "Active chunk collection contains a null entry.",
                        nameof(activeChunks));

                for (int i = 0; i < chunk.Spawns.Count; i++)
                {
                    WorldSpawnData spawn = chunk.Spawns[i];
                    if (spawn.category != SpawnCategory.Tree)
                        continue;
                    if (runtimeDelta != null &&
                        runtimeDelta.IsSpawnRemoved(spawn.stableId))
                        continue;

                    ForestAssetPresentationEligibility eligibility =
                        approvedEligibility != null
                            ? approvedEligibility(spawn)
                            : ForestAssetPresentationEligibility.SafeDefault;

                    scratch.Add(new ForestPresentationCandidate(
                        spawn.stableId,
                        new Vector2(
                            spawn.worldPosition.x,
                            spawn.worldPosition.z),
                        eligibility.requiresGameplayPresence,
                        eligibility.hasApprovedSilhouette,
                        eligibility.hasConfirmedForestCluster));
                }
            }

            return budget.Evaluate(
                scratch, viewpointXZ, visualSeed, settings, output);
        }
    }
}
