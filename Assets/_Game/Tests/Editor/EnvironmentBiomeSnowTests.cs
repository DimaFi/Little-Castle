using System;
using LittleCastle.World.Environment;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class EnvironmentBiomeSnowTests
    {
        [Test]
        public void SparseField_PreservesNegativeCoordinatesAndBilinearTileSeam()
        {
            var field = new SparseWorldFloatField(1f, 8);
            Assert.IsTrue(field.SetCell(-1, -1, 1f));
            Assert.AreEqual(1f, field.Sample(-0.5f, -0.5f), 0.00001f);

            field.SetCell(7, 0, 1f);
            field.SetCell(8, 0, 1f);
            Assert.AreEqual(1f, field.Sample(8f, 0.5f), 0.00001f);
            Assert.AreEqual(0.5f, field.Sample(9f, 0.5f), 0.00001f);
        }

        [Test]
        public void BiomePainter_OverlaysDoNotAlterBaseEcology_AndEmitOneRegion()
        {
            var field = new BiomeInfluenceField(1f, 8);
            Assert.AreEqual(1f, field.Sample(EnvironmentThemeIds.Summer, 0.5f, 0.5f));

            int events = 0;
            Rect dirty = default;
            field.Changed += region => { events++; dirty = region; };

            Assert.IsTrue(field.Paint(new Vector2(0.5f, 0.5f),
                4f, EnvironmentThemeIds.Winter, 1f));
            Assert.AreEqual(1, events);
            Assert.IsTrue(dirty.Contains(new Vector2(0.5f, 0.5f)));
            Assert.AreEqual(1f, field.Sample(EnvironmentThemeIds.Winter, 0.5f, 0.5f),
                0.00001f);
            Assert.AreEqual(0f, field.Sample(EnvironmentThemeIds.Winter, 30f, 30f),
                0.00001f);
            Assert.AreEqual(1f, field.Sample(EnvironmentThemeIds.Summer, 30f, 30f),
                0.00001f);

            field.Paint(new Vector2(0.5f, 0.5f), 4f, EnvironmentThemeIds.Summer, 1f);
            Assert.AreEqual(0f, field.Sample(EnvironmentThemeIds.Winter, 0.5f, 0.5f),
                0.00001f);
        }

        [Test]
        public void SnowField_AccumulatesAndCanMeltWithoutRepaintingBiome()
        {
            var biomes = new BiomeInfluenceField(1f, 8);
            biomes.Paint(new Vector2(0.5f, 0.5f),
                3f, EnvironmentThemeIds.Winter, 1f);
            var snow = new SnowAccumulationField(1f, 8);
            var bounds = new Rect(-2f, -2f, 5f, 5f);

            snow.Advance(bounds, 1f, 0.6f, 0f, biomes, EnvironmentThemeIds.Winter);
            float first = snow.Sample(0.5f, 0.5f);
            Assert.Greater(first, 0.5f);

            snow.Advance(bounds, 1f, 0.4f, 0f, biomes, EnvironmentThemeIds.Winter);
            Assert.Greater(snow.Sample(0.5f, 0.5f), first);

            snow.Advance(bounds, 1f, 0f, 0.4f, biomes, EnvironmentThemeIds.Winter);
            Assert.Less(snow.Sample(0.5f, 0.5f), 1f);
            Assert.AreEqual(1f, biomes.Sample(EnvironmentThemeIds.Winter, 0.5f, 0.5f));
        }



        [Test]
        public void PaintHardness_ControlsSoftBorder_WithoutChangingWorldMetreRadius()
        {
            var soft = new BiomeInfluenceField(1f, 8);
            var hard = new BiomeInfluenceField(1f, 8);
            var center = new Vector2(0.5f, 0.5f);
            soft.Paint(center, 4f, EnvironmentThemeIds.Winter, 1f, 0f);
            hard.Paint(center, 4f, EnvironmentThemeIds.Winter, 1f, 1f);

            Assert.AreEqual(1f, soft.Sample(EnvironmentThemeIds.Winter,
                0.5f, 0.5f), 0.00001f);
            Assert.Greater(hard.Sample(EnvironmentThemeIds.Winter, 2.5f, 0.5f),
                soft.Sample(EnvironmentThemeIds.Winter, 2.5f, 0.5f));
            Assert.AreEqual(0f, hard.Sample(EnvironmentThemeIds.Winter,
                12.5f, 0.5f), 0.00001f);
        }

        [Test]
        public void SmoothBrush_UsesOriginalNeighborsAndSoftensSharpPeak()
        {
            var biomes = new BiomeInfluenceField(1f, 8);
            var p = new Vector2(0.5f, 0.5f);
            biomes.Paint(p, 0.6f, EnvironmentThemeIds.Winter, 1f, 1f);
            Assert.AreEqual(1f, biomes.Sample(EnvironmentThemeIds.Winter,
                0.5f, 0.5f), 0.00001f);

            Assert.IsTrue(biomes.Smooth(p, 3f,
                EnvironmentThemeIds.Winter, 1f, 1f));
            Assert.Less(biomes.Sample(EnvironmentThemeIds.Winter,
                0.5f, 0.5f), 1f);
            Assert.Greater(biomes.Sample(EnvironmentThemeIds.Winter,
                1.5f, 0.5f), 0f);
        }



        [Test]
        public void CorruptSnapshot_DoesNotOverwritePreviousValidField()
        {
            var field = new SparseWorldFloatField(2f, 8);
            field.SetCell(1, 2, 0.5f);
            Assert.Throws<ArgumentException>(() => field.Restore(
                new System.Collections.Generic.List<ScalarTileSnapshot>
                {
                    new ScalarTileSnapshot
                    {
                        tileX = 0, tileZ = 0, values = new float[2]
                    }
                }));
            Assert.AreEqual(0.5f, field.GetCell(1, 2), 0.00001f);
        }
    }
}
