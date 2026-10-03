using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class WorldSpawnCatalogEntry
    {
        public string archetypeId;

        [Tooltip(
            "One or more visual variants for the same logical archetype. " +
            "A stable variant is selected from the generated object's stable ID.")]
        public GameObject[] prefabs;

        [Min(0.01f)]
        public float scaleMultiplier = 1f;

        public Vector3 rotationOffsetEuler;
    }

    /// <summary>
    /// Presentation-only mapping from generated archetype IDs to Unity prefabs.
    ///
    /// Replacing a prefab here changes visuals without changing generated
    /// world identity or save data.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldSpawnCatalog",
        menuName = "Little Castle/World/World Spawn Catalog")]
    public sealed class WorldSpawnCatalog : ScriptableObject
    {
#if UNITY_EDITOR
        public delegate bool LocalVariantResolver(
            WorldSpawnCatalog catalog,
            string archetypeId,
            long stableId,
            out WorldSpawnCatalogEntry entry,
            out GameObject prefab);

        // Editor-only local assets can override shared placeholder visuals.
        public static LocalVariantResolver EditorLocalResolver;
#endif

        [SerializeField]
        private List<WorldSpawnCatalogEntry> entries =
            new List<WorldSpawnCatalogEntry>();

        public IReadOnlyList<WorldSpawnCatalogEntry> Entries => entries;

        public bool TryResolve(
            string archetypeId,
            long stableId,
            out WorldSpawnCatalogEntry entry,
            out GameObject prefab)
        {
            entry = null;
            prefab = null;

            if (string.IsNullOrWhiteSpace(archetypeId))
                return false;

#if UNITY_EDITOR
            if (EditorLocalResolver != null &&
                EditorLocalResolver(this, archetypeId, stableId,
                    out entry, out prefab))
                return true;
#endif

            for (int i = 0; i < entries.Count; i++)
            {
                WorldSpawnCatalogEntry candidate = entries[i];

                if (candidate == null ||
                    candidate.archetypeId != archetypeId ||
                    candidate.prefabs == null ||
                    candidate.prefabs.Length == 0)
                {
                    continue;
                }

                int validCount = 0;

                for (int p = 0; p < candidate.prefabs.Length; p++)
                {
                    if (candidate.prefabs[p] != null)
                        validCount++;
                }

                if (validCount == 0)
                    return false;

                ulong positiveId = unchecked((ulong)stableId);
                int target = (int)(positiveId % (ulong)validCount);

                for (int p = 0; p < candidate.prefabs.Length; p++)
                {
                    GameObject variant = candidate.prefabs[p];

                    if (variant == null)
                        continue;

                    if (target == 0)
                    {
                        entry = candidate;
                        prefab = variant;
                        return true;
                    }

                    target--;
                }
            }

            return false;
        }
    }
}
