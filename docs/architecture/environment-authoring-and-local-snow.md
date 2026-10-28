# Environment fields in World Editor

The canonical project is `E:/Games_Develop/Little-Castle`. Extend it in place;
use small verified commits. Original source workspaces remain read-only until
their migration has been checked. No additional Unity project is required.

## Data and transactions

`MapEditSession` owns sparse, world-aligned `BiomeInfluenceField` and
`SnowAccumulationField` grids: 0.5 m cells, 16 × 16 cells per storage tile.
Theme IDs are separate from the generator's immutable `BiomeKind`:
Summer=0 (implicit background), EnchantedForest=1, ElvenForest=2, Winter=3.
Theme weights sum to at most one. Painting one theme proportionally replaces
others; smoothing operates on the entire vector, including implicit Summer.

`environment.biome` and `environment.snow` use the existing `MapToolController`,
pointer projection, brush preview, world-space settings and stroke lifecycle.
The session captures a tile before its first touched stamp and after commit.
Undo/Redo stores those tiles, not a complete map per mouse movement. Cancel
restores the captured tiles. Rights are separate for biomes and snow; the
existing Player role does not receive developer environment rights.

Regional `MapChangeKind.Biomes/Snow` notifications use the existing batching
and 32 m change regions. They do not invalidate terrain height/meshes. Field
dirty bounds include bilinear support across tile borders, including negative
coordinates. Operations clip authored cell centres to finite map bounds.

Full authored JSON and `SurfacePaintData.hasEnvironmentBase/environment` retain
both fields and local weather settings. Edit-save v6 includes environment data;
v1–v5 loaders remain available. v5 base fingerprints use the exact prior field
order. Runtime writes `meadow-map-edits-v6.json` and only reads older files as
fallback, leaving the old save intact.

## Local appearance and future synchronization

`ExportBiomeSnapshot/ApplyBiomeSnapshot` exchange map identity, bounds, grid,
revision and biome weights only. Applying a trusted snapshot preserves local
snow, clearing and weather. Stale revisions and incompatible maps are rejected.
The receiving session must have no pending stroke; its local undo history is
cleared when authority replaces biome state. There is no networking transport,
server permission system, reconnection protocol or incremental replication yet.
Those remain gameplay integration work, not part of a visual shader.

`MapEnvironmentPresentation` derives a camera-resident atlas, at most
256² pixels. Its world footprint grows with camera height/FOV in quantized
steps, so zooming out does not discard biome appearance outside a fixed radius.
Small maps use their complete finite bounds. Initial creation/camera-region changes upload the
resident view. Ordinary edits upload 16² pixel tiles, at most eight pairs or
2 ms of CPU sampling per Tick. The two half-float atlases occupy about 1 MiB
before staging buffers. They are presentation caches, not the save format.

Snow is independent of biome ownership. Winter can provide an automatic
coverage baseline; explicit local snow adds coverage, and `snowClear` removes
it even inside Winter. The eraser does not repaint the biome. Weather advances
from the existing world clock in a bounded resident area and is off by default.
One 512-quad mesh provides optional local snowfall using the existing shader.
Particles, clearing and accumulated coverage are absent from biome snapshots.

The shared environment HLSL affects terrain, foliage, grass, ordinary opaque
objects and the distant shader before existing lighting/fog. Material
`_SnowAcceptance` opts individual surfaces out. Geometric upward-facing normals
control accumulation; zero mask, vertical walls and undersides stay clean.
Wind, alpha clipping, paths, UV3 sand/wet data, interaction, instancing and
existing render passes remain in their original families.

This is thin surface snow: no snow geometry, shelter/roof-occlusion simulation,
footprint physics, water freezing or replacement of ecological vegetation.
Only materials using the supported Little Castle shaders receive the effect.
The three non-summer palettes are initial appearance multipliers; authored biome
asset sets and more detailed art profiles remain follow-up work.

## Derived river shores

`RiverShoreDecorationPresentation` derives small stones from existing sandy v2
river profiles and the existing scatter library. Stable IDs depend on river ID,
seed and world cell. It excludes water, fords and painted path footprints and
checks the complete stone footprint. One instanced renderer holds bounded
resident regions; rebuilds are budgeted. Delete/Undo of a river invalidates the
derived stones. It never changes authoritative river objects or source assets.

The existing continuous v2 bed/ford, sand and wetness shader blending, grass
suppression and vegetation setbacks remain the source of bank shape/transitions.
This adds automatic decoration; reference-quality cliff modules and final
water/sand art direction still require a separate visual pass.
