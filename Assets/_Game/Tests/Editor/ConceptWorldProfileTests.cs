using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class ConceptWorldProfileTests
    {
        private const string Root = "Assets/_Game/Settings/World/ConceptWorld_v001/";

        [Test]
        public void ConceptProfile_IsIsolatedAndOrdered()
        {
            var concept = AssetDatabase.LoadAssetAtPath<WorldDefinition>(Root + "ConceptWorldDefinition.asset");
            if (concept == null) Assert.Ignore("Optional concept profile has not been created yet.");
            var main = AssetDatabase.LoadAssetAtPath<WorldDefinition>("Assets/_Game/Settings/World/MainWorldDefinition.asset");
            Assert.That(concept.GenerationSettings, Is.Not.SameAs(main.GenerationSettings));
            Assert.That(concept.MacroPlannerSettings, Is.Not.SameAs(main.MacroPlannerSettings));
            Assert.That(concept.GenerationSettings.CellWorldSize, Is.EqualTo(0.5f));
            Assert.That(concept.MacroPlannerSettings.Bridges.useFixedStoneBridgeSites, Is.True);
            bool seenCarving = false, seenStamp = false, seenTerrain = false;
            foreach (var stage in concept.GenerationSettings.Stages)
            {
                Assert.That(AssetDatabase.GetAssetPath(stage), Does.StartWith(Root));
                if (stage is LayeredTerrainStage terrain)
                {
                    seenTerrain = true;
                    Assert.That(terrain.UseStylizedLandforms, Is.True);
                }
                if (stage is RiverTerrainCarvingStage) seenCarving = true;
                if (stage is FixedBridgeTerrainStage)
                {
                    Assert.That(seenCarving, Is.True);
                    seenStamp = true;
                }
                if (stage is TerrainClassificationStage) Assert.That(seenStamp, Is.True);
            }
            Assert.That(seenTerrain && seenStamp, Is.True);
            Assert.DoesNotThrow(() => new WorldGenerationPipeline(concept.GenerationSettings));
        }

        [Test]
        public void ConceptProfile_NegativeBordersAndDeterminism()
        {
            var concept = AssetDatabase.LoadAssetAtPath<WorldDefinition>(Root + "ConceptWorldDefinition.asset");
            if (concept == null) Assert.Ignore("Optional concept profile has not been created yet.");
            var pipeline = new WorldGenerationPipeline(concept.GenerationSettings);
            foreach (var coordinate in new[] { new ChunkCoordinate(-1, -1), new ChunkCoordinate(0, -1) })
            {
                Assert.That(WorldGenerationDiagnostics.ValidateDeterminism(pipeline, 12345, coordinate, .0001f, out string message), Is.True, message);
                Assert.That(WorldGenerationDiagnostics.ValidateEastWestBorder(pipeline, 12345, coordinate, .0001f, out message), Is.True, message);
                Assert.That(WorldGenerationDiagnostics.ValidateNorthSouthBorder(pipeline, 12345, coordinate, .0001f, out message), Is.True, message);
            }
        }
    }
}
