# Graphics 2.0 — granular backlog / dependencies

The legacy Q07/terrain, Q12/forest and Q13/night-light tasks (issues #36, #41, #42 and related) may be in progress on other branches. **Do not rewrite or claim ownership of those tasks.** Coordinate integration with root agent; finish conflicting Built-in work or move via planned ownership transfer before modifying shared shader/scene assets.

| ID | Priority | Dependency | Scoped deliverable | Proposed owner | Status |
| --- | --- | --- | --- | --- | --- |
| GR-00 | P0 | none | Desktop inventory + editor/player baseline; local pack check + fixed screenshot matrix | desktop integration Codex | TODO |
| GR-01 | P0 | GR-00 | Disposable URP install/config; Forward vs Forward+ proof; clean rollback | rendering Codex | TODO |
| GR-02 | P0 | GR-01 | LC StylizedLit URP parity; property map, normal, roughness, local lights, passes | shader Codex | TODO |
| GR-03 | P0 | GR-02 | LC Terrain URP parity, seam and mask tests | terrain rendering agent | TODO |
| GR-04 | P0 | GR-02 | LC Foliage + Grass/wheat URP parity, correct wind/shadow + thin geometry AA | foliage shader agent | TODO |
| GR-05 | P0 | GR-02 | Sky, moon, fog, emissive, night-light pool/distant URP parity | atmosphere/night agent | TODO |
| GR-06 | P0 | GR-03/04/05 | Approved Golden Slice with screenshot scorecard and texture/grounding/LOD art audit | art + desktop | TODO |
| GR-07 | P1 | GR-06 | Ground/rock/scarp/path/shoreline art rework using original models and licensed source | environment art agent | TODO |
| GR-08 | P1 | GR-06 | Consistent sun/ambient shadows + daytime/night color grade; optional restrained SSAO/decals | lighting technical artist | TODO |
| GR-09 | P1 | GR-06 | Performance matrix, quality tiers, SRP Batcher/GPU RD trial and MPB compatibility | perf agent | TODO |
| GR-10 | P1 | GR-07/08/09 | Player regressions with 2/8/16-player maps, no fog/save/clock or memory regressions | desktop QA | TODO |
| GR-11 | P1 | GR-10 | Production switch decision, update old rendering docs, rollback plan and reviewed PR | root integrator | TODO |
| GR-12 | P2 | GR-11 | APV static-sector prototype or custom Render Graph fog/reflection only if business case and proof | R&D | PARKED |

## Parallelism rule

- GR-02/03/04/05 should **not** simultaneously edit the same `Assets/_Game/Shaders`, shared includes, project settings, global lighting controller, central material catalog or world scene. Prefer separate branches, separate file owners and review handoff.
- GR-07 should not modify authoritative river carving, SurfaceKind masks or bridge geometry without world-owner design approval.
- Keep broad `ProjectSettings`, `Packages`, shader migration, scene/prefab GUID edits under a single root integrator at a time.
- Assign no tasks to an unconfirmed human/GitHub username; task tickets can remain unassigned.
- Existing #36, #41, #42 require consultation before GR-03/04/05, and their original acceptance criteria remain active.

## First Codex action

**GR-00 only**. Full prompt in `codex-handoff.md`. Once data exists, GR-01 becomes actionable and acceptance thresholds can be set without invention.
