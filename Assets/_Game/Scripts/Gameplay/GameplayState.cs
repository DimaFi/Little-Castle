using System;
using System.Collections.Generic;

namespace LittleCastle.Gameplay
{
    public enum PlayerMatchStatus : byte
    {
        Active = 0,
        Defeated = 1,
        Victorious = 2
    }

    public enum BuildingRole : byte
    {
        Other = 0,
        MainHouse = 1,
        House = 2,
        Farm = 3,
        Mill = 4,
        Market = 5,
        Church = 6,
        Barracks = 7,
        ArcherTower = 8
    }

    public enum ResidentActivity : byte
    {
        Idle = 0,
        Sleeping = 1,
        Working = 2,
        Defending = 3,
        Emergency = 4
    }

    [Serializable]
    public sealed class ResidentState
    {
        public long residentId;
        public long homeBuildingId;
        public long assignedBuildingId;

        public float energy = 100f;
        public float sleepDebt;
        public float mood = 70f;
        public float baseWorkEfficiency = 1f;

        public ResidentActivity activity =
            ResidentActivity.Idle;

        public bool forceWorkAtNight;

        public float GetWorkEfficiency()
        {
            float energyFactor =
                Lerp(0.35f, 1f, energy / 100f);

            float moodFactor =
                Lerp(0.55f, 1.05f, mood / 100f);

            float chronicSleepPenalty =
                sleepDebt <= 20f
                    ? 1f
                    : Lerp(
                        1f,
                        0.45f,
                        (sleepDebt - 20f) / 80f);

            return Clamp(
                baseWorkEfficiency *
                energyFactor *
                moodFactor *
                chronicSleepPenalty,
                0.15f,
                1.25f);
        }

        private static float Lerp(
            float a,
            float b,
            float t)
        {
            t = Clamp(t, 0f, 1f);
            return a + (b - a) * t;
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

    [Serializable]
    public sealed class RulerState
    {
        public float maxHealth = 100f;
        public float health = 100f;
        public bool isRespawning;
        public double respawnRemainingGameHours;

        public int darkMagicLevel;
        public double darkMagicExperience;
        public double meditationGameHours;

        public bool IsAlive =>
            !isRespawning &&
            health > 0f;
    }

    [Serializable]
    public sealed class BuildingRuntimeState
    {
        public long buildingId;
        public int ownerPlayerId;
        public string archetypeId = string.Empty;
        public BuildingRole role = BuildingRole.Other;
        public int level = 1;

        public float worldX;
        public float worldY;
        public float worldZ;
        public float yawDegrees;

        public float maxHitPoints = 100f;
        public float hitPoints = 100f;

        public double requiredConstructionWorkHours;
        public double completedConstructionWorkHours;
        public int assignedBuilders;

        public bool IsDestroyed =>
            hitPoints <= 0f;

        public bool IsConstructed =>
            requiredConstructionWorkHours <= 0.0 ||
            completedConstructionWorkHours >=
            requiredConstructionWorkHours;

        public float ConstructionProgress
        {
            get
            {
                if (requiredConstructionWorkHours <= 0.0)
                    return 1f;

                double progress =
                    completedConstructionWorkHours /
                    requiredConstructionWorkHours;

                if (progress <= 0.0)
                    return 0f;

                if (progress >= 1.0)
                    return 1f;

                return (float)progress;
            }
        }
    }

    [Serializable]
    public sealed class UnitStackState
    {
        public string unitArchetypeId = string.Empty;
        public int count;
    }

    [Serializable]
    public sealed class TrainingOrderState
    {
        public long orderId;
        public string unitArchetypeId = string.Empty;
        public int quantity = 1;
        public double remainingGameHours;
    }

    [Serializable]
    public sealed class BarracksQueueState
    {
        public long barracksBuildingId;
        public List<TrainingOrderState> orders =
            new List<TrainingOrderState>();
    }

    [Serializable]
    public sealed class TradeOfferState
    {
        public string offerId = string.Empty;
        public GameplayResourceType resource;
        public int priceCoinsPerUnit = 1;
        public int availableUnits;
        public int maxUnits;
        public double restockUnitsPerGameDay;
        public double restockRemainder;
    }

    [Serializable]
    public sealed class NeutralMarketState
    {
        public List<TradeOfferState> offers =
            new List<TradeOfferState>();
    }

    [Serializable]
    public sealed class SettlementSimulationAccumulators
    {
        public double wheatProduction;
        public double breadProduction;
        public double breadConsumption;
    }

    [Serializable]
    public sealed class SettlementGameplayState
    {
        public long settlementId;
        public int ownerPlayerId = -1;
        public bool isNeutral;
        public string sourceArchetypeId = string.Empty;
        public float worldX;
        public float worldZ;
        public long mainHouseBuildingId;

        public float happiness = 65f;

        public ResourceInventory inventory =
            new ResourceInventory();

        public List<ResidentState> residents =
            new List<ResidentState>();

        public List<BuildingRuntimeState> buildings =
            new List<BuildingRuntimeState>();

        public List<UnitStackState> units =
            new List<UnitStackState>();

        public List<BarracksQueueState> barracksQueues =
            new List<BarracksQueueState>();

        public NeutralMarketState market =
            new NeutralMarketState();

        public SettlementSimulationAccumulators simulation =
            new SettlementSimulationAccumulators();

        public BuildingRuntimeState FindBuilding(
            long buildingId)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].buildingId == buildingId)
                    return buildings[i];
            }

