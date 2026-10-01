# Recommended First World Generation Preset

This is the recommended first pipeline to configure once the project opens in Unity.

## Stage order

Create ScriptableObject assets and place them in exactly this order:

```text
1. LayeredTerrainStage
2. RiverTerrainCarvingStage
3. TerrainClassificationStage
4. MacroFeatureProjectionStage
5. ClimateStage
6. BiomeClassificationStage
7. TerrainSurfaceStage
8. ForestDensityStage
9. GroundCoverStage
10. ResourceDepositStage
11. ObjectScatterStage
```

The pipeline now validates generation phases and will fail fast if phases are ordered backwards.

The climate, biome, surface and ground-cover stages are included in the active
integration preset because their runtime data is already consumed and covered
by the Unity verification tests. Omitting them is only appropriate for a
deliberately reduced terrain-only diagnostic profile.

Do not add `HeightNoiseStage` together with `LayeredTerrainStage` unless intentionally composing/replacing height output.

## Suggested starting chunk settings

```text
Cells Per Side:   32
Chunk World Size: 64 m
Cell Size:         2 m
```

These are development values, not a final performance decision.

## Suggested tree rule

Example `ObjectScatterStage` rule:

```text
ruleId: tree_main_common
archetypeId: tree_main_01
category: Tree

spacing: 5-7
baseChance: 0.8
borderJitter: 0.12

allowedTerrain:
  Plains
  RollingHills

maxSlope: 28

multiplyByForestDensity: true
forestDensityExponent: 1.0

scale:
  0.9 - 1.15
```

For a very resource-rich world, prefer increasing forest regions/density rather than reducing tree spacing to extremely tiny values.

## Suggested rocks

```text
ruleId: rock_ground_common
archetypeId: rock_ground_01
category: Rock

spacing: 16-25
baseChance: 0.25-0.5
multiplyByForestDensity: false
maxSlope: 45
```

## Suggested stone deposit

```text
ruleId: stone_surface_common
resource: Stone
visualArchetypeId: deposit_stone_01

spacing: 120-200
chance: 0.25-0.4

radius: 5-12
capacity: 300-1500
richness: 0.25-1.0
```

## Suggested iron deposit

```text
ruleId: iron_highland_common
resource: IronOre
visualArchetypeId: deposit_iron_01

spacing: 300-500
chance: 0.15-0.3

allowedTerrain:
  RollingHills
  Steep
  Highlands

capacity: 500-3000
```

## Suggested neutral settlement macro rule

```text
ruleId: neutral_village_common
kind: NeutralSettlement
archetypeId: neutral_village_01

spacing: 800-1400
chance: 0.4-0.7

influenceRadius: 80-140
separationPadding: 80

allowedTerrain:
  Plains
  RollingHills

maxSlope: 10-12
```

The initial archetype can be one placeholder village prefab.

Later replace it with a settlement-layout generator without changing macro settlement identity.

## Suggested ruin rule

```text
ruleId: ruin_fortified_common
kind: Ruin
archetypeId: ruin_fortified_01

spacing: 500-900
chance: 0.2-0.4

influenceRadius: 30-60
maxSlope: 20
```

## Spawn catalog IDs

Suggested naming convention:

```text
tree_oak_01
tree_pine_01
bush_common_01
rock_ground_01
deposit_stone_01
deposit_iron_01

neutral_village_01
ruin_watchtower_01
ruin_fortified_01
bridge_wood_small
signpost_basic_01
```

Names are logical IDs. Actual prefab filenames may differ.

## First Unity validation

When Unity is available:

1. allow scripts to compile;
2. create generation ScriptableObjects;
3. configure stage order;
4. create MacroWorldPlannerSettings;
5. create WorldSpawnCatalog;
6. temporarily map only 1 tree, 1 rock, 1 deposit, 1 village, 1 ruin and 1 bridge prefab;
7. generate a small preview;
8. run generation diagnostics;
9. regenerate several seeds;
10. inspect chunk seams and Console errors before adding more assets.

Do not add hundreds of prefabs before this small vertical slice works.
