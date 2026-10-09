using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class TerrainSurfaceVariableWidthTests
    {
        [Test]
        public void Riverbed_UsesWideDownstreamProfileOnlyWhenEnabled()
        {
            var settings =
                ScriptableObject.CreateInstance<WorldGenerationSettings>();
            var stage =
                ScriptableObject.CreateInstance<TerrainSurfaceStage>();
            try
            {
                SetPrivate(settings, "chunkWorldSize", 32f);
                SetPrivate(settings, "cellsPerSide", 64);

                var river = new WorldRiverData
                {
                    stableId = 201,
                    nominalWidth = 2f,
                    nominalDepth = 1.5f,
                    centerline = new System.Collections.Generic.List<Vector2>
                    {
                        new Vector2(2f, 16f),
                        new Vector2(30f, 16f)
                    },
                    widths = new System.Collections.Generic.List<float>
                    {
                        2f, 10f
                    }
                };
                var plan = new MacroWorldPlan(123);
                plan.AddRiver(river);
                var context =
                    new GenerationContext(123, settings, plan);

                // Cell centers: near start is narrow; farther downstream
                // should be Riverbed only with the opt-in profile enabled.
                const int nearX = 10;
                const int farX = 50;
                const int bankZ = 38;

                var legacy = new WorldChunkData(
                    new ChunkCoordinate(0, 0), 64);
                Assert.That(stage.UseVariableRiverWidth, Is.False);
                stage.Generate(context, legacy);

                Assert.That(
                    legacy.GetSurface(farX, bankZ),
                    Is.Not.EqualTo(SurfaceKind.Riverbed),
                    "Legacy nominalWidth branch must be unchanged.");

                SetPrivate(stage, "useVariableRiverWidth", true);
                var profiled = new WorldChunkData(
                    new ChunkCoordinate(0, 0), 64);
                stage.Generate(context, profiled);

                Assert.That(
                    profiled.GetSurface(farX, bankZ),
                    Is.EqualTo(SurfaceKind.Riverbed));
                Assert.That(
                    profiled.GetSurface(nearX, bankZ),
                    Is.Not.EqualTo(SurfaceKind.Riverbed),
                    "Narrow river start must not acquire the downstream width.");
            }
            finally
            {
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void OnlyIsolatedConceptSurfaceOptsIntoVariableWidth()
        {
            var concept = AssetDatabase.LoadAssetAtPath<TerrainSurfaceStage>(
                "Assets/_Game/Settings/World/ConceptWorld_v001/" +
                "Stages/07_TerrainSurface.asset");
            var main = AssetDatabase.LoadAssetAtPath<TerrainSurfaceStage>(
                "Assets/_Game/Settings/World/Stages/07_TerrainSurface.asset");
            Assert.That(concept, Is.Not.Null);
            Assert.That(main, Is.Not.Null);
            Assert.That(concept.UseVariableRiverWidth, Is.True);
            Assert.That(main.UseVariableRiverWidth, Is.False);
        }

        private static void SetPrivate(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field " + name);
            field.SetValue(target, value);
        }
    }
}
