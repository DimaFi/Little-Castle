# Gameplay Foundation

Little Castle gameplay is server-authoritative, data-first and driven by the
existing authoritative world clock.

## Match identity and defeat

Each player owns:

- one ruler;
- one capital settlement;
- one main house in that capital.

The ruler is not the loss condition. Ruler defeat starts a timed respawn and the
ruler returns with partial health.

The default defeat condition is destruction of the player's main house.

Victory/defeat is intentionally implemented through the IMatchRule contract.
The default rules are:

- MainHouseDestructionDefeatRule;
- LastPlayerStandingVictoryRule.

New game modes should add rules instead of hard-coding new win/loss checks into
buildings or UI.

## Gameplay time

GameplaySimulationAuthority.Advance receives gameHours from WorldTimeSystem.

Do not use Time.deltaTime directly for:

- resident needs;
- construction progress;
- production;
- food consumption;
- training;
- village market restocking;
- ruler respawn.

GameplayTimeDriver is only a Unity adapter. In multiplayer the server/host owns
the GameplaySimulationAuthority.

## Residents

Residents are authoritative data, not decoration.

Current state:

- energy;
- sleepDebt;
- mood;
- base work efficiency;
- current activity.

Repeated night work increases sleepDebt. Penalties become meaningful only after
the chronic threshold, so a single difficult night is survivable.

Future AI priority logic can choose activities such as:

1. emergency/fire;
2. defence;
3. explicit player order;
4. sleep;
5. assigned work;
6. idle/home needs.

That decision layer must not replace the needs state.

## Economy

The first economy is deliberately small:

wheat -> mill -> bread

Farms produce wheat. Mills consume wheat and produce bread. Residents consume
bread over game time. Missing food reduces settlement happiness.

All fractional progress is accumulated in settlement simulation state so small
server ticks do not lose production.

## Construction

Player construction uses the data-driven `BuildingDefinition` catalog and
`SettlementBuildingService`. Starting a building spends resources, allocates a
stable runtime ID and stores its logical world transform before any prefab is
required.

Buildings are authoritative BuildingRuntimeState records with:

- stable runtime ID;
- owner;
- archetypeId;
- role;
- level;
- HP;
- required/completed construction work;
- assigned builder count.

Presentation prefabs are not authoritative.

Existing curved walls remain under the compact WallRuntimeState system; do not
expand a wall into independently saved segment transforms.

Walls have compact HP on the path state. Damage and repair never require storing
the derived section transforms. `WallRepairSystem` is intentionally called by
post-combat logic so residents do not automatically repair a wall during an
active siege.

### Main-house progression

The default upgrade data currently provides:

`main_house_level_1 -> main_house_level_2 -> main_house_keep -> main_house_small_castle`

Costs, work time and HP are balance data and may change without changing the
upgrade contract.

## Barracks and soldiers

Barracks own training queues. Enqueueing training spends resources immediately.
Training progresses in game hours and yields unit stacks when complete.

Unit definitions are data entries in GameplayRules so later troop types do not
require rewriting the queue.

## Neutral villages and trade

Neutral settlements use the same SettlementGameplayState with isNeutral=true.

`GameplaySessionBootstrap.ImportNeutralSettlements` derives those runtime
settlements from deterministic `WorldFeatureKind.NeutralSettlement` entries in
the macro plan. The macro feature stable ID remains the settlement ID, so the
same seed produces the same village identity and deterministic starting market.

A neutral market exposes offers with:

- resource;
- coin price;
- current/max stock;
- restock per game day.

Trading changes authoritative inventories only. UI, merchants and animations are
presentation.

## Nightly blessing / curse dice

At dawn each active player receives configurable blessing and curse roll charges.

Blessing power combines a deterministic roll with capital happiness.

Curse power combines a deterministic roll with the ruler's dark-magic level.
Higher dark-magic progression unlocks stronger curse definitions.

A curse also performs a separate deterministic backlash roll. Backlash chance
rises with curse power, so stronger dark magic can hurt the caster's own
settlement.

The roll result contains two independent tracks:

- primary event against every other active player;
- optional backlash event against the caster.

World-event effects are data definitions. Current effect primitives include
resources, happiness, resident energy/sleep debt and main-house damage.

Ruler meditation grants dark-magic XP. Balance values are provisional and belong
in GameplayRules.

## Fog of war

The existing WorldInformationMode already matches the lobby choice:

- FullMapLive: clients may receive current state for the whole finite map;
- FogOfWarLastKnown: explored territory keeps its last-known state and live
  dynamic state is available only where currently visible.

Rendering distance remains separate from information permission.

## Procedural world constraints

Small hills/highlands remain traversable.

TerrainClass.MountainBarrier is a hard traversal class. Terrain-aware road
planning does not route through it. Future unit/nav pathfinding must use the same
logical restriction rather than relying only on colliders.

Bridge crossings use one production bridge archetype by default:

- the river width is normalized around a generated road/river crossing;
- the standard crossing width is 8 world units;
- the bridge span is 12 world units;
- nearby river profile points blend toward the crossing width.

This keeps the authored bridge asset fixed while preserving varied rivers away
from crossings.

## Simulation LOD

Gameplay state must remain valid when presentation is unloaded.

Future simulation scheduling uses the existing tiers:

- Active: full local AI/combat;
- Warm: reduced-frequency ticks;
- Sleeping: mathematical catch-up only.

Production, construction and training are already expressed as game-time
progress, so Sleeping simulation can advance them without spawning residents.

## Save/network contract

WorldSaveState stores GameplaySessionState, the authoritative WorldTimeState
and compact wall paths alongside runtime world deltas.

The save still does not contain the untouched generated map. Base world comes
from seed; save/network payloads contain mutable runtime state.
