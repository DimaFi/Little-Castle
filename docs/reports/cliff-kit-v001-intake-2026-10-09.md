# CliffKit v001 desktop intake — 2026-10-09

Result: **Blender and isolated Unity art intake PASS; procedural-world/navigation
and Player acceptance BLOCKED / NOT RUN**. This is an inspectable candidate,
not automatic production approval. Q08 issue #37 is still open.

| Check | Actual result |
|---|---|
| Source authoring / texture provenance | PASS: 14 types, 42 deterministic meshes, 17 PNG hashes and tile borders |
| Blender geometry | PASS: all 42 closed, outward normals, positive volume, no degenerate faces/UV |
| FBX roundtrip | PASS: all 42, preserved positions within 0.1mm, triangle counts, basis/scale in Blender |
| Visual source QA | Reviewed actual Cycles images: 70 renders, all types, three LODs and front/rear |
| Unity import | PASS: 14 prefabs, 42 FBX, UV/normals/tangents, measured bounds, unit scale, sockets |
| Unity tests | Final EditMode 187/187 PASS, zero failures/skips; PlayMode XML supplied separately |
| Unity GPU fixture | Actual D3D11 RTX4070TiSUPER: 28 day/night images + three two-module LOD seams |
| Resource/collision policy | No resource spawns or decorative colliders; rigid grass cap, no foliage wind |
| Seeded terrain diagnostic | PASS negative seams and repeatability on four saved source profiles; no macro plan |
| Naive cliff/ramp footprints | REJECTED on all four selected probes: fixed mesh does not match sampled ground |
| Final terrain/scarp placements | BLOCKED pending Q08 integration; not enabled in WorldStreamer/catalog |
| Navigation/clearance | NOT RUN real agents/NavMesh; contact and infantry/cart grade example tests only |
| Streaming revisit/IDs | NOT RUN for cliffs: no Q08 stage attached |
| Player performance | NOT RUN CPU/GPU/draw calls/shadows/memory/spikes; no FPS claim |

Source geometry commit: `7dcd0945b507d4b78e620d5afd8a0cfedf2915c3`.
Immutable art release commit: `7d71b6c` (art branch `codex/cliff-kit-v001`).
Tested Unity code/prefab commit: `5c069b85d7fd67ebb2ff33b4cbfa6c3ffe06ed3c`;
base concept commit `e6a02df`; final documentation/attributes commits do not
change tested C# or prefab bytes. Art base was desktop acceptance `20a1ed4`.

Texture manifest raw SHA256:
`db4bb35eabdddbc2b8960467bda1fd0ef7d7ac55dd4957cbdf839056801d8739`.
Release manifest raw SHA256:
`6caf39663fb6c7c2956b620add0fdf20afde30b95900013b5850eb78782fcff0`.
55 payload files / 4,404,719 bytes, each checked against source and actual game
copy. Immutable `release.json` retains its packaging-time `unity_tested:false`;
this subsequent evidence records what was actually run.

## What changed after inspection

The supplied prototype had never run in Blender. It was archived unchanged in
art Tools, with its original maps preserved under Source. The new recipe fixes
open skirts/bottoms, winding, collapsed side UVs, independent LOD noise and socket
boundaries. The first rendered shapes were too regular and lost buttresses at
LOD2; the final source uses unequal block widths and shared crest/shoulders.

Actual Unity import caught a forward-basis failure. The final prefab composes
180° yaw with the existing FBX axis rotation; replacing the rotation outright
was also rejected by the measured rock bounds. The final real import passed
all 14. Extracted failure/pass diagnostics are retained in UnityChecks.txt.
Unity emits expected isolated-FBX LOD-name warnings (_LOD1/2 without earlier
tiers in the same FBX); the assembled prefab has its explicit correct LODGroup.

Actual far-tier captures revealed a brightness drop with the default distant
shader. Its three isolated materials were tuned (top light .9 / ambient1.25)
without editing shared shader code. Day/night and the three same-height joins
were rendered again. No open geometric seam was observed in this fixture.
This does not certify grass/soil blending against arbitrary procedural terrain.

