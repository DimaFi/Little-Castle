# Finite Session Maps

## Product rule

Little Castle does **not** use an infinite world.

Every multiplayer match creates one finite procedural map.

The host chooses:

- world seed;
- player count;
- one allowed map-size preset.

Map size is constrained by player count. A host must not be able to select a
map that is too small for the number of players.

Example rule:

```text
12 players
    ↓
small preset is rejected
    ↓
medium/large presets may remain available
```

Exact preset sizes are balance data and are intentionally not hard-coded into
architecture documentation yet.

## Runtime data

The finite-map foundation uses:

- `WorldMapSizePreset`
- `WorldMapRules`
- `WorldSessionMap`
- `WorldChunkBounds`
- `WorldSessionMapFactory`

`WorldMapRules` is a design-time ScriptableObject catalog.

`WorldSessionMap` is runtime session data created from the host's choice.

## Bounds model

Every session map has two important chunk rectangles.

### Playable bounds

The real gameplay area.

Only this area may contain authoritative gameplay such as:

- player movement;
- unit movement;
- construction;
- neutral settlements;
- resource extraction;
- combat;
- controllable villages;
- ordinary generated objects.

Camera and unit controllers should clamp/reject movement outside this area.

### Visual bounds

```text
visual bounds
┌─────────────────────────────┐
│ visual terrain padding      │
│   ┌─────────────────────┐   │
│   │ PLAYABLE MAP        │   │
│   │                     │   │
│   └─────────────────────┘   │
│ visual terrain padding      │
└─────────────────────────────┘
```

The visual padding exists only so the player does not see a hard terrain edge.

Current streamer behavior for visual-only chunks:

- terrain is generated only through `TerrainAnalysis`;
- no ordinary generated spawns are presented;
- no gameplay resource objects are presented;
- no MeshCollider is created;
- the area is not considered playable.

Beyond visual bounds, the streamer generates nothing.

A future border/fog presentation should hide the final visual edge completely.

## Macro world

A finite session solves the old moving macro-plan problem.

At match creation:

```text
Host chooses session
        ↓
Playable bounds become known
        ↓
One MacroWorldPlan is generated for the whole playable map
        ↓
settlements / rivers / roads / bridges become stable for the match
        ↓
chunk streaming only presents pieces of that already-defined world
```

Do not implement deterministic macro tiles for the current design.

Do not rebuild the macro plan around the camera/player while a match is active.

## Session pacing

Current design target:

- fast session: about 30 minutes;
- normal session: about 40–50 minutes;
- intended upper bound: about 2 hours.

The map should be large enough for:

- exploration;
- resource collection;
- building a settlement/base;
- military development;
- neutral-settlement interaction;
- player conflict;
- late-match battles;

while still allowing a normal match to substantially explore and contest the
map within the target session duration.

## How to choose real map dimensions later

Do not choose final kilometer dimensions by intuition alone.

Balance presets using measured gameplay data:

- average unit movement speed;
- fastest/slowest unit speed;
- camera traversal rules;
- expected time to first enemy contact;
- distance between starting areas;
- resource density;
- settlement density;
- average road travel speed;
- desired exploration time;
- player count.

A useful balancing relationship is:

```text
map scale
≈
movement speed
×
desired travel/exploration time
```

but final presets should be validated through real matches.

## Host validation

`WorldMapSizePreset` currently contains:

- stable `presetId`;
- display name;
- minimum players;
- optional maximum players;
- playable width in chunks;
- playable height in chunks;
- visual-padding chunks.

`WorldMapRules.TryResolvePreset(...)` rejects invalid host choices.

The UI should eventually show only presets allowed for the selected player
count, but server/session creation must still validate the choice authoritatively.

## Multiplayer rule

Map selection is session-authoritative.

All clients should receive the same:

- world seed;
- player count;
- map-size preset ID;
- generation version.

Clients must not independently choose map size.
