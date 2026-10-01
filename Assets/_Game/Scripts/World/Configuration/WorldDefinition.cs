using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Root configuration asset for one generated world profile.
    ///
    /// Scenes should normally reference this asset instead of independently
    /// wiring every generation/presentation settings object.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldDefinition",
        menuName = "Little Castle/World/World Definition")]
    public sealed class WorldDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string worldId = "little_castle_main";

        [Header("Generation")]
        [SerializeField]
        private WorldGenerationSettings generationSettings;

        [SerializeField]
        private MacroWorldPlannerSettings macroPlannerSettings;

        [Header("Presentation")]
        [SerializeField]
        private WorldSpawnCatalog spawnCatalog;

        public string WorldId =>
            string.IsNullOrWhiteSpace(worldId)
                ? "little_castle_main"
                : worldId;

        public WorldGenerationSettings GenerationSettings =>
            generationSettings;

        public MacroWorldPlannerSettings MacroPlannerSettings =>
            macroPlannerSettings;

        public WorldSpawnCatalog SpawnCatalog =>
            spawnCatalog;
    }
}
