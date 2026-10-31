# ADR-0007: versioned soft river section and recovery

Date: 2026-10-31. Scope: stage A of `water-soft-river-editing-20261031-ru.md`.

## Decision

River layouts 1 and 2 retain their historical geometry. Layout 3 explicitly
stores a `MapRiverSectionProfile`: bank rise, maximum cut and maximum fill.
The default retaining rise is 0.12 m, cut limit 10 m and fill limit 2 m.
Supported layout-3 node values are width 2–60 m, depth 0.15–10 m, speed
0.02–6 m/s and bank transition 0.5–30 m.

`MapRiverSection.Bed` is the shared soft cross section. The authored height
field remains the underlying ground. Neither carving nor retaining banks
are baked irreversibly into it. Water ends at the same nominal width used
by water queries. Removing a river restores the underlying ground.

Only intersecting resident terrain tiles subdivide their existing lattice
to a step no greater than 0.25 m. The session evaluates that same finer
triangle lattice; render meshes and collider meshes use it. Dry tiles keep
their original resolution. The bank's small derived retaining crest may
extend above the authored height ceiling by the configured `bankRise`;
the authored base, water limit and height-brush limit are unchanged.

The original chain's upper river requires about 3.56 m of retaining fill
near the existing ledge. Its review migration explicitly opts into a 4 m
fill cap. This does not change the default for new rivers. Migration keeps
river/node/network/waterfall IDs and the exact resolved endpoint widths.
All migrated records and their dependent route belong to one Undo command.

## Landing seam

For a soft receiving river, a terrain-supported waterfall tail has a bounded
monotone cubic transition, approximately 2 m long. Each strip reaches the
receiving level, slope and cross-sectional bed. Two short terminal samples
are added only if the existing 11,200-triangle body budget permits them.
The free-falling body does not inherit the river's depth.

The resolved points, normals, individual thicknesses and speeds are saved
in the existing route lattice. Derived landing points identify themselves
as `river-landing:<id>`, rather than pretending to retain an old triangle
contact after the terrain profile changes. Arbitrary rock assets are not
silently carved: their unsupported transition is retained for a separate
support-aware solution. All authoring limits and protected-area checks remain.

Joined sections omit internal transparent caps. The landing shader blends
into the receiving river's depth tint and foam across the full width, using
the shared flow phase. Receiving foam follows the river level at each vertex;
it is not a raised, horizontal oval floating above a sloped river.

## Persistence and historical compatibility

Map edit transport is v10. Readers for v2–9 remain. `MapRiverLegacyV9`
freezes the river fields and their order used by the v5–9 base fingerprints.
Existing v8 and real authored v9 golden files verify that contract. The
v10 fingerprint includes new profile data for authored soft rivers. An old
envelope cannot replace an authored layout-3 base or smuggle layout-3 data.
Authoring document identity stays at its existing version; layout versions
and v10 transport carry the new river contract.

Window recovery is separate from valid runtime map data. A saved map has a
`<map.json>.recovery.json` companion containing an exact valid map plus the
river-tool snapshot. Atomic replacement retains a `.bak`. On reopen the pair
must match; an unrelated recovery file cannot be attached to another map.
Source Apply first checkpoints the valid map and draft in Library, applies
a ready valid draft, preserves an incomplete one, and refuses an invalid
candidate without overwriting the last valid source. Normal draft states
are UI results rather than red stack traces.

## Rendering and editing boundaries

Vegetation exclusion includes the saved wet waterfall corridor and reverses
with its command. Spray sources derive from lips, detach/return events,
gradual impacts and the lower basin. The total per-waterfall caps are
40/96/176 for Low/Medium/High, rather than a separate full cap per source.
One owning presentation advances Edit Mode particles and material property
blocks at a bounded frequency; it never dirties shared materials per frame.

Direct handles, ordinary-bend correction, wet-bed sculpting, planar
reflections and lilies remain subsequent stages. This decision does not
claim the final calm-water appearance or full artistic acceptance.
