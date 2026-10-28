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
                Object.FindAnyObjectByType<WorldStreamer>();
            Assert.That(streamer, Is.Not.Null);

            GameObject focusObject = GameObject.Find("TestFocus");
            GameObject presentationObject = GameObject.Find("ChunkPresentationRoot");
            Assert.That(focusObject, Is.Not.Null);
            Assert.That(presentationObject, Is.Not.Null);
            Assert.That(presentationObject.transform.IsChildOf(focusObject.transform), Is.False);

            // The production camera writes TestFocus every Update. This
            // streaming test owns focus movement; otherwise the camera cancels
            // our teleport before any chunk can unload.
            foreach (LittleCastle.CameraSystem.StrategyCameraController cameraController in
                Object.FindObjectsByType<LittleCastle.CameraSystem.StrategyCameraController>())
                cameraController.enabled = false;

            // LoadSceneAsync completion does not promise Start has run yet.
            streamer.RefreshStreamingNow();

            float readyDeadline =
                Time.realtimeSinceStartup +
                20f;

            while (streamer.MissingDesiredChunkCount > 0 &&
                   Time.realtimeSinceStartup < readyDeadline)
            {
                float frameStartedAt = Time.realtimeSinceStartup;
                yield return null;
                maximumObservedFrameMilliseconds = Mathf.Max(
                    maximumObservedFrameMilliseconds,
                    (Time.realtimeSinceStartup - frameStartedAt) * 1000f);
            }

            Assert.That(
                streamer.MissingDesiredChunkCount,
                Is.EqualTo(0),
                "Initial visible streaming did not become ready before timeout.");
            LogPhase("initial-ready", streamer,
                (Time.realtimeSinceStartup - loadStartedAt) * 1000f);

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
                    Is.GreaterThan(0), "Initial collider ring was not created.");

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

            int exitDistance = streamer.Definition.StreamingSettings.UnloadRadiusChunks +
                streamer.Definition.StreamingSettings.LoadRadiusChunks + 1;
            Assert.That(exitDistance,
                Is.LessThanOrEqualTo(streamer.Definition.StreamingSettings.MacroPlanRadiusChunks),
                "Unload test movement must stay inside the session macro plan.");
            focusObject.transform.position += new Vector3(64f * exitDistance, 0f, 0f);
            float movedAt = Time.realtimeSinceStartup;
            // Without refreshing, MissingDesiredChunkCount still describes
            // the previous focus until the next Update. The wait may then
            // exit before a single frame has processed the move.
            streamer.RefreshStreamingNow();

            readyDeadline =
                Time.realtimeSinceStartup +
                20f;

            while ((streamer.MissingDesiredChunkCount > 0 ||
                    originalMeshes.Exists(mesh => mesh != null)) &&
                   Time.realtimeSinceStartup < readyDeadline)
            {
                float frameStartedAt = Time.realtimeSinceStartup;
                yield return null;
                maximumObservedFrameMilliseconds = Mathf.Max(
                    maximumObservedFrameMilliseconds,
                    (Time.realtimeSinceStartup - frameStartedAt) * 1000f);
            }

            Assert.That(
                streamer.MissingDesiredChunkCount,
                Is.EqualTo(0),
                "Moved visible streaming did not become ready before timeout.");
            LogPhase("first-visit-ready", streamer,
                (Time.realtimeSinceStartup - movedAt) * 1000f);

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
                    Is.GreaterThan(0), "Moved collider ring was not created.");

                Assert.That(
                    streamer.ActiveTerrainColliderCount,
                    Is.LessThanOrEqualTo(
                        streamer.ActiveChunkCount));
            }
            Assert.That(presentationObject.transform.position, Is.EqualTo(stationaryRootPosition));

            foreach (Mesh mesh in originalMeshes)
                Assert.That(mesh == null, Is.True,
                    "An old runtime mesh survived movement beyond the unload ring.");

            focusObject.transform.position = removedChunk.GetWorldOrigin(64f);
            float returnedAt = Time.realtimeSinceStartup;
            streamer.RefreshStreamingNow();

            readyDeadline =
                Time.realtimeSinceStartup +
                20f;

            while (streamer.MissingDesiredChunkCount > 0 &&
                   Time.realtimeSinceStartup < readyDeadline)
            {
                float frameStartedAt = Time.realtimeSinceStartup;
                yield return null;
                maximumObservedFrameMilliseconds = Mathf.Max(
                    maximumObservedFrameMilliseconds,
                    (Time.realtimeSinceStartup - frameStartedAt) * 1000f);
            }

            Assert.That(
                streamer.MissingDesiredChunkCount,
                Is.EqualTo(0),
                "Reloaded visible streaming did not become ready before timeout.");
            LogPhase("return-ready", streamer,
                (Time.realtimeSinceStartup - returnedAt) * 1000f);
            foreach (GeneratedWorldObject worldObject in
                presentationObject.GetComponentsInChildren<GeneratedWorldObject>())
            {
                Assert.That(worldObject.StableId, Is.Not.EqualTo(removedStableId),
                    "Removed generated object returned after runtime unload/reload.");
            }

            Debug.Log(
                $"WORLD_VERIFY playmode sceneLoad={sceneLoadMilliseconds:F2} ms, " +
                $"maxObservedFrame={maximumObservedFrameMilliseconds:F2} ms, " +
                $"activeTarget={streamer.DesiredChunkCount}, " +
                $"colliders={streamer.ActiveTerrainColliderCount}.");
            streamer.ShutdownStreaming();
            yield return null;
            Assert.That(streamer.ActiveChunkCount, Is.EqualTo(0));
        }

        private static void LogPhase(string phase, WorldStreamer streamer, float elapsedMs)
        {
            Debug.Log($"WORLD_VERIFY {phase}: elapsed={elapsedMs:F2} ms, " +
                $"active={streamer.ActiveChunkCount}, desired={streamer.DesiredChunkCount}, " +
                $"cached={streamer.CachedChunkCount}, colliders={streamer.ActiveTerrainColliderCount}, " +
                $"loads={streamer.TotalChunkLoads}, unloads={streamer.TotalChunkUnloads}, " +
                $"worstStage={streamer.WorstGenerationStageMilliseconds:F2} ms. " +
                "Legacy WorldGenerationTest, nographics Editor; NOT concept MATCH READY or GPU profiling.");
        }
    }
}
