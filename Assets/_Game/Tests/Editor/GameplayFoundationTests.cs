using LittleCastle.Gameplay;
using LittleCastle.World;
using NUnit.Framework;

namespace LittleCastle.Tests
{
    public sealed class GameplayFoundationTests
    {
        [Test]
        public void RulerDefeat_RespawnsWithoutDefeatingPlayer()
        {
            GameplaySessionState state =
                CreateTwoPlayerSession();

            var authority =
                new GameplaySimulationAuthority(
                    state);

            authority.DefeatRuler(1);

            Assert.That(
                state.FindPlayer(1).matchStatus,
                Is.EqualTo(PlayerMatchStatus.Active));

            Assert.That(
                state.FindPlayer(1).ruler.isRespawning,
                Is.True);

            authority.Advance(
                new WorldTimeState(1, 12 * 60),
                authority.Rules.ruler.respawnGameHours,
                false);

            Assert.That(
                state.FindPlayer(1).ruler.IsAlive,
                Is.True);

            Assert.That(
                state.FindPlayer(1).matchStatus,
                Is.EqualTo(PlayerMatchStatus.Active));
        }

        [Test]
        public void MainHouseDestruction_DefeatsOwnerAndResolvesWinner()
        {
            GameplaySessionState state =
                CreateTwoPlayerSession();

            SettlementGameplayState firstCapital =
                state.FindCapital(1);

            firstCapital.FindBuilding(
                firstCapital.mainHouseBuildingId)
                .hitPoints = 0f;

            var rules =
                new MatchRuleEngine();

            rules.Evaluate(state);

            Assert.That(
                state.FindPlayer(1).matchStatus,
                Is.EqualTo(PlayerMatchStatus.Defeated));

            Assert.That(
                state.FindPlayer(2).matchStatus,
                Is.EqualTo(PlayerMatchStatus.Victorious));
        }

        [Test]
        public void Economy_UsesGameHoursForWheatAndBread()
        {
            var settlement =
                new SettlementGameplayState();

            settlement.residents.Add(
                new ResidentState
                {
                    residentId = 1,
                    energy = 100f,
                    mood = 100f,
                    activity = ResidentActivity.Working
                });

            settlement.buildings.Add(
                CompletedBuilding(
                    10,
                    BuildingRole.Farm));

            settlement.buildings.Add(
                CompletedBuilding(
                    11,
                    BuildingRole.Mill));

            GameplayRules rules =
                GameplayRules.CreateDefault();

            SettlementEconomySystem.Advance(
                settlement,
                2.0,
                rules.economy,
                rules.population);

            Assert.That(
                settlement.inventory.Get(
                    GameplayResourceType.Bread),
                Is.GreaterThan(0));

            Assert.That(
                settlement.simulation.wheatProduction,
                Is.GreaterThanOrEqualTo(0.0));
        }

        [Test]
        public void BarracksTraining_SpendsCostAndCompletesByGameTime()
        {
            var state =
                new GameplaySessionState();

            var settlement =
                new SettlementGameplayState
                {
                    settlementId = 1,
                    ownerPlayerId = 1
                };

            BuildingRuntimeState barracks =
                CompletedBuilding(
                    100,
                    BuildingRole.Barracks);

            settlement.buildings.Add(
                barracks);

            settlement.inventory.Set(
                GameplayResourceType.Bread,
                10);

            settlement.inventory.Set(
                GameplayResourceType.Coin,
                10);

            GameplayRules rules =
                GameplayRules.CreateDefault();

            bool queued =
                BarracksTrainingSystem.TryEnqueue(
                    state,
                    settlement,
                    barracks.buildingId,
                    "soldier_basic",
                    1,
                    rules,
                    out long orderId);

            Assert.That(queued, Is.True);
            Assert.That(orderId, Is.GreaterThan(0));
            Assert.That(
                settlement.inventory.Get(
                    GameplayResourceType.Bread),
                Is.EqualTo(8));
            Assert.That(
                settlement.inventory.Get(
                    GameplayResourceType.Coin),
                Is.EqualTo(5));

            BarracksTrainingSystem.Advance(
                settlement,
                6.0);

            Assert.That(
                settlement.units.Count,
                Is.EqualTo(1));
            Assert.That(
                settlement.units[0].unitArchetypeId,
                Is.EqualTo("soldier_basic"));
            Assert.That(
                settlement.units[0].count,
                Is.EqualTo(1));
        }

        [Test]
        public void NeutralVillageTrade_TransfersStockForCoins()
        {
            var buyer =
                new SettlementGameplayState();

            buyer.inventory.Set(
                GameplayResourceType.Coin,
                50);

            var village =
                new SettlementGameplayState
                {
                    isNeutral = true
                };

            village.market.offers.Add(
                new TradeOfferState
                {
                    offerId = "bread",
                    resource =
                        GameplayResourceType.Bread,
                    priceCoinsPerUnit = 2,
                    availableUnits = 20,
                    maxUnits = 20
                });

            bool bought =
                NeutralTradeSystem.TryBuyFromVillage(
                    buyer,
                    village,
                    "bread",
                    5);

            Assert.That(bought, Is.True);
            Assert.That(
                buyer.inventory.Get(
                    GameplayResourceType.Coin),
                Is.EqualTo(40));
            Assert.That(
                buyer.inventory.Get(
                    GameplayResourceType.Bread),
                Is.EqualTo(5));
            Assert.That(
                village.market.offers[0].availableUnits,
                Is.EqualTo(15));
        }

