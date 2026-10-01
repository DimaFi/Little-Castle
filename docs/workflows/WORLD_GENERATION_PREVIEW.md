# World Generation Preview Workflow

This is the first manual workflow for seeing procedural terrain in Unity.

## Required assets

Create:

```text
Assets/_Game/Settings/World/
├─ MainWorldGenerationSettings.asset
├─ MainLayeredTerrainStage.asset
└─ MainTerrainClassificationStage.asset
```

The exact filenames may change later, but keep generation configuration under project-owned settings.

## Pipeline order

Assign stages in this order:

1. `LayeredTerrainStage`
2. `TerrainClassificationStage`

Do not also add `HeightNoiseStage` unless intentionally testing a different height writer.

## Scene

Create a GameObject:

```text
WorldGenerator
```

Add the `WorldGenerator` component.

Assign:

- world seed;
- generation settings;
- preview radius;
- preview material.

A preview radius of 1 creates a 3x3 chunk preview.

## Commands

From the component context menu:

### Generate Preview

Rebuilds the visible chunk preview using the current seed.

### Regenerate Preview (Next Seed)

Changes the seed using a deterministic integer sequence and rebuilds the terrain.

This is useful for quickly looking at different world shapes.

### Run Generation Diagnostics

Checks:

- repeated generation is identical;
- east/west borders match;
- north/south borders match.

Run this whenever changing terrain/noise algorithms.

### Clear Preview

Removes generated preview GameObjects.

## What this preview is NOT

It is not the final world-streaming implementation.

It intentionally creates simple GameObjects and meshes so terrain algorithms can be inspected early.

Later a dedicated streamer will own loading/unloading and pooling.

## First tuning targets

Tune `LayeredTerrainStage` carefully:

- regional frequency controls the scale of broad terrain zones;
- hill frequency/amplitude controls rolling land;
- mountain frequency/amplitude controls larger relief;
- ridge sharpness controls how narrow mountain ridges become;
- detail frequency/amplitude controls small terrain variation.

Prefer changing one category at a time.

Do not compensate for bad large-scale terrain by adding huge fine-detail amplitude.
