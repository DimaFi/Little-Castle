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
