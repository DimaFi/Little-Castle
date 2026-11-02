using UnityEngine;

namespace LittleCastle.World.Weather
{
    /// <summary>
    /// One small camera-resident, CPU-rasterized RGBA32 shelter footprint.
    /// R is smooth protection; G is roof height. Shared by wet materials and rain particles.
    /// We only rasterize projected rectangles of nearby prefab markers, not the whole map.
    /// </summary>
    public sealed class RainShelterMask : System.IDisposable
    {
        public const int Resolution = 128;
        private const float WorldSize = 152f;
        private const float RefreshSeconds = .75f;
        private const float MoveStep = 2f;
        private static readonly int MapId = Shader.PropertyToID("_LC_RainShelterMap");
        private static readonly int EnabledId = Shader.PropertyToID("_LC_RainShelterEnabled");
        private static readonly int BoundsId = Shader.PropertyToID("_LC_RainShelterBounds");
        private static readonly int HeightsId = Shader.PropertyToID("_LC_RainShelterHeightRange");

        private Texture2D texture;
        private Color32[] pixels;
        private float x0, z0, y0;
        private Vector2 previousFocus = new Vector2(float.MaxValue, float.MaxValue);
        private float previousHeight = float.MaxValue;
        private float nextRefresh;
        private bool hasAny;
        private const float HeightRange = 160f;

        public static void ResetGlobals()
        {
            Shader.SetGlobalFloat(EnabledId, 0);
            Shader.SetGlobalTexture(MapId, Texture2D.blackTexture);
        }

        public void Tick(Vector3 focus)
        {
            var sources = RainShelterVolume.Active;
            if (sources.Count == 0)
            {
                Shader.SetGlobalFloat(EnabledId, 0f);
                hasAny = false;
                return;
            }
            float anchorX = Mathf.Floor(focus.x / MoveStep) * MoveStep;
            float anchorZ = Mathf.Floor(focus.z / MoveStep) * MoveStep;
            if (Time.unscaledTime < nextRefresh &&
                Mathf.Abs(anchorX - previousFocus.x) < .001f &&
                Mathf.Abs(anchorZ - previousFocus.y) < .001f &&
                Mathf.Abs(focus.y - previousHeight) < 3f)
            {
                Shader.SetGlobalFloat(EnabledId, hasAny ? 1f : 0f);
                return;
            }
            if (!texture)
            {
                texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true)
                {
                    name = "Little Castle Rain Shelter (temporary derived appearance)",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                pixels = new Color32[Resolution * Resolution];
            }
            previousFocus = new Vector2(anchorX, anchorZ);
            previousHeight = focus.y;
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            x0 = anchorX - WorldSize * .5f;
            z0 = anchorZ - WorldSize * .5f;
            y0 = focus.y - 40f;

            System.Array.Clear(pixels, 0, pixels.Length);
            bool occupied = false;
            float metresPerPixel = WorldSize / Resolution;
            for (int i = 0; i < sources.Count; i++)
            {
                RainShelterVolume marker = sources[i];
                if (!marker || !marker.isActiveAndEnabled) continue;
                var s = marker.WorldShape;
                float reachX = Mathf.Abs(s.right.x) * s.halfSize.x + Mathf.Abs(s.forward.x) * s.halfSize.y;
                float reachZ = Mathf.Abs(s.right.y) * s.halfSize.x + Mathf.Abs(s.forward.y) * s.halfSize.y;
                if (s.center.x + reachX < x0 || s.center.x - reachX > x0 + WorldSize ||
                    s.center.y + reachZ < z0 || s.center.y - reachZ > z0 + WorldSize)
                    continue;

                int minX = Mathf.Clamp(Mathf.FloorToInt((s.center.x - reachX - x0) / metresPerPixel), 0, Resolution - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt((s.center.x + reachX - x0) / metresPerPixel), 0, Resolution - 1);
                int minZ = Mathf.Clamp(Mathf.FloorToInt((s.center.y - reachZ - z0) / metresPerPixel), 0, Resolution - 1);
                int maxZ = Mathf.Clamp(Mathf.CeilToInt((s.center.y + reachZ - z0) / metresPerPixel), 0, Resolution - 1);
                byte encodedRoof = (byte)Mathf.RoundToInt(Mathf.Clamp01((s.roofY - y0) / HeightRange) * 255f);
                for (int z = minZ; z <= maxZ; z++)
                {
                    float worldZ = z0 + (z + .5f) * metresPerPixel;
                    for (int x = minX; x <= maxX; x++)
                    {
                        float coverage = RainShelterVolume.HorizontalCoverage(x0 + (x + .5f) * metresPerPixel, worldZ, s);
                        if (coverage <= .001f) continue;
                        int at = z * Resolution + x;
                        byte weight = (byte)Mathf.RoundToInt(255f * coverage);
                        if (weight <= pixels[at].r) continue;
                        pixels[at] = new Color32(weight, encodedRoof, 0, 255);
                        occupied = true;
                    }
                }
            }
            hasAny = occupied;
            if (!occupied)
            {
                Shader.SetGlobalFloat(EnabledId, 0f);
                return;
            }
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, false);
            Shader.SetGlobalTexture(MapId, texture);
            Shader.SetGlobalVector(BoundsId, new Vector4(x0, z0, 1f / WorldSize, 1f / WorldSize));
            Shader.SetGlobalVector(HeightsId, new Vector4(y0, HeightRange, 0f, 0f));
            Shader.SetGlobalFloat(EnabledId, 1f);
        }

        public void Dispose()
        {
            ResetGlobals();
            if (texture)
            {
                if (Application.isPlaying) Object.Destroy(texture);
                else Object.DestroyImmediate(texture);
            }
            texture = null;
            pixels = null;
            hasAny = false;
        }
    }
}
