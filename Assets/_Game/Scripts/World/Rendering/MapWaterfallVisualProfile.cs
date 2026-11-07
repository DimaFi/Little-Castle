using UnityEngine;

namespace LittleCastle.Rendering
{
    [CreateAssetMenu(menuName = "Little Castle/Rendering/Connected waterfall visuals")]
    public sealed class MapWaterfallVisualProfile : ScriptableObject
    {
        public Material body, river, foam, spray;
        public RiverEffectsQuality sprayQuality=RiverEffectsQuality.Medium;
        [Min(10)] public float maximumDistance = 120;
        [Range(1, 8)] public int maximumResident = 4;
    }
}
