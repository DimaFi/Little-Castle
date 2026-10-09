using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class ProductionCrownClearanceTests
    {
        [Test]
        public void OptInClearanceBlocksOnlyTreesOutsideLegacyRoadRadius()
        {
            var settings = ScriptableObject.CreateInstance<WorldGenerationSettings>();
            var stage = ScriptableObject.CreateInstance<MacroFeatureProjectionStage>();
            try
            {
                var data = new SerializedObject(settings);
                data.FindProperty("cellsPerSide").intValue = 8;
                data.FindProperty("chunkWorldSize").floatValue = 8;
                data.ApplyModifiedPropertiesWithoutUndo();
                var plan = new MacroWorldPlan(1);
                var road = new WorldRoadData { stableId = 2, width = 2 };
                road.centerline.Add(new Vector2(-8, 0));
                road.centerline.Add(new Vector2(0, 0));
                plan.AddRoad(road);
                var legacy = new WorldChunkData(new ChunkCoordinate(-1, 0), 8);
                stage.Generate(new GenerationContext(1, settings, plan), legacy);
                var state = new SerializedObject(stage);
                state.FindProperty("treeCrownClearance").floatValue = 5;
                state.ApplyModifiedPropertiesWithoutUndo();
                var concept = new WorldChunkData(new ChunkCoordinate(-1, 0), 8);
                stage.Generate(new GenerationContext(1, settings, plan), concept);
                Assert.That(legacy.IsPlacementBlocked(4, 4, PlacementBlockFlags.Trees), Is.False);
                Assert.That(concept.IsPlacementBlocked(4, 4, PlacementBlockFlags.Trees), Is.True);
                Assert.That(concept.IsPlacementBlocked(4, 4, PlacementBlockFlags.Resources),
                    Is.EqualTo(legacy.IsPlacementBlocked(4, 4, PlacementBlockFlags.Resources)));
                Assert.That(concept.IsPlacementBlocked(4, 4, PlacementBlockFlags.Grass),
                    Is.EqualTo(legacy.IsPlacementBlocked(4, 4, PlacementBlockFlags.Grass)));
            }
            finally { Object.DestroyImmediate(stage); Object.DestroyImmediate(settings); }
        }
    }
}
