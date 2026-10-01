using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed class WorldGenerator : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private WorldDefinition worldDefinition;

        [Header("Preview")]
        [Min(0)]
        [SerializeField] private int previewRadius = 1;

        [SerializeField] private Material previewMaterial;
        [SerializeField] private bool renderGeneratedSpawns = true;
        [SerializeField] private bool generateOnStart = true;

        private readonly List<GameObject> previewObjects =
            new List<GameObject>();

        public int WorldSeed => worldSeed;
        public WorldDefinition Definition => worldDefinition;

        private WorldGenerationSettings GenerationSettings =>
            worldDefinition != null
                ? worldDefinition.GenerationSettings
                : null;

        private MacroWorldPlannerSettings MacroSettings =>
            worldDefinition != null
                ? worldDefinition.MacroPlannerSettings
                : null;

        private WorldSpawnCatalog SpawnCatalog =>
            worldDefinition != null
                ? worldDefinition.SpawnCatalog
                : null;

        private void Start()
        {
            if (generateOnStart)
                GeneratePreview();
        }

        [ContextMenu("Generate Preview")]
        public void GeneratePreview()
        {
            ClearPreview();

            if (!TryCreatePipeline(
                    out WorldGenerationPipeline pipeline))
            {
                return;
            }

            var cache =
                new WorldChunkCache(
                    pipeline,
                    worldSeed);

            for (int z = -previewRadius;
                 z <= previewRadius;
                 z++)
            {
                for (int x = -previewRadius;
                     x <= previewRadius;
                     x++)
                {
                    CreatePreviewChunk(
                        cache,
                        new ChunkCoordinate(x, z));
                }
            }
        }

        [ContextMenu("Regenerate Preview (Next Seed)")]
        public void RegenerateWithNextSeed()
        {
            unchecked
            {
                worldSeed =
                    worldSeed * 1664525 +
                    1013904223;
            }

            GeneratePreview();
        }

        [ContextMenu("Validate World Configuration")]
        public void ValidateWorldConfiguration()
        {
            WorldConfigurationValidationReport report =
                WorldGenerationConfigurationValidator.Validate(
                    worldDefinition);

            if (report.IsValid)
            {
                if (report.Warnings.Count > 0)
                {
                    Debug.LogWarning(
                        report.ToMultilineString(),
                        this);
                }
                else
                {
                    Debug.Log(
                        report.ToMultilineString(),
                        this);
                }
            }
            else
            {
                Debug.LogError(
                    report.ToMultilineString(),
                    this);
            }
        }

        [ContextMenu("Run Generation Diagnostics")]
        public void RunGenerationDiagnostics()
        {
            if (!TryCreatePipeline(
                    out WorldGenerationPipeline pipeline))
            {
                return;
            }

            const float epsilon = 0.0001f;
            var origin =
                new ChunkCoordinate(0, 0);

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
            for (int i = previewObjects.Count - 1;
                 i >= 0;
                 i--)
            {
                GameObject previewObject =
                    previewObjects[i];

                if (previewObject == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(previewObject);
                else
                    DestroyImmediate(previewObject);
            }

            previewObjects.Clear();
        }

        private bool TryCreatePipeline(
            out WorldGenerationPipeline pipeline)
        {
            WorldGenerationSettings settings =
                GenerationSettings;

            if (worldDefinition == null)
            {
                Debug.LogError(
                    "WorldGenerator requires a WorldDefinition.",
                    this);

                pipeline = null;
                return false;
            }

            if (settings == null)
            {
                Debug.LogError(
                    "WorldDefinition requires WorldGenerationSettings.",
                    this);

                pipeline = null;
                return false;
            }

            MacroWorldPlan macroPlan =
                BuildPreviewMacroPlan();

            pipeline =
                new WorldGenerationPipeline(
                    settings,
                    macroPlan);

            return true;
        }

        private MacroWorldPlan BuildPreviewMacroPlan()
        {
            WorldGenerationSettings settings =
                GenerationSettings;

            MacroWorldPlannerSettings macroSettings =
                MacroSettings;

            if (macroSettings == null ||
                settings == null)
            {
                return null;
            }

            float chunkSize =
                settings.ChunkWorldSize;

            float min =
                -previewRadius *
                chunkSize;

            float size =
                (previewRadius * 2 + 1) *
                chunkSize;

            var bounds =
                new Rect(
                    min,
                    min,
                    size,
                    size);

            var terrainOnlyPipeline =
                new WorldGenerationPipeline(
                    settings,
                    null);

            var terrainProbe =
                new WorldTerrainProbe(
                    terrainOnlyPipeline,
                    settings,
                    worldSeed);

            var planner =
                new MacroWorldPlanner(
                    macroSettings);

            return
                planner.GenerateForBounds(
                    worldSeed,
                    bounds,
                    terrainProbe);
        }

        private void CreatePreviewChunk(
            WorldChunkCache cache,
            ChunkCoordinate coordinate)
        {
            WorldGenerationSettings settings =
                GenerationSettings;

            if (settings == null)
                return;

            WorldChunkData chunkData =
                cache.GetOrGenerate(
                    coordinate);

            Mesh mesh =
                ChunkMeshBuilder.Build(
                    chunkData,
                    settings.ChunkWorldSize);

            var chunkObject =
                new GameObject(
                    "Chunk_" +
                    coordinate.x +
                    "_" +
                    coordinate.z);

            chunkObject.transform.SetParent(
                transform,
                false);

            Vector3 chunkWorldOrigin =
                coordinate.GetWorldOrigin(
                    settings.ChunkWorldSize);

            chunkObject.transform.localPosition =
                chunkWorldOrigin;

            var meshFilter =
                chunkObject.AddComponent<MeshFilter>();

            meshFilter.sharedMesh = mesh;

            var meshRenderer =
                chunkObject.AddComponent<MeshRenderer>();

            meshRenderer.sharedMaterial =
                previewMaterial;

            WorldSpawnCatalog catalog =
                SpawnCatalog;

            if (renderGeneratedSpawns &&
                catalog != null)
            {
                ChunkSpawnPresenter.Populate(
                    chunkObject.transform,
                    chunkData,
                    catalog,
                    chunkWorldOrigin);
            }

            previewObjects.Add(
                chunkObject);
        }
    }
}
