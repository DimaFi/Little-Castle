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
    ///
    /// Runtime resolution is cached by archetype ID so dense chunks do not
    /// linearly scan the full catalog for every tree/rock/prop instance.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldSpawnCatalog",
        menuName = "Little Castle/World/World Spawn Catalog")]
    public sealed class WorldSpawnCatalog : ScriptableObject
    {
        private sealed class CachedEntry
        {
            public WorldSpawnCatalogEntry entry;
            public GameObject[] validPrefabs;
        }

        [SerializeField]
        private List<WorldSpawnCatalogEntry> entries =
            new List<WorldSpawnCatalogEntry>();

        [NonSerialized]
        private Dictionary<string, CachedEntry> runtimeLookup;

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

            EnsureRuntimeLookup();

            if (!runtimeLookup.TryGetValue(
                    archetypeId,
                    out CachedEntry cached) ||
                cached == null ||
                cached.validPrefabs == null ||
                cached.validPrefabs.Length == 0)
            {
                return false;
            }

            ulong positiveId =
                unchecked((ulong)stableId);

            int target =
                (int)(
                    positiveId %
                    (ulong)cached.validPrefabs.Length);

            entry = cached.entry;
            prefab = cached.validPrefabs[target];
            return prefab != null;
        }

        public bool ContainsArchetype(
            string archetypeId)
        {
            if (string.IsNullOrWhiteSpace(archetypeId))
                return false;

            EnsureRuntimeLookup();

            return
                runtimeLookup.ContainsKey(
                    archetypeId);
        }

        public int GetValidVariantCount(
            string archetypeId)
        {
            if (string.IsNullOrWhiteSpace(archetypeId))
                return 0;

            EnsureRuntimeLookup();

            return
                runtimeLookup.TryGetValue(
                    archetypeId,
                    out CachedEntry cached) &&
                cached != null &&
                cached.validPrefabs != null
                    ? cached.validPrefabs.Length
                    : 0;
        }

        public void InvalidateRuntimeCache()
        {
            runtimeLookup = null;
        }

        private void OnEnable()
        {
            InvalidateRuntimeCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            InvalidateRuntimeCache();
        }
#endif

        private void EnsureRuntimeLookup()
        {
            if (runtimeLookup != null)
                return;

            runtimeLookup =
                new Dictionary<string, CachedEntry>(
                    StringComparer.Ordinal);

            for (int i = 0; i < entries.Count; i++)
            {
                WorldSpawnCatalogEntry candidate =
                    entries[i];

                if (candidate == null ||
                    string.IsNullOrWhiteSpace(
                        candidate.archetypeId) ||
                    runtimeLookup.ContainsKey(
                        candidate.archetypeId) ||
                    candidate.prefabs == null ||
                    candidate.prefabs.Length == 0)
                {
                    continue;
                }

                int validCount = 0;

                for (int p = 0;
                     p < candidate.prefabs.Length;
                     p++)
                {
                    if (candidate.prefabs[p] != null)
                        validCount++;
                }

                if (validCount == 0)
                    continue;

                var validPrefabs =
                    new GameObject[validCount];

                int write = 0;

                for (int p = 0;
                     p < candidate.prefabs.Length;
                     p++)
                {
                    GameObject variant =
                        candidate.prefabs[p];

                    if (variant == null)
                        continue;

                    validPrefabs[write++] =
                        variant;
                }

                runtimeLookup.Add(
                    candidate.archetypeId,
                    new CachedEntry
                    {
                        entry = candidate,
                        validPrefabs = validPrefabs
                    });
            }
        }
    }
}
