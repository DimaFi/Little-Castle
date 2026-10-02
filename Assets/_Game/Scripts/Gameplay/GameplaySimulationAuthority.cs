using System;
using LittleCastle.World;

namespace LittleCastle.Gameplay
{
    /// <summary>
    /// Server/host gameplay simulation facade.
    ///
    /// Call Advance only with authoritative game-time delta. This class never
    /// reads Time.deltaTime and never depends on rendered GameObjects.
    /// </summary>
    public sealed class GameplaySimulationAuthority
    {
        private readonly GameplaySessionState state;
        private readonly GameplayRules rules;
        private readonly WorldEventCatalog eventCatalog;
        private readonly NightlyRitualEngine rituals;
        private readonly MatchRuleEngine matchRules;

        public GameplaySessionState State => state;
        public GameplayRules Rules => rules;
        public WorldEventCatalog EventCatalog => eventCatalog;
        public MatchRuleEngine MatchRules => matchRules;

        public GameplaySimulationAuthority(
            GameplaySessionState state,
            GameplayRules rules = null,
            WorldEventCatalog eventCatalog = null,
            MatchRuleEngine matchRules = null)
        {
            this.state =
                state ??
                throw new ArgumentNullException(nameof(state));

            this.rules =
                rules ??
                GameplayRules.CreateDefault();

            this.eventCatalog =
                eventCatalog ??
                WorldEventCatalog.CreateDefault();

            this.matchRules =
                matchRules ??
                new MatchRuleEngine();

            rituals =
                new NightlyRitualEngine(
                    this.eventCatalog,
                    this.rules.rituals);
        }

        public void Advance(
            WorldTimeState timeState,
            double gameHours,
            bool isNight)
        {
            if (gameHours < 0.0 ||
                double.IsNaN(gameHours) ||
                double.IsInfinity(gameHours))
            {
                throw new ArgumentOutOfRangeException(nameof(gameHours));
            }

            if (state.lastObservedNight &&
                !isNight)
            {
                GrantDawnRitualRolls(
                    timeState.dayIndex);
            }

            AdvanceRulers(gameHours);

            for (int i = 0;
                 i < state.settlements.Count;
                 i++)
            {
                SettlementGameplayState settlement =
                    state.settlements[i];

                AdvanceResidents(
                    settlement,
                    gameHours,
                    isNight);

                SettlementHappinessSystem.Advance(
                    settlement,
                    gameHours,
                    rules.population);

                ConstructionSystem.Advance(
                    settlement,
                    gameHours);

                SettlementEconomySystem.Advance(
                    settlement,
                    gameHours,
                    rules.economy,
                    rules.population);

                BarracksTrainingSystem.Advance(
                    settlement,
                    gameHours);

                NeutralTradeSystem.AdvanceMarket(
                    settlement,
                    gameHours);
            }

            state.lastObservedNight =
                isNight;

            state.lastObservedDay =
                Math.Max(
                    1,
                    timeState.dayIndex);

            matchRules.Evaluate(state);
        }

        public bool SetResidentForcedNightWork(
            long settlementId,
            long residentId,
            bool forced)
        {
            SettlementGameplayState settlement =
                state.FindSettlement(
                    settlementId);

            if (settlement == null)
                return false;

            for (int i = 0;
                 i < settlement.residents.Count;
                 i++)
            {
                ResidentState resident =
                    settlement.residents[i];

                if (resident != null &&
                    resident.residentId ==
                    residentId)
                {
                    resident.forceWorkAtNight =
                        forced;

                    return true;
                }
            }

            return false;
        }

        public void DefeatRuler(
            int playerId)
        {
            PlayerGameplayState player =
                state.FindPlayer(playerId);

            if (player == null ||
                player.matchStatus !=
                    PlayerMatchStatus.Active)
            {
                return;
            }

            player.ruler.health = 0f;
            player.ruler.isRespawning = true;
            player.ruler.respawnRemainingGameHours =
                Math.Max(
                    0.01,
                    rules.ruler.respawnGameHours);
        }

