# ADR-0001: Deterministic chunk generation pipeline

**Status:** Accepted

## Context

Little Castle is expected to grow into a large procedural world with roads, rivers, vegetation, settlements, simulation, save games and possibly multiplayer.

A monolithic scene-based generator would become difficult to test, stream, save and synchronize.

## Decision

World generation will use:

1. a stable integer world seed;
2. chunk coordinates;
3. world-space sampling;
4. an ordered generation pipeline;
5. plain chunk data as authoritative output;
6. a separate rendering layer that converts data into Unity objects.

Generation stages are ScriptableObjects for authoring convenience, but their output must remain plain data.

## Consequences

### Benefits
- repeatable generation;
- easier debugging;
- chunk streaming;
- future headless testing;
- save/load friendliness;
- easier multiplayer synchronization.

### Costs
- more structure than a one-file prototype;
- large cross-chunk systems need dedicated macro planners;
- rendering cannot be mixed casually into generation code.

## Important follow-up

Roads, rivers and settlement placement should not be implemented as isolated per-chunk random spawns. They require world/macro planning and deterministic projection into chunk data.
