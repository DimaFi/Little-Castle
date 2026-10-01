using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Deterministic on-demand macro point planner.
    ///
    /// It can query any rectangular world area without pre-generating an
    /// infinite world. Terrain suitability is intentionally a future filtering
    /// layer; this class establishes stable candidates/identity first.
    /// </summary>
    public sealed class MacroWorldPlanner
    {
        private readonly MacroWorldPlannerSettings settings;

        public MacroWorldPlanner(MacroWorldPlannerSettings settings)
        {
            this.settings = settings;
        }

        public MacroWorldPlan GenerateForBounds(
            int worldSeed,
            Rect worldBounds)
        {
            var plan = new MacroWorldPlan(worldSeed);

            if (settings == null)
                return plan;

            for (int i = 0; i < settings.PointFeatureRules.Count; i++)
            {
                MacroPointFeatureRule rule =
                    settings.PointFeatureRules[i];

                if (rule == null)
                    continue;

                AppendRule(worldSeed, worldBounds, rule, plan);
            }

            return plan;
        }

        private static void AppendRule(
            int worldSeed,
            Rect bounds,
            MacroPointFeatureRule rule,
            MacroWorldPlan plan)
        {
            float spacing = Mathf.Max(50f, rule.spacing);
            int salt = DeterministicHash.String32(
                string.IsNullOrWhiteSpace(rule.ruleId)
                    ? rule.kind.ToString()
                    : rule.ruleId);

            int minGridX =
                Mathf.FloorToInt(bounds.xMin / spacing) - 1;

            int maxGridX =
                Mathf.FloorToInt(bounds.xMax / spacing) + 1;

            int minGridZ =
                Mathf.FloorToInt(bounds.yMin / spacing) - 1;

            int maxGridZ =
                Mathf.FloorToInt(bounds.yMax / spacing) + 1;

            float margin =
                Mathf.Clamp(rule.borderJitter, 0f, 0.45f);

            for (int gz = minGridZ; gz <= maxGridZ; gz++)
            {
                for (int gx = minGridX; gx <= maxGridX; gx++)
                {
                    float roll = DeterministicHash.Hash01(
                        worldSeed,
                        gx,
                        gz,
                        salt ^ 0x101);

                    if (roll > rule.chance)
                        continue;

                    float jx = Mathf.Lerp(
                        margin,
                        1f - margin,
                        DeterministicHash.Hash01(
                            worldSeed,
                            gx,
                            gz,
                            salt ^ 0x102));

                    float jz = Mathf.Lerp(
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

                    long id = DeterministicHash.StableId(
                        worldSeed,
                        gx,
                        gz,
                        salt);

                    plan.AddPointFeature(
                        new WorldPointFeatureData(
                            id,
                            rule.kind,
                            rule.archetypeId,
                            position,
                            rule.influenceRadius));
                }
            }
        }
    }
}
