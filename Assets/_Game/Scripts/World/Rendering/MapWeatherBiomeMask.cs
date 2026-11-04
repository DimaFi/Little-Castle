using System;
using LittleCastle.World;
using LittleCastle.World.Environment;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// One bounded, reusable world-space winter coverage map. Snow and rain use identical
    /// per-pixel masks; this changes only appearance, never the authored map or save.
    /// </summary>
    public sealed class MapWeatherBiomeMask : IDisposable
    {
        const int Resolution = 64;
        const float WorldSize = 256f;
        const float HalfSize = WorldSize * .5f;
        static readonly int MapId = Shader.PropertyToID("_LC_WeatherBiomeMap");
        static readonly int BoundsId = Shader.PropertyToID("_LC_WeatherBiomeBounds");
        static readonly int EnabledId = Shader.PropertyToID("_LC_WeatherBiomeEnabled");
        readonly MapEditSession session;
        readonly Texture2D texture;
        readonly Color32[] pixels = new Color32[Resolution * Resolution];
        Vector2 centerCached;
        float nextUpdate;
        bool filled;
        public float VisibleWinterCoverage { get; private set; }

        public MapWeatherBiomeMask(MapEditSession session)
        {
            this.session = session;
            texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true)
            {
                name = "Local weather winter mask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Shader.SetGlobalFloat(EnabledId, 0f);
        }

        public static float SnowWeight(MapEditSession map, float x, float z)
        {
            if (map == null) return 0f;
            float winter = map.EnvironmentThemes.Sample(EnvironmentThemeIds.Winter, x, z);
            float snow = map.SampleEnvironmentSnow(x, z);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Max(winter, snow)));
        }

        public float NearbyWinter(Vector3 focus)
        {
            if (session == null) return 0f;
            float highest = 0f;
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                    highest = Mathf.Max(highest, SnowWeight(session, focus.x + x * 20f, focus.z + z * 20f));
            return highest;
        }

        public void Tick(Vector3 focus)
        {
            if (session == null) return;
            Vector2 center = new Vector2(Mathf.Round(focus.x / 2f) * 2f, Mathf.Round(focus.z / 2f) * 2f);
            // Cap uploads even if the winter revision advances every simulation frame.
            if (!filled || (center - centerCached).sqrMagnitude > 144f || Time.time >= nextUpdate)
            {
                centerCached = center;
                nextUpdate = Time.time + .65f;
                float highest = 0f;
                float unit = WorldSize / Resolution;
                float minX = center.x - HalfSize, minZ = center.y - HalfSize;
                for (int z = 0; z < Resolution; z++)
                {
                    float wz = minZ + (z + .5f) * unit;
                    for (int x = 0; x < Resolution; x++)
                    {
                        float wx = minX + (x + .5f) * unit;
                        byte value = (byte)Mathf.RoundToInt(255f * SnowWeight(session, wx, wz));
                        pixels[z * Resolution + x] = new Color32(value, 0, 0, 255);
                        highest = Mathf.Max(highest, value / 255f);
                    }
                }
                VisibleWinterCoverage = highest;
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                filled = true;
            }
            Shader.SetGlobalTexture(MapId, texture);
            Shader.SetGlobalVector(BoundsId, new Vector4(centerCached.x - HalfSize,
                centerCached.y - HalfSize, 1f / WorldSize, 1f / WorldSize));
            Shader.SetGlobalFloat(EnabledId, 1f);
        }

        public void Dispose()
        {
            Shader.SetGlobalFloat(EnabledId, 0f);
            Shader.SetGlobalTexture(MapId, Texture2D.blackTexture);
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
