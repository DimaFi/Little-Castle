using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Host-selectable finite map preset.
    ///
    /// Exact map sizes are design/balance data, not hard-coded gameplay rules.
    /// A preset declares which player counts are allowed to use it.
    /// </summary>
    [Serializable]
    public sealed class WorldMapSizePreset
    {
        public string presetId = "medium";
        public string displayName = "Medium";

        [Header("Allowed session size")]
        [Min(1)]
        public int minimumPlayers = 1;

        [Tooltip("0 means no explicit maximum.")]
        [Min(0)]
        public int maximumPlayers = 0;

        [Header("Playable map")]
        [Min(4)]
        public int widthChunks = 32;

        [Min(4)]
        public int heightChunks = 32;

        [Header("Visual border")]
        [Tooltip(
            "Terrain-only padding generated outside gameplay bounds so the " +
            "player never sees the world end directly.")]
        [Min(0)]
        public int visualPaddingChunks = 3;

        public bool AllowsPlayerCount(int playerCount)
        {
            int safePlayers =
                Mathf.Max(
                    1,
                    playerCount);

            if (safePlayers <
                Mathf.Max(
                    1,
                    minimumPlayers))
            {
                return false;
            }

            return
                maximumPlayers <= 0 ||
                safePlayers <= maximumPlayers;
        }

        public int PlayableChunkCount =>
            Mathf.Max(4, widthChunks) *
            Mathf.Max(4, heightChunks);
    }

    /// <summary>
    /// Design-time catalog of host-selectable finite map sizes.
    ///
    /// Example design rule:
    /// a preset intended for 1-4 players can be forbidden for a 12-player
    /// session simply by setting its maximumPlayers to 4.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldMapRules",
        menuName = "Little Castle/World/World Map Rules")]
    public sealed class WorldMapRules : ScriptableObject
    {
        [SerializeField]
        private List<WorldMapSizePreset> presets =
            new List<WorldMapSizePreset>();

        public IReadOnlyList<WorldMapSizePreset> Presets =>
            presets;

        public bool TryResolvePreset(
            string presetId,
            int playerCount,
            out WorldMapSizePreset preset,
            out string error)
        {
            preset = null;
            error = null;

            if (string.IsNullOrWhiteSpace(
                    presetId))
            {
                error =
                    "Map size preset ID is empty.";

                return false;
            }

            for (int i = 0;
                 i < presets.Count;
                 i++)
            {
                WorldMapSizePreset candidate =
                    presets[i];

                if (candidate == null ||
                    candidate.presetId != presetId)
                {
                    continue;
                }

                if (!candidate.AllowsPlayerCount(
                        playerCount))
                {
                    error =
                        "Map preset '" +
                        presetId +
                        "' does not allow " +
                        playerCount +
                        " players.";

                    return false;
                }

                preset = candidate;
                return true;
            }

            error =
                "Unknown map size preset '" +
                presetId +
                "'.";

            return false;
        }

        public bool TryGetSmallestAllowedPreset(
            int playerCount,
            out WorldMapSizePreset preset)
        {
            preset = null;

            for (int i = 0;
                 i < presets.Count;
                 i++)
            {
                WorldMapSizePreset candidate =
                    presets[i];

                if (candidate == null ||
                    !candidate.AllowsPlayerCount(
                        playerCount))
                {
                    continue;
                }

                if (preset == null ||
                    candidate.PlayableChunkCount <
                    preset.PlayableChunkCount)
                {
                    preset = candidate;
                }
            }

            return preset != null;
        }
    }
}