        [Test]
        public void Dawn_GrantsOneBlessingAndCurseRoll()
        {
            GameplaySessionState state =
                CreateTwoPlayerSession();

            state.lastObservedNight = true;

            var authority =
                new GameplaySimulationAuthority(
                    state);

            authority.Advance(
                new WorldTimeState(2, 6 * 60),
                0.25,
                false);

            PlayerGameplayState player =
                state.FindPlayer(1);

            Assert.That(
                player.rituals.blessingRollsAvailable,
                Is.EqualTo(1));

            Assert.That(
                player.rituals.curseRollsAvailable,
                Is.EqualTo(1));
        }

        [Test]
        public void CurseRoll_IsDeterministicForSameSessionInputs()
        {
            GameplaySessionState first =
                CreateTwoPlayerSession();

            GameplaySessionState second =
                CreateTwoPlayerSession();

            first.worldSeed = 4321;
            second.worldSeed = 4321;

            first.lastObservedDay = 4;
            second.lastObservedDay = 4;

            first.FindPlayer(1).ruler.darkMagicLevel = 8;
            second.FindPlayer(1).ruler.darkMagicLevel = 8;

            first.FindPlayer(1).rituals.curseRollsAvailable = 1;
            second.FindPlayer(1).rituals.curseRollsAvailable = 1;

            var firstAuthority =
                new GameplaySimulationAuthority(
                    first);

            var secondAuthority =
                new GameplaySimulationAuthority(
                    second);

            Assert.That(
                firstAuthority.TryRollCurse(
                    1,
                    out WorldEventRollResult a),
                Is.True);

            Assert.That(
                secondAuthority.TryRollCurse(
                    1,
                    out WorldEventRollResult b),
                Is.True);

            Assert.That(
                b.primaryEventId,
                Is.EqualTo(a.primaryEventId));

            Assert.That(
                b.primaryPower,
                Is.EqualTo(a.primaryPower));

            Assert.That(
                b.backlashTriggered,
                Is.EqualTo(a.backlashTriggered));

            Assert.That(
                b.backlashEventId,
                Is.EqualTo(a.backlashEventId));
        }

        [Test]
        public void CurseEvent_AppliesToOtherPlayerNotSource()
        {
            GameplaySessionState state =
                CreateTwoPlayerSession();

            SettlementGameplayState source =
                state.FindCapital(1);

            SettlementGameplayState target =
                state.FindCapital(2);

            source.inventory.Set(
                GameplayResourceType.Bread,
                100);

            target.inventory.Set(
                GameplayResourceType.Bread,
                100);

            target.inventory.Set(
                GameplayResourceType.Coin,
                100);

            WorldEventCatalog catalog =
                WorldEventCatalog.CreateDefault();

            WorldEventApplier.ApplyRoll(
                state,
                catalog,
                new WorldEventRollResult
                {
                    sourcePlayerId = 1,
                    primaryEventId =
                        "curse_bandit_theft",
                    primaryPower = 1f,
                    targetsOtherPlayers = true,
                    backlashTriggered = false
                });

            Assert.That(
                source.inventory.Get(
                    GameplayResourceType.Bread),
                Is.EqualTo(100));

            Assert.That(
                target.inventory.Get(
                    GameplayResourceType.Bread),
                Is.LessThan(100));

            Assert.That(
                target.inventory.Get(
                    GameplayResourceType.Coin),
                Is.LessThan(100));
        }

        [Test]
        public void NewSave_StoresGameplaySeedWithoutMaterializedMap()
        {
            WorldSaveState save =
                WorldSaveState.CreateNew(
                    null,
                    9876);

            Assert.That(
                save.gameplay,
                Is.Not.Null);

            Assert.That(
                save.gameplay.worldSeed,
                Is.EqualTo(9876));

            Assert.That(
                save.runtimeDelta,
                Is.Not.Null);
        }

        [Test]
        public void MountainBarrier_IsRepresentedInTerrainMask()
        {
            Assert.That(
                TerrainClassMask.MountainBarrier.Contains(
                    TerrainClass.MountainBarrier),
                Is.True);

            Assert.That(
                TerrainClassMask.Plains.Contains(
                    TerrainClass.MountainBarrier),
                Is.False);
        }

        private static GameplaySessionState CreateTwoPlayerSession()
        {
            var state =
                new GameplaySessionState
                {
                    worldSeed = 1234,
                    lastObservedDay = 1
                };

            state.players.Add(
                new PlayerGameplayState
                {
                    playerId = 1,
                    capitalSettlementId = 101
                });

            state.players.Add(
                new PlayerGameplayState
                {
                    playerId = 2,
                    capitalSettlementId = 202
                });

            state.settlements.Add(
                Capital(
                    101,
                    1,
                    1001));

            state.settlements.Add(
                Capital(
                    202,
                    2,
                    2002));

            return state;
        }

        private static SettlementGameplayState Capital(
            long settlementId,
            int playerId,
            long mainHouseId)
        {
            var settlement =
                new SettlementGameplayState
                {
                    settlementId = settlementId,
                    ownerPlayerId = playerId,
                    mainHouseBuildingId =
                        mainHouseId,
                    happiness = 70f
                };

            settlement.buildings.Add(
                CompletedBuilding(
                    mainHouseId,
                    BuildingRole.MainHouse));

            return settlement;
        }

        private static BuildingRuntimeState CompletedBuilding(
            long id,
            BuildingRole role)
        {
            return new BuildingRuntimeState
            {
                buildingId = id,
                role = role,
                maxHitPoints = 100f,
                hitPoints = 100f,
                requiredConstructionWorkHours = 0.0,
                completedConstructionWorkHours = 0.0
            };
        }
    }
}
