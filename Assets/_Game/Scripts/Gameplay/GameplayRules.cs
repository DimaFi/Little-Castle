using System;
using System.Collections.Generic;

namespace LittleCastle.Gameplay
{
    [Serializable]
    public sealed class EconomyRules
    {
        public double wheatPerFarmPerGameHour = 2.0;
        public double breadPerMillPerGameHour = 3.0;
        public double wheatPerBread = 1.0;
        public double breadPerResidentPerGameDay = 0.75;
    }

    [Serializable]
    public sealed class PopulationRules
    {
        public float sleepingEnergyPerGameHour = 15f;
        public float idleEnergyPerGameHour = 2f;
        public float workingEnergyCostPerGameHour = 5f;

        public float sleepDebtAddedPerNightWorkHour = 4f;
        public float sleepDebtRecoveredPerSleepHour = 10f;

        public float chronicSleepDebtThreshold = 20f;
        public float moodLossPerExcessSleepDebtHour = 0.035f;
        public float hungerHappinessLossPerMissingBread = 0.8f;
        public float settlementHappinessAdaptationPerGameHour = 0.15f;
    }

    [Serializable]
    public sealed class WallRepairRules
    {
        public float hitPointsPerResidentGameHour = 5f;
        public int maximumResidentsPerWall = 3;
    }

    [Serializable]
    public sealed class RulerRules
    {
        public double respawnGameHours = 8.0;
        public double darkMagicXpPerMeditationHour = 10.0;
        public double darkMagicXpPerLevel = 100.0;
        public int maximumDarkMagicLevel = 10;
    }

    [Serializable]
    public sealed class RitualRules
    {
        public int blessingRollsPerDawn = 1;
        public int curseRollsPerDawn = 1;

        public float minimumBacklashChance = 0.05f;
        public float maximumBacklashChance = 0.65f;
    }

    [Serializable]
    public sealed class BuildingDefinition
    {
        public string archetypeId = string.Empty;
        public BuildingRole role = BuildingRole.Other;
        public int level = 1;
        public float maxHitPoints = 100f;
        public double constructionWorkHours = 4.0;
        public List<ResourceAmount> cost =
            new List<ResourceAmount>();
    }

    [Serializable]
    public sealed class BuildingUpgradeDefinition
    {
        public BuildingRole role = BuildingRole.Other;
        public int fromLevel = 1;
        public int toLevel = 2;
        public string targetArchetypeId = string.Empty;
        public float targetMaxHitPoints = 100f;
        public double constructionWorkHours = 6.0;
        public List<ResourceAmount> cost =
            new List<ResourceAmount>();
    }

    [Serializable]
    public sealed class UnitTrainingDefinition
    {
        public string unitArchetypeId = "soldier_basic";
        public double trainingGameHours = 6.0;
        public List<ResourceAmount> cost =
            new List<ResourceAmount>();
    }

    [Serializable]
    public sealed class GameplayRules
    {
        public EconomyRules economy =
            new EconomyRules();

        public PopulationRules population =
            new PopulationRules();

        public RulerRules ruler =
            new RulerRules();

        public WallRepairRules wallRepair =
            new WallRepairRules();

        public RitualRules rituals =
            new RitualRules();

        public List<BuildingDefinition> buildings =
            new List<BuildingDefinition>();

        public List<BuildingUpgradeDefinition> buildingUpgrades =
            new List<BuildingUpgradeDefinition>();

        public List<UnitTrainingDefinition> unitTraining =
            new List<UnitTrainingDefinition>();

        public BuildingDefinition FindBuilding(
            string archetypeId)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingDefinition definition =
                    buildings[i];

                if (definition != null &&
                    definition.archetypeId ==
                    archetypeId)
                {
                    return definition;
                }
            }

