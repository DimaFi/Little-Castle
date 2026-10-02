using System.Collections.Generic;
using LittleCastle.Building;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class WallPathLayoutTests
    {
        [Test]
        public void StraightWall_ProducesOrderedRigidSections()
        {
            var wall =
                new WallRuntimeState
                {
                    wallId = 1001,
                    definitionId = "test_wall"
                };

            wall.controlPoints.Add(
                Vector3.zero);

            wall.controlPoints.Add(
                new Vector3(
                    0f,
                    0f,
                    10f));

            var sections =
                new List<WallSectionPose>();

            WallPathLayoutUtility.BuildSections(
                wall,
                2f,
                0.5f,
                sections);

            Assert.That(
                sections.Count,
                Is.EqualTo(5));

            for (int i = 0;
                 i < sections.Count;
                 i++)
            {
                Assert.That(
                    sections[i].position.x,
                    Is.EqualTo(0f)
                        .Within(0.001f));

                Assert.That(
                    Mathf.Abs(
                        Mathf.DeltaAngle(
                            sections[i].yawDegrees,
                            0f)),
                    Is.LessThan(0.01f));
            }
        }

        [Test]
        public void SectionIds_AreStableForSameWallAndIndex()
        {
            long a =
                WallPathLayoutUtility.CreateSectionId(
                    77,
                    4);

            long b =
                WallPathLayoutUtility.CreateSectionId(
                    77,
                    4);

            long c =
                WallPathLayoutUtility.CreateSectionId(
                    77,
                    5);

            Assert.That(
                a,
                Is.EqualTo(b));

            Assert.That(
                c,
                Is.Not.EqualTo(a));
        }

        [Test]
        public void CurvedWall_ChangesYawAcrossSections()
        {
            var wall =
                new WallRuntimeState
                {
                    wallId = 88,
                    definitionId = "curve"
                };

            wall.controlPoints.Add(
                new Vector3(
                    0f,
                    0f,
                    0f));

            wall.controlPoints.Add(
                new Vector3(
                    0f,
                    0f,
                    8f));

            wall.controlPoints.Add(
                new Vector3(
                    8f,
                    0f,
                    16f));

            var sections =
                new List<WallSectionPose>();

            WallPathLayoutUtility.BuildSections(
                wall,
                1.5f,
                0.35f,
                sections);

            Assert.That(
                sections.Count,
                Is.GreaterThan(5));

            float first =
                sections[0].yawDegrees;

            bool foundDifferentYaw = false;

            for (int i = 1;
                 i < sections.Count;
                 i++)
            {
                if (Mathf.Abs(
                        Mathf.DeltaAngle(
                            first,
                            sections[i].yawDegrees)) >
                    5f)
                {
                    foundDifferentYaw = true;
                    break;
                }
            }

            Assert.That(
                foundDifferentYaw,
                Is.True);
        }

        [Test]
        public void ClosedWall_ProducesContinuousLoop()
        {
            var wall =
                new WallRuntimeState
                {
                    wallId = 99,
                    definitionId = "closed",
                    closedLoop = true
                };

            wall.controlPoints.Add(
                new Vector3(0f, 0f, 0f));

            wall.controlPoints.Add(
                new Vector3(10f, 0f, 0f));

            wall.controlPoints.Add(
                new Vector3(10f, 0f, 10f));

            wall.controlPoints.Add(
                new Vector3(0f, 0f, 10f));

            var sections =
                new List<WallSectionPose>();

            WallPathLayoutUtility.BuildSections(
                wall,
                2f,
                0.5f,
                sections);

            Assert.That(
                sections.Count,
                Is.GreaterThanOrEqualTo(18));

            Assert.That(
                sections[0].sectionId,
                Is.Not.EqualTo(
                    sections[
                        sections.Count - 1]
                        .sectionId));
        }

        [Test]
        public void SelfCrossingPath_IsRejected()
        {
            WallPlacementDefinition definition =
                ScriptableObject.CreateInstance<
                    WallPlacementDefinition>();

            try
            {
                var wall =
                    new WallRuntimeState
                    {
                        wallId = 12,
                        definitionId = "test"
                    };

                wall.controlPoints.Add(
                    new Vector3(0f, 0f, 0f));

                wall.controlPoints.Add(
                    new Vector3(10f, 0f, 10f));

                wall.controlPoints.Add(
                    new Vector3(0f, 0f, 10f));

                wall.controlPoints.Add(
                    new Vector3(10f, 0f, 0f));

                var sections =
                    new List<WallSectionPose>();

                WallPathLayoutUtility.BuildSections(
                    wall,
                    definition.SegmentSpacing,
                    definition.CurveSampleStep,
                    sections);

                Assert.That(
                    WallPlacementValidator.Validate(
                        wall,
                        definition,
                        sections,
                        out string reason),
                    Is.False);

                Assert.That(
                    reason,
                    Does.Contain(
                        "intersects"));
            }
            finally
            {
                Object.DestroyImmediate(
                    definition);
            }
        }
    }
}
