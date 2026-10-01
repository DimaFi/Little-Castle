using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Scene-side handle that links a rendered object back to authoritative
    /// generated world identity.
    ///
    /// Runtime gameplay may use StableId to record deltas such as a chopped tree.
    /// </summary>
    public sealed class GeneratedWorldObject : MonoBehaviour
    {
        [SerializeField] private long stableId;
        [SerializeField] private string archetypeId;
        [SerializeField] private SpawnCategory category;

        public long StableId => stableId;
        public string ArchetypeId => archetypeId;
        public SpawnCategory Category => category;

        public void Initialize(WorldSpawnData data)
        {
            stableId = data.stableId;
            archetypeId = data.archetypeId;
            category = data.category;
        }
    }
}
