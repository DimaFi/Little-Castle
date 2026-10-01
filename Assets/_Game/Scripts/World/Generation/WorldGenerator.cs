using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Scene-facing preview entry point.
    /// Future streaming belongs in a separate WorldStreamer subsystem.
    /// </summary>
    public sealed class WorldGenerator : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private WorldGenerationSettings settings;

        [Header("Preview")]
        [Min(0)]
        [SerializeField] private int previewRadius = 1;

        [SerializeField] private Material previewMaterial;
        [SerializeField] private WorldSpawnCatalog previewSpawnCatalog;
        [SerializeField] private bool renderGeneratedSpawns = true;
        [SerializeField] private bool generateOnStart = true;

        private readonly List<GameObject> previewObjects =
            new List<GameObject>();

        public int WorldSeed => worldSeed;

        private void Start()
        {
            if (generateOnStart)
                GeneratePreview();
        }

        [ContextMenu("Generate Preview")]
        public void GeneratePreview()
        {
            ClearPreview();

            if (!TryCreatePipeline(out WorldGenerationPipeline pipeline))
                return;

            for (int z = -previewRadius; z <= previewRadius; z++)
            {
                for (int x = -previewRadius; x <= previewRadius; x++)
                    CreatePreviewChunk(pipeline, new ChunkCoordinate(x, z));
            }
        }

        [ContextMenu("Regenerate Preview (Next Seed)")]
        public void RegenerateWithNextSeed()
        {
            unchecked
            {
                worldSeed = worldSeed * 1664525 + 1013904223;
            }

            GeneratePreview();
        }

        [ContextMenu("Run Generation Diagnostics")]
        public void RunGenerationDiagnostics()
        {
            if (!TryCreatePipeline(out WorldGenerationPipeline pipeline))
                return;

            const float epsilon = 0.0001f;
            var origin = new ChunkCoordinate(0, 0);

            bool deterministic =
                WorldGenerationDiagnostics.ValidateDeterminism(
                    pipeline,
                    worldSeed,
                    origin,
                    epsilon,
                    out string determinismMessage);

            bool eastWest =
                WorldGenerationDiagnostics.ValidateEastWestBorder(
                    pipeline,
                    worldSeed,
                    origin,
                    epsilon,
                    out string eastWestMessage);

            bool northSouth =
                WorldGenerationDiagnostics.ValidateNorthSouthBorder(
                    pipeline,
                    worldSeed,
                    origin,
                    epsilon,
                    out string northSouthMessage);

            bool spawnDeterministic =
                WorldGenerationDiagnostics.ValidateSpawnDeterminism(
                    pipeline,
                    worldSeed,
                    origin,
                    epsilon,
                    out string spawnMessage);

            if (deterministic &&
                eastWest &&
                northSouth &&
                spawnDeterministic)
            {
                Debug.Log(
                    "World generation diagnostics passed.\n" +
                    determinismMessage + "\n" +
                    eastWestMessage + "\n" +
                    northSouthMessage + "\n" +
                    spawnMessage,
                    this);
            }
            else
            {
                Debug.LogError(
                    "World generation diagnostics failed.\n" +
                    determinismMessage + "\n" +
                    eastWestMessage + "\n" +
                    northSouthMessage + "\n" +
                    spawnMessage,
                    this);
            }
        }

        [ContextMenu("Clear Preview")]
        public void ClearPreview()
        {
            for (int i = previewObjects.Count - 1; i >= 0; i--)
            {
                GameObject previewObject = previewObjects[i];

                if (previewObject == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(previewObject);
                else
                    DestroyImmediate(previewObject);
            }

            previewObjects.Clear();
        }

        private bool TryCreatePipeline(out WorldGenerationPipeline pipeline)
        {
            if (settings == null)
            {
                Debug.LogError(
                    "WorldGenerator requires WorldGenerationSettings.",
                    this);

                pipeline = null;
                return false;
            }

            pipeline = new WorldGenerationPipeline(settings);
            return true;
        }

        private void CreatePreviewChunk(
            WorldGenerationPipeline pipeline,
            ChunkCoordinate coordinate)
        {
            WorldChunkData chunkData =
                pipeline.GenerateChunk(worldSeed, coordinate);

            Mesh mesh =
                ChunkMeshBuilder.Build(
                    chunkData,
                    settings.ChunkWorldSize);

            var chunkObject =
                new GameObject($"Chunk_{coordinate.x}_{coordinate.z}");

            chunkObject.transform.SetParent(transform, false);

            Vector3 chunkWorldOrigin =
                coordinate.GetWorldOrigin(settings.ChunkWorldSize);

            chunkObject.transform.localPosition = chunkWorldOrigin;

            var meshFilter = chunkObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = chunkObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = previewMaterial;

            if (renderGeneratedSpawns && previewSpawnCatalog != null)
            {
                ChunkSpawnPresenter.Populate(
                    chunkObject.transform,
                    chunkData,
                    previewSpawnCatalog,
                    chunkWorldOrigin);
            }

            previewObjects.Add(chunkObject);
        }
    }
}