        public bool Meditate(
            int playerId,
            double gameHours)
        {
            if (gameHours <= 0.0)
                return false;

            PlayerGameplayState player =
                state.FindPlayer(playerId);

            if (player == null ||
                player.matchStatus !=
                    PlayerMatchStatus.Active ||
                !player.ruler.IsAlive)
            {
                return false;
            }

            player.ruler.meditationGameHours +=
                gameHours;

            player.ruler.darkMagicExperience +=
                gameHours *
                rules.ruler.darkMagicXpPerMeditationHour;

            double xpPerLevel =
                Math.Max(
                    1.0,
                    rules.ruler.darkMagicXpPerLevel);

            int level =
                (int)Math.Floor(
                    player.ruler.darkMagicExperience /
                    xpPerLevel);

            player.ruler.darkMagicLevel =
                Math.Min(
                    Math.Max(
                        0,
                        level),
                    Math.Max(
                        0,
                        rules.ruler.maximumDarkMagicLevel));

            return true;
        }

        public bool TryRollBlessing(
            int playerId,
            out WorldEventRollResult result)
        {
            bool rolled =
                rituals.TryRollBlessing(
                    state,
                    playerId,
                    state.lastObservedDay,
                    out result);

            if (rolled)
            {
                WorldEventApplier.ApplyRoll(
                    state,
                    eventCatalog,
                    result);

                matchRules.Evaluate(state);
            }

            return rolled;
        }

        public bool TryRollCurse(
            int playerId,
            out WorldEventRollResult result)
        {
            bool rolled =
                rituals.TryRollCurse(
                    state,
                    playerId,
                    state.lastObservedDay,
                    rules.ruler.maximumDarkMagicLevel,
                    out result);

            if (rolled)
            {
                WorldEventApplier.ApplyRoll(
                    state,
                    eventCatalog,
                    result);

                matchRules.Evaluate(state);
            }

            return rolled;
        }

        public void GrantDawnRitualRolls(
            int dayIndex)
        {
            if (dayIndex <= 0)
                return;

            for (int i = 0;
                 i < state.players.Count;
                 i++)
            {
                PlayerGameplayState player =
                    state.players[i];

                if (player.matchStatus !=
                    PlayerMatchStatus.Active ||
                    player.rituals.lastGrantedDawnDay ==
                    dayIndex)
                {
                    continue;
                }

                player.rituals.blessingRollsAvailable +=
                    Math.Max(
                        0,
                        rules.rituals.blessingRollsPerDawn);

                player.rituals.curseRollsAvailable +=
                    Math.Max(
                        0,
                        rules.rituals.curseRollsPerDawn);

                player.rituals.lastGrantedDawnDay =
                    dayIndex;
            }
        }

        private void AdvanceRulers(double gameHours)
        {
            if (gameHours <= 0.0)
                return;

            for (int i = 0;
                 i < state.players.Count;
                 i++)
            {
                RulerState ruler =
                    state.players[i].ruler;

                if (!ruler.isRespawning)
                    continue;

                ruler.respawnRemainingGameHours =
                    Math.Max(
                        0.0,
                        ruler.respawnRemainingGameHours -
                        gameHours);

                if (ruler.respawnRemainingGameHours >
                    0.0)
                {
                    continue;
                }

                ruler.isRespawning = false;
                ruler.health =
                    Math.Max(
                        1f,
                        ruler.maxHealth *
                        0.35f);
            }
        }

        private void AdvanceResidents(
            SettlementGameplayState settlement,
            double gameHours,
            bool isNight)
        {
            for (int i = 0;
                 i < settlement.residents.Count;
                 i++)
            {
                ResidentState resident =
                    settlement.residents[i];

                if (resident == null)
                    continue;

                ResidentScheduleSystem.UpdatePreferredActivity(
                    resident,
                    isNight);

                ResidentNeedsSystem.Advance(
                    resident,
                    gameHours,
                    isNight,
                    rules.population);
            }
        }
    }
}
