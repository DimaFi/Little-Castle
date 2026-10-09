using System.Collections.Generic;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class ChunkTerrainVertexMaskTests
    {
        [Test]
        public void RoadAndRiverMasks_AreContinuousAcrossNegativeChunkBoundary()
        {
            var plan = BuildPlan();
            var left = new WorldChunkData(new ChunkCoordinate(-1, -1), 32);
            var right = new WorldChunkData(new ChunkCoordinate(0, -1), 32);

            Color32[] a = ChunkTerrainVertexMasks.Build(left, 32f, plan);
            Color32[] b = ChunkTerrainVertexMasks.Build(right, 32f, plan);

            const int stride = 33;
            for (int z = 0; z <= 32; z++)
            {
                Color32 fromLeft = a[z * stride + 32];
                Color32 fromRight = b[z * stride];
                Assert.That(fromLeft.r, Is.EqualTo(fromRight.r),
                    "Path seam must agree at z=" + z);
                Assert.That(fromLeft.g, Is.EqualTo(fromRight.g),
                    "Water seam must agree at z=" + z);
                Assert.That(fromLeft.b, Is.EqualTo(fromRight.b));
                Assert.That(fromLeft.a, Is.EqualTo(255));
            }

            Assert.That(a[28 * stride + 32].r, Is.GreaterThan(200),
                "World (0,-4) should be a real road vertex.");
            Assert.That(a[16 * stride + 32].g, Is.GreaterThan(200),
                "World (0,-16) should be on the river.");
            Assert.That(a[28 * stride + 32].g, Is.LessThan(20));
        }

        [Test]
        public void MeshBuild_IsLegacyTransparentUntilExplicitlyEnabled()
        {
            var plan = BuildPlan();
            var chunk = new WorldChunkData(new ChunkCoordinate(-1, -1), 32);
            Mesh legacy = ChunkMeshBuilder.Build(chunk, 32f);
            Mesh enabled = ChunkMeshBuilder.Build(chunk, 32f, plan, true);
            try
            {
                Assert.That(legacy.colors32.Length, Is.Zero,
                    "Existing two-argument mesh API should not allocate masks.");
                Assert.That(enabled.colors32.Length, Is.EqualTo(33 * 33));
                Assert.That(enabled.vertexCount, Is.EqualTo(legacy.vertexCount));
                Assert.That(enabled.triangles, Is.EqualTo(legacy.triangles));
                Assert.That(enabled.bounds, Is.EqualTo(legacy.bounds),
                    "The masking pass must never modify height/geometry.");
            }
            finally
            {
                Object.DestroyImmediate(legacy);
                Object.DestroyImmediate(enabled);
            }
        }

        [Test]
        public void RiverProfile_WidensBankOnlyDownstream()
        {
            var plan = new MacroWorldPlan(123);
            var river = new WorldRiverData
            {
                stableId = 88,
                nominalWidth = 3f,
                centerline = new List<Vector2>
                {
                    new Vector2(-30f, -16f),
                    new Vector2(30f, -16f)
                },
                widths = new List<float> { 2f, 10f }
            };
            plan.AddRiver(river);

            // At +/-24m from origin, same offset of 3m from river center.
            Color32[] upstream = ChunkTerrainVertexMasks.Build(
                new WorldChunkData(new ChunkCoordinate(-1, -1), 32),
                32f, plan);
            Color32[] downstream = ChunkTerrainVertexMasks.Build(
                new WorldChunkData(new ChunkCoordinate(0, -1), 32),
                32f, plan);

            // World x=-24 -> local x=8; x=+24 -> local x=24.
            // z=-13 -> local z=19. Upstream half-width ~1.53,
            // downstream ~4.47. Blue reserved; green signifies damp bank.
            int upstreamGreen = upstream[19 * 33 + 8].g;
            int downstreamGreen = downstream[19 * 33 + 24].g;
            Assert.That(downstreamGreen, Is.GreaterThan(upstreamGreen + 50));
        }

        [Test]
        public void MaskedConceptMaterial_DoesNotEnableMasksInLegacyMaterial()
        {
            Material concept = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Settings/World/ConceptWorld_v001/" +
                "Concept_Terrain_Masked.mat");
            Material legacy = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Materials/Material_terrain/LC_Terrain_Default.mat");

            Assert.That(concept, Is.Not.Null);
            Assert.That(legacy, Is.Not.Null);
            Assert.That(concept.shader, Is.EqualTo(legacy.shader));
            Assert.That(concept.shader.name,
                Is.EqualTo("Little Castle/Terrain/LC Terrain"));
            Assert.That(concept.GetFloat("_UseVertexMasks"), Is.EqualTo(1f));
            Assert.That(legacy.GetFloat("_UseVertexMasks"), Is.EqualTo(0f));
        }

        [Test]
        public void EmptyMacroMask_ReturnsOpaqueNeutralVertexColors()
        {
            var chunk = new WorldChunkData(new ChunkCoordinate(-11, -3), 32);
            Color32[] neutral = ChunkTerrainVertexMasks.Build(chunk, 32f, null);
            Assert.That(neutral.Length, Is.EqualTo(33 * 33));
            for (int i = 0; i < neutral.Length; i++)
            {
                Assert.That(neutral[i].r, Is.Zero);
                Assert.That(neutral[i].g, Is.Zero);
                Assert.That(neutral[i].b, Is.Zero);
                Assert.That(neutral[i].a, Is.EqualTo(255));
            }
        }

        private static MacroWorldPlan BuildPlan()
        {
            var plan = new MacroWorldPlan(12345);
            plan.AddRoad(new WorldRoadData
            {
                stableId = 101,
                roadKind = RoadKind.DirtRoad,
                width = 2.6f,
                centerline = new List<Vector2>
                {
                    new Vector2(-48f, -4f), new Vector2(48f, -4f)
                }
            });
            plan.AddRiver(new WorldRiverData
            {
                stableId = 201,
                nominalWidth = 5f,
                centerline = new List<Vector2>
                {
                    new Vector2(-48f, -16f), new Vector2(48f, -16f)
                }
            });
            return plan;
        }
    }
}
