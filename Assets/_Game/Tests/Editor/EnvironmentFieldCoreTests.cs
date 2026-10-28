using System;
using System.Collections.Generic;
using LittleCastle.World.Environment;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class EnvironmentFieldCoreTests
    {
        private const int Winter = EnvironmentThemeIds.Winter;
        private static Vector2 Centre(int x, int z = 0) => new Vector2(x + 0.5f, z + 0.5f);
        private static void Fill(BiomeInfluenceField field, int x, int z, int theme) =>
            field.Paint(Centre(x, z), 0.1f, theme, 1f, 1f);

        [TestCase(-16777217)] [TestCase(-17)] [TestCase(-16)] [TestCase(-1)]
        [TestCase(0)] [TestCase(7)] [TestCase(8)] [TestCase(int.MinValue)] [TestCase(int.MaxValue)]
        public void IntegerTileAddressingPreservesExactCell(int cell)
        {
            var field = new SparseWorldFloatField(1f, 8);
            field.SetCell(cell, cell, 0.75f);
            Assert.That(field.GetCell(cell, cell), Is.EqualTo(0.75f));
            var restored = new SparseWorldFloatField(1f, 8);
            restored.Restore(field.Capture());
            Assert.That(restored.GetCell(cell, cell), Is.EqualTo(0.75f));
        }

        [Test]
        public void SubEpsilonWeatherTicksAccumulateAndMeltPrunesStorage()
        {
            var themes = new BiomeInfluenceField(1f, 8);
            Fill(themes, 0, 0, Winter);
            var snow = new SnowAccumulationField(1f, 8);
            var area = new Rect(0f, 0f, 1f, 1f);
            for (int i = 0; i < 100; i++) snow.Advance(area, 0.01f, 0.0001f, 0f, themes, Winter);
            Assert.That(snow.GetCell(0, 0), Is.EqualTo(0.0001f).Within(0.00000001f));
            snow.Advance(area, 1f, 0f, 1f, themes, Winter);
            Assert.That(snow.IsEmpty, Is.True);
            Assert.That(snow.Capture(), Is.Empty);
        }

        [Test]
        public void ErasePrunesTilesAndLayersWithoutDroppingSmallWeights()
        {
            var field = new BiomeInfluenceField(1f, 8);
            field.Paint(Centre(0), 0.1f, Winter, 0.000001f, 1f);
            Assert.That(field.GetCell(Winter, 0, 0), Is.GreaterThan(0f));
            field.Paint(Centre(0), 0.1f, EnvironmentThemeIds.Summer, 1f, 1f);
            Assert.That(field.Capture(), Is.Empty);
        }

        [Test]
        public void DirtyBoundsContainCompleteBilinearSupportButStayLocal()
        {
            var field = new BiomeInfluenceField(1f, 8);
            Rect dirty = default;
            int events = 0;
            field.Changed += bounds => { dirty = bounds; events++; };
            Fill(field, 0, 0, Winter);
            Assert.That(field.Sample(Winter, 1.25f, 0.5f), Is.EqualTo(0.25f));
            Assert.That(dirty.Contains(new Vector2(1.25f, 0.5f)), Is.True);
            Assert.That(dirty, Is.EqualTo(new Rect(-0.5f, -0.5f, 2f, 2f)));
            Assert.That(events, Is.EqualTo(1));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidBrushIsAtomicIncludingHardness(float invalid)
        {
            var field = new BiomeInfluenceField(1f, 8);
            Fill(field, 0, 0, Winter);
            string before = JsonUtility.ToJson(new EnvironmentFieldSnapshot { themes = field.Capture() });
            uint revision = field.Revision;
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Paint(Centre(2), 1f, 7, 1f, invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Smooth(Centre(0), 1f, Winter, 1f, invalid));
            Assert.That(JsonUtility.ToJson(new EnvironmentFieldSnapshot { themes = field.Capture() }), Is.EqualTo(before));
            Assert.That(field.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void SmoothMixesWholeVectorWithoutIntroducingSummerAtSaturatedBoundary()
        {
            var field = new BiomeInfluenceField(1f, 8);
            for (int z = -2; z <= 2; z++)
            for (int x = -2; x <= 2; x++) Fill(field, x, z, x <= 0 ? Winter : 1);
            field.Smooth(Centre(0), 0.1f, Winter, 1f, 1f);
            Assert.That(field.GetCell(Winter, 0, 0), Is.EqualTo(0.875f));
            Assert.That(field.GetCell(1, 0, 0), Is.EqualTo(0.125f));
            Assert.That(field.GetCell(0, 0, 0), Is.EqualTo(0f));
            foreach (var layer in field.Capture())
            foreach (var tile in layer.tiles)
            for (int i = 0; i < tile.values.Length; i++) Assert.That(tile.values[i], Is.InRange(0f, 1f));
            // Summer selection uses exactly the same full-vector operation.
            Assert.That(field.Smooth(Centre(1), 0.1f, 0, 1f, 1f), Is.True);
            Assert.That(field.GetCell(0, 1, 0), Is.EqualTo(0f));
        }

        [Test]
        public void SmoothIncludesImplicitSummerAndUsesStagedNeighbourValues()
        {
            var a = new BiomeInfluenceField(1f, 8);
            var b = new BiomeInfluenceField(1f, 8);
            Fill(a, 0, 0, Winter);
            Fill(b, 0, 0, Winter);
            a.Smooth(Centre(0), 2f, Winter, 1f, 1f);
            b.Smooth(Centre(0), 2f, 0, 1f, 1f);
            Assert.That(a.GetCell(Winter, 0, 0), Is.EqualTo(0.5f));
            Assert.That(a.GetCell(Winter, 1, 0), Is.EqualTo(0.125f));
            Assert.That(a.GetCell(Winter, -1, 0), Is.EqualTo(0.125f));
            Assert.That(JsonUtility.ToJson(new EnvironmentFieldSnapshot { themes = a.Capture() }),
                Is.EqualTo(JsonUtility.ToJson(new EnvironmentFieldSnapshot { themes = b.Capture() })));
        }

        [Test]
        public void RestoreRejectsInvalidLaterLayerAtomicallyAndSnapshotsAreOwned()
        {
            var field = new BiomeInfluenceField(1f, 8);
            Fill(field, 0, 0, Winter);
            var source = field.Capture();
            source.Add(new ThemeLayerSnapshot { themeId = 4,
                tiles = new List<ScalarTileSnapshot> { new ScalarTileSnapshot { values = new float[1] } } });
            uint revision = field.Revision;
            Assert.Throws<ArgumentException>(() => field.Restore(source));
            Assert.That(field.GetCell(Winter, 0, 0), Is.EqualTo(1f));
            Assert.That(field.Revision, Is.EqualTo(revision));
            source[0].tiles[0].values[0] = 0f;
            Assert.That(field.GetCell(Winter, 0, 0), Is.EqualTo(1f));
        }

        [Test]
        public void CompactTileBeforeAfterCanUndoAcrossSeamsWithoutTouchingOtherTiles()
        {
            var field = new BiomeInfluenceField(1f, 8);
            Fill(field, -1, 0, Winter);
            Fill(field, 15, 0, 1);
            var before = field.CaptureTile(-1, 0);
            Fill(field, -1, 0, 2);
            var after = field.CaptureTile(-1, 0);
            field.RestoreTile(-1, 0, before);
            Assert.That(field.GetCell(Winter, -1, 0), Is.EqualTo(1f));
            Assert.That(field.GetCell(1, 15, 0), Is.EqualTo(1f));
            field.RestoreTile(-1, 0, after);
            Assert.That(field.GetCell(2, -1, 0), Is.EqualTo(1f));
            Assert.That(field.GetCell(Winter, -1, 0), Is.EqualTo(0f));
            after[0].tiles[0].tileX = 0;
            Assert.Throws<ArgumentException>(() => field.RestoreTile(-1, 0, after));
            Assert.That(field.GetCell(2, -1, 0), Is.EqualTo(1f));
        }

        [Test]
        public void BrushClippingRestrictsAuthoredCellsAndKeepsDirtyFilterHalo()
        {
            var field = new BiomeInfluenceField(1f, 8);
            var clip = new Rect(0f, 0f, 1f, 1f);
            Rect dirty = default;
            field.Changed += value => dirty = value;
            field.Paint(Centre(0), 3f, Winter, 1f, 1f, clip);
            Assert.That(field.GetCell(Winter, 0, 0), Is.EqualTo(1f));
            Assert.That(field.GetCell(Winter, -1, 0), Is.EqualTo(0f));
            Assert.That(field.GetCell(Winter, 1, 0), Is.EqualTo(0f));
            Assert.That(dirty.Contains(new Vector2(1.25f, 0.5f)), Is.True);
            field.Smooth(Centre(0), 3f, Winter, 1f, 1f, clip);
            Assert.That(field.GetCell(Winter, 0, 0), Is.EqualTo(0.5f));
            Assert.That(field.GetCell(Winter, 1, 0), Is.EqualTo(0f));
            Assert.That(field.Paint(Centre(10), 1f, 9, 1f, 1f, clip), Is.False);
            Assert.That(field.Capture().Count, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Paint(Centre(0), 1f, 9, 1f, 1f,
                new Rect(float.NaN, 0f, 1f, 1f)));
            Assert.That(field.Capture().Count, Is.EqualTo(1));
        }

        [Test]
        public void SignedSnowBrushSupportsExactTileUndoClippingAndErasePruning()
        {
            var snow = new SnowAccumulationField(1f, 8);
            var clip = new Rect(0f, 0f, 1f, 1f);
            snow.Paint(Centre(0), 3f, 0.25f, 1f, clip);
            Assert.That(snow.GetCell(0, 0), Is.EqualTo(0.25f));
            Assert.That(snow.GetCell(1, 0), Is.EqualTo(0f));
            var before = snow.CaptureTile(0, 0);
            snow.Paint(Centre(0), 3f, -1f, 1f, clip);
            Assert.That(snow.IsEmpty, Is.True);
            snow.RestoreTile(0, 0, before);
            Assert.That(snow.GetCell(0, 0), Is.EqualTo(0.25f));
            uint revision = snow.Revision;
            Assert.Throws<ArgumentOutOfRangeException>(() => snow.Paint(Centre(0), 1f, 1f, float.NaN));
            Assert.That(snow.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void InvalidWeatherAndExcessiveBrushAreRejectedBeforeMutation()
        {
            var field = new BiomeInfluenceField(1f, 8);
            var snow = new SnowAccumulationField(1f, 8);
            Assert.Throws<ArgumentException>(() => snow.Advance(new Rect(float.NaN, 0, 1, 1),
                1f, 1f, 0f, field, Winter));
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Paint(Vector2.zero, 100000f, Winter, 1f));
            Assert.That(field.Capture(), Is.Empty);
            Assert.That(snow.Capture(), Is.Empty);
        }
    }
}
