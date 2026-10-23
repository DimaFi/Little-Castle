using System;
using System.Collections.Generic;
using LittleCastle.World;

namespace LittleCastle.Gameplay
{
    public enum WorldEventEffectKind : byte
    {
        ResourceDelta = 0,
        ResourcePercentDelta = 1,
        HappinessDelta = 2,
        ResidentEnergyDelta = 3,
        ResidentSleepDebtDelta = 4,
        MainHouseDamagePercent = 5
    }

    [Serializable]
    public sealed class WorldEventEffect
    {
        public WorldEventEffectKind kind;
        public GameplayResourceType resource;
        public float value;
    }

    [Serializable]
    public sealed class WorldEventDefinition
    {
        public string eventId = string.Empty;
        public int requiredDarkMagicLevel;
        public float weight = 1f;
        public List<WorldEventEffect> effects =
            new List<WorldEventEffect>();
    }

    [Serializable]
    public sealed class WorldEventCatalog
    {
        public List<WorldEventDefinition> blessings =
            new List<WorldEventDefinition>();

        public List<WorldEventDefinition> curses =
            new List<WorldEventDefinition>();

        public List<WorldEventDefinition> backlashes =
            new List<WorldEventDefinition>();

        public WorldEventDefinition Find(string eventId)
        {
            WorldEventDefinition found =
                FindInList(
                    blessings,
                    eventId);

            if (found != null)
                return found;

            found =
                FindInList(
                    curses,
                    eventId);

            return found ??
                FindInList(
                    backlashes,
                    eventId);
        }

        public static WorldEventCatalog CreateDefault()
        {
            var catalog =
                new WorldEventCatalog();

            catalog.blessings.Add(
                Event(
                    "blessing_good_harvest",
                    0,
                    1f,
                    Effect(
                        WorldEventEffectKind.ResourceDelta,
                        GameplayResourceType.Wheat,
                        18f)));

            catalog.blessings.Add(
                Event(
                    "blessing_full_pantry",
                    0,
                    0.8f,
                    Effect(
                        WorldEventEffectKind.ResourceDelta,
                        GameplayResourceType.Bread,
                        10f)));

            catalog.blessings.Add(
                Event(
                    "blessing_high_spirits",
                    0,
                    0.7f,
                    Effect(
                        WorldEventEffectKind.HappinessDelta,
                        GameplayResourceType.Bread,
                        8f)));

            catalog.curses.Add(
                Event(
                    "curse_spoiled_provisions",
                    0,
                    1f,
                    Effect(
                        WorldEventEffectKind.ResourcePercentDelta,
                        GameplayResourceType.Bread,
                        -0.15f)));

            catalog.curses.Add(
                Event(
                    "curse_bandit_theft",
                    2,
                    0.8f,
                    Effect(
                        WorldEventEffectKind.ResourcePercentDelta,
                        GameplayResourceType.Bread,
                        -0.22f),
                    Effect(
                        WorldEventEffectKind.ResourcePercentDelta,
                        GameplayResourceType.Coin,
                        -0.12f)));

            catalog.curses.Add(
                Event(
                    "curse_sickness",
                    4,
                    0.65f,
                    Effect(
                        WorldEventEffectKind.ResidentEnergyDelta,
                        GameplayResourceType.Bread,
                        -18f),
                    Effect(
                        WorldEventEffectKind.HappinessDelta,
                        GameplayResourceType.Bread,
                        -7f)));

            catalog.curses.Add(
                Event(
                    "curse_night_raid",
                    7,
                    0.45f,
                    Effect(
                        WorldEventEffectKind.MainHouseDamagePercent,
                        GameplayResourceType.Stone,
                        0.12f),
                    Effect(
                        WorldEventEffectKind.ResourcePercentDelta,
                        GameplayResourceType.Bread,
                        -0.30f)));

            catalog.backlashes.Add(
                Event(
                    "backlash_exhaustion",
                    0,
                    1f,
                    Effect(
                        WorldEventEffectKind.ResidentEnergyDelta,
                        GameplayResourceType.Bread,
                        -8f)));

            catalog.backlashes.Add(
                Event(
                    "backlash_restless_night",
                    3,
                    0.8f,
                    Effect(
                        WorldEventEffectKind.ResidentSleepDebtDelta,
                        GameplayResourceType.Bread,
                        10f),
                    Effect(
                        WorldEventEffectKind.HappinessDelta,
                        GameplayResourceType.Bread,
                        -4f)));

            catalog.backlashes.Add(
                Event(
                    "backlash_food_rot",
                    6,
                    0.6f,
                    Effect(
                        WorldEventEffectKind.ResourcePercentDelta,
                        GameplayResourceType.Bread,
                        -0.18f)));

            return catalog;
        }

