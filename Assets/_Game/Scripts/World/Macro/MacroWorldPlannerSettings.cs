using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class MacroPointFeatureRule
    {
        public string ruleId = "neutral_village_common";
        public WorldFeatureKind kind = WorldFeatureKind.NeutralSettlement;
        public string archetypeId = "neutral_village_01";

        [Min(50f)]
        public float spacing = 800f;

        [Range(0f, 1f)]
        public float chance = 0.5f;

        [Range(0f, 0.45f)]
        public float borderJitter = 0.2f;

        [Min(0f)]
        public float influenceRadius = 100f;
    }

    [CreateAssetMenu(
        fileName = "MacroWorldPlannerSettings",
        menuName = "Little Castle/World/Macro World Planner Settings")]
    public sealed class MacroWorldPlannerSettings : ScriptableObject
    {
        [SerializeField]
        private List<MacroPointFeatureRule> pointFeatureRules =
            new List<MacroPointFeatureRule>();

        public IReadOnlyList<MacroPointFeatureRule> PointFeatureRules =>
            pointFeatureRules;
    }
}
