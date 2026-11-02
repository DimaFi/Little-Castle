using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Derived weather sampling of the existing authored river paths.
    /// Read-only and chunk-safe: no additional water surface or runtime map mutation.
    /// </summary>
    public sealed partial class MapEditSession
    {
        public bool TrySampleWeatherRiverWater(float x, float z, out float height)
        {
            height = 0f;
            Vector2 point = new Vector2(x, z);
            foreach (MapRiverPath river in RiversAt(point))
            {
                if (!river.Nearest(point, out MapRiverFrame frame, out float signed))
                    continue;
                // Exclude shoreline transitions: ripples should stay inside the water corridor.
                if (Mathf.Abs(signed) >= Mathf.Max(0f, frame.width * .5f - .25f))
                    continue;
                height = frame.water;
                return true;
            }
            return false;
        }
    }
}
