using UnityEngine;

namespace LittleCastle.World.Environment
{
    /// <summary>
    /// Bounded proof-of-concept: converts painted biome influence + accumulated snow
    /// into ONE world-space shader texture, so a whole fence/road can transition
    /// smoothly without swapping each module. Future large worlds need tiled streaming.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SnowMaskTextureBridge : MonoBehaviour
    {
        [SerializeField] private BiomeSnowWorld world;
        [Range(32, 512)] [SerializeField] private int textureResolution = 128;
        [Min(0.05f)] [SerializeField] private float refreshInterval = 0.2f;

        private Texture2D mask;
        private Color32[] pixels;
        private bool dirty = true;
        private float elapsed;

        private static readonly int SnowField = Shader.PropertyToID("_LC_SnowField");
        private static readonly int SnowRect = Shader.PropertyToID("_LC_SnowFieldRect");

        private void OnEnable()
        {
            if (world == null) world = GetComponent<BiomeSnowWorld>();
            if (world != null) world.Changed += OnWorldChanged;
            dirty = true;
        }

        private void OnWorldChanged(Rect bounds)
        {
            // Coalesce all brush/weather events and rebuild at a controlled rate.
            if (world == null || bounds.Overlaps(world.SimulationBounds))
                dirty = true;
        }

        private void LateUpdate()
        {
            if (world == null) return;
            elapsed += Time.deltaTime;
            if (!dirty || elapsed < refreshInterval) return;
            elapsed = 0f;
            Rebuild();
        }

        public void RefreshNow()
        {
            if (world == null) world = GetComponent<BiomeSnowWorld>();
            if (world != null) Rebuild();
        }

        private void Rebuild()
        {
            int res = Mathf.Clamp(textureResolution, 32, 512);
            if (mask == null || mask.width != res || mask.height != res)
            {
                Release();
                mask = new Texture2D(res, res, TextureFormat.RGBA32, false, true)
                {
                    name = "LC Snow/Biome World Mask (runtime)",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                pixels = new Color32[res * res];
            }

            Rect area = world.SimulationBounds;
            if (area.width <= 0f || area.height <= 0f)
            {
                Debug.LogWarning("Snow world simulation rectangle must have a positive size.", this);
                return;
            }

            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = area.xMin + (x + 0.5f) * area.width / res;
                float wz = area.yMin + (z + 0.5f) * area.height / res;
                byte snow = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(world.Snow.Sample(wx, wz)) * 255f);
                byte winter = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(world.Themes.Sample(world.WinterThemeId, wx, wz)) * 255f);
                pixels[z * res + x] = new Color32(snow, winter, 0, 255);
            }

            mask.SetPixels32(pixels);
            mask.Apply(false, false);
            Shader.SetGlobalTexture(SnowField, mask);
            Shader.SetGlobalVector(SnowRect, new Vector4(
                area.xMin, area.yMin, 1f / area.width, 1f / area.height));
            dirty = false;
        }

        private void OnDisable()
        {
            if (world != null) world.Changed -= OnWorldChanged;
            Shader.SetGlobalTexture(SnowField, Texture2D.blackTexture);
            Shader.SetGlobalVector(SnowRect, Vector4.zero);
            Release();
            dirty = true;
            elapsed = 0f;
        }

        private void Release()
        {
            if (mask != null)
            {
                if (Application.isPlaying) Destroy(mask);
                else DestroyImmediate(mask);
                mask = null;
            }
            pixels = null;
        }
    }
}
