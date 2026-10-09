using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class ConceptNaturalVegetationTests
    {
        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null)
                    Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void GroundCover_RespectsRealPlacementClearanceAndSlopeOnlyWhenOptedIn()
        {
            WorldGenerationSettings settings = Settings();
            var chunk = GrassChunk(new ChunkCoordinate(-1, 0));
            // Real rasterized macro placement mask, in negative world X.
            WorldPlacementMaskUtility.BlockCircle(
                chunk, settings, new Vector2(-4f, 4f), 1.5f,
                PlacementBlockFlags.Grass);

            chunk.SetCellSlope(0, 0, 28f);
            chunk.SetCellSlope(1, 0, 42f);
            var stage = Own(ScriptableObject.CreateInstance<GroundCoverStage>());
            Set(stage, "respectGrassPlacementBlocks", true);
            Set(stage, "fadeGrassOnSteepSlopes", true);
            Set(stage, "slopeFadeStart", 20f);
            Set(stage, "slopeFadeEnd", 36f);

            stage.Generate(new GenerationContext(123, settings), chunk);
            Assert.That(chunk.GetGrassDensity(4, 4), Is.Zero,
                "Macro placement mask should keep grass off clearings.");
            Assert.That(chunk.GetGrassDensity(0, 0), Is.GreaterThan(0f));
            Assert.That(chunk.GetGrassDensity(0, 0), Is.LessThan(
                chunk.GetGrassDensity(0, 1)));
            Assert.That(chunk.GetGrassDensity(1, 0), Is.Zero);
            Assert.That(chunk.GetGrassDensity(0, 1), Is.GreaterThan(0.1f));
        }

        [Test]
        public void GroundCover_LegacyWithoutFlagsRetainsPreviousBehavior()
        {
            WorldGenerationSettings settings = Settings();
            var chunk = GrassChunk(new ChunkCoordinate(-1, 0));
            chunk.AddPlacementBlocks(2, 2, PlacementBlockFlags.Grass);
            chunk.SetCellSlope(1, 1, 65f);
            var stage = Own(ScriptableObject.CreateInstance<GroundCoverStage>());
            // All concept changes default to disabled for Main/legacy.
            stage.Generate(new GenerationContext(123, settings), chunk);
            Assert.That(chunk.GetGrassDensity(2, 2), Is.GreaterThan(0f));
            Assert.That(chunk.GetGrassDensity(1, 1), Is.GreaterThan(0f));
        }

        [Test]
        public void Scatter_TreesStayOnNaturalGroundWithoutChangingStableIds()
        {
            WorldGenerationSettings settings = Settings();
            var legacyChunk = GrassChunk(new ChunkCoordinate(-1, 0));
            var conceptChunk = GrassChunk(new ChunkCoordinate(-1, 0));
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 4; x++)
            {
                legacyChunk.SetSurface(x, z, SurfaceKind.Rock);
                conceptChunk.SetSurface(x, z, SurfaceKind.Rock);
            }

            var stage = Own(ScriptableObject.CreateInstance<ObjectScatterStage>());
            Set(stage, "rules", new List<ScatterSpawnRule> { TreeRule() });
            stage.Generate(new GenerationContext(8102026, settings), legacyChunk);
            Set(stage, "restrictTreeSurfaces", true);
            stage.Generate(new GenerationContext(8102026, settings), conceptChunk);

            Assert.That(legacyChunk.Spawns.Count, Is.EqualTo(64));
            Assert.That(conceptChunk.Spawns.Count, Is.EqualTo(32),
                "Tree candidates on exposed rock must be refused, not moved.");
            var legacyIds = new HashSet<long>();
            foreach (WorldSpawnData spawn in legacyChunk.Spawns)
                legacyIds.Add(spawn.stableId);

            foreach (WorldSpawnData spawn in conceptChunk.Spawns)
            {
                Assert.That(spawn.worldPosition.x, Is.GreaterThanOrEqualTo(-4f));
                Assert.That(legacyIds.Contains(spawn.stableId), Is.True,
                    "Unchanged world-grid and rule stable ID contract.");
            }
        }

        [Test]
        public void Scatter_DecorativeRocksShiftToExposedRockWithoutAddingSpawns()
        {
            WorldGenerationSettings settings = Settings();
            WorldChunkData legacy = GrassChunk(new ChunkCoordinate(0, 0));
            WorldChunkData concept = GrassChunk(new ChunkCoordinate(0, 0));
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 4; x++)
            {
                legacy.SetSurface(x, z, SurfaceKind.Rock);
                concept.SetSurface(x, z, SurfaceKind.Rock);
            }

            var stage = Own(ScriptableObject.CreateInstance<ObjectScatterStage>());
            var rule = TreeRule();
            rule.category = SpawnCategory.Rock;
            rule.archetypeId = "rock_ground_01";
            Set(stage, "rules", new List<ScatterSpawnRule> { rule });
            stage.Generate(new GenerationContext(777, settings), legacy);

            Set(stage, "biasDecorativeRocksToSlopes", true);
            Set(stage, "flatRockChanceMultiplier", 0f);
            Set(stage, "rockSlopeBiasStart", 8f);
            Set(stage, "rockSlopeBiasFull", 30f);
            stage.Generate(new GenerationContext(777, settings), concept);

            Assert.That(legacy.Spawns.Count, Is.EqualTo(64));
            Assert.That(concept.Spawns.Count, Is.EqualTo(32));
            var allowed = new HashSet<long>();
            foreach (WorldSpawnData spawn in legacy.Spawns)
                allowed.Add(spawn.stableId);
            foreach (WorldSpawnData spawn in concept.Spawns)
            {
                Assert.That(spawn.worldPosition.x, Is.LessThan(4f));
                Assert.That(allowed.Contains(spawn.stableId), Is.True);
            }
        }

        [Test]
        public void ConceptOnlyYamlFlags_AreEnabledWhileMainPresetsStayLegacy()
        {
            var conceptGrass = AssetDatabase.LoadAssetAtPath<GroundCoverStage>(
                "Assets/_Game/Settings/World/ConceptWorld_v001/Stages/09_GroundCover.asset");
            var mainGrass = AssetDatabase.LoadAssetAtPath<GroundCoverStage>(
                "Assets/_Game/Settings/World/Stages/09_GroundCover.asset");
            var conceptScatter = AssetDatabase.LoadAssetAtPath<ObjectScatterStage>(
                "Assets/_Game/Settings/World/ConceptWorld_v001/Stages/11_ObjectScatter.asset");
            var mainScatter = AssetDatabase.LoadAssetAtPath<ObjectScatterStage>(
                "Assets/_Game/Settings/World/Stages/11_ObjectScatter.asset");

            Assert.That(conceptGrass, Is.Not.Null);
            Assert.That(mainGrass, Is.Not.Null);
            Assert.That(conceptScatter, Is.Not.Null);
            Assert.That(mainScatter, Is.Not.Null);

            Assert.That(new SerializedObject(conceptGrass)
                .FindProperty("respectGrassPlacementBlocks").boolValue, Is.True);
            Assert.That(new SerializedObject(conceptGrass)
                .FindProperty("fadeGrassOnSteepSlopes").boolValue, Is.True);
            Assert.That(new SerializedObject(mainGrass)
                .FindProperty("respectGrassPlacementBlocks").boolValue, Is.False);
            Assert.That(new SerializedObject(mainGrass)
                .FindProperty("fadeGrassOnSteepSlopes").boolValue, Is.False);
            Assert.That(new SerializedObject(conceptScatter)
                .FindProperty("restrictTreeSurfaces").boolValue, Is.True);
            Assert.That(new SerializedObject(conceptScatter)
                .FindProperty("biasDecorativeRocksToSlopes").boolValue, Is.True);
            Assert.That(new SerializedObject(mainScatter)
                .FindProperty("restrictTreeSurfaces").boolValue, Is.False);
            Assert.That(new SerializedObject(mainScatter)
                .FindProperty("biasDecorativeRocksToSlopes").boolValue, Is.False);
        }

        private WorldGenerationSettings Settings()
        {
            var settings = Own(
                ScriptableObject.CreateInstance<WorldGenerationSettings>());
            Set(settings, "cellsPerSide", 8);
            Set(settings, "chunkWorldSize", 8f);
            return settings;
        }

        private static WorldChunkData GrassChunk(ChunkCoordinate position)
        {
            var chunk = new WorldChunkData(position, 8);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
            {
                chunk.SetSurface(x, z, SurfaceKind.Grass);
                chunk.SetBiome(x, z, BiomeKind.TemperateGrassland);
                chunk.SetTerrainClass(x, z, TerrainClass.Plains);
                chunk.SetMoisture(x, z, 0.5f);
                chunk.SetForestDensity(x, z, 1f);
                chunk.SetCellSlope(x, z, 0f);
            }
            return chunk;
        }

        private static ScatterSpawnRule TreeRule()
        {
            return new ScatterSpawnRule
            {
                ruleId = "natural_scatter_test",
                archetypeId = "tree_test",
                category = SpawnCategory.Tree,
                spacing = 1f,
                baseChance = 1f,
                borderJitter = 0.49f,
                allowedTerrain = TerrainClassMask.All,
                allowedBiomes = BiomeMask.All,
                multiplyByForestDensity = false,
                minScale = 1f,
                maxScale = 1f
            };
        }

        private T Own<T>(T target) where T : Object
        {
            owned.Add(target);
            return target;
        }

        private static void Set(Object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing field: " + name);
            field.SetValue(target, value);
        }
    }
}
