using System;
using System.Collections.Generic;

namespace LittleCastle.Gameplay
{
    [Serializable]
    public struct MatchRuleDecision
    {
        public int playerId;
        public PlayerMatchStatus status;
        public string reason;

        public MatchRuleDecision(
            int playerId,
            PlayerMatchStatus status,
            string reason)
        {
            this.playerId = playerId;
            this.status = status;
            this.reason = reason ?? string.Empty;
        }
    }

    /// <summary>
    /// Extensible match rule contract.
    ///
    /// Future modes may add score, time-limit, escort, relic or cooperative
    /// rules without changing the player/settlement state model.
    /// </summary>
    public interface IMatchRule
    {
        void Evaluate(
            GameplaySessionState state,
            List<MatchRuleDecision> output);
    }

    public sealed class MainHouseDestructionDefeatRule :
        IMatchRule
    {
        public void Evaluate(
            GameplaySessionState state,
            List<MatchRuleDecision> output)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (output == null)
                throw new ArgumentNullException(nameof(output));

            for (int i = 0; i < state.players.Count; i++)
            {
                PlayerGameplayState player =
                    state.players[i];

                if (player.matchStatus !=
                    PlayerMatchStatus.Active)
                {
                    continue;
                }

                SettlementGameplayState capital =
                    state.FindCapital(
                        player.playerId);

                if (capital == null)
                    continue;

                BuildingRuntimeState mainHouse =
                    capital.FindBuilding(
                        capital.mainHouseBuildingId);

                if (mainHouse != null &&
                    mainHouse.IsDestroyed)
                {
                    output.Add(
                        new MatchRuleDecision(
                            player.playerId,
                            PlayerMatchStatus.Defeated,
                            "main_house_destroyed"));
                }
            }
        }
    }

    public sealed class LastPlayerStandingVictoryRule :
        IMatchRule
    {
        public void Evaluate(
            GameplaySessionState state,
            List<MatchRuleDecision> output)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int activeCount = 0;
            int activePlayerId = -1;
            int participatingCount = 0;

            for (int i = 0; i < state.players.Count; i++)
            {
                PlayerGameplayState player =
                    state.players[i];

                if (player.matchStatus ==
                    PlayerMatchStatus.Victorious)
                {
                    return;
                }

                participatingCount++;

                if (player.matchStatus ==
                    PlayerMatchStatus.Active)
                {
                    activeCount++;
                    activePlayerId =
                        player.playerId;
                }
            }

            if (participatingCount > 1 &&
                activeCount == 1)
            {
                output.Add(
                    new MatchRuleDecision(
                        activePlayerId,
                        PlayerMatchStatus.Victorious,
                        "last_player_standing"));
            }
        }
    }

    public sealed class MatchRuleEngine
    {
        private readonly List<IMatchRule> rules =
            new List<IMatchRule>();

        private readonly List<MatchRuleDecision> decisions =
            new List<MatchRuleDecision>();

        public MatchRuleEngine()
        {
            rules.Add(
                new MainHouseDestructionDefeatRule());

            rules.Add(
                new LastPlayerStandingVictoryRule());
        }

        public void AddRule(IMatchRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            rules.Add(rule);
        }

        public int Evaluate(GameplaySessionState state)
        {
            decisions.Clear();

            for (int i = 0; i < rules.Count; i++)
            {
                rules[i].Evaluate(
                    state,
                    decisions);
            }

            int applied = 0;

            for (int i = 0; i < decisions.Count; i++)
            {
                MatchRuleDecision decision =
                    decisions[i];

                PlayerGameplayState player =
                    state.FindPlayer(
                        decision.playerId);

                if (player == null)
                    continue;

                if (player.matchStatus ==
                    decision.status)
                {
                    continue;
                }

                if (player.matchStatus ==
                        PlayerMatchStatus.Defeated &&
                    decision.status ==
                        PlayerMatchStatus.Victorious)
                {
                    continue;
                }

                player.matchStatus =
                    decision.status;

                player.lastOutcomeReason =
                    decision.reason;

                applied++;
            }

            return applied;
        }
    }
}
