using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Presentation policy only. Never modifies WorldSpawnData, the authored
    /// tree's LODGroup, resource availability, saves, or collision authority.
    /// Quantities are initial screening knobs, NOT measured GPU budgets.
    /// </summary>
    [Serializable]
    public struct ForestPresentationSettings
    {
        public float nearDistanceMeters;
        public float distantDistanceMeters;
        public float optionalColliderDistanceMeters;
        public int maxFullPrefabInstances;
        public int maxOptionalDecorativeColliders;
        public float clusterCoveredFarRetention;

        public bool IsValid =>
            Finite(nearDistanceMeters) && nearDistanceMeters > 0f &&
            Finite(distantDistanceMeters) &&
            distantDistanceMeters > nearDistanceMeters &&
            Finite(optionalColliderDistanceMeters) &&
            optionalColliderDistanceMeters >= 0f &&
            optionalColliderDistanceMeters <= nearDistanceMeters &&
            maxFullPrefabInstances >= 0 &&
            maxOptionalDecorativeColliders >= 0 &&
            Finite(clusterCoveredFarRetention) &&
            clusterCoveredFarRetention >= 0f &&
            clusterCoveredFarRetention <= 1f;

        private static bool Finite(float x) =>
            !float.IsNaN(x) && !float.IsInfinity(x);
    }

    /// <summary>
    /// Per-tree permissions come from a reviewed presenter catalog, NEVER
    /// inferred from SpawnCategory.Tree. Gameplay trees may be chopped/used:
    /// their identity and collider requirements must not be budgeted away.
    /// </summary>
    public readonly struct ForestPresentationCandidate
    {
        public readonly long stableId;
        public readonly Vector2 worldXZ;
        public readonly bool requiresGameplayPresence;
        public readonly bool hasApprovedSilhouette;
        public readonly bool hasConfirmedForestCluster;

        public ForestPresentationCandidate(
            long stableId,
            Vector2 worldXZ,
            bool requiresGameplayPresence,
            bool hasApprovedSilhouette,
            bool hasConfirmedForestCluster)
        {
            this.stableId = stableId;
            this.worldXZ = worldXZ;
            this.requiresGameplayPresence = requiresGameplayPresence;
            this.hasApprovedSilhouette = hasApprovedSilhouette;
            this.hasConfirmedForestCluster = hasConfirmedForestCluster;
        }
    }

    public enum ForestPresentationKind : byte
    {
        AuthoredPrefab = 0,
        ApprovedSilhouette = 1,
        CoveredByForestCluster = 2
    }

    public readonly struct ForestPresentationChoice
    {
        public readonly long stableId;
        public readonly ForestPresentationKind presentation;
        public readonly bool retainGameplayCollider;
        public readonly bool allowOptionalVisualCollider;
        public readonly bool isInsideNearBand;

        public ForestPresentationChoice(
            long stableId,
            ForestPresentationKind presentation,
            bool retainGameplayCollider,
            bool allowOptionalVisualCollider,
            bool isInsideNearBand)
        {
            this.stableId = stableId;
            this.presentation = presentation;
            this.retainGameplayCollider = retainGameplayCollider;
            this.allowOptionalVisualCollider = allowOptionalVisualCollider;
            this.isInsideNearBand = isInsideNearBand;
        }
    }

    public readonly struct ForestPresentationMetrics
    {
        public readonly int candidates;
        public readonly int fullPrefabs;
        public readonly int silhouettes;
        public readonly int clusterCovered;
        public readonly int gameplayProtected;
        public readonly int optionalColliders;
        public readonly int fullPrefabBudgetOverflow;

        internal ForestPresentationMetrics(
            int candidates,
            int fullPrefabs,
            int silhouettes,
            int clusterCovered,
            int gameplayProtected,
            int optionalColliders,
            int fullPrefabBudgetOverflow)
        {
            this.candidates = candidates;
            this.fullPrefabs = fullPrefabs;
            this.silhouettes = silhouettes;
            this.clusterCovered = clusterCovered;
            this.gameplayProtected = gameplayProtected;
            this.optionalColliders = optionalColliders;
            this.fullPrefabBudgetOverflow = fullPrefabBudgetOverflow;
        }
    }

    /// <summary>
    /// Deterministic view-centric candidate selector with reusable scratch.
    /// Caller must evaluate one coherent visible-interest set at a time.
    /// If called separately per chunk, quotas are PER CHUNK, not global.
    /// Not thread-safe; no GameObjects, GPU API, or async work.
    /// </summary>
    public sealed class ForestPresentationBudget
    {
        private const int FarMaskSalt = 0x464F5245;
        private readonly List<Evaluated> scratch =
            new List<Evaluated>(128);
        private readonly HashSet<long> observedIds =
            new HashSet<long>();

        private struct Evaluated
        {
            public ForestPresentationCandidate input;
            public double distanceSquared;
            public ForestPresentationKind kind;
            public bool optionalCollider;
            public bool near;
        }

        private sealed class NearestFirst : IComparer<Evaluated>
        {
            public static readonly NearestFirst Instance = new NearestFirst();
            public int Compare(Evaluated x, Evaluated y)
            {
                int byDistance = x.distanceSquared.CompareTo(y.distanceSquared);
                return byDistance != 0
                    ? byDistance
                    : x.input.stableId.CompareTo(y.input.stableId);
            }
        }

        private sealed class IdFirst : IComparer<Evaluated>
        {
            public static readonly IdFirst Instance = new IdFirst();
            public int Compare(Evaluated x, Evaluated y) =>
                x.input.stableId.CompareTo(y.input.stableId);
        }

        /// <summary>
        /// Caller-owned output is untouched when input validation fails.
        /// Existing contents are replaced only after all inputs are checked.
        /// The order of final choices is stableId ascending, regardless of
        /// original spawn iteration order / streamed chunk arrival order.
        /// </summary>
        public ForestPresentationMetrics Evaluate(
            IReadOnlyList<ForestPresentationCandidate> candidates,
            Vector2 viewpointXZ,
            int visualSeed,
            ForestPresentationSettings settings,
            List<ForestPresentationChoice> output)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (!settings.IsValid || !Finite(viewpointXZ.x) ||
                !Finite(viewpointXZ.y))
                throw new ArgumentException(
                    "Invalid forest presentation settings/viewpoint.");

            scratch.Clear();
            observedIds.Clear();

            // Validate the entire set before touching caller output.
            for (int i = 0; i < candidates.Count; i++)
            {
                ForestPresentationCandidate candidate = candidates[i];
                if (!Finite(candidate.worldXZ.x) ||
                    !Finite(candidate.worldXZ.y) ||
                    !observedIds.Add(candidate.stableId))
                    throw new ArgumentException(
                        "Nonfinite tree position or duplicate stable ID.");

                double dx = (double)candidate.worldXZ.x - viewpointXZ.x;
                double dz = (double)candidate.worldXZ.y - viewpointXZ.y;
                scratch.Add(new Evaluated
                {
                    input = candidate,
                    distanceSquared = dx * dx + dz * dz
                });
            }

            scratch.Sort(NearestFirst.Instance);

            double nearSquared =
                (double)settings.nearDistanceMeters *
                settings.nearDistanceMeters;
            double distantSquared =
                (double)settings.distantDistanceMeters *
                settings.distantDistanceMeters;
            double optionalColliderSquared =
                (double)settings.optionalColliderDistanceMeters *
                settings.optionalColliderDistanceMeters;

            int fullPrefabs = 0;
            int silhouettes = 0;
            int clusterCovered = 0;
            int gameplayProtected = 0;
            int optionalColliders = 0;
            int overBudget = 0;

            for (int i = 0; i < scratch.Count; i++)
            {
                Evaluated item = scratch[i];
                bool near = item.distanceSquared <= nearSquared;
                bool far = item.distanceSquared > distantSquared;
                bool gameplay = item.input.requiresGameplayPresence;

                // Nothing can suppress a gameplay-relevant generated tree.
                // It remains present through the normal authored LODGroup;
                // its gameplay collider remains owned by the root system.
                ForestPresentationKind chosen =
                    ForestPresentationKind.AuthoredPrefab;

                if (!gameplay && !near)
                {
                    if (far && item.input.hasConfirmedForestCluster &&
                        !KeepIndividualFarTree(
                            visualSeed, item.input.stableId,
                            settings.clusterCoveredFarRetention))
                    {
                        chosen = ForestPresentationKind.CoveredByForestCluster;
                    }
                    else if (item.input.hasApprovedSilhouette &&
                             (far ||
                              fullPrefabs >= settings.maxFullPrefabInstances))
                    {
                        chosen = ForestPresentationKind.ApprovedSilhouette;
                    }
                }

                if (chosen == ForestPresentationKind.AuthoredPrefab)
                {
                    fullPrefabs++;
                    if (fullPrefabs > settings.maxFullPrefabInstances)
                        overBudget++;
                }
                else if (chosen == ForestPresentationKind.ApprovedSilhouette)
                    silhouettes++;
                else
                    clusterCovered++;

                if (gameplay)
                    gameplayProtected++;

                bool optional = !gameplay && near &&
                    item.distanceSquared <= optionalColliderSquared &&
                    optionalColliders <
                        settings.maxOptionalDecorativeColliders;
                if (optional)
                    optionalColliders++;

                item.near = near;
                item.kind = chosen;
                item.optionalCollider = optional;
                scratch[i] = item;
            }

            scratch.Sort(IdFirst.Instance);
            output.Clear();
            if (output.Capacity < scratch.Count)
                output.Capacity = scratch.Count;

            foreach (Evaluated item in scratch)
            {
                output.Add(new ForestPresentationChoice(
                    item.input.stableId,
                    item.kind,
                    item.input.requiresGameplayPresence,
                    item.optionalCollider,
                    item.near));
            }

            return new ForestPresentationMetrics(
                scratch.Count,
                fullPrefabs, silhouettes, clusterCovered,
                gameplayProtected, optionalColliders, overBudget);
        }

        private static bool KeepIndividualFarTree(
            int worldSeed, long id, float retention)
        {
            if (retention >= 1f)
                return true;
            if (retention <= 0f)
                return false;
            int low = unchecked((int)id);
            int high = unchecked((int)(id >> 32));
            return DeterministicHash.Hash01(
                worldSeed, low, high, FarMaskSalt) < retention;
        }

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