            return null;
        }

        public BuildingRuntimeState FindFirstBuilding(
            BuildingRole role)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingRuntimeState building =
                    buildings[i];

                if (building.role == role &&
                    !building.IsDestroyed)
                {
                    return building;
                }
            }

            return null;
        }

        public int CountOperationalBuildings(
            BuildingRole role)
        {
            int count = 0;

            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingRuntimeState building =
                    buildings[i];

                if (building.role == role &&
                    building.IsConstructed &&
                    !building.IsDestroyed)
                {
                    count++;
                }
            }

            return count;
        }

        public void AddUnit(
            string unitArchetypeId,
            int amount)
        {
            if (amount <= 0 ||
                string.IsNullOrWhiteSpace(unitArchetypeId))
            {
                return;
            }

            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].unitArchetypeId !=
                    unitArchetypeId)
                {
                    continue;
                }

                units[i].count += amount;
                return;
            }

            units.Add(
                new UnitStackState
                {
                    unitArchetypeId =
                        unitArchetypeId,
                    count = amount
                });
        }
    }

    [Serializable]
    public sealed class NightRitualState
    {
        public int blessingRollsAvailable;
        public int curseRollsAvailable;
        public int rollsPerformed;
        public int lastGrantedDawnDay;
    }

    [Serializable]
    public sealed class PlayerGameplayState
    {
        public int playerId;
        public long capitalSettlementId;
        public PlayerMatchStatus matchStatus =
            PlayerMatchStatus.Active;
        public string lastOutcomeReason = string.Empty;

        public RulerState ruler =
            new RulerState();

        public NightRitualState rituals =
            new NightRitualState();
    }

    /// <summary>
    /// Serializable server-owned gameplay state.
    ///
    /// This is intentionally independent from rendered Unity objects. A client
    /// can unload a whole settlement and this state still remains authoritative.
    /// </summary>
    [Serializable]
    public sealed class GameplaySessionState
    {
        public int worldSeed;
        public long nextRuntimeId = 1;
        public int lastObservedDay = 1;
        public bool lastObservedNight;

        public List<PlayerGameplayState> players =
            new List<PlayerGameplayState>();

        public List<SettlementGameplayState> settlements =
            new List<SettlementGameplayState>();

        public long AllocateRuntimeId()
        {
            long id =
                Math.Max(
                    1L,
                    nextRuntimeId);

            nextRuntimeId =
                id == long.MaxValue
                    ? long.MaxValue
                    : id + 1;

            return id;
        }

        public PlayerGameplayState FindPlayer(
            int playerId)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].playerId == playerId)
                    return players[i];
            }

            return null;
        }

        public SettlementGameplayState FindSettlement(
            long settlementId)
        {
            for (int i = 0; i < settlements.Count; i++)
            {
                if (settlements[i].settlementId ==
                    settlementId)
                {
                    return settlements[i];
                }
            }

            return null;
        }

        public SettlementGameplayState FindCapital(
            int playerId)
        {
            PlayerGameplayState player =
                FindPlayer(playerId);

            return player == null
                ? null
                : FindSettlement(
                    player.capitalSettlementId);
        }
    }
}
