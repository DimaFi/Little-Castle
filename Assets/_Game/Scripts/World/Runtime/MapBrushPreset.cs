using UnityEngine;

namespace LittleCastle.World
{
    [CreateAssetMenu(menuName="Little Castle/Map/Brush preset")]
    public sealed class MapBrushPreset : ScriptableObject
    {
        public string toolId="surface.meadow";
        public string pathMaterialId;
        public MapBrushSettings settings=new MapBrushSettings();
    }
}
