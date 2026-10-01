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

        [Header("Distribution")]
        [Min(50f)]
        public float spacing = 800f;

        [Range(0f, 1f)]
        public float chance = 0.5f;

        [Range(0f, 0.45f)]
        public float borderJitter = 0.2f;

        [Min(0f)]
        public float influenceRadius = 100f;

        [Header("Terrain suitability")]
        public TerrainClassMask allowedTerrain =
            TerrainClassMask.Plains |
            TerrainClassMask.RollingHills;

        public float minHeight = -1000f;
        public float maxHeight = 1000f;

        [Range(0f, 90f)]
        public float maxSlope = 12f;
    }

    [CreateAssetMenu(
        fileName = "MacroWorldPlannerSettings",
        menuName = "Little Castle/World/Macro World Planner Settings")]
    public sealed class MacroWorldPlannerSettings : ScriptableObject
    {
        [SerializeField]
        private List<MacroPointFeatureRule> pointFeatureRules =
            new List<MacroPointFeatureRule>();

        [Header("Road graph")]
        [SerializeField]
        private RoadNetworkPlannerSettings roadNetwork =
            new RoadNetworkPlannerSettings();

        public IReadOnlyList<MacroPointFeatureRule> PointFeatureRules =>
            pointFeatureRules;

        public RoadNetworkPlannerSettings RoadNetwork =>
            roadNetwork;

        public float MaxPointInfluenceRadius
        {
            get
            {
                float max = 0f;

                for (int i = 0; i < pointFeatureRules.Count; i++)
                {
                    MacroPointFeatureRule rule =
                        pointFeatureRules[i];

                    if (rule != null)
                    {
                        max =
                            Mathf.Max(
                                max,
                                rule.influenceRadius);
                    }
                }

                return max;
            }
        }
    }
}
