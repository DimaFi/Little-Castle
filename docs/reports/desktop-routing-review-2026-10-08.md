# Desktop routing review — 2026-10-08

Scope: integrated PR #15 bridge-aware macro routing. This report covers source
review and regression additions. Unity 6000.5.5f1 compiled them; final EditMode
passed 120/120. Strict real-map acceptance still FAILS (see matrix below).

## Corrections

- The route validator now rejects road segments that overlap a river centerline.
  A fixed bridge cannot serve a road that runs along the channel. The fixed
  bridge planner rejects the same geometry, including its no-terrain fallback.
- A bridge site serves a fixed crossing only when it is a fixed site with the
  authored contract and its deck axis follows the crossing road. A legacy site
  or a fixed site rotated along the river is reported as incompatible/orphan.
- At a shared river vertex, the planner still checks both incident segments.
  A gentle bend may form one deduplicated fixed site; an invalid sharp bend
  rejects the whole road. No angle or fixed footprint limit was relaxed.
- The strict opt-in connected-graph mode now adds a deterministic logical
  backbone across all point features before road pathfinding. It starts with
  the existing settlement edges, then uses shortest eligible component joins
  with stable ID tie breaks and the existing maximum connection distance.
  Legacy mode still uses settlement-only connections.

## Regression coverage added

- Centerline overlap in the validator and in fixed planning with and without
  a terrain probe.
- Fixed bridge rotated 90 degrees and a nonfixed legacy site at an otherwise
  valid crossing.
- Gentle and sharp river bends at the exact road intersection vertex.
- Two actual terrain-aware recovery attempts across an over-width river, with
  no guided anchors, no fabricated road, and no bridge site.
- Mixed settlement/ruin graph, insertion-order determinism, and an impossible
  maximum-distance graph that remains explicitly disconnected.

## Remaining acceptance

Integrated Unity EditMode passed 117/117 after the initial routing regression
additions, before the connectivity-backbone edits. The first real audit for
seed 12345 at 1024 m took 16.156 s, sampled a 261 MB managed peak, and found
33 roads, zero rivers/bridges, and 9 graph components. The root found the
cause: the source road network connects only neutral settlements while strict
validation includes ruins. Recompile and rerun the audit after the backbone.

`DesktopRoutingAudit.Run` loads the actual concept definition and terrain
probe, clones only macro settings in memory, enables strict bridge-aware flags,
and writes one JSON result per seed and map size to
`Logs/Desktop-Routing-{seed}-{size}.json`. Defaults are seed 12345 and 1024 m;
set `LC_AUDIT_SEED` and `LC_AUDIT_SIZE` for other cases. The output includes
actual route diagnostics, elapsed time, and sampled managed-memory peak.

Run 1024 m (32 concept chunks) and 3072 m (96 concept chunks) for seeds 12345,
54321, -10101, and 777 only as the desktop performance gate permits. A macro
plan does not accept a player count, so 2/8/16 player accessibility needs the
separate start/fairness integration path; synthetic feature counts cannot prove
it. Keep the existing finite-map and fixed-bridge geometry contracts.

## Actual strict matrix after the backbone

| Seed / map metres | Time s | Roads | Rivers | Fixed bridges | Components | Result |
|---|---:|---:|---:|---:|---:|---|
| 12345 / 1024 | 17.300 | 41 | 0 | 0 | 1 | FAIL: minimum bridge count |
| 54321 / 1024 | 21.022 | 42 | 0 | 0 | 1 | FAIL: minimum bridge count |
| -10101 / 1024 | 25.361 | 34 | 2 | 1 | 2 | FAIL: disconnected; 2 requested edges exhausted 12 bounded retries |
| 12345 / 3072 | 70.698 | 162 | 1 | 2 | 1 | PASS for this data-only route plan |

All four report zero orphan bridges, unbridged realized crossings and missing
realized connections. Failed requested edges are nevertheless explicitly
reported; those zero counters do not prove all requested routes were built.
JSON files in `desktop-verification-2026-10-08` retain exact settings and errors.
The matrix runs in one Editor process, so later memory samples include prior
runs and GC state; the final observed managed peak is 1,098,055,680 bytes.
These are macro timings, not player FPS or per-seed cold memory benchmarks.

River source selection is sparse (480 m, chance .65, height >=8 m, only
RollingHills/Highlands). Trace acceptance also requires at least five points.
The JSON lacks filter-stage counts, so the exact reason for zero rivers is
not established. Next: instrument source eligibility/trace rejection, test
concept-only parameter changes, and reconnect *realizable* components after
rejected crossings without weakening the fixed bridge geometry or validator.
