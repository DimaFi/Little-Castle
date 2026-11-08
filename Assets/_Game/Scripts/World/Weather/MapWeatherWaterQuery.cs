using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Derived weather sampling of the existing authored river paths.
    /// Read-only and chunk-safe: no additional water surface or runtime map mutation.
    /// </summary>
    public sealed partial class MapEditSession
    {
        internal bool WeatherPuddleOverlapsRiver(Vector2 point, float radius)
        {
            var area = new Rect(point - Vector2.one * radius, Vector2.one * (radius * 2f));
            foreach (var cell in Regions(area))
            {
                if (!riverIndex.TryGetValue(cell, out var paths)) continue;
                foreach (long id in paths)
                {
                    if (!rivers[id].Nearest(point, out MapRiverFrame frame, out _)) continue;
                    // Older wide ribbons also draw through part of the authored bank transition.
                    float shoulder = frame.layoutVersion == 2
                        ? Mathf.Clamp(Mathf.Max(frame.leftTransition, frame.rightTransition) * .3f, .6f, 3f)
                        : 0f;
                    if (Vector2.Distance(point, frame.center) <= frame.width * .5f + shoulder + radius)
                        return true;
                }
            }
            return false;
        }
    }
}
