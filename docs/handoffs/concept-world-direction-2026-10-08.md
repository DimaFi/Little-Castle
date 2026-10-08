# Concept world: implemented foundation and visual direction

The two October 8 landscape images are artistic references, not a recipe to
automatically build the player's village. Buildings, gates and walls already
exist; players will place them on naturally mixed flat and uneven terrain.
Watermills are explicitly out of scope for now.

## Generation dependency graph

```text
seed + absolute world coordinates
  -> broad meadows / rounded hills / selected terraced highlands
  -> finite macro river network and candidate road graph
  -> compatible, non-overlapping fixed stone bridge sites
  -> river carving -> exact bridge site stamp -> slope/classification
  -> road / river / protected-site masks -> environment fields and scattering
  -> streamed presentation, registered reviewed assets, placement validation
```

The original generation mode stays unchanged by default. New optional profile:
`Assets/_Game/Settings/World/ConceptWorld_v001/ConceptWorldDefinition.asset`.
Creation command: `Little Castle/World/Create Isolated Concept World Profile`.
The builder refuses to overwrite an existing profile and does not change the
current scene, Main* settings, lighting, materials or renderer. The prototype
uses 32 m streamed chunks / 64 cells (0.5 m samples) to resolve a small crossing;
this changes streaming coverage and needs a later frame-time/memory budget pass
before it becomes a production profile.

## Bridge contract

Bridge_Stone_A v002 is one unchanged model, not a stretch-to-fit prop. Local +Z
is travel, +X is river. Length 10.8 m, clear walkable width 2.86 m, crown 1.05 m;
entry sockets at local (0,0,+/-5.4). The protected authored site has half extents
X=7 m/Z=8 m and uses one deterministic local bank/bed/approach profile. Only
placement translation and yaw vary. Water level is baseElevation -0.85 m.
Flags sit on the two entrance stone caps, each on the entering traveler's right.

Acceptable crossings are checked for channel width, angle, terrain grade and
site separation. Unsuitable crossings must not be rendered as safe roads with
no bridge. The prototype rejects such road geometry; automatic rerouting and
guaranteed connectivity across the entire map remain a separate algorithmic
step. Candidate road connections are not proof of navigable paths.

Art release contains bridge, simplified collision, LODs, shoreline rocks,
plants and a standalone site preview. Runtime must not layer standalone terrain
over generated terrain or standalone water over global water: use the common
stamp and water elevation, with decor protected from generic scatter. Do not
stretch the model to compensate for bad generation.

## Getting closer to the references

1. First validate the silhouette and traversal: broad useful meadows, occasional
   uneven hills, highland terraces, rivers and readable approaches. Current
   code is a heightfield foundation; it is not the finished pictured world.
2. Add authored faceted rock outcrops to selected scarp bands, with their own
   mesh LODs/collision. A heightfield alone cannot form overhangs or all the
   reference's chunky rock faces. Use calm grass surfaces on shelf tops and
   stone by slope/height/region, not noise splashed over every plain.
3. Cluster existing trees by biome/forest density and keep clear meadows,
   village footprints and road margins. Validate actual foliage meshes against
   existing wind/alpha/shared-material contracts before importing variants.
4. Dress river banks with grouped rocks, reeds and occasional flowers, preserving
   water/road masks. The bridge's close dressing is authored, not random on
   every seed. Do not scatter through the walkable corridor.
5. Tune the real Unity gameplay view: warm directional light, soft shadows,
   controlled contrast, quiet material roughness and consistent asset palette.
   Blender hero renders are visual checks, not evidence that runtime rendering
   already matches the concepts. Keep the existing Built-in rendering pipeline.
6. Measure representative scenes, LOD transitions, far shadows, frame-time and
   memory before increasing density. Textures alone do not fix silhouette,
   placement, water seams or disconnected roads.

## Future asset and character backlog

Buildings: sawmill, quarry, smithy, mill (non-water for now), barracks, market.
Characters: ruler, farmer (farm houses), lumberjack (sawmill), stonemason
(quarry), archer and swordsman (barracks), traveling merchant, priest.
All faction characters need visible recolorable clothing accents tied to the
player's flag color; retain the same garment design across factions. Merchant
is neutral white/cream and has no player color. This is a stored art contract,
not a claim that these characters have been implemented.
