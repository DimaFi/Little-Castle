# Codex Workflow

This document tells coding agents how to work safely in Little Castle.

## Before coding

1. Read `AGENTS.md`.
2. Read `docs/PROJECT_STRUCTURE.md`.
3. Read architecture documentation for the subsystem being changed.
4. Read relevant ADRs.
5. Inspect existing code before introducing a parallel implementation.

## Change strategy

Prefer small coherent changes.

For substantial features:
1. identify the owning subsystem;
2. define or update its data contract;
3. implement pure/data logic first;
4. add Unity presentation/editor integration afterward;
5. update documentation;
6. test deterministic behavior where applicable.

## Never do this

- Create a second Unity project inside the repository.
- Move folders only for aesthetics.
- Rename public serialized fields/classes without considering Unity serialization.
- Replace deterministic world generation with `UnityEngine.Random`.
- Put prefab spawning directly into authoritative generation stages.
- Generate roads independently per chunk.
- Add a new framework because a simple local abstraction already exists.
- Delete documentation because code appears self-explanatory.

## Unity serialization safety

Unity serializes fields and assets by names and GUIDs.

When the real Unity project begins:
- keep `*.meta` files committed;
- avoid unnecessary asset moves;
- use Unity Editor for asset moves when possible;
- preserve serialized field names or use `FormerlySerializedAs` when renaming;
- avoid changing ScriptableObject types casually.

## Documentation rule

Any subsystem that becomes non-trivial should have:

```text
docs/architecture/<system>.md
```

If a cross-cutting architectural choice is made, add:

```text
docs/decisions/ADR-XXXX-<decision>.md
```

## Generated world features

When adding a world feature, determine whether it is:

- **local/chunk feature** — can be sampled from absolute world coordinates;
- **macro feature** — spans chunks and needs a world-scale planner;
- **runtime modification** — player/NPC changes layered over generated base data.

Examples:

| Feature | Type |
|---|---|
| Terrain height | Local |
| Basic biome sampling | Local / regional |
| Grass scatter | Local |
| River network | Macro |
| Road graph | Macro |
| Settlement locations | Macro |
| Player-built road | Runtime modification |
| Chopped tree | Runtime modification |

Do not force macro features into local generation stages.
