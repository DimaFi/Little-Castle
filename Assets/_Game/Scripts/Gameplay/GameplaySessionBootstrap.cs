using System;
using System.Collections.Generic;
using LittleCastle.World;

namespace LittleCastle.Gameplay
{
    /// <summary>
    /// Converts deterministic session/macro data into mutable gameplay state.
    ///
    /// The macro plan stays immutable base-world data. This bootstrap creates
    /// the runtime layer once when the authoritative session starts.
    /// </summary>
    public static class GameplaySessionBootstrap
    {
        public static void ImportNeutralSettlements(
            GameplaySessionState state,
            MacroWorldPlan macroPlan)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (macroPlan == null)
                return;

            for (int i = 0;
                 i < macroPlan.PointFeatures.Count;
                 i++)
            {
                WorldPointFeatureData feature =
                    macroPlan.PointFeatures[i];

                if (feature.kind !=
                    WorldFeatureKind.NeutralSettlement)
                {
                    continue;
                }

                if (state.FindSettlement(
                        feature.stableId) != null)
                {
                    continue;
                }

                state.settlements.Add(
                    CreateNeutralVillage(
                        state.worldSeed,
                        feature));
            }
        }

        public static void CreatePlayerCapitals(
            GameplaySessionState state,
            IReadOnlyList<int> playerIds,
            IReadOnlyList<WorldPlayerStartData> starts)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (playerIds == null)
                throw new ArgumentNullException(nameof(playerIds));

            if (starts == null)
                throw new ArgumentNullException(nameof(starts));

            if (playerIds.Count != starts.Count)
            {
                throw new ArgumentException(
                    "Player ID count must match selected start count.");
            }

            for (int i = 0; i < playerIds.Count; i++)
            {
                int playerId =
                    playerIds[i];

                if (state.FindPlayer(playerId) != null)
                    continue;

                WorldPlayerStartData start =
                    starts[i];

                long settlementId =
                    state.AllocateRuntimeId();

                long mainHouseId =
                    state.AllocateRuntimeId();

                var settlement =
                    new SettlementGameplayState
                    {
                        settlementId =
                            settlementId,
                        ownerPlayerId =
                            playerId,
                        isNeutral = false,
                        sourceArchetypeId =
                            "player_capital",
                        worldX =
                            start.worldPosition.x,
                        worldZ =
                            start.worldPosition.z,
                        mainHouseBuildingId =
                            mainHouseId,
                        happiness = 70f
                    };

                settlement.buildings.Add(
                    new BuildingRuntimeState
                    {
                        buildingId =
                            mainHouseId,
                        ownerPlayerId =
                            playerId,
                        archetypeId =
                            "main_house_level_1",
                        role =
                            BuildingRole.MainHouse,
                        level = 1,
                        maxHitPoints = 500f,
                        hitPoints = 500f,
                        requiredConstructionWorkHours = 0.0,
                        completedConstructionWorkHours = 0.0
                    });

                // Minimal starting buffer. Balance is intentionally provisional.
                settlement.inventory.Set(
                    GameplayResourceType.Bread,
                    12);

                settlement.inventory.Set(
                    GameplayResourceType.Wood,
                    20);

                settlement.inventory.Set(
                    GameplayResourceType.Coin,
                    20);

                state.settlements.Add(
                    settlement);

                state.players.Add(
                    new PlayerGameplayState
                    {
                        playerId = playerId,
                        capitalSettlementId =
                            settlementId
                    });
            }
        }

        private static SettlementGameplayState CreateNeutralVillage(
            int worldSeed,
            WorldPointFeatureData feature)
        {
            var settlement =
                new SettlementGameplayState
                {
                    settlementId =
                        feature.stableId,
                    ownerPlayerId = -1,
                    isNeutral = true,
                    sourceArchetypeId =
                        feature.archetypeId ??
                        string.Empty,
                    worldX =
                        feature.worldPosition.x,
                    worldZ =
                        feature.worldPosition.y,
                    happiness = 65f
                };

            int x =
                StableLow32(
                    feature.stableId);

            int z =
                StableHigh32(
                    feature.stableId);

            int wheat =
                20 +
                DeterministicRange(
                    worldSeed,
                    x,
                    z,
                    "neutral_wheat",
                    0,
                    31);

            int bread =
                10 +
                DeterministicRange(
                    worldSeed,
                    x,
                    z,
                    "neutral_bread",
                    0,
                    16);

            int wood =
                12 +
                DeterministicRange(
                    worldSeed,
                    x,
                    z,
                    "neutral_wood",
                    0,
                    25);

            settlement.inventory.Set(
                GameplayResourceType.Wheat,
                wheat);

            settlement.inventory.Set(
                GameplayResourceType.Bread,
                bread);

            settlement.inventory.Set(
                GameplayResourceType.Wood,
                wood);

            settlement.market.offers.Add(
                Offer(
                    "wheat",
                    GameplayResourceType.Wheat,
                    1,
                    wheat,
                    8.0));

            settlement.market.offers.Add(
                Offer(
                    "bread",
                    GameplayResourceType.Bread,
                    2,
                    bread,
                    5.0));

            settlement.market.offers.Add(
                Offer(
                    "wood",
                    GameplayResourceType.Wood,
                    2,
                    wood,
                    6.0));

            return settlement;
        }

        private static TradeOfferState Offer(
            string id,
            GameplayResourceType resource,
            int price,
            int stock,
            double restockPerDay)
        {
            return new TradeOfferState
            {
                offerId = id,
                resource = resource,
                priceCoinsPerUnit = price,
                availableUnits = stock,
                maxUnits = stock,
                restockUnitsPerGameDay =
                    restockPerDay
            };
        }

        private static int DeterministicRange(
            int seed,
            int x,
            int z,
            string key,
            int minInclusive,
            int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;

            float roll =
                DeterministicHash.Hash01(
                    seed,
                    x,
                    z,
                    DeterministicHash.String32(key));

            int width =
                maxExclusive -
                minInclusive;

            int value =
                minInclusive +
                (int)Math.Floor(
                    roll *
                    width);

            return Math.Min(
                maxExclusive - 1,
                value);
        }

        private static int StableLow32(long value)
        {
            unchecked
            {
                return (int)(
                    (ulong)value &
                    0xFFFFFFFFUL);
            }
        }

        private static int StableHigh32(long value)
        {
            unchecked
            {
                return (int)(
                    (ulong)value >>
                    32);
            }
        }
    }
}
