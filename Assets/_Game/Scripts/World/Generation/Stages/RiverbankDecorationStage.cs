using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class RiverbankDecorationRule
    {
        public string ruleId = "bank_reed_visual_v1";
        public string archetypeId = "concept_reed_PENDING_INTAKE";
        public SpawnCategory category = SpawnCategory.Decoration;

        [Min(4f)] public float spacing = 6f;
        [Range(0f, 1f)] public float chance = 0.35f;
        [Range(0f, 0.49f)] public float jitterMargin = 0.15f;
        [Min(0f)] public float minimumBankDistance = 3.5f;
        [Min(0f)] public float maximumBankDistance = 10f;
        [Range(0f, 1f)] public float minimumMoisture = 0.45f;
        [Range(0f, 90f)] public float maximumSlope = 18f;
        [Min(0.01f)] public float minimumScale = 0.7f;
        [Min(0.01f)] public float maximumScale = 1.2f;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(ruleId) &&
            !string.IsNullOrWhiteSpace(archetypeId) &&
            (category == SpawnCategory.Decoration ||
             category == SpawnCategory.Bush) &&
            Finite(spacing) && spacing >= 4f &&
            Finite(chance) && chance >= 0f && chance <= 1f &&
            Finite(jitterMargin) &&
            jitterMargin >= 0f && jitterMargin < 0.5f &&
            Finite(minimumBankDistance) &&
            minimumBankDistance >= 0f &&
            Finite(maximumBankDistance) &&
            maximumBankDistance > minimumBankDistance &&
            Finite(minimumMoisture) &&
            minimumMoisture >= 0f && minimumMoisture <= 1f &&
            Finite(maximumSlope) &&
            maximumSlope >= 0f && maximumSlope <= 90f &&
            Finite(minimumScale) && minimumScale > 0f &&
            Finite(maximumScale) && maximumScale >= minimumScale;

        private static bool Finite(float x) =>
            !float.IsNaN(x) && !float.IsInfinity(x);
    }

    /// <summary>
    /// Optional standalone, deterministic river-edge DECORATION source.
    /// Never touches terrain/rivers/roads, resources or gameplay routes.
    /// Archetypes are deliberately PENDING art intake; no prefab is invented.
    /// Concept profile holds a DISABLED sample stage until catalog approval.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RiverbankDecorationStage",
        menuName = "Little Castle/World/Generation/Riverbank Decoration Stage")]
    public sealed class RiverbankDecorationStage : WorldGenerationStage
    {
        public override WorldGenerationStagePhase Phase =>
            WorldGenerationStagePhase.LocalSpawns;

        [SerializeField] private List<RiverbankDecorationRule> rules =
            new List<RiverbankDecorationRule>
            {
                new RiverbankDecorationRule()
            };

        [Header("Safety — decorative only")]
        [SerializeField, Min(1f)] private float roadExtraClearance = 2f;
        [SerializeField, Min(1f)] private float bridgeExtraClearance = 2f;
        [SerializeField, Min(0f)] private float featureExtraClearance = 2f;

        [Header("Deterministic per-chunk work limits")]
        [SerializeField, Range(1, 4096)]
        private int maxCandidateChecksPerChunk = 1024;

        [SerializeField, Range(0, 128)]
        private int maxSpawnsPerChunk = 24;

        public IReadOnlyList<RiverbankDecorationRule> Rules => rules;
        public int LastCandidateChecks { get; private set; }
        public int LastAddedSpawns { get; private set; }

        private readonly struct RiverBand
        {
            public readonly RiverEnvelopeUtility.Segment segment;
            public RiverBand(RiverEnvelopeUtility.Segment source) =>
                segment = source;
        }

        private readonly struct RoadBand
        {
            public readonly Vector2 start;
            public readonly Vector2 end;
            public readonly float exclusion;
            public RoadBand(Vector2 a, Vector2 b, float distance)
            {
                start = a;
                end = b;
                exclusion = distance;
            }
        }

        public override void Generate(
            GenerationContext context, WorldChunkData chunk)
        {
            LastCandidateChecks = 0;
            LastAddedSpawns = 0;
            if (context?.Settings == null || context.MacroPlan == null ||
                chunk == null || rules == null || rules.Count == 0)
                return;

            float chunkSize = context.Settings.ChunkWorldSize;
            if (!Finite(chunkSize) || chunkSize <= 0f)
                return;
            float minX = chunk.Coordinate.x * chunkSize;
            float minZ = chunk.Coordinate.z * chunkSize;
            float maxX = minX + chunkSize;
            float maxZ = minZ + chunkSize;

            float maximumBankReach = 0f;
            for (int i = 0; i < rules.Count; i++)
                if (rules[i] != null && rules[i].IsValid)
                    maximumBankReach = Mathf.Max(
                        maximumBankReach, rules[i].maximumBankDistance);
            if (maximumBankReach <= 0f)
                return;

            MacroWorldPlan plan = context.MacroPlan;
            var rivers = new List<RiverBand>(16);
            for (int i = 0; i < plan.Rivers.Count; i++)
            {
                WorldRiverData river = plan.Rivers[i];
                if (river?.centerline == null)
                    continue;
                for (int j = 0; j < river.centerline.Count - 1; j++)
                    if (RiverEnvelopeUtility.TryCreateSegment(
                            river, j, out RiverEnvelopeUtility.Segment seg) &&
                        RiverEnvelopeUtility.OverlapsChunk(
                            seg, maximumBankReach,
                            minX, minZ, maxX, maxZ))
                        rivers.Add(new RiverBand(seg));
            }
            if (rivers.Count == 0)
                return;

            var roads = new List<RoadBand>(16);
            for (int i = 0; i < plan.Roads.Count; i++)
            {
                WorldRoadData road = plan.Roads[i];
                if (road?.centerline == null ||
                    road.centerline.Count < 2)
                    continue;
                float exclusion = Mathf.Max(
                    0f, road.width * 0.5f + roadExtraClearance);
                for (int j = 0; j < road.centerline.Count - 1; j++)
                {
                    Vector2 a = road.centerline[j];
                    Vector2 b = road.centerline[j + 1];
                    if ((b - a).sqrMagnitude <= 0.000001f)
                        continue;
                    if (Mathf.Max(a.x, b.x) + exclusion < minX ||
                        Mathf.Min(a.x, b.x) - exclusion > maxX ||
                        Mathf.Max(a.y, b.y) + exclusion < minZ ||
                        Mathf.Min(a.y, b.y) - exclusion > maxZ)
                        continue;
                    roads.Add(new RoadBand(a, b, exclusion));
                }
            }

            var knownIds = new HashSet<long>();
            foreach (WorldSpawnData existing in chunk.Spawns)
                knownIds.Add(existing.stableId);

            // Per-rule grid coordinates are ABSOLUTE world space and
            // half-open at the maximum chunk boundary. Revisit and adjacent
            // chunks can never claim the same logical candidate.
            for (int r = 0; r < rules.Count; r++)
            {
                RiverbankDecorationRule rule = rules[r];
                if (rule == null || !rule.IsValid || rule.chance <= 0f)
                    continue;
                int salt = DeterministicHash.String32(rule.ruleId);
                float spacing = rule.spacing;
                int startX = Mathf.FloorToInt(minX / spacing) - 1;
                int endX = Mathf.FloorToInt(maxX / spacing) + 1;
                int startZ = Mathf.FloorToInt(minZ / spacing) - 1;
                int endZ = Mathf.FloorToInt(maxZ / spacing) + 1;

                for (int gz = startZ; gz <= endZ; gz++)
                {
                    for (int gx = startX; gx <= endX; gx++)
                    {
                        if (LastCandidateChecks >= maxCandidateChecksPerChunk ||
                            LastAddedSpawns >= maxSpawnsPerChunk)
                            return;

                        float jitterX = Mathf.Lerp(
                            rule.jitterMargin, 1f - rule.jitterMargin,
                            DeterministicHash.Hash01(
                                context.WorldSeed, gx, gz, salt ^ 0x11));
                        float jitterZ = Mathf.Lerp(
                            rule.jitterMargin, 1f - rule.jitterMargin,
                            DeterministicHash.Hash01(
                                context.WorldSeed, gx, gz, salt ^ 0x22));
                        float worldX = (gx + jitterX) * spacing;
                        float worldZ = (gz + jitterZ) * spacing;
                        if (worldX < minX || worldX >= maxX ||
                            worldZ < minZ || worldZ >= maxZ)
                            continue;

                        LastCandidateChecks++;
                        if (DeterministicHash.Hash01(
                                context.WorldSeed, gx, gz, salt ^ 0x33) >
                            rule.chance)
                            continue;

                        Vector2 position = new Vector2(worldX, worldZ);
                        if (!SafeBank(
                                position, rule, rivers) ||
                            !ClearRoad(position, roads) ||
                            !ClearFeatureOrBridge(
                                position, plan,
                                bridgeExtraClearance,
                                featureExtraClearance))
                            continue;

                        PlacementBlockFlags blocked =
                            WorldChunkSampling.SamplePlacementBlocks(
                                chunk, context.Settings, worldX, worldZ);
                        if ((blocked & PlacementBlockFlags.Decoration) != 0)
                            continue;

                        SurfaceKind surface = WorldChunkSampling.SampleSurface(
                            chunk, context.Settings, worldX, worldZ);
                        if (surface != SurfaceKind.Grass &&
                            surface != SurfaceKind.Mud &&
                            surface != SurfaceKind.WoodlandFloor)
                            continue;

                        float moisture = WorldChunkSampling.SampleMoisture(
                            chunk, context.Settings, worldX, worldZ);
                        float slope = WorldChunkSampling.SampleSlope(
                            chunk, context.Settings, worldX, worldZ);
                        float height = WorldChunkSampling.SampleHeight(
                            chunk, context.Settings, worldX, worldZ);
                        if (!Finite(moisture) ||
                            moisture < rule.minimumMoisture ||
                            !Finite(slope) || slope < 0f ||
                            slope > rule.maximumSlope ||
                            !Finite(height))
                            continue;

                        long id = DeterministicHash.StableId(
                            context.WorldSeed, gx, gz, salt);
                        if (!knownIds.Add(id))
                            continue;

                        float yaw = DeterministicHash.Hash01(
                            context.WorldSeed, gx, gz, salt ^ 0x44) * 360f;
                        float scale = Mathf.Lerp(
                            rule.minimumScale, rule.maximumScale,
                            DeterministicHash.Hash01(
                                context.WorldSeed, gx, gz, salt ^ 0x55));
                        chunk.AddSpawn(new WorldSpawnData(
                            id, rule.archetypeId, rule.category,
                            new Vector3(worldX, height, worldZ), yaw, scale));
                        LastAddedSpawns++;
                    }
                }
            }
        }

        private static bool SafeBank(
            Vector2 at, RiverbankDecorationRule rule,
            List<RiverBand> rivers)
        {
            float nearestSignedBankDistance = float.PositiveInfinity;
            for (int i = 0; i < rivers.Count; i++)
            {
                RiverEnvelopeUtility.Segment seg = rivers[i].segment;
                float distance = RiverEnvelopeUtility.DistanceToSegment(
                    at, seg.a, seg.b, out float t);
                float bank = distance - Mathf.Lerp(
                    seg.halfStartWidth, seg.halfEndWidth, t);
                nearestSignedBankDistance = Mathf.Min(
                    nearestSignedBankDistance, bank);
            }
            return nearestSignedBankDistance >=
                    rule.minimumBankDistance &&
                nearestSignedBankDistance <= rule.maximumBankDistance;
        }

        private static bool ClearRoad(
            Vector2 at, List<RoadBand> roads)
        {
            for (int i = 0; i < roads.Count; i++)
                if (RiverEnvelopeUtility.DistanceToSegment(
                        at, roads[i].start, roads[i].end, out _) <=
                    roads[i].exclusion)
                    return false;
            return true;
        }

        private static bool ClearFeatureOrBridge(
            Vector2 at, MacroWorldPlan plan,
            float bridgeMargin, float featureMargin)
        {
            for (int i = 0; i < plan.PointFeatures.Count; i++)
            {
                WorldPointFeatureData feature = plan.PointFeatures[i];
                float clearance = Mathf.Max(
                    0f, feature.influenceRadius + featureMargin);
                if ((feature.worldPosition - at).sqrMagnitude <=
                    clearance * clearance)
                    return false;
            }

            for (int i = 0; i < plan.BridgeSites.Count; i++)
            {
                WorldBridgeSiteData site = plan.BridgeSites[i];
                if (FixedBridgeSiteProfile.IsSupported(site))
                {
                    Vector2 local = FixedBridgeSiteProfile.WorldToLocal(
                        at, site.worldPosition, site.yawDegrees);
                    if (Mathf.Abs(local.x) <=
                            FixedBridgeSiteProfile.SupportHalfExtentX +
                            bridgeMargin &&
                        Mathf.Abs(local.y) <=
                            FixedBridgeSiteProfile.SupportHalfExtentZ +
                            bridgeMargin)
                        return false;
                }
                else if ((site.worldPosition - at).sqrMagnitude <=
                    (8f + bridgeMargin) * (8f + bridgeMargin))
                {
                    // Generic bridge sites are kept clear conservatively,
                    // rather than guessing a missing production footprint.
                    return false;
                }
            }
            return true;
        }

        private static bool Finite(float x) =>
            !float.IsNaN(x) && !float.IsInfinity(x);
    }
}
