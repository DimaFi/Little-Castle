using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class WorldStartFairnessTests
    {
        private const string DefinitionPath =
            "Assets/_Game/Settings/World/MainWorldDefinition.asset";

        [Test]
        public void StartPlanner_IsDeterministicAndKeepsStartsSeparated()
        {
            WorldDefinition definition =
                AssetDatabase.LoadAssetAtPath<WorldDefinition>(
                    DefinitionPath);

            Assert.That(
                definition,
                Is.Not.Null);

            WorldStartFairnessSettings settings =
                CreateRelaxedSettings();

            try
            {
                const int seed = 12345;
                const int players = 4;

                var sessionMap =
                    new WorldSessionMap
                    {
                        worldSeed = seed,
                        playerCount = players,
                        mapSizePresetId = "fairness_test",
                        playableChunks =
                            new WorldChunkBounds(
                                -8,
                                -8,
                                16,
                                16),
                        visualChunks =
                            new WorldChunkBounds(
                                -10,
                                -10,
                                20,
                                20)
                    };

                var macroPlan =
                    new MacroWorldPlan(
                        seed);

                var firstPlanner =
                    new WorldStartFairnessPlanner(
                        settings,
                        definition.GenerationSettings,
                        macroPlan,
                        seed);

                var secondPlanner =
                    new WorldStartFairnessPlanner(
                        settings,
                        definition.GenerationSettings,
                        macroPlan,
                        seed);

                WorldStartFairnessReport first =
                    firstPlanner.Plan(
                        sessionMap);

                WorldStartFairnessReport second =
                    secondPlanner.Plan(
                        sessionMap);

                Assert.That(
                    first.accepted,
                    Is.True,
                    first.rejectionReason);

                Assert.That(
                    second.accepted,
                    Is.True,
                    second.rejectionReason);

                Assert.That(
                    first.Starts.Count,
                    Is.EqualTo(players));

                Assert.That(
                    second.Starts.Count,
                    Is.EqualTo(players));

                for (int i = 0;
                     i < players;
                     i++)
                {
                    Assert.That(
                        second.Starts[i].stableId,
                        Is.EqualTo(
                            first.Starts[i].stableId));

                    Assert.That(
                        second.Starts[i].worldPosition,
                        Is.EqualTo(
                            first.Starts[i].worldPosition));
                }

                for (int a = 0;
                     a < first.Starts.Count;
                     a++)
                {
                    for (int b = a + 1;
                         b < first.Starts.Count;
                         b++)
                    {
                        Vector3 pa =
                            first.Starts[a].worldPosition;

                        Vector3 pb =
                            first.Starts[b].worldPosition;

                        float distance =
                            Vector2.Distance(
                                new Vector2(
                                    pa.x,
                                    pa.z),
                                new Vector2(
                                    pb.x,
                                    pb.z));

                        Assert.That(
                            distance,
                            Is.GreaterThanOrEqualTo(
                                first.minimumStartDistance -
                                0.01f));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(
                    settings);
            }
        }

        private static WorldStartFairnessSettings CreateRelaxedSettings()
        {
            WorldStartFairnessSettings settings =
                ScriptableObject.CreateInstance<
                    WorldStartFairnessSettings>();

            SetPrivateField(
                settings,
                "candidateSpacing",
                128f);

            SetPrivateField(
                settings,
                "playableEdgeMargin",
                64f);

            SetPrivateField(
                settings,
                "maximumCandidatesToEvaluate",
                256);

            SetPrivateField(
                settings,
                "buildAreaRadius",
                32f);

            SetPrivateField(
                settings,
                "maximumCenterSlope",
                45f);

            SetPrivateField(
                settings,
                "maximumBuildAreaSlope",
                45f);

            SetPrivateField(
                settings,
                "maximumBuildAreaHeightRange",
                1000f);

            SetPrivateField(
                settings,
                "forestSampleRadius",
                64f);

            SetPrivateField(
                settings,
                "minimumAverageForestDensity",
                0f);

            SetPrivateField(
                settings,
                "resourceRequirements",
                new List<StartResourceRequirement>());

            SetPrivateField(
                settings,
                "minimumSeparationMultiplier",
                0.3f);

            SetPrivateField(
                settings,
                "maximumAcceptedScoreSpread",
                1f);

            return settings;
        }

        private static void SetPrivateField(
            object target,
            string name,
            object value)
        {
            FieldInfo field =
                target.GetType().GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                "Missing private field: " +
                name);

            field.SetValue(
                target,
                value);
        }
    }
}
