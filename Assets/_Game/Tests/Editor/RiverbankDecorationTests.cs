using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class RiverbankDecorationTests
    {
        private const int Cells = 32;
        private const float Size = 64f;
        private WorldGenerationSettings settings;
        private RiverbankDecorationStage stage;

        [SetUp]
        public void Setup()
        {
            settings = ScriptableObject.CreateInstance<
                WorldGenerationSettings>();
            stage = ScriptableObject.CreateInstance<
                RiverbankDecorationStage>();
            Set(settings, "cellsPerSide", Cells);
            Set(settings, "chunkWorldSize", Size);
            Set(stage, "maxCandidateChecksPerChunk", 1024);
            Set(stage, "maxSpawnsPerChunk", 128);

            RiverbankDecorationRule rule = stage.Rules[0];
            rule.ruleId = "riverbank_fixture_reeds_v1";
            rule.archetypeId = "concept_reed_PENDING_INTAKE";
            rule.category = SpawnCategory.Decoration;
            rule.spacing = 4f;
            rule.chance = 1f;
            rule.jitterMargin = 0.15f;
            rule.minimumBankDistance = 3.5f;
            rule.maximumBankDistance = 10f;
            rule.minimumMoisture = 0.4f;
            rule.maximumSlope = 18f;
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(stage);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void NoMacroRiver_MeansNoRiverbankPlants()
        {
            var data = Chunk(-1, -1);
            stage.Generate(new GenerationContext(
                -10101, settings, new MacroWorldPlan(-10101)), data);
            Assert.That(data.Spawns, Is.Empty);
            Assert.That(stage.LastCandidateChecks, Is.Zero);
        }

        [Test]
        public void WetBank_SpawnsOnlyOutsideActualRiverWidth()
        {
            MacroWorldPlan plan = RiverPlan();
            var data = Chunk(-1, -1);
            stage.Generate(new GenerationContext(-10101, settings, plan), data);
            Assert.That(data.Spawns.Count, Is.GreaterThan(0));
            foreach (WorldSpawnData spawn in data.Spawns)
            {
                float distanceFromBank =
                    Mathf.Abs(spawn.worldPosition.z + 32f) - 4f;
                Assert.That(distanceFromBank,
                    Is.InRange(3.5f, 10f));
                Assert.That(spawn.category,
                    Is.EqualTo(SpawnCategory.Decoration));
                Assert.That(spawn.archetypeId,
                    Is.EqualTo("concept_reed_PENDING_INTAKE"));
                Assert.That(spawn.worldPosition.x,
                    Is.InRange(-64f, -0.00001f));
                Assert.That(spawn.worldPosition.z,
                    Is.InRange(-64f, -0.00001f));
            }
        }

        [Test]
        public void NegativeBorder_ProducesStableNonOverlappingChunkOwnership()
        {
            MacroWorldPlan plan = RiverPlan();
            var left = Chunk(-1, -1);
            var right = Chunk(0, -1);
            stage.Generate(new GenerationContext(-10101, settings, plan), left);
            stage.Generate(new GenerationContext(-10101, settings, plan), right);
            Assert.That(left.Spawns.Count, Is.GreaterThan(0));
            Assert.That(right.Spawns.Count, Is.GreaterThan(0));
            var ids = new HashSet<long>();
            foreach (WorldSpawnData spawn in left.Spawns)
            {
                Assert.That(spawn.worldPosition.x, Is.LessThan(0f));
                Assert.That(ids.Add(spawn.stableId), Is.True);
            }
            foreach (WorldSpawnData spawn in right.Spawns)
            {
                Assert.That(spawn.worldPosition.x,
                    Is.GreaterThanOrEqualTo(0f));
                Assert.That(ids.Add(spawn.stableId), Is.True,
                    "Adjacent chunks must never claim the same grid ID.");
            }
        }

        [Test]
        public void RevisitAndSecondGenerate_KeepExactSameIDsAndNoDuplicates()
        {
            MacroWorldPlan plan = RiverPlan();
            var original = Chunk(-1, -1);
            var fresh = Chunk(-1, -1);
            var context = new GenerationContext(-10101, settings, plan);
            stage.Generate(context, original);
            stage.Generate(context, fresh);
            Assert.That(original.Spawns.Count,
                Is.EqualTo(fresh.Spawns.Count));
            for (int i = 0; i < original.Spawns.Count; i++)
            {
                Assert.That(original.Spawns[i].stableId,
                    Is.EqualTo(fresh.Spawns[i].stableId));
                Assert.That(original.Spawns[i].worldPosition,
                    Is.EqualTo(fresh.Spawns[i].worldPosition));
                Assert.That(original.Spawns[i].uniformScale,
                    Is.EqualTo(fresh.Spawns[i].uniformScale));
            }
            int before = original.Spawns.Count;
            stage.Generate(context, original);
            Assert.That(original.Spawns.Count, Is.EqualTo(before),
                "Repeated generation must not append already known IDs.");
            Assert.That(stage.LastAddedSpawns, Is.Zero);
        }

        [Test]
        public void DrySteepBlockedOrSubmergedTerrain_NeverAcceptsDecoration()
        {
            foreach (int mode in new[] { 0, 1, 2, 3 })
            {
                WorldChunkData chunk = Chunk(-1, -1);
                for (int z = 0; z < Cells; z++)
                    for (int x = 0; x < Cells; x++)
                    {
                        if (mode == 0)
                            chunk.SetMoisture(x, z, 0f);
                        if (mode == 1)
                            chunk.SetCellSlope(x, z, 55f);
                        if (mode == 2)
                            chunk.SetSurface(x, z, SurfaceKind.Riverbed);
                        if (mode == 3)
                            chunk.AddPlacementBlocks(
                                x, z, PlacementBlockFlags.Decoration);
                    }

                stage.Generate(new GenerationContext(
                    -10101, settings, RiverPlan()), chunk);
                Assert.That(chunk.Spawns, Is.Empty, "mode=" + mode);
            }
        }

        [Test]
        public void RoadExclusion_IsIndependentOfLegacyMacroDecorationMask()
        {
            MacroWorldPlan plan = RiverPlan();
            var road = new WorldRoadData
            {
                stableId = 2309,
                width = 12f
            };
            road.centerline.Add(new Vector2(-100f, -23f));
            road.centerline.Add(new Vector2(100f, -23f));
            plan.AddRoad(road);
            WorldChunkData chunk = Chunk(-1, -1);

            stage.Generate(new GenerationContext(
                -10101, settings, plan), chunk);
            foreach (WorldSpawnData spawn in chunk.Spawns)
            {
                float separation = Mathf.Abs(spawn.worldPosition.z + 23f);
                Assert.That(separation,
                    Is.GreaterThan(8f),
                    "Road half width 6 + explicit clearance 2.");
            }
        }

        [Test]
        public void FixedBridgeExcludesEntrancesAndApproachRegion()
        {
            MacroWorldPlan plan = RiverPlan();
            Vector2 site = new Vector2(-32f, -32f);
            plan.AddBridgeSite(new WorldBridgeSiteData(
                456, 457, 101,
                FixedBridgeSiteProfile.AssetId,
                site, 37f, 5f, 12f,
                FixedBridgeSiteProfile.ContractVersion, true));

            var chunk = Chunk(-1, -1);
            stage.Generate(new GenerationContext(
                -10101, settings, plan), chunk);
            foreach (WorldSpawnData spawn in chunk.Spawns)
            {
                Vector2 local = FixedBridgeSiteProfile.WorldToLocal(
                    new Vector2(
                        spawn.worldPosition.x,
                        spawn.worldPosition.z),
                    site, 37f);
                bool onBridgeApproach =
                    Mathf.Abs(local.x) <=
                        FixedBridgeSiteProfile.SupportHalfExtentX + 2f &&
                    Mathf.Abs(local.y) <=
                        FixedBridgeSiteProfile.SupportHalfExtentZ + 2f;
                Assert.That(onBridgeApproach, Is.False);
            }
        }

        [Test]
        public void SettlementInfluenceArea_LeavesPassageUnobstructed()
        {
            MacroWorldPlan plan = RiverPlan();
            plan.AddPointFeature(new WorldPointFeatureData(
                999, WorldFeatureKind.NeutralSettlement,
                "village", new Vector2(-32f, -24f), 18f));
            var chunk = Chunk(-1, -1);
            stage.Generate(new GenerationContext(
                -10101, settings, plan), chunk);
            foreach (WorldSpawnData spawn in chunk.Spawns)
            {
                float distance = Vector2.Distance(
                    new Vector2(spawn.worldPosition.x,
                        spawn.worldPosition.z),
                    new Vector2(-32f, -24f));
                Assert.That(distance, Is.GreaterThan(20f));
            }
        }

        [Test]
        public void WorkCaps_AreBoundedAndDoNotMaterializeWholeWorld()
        {
            Set(stage, "maxCandidateChecksPerChunk", 5);
            Set(stage, "maxSpawnsPerChunk", 2);
            var chunk = Chunk(-1, -1);
            stage.Generate(new GenerationContext(
                -10101, settings, RiverPlan()), chunk);
            Assert.That(stage.LastCandidateChecks, Is.LessThanOrEqualTo(5));
            Assert.That(stage.LastAddedSpawns, Is.LessThanOrEqualTo(2));
            Assert.That(chunk.Spawns.Count, Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void InvalidRuleOrGameplayBushCategory_FailsClosed()
        {
            stage.Rules[0].minimumBankDistance = 20f;
            stage.Rules[0].maximumBankDistance = 8f;
            var chunk = Chunk(-1, -1);
            stage.Generate(new GenerationContext(
                12345, settings, RiverPlan()), chunk);
            Assert.That(chunk.Spawns, Is.Empty);

            stage.Rules[0].maximumBankDistance = 25f;
            stage.Rules[0].category = SpawnCategory.Bush;
            stage.Generate(new GenerationContext(
                12345, settings, RiverPlan()), chunk);
            Assert.That(chunk.Spawns, Is.Empty,
                "No interactive gameplay bush can sneak into visual-only Q09.");
        }

        [Test]
        public void IsolatedStageAssetIsDisabledUntilProductionPrefabIntake()
        {
            const string path =
                "Assets/_Game/Settings/World/ConceptWorld_v001/" +
                "Stages/12_RiverbankDecoration.asset";
            var imported = AssetDatabase.LoadAssetAtPath<
                RiverbankDecorationStage>(path);
            Assert.That(imported, Is.Not.Null);
            Assert.That(imported.Enabled, Is.False);
            Assert.That(imported.Phase,
                Is.EqualTo(WorldGenerationStagePhase.LocalSpawns));
            Assert.That(imported.Rules.Count, Is.EqualTo(3));
            foreach (RiverbankDecorationRule rule in imported.Rules)
            {
                Assert.That(rule.IsValid, Is.True);
                Assert.That(rule.archetypeId,
                    Does.Contain("PENDING_INTAKE"),
                    "Root must provide approved real visual prefabs.");
                Assert.That(rule.category,
                    Is.EqualTo(SpawnCategory.Decoration));
            }
        }

        private static void Set(
            object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(
                field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(info, Is.Not.Null, field);
            info.SetValue(target, value);
        }

        private static WorldChunkData Chunk(int cx, int cz)
        {
            var chunk = new WorldChunkData(
                new ChunkCoordinate(cx, cz), Cells);
            for (int z = 0; z <= Cells; z++)
                for (int x = 0; x <= Cells; x++)
                    chunk.SetHeight(x, z, 4f);
            for (int z = 0; z < Cells; z++)
                for (int x = 0; x < Cells; x++)
                {
                    chunk.SetSurface(x, z, SurfaceKind.Mud);
                    chunk.SetTerrainClass(x, z, TerrainClass.Plains);
                    chunk.SetCellSlope(x, z, 0f);
                    chunk.SetMoisture(x, z, 0.9f);
                }
            return chunk;
        }

        private static MacroWorldPlan RiverPlan()
        {
            var plan = new MacroWorldPlan(-10101);
            var river = new WorldRiverData
            {
                stableId = 101,
                nominalWidth = 8f,
                nominalDepth = 2f
            };
            river.centerline.Add(new Vector2(-120f, -32f));
            river.centerline.Add(new Vector2(120f, -32f));
            river.widths.Add(8f);
            river.widths.Add(8f);
            river.depths.Add(2f);
            river.depths.Add(2f);
            plan.AddRiver(river);
            return plan;
        }
    }
}
