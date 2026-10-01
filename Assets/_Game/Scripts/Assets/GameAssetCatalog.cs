using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Assets
{
    public enum GameAssetKind : byte
    {
        Unknown = 0,
        EnvironmentProp = 1,
        Tree = 2,
        Bush = 3,
        Rock = 4,
        ResourceVisual = 5,
        Structure = 6,
        Bridge = 7,
        Sign = 8,
        Ruin = 9,
        Landmark = 10,
        Decoration = 11
    }

    [Serializable]
    public sealed class GameAssetCatalogEntry
    {
        [Tooltip("Stable project-wide ID. Do not rename after saves/content reference it.")]
        public string assetId;

        [Tooltip(
            "Optional procedural-world archetype ID. When present, this asset " +
            "may also be registered in WorldSpawnCatalog.")]
        public string archetypeId;

        public GameAssetKind kind = GameAssetKind.EnvironmentProp;

        [Tooltip("Final project-owned prefab used by runtime systems.")]
        public GameObject prefab;

        [Tooltip("Optional human-readable tags such as forest, medieval, road.")]
        public string[] tags;

        [Min(0.01f)]
        public float nominalScale = 1f;

        [Tooltip(
            "Approximate authored bounds in local prefab space. Editor intake " +
            "fills this from renderers so placement/debug tools can reason about size.")]
        public Bounds localBounds;

        public bool proceduralSpawnAllowed = true;
    }

    /// <summary>
    /// Runtime-safe registry of final game prefabs.
    ///
    /// Raw FBX/OBJ import sources are deliberately not referenced here.
    /// This prevents editor-only source content from becoming the runtime source
    /// of truth and keeps generation dependent only on finalized prefabs.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameAssetCatalog",
        menuName = "Little Castle/Assets/Game Asset Catalog")]
    public sealed class GameAssetCatalog : ScriptableObject
    {
        [SerializeField]
        private List<GameAssetCatalogEntry> entries =
            new List<GameAssetCatalogEntry>();

        public IReadOnlyList<GameAssetCatalogEntry> Entries => entries;

        public bool TryGetByAssetId(
            string assetId,
            out GameAssetCatalogEntry entry)
        {
            entry = null;

            if (string.IsNullOrWhiteSpace(assetId))
                return false;

            for (int i = 0; i < entries.Count; i++)
            {
                GameAssetCatalogEntry candidate = entries[i];

                if (candidate != null &&
                    candidate.assetId == assetId &&
                    candidate.prefab != null)
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetByArchetypeId(
            string archetypeId,
            out GameAssetCatalogEntry entry)
        {
            entry = null;

            if (string.IsNullOrWhiteSpace(archetypeId))
                return false;

            for (int i = 0; i < entries.Count; i++)
            {
                GameAssetCatalogEntry candidate = entries[i];

                if (candidate != null &&
                    candidate.archetypeId == archetypeId &&
                    candidate.prefab != null)
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
