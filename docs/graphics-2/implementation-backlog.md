# Graphics 2.0 — granular backlog / dependencies

**Tracking epic:** [#76](https://github.com/DimaFi/Little-Castle/issues/76). **Roadmap draft PR:** [#66](https://github.com/DimaFi/Little-Castle/pull/66).

The legacy Q07/terrain, Q12/forest and Q13/night-light tasks (issues #36, #41, #42 and related) may be in progress on other branches. **Do not rewrite or claim ownership of those tasks.** Coordinate integration with root agent; finish conflicting Built-in work or move via planned ownership transfer before modifying shared shader/scene assets.

| ID | Priority | Dependency | Scoped deliverable | Proposed owner | Status |
| --- | --- | --- | --- | --- | --- |
| [#67 GR-00](https://github.com/DimaFi/Little-Castle/issues/67) | P0 | none | Desktop inventory + editor/player baseline; local pack check + fixed screenshot matrix | desktop integration Codex | TODO |
| [#68 GR-01](https://github.com/DimaFi/Little-Castle/issues/68) | P0 | [#67 GR-00](https://github.com/DimaFi/Little-Castle/issues/67) | Disposable URP install/config; Forward vs Forward+ proof; clean rollback | rendering Codex | TODO |
| [#69 GR-02](https://github.com/DimaFi/Little-Castle/issues/69) | P0 | [#68 GR-01](https://github.com/DimaFi/Little-Castle/issues/68) | LC StylizedLit URP parity; property map, normal, roughness, local lights, passes | shader Codex | TODO |
| [#70 GR-03](https://github.com/DimaFi/Little-Castle/issues/70) | P0 | [#69 GR-02](https://github.com/DimaFi/Little-Castle/issues/69) | LC Terrain URP parity, seam and mask tests | terrain rendering agent | TODO |
| [#71 GR-04](https://github.com/DimaFi/Little-Castle/issues/71) | P0 | [#69 GR-02](https://github.com/DimaFi/Little-Castle/issues/69) | LC Foliage + Grass/wheat URP parity, correct wind/shadow + thin geometry AA | foliage shader agent | TODO |
| [#72 GR-05](https://github.com/DimaFi/Little-Castle/issues/72) | P0 | [#69 GR-02](https://github.com/DimaFi/Little-Castle/issues/69) | Sky, moon, fog, emissive, night-light pool/distant URP parity | atmosphere/night agent | TODO |
| [#73 GR-06](https://github.com/DimaFi/Little-Castle/issues/73) | P0 | GR-03/04/05 | Approved Golden Slice with screenshot scorecard and texture/grounding/LOD art audit | art + desktop | TODO |
| GR-07 | P1 | [#73 GR-06](https://github.com/DimaFi/Little-Castle/issues/73) | Ground/rock/scarp/path/shoreline art rework using original models and licensed source | environment art agent | TODO |
| GR-08 | P1 | [#73 GR-06](https://github.com/DimaFi/Little-Castle/issues/73) | Consistent sun/ambient shadows + daytime/night color grade; optional restrained SSAO/decals | lighting technical artist | TODO |
| [#74 GR-09](https://github.com/DimaFi/Little-Castle/issues/74) | P1 | [#73 GR-06](https://github.com/DimaFi/Little-Castle/issues/73) | Performance matrix, quality tiers, SRP Batcher/GPU RD trial and MPB compatibility | perf agent | TODO |
| GR-10 | P1 | GR-07/08/09 | Player regressions with 2/8/16-player maps, no fog/save/clock or memory regressions | desktop QA | TODO |
| [#75 GR-11](https://github.com/DimaFi/Little-Castle/issues/75) | P1 | GR-10 | Production switch decision, update old rendering docs, rollback plan and reviewed PR | root integrator | TODO |
| GR-12 | P2 | [#75 GR-11](https://github.com/DimaFi/Little-Castle/issues/75) | APV static-sector prototype or custom Render Graph fog/reflection only if business case and proof | R&D | PARKED |

## Parallelism rule

- GR-02/03/04/05 should **not** simultaneously edit the same `Assets/_Game/Shaders`, shared includes, project settings, global lighting controller, central material catalog or world scene. Prefer separate branches, separate file owners and review handoff.
- GR-07 should not modify authoritative river carving, SurfaceKind masks or bridge geometry without world-owner design approval.
- Keep broad `ProjectSettings`, `Packages`, shader migration, scene/prefab GUID edits under a single root integrator at a time.
- Assign no tasks to an unconfirmed human/GitHub username; task tickets can remain unassigned.
- Existing #36, #41, #42 require consultation before GR-03/04/05, and their original acceptance criteria remain active.

## First Codex action

**GR-00 only**. Full prompt in `codex-handoff.md`. Once data exists, GR-01 becomes actionable and acceptance thresholds can be set without invention.

## Desktop integration hazards seen during planning

- [Draft PR #62](https://github.com/DimaFi/Little-Castle/pull/62) contains an art-**rejected** older CliffKit; technical proof is **not** art acceptance.
- [Draft PR #64](https://github.com/DimaFi/Little-Castle/pull/64) water shader and [PR #63](https://github.com/DimaFi/Little-Castle/pull/63) river geometry are stacked and unvalidated in Unity as of their PR descriptions; coordinate with owners before porting.
- [Draft PR #61](https://github.com/DimaFi/Little-Castle/pull/61) quality tiers are presentation-only and not wired into live game as described there.
- No task assumes these branches were merged into current main; inspect actual source and desktop state before applying changes.