| Family | LOD0 / LOD1 / LOD2 triangles | Materials per rendered tier |
|---|---|---|
| Eight cliff modules | 924 / 272 / 170 each | 2 |
| Four rocks/outcrops | 192 / 96 / 64 each | 1 |
| Two hike ramps | 576 / 192 / 108 each | 2 |

Three shared near materials + three far variants; existing Built-in LC Stylized
Lit and LC Distant Simple. LOD2 shadows/probes disabled, no colliders, texture
mips and compression enabled, ReadWrite off. Instancing is eligible; actual
batching has not been measured. Roughness maps feed the existing roughness slot
directly. Height/MossMask remain source inputs only. AO is procedural approximate
cavity, not mesh-baked. Maps retain prototype detail; further art direction can
improve weathering/material variety before final concept-level approval.

## Real terrain result and integration boundary

Saved concept_slice interior was scanned at16m spacing, seeds12345/54321/−10101/777.
Maximum scarp masks were0/0.251588/1/0. The two strongest saved samples were
(−128,−272) and (48,−288). Three terrain-phase chunks per seed (west/east/repeat)
confirmed negative borders and regeneration. Probe generation omits macro plan;
roads/rivers/bridges/building/start/resource exclusions were **not** validated.

Naive yaw0, fixed 6m cliff / 6.5m ramp at the chosen sample did not match ground;
the new contact helper correctly refused all cases. This is a real fail-case,
not evidence that every possible oriented contour placement is impossible.
The helper also rejects reservations, missing adjacent data, floating ramps,
interior height spikes, bowed shoulders and excessive last-cell slopes.
It cannot grant navigation or safely alter the heightfield. Full requirements
and exact Q08 handoff are in the linked integration document.

## Evidence and reproduction

- [Unity import bounds/counts](cliff-kit-v001-evidence/CliffKitImport.json)
- [Actual four-seed diagnostic](cliff-kit-v001-evidence/CliffKitTerrainAudit.json)
- [Final EditMode XML](cliff-kit-v001-evidence/CliffKitEditMode.xml)
- [PlayMode XML](cliff-kit-v001-evidence/CliffKitPlayMode.xml)
- [Commands/result digest](cliff-kit-v001-evidence/checks.json)
- [GPU and import diagnostics](cliff-kit-v001-evidence/UnityChecks.txt)
- [Q08 integration contract](../handoffs/cliffkit-v001-integration-2026-10-09.md)

![Actual Unity day fixture](cliff-kit-v001-evidence/Unity_Day_Contact.jpg)
![Actual Unity night fixture](cliff-kit-v001-evidence/Unity_Night_Contact.jpg)

Blender4.4.3 build hash802179c51ccc. Source/Environment/CliffKit/v001/QA contains
geometry.json, source_contract.json, 70 images and contact sheets. Exact Blender
build/QA logs are archived there; use --factory-startup to avoid local add-ons.
Asset Book validates all37 entries while preserving legacy IDs. Portable-release
audit covers469 LFS paths/309 unique objects; full LFS fsck passed. Ten original
portability tests and complete/partial CliffKit catalog test passed. The narrow
portable-audit count guard was extended from historic23 to23+complete14 so the
new catalog does not break its audit; missing/duplicate/pointer checks remain.

```powershell
$env:LITTLE_CASTLE_CLIFF_RELEASE='<hydrated art repo>\Releases\CliffKit\v001'
Unity.exe -batchmode -nographics -projectPath '<fresh game checkout>' -executeMethod LittleCastle.Editor.CliffKitAssetIntake.Import -quit -logFile '<log>'
Unity.exe -batchmode -force-d3d11 -projectPath '<game checkout>' -executeMethod LittleCastle.Editor.CliffKitDesktopReview.Run -quit -logFile '<log>'
Unity.exe -batchmode -nographics -projectPath '<game checkout>' -runTests -testPlatform EditMode -testResults '<xml>' -logFile '<log>'
```

On the delivered branch import is already present; importer refuses overwrite.
Run review on that branch, or import into a fresh base checkout with the new
Editor code first. Review owns only CliffKit_v001_Review scene, not the actual
concept scene. Main/terrain/foliage shaders, existing catalogs, heights/deposits,
WorldStreamer and other queued tasks are untouched. Test-side sky/settings edits
were discarded in this isolated checkout. No full-map object creation, fake
village, arbitrary height offsets or automatic merge.
