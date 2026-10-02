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

        public RitualRules rituals =
            new RitualRules();

        public List<UnitTrainingDefinition> unitTraining =
            new List<UnitTrainingDefinition>();

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
    }
}
