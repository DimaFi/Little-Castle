# Fair Procedural Player Starts

## Goal

Little Castle maps should remain random and worth exploring, but the random seed
must not decide the match before players begin.

Fairness does **not** mean:

- mirrored terrain;
- identical forests;
- one guaranteed resource pile placed next to every player;
- symmetrical roads/villages;
- editing the world after players receive their positions.

The intended model is:

```text
random deterministic world
        ↓
generate macro world
        ↓
evaluate many possible player starts
        ↓
select well-separated viable starts
        ↓
compare start quality
        ↓
accept seed OR reject/reroll seed
```

The generator remains random. The session only starts when that random world
passes a fairness gate.

## Current implementation

Files:

- `WorldStartFairnessSettings.cs`
- `WorldPlayerStartData.cs`
- `WorldStartFairnessReport.cs`
- `WorldStartFairnessPlanner.cs`

`WorldDefinition` can reference one `WorldStartFairnessSettings` asset.

## Evaluation order

The fairness planner is created after the match's `MacroWorldPlan` exists.

It uses the normal `WorldGenerationPipeline` through the `Resources` phase.

Therefore candidate evaluation sees the same authoritative data that gameplay
will later use:

- terrain height;
- slope;
- macro placement masks from roads/rivers/settlements;
- biome/environment data;
- forest-density field;
- generated resource deposits.

It does not require prefab presentation.

## Candidate generation

Start candidates are generated from a deterministic global grid with seed-based
jitter.

This provides controlled randomness without `UnityEngine.Random`.

Candidates are kept away from the playable map edge and then tested for local
viability.

## Buildable-area checks

A candidate currently requires:

- acceptable center slope;
- acceptable slopes around a configurable build radius;
- acceptable height variation around the build radius;
- no conflicting macro placement blocks in the immediate start/build area.

The intent is that the player can actually begin building instead of spawning
on a cliff, river corridor or occupied macro feature.

## Forest access

The planner samples the existing `ForestDensity` field around the start.

It does not create trees.

Only forest samples inside playable bounds count.

A start below the configured minimum average forest density is rejected.

This is especially important because wood is expected to be a high-consumption
strategic resource for buildings and military development.

## Strategic resource access

`StartResourceRequirement` defines per-resource policy.

Example configuration:

```text
Stone:
  search radius
  minimum effective capacity
  target effective capacity
  weight

IronOre:
  search radius
  minimum effective capacity
  target effective capacity
  weight
```

Effective capacity is currently approximated as:

```text
deposit.capacity × deposit.richness
```

Only deposits inside playable bounds count.

The minimum is a hard viability gate.

The target is used for normalized quality scoring.

This allows a start with 520 effective stone and one with 650 effective stone
to both be valid without requiring exact equality.

## Start separation

Starts are not selected independently.

After viable candidates are found, the planner selects a set that maximizes a
combination of:

- local start quality;
- distance from already selected starts.

Minimum start distance scales with:

```text
sqrt(playable map area / player count)
× separation multiplier
```

so the same rule can work for different finite map presets.

## Player assignment

Selected start locations receive deterministic shuffled player indices.

Player 1 is therefore not permanently assigned to the first/best geometric
candidate.

The future multiplayer session layer can use these deterministic assignments or
apply a separate lobby/team allocation policy if required.

## Seed acceptance

`WorldStartFairnessReport` records:

- requested player count;
- generated candidate count;
- viable candidate count;
- selected starts;
- required minimum separation;
- minimum selected quality;
- maximum selected quality;
- score spread;
- rejection reason.

A seed is rejected when:

- too few viable candidates exist;
- enough candidates exist but they cannot satisfy required separation;
- selected quality spread exceeds the configured tolerance.

Recommended session creation flow:

```text
Host presses Create Match
        ↓
candidate seed
        ↓
finite session map bounds
        ↓
MacroWorldPlan
        ↓
WorldStartFairnessPlanner
        ↓
accepted?
   yes ─────→ create lobby/match
   no
   ↓
try another seed
```

Do not patch a rejected seed by secretly spawning emergency ore/wood beside a
specific player unless game design explicitly introduces such a fallback.

## Why exact equality is undesirable

Perfect equality would make procedural maps predictable.

The target is a bounded competitive difference.

Players should still have meaningful strategic variation:

- one start may have denser nearby forest;
- another may have slightly better stone;
- another may have easier open terrain;

but all starts must cross the same minimum viability floor and stay within the
accepted total quality spread.

## Current limitations

The first fairness layer focuses on **starting viability**.

It does not yet measure all strategic map advantages.

Future fairness metrics can include:

- travel-time distance to neutral settlements;
- travel-time distance to major roads;
- river crossing access;
- number/value of nearby neutral settlements;
- chokepoint advantage;
- defensibility;
- distance to map center;
- distance to contested high-value resource regions;
- team-vs-team regional symmetry;
- expected first-contact time.

Prefer path/travel cost over straight-line distance once navigation data exists.

## Testing

`WorldStartFairnessTests.cs` verifies that, under relaxed test thresholds:

- the same seed returns the same starts;
- requested player count is satisfied;
- selected starts respect computed minimum separation.

Production balance thresholds must later be tuned from real matches rather than
guessed from editor screenshots.

## 2–16 player direction

The architecture is intended to support session player counts up to the current
design target of 16 players.

Do not hard-code assumptions that only 2 or 4 starts exist.

Map-size presets and fairness settings should be tuned together:

- larger player counts require larger allowed maps;
- larger maps require enough candidate coverage;
- resource search radii should reflect actual unit travel speed and match pace.

The 30–120 minute match-duration target should remain part of this balancing
work.
