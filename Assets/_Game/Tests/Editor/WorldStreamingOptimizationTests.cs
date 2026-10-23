using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class WorldStreamingOptimizationTests
    {
        [Test]
        public void ColliderRadius_IsClampedToVisibleLoadRadius()
        {
            WorldStreamingSettings settings =
                ScriptableObject.CreateInstance<
                    WorldStreamingSettings>();

            try
            {
                var serialized =
                    new SerializedObject(
                        settings);

                serialized.FindProperty(
                    "loadRadiusChunks").intValue = 3;

                serialized.FindProperty(
                    "colliderRadiusChunks").intValue = 9;

                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    settings.ColliderRadiusChunks,
                    Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(
                    settings);
            }
        }

        [Test]
        public void GeneratedObjectColliderRadius_IsClampedToVisibleLoadRadius()
        {
            WorldStreamingSettings settings =
                ScriptableObject.CreateInstance<
                    WorldStreamingSettings>();

            try
            {
                var serialized =
                    new SerializedObject(
                        settings);

                serialized.FindProperty(
                    "loadRadiusChunks").intValue = 3;

                serialized.FindProperty(
                    "generatedObjectColliderRadiusChunks").intValue = 9;

                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    settings.GeneratedObjectColliderRadiusChunks,
                    Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(
                    settings);
            }
        }

        [Test]
        public void StreamedChunkView_TiersGeneratedCollidersAndPreservesAuthoredDisabledState()
        {
            var root =
                new GameObject(
                    "GeneratedColliderTier_Test");

            var generatedA =
                new GameObject(
                    "Generated_A");

            generatedA.transform.SetParent(
                root.transform,
                false);

            BoxCollider enabledCollider =
                generatedA.AddComponent<
                    BoxCollider>();

            var generatedB =
                new GameObject(
                    "Generated_B");

            generatedB.transform.SetParent(
                root.transform,
                false);

            BoxCollider authoredDisabled =
                generatedB.AddComponent<
                    BoxCollider>();

            authoredDisabled.enabled = false;

            StreamedChunkView view =
                root.AddComponent<
                    StreamedChunkView>();

            view.Initialize(
                new ChunkCoordinate(
                    0,
                    0),
                null,
                true);

            view.SetGeneratedObjectCollidersEnabled(
                false);

            Assert.That(
                enabledCollider.enabled,
                Is.False);

            Assert.That(
                authoredDisabled.enabled,
                Is.False);

            Assert.That(
                view.ActiveGeneratedObjectColliderCount,
                Is.EqualTo(0));

            view.SetGeneratedObjectCollidersEnabled(
                true);

            Assert.That(
                enabledCollider.enabled,
                Is.True);

            Assert.That(
                authoredDisabled.enabled,
                Is.False,
                "Distance tier must not enable a collider that the prefab author disabled.");

            Assert.That(
                view.ActiveGeneratedObjectColliderCount,
                Is.EqualTo(1));

            Object.DestroyImmediate(
                root);
        }

        [Test]
        public void StreamedChunkView_CanEnableAndDisableTerrainCollider()
        {
            var gameObject =
                new GameObject(
                    "ChunkView_Test");

            var mesh =
                new Mesh
                {
                    name =
                        "ChunkCollider_Test"
                };

            mesh.vertices =
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 0f, 1f)
                };

            mesh.triangles =
                new[]
                {
                    0,
                    1,
                    2
                };

            StreamedChunkView view =
                gameObject.AddComponent<
                    StreamedChunkView>();

            view.Initialize(
                new ChunkCoordinate(
                    0,
                    0),
                mesh,
                true);

            view.SetTerrainColliderEnabled(
                true);

            MeshCollider collider =
                gameObject.GetComponent<
                    MeshCollider>();

            Assert.That(
                collider,
                Is.Not.Null);

            Assert.That(
                view.TerrainColliderEnabled,
                Is.True);

            Assert.That(
                collider.sharedMesh,
                Is.SameAs(mesh));

            view.SetTerrainColliderEnabled(
                false);

            Assert.That(
                view.TerrainColliderEnabled,
                Is.False);

            view.ReleaseOwnedResources();

            Object.DestroyImmediate(
                gameObject);
        }

        [Test]
        public void VisualOnlyChunk_NeverEnablesTerrainCollider()
        {
            var gameObject =
                new GameObject(
                    "VisualChunk_Test");

            var mesh =
                new Mesh
                {
                    name =
                        "VisualChunkMesh_Test"
                };

            StreamedChunkView view =
                gameObject.AddComponent<
                    StreamedChunkView>();

            view.Initialize(
                new ChunkCoordinate(
                    0,
                    0),
                mesh,
                false);

            view.SetTerrainColliderEnabled(
                true);

            Assert.That(
                view.TerrainColliderEnabled,
                Is.False);

            view.ReleaseOwnedResources();

            Object.DestroyImmediate(
                gameObject);
        }
    }
}
