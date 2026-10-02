using System;

namespace LittleCastle.Gameplay
{
    public static class ResidentNeedsSystem
    {
        public static void Advance(
            ResidentState resident,
            double gameHours,
            bool isNight,
            PopulationRules rules)
        {
            if (resident == null)
                throw new ArgumentNullException(nameof(resident));

            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            if (gameHours <= 0.0)
                return;

            float hours =
                (float)Math.Min(
                    gameHours,
                    100000.0);

            switch (resident.activity)
            {
                case ResidentActivity.Sleeping:
                    resident.energy =
                        Clamp(
                            resident.energy +
                            rules.sleepingEnergyPerGameHour *
                            hours,
                            0f,
                            100f);

                    resident.sleepDebt =
                        Clamp(
                            resident.sleepDebt -
                            rules.sleepDebtRecoveredPerSleepHour *
                            hours,
                            0f,
                            100f);
                    break;

                case ResidentActivity.Working:
                case ResidentActivity.Defending:
                case ResidentActivity.Emergency:
                    resident.energy =
                        Clamp(
                            resident.energy -
                            rules.workingEnergyCostPerGameHour *
                            hours,
                            0f,
                            100f);

                    if (isNight)
                    {
                        resident.sleepDebt =
                            Clamp(
                                resident.sleepDebt +
                                rules.sleepDebtAddedPerNightWorkHour *
                                hours,
                                0f,
                                100f);
                    }
                    break;

                default:
                    resident.energy =
                        Clamp(
                            resident.energy +
                            rules.idleEnergyPerGameHour *
                            hours,
                            0f,
                            100f);

                    if (isNight)
                    {
                        resident.sleepDebt =
                            Clamp(
                                resident.sleepDebt +
                                rules.sleepDebtAddedPerNightWorkHour *
                                hours * 0.35f,
                                0f,
                                100f);
                    }
                    break;
            }

            float excessiveDebt =
                Math.Max(
                    0f,
                    resident.sleepDebt -
                    rules.chronicSleepDebtThreshold);

            if (excessiveDebt > 0f)
            {
                resident.mood =
                    Clamp(
                        resident.mood -
                        excessiveDebt *
                        rules.moodLossPerExcessSleepDebtHour *
                        hours,
                        0f,
                        100f);
            }
        }

        private static float Clamp(
            float value,
            float min,
            float max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }

    public static class ConstructionSystem
    {
        public static void Advance(
            SettlementGameplayState settlement,
            double gameHours)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));

            if (gameHours <= 0.0)
                return;

            float averageEfficiency =
                CalculateAvailableLaborEfficiency(
                    settlement);