            return null;
        }

        public BuildingUpgradeDefinition FindUpgrade(
            BuildingRole role,
            int fromLevel)
        {
            for (int i = 0;
                 i < buildingUpgrades.Count;
                 i++)
            {
                BuildingUpgradeDefinition definition =
                    buildingUpgrades[i];

                if (definition != null &&
                    definition.role == role &&
                    definition.fromLevel == fromLevel)
                {
                    return definition;
                }
            }

            return null;
        }

        public UnitTrainingDefinition FindUnitTraining(
            string unitArchetypeId)
        {
            for (int i = 0; i < unitTraining.Count; i++)
            {
                UnitTrainingDefinition definition =
                    unitTraining[i];

                if (definition != null &&
                    definition.unitArchetypeId ==
                    unitArchetypeId)
                {
                    return definition;
                }
            }

            return null;
        }

        public static GameplayRules CreateDefault()
        {
            var rules =
                new GameplayRules();

            AddDefaultBuildings(rules);

            rules.unitTraining.Add(
                new UnitTrainingDefinition
                {
                    unitArchetypeId =
                        "soldier_basic",
                    trainingGameHours = 6.0,
                    cost =
                        new List<ResourceAmount>
                        {
                            new ResourceAmount(
                                GameplayResourceType.Bread,
                                2),
                            new ResourceAmount(
                                GameplayResourceType.Coin,
                                5)
                        }
                });

            return rules;
        }

        private static void AddDefaultBuildings(
            GameplayRules rules)
        {
            rules.buildings.Add(
                Building(
                    "house_basic",
                    BuildingRole.House,
                    150f,
                    3.0,
                    Resource(
                        GameplayResourceType.Wood,
                        8)));

            rules.buildings.Add(
                Building(
                    "farm_wheat_basic",
                    BuildingRole.Farm,
                    120f,
                    4.0,
                    Resource(
                        GameplayResourceType.Wood,
                        10)));

            rules.buildings.Add(
                Building(
                    "mill_basic",
                    BuildingRole.Mill,
                    220f,
                    7.0,
                    Resource(
                        GameplayResourceType.Wood,
                        14),
                    Resource(
                        GameplayResourceType.Stone,
                        8)));

            rules.buildings.Add(
                Building(
                    "market_basic",
                    BuildingRole.Market,
                    180f,
                    5.0,
                    Resource(
                        GameplayResourceType.Wood,
                        12)));

            rules.buildings.Add(
                Building(
                    "church_basic",
                    BuildingRole.Church,
                    250f,
                    8.0,
                    Resource(
                        GameplayResourceType.Wood,
                        12),
                    Resource(
                        GameplayResourceType.Stone,
                        14)));

            rules.buildings.Add(
                Building(
                    "barracks_basic",
                    BuildingRole.Barracks,
                    300f,
                    9.0,
                    Resource(
                        GameplayResourceType.Wood,
                        18),
                    Resource(
                        GameplayResourceType.Stone,
                        10)));

            rules.buildings.Add(
                Building(
                    "archer_tower_basic",
                    BuildingRole.ArcherTower,
                    350f,
                    10.0,
                    Resource(
                        GameplayResourceType.Wood,
                        16),
                    Resource(
                        GameplayResourceType.Stone,
                        18)));

            rules.buildingUpgrades.Add(
                Upgrade(
                    BuildingRole.MainHouse,
                    1,
                    2,
                    "main_house_level_2",
                    750f,
                    12.0,
                    Resource(
                        GameplayResourceType.Wood,
                        24),
                    Resource(
                        GameplayResourceType.Stone,
                        14)));

            rules.buildingUpgrades.Add(
                Upgrade(
                    BuildingRole.MainHouse,
                    2,
                    3,
                    "main_house_keep",
                    1100f,
                    18.0,
                    Resource(
                        GameplayResourceType.Wood,
                        32),
                    Resource(
                        GameplayResourceType.Stone,
                        32),
                    Resource(
                        GameplayResourceType.Iron,
                        8)));

            rules.buildingUpgrades.Add(
                Upgrade(
                    BuildingRole.MainHouse,
                    3,
                    4,
                    "main_house_small_castle",
                    1600f,
                    28.0,
                    Resource(
                        GameplayResourceType.Wood,
                        45),
                    Resource(
                        GameplayResourceType.Stone,
                        60),
                    Resource(
                        GameplayResourceType.Iron,
                        18)));
        }

        private static BuildingDefinition Building(
            string archetypeId,
            BuildingRole role,
            float hp,
            double workHours,
            params ResourceAmount[] cost)
        {
            var definition =
                new BuildingDefinition
                {
                    archetypeId = archetypeId,
                    role = role,
                    maxHitPoints = hp,
                    constructionWorkHours =
                        workHours
                };

            definition.cost.AddRange(cost);
            return definition;
        }

        private static BuildingUpgradeDefinition Upgrade(
            BuildingRole role,
            int from,
            int to,
            string targetArchetype,
            float hp,
            double workHours,
            params ResourceAmount[] cost)
        {
            var definition =
                new BuildingUpgradeDefinition
                {
                    role = role,
                    fromLevel = from,
                    toLevel = to,
                    targetArchetypeId =
                        targetArchetype,
                    targetMaxHitPoints = hp,
                    constructionWorkHours =
                        workHours
                };

            definition.cost.AddRange(cost);
            return definition;
        }

        private static ResourceAmount Resource(
            GameplayResourceType resource,
            int amount)
        {
            return new ResourceAmount(
                resource,
                amount);
        }
    }
}
