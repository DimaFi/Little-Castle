# Stylized concept-world landforms

## Scope

The optional stylized landform mode gives the streamed deterministic terrain a
warmer, authored silhouette:

- broad connected meadow floors suitable for player-chosen construction;
- rounded middle-distance hill belts;
- highlands confined to selected regional clusters;
- discrete rock-scarp and terrace shelves with smoothly bounded heightfield
  transitions;
- strongly suppressed small detail on meadow plains.

It does not place houses, walls, gates, villages, roads, rivers, bridges or
watermills. Players continue to place their existing construction content.
Rivers and fixed bridges remain separate macro features and later stages may
carve or stamp the sampled base terrain.

## Opt-in contract

`LayeredTerrainStage.useStylizedLandforms` is disabled by default. When it is
disabled, `LayeredTerrainStage` executes the original regional/hill/ridge/detail
formula without routing through the new sampler.

When enabled, the stage samples:

```text
StylizedLandformSampler.SampleHeight(
    worldSeed,
    absoluteWorldX,
    absoluteWorldZ,
    settings)
```

The sampler is stateless and uses no `UnityEngine.Random`, scene objects or
full-map bootstrap. A chunk only evaluates its own `(cellsPerSide + 1)²`
height samples. Adjacent chunks therefore evaluate the same mathematical
world-space coordinates at shared borders, including negative coordinates.

`StylizedLandformSampler.Sample(...)` also returns smooth plain, hill,
highland and scarp masks for diagnostics and future presentation decisions.
They are derived values, not additional authoritative map state.

## Cozy concept preset

The serialized defaults are the first numeric concept preset:

| Control | Default | Purpose |
|---|---:|---|
| base height | 3 m | meadow datum |
| region size | 960 m | broad silhouette scale |
| plain coverage | 0.62 | prefers long meadow regions |
| meadow undulation | 1.35 m | avoids perfectly flat ground |
| hill height | 9.5 m | rounded middle terrain |
| highland coverage | 0.18 | confines mountains/highlands |
| highland height | 36 m | major selected uplift |
| scarp coverage | 0.42 | limits terracing to highland shoulders |
| terrace strength | 0.82 | legible shelves without total quantization |
| terrace step | 5.5 m | vertical shelf spacing |
| detail amplitude | 0.55 m | rough-region surface breakup |

All smaller frequencies are derived from `region size`. This intentionally
keeps the inspector focused on recognizable silhouette regions rather than a
large collection of unrelated noise knobs.

Plain coverage is regional selection, not a guarantee that every future
building footprint is flattened. The sampler does not inspect proposed
buildings and does not modify resource distribution or start fairness.

## Shape construction

The heightfield uses a low-frequency domain bend and a small number of stable
seed salts to build separate shape roles:

```text
broad region selector
    ├─ meadow floor: long-wave low-amplitude undulation
    ├─ hill belt: bounded rounded relief
    └─ selected highland mask
          ├─ smooth regional uplift
          └─ selected scarp mask -> softly blended terrace shelves

fine detail × relief mask
    └─ only 8% strength on the calmest plains
```

Terracing quantizes only the highland uplift and blends back to the smooth
surface outside selected scarp regions. The terrace riser uses a smooth finite
transition, so it remains representable by the existing terrain mesh instead
of requiring vertical or overhanging geometry.

## Verification metrics

`StylizedLandformTests` evaluates deterministic equality, changed-seed
variation, finite values, negative and positive chunk seams, the exact legacy
formula while the option is off, and distribution metrics.

The distribution diagnostic samples four seeds over a 3,072 m square. It
classifies buildable samples at at most 8 degrees and controlled steep samples
at at least 18 degrees. It requires:

- more than 52% buildable samples on average;
- one connected buildable region larger than 20% of the sample;
- more than 42% strong-plain samples;
- selected, non-global highlands;
- present but bounded scarp/steep terrain.

A lightweight scale report samples 2,048 m, 4,096 m and 6,144 m spans as
proxies for 2-, 8- and 16-player map implications. This is sampler coverage,
not a full session-bootstrap benchmark and it does not materialize detailed
chunks or GameObjects.

## Remaining art and gameplay integration

- Slope/surface presentation still decides which steep faces receive exposed
  rock materials; the height sampler itself stores no material IDs.
- Rivers and bridge approaches can locally carve/stamp the base heightfield in
  their owned stages. They should not be baked into this sampler.
- Final region scale and height amplitudes need visual review with the real
  camera, terrain shader, player buildings and authored rock assets.
- True overhangs, detached cliffs and rock silhouettes require presentation
  meshes or decals; a single-valued heightfield cannot represent them.
- Start placement must continue to measure actual generated slope and exits.
  The broad-meadow bias is not a replacement for fairness validation.
