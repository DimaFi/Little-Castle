# Bridge Stone A v002 visual review

Reviewed on 2026-10-08 from the actual generated Blender source and renders,
not from an AI concept illustration.

## Accepted visual changes

- The arch, spandrels and paving are assembled from broad, individually readable
  stones. The paving crown and open arch remain clear in hero, side and top views.
- One stone material uses a 4 x 4 limestone atlas derived from the authored
  limestone source. Adjacent blocks receive restrained ivory, warm beige and cool
  grey variation without extra material slots or vertex-colour dependency.
- Water is a continuous turquoise surface with broken flow accents. The former
  per-face checkerboard is absent in both Cycles and Eevee renders.
- Bank dressing uses faceted, flattened rock groups, three grass colour bands,
  broad-leaf shrubs, rosettes, moss and sparse flowers. Large boulders no longer
  read as smooth spheres.
- Timber rails remain simple and readable: two continuous beams pass through five
  stone supports per side. There is no repeated bolt clutter.
- The two banners are seated on the entrance cap stones at the fixed right-hand
  travel positions `(1.64, -5.12)` and `(-1.64, 5.12)`.

## Render evidence

- `Bridge_Hero.png`: primary Cycles perspective.
- `Bridge_Reverse.png`: opposite entry and reverse banner check.
- `Bridge_Side.png`: arch opening, support roots and bridge silhouette.
- `Bridge_Top.png`: 2.86 m clear path, fixed site alignment and bank composition.
- `Bridge_Detail.png`: paving, rail, banner and atlas variation.
- `Bridge_Realtime_Eevee.png`: honest raster lighting check.

## Remaining limitations

The concept sheet is a painted target with dense compositional foliage and
illustrated edge treatment. This source remains game geometry with a fixed,
rectangular 14 x 16 m terrain-reference mask. Its outer terrain edge is visible in
isolated renders; the production world terrain must replace that reference mesh at
runtime. Water uses a lightweight base-colour surface and still needs the game's
final animated water shader. Unity rendering and play-mode checks were outside this
source-art task.
