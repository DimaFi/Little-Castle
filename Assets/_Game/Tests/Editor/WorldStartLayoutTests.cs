using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class WorldStartLayoutTests
    {
        [Test]
        public void PolygonRing_ThreePlayers_ProducesTriangleLikeTargets()
        {
            var options =
                new WorldSessionStartOptions
                {
                    placementMode =
                        WorldStartPlacementMode.PolygonRing,
                    ringRadius = 0.7f,
                    randomizeFormationRotation = false
                };

            var targets =
                WorldStartLayoutUtility.BuildTargets(
                    new Rect(
                        -500f,
                        -500f,
                        1000f,
                        1000f),
                    3,
                    12345,
                    options);

            Assert.That(
                targets.Count,
                Is.EqualTo(3));

            float a =
                Vector2.Distance(
                    targets[0],
                    targets[1]);

            float b =
                Vector2.Distance(
                    targets[1],
                    targets[2]);

            float c =
                Vector2.Distance(
                    targets[2],
                    targets[0]);

            Assert.That(
                Mathf.Abs(a - b),
                Is.LessThan(0.01f));

            Assert.That(
                Mathf.Abs(b - c),
                Is.LessThan(0.01f));
        }

        [Test]
        public void RadialStar_AlternatesInnerAndOuterRadius()
        {
            var options =
                new WorldSessionStartOptions
                {
                    placementMode =
                        WorldStartPlacementMode.RadialStar,
                    starInnerRadius = 0.3f,
                    starOuterRadius = 0.8f,
                    randomizeFormationRotation = false
                };

            var rect =
                new Rect(
                    -500f,
                    -500f,
                    1000f,
                    1000f);

            var targets =
                WorldStartLayoutUtility.BuildTargets(
                    rect,
                    6,
                    222,
                    options);

            Assert.That(
                targets.Count,
                Is.EqualTo(6));

            Vector2 center =
                rect.center;

            float outer =
                Vector2.Distance(
                    targets[0],
                    center);

            float inner =
                Vector2.Distance(
                    targets[1],
                    center);

            Assert.That(
                outer,
                Is.GreaterThan(inner));
        }

        [Test]
        public void SessionMapFactory_ClonesHostStartOptions()
        {
            var preset =
                new WorldMapSizePreset
                {
                    presetId = "host_options_test",
                    minimumPlayers = 1,
                    maximumPlayers = 16,
                    widthChunks = 20,
                    heightChunks = 20,
                    visualPaddingChunks = 2
                };

            var options =
                new WorldSessionStartOptions
                {
                    placementMode =
                        WorldStartPlacementMode.MapPerimeter,
                    fairnessMode =
                        WorldStartFairnessMode.WildRandom,
                    pressureResourceCompensation = 0.2f,
                    minimumExitRoutesOverride = 3
                };

            WorldSessionMap map =
                WorldSessionMapFactory.Create(
                    123,
                    8,
                    preset,
                    new ChunkCoordinate(0, 0),
                    options);

            options.placementMode =
                WorldStartPlacementMode.RandomScattered;

            options.minimumExitRoutesOverride = 1;

            Assert.That(
                map.startOptions.placementMode,
                Is.EqualTo(
                    WorldStartPlacementMode.MapPerimeter));

            Assert.That(
                map.startOptions.fairnessMode,
                Is.EqualTo(
                    WorldStartFairnessMode.WildRandom));

            Assert.That(
                map.startOptions.minimumExitRoutesOverride,
                Is.EqualTo(3));
        }
    }
}
