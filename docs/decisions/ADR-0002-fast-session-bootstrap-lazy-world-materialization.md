# ADR-0002 — Fast Session Bootstrap and Lazy World Materialization

## Status

Accepted.

## Context

Little Castle uses finite procedural maps that may contain a large number of
visual assets, generated trees, rocks, resource deposits, buildings and other
objects.

The installed game may eventually contain gigabytes of models and textures.

That must **not** mean that creating or joining a multiplayer session requires:

- transmitting those assets from the host;
- loading every game asset into memory;
- generating every chunk;
- instantiating every tree/rock/building;
- building meshes for the entire map;
- constructing the entire Unity scene before the match can begin.

Players already have game assets installed locally.

The network/session contract should synchronize small deterministic inputs and
authoritative runtime state rather than a serialized copy of the untouched
procedural world.

## Decision

Session creation is split into two layers.

### Layer A — session bootstrap

Must remain lightweight.

Typical inputs:

```text
world seed
generation version
map preset
player count
host world/start options
session rules
```

Bootstrap may calculate world-scale **data** required for correctness, such as:

- finite playable/visual bounds;
- MacroWorldPlan;
- rivers;
- roads;
- bridges;
- neutral settlement anchors;
- player start positions/fairness;
- other compact deterministic strategic data.

Bootstrap must not instantiate the full map.

### Layer B — lazy materialization

Detailed chunk contents are generated/presented when required.

Examples:

- terrain meshes near active player/camera focuses;
- local tree/rock scatter;
- local resource visuals;
- local building/ruin presentation;
- colliders;
- detailed vegetation.

Distant parts of the finite map may exist only implicitly through deterministic
generation rules until a player approaches them.

## Required startup flow

```text
Host creates session
        ↓
Validate player count + map preset
        ↓
Resolve seed + generation version + host options
        ↓
Create finite WorldSessionMap
        ↓
Build compact MacroWorldPlan for playable bounds
        ↓
Resolve/validate player starts
        ↓
Prewarm only required starting areas
        ↓
MATCH READY
        ↓
WorldStreamer lazily materializes additional chunks as players explore
```

## Asset rule

Model/texture size in the installed game is not session-state size.

The host does not send FBX/mesh/texture data to clients when a normal match is
created.

Generated records refer to stable logical IDs such as:

```text
tree_main_01
rock_ground_01
bridge_wood_small
```

Each client resolves those IDs to locally installed presentation assets.

## Prohibited implementation patterns

Do not:

- pre-generate every detailed chunk before allowing the match to start;
- instantiate all generated objects on the complete finite map during bootstrap;
- load all texture/model assets solely because they may appear somewhere on the map;
- serialize/transmit the complete untouched generated map between players;
- make session creation time scale directly with the total number of possible
  tree/rock/decorative GameObjects on the map.

## Prewarming

Prewarming is allowed for areas required immediately when the match begins.

For example, with 16 players, bootstrap may prepare chunks around 16 spawn
locations.

Prewarming policy must remain configurable.

It should not silently become "load the complete map".

## Determinism and multiplayer

The same session inputs must resolve to the same generated base world on all
participants.

Expected synchronized session data should eventually include at least:

- seed;
- generation version;
- finite map preset/size;
- player count;
- start-generation options;
- authoritative selected start assignment;
- other non-derivable authoritative runtime state.

Runtime modifications remain authoritative deltas over the deterministic base
world.

## Verification requirements

Every significant world-generation/streaming change must preserve the fast
bootstrap contract.

Codex/Unity verification should measure or report:

1. session bootstrap wall-clock time;
2. MacroWorldPlan generation time;
3. fairness/start-selection time when enabled;
4. number of detailed chunks generated before MATCH READY;
5. number of chunk GameObjects active at MATCH READY;
6. generated object count at MATCH READY;
7. whether distant chunks remain unmaterialized;
8. time to materialize a newly explored chunk;
9. memory growth while moving through several regions;
10. same-seed deterministic regeneration after cache eviction.

Test at representative player counts, especially:

```text
2
8
16
```

and at more than one map size.

Do not hide poor startup performance by pre-generating the world before timing
begins.

## Performance targets

Exact numerical budgets are intentionally TBD until real game-ready models,
terrain settings and representative hardware exist.

Until then:

- collect timings;
- prevent architectural regressions;
- keep work proportional to immediately required areas;
- avoid full-map detailed generation.

Once representative content exists, replace TBD with explicit release budgets.

## Consequences

Positive:

- very large finite maps can start quickly;
- total installed asset-library size is decoupled from network session creation;
- memory use grows with active streamed content rather than complete map size;
- deterministic multiplayer synchronization remains compact.

Costs:

- streamer/cache correctness is critical;
- runtime deltas must survive chunk unload/regeneration;
- some first-visit chunk generation cost exists;
- later optimization may require async generation, pooling and asset streaming.

## Related documents

- `docs/architecture/world-streaming.md`
- `docs/architecture/finite-session-maps.md`
- `docs/architecture/world-generation-roadmap.md`
- `docs/workflows/CODEX_UNITY_HANDOFF.md`
