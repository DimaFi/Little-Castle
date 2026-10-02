using System.Collections;
using System.Collections.Generic;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LittleCastle.Tests
{
    public sealed class WorldStreamerPlayModeTests
    {
        [UnityTest]
        public IEnumerator TestScene_StreamsAcrossFramesAndPreservesRuntimeDelta()
        {
            float loadStartedAt = Time.realtimeSinceStartup;
            AsyncOperation load = SceneManager.LoadSceneAsync("WorldGenerationTest");
            while (!load.isDone)
                yield return null;
            float sceneLoadMilliseconds =
                (Time.realtimeSinceStartup - loadStartedAt) * 1000f;
            float maximumObservedFrameMilliseconds = 0f;

            WorldStreamer streamer =
                Object.FindFirstObjectByType<WorldStreamer>();
            Assert.That(streamer, Is.Not.Null);

            GameObject focusObject = GameObject.Find("TestFocus");
            GameObject presentationObject = GameObject.Find("ChunkPresentationRoot");
            Assert.That(focusObject, Is.Not.Null);
            Assert.That(presentationObject, Is.Not.Null);
            Assert.That(presentationObject.transform.IsChildOf(focusObject.transform), Is.False);

            for (int i = 0; i < 10; i++)
            {
                float frameStartedAt = Time.realtimeSinceStartup;
                yield return null;
                maximumObservedFrameMilliseconds = Mathf.Max(
                    maximumObservedFrameMilliseconds,
                    (Time.realtimeSinceStartup - frameStartedAt) * 1000f);
            }

            Assert.That(
                streamer.ActiveChunkCount,
                Is.EqualTo(
                    streamer.DesiredChunkCount));

            int cacheLimit =
                streamer.Definition.StreamingSettings.MaxCachedChunks;

            if (cacheLimit > 0)
            {
                Assert.That(
                    streamer.CachedChunkCount,
                    Is.LessThanOrEqualTo(
                        cacheLimit));
            }

            if (streamer.Definition.StreamingSettings.AddMeshCollider)
            {
                Assert.That(
                    streamer.ActiveTerrainColliderCount,
                    Is.GreaterThan(0));

                Assert.That(
                    streamer.ActiveTerrainColliderCount,
                    Is.LessThanOrEqualTo(
                        streamer.ActiveChunkCount));
            }

            Vector3 stationaryRootPosition = presentationObject.transform.position;
            StreamedChunkView[] originalViews =
                presentationObject.GetComponentsInChildren<StreamedChunkView>();
            var originalMeshes = new List<Mesh>();

            foreach (StreamedChunkView view in originalViews)
            {
                originalMeshes.Add(view.GetComponent<MeshFilter>().sharedMesh);
                Assert.That(view.transform.position,
                    Is.EqualTo(view.Coordinate.GetWorldOrigin(64f)));
            }

            GeneratedWorldObject removedObject =
                presentationObject.GetComponentInChildren<GeneratedWorldObject>();
            Assert.That(removedObject, Is.Not.Null);
            long removedStableId = removedObject.StableId;
            ChunkCoordinate removedChunk =
                removedObject.GetComponentInParent<StreamedChunkView>().Coordinate;
            Assert.That(streamer.RuntimeDelta.MarkSpawnRemoved(removedStableId), Is.True);

            focusObject.transform.position = new Vector3(64f * 5f, 0f, -64f * 2f);
            for (int i = 0; i < 16; i++)
            {
                float frameStartedAt = Time.realtimeSinceStartup;
                yield return null;
                maximumObservedFrameMilliseconds = Mathf.Max(
                    maximumObservedFrameMilliseconds,
                    (Time.realtimeSinceStartup - frameStartedAt) * 1000f);
            }

            Assert.That(
                streamer.ActiveChunkCount,
                Is.EqualTo(
                    streamer.DesiredChunkCount));

            int cacheLimit =
                streamer.Definition.StreamingSettings.MaxCachedChunks;

            if (cacheLimit > 0)
            {
                Assert.That(
                    streamer.CachedChunkCount,
                    Is.LessThanOrEqualTo(
                        cacheLimit));
            }

            if (streamer.Definition.StreamingSettings.AddMeshCollider)
            {
                Assert.That(
                    streamer.ActiveTerrainColliderCount,
                    Is.GreaterThan(0));

                Assert.That(
                    streamer.ActiveTerrainColliderCount,
                    Is.LessThanOrEqualTo(
                        streamer.ActiveChunkCount));
            }
            Assert.That(presentationObject.transform.position, Is.EqualTo(stationaryRootPosition));

            bool releasedAnyMesh = false;
            foreach (Mesh mesh in originalMeshes)
                releasedAnyMesh |= mesh == null;
            Assert.That(releasedAnyMesh, Is.True, "No old runtime mesh was released after moving focus.");

            focusObject.transform.position = removedChunk.GetWorldOrigin(64f);
            for (int i = 0; i < 16; i++)
            {
                float frameStartedAt = Time.realtimeSinceStartup;
                yield return null;
                maximumObservedFrameMilliseconds = Mathf.Max(
                    maximumObservedFrameMilliseconds,
                    (Time.realtimeSinceStartup - frameStartedAt) * 1000f);
            }

            Assert.That(streamer.ActiveChunkCount, Is.EqualTo(5));
            foreach (GeneratedWorldObject worldObject in
                presentationObject.GetComponentsInChildren<GeneratedWorldObject>())
            {
                Assert.That(worldObject.StableId, Is.Not.EqualTo(removedStableId),
                    "Removed generated object returned after runtime unload/reload.");
            }

            streamer.ShutdownStreaming();
            yield return null;
            Assert.That(streamer.ActiveChunkCount, Is.EqualTo(0));

            Debug.Log(
                $"WORLD_VERIFY playmode sceneLoad={sceneLoadMilliseconds:F2} ms, " +
                $"maxObservedFrame={maximumObservedFrameMilliseconds:F2} ms, " +
                $"activeTarget={streamer.DesiredChunkCount}, " +
                $"colliders={streamer.ActiveTerrainColliderCount}.");
        }
    }
}
