using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Simple GameObject presenter for generated + runtime authoritative spawns.
    ///
    /// Deterministic base objects come from WorldChunkData.
    /// Server/runtime changes come from WorldRuntimeDeltaState.
    ///
    /// Large forests/grass should later use pooling/batching/GPU instancing
    /// without changing either data contract.
    /// </summary>
    public static class ChunkSpawnPresenter
    {
        public static int Populate(
            Transform chunkRoot,
            WorldChunkData chunk,
            WorldSpawnCatalog catalog,
            Vector3 chunkWorldOrigin,
            WorldRuntimeDeltaState runtimeDelta = null)
        {
            if (chunkRoot == null ||
                chunk == null ||
                catalog == null)
            {
                return 0;
            }

            int created = 0;

            for (int i = 0;
                 i < chunk.Spawns.Count;
                 i++)
            {
                WorldSpawnData spawn =
                    chunk.Spawns[i];

                if (runtimeDelta != null &&
                    runtimeDelta.IsSpawnRemoved(
                        spawn.stableId))
                {
                    continue;
                }

                if (TryCreateSpawn(
                        chunkRoot,
                        catalog,
                        chunkWorldOrigin,
                        spawn,
                        out GameObject ignored))
                {
                    created++;
                }
            }

            if (runtimeDelta != null)
            {
                var runtimeEntities =
                    new List<WorldRuntimeEntityState>();

                runtimeDelta.GetRuntimeEntitiesForChunk(
                    chunk.Coordinate,
                    runtimeEntities);

                for (int i = 0;
                     i < runtimeEntities.Count;
                     i++)
                {
                    WorldSpawnData runtimeSpawn =
                        runtimeEntities[i]
                            .ToSpawnData();

                    if (TryCreateSpawn(
                            chunkRoot,
                            catalog,
                            chunkWorldOrigin,
                            runtimeSpawn,
                            out GameObject ignored))
                    {
                        created++;
                    }
                }
            }

            return created;
        }

        private static bool TryCreateSpawn(
            Transform chunkRoot,
            WorldSpawnCatalog catalog,
            Vector3 chunkWorldOrigin,
            WorldSpawnData spawn,
            out GameObject instance)
        {
            instance = null;

            if (!catalog.TryResolve(
                    spawn.archetypeId,
                    spawn.stableId,
                    out WorldSpawnCatalogEntry entry,
                    out GameObject prefab))
            {
                return false;
            }

            instance =
                Object.Instantiate(
                    prefab,
                    chunkRoot);

            instance.name =
                spawn.archetypeId +
                "_" +
                spawn.stableId;

            instance.transform.localPosition =
                spawn.worldPosition -
                chunkWorldOrigin;

            Quaternion generatedRotation =
                Quaternion.Euler(
                    0f,
                    spawn.yawDegrees,
                    0f);

            instance.transform.localRotation =
                generatedRotation *
                Quaternion.Euler(
                    entry.rotationOffsetEuler);

            float scale =
                spawn.uniformScale *
                Mathf.Max(
                    0.01f,
                    entry.scaleMultiplier);

            instance.transform.localScale =
                instance.transform.localScale *
                scale;

            GeneratedWorldObject handle =
                instance.GetComponent<
                    GeneratedWorldObject>();

            if (handle == null)
            {
                handle =
                    instance.AddComponent<
                        GeneratedWorldObject>();
            }

            handle.Initialize(
                spawn);

            return true;
        }
    }
}