        private static WorldEventDefinition FindInList(
            List<WorldEventDefinition> list,
            string eventId)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null &&
                    list[i].eventId == eventId)
                {
                    return list[i];
                }
            }

            return null;
        }

        private static WorldEventDefinition Event(
            string id,
            int requiredLevel,
            float weight,
            params WorldEventEffect[] effects)
        {
            var result =
                new WorldEventDefinition
                {
                    eventId = id,
                    requiredDarkMagicLevel =
                        requiredLevel,
                    weight = weight
                };

            result.effects.AddRange(
                effects);

            return result;
        }

        private static WorldEventEffect Effect(
            WorldEventEffectKind kind,
            GameplayResourceType resource,
            float value)
        {
            return new WorldEventEffect
            {
                kind = kind,
                resource = resource,
                value = value
            };
        }
    }

    [Serializable]
    public struct WorldEventRollResult
    {
        public int sourcePlayerId;
        public string primaryEventId;
        public float primaryPower;
        public bool targetsOtherPlayers;

        public bool backlashTriggered;
        public string backlashEventId;
        public float backlashPower;
    }

    public sealed class NightlyRitualEngine
    {
        private readonly WorldEventCatalog catalog;
        private readonly RitualRules rules;

        public NightlyRitualEngine(
            WorldEventCatalog catalog,
            RitualRules rules)
        {
            this.catalog =
                catalog ??
                throw new ArgumentNullException(nameof(catalog));

            this.rules =
                rules ??
                throw new ArgumentNullException(nameof(rules));
        }

        public bool TryRollBlessing(
            GameplaySessionState state,
            int playerId,
            int dayIndex,
            out WorldEventRollResult result)
        {
            result = default;

            PlayerGameplayState player =
                state?.FindPlayer(playerId);

            if (player == null ||
                player.matchStatus !=
                    PlayerMatchStatus.Active ||
                player.rituals.blessingRollsAvailable <= 0)
            {
                return false;
            }

            SettlementGameplayState capital =
                state.FindCapital(
                    playerId);

            if (capital == null)
                return false;

            int rollIndex =
                player.rituals.rollsPerformed++;

            float raw =
                Roll01(
                    state.worldSeed,
                    playerId,
                    dayIndex,
                    rollIndex,
                    "blessing_primary");

            float happiness =
                Clamp01(
                    capital.happiness /
                    100f);

            float power =
                Clamp01(
                    raw * 0.65f +
                    happiness * 0.35f);

            WorldEventDefinition selected =
                SelectWeighted(
                    catalog.blessings,
                    0,
                    power);

            if (selected == null)
                return false;

            player.rituals.blessingRollsAvailable--;

            result =
                new WorldEventRollResult
                {
                    sourcePlayerId = playerId,
                    primaryEventId =
                        selected.eventId,
                    primaryPower = power,
                    targetsOtherPlayers = false
                };

            return true;
        }

        public bool TryRollCurse(
            GameplaySessionState state,
            int playerId,
            int dayIndex,
            int maximumMagicLevel,
            out WorldEventRollResult result)
        {
            result = default;

            PlayerGameplayState player =
                state?.FindPlayer(playerId);

            if (player == null ||
                player.matchStatus !=
                    PlayerMatchStatus.Active ||
                player.rituals.curseRollsAvailable <= 0)
            {
                return false;
            }

            int level =
                Math.Max(
                    0,
                    player.ruler.darkMagicLevel);

            int rollIndex =
                player.rituals.rollsPerformed++;

            float raw =
                Roll01(
                    state.worldSeed,
                    playerId,
                    dayIndex,
                    rollIndex,
                    "curse_primary");

            float levelFactor =
                maximumMagicLevel <= 0
                    ? 0f
                    : Clamp01(
                        (float)level /
                        maximumMagicLevel);

            float power =
                Clamp01(
                    raw * 0.45f +
                    levelFactor * 0.55f);

            WorldEventDefinition selected =
                SelectWeighted(
                    catalog.curses,
                    level,
                    power);

            if (selected == null)
                return false;

            float backlashChance =
                Lerp(
                    rules.minimumBacklashChance,
                    rules.maximumBacklashChance,
                    power);

            float backlashRoll =
                Roll01(
                    state.worldSeed,
                    playerId,
                    dayIndex,
                    rollIndex,
                    "curse_backlash_check");

            string backlashId =
                string.Empty;

            bool backlash =
                backlashRoll <
                backlashChance;

            float backlashPower = 0f;

            if (backlash)
            {
                backlashPower =
                    Roll01(
                        state.worldSeed,
                        playerId,
                        dayIndex,
                        rollIndex,
                        "curse_backlash_power");

                WorldEventDefinition backlashEvent =
                    SelectWeighted(
                        catalog.backlashes,
                        level,
                        backlashPower);

                if (backlashEvent != null)
                {
                    backlashId =
                        backlashEvent.eventId;
                }
                else
                {
                    backlash = false;
                }
            }

            player.rituals.curseRollsAvailable--;

            result =
                new WorldEventRollResult
                {
                    sourcePlayerId = playerId,
                    primaryEventId =
                        selected.eventId,
                    primaryPower = power,
                    targetsOtherPlayers = true,
                    backlashTriggered = backlash,
                    backlashEventId =
                        backlashId,
                    backlashPower =
                        backlashPower
                };

            return true;
        }

        private static WorldEventDefinition SelectWeighted(
            List<WorldEventDefinition> definitions,
            int magicLevel,
            float roll)
        {
            float totalWeight = 0f;

            for (int i = 0; i < definitions.Count; i++)
            {
                WorldEventDefinition definition =
                    definitions[i];

                if (definition == null ||
                    definition.requiredDarkMagicLevel >
                    magicLevel)
                {
                    continue;
                }

                totalWeight +=
                    Math.Max(
                        0f,
                        definition.weight);
            }

            if (totalWeight <= 0f)
                return null;

            float target =
                Clamp01(roll) *
                totalWeight;

            float cursor = 0f;
            WorldEventDefinition last =
                null;

            for (int i = 0; i < definitions.Count; i++)
            {
                WorldEventDefinition definition =
                    definitions[i];

                if (definition == null ||
                    definition.requiredDarkMagicLevel >
                    magicLevel)
                {
                    continue;
                }

                float weight =
                    Math.Max(
                        0f,
                        definition.weight);

                if (weight <= 0f)
                    continue;

                last = definition;
                cursor += weight;

                if (target <= cursor)
                    return definition;
            }

            return last;
        }

        private static float Roll01(
            int seed,
            int playerId,
            int dayIndex,
            int rollIndex,
            string key)
        {
            int salt =
                DeterministicHash.String32(
                    key);

            return
                DeterministicHash.Hash01(
                    seed,
                    playerId,
                    dayIndex,
                    salt ^ rollIndex);
        }

        private static float Lerp(
            float a,
            float b,
            float t)
        {
            t = Clamp01(t);
            return a + (b - a) * t;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;

            if (value > 1f)
                return 1f;

            return value;
        }
    }

    public static class WorldEventApplier
    {
        public static void ApplyRoll(
            GameplaySessionState state,
            WorldEventCatalog catalog,
            WorldEventRollResult roll)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            WorldEventDefinition primary =
                catalog.Find(
                    roll.primaryEventId);

            if (primary != null)
            {
                if (roll.targetsOtherPlayers)
                {
                    for (int i = 0;
                         i < state.players.Count;
                         i++)
                    {
                        PlayerGameplayState target =
                            state.players[i];

                        if (target.playerId ==
                                roll.sourcePlayerId ||
                            target.matchStatus !=
                                PlayerMatchStatus.Active)
                        {
                            continue;
                        }

                        ApplyToPlayerCapital(
                            state,
                            target.playerId,
                            primary,
                            roll.primaryPower);
                    }
                }
                else
                {
                    ApplyToPlayerCapital(
                        state,
                        roll.sourcePlayerId,
                        primary,
                        roll.primaryPower);
                }
            }

            if (roll.backlashTriggered)
            {
                WorldEventDefinition backlash =
                    catalog.Find(
                        roll.backlashEventId);

                if (backlash != null)
                {
                    ApplyToPlayerCapital(
                        state,
                        roll.sourcePlayerId,
                        backlash,
                        roll.backlashPower);
                }
            }
        }

        private static void ApplyToPlayerCapital(
            GameplaySessionState state,
            int playerId,
            WorldEventDefinition definition,
            float power)
        {
            SettlementGameplayState settlement =
                state.FindCapital(playerId);

            if (settlement == null)
                return;

            float scale =
                0.6f +
                Clamp01(power) *
                0.8f;

            for (int i = 0;
                 i < definition.effects.Count;
                 i++)
            {
                ApplyEffect(
                    settlement,
                    definition.effects[i],
                    scale);
            }
        }

        private static void ApplyEffect(
            SettlementGameplayState settlement,
            WorldEventEffect effect,
            float scale)
        {
            if (effect == null)
                return;

            switch (effect.kind)
            {
                case WorldEventEffectKind.ResourceDelta:
                    settlement.inventory.Add(
                        effect.resource,
                        RoundAwayFromZero(
                            effect.value *
                            scale));
                    break;

                case WorldEventEffectKind.ResourcePercentDelta:
                {
                    int current =
                        settlement.inventory.Get(
                            effect.resource);

                    int delta =
                        RoundAwayFromZero(
                            current *
                            effect.value *
                            scale);

                    settlement.inventory.Add(
                        effect.resource,
                        delta);
                    break;
                }

                case WorldEventEffectKind.HappinessDelta:
                    settlement.happiness =
                        Clamp(
                            settlement.happiness +
                            effect.value *
                            scale,
                            0f,
                            100f);
                    break;

                case WorldEventEffectKind.ResidentEnergyDelta:
                    for (int i = 0;
                         i < settlement.residents.Count;
                         i++)
                    {
                        ResidentState resident =
                            settlement.residents[i];

                        resident.energy =
                            Clamp(
                                resident.energy +
                                effect.value *
                                scale,
                                0f,
                                100f);
                    }
                    break;

                case WorldEventEffectKind.ResidentSleepDebtDelta:
                    for (int i = 0;
                         i < settlement.residents.Count;
                         i++)
                    {
                        ResidentState resident =
                            settlement.residents[i];

                        resident.sleepDebt =
                            Clamp(
                                resident.sleepDebt +
                                effect.value *
                                scale,
                                0f,
                                100f);
                    }
                    break;

                case WorldEventEffectKind.MainHouseDamagePercent:
                {
                    BuildingRuntimeState mainHouse =
                        settlement.FindBuilding(
                            settlement.mainHouseBuildingId);

                    if (mainHouse == null ||
                        mainHouse.IsDestroyed)
                    {
                        break;
                    }

                    mainHouse.hitPoints =
                        Math.Max(
                            0f,
                            mainHouse.hitPoints -
                            mainHouse.maxHitPoints *
                            Math.Max(
                                0f,
                                effect.value) *
                            scale);
                    break;
                }
            }
        }

        private static int RoundAwayFromZero(float value)
        {
            return value >= 0f
                ? (int)Math.Ceiling(value)
                : (int)Math.Floor(value);
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
