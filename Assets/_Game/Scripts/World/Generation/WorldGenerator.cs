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
        [SerializeField] private bool generateOnStart = true;

        private readonly List<GameObject> previewObjects = new();

        private void Start()
        {
            if (generateOnStart)
                GeneratePreview();
        }

        [ContextMenu("Generate Preview")]
        public void GeneratePreview()
        {
            ClearPreview();

            if (settings == null)
            {
                Debug.LogError("WorldGenerator requires WorldGenerationSettings.", this);
                return;
            }

            var pipeline = new WorldGenerationPipeline(settings);

            for (int z = -previewRadius; z <= previewRadius; z++)
            {
                for (int x = -previewRadius; x <= previewRadius; x++)
                    CreatePreviewChunk(pipeline, new ChunkCoordinate(x, z));
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

        private void CreatePreviewChunk(
            WorldGenerationPipeline pipeline,
            ChunkCoordinate coordinate)
        {
            WorldChunkData chunkData = pipeline.GenerateChunk(worldSeed, coordinate);
            Mesh mesh = ChunkMeshBuilder.Build(chunkData, settings.ChunkWorldSize);

            var chunkObject = new GameObject($"Chunk_{coordinate.x}_{coordinate.z}");
            chunkObject.transform.SetParent(transform, false);
            chunkObject.transform.localPosition =
                coordinate.GetWorldOrigin(settings.ChunkWorldSize);

            var meshFilter = chunkObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = chunkObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = previewMaterial;

            previewObjects.Add(chunkObject);
        }
    }
}