            for (int i = 0; i < settlement.buildings.Count; i++)
            {
                BuildingRuntimeState building =
                    settlement.buildings[i];

                if (building == null ||
                    building.IsConstructed ||
                    building.IsDestroyed ||
                    building.assignedBuilders <= 0)
                {
                    continue;
                }

                double work =
                    gameHours *
                    building.assignedBuilders *
                    averageEfficiency;

                building.completedConstructionWorkHours =
                    Math.Min(
                        building.requiredConstructionWorkHours,
                        building.completedConstructionWorkHours +
                        work);
            }
        }

        private static float CalculateAvailableLaborEfficiency(
            SettlementGameplayState settlement)
        {
            if (settlement.residents.Count == 0)
                return 1f;

            float total = 0f;
            int count = 0;

            for (int i = 0; i < settlement.residents.Count; i++)
            {
                ResidentState resident =
                    settlement.residents[i];

                if (resident == null ||
                    resident.activity ==
                    ResidentActivity.Sleeping)
                {
                    continue;
                }

                total +=
                    resident.GetWorkEfficiency();

                count++;
            }

            return count > 0
                ? total / count
                : 0f;
        }
    }

    public static class SettlementEconomySystem
    {
        public static void Advance(
            SettlementGameplayState settlement,
            double gameHours,
            EconomyRules rules,
            PopulationRules populationRules)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));

            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            if (populationRules == null)
                throw new ArgumentNullException(nameof(populationRules));

            if (gameHours <= 0.0)
                return;

            float laborFactor =
                CalculateLaborFactor(
                    settlement);

            ProduceWheat(
                settlement,
                gameHours,
                laborFactor,
                rules);

            ProduceBread(
                settlement,
                gameHours,
                laborFactor,
                rules);

            ConsumeFood(
                settlement,
                gameHours,
                rules,
                populationRules);
        }

        private static void ProduceWheat(
            SettlementGameplayState settlement,
            double gameHours,
            float laborFactor,
            EconomyRules rules)
        {
            int farms =
                settlement.CountOperationalBuildings(
                    BuildingRole.Farm);

            if (farms <= 0 ||
                laborFactor <= 0f)
            {
                return;
            }

            settlement.simulation.wheatProduction +=
                farms *
                rules.wheatPerFarmPerGameHour *
                gameHours *
                laborFactor;

            int whole =
                (int)Math.Floor(
                    settlement.simulation.wheatProduction);

            if (whole <= 0)
                return;

            settlement.simulation.wheatProduction -=
                whole;

            settlement.inventory.Add(
                GameplayResourceType.Wheat,
                whole);
        }

        private static void ProduceBread(
            SettlementGameplayState settlement,
            double gameHours,
            float laborFactor,
            EconomyRules rules)
        {
            int mills =
                settlement.CountOperationalBuildings(
                    BuildingRole.Mill);

            if (mills <= 0 ||
                laborFactor <= 0f ||
                rules.wheatPerBread <= 0.0)
            {
                return;
            }

            settlement.simulation.breadProduction +=
                mills *
                rules.breadPerMillPerGameHour *
                gameHours *
                laborFactor;

            int desiredBread =
                (int)Math.Floor(
                    settlement.simulation.breadProduction);

            if (desiredBread <= 0)
                return;

            int wheatAvailable =
                settlement.inventory.Get(
                    GameplayResourceType.Wheat);

            int breadFromWheat =
                (int)Math.Floor(
                    wheatAvailable /
                    rules.wheatPerBread);

            int produced =
                Math.Min(
                    desiredBread,
                    breadFromWheat);

            if (produced <= 0)
                return;

            int wheatCost =
                (int)Math.Ceiling(
                    produced *
                    rules.wheatPerBread);

            settlement.inventory.TryRemove(
                GameplayResourceType.Wheat,
                wheatCost);

            settlement.inventory.Add(
                GameplayResourceType.Bread,
                produced);

            settlement.simulation.breadProduction -=
                produced;
        }

        private static void ConsumeFood(
            SettlementGameplayState settlement,
            double gameHours,
            EconomyRules rules,
            PopulationRules populationRules)
        {
            if (settlement.residents.Count == 0)
                return;

            settlement.simulation.breadConsumption +=
                settlement.residents.Count *
                rules.breadPerResidentPerGameDay *
                (gameHours / 24.0);

            int requested =
                (int)Math.Floor(
                    settlement.simulation.breadConsumption);

            if (requested <= 0)
                return;

            settlement.simulation.breadConsumption -=
                requested;

            int available =
                settlement.inventory.Get(
                    GameplayResourceType.Bread);

            int consumed =
                Math.Min(
                    requested,
                    available);

            if (consumed > 0)
            {
                settlement.inventory.TryRemove(
                    GameplayResourceType.Bread,
                    consumed);
            }

            int missing =
                requested -
                consumed;

            if (missing > 0)
            {
                settlement.happiness =
                    Clamp(
                        settlement.happiness -
                        missing *
                        populationRules.hungerHappinessLossPerMissingBread,
                        0f,
                        100f);
            }
        }

        private static float CalculateLaborFactor(
            SettlementGameplayState settlement)
        {
            if (settlement.residents.Count == 0)
                return 0f;

            float total = 0f;
            int available = 0;

            for (int i = 0; i < settlement.residents.Count; i++)
            {
                ResidentState resident =
                    settlement.residents[i];

                if (resident == null ||
                    resident.activity ==
                    ResidentActivity.Sleeping)
                {
                    continue;
                }

                total +=
                    resident.GetWorkEfficiency();

                available++;
            }

            if (available == 0)
                return 0f;

            float availability =
                (float)available /
                settlement.residents.Count;

            return Clamp(
                total / available *
                availability,
                0f,
                1.25f);
        }

        private static float Clamp(
            float value,
            float min,
            float max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
