using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// One deterministic stage that modifies authoritative chunk data.
    /// Do not spawn visual GameObjects here.
    /// </summary>
    public abstract class WorldGenerationStage : ScriptableObject
    {
        [SerializeField] private bool enabledStage = true;

        public bool Enabled => enabledStage;

        public abstract void Generate(GenerationContext context, WorldChunkData chunk);
    }
}
