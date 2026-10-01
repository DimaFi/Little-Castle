using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Deterministically selects fair player start areas from an already
    /// generated random finite world.
    ///
    /// This planner does not inject or move resources. If the generated world
    /// cannot provide enough comparable starts for the requested player count,
    /// the seed is rejected before the match starts.
    /// </summary>
    public sealed class WorldStartFairnessPlanner
    {
        private sealed class RawCandidate
        {
            public int gridX;
            public int gridZ;
            public long stableId;
            public float priority;
            public Vector2 worldPosition;
        }

        private sealed class EvaluatedCandidate
        {
            public long stableId;
            public Vector3 worldPosition;
            public WorldStartAreaMetrics metrics;
            public float quality;
        }

        private readonly WorldStartFairnessSettings fairnessSettings;
        private readonly WorldGenerationSettings generationSettings;
        private readonly WorldGenerationPipeline pipeline;
        private readonly int worldSeed;

        private readonly Dictionary<ChunkCoordinate, WorldChunkData> chunkCache =
            new Dictionary<ChunkCoordinate, WorldChunkData>();

        public WorldStartFairnessPlanner(
            WorldStartFairnessSettings fairnessSettings,
            WorldGenerationSettings generationSettings,
            MacroWorldPlan macroPlan,
            int worldSeed)
        {
            this.fairnessSettings =
                fairnessSettings ??
                throw new ArgumentNullException(nameof(fairnessSettings));

            this.generationSettings =
                generationSettings ??
                throw new ArgumentNullException(nameof(generationSettings));

            this.worldSeed = worldSeed;

            pipeline =
                new WorldGenerationPipeline(
                    generationSettings,
                    macroPlan);
        }

        public WorldStartFairnessReport Plan(
            WorldSessionMap sessionMap)
        {
            if (sessionMap == null)
                throw new ArgumentNullException(nameof(sessionMap));

            var report =
                new WorldStartFairnessReport
                {
                    requestedPlayerCount =
                        Mathf.Max(
                            1,
                            sessionMap.playerCount)
                };

            Rect playableRect =
                sessionMap.playableChunks.ToWorldRect(
                    generationSettings.ChunkWorldSize);

            List<RawCandidate> rawCandidates =
                BuildRawCandidates(
                    playableRect);

            report.candidatesGenerated =
                rawCandidates.Count;

            var viable =
                new List<EvaluatedCandidate>();

            int evaluateCount =
                Mathf.Min(
                    rawCandidates.Count,
                    fairnessSettings.MaximumCandidatesToEvaluate);

            for (int i = 0;
                 i < evaluateCount;
                 i++)
            {
                RawCandidate raw =
                    rawCandidates[i];

                if (TryEvaluateCandidate(
                        raw,
                        playableRect,
                        out EvaluatedCandidate candidate))
                {
                    viable.Add(candidate);
                }
            }

            report.viableCandidates =
                viable.Count;

            int playerCount =
                report.requestedPlayerCount;

            if (viable.Count < playerCount)
            {
                Reject(
                    report,
                    "Only " +
                    viable.Count +
                    " viable start areas were found for " +
                    playerCount +
                    " players.");

                return report;
            }

            float minimumStartDistance =
                CalculateMinimumStartDistance(
                    playableRect,
                    playerCount);

            report.minimumStartDistance =
                minimumStartDistance;

            List<EvaluatedCandidate> selected =
                SelectStarts(
                    viable,
                    playerCount,
                    minimumStartDistance);

            if (selected.Count < playerCount)
            {
                Reject(
                    report,
                    "The map has enough individually viable starts, but not " +
                    "enough starts with the required separation for " +
                    playerCount +
                    " players.");

                return report;
            }

            AssignPlayerIndices(
                selected,
                report);

            CalculateSelectedScoreSummary(
                report);

            if (report.selectedScoreSpread >
                fairnessSettings.MaximumAcceptedScoreSpread)
            {
                Reject(
                    report,
                    "Selected start quality spread is " +
                    report.selectedScoreSpread.ToString("F3") +
                    ", above the allowed " +
                    fairnessSettings.MaximumAcceptedScoreSpread.ToString("F3") +
                    ".");

                return report;
            }

            report.accepted = true;
            report.rejectionReason = string.Empty;
            return report;
        }

        private List<RawCandidate> BuildRawCandidates(
            Rect playableRect)
        {
            float spacing =
                fairnessSettings.CandidateSpacing;

            float requiredMargin =
                Mathf.Max(
                    fairnessSettings.PlayableEdgeMargin,
                    fairnessSettings.BuildAreaRadius);

            float maximumMarginX =
                Mathf.Max(
                    0f,
                    playableRect.width * 0.5f -
                    spacing * 0.5f);

            float maximumMarginZ =
                Mathf.Max(
                    0f,
                    playableRect.height * 0.5f -
                    spacing * 0.5f);

            float marginX =
                Mathf.Min(
                    requiredMargin,
                    maximumMarginX);

            float marginZ =
                Mathf.Min(
                    requiredMargin,
                    maximumMarginZ);

            var candidateRect =
                Rect.MinMaxRect(
                    playableRect.xMin + marginX,
                    playableRect.yMin + marginZ,
                    playableRect.xMax - marginX,
                    playableRect.yMax - marginZ);

            int salt =
                DeterministicHash.String32(
                    "player_start_candidate");

            int minGridX =
                Mathf.FloorToInt(
                    candidateRect.xMin /
                    spacing) - 1;

            int maxGridX =
                Mathf.CeilToInt(
                    candidateRect.xMax /
                    spacing) + 1;

            int minGridZ =
                Mathf.FloorToInt(
                    candidateRect.yMin /
                    spacing) - 1;

            int maxGridZ =
                Mathf.CeilToInt(
                    candidateRect.yMax /
                    spacing) + 1;

            var result =
                new List<RawCandidate>();

            for (int gz = minGridZ;
                 gz <= maxGridZ;
                 gz++)
            {
                for (int gx = minGridX;
                     gx <= maxGridX;
                     gx++)
                {
                    float jitterX =
                        Mathf.Lerp(
                            0.15f,
                            0.85f,
                            DeterministicHash.Hash01(
                                worldSeed,
                                gx,
                                gz,
                                salt ^ 0x31));

                    float jitterZ =
                        Mathf.Lerp(
                            0.15f,
                            0.85f,
                            DeterministicHash.Hash01(
                                worldSeed,
                                gx,
                                gz,
                                salt ^ 0x32));

                    var position =
                        new Vector2(
                            (gx + jitterX) *
                            spacing,
                            (gz + jitterZ) *
                            spacing);

                    if (!candidateRect.Contains(
                            position))
                    {
                        continue;
                    }

                    long stableId =
                        DeterministicHash.StableId(
                            worldSeed,
                            gx,
                            gz,
                            salt);

                    float priority =
                        DeterministicHash.Hash01(
                            worldSeed,
                            gx,
                            gz,
                            salt ^ 0x33);

                    result.Add(
                        new RawCandidate
                        {
                            gridX = gx,
                            gridZ = gz,
                            stableId = stableId,
                            priority = priority,
                            worldPosition = position
                        });
                }
            }

            result.Sort(
                delegate(
                    RawCandidate a,
                    RawCandidate b)
                {
                    int compare =
                        a.priority.CompareTo(
                            b.priority);

                    if (compare != 0)
                        return compare;

                    return
                        a.stableId.CompareTo(
                            b.stableId);
                });

            return result;
        }

        private bool TryEvaluateCandidate(
            RawCandidate raw,
            Rect playableRect,
            out EvaluatedCandidate candidate)
        {
            candidate = null;

            Vector2 center =
                raw.worldPosition;

            WorldChunkData centerChunk =
                GetChunk(
                    center);

            float centerSlope =
                WorldChunkSampling.SampleSlope(
                    centerChunk,
                    generationSettings,
                    center.x,
                    center.y);

            if (centerSlope >
                fairnessSettings.MaximumCenterSlope)
            {
                return false;
            }

            PlacementBlockFlags centerBlocks =
                WorldChunkSampling.SamplePlacementBlocks(
                    centerChunk,
                    generationSettings,
                    center.x,
                    center.y);

            if (centerBlocks !=
                PlacementBlockFlags.None)
            {
                return false;
            }

            if (!TryEvaluateBuildArea(
                    center,
                    playableRect,
                    out float maximumSlope,
                    out float heightRange,
                    out float terrainScore))
            {
                return false;
            }

            float forestDensity =
                SampleAverageForestDensity(
                    center,
                    playableRect);

            if (forestDensity <
                fairnessSettings.MinimumAverageForestDensity)
            {
                return false;
            }

            float forestScore =
                Mathf.InverseLerp(
                    fairnessSettings.MinimumAverageForestDensity,
                    0.65f,
                    forestDensity);

            var metrics =
                new WorldStartAreaMetrics
                {
                    centerSlope = centerSlope,
                    maximumSampledSlope = maximumSlope,
                    sampledHeightRange = heightRange,
                    averageForestDensity = forestDensity,
                    terrainScore = terrainScore,
                    forestScore = forestScore
                };

            if (!EvaluateResources(
                    center,
                    playableRect,
                    metrics,
                    out float resourceScore))
            {
                return false;
            }

            metrics.resourceScore =
                resourceScore;

            float weightedScore = 0f;
            float totalWeight = 0f;

            AddWeighted(
                terrainScore,
                fairnessSettings.TerrainWeight,
                ref weightedScore,
                ref totalWeight);

            AddWeighted(
                forestScore,
                fairnessSettings.ForestWeight,
                ref weightedScore,
                ref totalWeight);

            AddWeighted(
                resourceScore,
                fairnessSettings.ResourceWeight,
                ref weightedScore,
                ref totalWeight);

            float quality =
                totalWeight > 0f
                    ? weightedScore /
                      totalWeight
                    : 1f;

            metrics.overallQualityScore =
                Mathf.Clamp01(
                    quality);

            float height =
                WorldChunkSampling.SampleHeight(
                    centerChunk,
                    generationSettings,
                    center.x,
                    center.y);

            candidate =
                new EvaluatedCandidate
                {
                    stableId = raw.stableId,
                    worldPosition =
                        new Vector3(
                            center.x,
                            height,
                            center.y),
                    metrics = metrics,
                    quality =
                        metrics.overallQualityScore
                };

            return true;
        }

        private bool TryEvaluateBuildArea(
            Vector2 center,
            Rect playableRect,
            out float maximumSlope,
            out float heightRange,
            out float terrainScore)
        {
            maximumSlope = 0f;
            heightRange = 0f;
            terrainScore = 0f;

            float minHeight =
                float.PositiveInfinity;

            float maxHeight =
                float.NegativeInfinity;

            const int sampleCount = 12;

            for (int i = 0;
                 i < sampleCount;
                 i++)
            {
                float angle =
                    Mathf.PI *
                    2f *
                    i /
                    sampleCount;

                Vector2 point =
                    center +
                    new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle)) *
                    fairnessSettings.BuildAreaRadius;

                if (!playableRect.Contains(point))
                    return false;

                WorldChunkData chunk =
                    GetChunk(point);

                float slope =
                    WorldChunkSampling.SampleSlope(
                        chunk,
                        generationSettings,
                        point.x,
                        point.y);

                if (slope >
                    fairnessSettings.MaximumBuildAreaSlope)
                {
                    return false;
                }

                PlacementBlockFlags blocks =
                    WorldChunkSampling.SamplePlacementBlocks(
                        chunk,
                        generationSettings,
                        point.x,
                        point.y);

                if ((blocks &
                    PlacementBlockFlags.LargeObjects) != 0)
                {
                    return false;
                }

                float height =
                    WorldChunkSampling.SampleHeight(
                        chunk,
                        generationSettings,
                        point.x,
                        point.y);

                maximumSlope =
                    Mathf.Max(
                        maximumSlope,
                        slope);

                minHeight =
                    Mathf.Min(
                        minHeight,
                        height);

                maxHeight =
                    Mathf.Max(
                        maxHeight,
                        height);
            }

            heightRange =
                Mathf.Max(
                    0f,
                    maxHeight -
                    minHeight);

            if (heightRange >
                fairnessSettings.MaximumBuildAreaHeightRange)
            {
                return false;
            }

            float slopeScore =
                1f -
                Mathf.InverseLerp(
                    0f,
                    fairnessSettings.MaximumBuildAreaSlope,
                    maximumSlope);

            float heightScore =
                fairnessSettings.MaximumBuildAreaHeightRange >
                0.0001f
                    ? 1f -
                      Mathf.InverseLerp(
                          0f,
                          fairnessSettings.MaximumBuildAreaHeightRange,
                          heightRange)
                    : 1f;

            terrainScore =
                Mathf.Clamp01(
                    (slopeScore +
                     heightScore) *
                    0.5f);

            return true;
        }

        private float SampleAverageForestDensity(
            Vector2 center,
            Rect playableRect)
        {
            float total = 0f;
            int count = 0;

            SampleForestPoint(
                center,
                playableRect,
                ref total,
                ref count);

            const int ringSamples = 16;

            for (int i = 0;
                 i < ringSamples;
                 i++)
            {
                float angle =
                    Mathf.PI *
                    2f *
                    i /
                    ringSamples;

                Vector2 point =
                    center +
                    new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle)) *
                    fairnessSettings.ForestSampleRadius;

                SampleForestPoint(
                    point,
                    playableRect,
                    ref total,
                    ref count);
            }

            return
                count > 0
                    ? total / count
                    : 0f;
        }

        private void SampleForestPoint(
            Vector2 point,
            Rect playableRect,
            ref float total,
            ref int count)
        {
            count++;

            if (!playableRect.Contains(point))
                return;

            WorldChunkData chunk =
                GetChunk(point);

            total +=
                WorldChunkSampling.SampleForestDensity(
                    chunk,
                    generationSettings,
                    point.x,
                    point.y);
        }

        private bool EvaluateResources(
            Vector2 center,
            Rect playableRect,
            WorldStartAreaMetrics metrics,
            out float resourceScore)
        {
            resourceScore = 1f;

            IReadOnlyList<StartResourceRequirement> requirements =
                fairnessSettings.ResourceRequirements;

            if (requirements == null ||
                requirements.Count == 0)
            {
                return true;
            }

            float weighted = 0f;
            float totalWeight = 0f;

            for (int i = 0;
                 i < requirements.Count;
                 i++)
            {
                StartResourceRequirement requirement =
                    requirements[i];

                if (requirement == null)
                    continue;

                int effectiveCapacity =
                    SumEffectiveResourceCapacity(
                        center,
                        playableRect,
                        requirement.resourceKind,
                        requirement.searchRadius);

                int minimum =
                    Mathf.Max(
                        0,
                        requirement.minimumEffectiveCapacity);

                if (effectiveCapacity <
                    minimum)
                {
                    return false;
                }

                int target =
                    Mathf.Max(
                        1,
                        requirement.targetEffectiveCapacity);

                float score =
                    Mathf.Clamp01(
                        effectiveCapacity /
                        (float)target);

                metrics.resources.Add(
                    new StartResourceMetric(
                        requirement.resourceKind,
                        effectiveCapacity,
                        score));

                float weight =
                    Mathf.Max(
                        0f,
                        requirement.weight);

                weighted +=
                    score *
                    weight;

                totalWeight +=
                    weight;
            }

            resourceScore =
                totalWeight > 0f
                    ? weighted /
                      totalWeight
                    : 1f;

            return true;
        }

        private int SumEffectiveResourceCapacity(
            Vector2 center,
            Rect playableRect,
            ResourceKind resourceKind,
            float searchRadius)
        {
            float radius =
                Mathf.Max(
                    1f,
                    searchRadius);

            ChunkCoordinate min =
                WorldChunkCoordinateUtility.FromWorldPosition(
                    center.x - radius,
                    center.y - radius,
                    generationSettings.ChunkWorldSize);

            ChunkCoordinate max =
                WorldChunkCoordinateUtility.FromWorldPosition(
                    center.x + radius,
                    center.y + radius,
                    generationSettings.ChunkWorldSize);

            float radiusSqr =
                radius *
                radius;

            long total = 0L;

            for (int z = min.z;
                 z <= max.z;
                 z++)
            {
                for (int x = min.x;
                     x <= max.x;
                     x++)
                {
                    WorldChunkData chunk =
                        GetChunk(
                            new ChunkCoordinate(
                                x,
                                z));

                    for (int i = 0;
                         i < chunk.ResourceDeposits.Count;
                         i++)
                    {
                        WorldResourceDepositData deposit =
                            chunk.ResourceDeposits[i];

                        if (deposit.resourceKind !=
                            resourceKind)
                        {
                            continue;
                        }

                        var depositPosition =
                            new Vector2(
                                deposit.worldPosition.x,
                                deposit.worldPosition.z);

                        if (!playableRect.Contains(
                                depositPosition))
                        {
                            continue;
                        }

                        if ((depositPosition -
                             center).sqrMagnitude >
                            radiusSqr)
                        {
                            continue;
                        }

                        total +=
                            Mathf.Max(
                                0,
                                Mathf.RoundToInt(
                                    deposit.capacity *
                                    Mathf.Clamp01(
                                        deposit.richness)));
                    }
                }
            }

            return
                total >= int.MaxValue
                    ? int.MaxValue
                    : (int)total;
        }

        private List<EvaluatedCandidate> SelectStarts(
            List<EvaluatedCandidate> viable,
            int playerCount,
            float minimumStartDistance)
        {
            var selected =
                new List<EvaluatedCandidate>(
                    playerCount);

            EvaluatedCandidate first = null;
            float firstScore =
                float.NegativeInfinity;

            for (int i = 0;
                 i < viable.Count;
                 i++)
            {
                EvaluatedCandidate candidate =
                    viable[i];

                float tie =
                    StableTieBreaker(
                        candidate.stableId,
                        0) *
                    0.0001f;

                float score =
                    candidate.quality +
                    tie;

                if (score > firstScore)
                {
                    firstScore = score;
                    first = candidate;
                }
            }

            if (first == null)
                return selected;

            selected.Add(first);

            while (selected.Count <
                   playerCount)
            {
                EvaluatedCandidate best = null;
                float bestCombined =
                    float.NegativeInfinity;

                for (int i = 0;
                     i < viable.Count;
                     i++)
                {
                    EvaluatedCandidate candidate =
                        viable[i];

                    if (selected.Contains(
                            candidate))
                    {
                        continue;
                    }

                    float nearestDistance =
                        GetNearestSelectedDistance(
                            candidate,
                            selected);

                    if (nearestDistance <
                        minimumStartDistance)
                    {
                        continue;
                    }

                    float separationScore =
                        Mathf.Clamp01(
                            nearestDistance /
                            Mathf.Max(
                                1f,
                                minimumStartDistance *
                                1.75f));

                    float combined =
                        candidate.quality *
                        fairnessSettings.QualityWeight +
                        separationScore *
                        fairnessSettings.SeparationWeight +
                        StableTieBreaker(
                            candidate.stableId,
                            selected.Count) *
                        0.0001f;

                    if (combined >
                        bestCombined)
                    {
                        bestCombined =
                            combined;

                        best = candidate;
                    }
                }

                if (best == null)
                    break;

                selected.Add(best);
            }

            return selected;
        }

        private void AssignPlayerIndices(
            List<EvaluatedCandidate> selected,
            WorldStartFairnessReport report)
        {
            selected.Sort(
                delegate(
                    EvaluatedCandidate a,
                    EvaluatedCandidate b)
                {
                    float aPriority =
                        StableTieBreaker(
                            a.stableId,
                            0x51);

                    float bPriority =
                        StableTieBreaker(
                            b.stableId,
                            0x51);

                    int compare =
                        aPriority.CompareTo(
                            bPriority);

                    if (compare != 0)
                        return compare;

                    return
                        a.stableId.CompareTo(
                            b.stableId);
                });

            for (int i = 0;
                 i < selected.Count;
                 i++)
            {
                EvaluatedCandidate candidate =
                    selected[i];

                report.AddStart(
                    new WorldPlayerStartData(
                        i,
                        candidate.stableId,
                        candidate.worldPosition,
                        candidate.quality,
                        candidate.metrics));
            }
        }

        private void CalculateSelectedScoreSummary(
            WorldStartFairnessReport report)
        {
            if (report.Starts.Count == 0)
            {
                report.minimumSelectedScore = 0f;
                report.maximumSelectedScore = 0f;
                report.selectedScoreSpread = 0f;
                return;
            }

            float min =
                float.PositiveInfinity;

            float max =
                float.NegativeInfinity;

            for (int i = 0;
                 i < report.Starts.Count;
                 i++)
            {
                float score =
                    report.Starts[i].normalizedScore;

                min =
                    Mathf.Min(
                        min,
                        score);

                max =
                    Mathf.Max(
                        max,
                        score);
            }

            report.minimumSelectedScore = min;
            report.maximumSelectedScore = max;
            report.selectedScoreSpread =
                Mathf.Max(
                    0f,
                    max - min);
        }

        private float CalculateMinimumStartDistance(
            Rect playableRect,
            int playerCount)
        {
            float areaPerPlayer =
                Mathf.Max(
                    1f,
                    playableRect.width *
                    playableRect.height /
                    Mathf.Max(
                        1,
                        playerCount));

            return
                Mathf.Sqrt(
                    areaPerPlayer) *
                fairnessSettings.MinimumSeparationMultiplier;
        }

        private float GetNearestSelectedDistance(
            EvaluatedCandidate candidate,
            List<EvaluatedCandidate> selected)
        {
            float nearest =
                float.PositiveInfinity;

            var candidatePosition =
                new Vector2(
                    candidate.worldPosition.x,
                    candidate.worldPosition.z);

            for (int i = 0;
                 i < selected.Count;
                 i++)
            {
                var otherPosition =
                    new Vector2(
                        selected[i].worldPosition.x,
                        selected[i].worldPosition.z);

                nearest =
                    Mathf.Min(
                        nearest,
                        Vector2.Distance(
                            candidatePosition,
                            otherPosition));
            }

            return nearest;
        }

        private WorldChunkData GetChunk(
            Vector2 worldPosition)
        {
            ChunkCoordinate coordinate =
                WorldChunkCoordinateUtility.FromWorldPosition(
                    worldPosition.x,
                    worldPosition.y,
                    generationSettings.ChunkWorldSize);

            return
                GetChunk(
                    coordinate);
        }

        private WorldChunkData GetChunk(
            ChunkCoordinate coordinate)
        {
            if (chunkCache.TryGetValue(
                    coordinate,
                    out WorldChunkData chunk))
            {
                return chunk;
            }

            chunk =
                pipeline.GenerateChunkThroughPhase(
                    worldSeed,
                    coordinate,
                    WorldGenerationStagePhase.Resources);

            chunkCache.Add(
                coordinate,
                chunk);

            return chunk;
        }

        private float StableTieBreaker(
            long stableId,
            int salt)
        {
            unchecked
            {
                int low =
                    (int)stableId;

                int high =
                    (int)(
                        stableId >>
                        32);

                return
                    DeterministicHash.Hash01(
                        worldSeed,
                        low,
                        high,
                        salt);
            }
        }

        private static void AddWeighted(
            float value,
            float weight,
            ref float weighted,
            ref float totalWeight)
        {
            if (weight <= 0f)
                return;

            weighted +=
                Mathf.Clamp01(value) *
                weight;

            totalWeight +=
                weight;
        }

        private static void Reject(
            WorldStartFairnessReport report,
            string reason)
        {
            report.accepted = false;
            report.rejectionReason =
                reason ??
                "Unknown fairness rejection.";
        }
    }
}
