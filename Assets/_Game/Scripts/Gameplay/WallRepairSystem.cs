using System;
using System.Collections.Generic;
using LittleCastle.Building;

namespace LittleCastle.Gameplay
{
    /// <summary>
    /// Lightweight post-combat wall repair.
    ///
    /// Combat decides when an area is safe enough to call this system. It does
    /// not run automatically during a siege. Destroyed walls require a rebuild;
    /// damaged surviving walls can be restored gradually by residents.
    /// </summary>
    public static class WallRepairSystem
    {
        public static int AdvanceAfterBattle(
            WallRuntimeRegistry walls,
            SettlementGameplayState settlement,
            double gameHours,
            WallRepairRules rules)
        {
            if (walls == null)
                throw new ArgumentNullException(nameof(walls));

            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));

            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            if (gameHours <= 0.0 ||
                settlement.ownerPlayerId < 0)
            {
                return 0;
            }

            int availableResidents =
                CountAvailableResidents(
                    settlement);

            if (availableResidents <= 0)
                return 0;

            var all =
                new List<WallRuntimeState>();

            walls.CopyAllTo(all);

            var damaged =
                new List<WallRuntimeState>();

            for (int i = 0; i < all.Count; i++)
            {
                WallRuntimeState wall =
                    all[i];

                if (wall.ownerPlayerId ==
                        settlement.ownerPlayerId &&
                    wall.IsDamaged)
                {
                    damaged.Add(wall);
                }
            }

            if (damaged.Count == 0)
                return 0;

            int repairedWalls = 0;
            int remainingResidents =
                availableResidents;

            for (int i = 0;
                 i < damaged.Count &&
                 remainingResidents > 0;
                 i++)
            {
                int wallsRemaining =
                    damaged.Count - i;

                int fairShare =
                    Math.Max(
                        1,
                        remainingResidents /
                        wallsRemaining);

                int workers =
                    Math.Min(
                        Math.Max(
                            1,
                            rules.maximumResidentsPerWall),
                        fairShare);

                float repair =
                    workers *
                    Math.Max(
                        0f,
                        rules.hitPointsPerResidentGameHour) *
                    (float)gameHours;

                if (walls.Repair(
                    damaged[i].wallId,
                    repair))
                {
                    repairedWalls++;
                }

                remainingResidents -=
                    workers;
            }

            return repairedWalls;
        }

        private static int CountAvailableResidents(
            SettlementGameplayState settlement)
        {
            int count = 0;

            for (int i = 0;
                 i < settlement.residents.Count;
                 i++)
            {
                ResidentState resident =
                    settlement.residents[i];

                if (resident == null ||
                    resident.activity ==
                        ResidentActivity.Sleeping ||
                    resident.activity ==
                        ResidentActivity.Defending ||
                    resident.activity ==
                        ResidentActivity.Emergency)
                {
                    continue;
                }

                if (resident.energy <= 10f)
                    continue;

                count++;
            }

            return count;
        }
    }
}
