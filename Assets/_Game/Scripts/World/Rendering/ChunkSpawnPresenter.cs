using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Simple GameObject presenter for generated spawn data.
    ///
    /// Suitable for early development and low/medium object counts.
    /// Large forests/grass should later use pooling, batching and/or GPU instancing
    /// without changing WorldSpawnData.
    /// </summary>
    public static class ChunkSpawnPresenter
    {
        public static int Populate(
            Transform chunkRoot,
            WorldChunkData chunk,
            WorldSpawnCatalog catalog,
            Vector3 chunkWorldOrigin)
        {
            if (chunkRoot == null || chunk == null || catalog == null)
                return 0;

            int created = 0;

            for (int i = 0; i < chunk.Spawns.Count; i++)
            {
                WorldSpawnData spawn = chunk.Spawns[i];

                if (!catalog.TryResolve(
                    spawn.archetypeId,
                    spawn.stableId,
                    out WorldSpawnCatalogEntry entry,
                    out GameObject prefab))
                {
                    continue;
                }

                GameObject instance =
                    Object.Instantiate(prefab, chunkRoot);

                instance.name =
                    spawn.archetypeId + "_" + spawn.stableId;

                instance.transform.localPosition =
                    spawn.worldPosition - chunkWorldOrigin;

                Quaternion generatedRotation =
                    Quaternion.Euler(0f, spawn.yawDegrees, 0f);

                instance.transform.localRotation =
                    generatedRotation *
                    Quaternion.Euler(entry.rotationOffsetEuler);

                float scale =
                    spawn.uniformScale *
                    Mathf.Max(0.01f, entry.scaleMultiplier);

                instance.transform.localScale =
                    instance.transform.localScale * scale;

                GeneratedWorldObject handle =
                    instance.GetComponent<GeneratedWorldObject>();

                if (handle == null)
                    handle = instance.AddComponent<GeneratedWorldObject>();

                handle.Initialize(spawn);
                created++;
            }

            return created;
        }
    }
}
