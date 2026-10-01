using UnityEditor;

namespace LittleCastle.Editor
{
    public static class ReadyModelCatalogSync
    {
        public const string ReadyRoot = "Assets/_Game/Models/Ready";
        public const string SpawnCatalogPath = "Assets/_Game/Settings/World/MainWorldSpawnCatalog.asset";

        [MenuItem("Little Castle/Assets/Sync Ready Models")]
        public static void Sync()
        {
            UnityEngine.Debug.Log("Ready model sync scaffold is installed.");
        }
    }
}
