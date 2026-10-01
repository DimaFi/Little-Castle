# Fair Procedural Player Starts

## Product rule

Little Castle should create **random, uneven and sometimes funny** starts.

A player may begin with:

- little nearby forest;
- weak stone access;
- iron noticeably farther away;
- awkward terrain;
- a richer or poorer local economy than another player.

That procedural inequality is part of the game.

Fairness exists to prevent **broken topology**, not to make every opening equal.

Examples of starts that should normally be rejected:

- there is no practical buildable starting area;
- the player is effectively trapped by cliffs/water;
- the only way out is one bridge/choke;
- there are too few independent exits for the selected map rules;
- the selected start formation cannot place all players at reasonable separation.

Examples that are normally allowed:

- one player has much less nearby wood;
- one player must travel farther for stone;
- one player has excellent iron but mediocre forest;
- one player gets an economically awkward start that creates emergent gameplay.

## Host-facing controls

Safe match-creation controls live in `WorldSessionStartOptions`.

Current controls:

### Placement mode

`RandomScattered`

Starts are selected freely across the playable map.

`MapPerimeter`

Starts prefer the outer part of the map and generally expand inward.

`PolygonRing`

Starts prefer evenly spaced positions around a ring.

With 3 players this naturally produces a triangle-like arrangement.

`RadialStar`

Alternates outer and inner radial targets, creating a star-like layout where
some positions are intentionally more central/pressured.

### Fairness mode

`WildRandom`

Maximum procedural chaos. Resource/forest poverty is not a hard rejection.

`Light`

Default intended mode. Scarcity remains common, but highly pressured interior
positions prefer somewhat better opportunities.

`Balanced`

Stronger filtering while retaining asymmetric worlds.

`Competitive`

Strongest host-selectable filtering.

### Additional host controls

- layout freedom;
- positional-pressure resource compensation;
- start-separation multiplier;
- minimum local exit routes override;
- whether rivers require bridges to count as exits;
- ring radius;
- star inner/outer radius;
- deterministic random rotation of formations.

These are intended to be exposed by a future create-match UI.

Low-level safety/tuning remains in `WorldStartFairnessSettings`.

## Pressure compensation

A start near the middle of other players is strategically worse than an edge
start because threats can approach from more directions.

The planner therefore computes/uses positional pressure.

Higher-pressure layout targets are processed earlier and may place greater
weight on nearby resource quality.

This does **not** inject resources.

Instead:

```text
generated world
    ↓
central/pressured target
    ↓
among viable nearby candidates,
prefer a somewhat richer candidate
```

How strongly this happens is controlled by:

`pressureResourceCompensation`

Setting it to 0 disables this compensation.

## Local exits

Player starts are evaluated for local escape/access directions.

The probe samples many radial directions around the start.

A direction can be blocked by:

- terrain exceeding the configured slope;
- leaving playable bounds;
- a river crossing without a suitable generated bridge nearby, when that host
  option is enabled.

Open angular sectors are converted into independent exit routes.

Automatic target:

- cramped/small area per player: 2 exits may be allowed;
- normal map: 3 exits;
- roomy/large area per player: 4 exits.

The host can override this.

This is deliberately more important than resource equality.

A resource-poor player can create interesting gameplay.

A player whose only exit is one bridge can be strategically doomed before the
match begins.

## Resources are mostly scores, not hard gates

`StartResourceRequirement` supports:

- resource type;
- search radius;
- optional hard minimum effective capacity;
- target effective capacity;
- scoring weight.

Default hard minimums are intentionally 0.

Effective capacity is approximately:

```text
deposit.capacity × deposit.richness
```

The target affects quality scoring and compensation.

It does not mean every player must receive that amount.

Fairness modes can scale hard gates if designers later configure non-zero
minimums.

Only resources inside playable bounds count.

## Forest scarcity

Forest density remains part of start quality, but in `WildRandom` it is not a
hard gate.

`Light` uses only a small fraction of the developer minimum.

Therefore very poor forest starts can exist.

The player still lives in the same finite world and can travel, trade, fight or
expand toward better areas.

## Formation adaptation

Formation modes define ideal targets, not exact spawn coordinates.

Generated terrain/resources can move a start away from its geometric target.

`layoutFreedom` controls how much adaptation is allowed.

This preserves recognizable layouts without spawning players on unsuitable
terrain just to draw a perfect shape.

## Random mode

`RandomScattered` has no predefined geometric targets.

The planner still:

- enforces required separation;
- checks local exits;
- checks start buildability;
- can slightly prefer better resource quality for central/pressured positions.

The map itself remains deterministic from the session seed.

## Determinism

The same:

- seed;
- finite map preset;
- player count;
- start options;
- generation settings;

must produce the same selected starts.

Do not use `UnityEngine.Random` for authoritative start selection.

## Seed rejection

A seed does not need equal resources to be accepted.

Seed rejection should primarily represent:

- insufficient number of physically viable starts;
- impossible required separation;
- insufficient exits/chokepoint safety;
- stricter host fairness modes exceeding their allowed quality spread.

`WildRandom` effectively disables score-spread rejection.

`Light` allows a large score spread.

This preserves strange/random worlds.

## Current data

Files:

- `WorldStartPlacementMode.cs`
- `WorldStartFairnessMode.cs`
- `WorldSessionStartOptions.cs`
- `WorldStartLayoutUtility.cs`
- `WorldStartAccessEvaluator.cs`
- `WorldStartFairnessSettings.cs`
- `WorldPlayerStartData.cs`
- `WorldStartFairnessReport.cs`
- `WorldStartFairnessPlanner.cs`

## Current metrics

A start can record:

- terrain/buildability quality;
- average forest density;
- resource metrics;
- number of independent exits;
- positional pressure;
- formation/layout affinity;
- overall local quality.

## Future strategic fairness

The current exit probe is intentionally local.

Future global strategic evaluation should measure:

- travel-time access to neutral villages;
- distance/travel cost to roads;
- number of bridge/river crossing alternatives;
- whether one player controls a unique choke;
- central high-value resource access;
- expected time to first hostile contact;
- team-vs-team regional pressure.

Prefer navigation/travel cost rather than straight-line distance once navigation
data exists.

## 2–16 players

The start system must not assume 2 or 4 players.

Current design target is up to 16 players.

Formation generation, separation and exit requirements are derived from player
count/map scale rather than hard-coded spawn coordinates.
