using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [CreateAssetMenu(
        fileName = "WorldGenerationSettings",
        menuName = "Little Castle/World/World Generation Settings")]
    public sealed class WorldGenerationSettings : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip(
            "Stable logical profile ID used by saves/tools. " +
            "Do not change casually once persistent worlds exist.")]
        [SerializeField] private string profileId = "main_world";

        [Min(1)]
        [SerializeField] private int generationVersion = 1;

        [Header("Chunk")]
        [Min(1)]
        [SerializeField] private int cellsPerSide = 32;

        [Min(1f)]
        [SerializeField] private float chunkWorldSize = 64f;

        [Header("Pipeline")]
        [Tooltip("Stages run in this exact order.")]
        [SerializeField]
        private List<WorldGenerationStage> stages =
            new List<WorldGenerationStage>();

        public string ProfileId =>
            string.IsNullOrWhiteSpace(profileId)
                ? "main_world"
                : profileId;

        public int GenerationVersion =>
            Mathf.Max(1, generationVersion);

        public int CellsPerSide =>
            Mathf.Max(1, cellsPerSide);

        public float ChunkWorldSize =>
            Mathf.Max(1f, chunkWorldSize);

        public float CellWorldSize =>
            ChunkWorldSize / CellsPerSide;

        public IReadOnlyList<WorldGenerationStage> Stages =>
            stages;
    }
}
