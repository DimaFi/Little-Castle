using System;

namespace LittleCastle.Gameplay
{
    public static class ResidentScheduleSystem
    {
        public static void UpdatePreferredActivity(
            ResidentState resident,
            bool isNight)
        {
            if (resident == null)
                throw new ArgumentNullException(nameof(resident));

            if (resident.activity ==
                    ResidentActivity.Emergency ||
                resident.activity ==
                    ResidentActivity.Defending)
            {
                return;
            }

            if (isNight)
            {
                resident.activity =
                    resident.forceWorkAtNight
                        ? ResidentActivity.Working
                        : ResidentActivity.Sleeping;

                return;
            }

            if (resident.activity ==
                    ResidentActivity.Sleeping)
            {
                resident.activity =
                    resident.assignedBuildingId != 0
                        ? ResidentActivity.Working
                        : ResidentActivity.Idle;
            }
        }
    }

    public static class SettlementHappinessSystem
    {
        public static void Advance(
            SettlementGameplayState settlement,
            double gameHours,
            PopulationRules rules)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));

            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            if (gameHours <= 0.0 ||
                settlement.residents.Count == 0)
            {
                return;
            }

            float totalMood = 0f;
            int count = 0;

            for (int i = 0;
                 i < settlement.residents.Count;
                 i++)
            {
                ResidentState resident =
                    settlement.residents[i];

                if (resident == null)
                    continue;

                totalMood += resident.mood;
                count++;
            }

            if (count == 0)
                return;

            float target =
                totalMood /
                count;

            float blend =
                Clamp01(
                    rules.settlementHappinessAdaptationPerGameHour *
                    (float)gameHours);

            settlement.happiness =
                Clamp(
                    settlement.happiness +
                    (target - settlement.happiness) *
                    blend,
                    0f,
                    100f);
        }

        private static float Clamp01(float value) =>
            Clamp(value, 0f, 1f);

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
