using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class FiniteSessionMapAndFogTests
    {
        [Test]
        public void MapRules_RejectPresetThatIsTooSmallForPlayerCount()
        {
            var small =
                new WorldMapSizePreset
                {
                    presetId = "small",
                    displayName = "Small",
                    minimumPlayers = 1,
                    maximumPlayers = 4,
                    widthChunks = 16,
                    heightChunks = 16,
                    visualPaddingChunks = 2
                };

            var large =
                new WorldMapSizePreset
                {
                    presetId = "large",
                    displayName = "Large",
                    minimumPlayers = 5,
                    maximumPlayers = 16,
                    widthChunks = 40,
                    heightChunks = 40,
                    visualPaddingChunks = 3
                };

            WorldMapRules rules =
                ScriptableObject.CreateInstance<
                    WorldMapRules>();

            try
            {
                FieldInfo field =
                    typeof(WorldMapRules).GetField(
                        "presets",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

                Assert.That(
                    field,
                    Is.Not.Null);

                field.SetValue(
                    rules,
                    new List<WorldMapSizePreset>
                    {
                        small,
                        large
                    });

                Assert.That(
                    rules.TryResolvePreset(
                        "small",
                        12,
                        out _,
                        out string error),
                    Is.False);

                Assert.That(
                    error,
                    Does.Contain("12"));

                Assert.That(
                    rules.TryResolvePreset(
                        "large",
                        12,
                        out WorldMapSizePreset resolved,
                        out error),
                    Is.True,
                    error);

                Assert.That(
                    resolved,
                    Is.SameAs(large));

                Assert.That(
                    rules.TryGetSmallestAllowedPreset(
                        12,
                        out WorldMapSizePreset smallest),
                    Is.True);

                Assert.That(
                    smallest,
                    Is.SameAs(large));
            }
            finally
            {
                Object.DestroyImmediate(
                    rules);
            }
        }

        [Test]
        public void SessionMap_HasSeparatePlayableAndVisualBounds()
        {
            var preset =
                new WorldMapSizePreset
                {
                    presetId = "test",
                    minimumPlayers = 1,
                    maximumPlayers = 12,
                    widthChunks = 20,
                    heightChunks = 16,
                    visualPaddingChunks = 3
                };

            WorldSessionMap map =
                WorldSessionMapFactory.Create(
                    12345,
                    8,
                    preset,
                    new ChunkCoordinate(0, 0));

            Assert.That(
                map.playableChunks.MinX,
                Is.EqualTo(-10));

            Assert.That(
                map.playableChunks.MinZ,
                Is.EqualTo(-8));

            Assert.That(
                map.playableChunks.SizeX,
                Is.EqualTo(20));

            Assert.That(
                map.playableChunks.SizeZ,
                Is.EqualTo(16));

            Assert.That(
                map.visualChunks.MinX,
                Is.EqualTo(-13));

            Assert.That(
                map.visualChunks.MinZ,
                Is.EqualTo(-11));

            Assert.That(
                map.visualChunks.SizeX,
                Is.EqualTo(26));

            Assert.That(
                map.visualChunks.SizeZ,
                Is.EqualTo(22));

            Assert.That(
                map.IsPlayableChunk(
                    new ChunkCoordinate(0, 0)),
                Is.True);

            Assert.That(
                map.IsPlayableChunk(
                    new ChunkCoordinate(11, 0)),
                Is.False);

            Assert.That(
                map.IsVisualChunk(
                    new ChunkCoordinate(11, 0)),
                Is.True);

            Assert.That(
                map.IsVisualChunk(
                    new ChunkCoordinate(20, 0)),
                Is.False);
        }

        [Test]
        public void FogOfWar_TransitionsFromHiddenToVisibleToExplored()
        {
            var fog =
                new FogOfWarGrid(
                    new Rect(
                        0f,
                        0f,
                        100f,
                        100f),
                    5f);

            var point =
                new Vector2(
                    50f,
                    50f);

            Assert.That(
                fog.GetVisibility(
                    point),
                Is.EqualTo(
                    FogOfWarVisibility.Hidden));

            fog.BeginVisibilityUpdate();

            fog.RevealCircle(
                point,
                12f);

            Assert.That(
                fog.GetVisibility(
                    point),
                Is.EqualTo(
                    FogOfWarVisibility.Visible));

            fog.BeginVisibilityUpdate();

            Assert.That(
                fog.GetVisibility(
                    point),
                Is.EqualTo(
                    FogOfWarVisibility.Explored));

            Assert.That(
                fog.GetVisibility(
                    new Vector2(
                        500f,
                        500f)),
                Is.EqualTo(
                    FogOfWarVisibility.Hidden));
        }

        [Test]
        public void PlayableBounds_CanClampCameraOrMovementTarget()
        {
            var bounds =
                new WorldChunkBounds(
                    -2,
                    -2,
                    4,
                    4);

            Vector3 outside =
                new Vector3(
                    1000f,
                    25f,
                    -1000f);

            Vector3 clamped =
                bounds.ClampWorldPosition(
                    outside,
                    64f,
                    1f);

            Rect worldRect =
                bounds.ToWorldRect(
                    64f);

            Assert.That(
                clamped.x,
                Is.InRange(
                    worldRect.xMin + 1f,
                    worldRect.xMax - 1f));

            Assert.That(
                clamped.z,
                Is.InRange(
                    worldRect.yMin + 1f,
                    worldRect.yMax - 1f));

            Assert.That(
                clamped.y,
                Is.EqualTo(25f));
        }
    }
}
