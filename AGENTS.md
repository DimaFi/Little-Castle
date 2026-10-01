# AGENTS.md — Little Castle

This file defines rules for AI coding agents working in this repository.

## Project direction

Little Castle is intended to become a long-lived Unity project with:
- procedural world generation;
- streamed/chunked world data;
- roads, rivers, terrain, vegetation and settlements;
- population, economy, combat and events;
- save/load;
- possible multiplayer later.

Do not optimize for a one-off prototype at the cost of architecture.

## Mandatory engineering rules

1. **Determinism first**
   - Generation must be reproducible from a world seed.
   - Never rely on UnityEngine.Random for authoritative generation.
   - Any random-looking choice must derive from stable inputs such as world seed, chunk coordinate and feature key.

2. **World data is not scene presentation**
   - Generation stages write data.
   - Rendering reads data.
   - Do not spawn visual prefabs directly from core generation stages.

3. **Chunk-safe generation**
   - Neighboring chunks must agree on shared borders.
   - Sampling must use world-space coordinates, not isolated per-chunk local noise.

4. **Stage-based pipeline**
   - New generation features should be added as stages or clearly separated planners.
   - Keep responsibilities narrow.
   - Do not grow one giant WorldGenerator class.

5. **Macro features before local projection**
   - Roads, rivers, settlements and other cross-chunk features should eventually be planned at world/macro scale and then projected into chunks.
   - Do not generate a road independently inside each chunk.

6. **Save-game compatibility**
   - Avoid storing transient Unity object references in authoritative world data.
   - Prefer serializable identifiers and plain data.

7. **Future multiplayer**
   - Treat generation output as deterministic simulation data.
   - Avoid hidden local state that would differ between machines.

8. **No premature package lock-in**
   - Core systems should not depend on optional third-party packages unless explicitly approved.

9. **Small verified changes**
   - Preserve existing architecture unless a change is documented.
   - If an architecture rule must change, create or update an ADR under docs/decisions/.

## Folder intentions

- `Assets/_Game/Scripts/World/Core`: stable world primitives and data.
- `Assets/_Game/Scripts/World/Generation`: generation orchestration.
- `Assets/_Game/Scripts/World/Generation/Stages`: data-producing generation stages.
- `Assets/_Game/Scripts/World/Noise`: deterministic sampling utilities.
- `Assets/_Game/Scripts/World/Rendering`: Unity visual representation only.
- `docs/architecture`: current architecture.
- `docs/decisions`: important irreversible or cross-cutting decisions.

## Before changing world generation

Read:
- `docs/architecture/world-generation.md`
- relevant ADRs in `docs/decisions/`

## Definition of done for generation work

A generation change should:
- be deterministic;
- work for negative chunk coordinates;
- produce matching borders between adjacent chunks;
- not require scene objects to produce authoritative data;
- document any new data contract;
- preserve compatibility with future chunk streaming.
