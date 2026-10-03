using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class WorldSpawnCatalogOptimizationTests
    {
        [Test]
        public void CachedCatalogResolution_IsStableAndInvalidatesExplicitly()
        {
            WorldSpawnCatalog catalog =
                ScriptableObject.CreateInstance<
                    WorldSpawnCatalog>();

            var variantA =
                new GameObject(
                    "Variant_A");

            var variantB =
                new GameObject(
                    "Variant_B");

            try
            {
                var entry =
                    new WorldSpawnCatalogEntry
                    {
                        archetypeId =
                            "tree_test",
                        prefabs =
                            new[]
                            {
                                variantA,
                                variantB
                            }
                    };

                SetEntries(
                    catalog,
                    new List<WorldSpawnCatalogEntry>
                    {
                        entry
                    });

                Assert.That(
                    catalog.ContainsArchetype(
                        "tree_test"),
                    Is.True);

                Assert.That(
                    catalog.GetValidVariantCount(
                        "tree_test"),
                    Is.EqualTo(2));

                Assert.That(
                    catalog.TryResolve(
                        "tree_test",
                        0,
                        out WorldSpawnCatalogEntry firstEntry,
                        out GameObject first),
                    Is.True);

                Assert.That(
                    firstEntry,
                    Is.SameAs(entry));

                Assert.That(
                    first,
                    Is.SameAs(variantA));

                Assert.That(
                    catalog.TryResolve(
                        "tree_test",
                        1,
                        out _,
                        out GameObject second),
                    Is.True);

                Assert.That(
                    second,
                    Is.SameAs(variantB));

                Assert.That(
                    catalog.TryResolve(
                        "tree_test",
                        2,
                        out _,
                        out GameObject wrapped),
                    Is.True);

                Assert.That(
                    wrapped,
                    Is.SameAs(variantA));

                entry.prefabs =
                    new[]
                    {
                        variantB
                    };

                catalog.InvalidateRuntimeCache();

                Assert.That(
                    catalog.GetValidVariantCount(
                        "tree_test"),
                    Is.EqualTo(1));

                Assert.That(
                    catalog.TryResolve(
                        "tree_test",
                        0,
                        out _,
                        out GameObject afterInvalidation),
                    Is.True);

                Assert.That(
                    afterInvalidation,
                    Is.SameAs(variantB));
            }
            finally
            {
                Object.DestroyImmediate(
                    catalog);

                Object.DestroyImmediate(
                    variantA);

                Object.DestroyImmediate(
                    variantB);
            }
        }

        [Test]
        public void DuplicateArchetype_PreservesFirstEntryResolution()
        {
            WorldSpawnCatalog catalog =
                ScriptableObject.CreateInstance<
                    WorldSpawnCatalog>();

            var firstPrefab =
                new GameObject(
                    "First");

            var duplicatePrefab =
                new GameObject(
                    "Duplicate");

            try
            {
                var first =
                    new WorldSpawnCatalogEntry
                    {
                        archetypeId =
                            "same_id",
                        prefabs =
                            new[]
                            {
                                firstPrefab
                            }
                    };

                var duplicate =
                    new WorldSpawnCatalogEntry
                    {
                        archetypeId =
                            "same_id",
                        prefabs =
                            new[]
                            {
                                duplicatePrefab
                            }
                    };

                SetEntries(
                    catalog,
                    new List<WorldSpawnCatalogEntry>
                    {
                        first,
                        duplicate
                    });

                Assert.That(
                    catalog.TryResolve(
                        "same_id",
                        99,
                        out WorldSpawnCatalogEntry resolvedEntry,
                        out GameObject resolvedPrefab),
                    Is.True);

                Assert.That(
                    resolvedEntry,
                    Is.SameAs(first));

                Assert.That(
                    resolvedPrefab,
                    Is.SameAs(firstPrefab));
            }
            finally
            {
                Object.DestroyImmediate(
                    catalog);

                Object.DestroyImmediate(
                    firstPrefab);

                Object.DestroyImmediate(
                    duplicatePrefab);
            }
        }

        private static void SetEntries(
            WorldSpawnCatalog catalog,
            List<WorldSpawnCatalogEntry> entries)
        {
            FieldInfo field =
                typeof(WorldSpawnCatalog)
                    .GetField(
                        "entries",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null);

            field.SetValue(
                catalog,
                entries);

            catalog.InvalidateRuntimeCache();
        }
    }
}
