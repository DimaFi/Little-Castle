using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed class MacroWorldPlanner
    {
        /// <summary>
        /// Exact spatial acceleration for point-feature separation checks.
        ///
        /// It changes only lookup cost, not placement semantics: candidates are
        /// still compared with every existing feature that could possibly
        /// violate the same distance rule.
        /// </summary>
        private sealed class PointFeatureSpatialIndex
        {
            private const float CellSize = 128f;

            private readonly Dictionary<Vector2Int, List<WorldPointFeatureData>>
                buckets =
                    new Dictionary<Vector2Int, List<WorldPointFeatureData>>();

            private float maximumInfluenceRadius;

            public void Add(
                WorldPointFeatureData feature)
            {
                Vector2Int key =
                    ToCell(
                        feature.worldPosition);

                if (!buckets.TryGetValue(
                        key,
                        out List<WorldPointFeatureData> bucket))
                {
                    bucket =
                        new List<WorldPointFeatureData>();

                    buckets.Add(
                        key,
                        bucket);
                }

                bucket.Add(feature);

                maximumInfluenceRadius =
                    Mathf.Max(
                        maximumInfluenceRadius,
                        Mathf.Max(
                            0f,
                            feature.influenceRadius));
            }

            public bool HasSeparation(
                Vector2 position,
                float influenceRadius,
                float padding)
            {
                float ownRadius =
                    Mathf.Max(
                        0f,
                        influenceRadius);

                float safePadding =
                    Mathf.Max(
                        0f,
                        padding);

                float searchRadius =
                    ownRadius +
                    maximumInfluenceRadius +
                    safePadding;

                int minX =
                    Mathf.FloorToInt(
                        (position.x - searchRadius) /
                        CellSize);

                int maxX =
                    Mathf.FloorToInt(
                        (position.x + searchRadius) /
                        CellSize);

                int minZ =
                    Mathf.FloorToInt(
                        (position.y - searchRadius) /
                        CellSize);

                int maxZ =
                    Mathf.FloorToInt(
                        (position.y + searchRadius) /
                        CellSize);

                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (!buckets.TryGetValue(
                                new Vector2Int(x, z),
                                out List<WorldPointFeatureData> bucket))
                        {
                            continue;
                        }

                        for (int i = 0;
                             i < bucket.Count;
                             i++)
                        {
                            WorldPointFeatureData existing =
                                bucket[i];

                            float required =
                                ownRadius +
                                Mathf.Max(
                                    0f,
                                    existing.influenceRadius) +
                                safePadding;

                            if ((existing.worldPosition -
                                 position).sqrMagnitude <
                                required * required)
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            }

            private static Vector2Int ToCell(
                Vector2 position)
            {
                return new Vector2Int(
                    Mathf.FloorToInt(
                        position.x /
                        CellSize),
                    Mathf.FloorToInt(
                        position.y /
                        CellSize));
            }
        }

        private readonly MacroWorldPlannerSettings settings;

        public MacroWorldPlanner(
            MacroWorldPlannerSettings settings)
        {
            this.settings = settings;
        }

        public MacroWorldPlan GenerateForBounds(
            int worldSeed,
            Rect worldBounds,
            WorldTerrainProbe terrainProbe = null)
        {
            var plan =
                new MacroWorldPlan(
                    worldSeed);

            if (settings == null)
                return plan;

            Rect planningBounds =
                ExpandRect(
                    worldBounds,
                    settings.PlanningHalo);

            var pointSpatialIndex =
                new PointFeatureSpatialIndex();

            for (int i = 0;
                 i < settings.PointFeatureRules.Count;
                 i++)
            {
                MacroPointFeatureRule rule =
                    settings.PointFeatureRules[i];

                if (rule == null)
                    continue;

                AppendRule(
                    worldSeed,
                    planningBounds,
                    rule,
                    terrainProbe,
                    plan,
                    pointSpatialIndex);
            }

            if (terrainProbe != null)
            {
                RiverNetworkPlanner.BuildRivers(
                    worldSeed,
                    planningBounds,
                    terrainProbe,
                    settings.Rivers,
                    plan);
            }

            RoadNetworkPlanner.BuildConnections(
                worldSeed,
                plan,
                settings.RoadNetwork);

            if (terrainProbe != null)
            {
                TerrainRoadPathPlanner.BuildRoadPaths(
                    plan,
                    terrainProbe,
                    settings.RoadPaths);
            }

            BridgeSitePlanner.BuildBridgeSites(
                worldSeed,
                plan,
                settings.Bridges);

            return plan;
        }

        private static void AppendRule(
            int worldSeed,
            Rect bounds,
            MacroPointFeatureRule rule,
            WorldTerrainProbe terrainProbe,
            MacroWorldPlan plan,
            PointFeatureSpatialIndex pointSpatialIndex)
        {
            float spacing =
                Mathf.Max(
                    50f,
                    rule.spacing);

            int salt =
                DeterministicHash.String32(
                    string.IsNullOrWhiteSpace(
                        rule.ruleId)
                        ? rule.kind.ToString()
                        : rule.ruleId);

            int minGridX =
                Mathf.FloorToInt(
                    bounds.xMin / spacing) - 1;

            int maxGridX =
                Mathf.FloorToInt(
                    bounds.xMax / spacing) + 1;

            int minGridZ =
                Mathf.FloorToInt(
                    bounds.yMin / spacing) - 1;

            int maxGridZ =
                Mathf.FloorToInt(
                    bounds.yMax / spacing) + 1;

            float margin =
                Mathf.Clamp(
                    rule.borderJitter,
                    0f,
                    0.45f);

            for (int gz = minGridZ;
                 gz <= maxGridZ;
                 gz++)
            {
                for (int gx = minGridX;
                     gx <= maxGridX;
                     gx++)
                {
                    float roll =
                        DeterministicHash.Hash01(
                            worldSeed,
                            gx,
                            gz,
                            salt ^ 0x101);

                    if (roll > rule.chance)
                        continue;

                    float jx =
                        Mathf.Lerp(
                            margin,
                            1f - margin,
                            DeterministicHash.Hash01(
                                worldSeed,
                                gx,
                                gz,
                                salt ^ 0x102));

                    float jz =
                        Mathf.Lerp(
                            margin,
                            1f - margin,
                            DeterministicHash.Hash01(
                                worldSeed,
                                gx,
                                gz,
                                salt ^ 0x103));

                    var position =
                        new Vector2(
                            (gx + jx) * spacing,
                            (gz + jz) * spacing);

                    if (!bounds.Contains(position))
                        continue;

                    if (terrainProbe != null)
                    {
                        WorldTerrainSample sample =
                            terrainProbe.Sample(
                                position);

                        if (!rule.allowedTerrain.Contains(
                                sample.terrainClass) ||
                            sample.height <
                                rule.minHeight ||
                            sample.height >
                                rule.maxHeight ||
                            sample.slope >
                                rule.maxSlope)
                        {
                            continue;
                        }
                    }

                    if (rule.avoidOtherPointFeatures &&
                        !pointSpatialIndex.HasSeparation(
                            position,
                            rule.influenceRadius,
                            rule.separationPadding))
                    {
                        continue;
                    }

                    long id =
                        DeterministicHash.StableId(
                            worldSeed,
                            gx,
                            gz,
                            salt);

                    var feature =
                        new WorldPointFeatureData(
                            id,
                            rule.kind,
                            rule.archetypeId,
                            position,
                            rule.influenceRadius);

                    if (plan.AddPointFeature(
                            feature))
                    {
                        pointSpatialIndex.Add(
                            feature);
                    }
                }
            }
        }

        private static Rect ExpandRect(
            Rect rect,
            float padding)
        {
            float safe =
                Mathf.Max(
                    0f,
                    padding);

            rect.xMin -= safe;
            rect.xMax += safe;
            rect.yMin -= safe;
            rect.yMax += safe;
            return rect;
        }
    }
}
