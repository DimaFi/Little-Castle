using System;
using System.Collections.Generic;
using LittleCastle.World;
using LittleCastle.World.Environment;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Disposable resident presentation atlas. Authored fields remain in the map session;
    /// camera movement only changes this bounded GPU view, never the world or its geometry.
    /// </summary>
    public sealed class MapEnvironmentPresentation : IDisposable
    {
        const float ResidentSize = 256f;
        const float ResidentSnap = 32f;
        const int UploadTileSize = 16;
        static readonly int EnabledId = Shader.PropertyToID("_LC_EnvironmentEnabled");
        static readonly int TintId = Shader.PropertyToID("_LC_BiomeTint");
        static readonly int SnowId = Shader.PropertyToID("_LC_EnvironmentSnow");
        static readonly int BoundsId = Shader.PropertyToID("_LC_EnvironmentBounds");
        static readonly int SnowColorId = Shader.PropertyToID("_LC_EnvironmentSnowColor");
        static MapEnvironmentPresentation owner;

        readonly MapEditSession session;
        readonly int resolution;
        readonly int tileColumns;
        readonly Queue<int> dirtyTiles = new Queue<int>();
        readonly bool[] queued;
        readonly List<int> themeIds = new List<int>();
        readonly Color[] tileTint = new Color[UploadTileSize * UploadTileSize];
        readonly Color[] tileSnow = new Color[UploadTileSize * UploadTileSize];
        RenderTexture tintAtlas, snowAtlas;
        Texture2D tintUpload, snowUpload;
        Rect residentBounds;
        long observedRevision;
        bool refreshThemes = true;
        bool initialized;
        bool disposed;
        float weatherSeconds;
        float observedSnowAmount;
        bool observedWinterPreview;

        public Rect ResidentBounds => residentBounds;
        public Vector3 GroundFocus { get; private set; }
        public int PendingUploadTiles => dirtyTiles.Count;
        public int LastUploadPixelCount { get; private set; }

        public MapEnvironmentPresentation(MapEditSession session,
            BiomeThemeProfile[] profiles = null, int resolution = 256)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            // Whole tiles avoid partial staging resources at the right/top edge.
            this.resolution = Mathf.Clamp(Mathf.CeilToInt(resolution / 16f) * 16, 16, 256);
            tileColumns = this.resolution / UploadTileSize;
            queued = new bool[tileColumns * tileColumns];
            observedRevision = session.EnvironmentRevision;
            session.Changed += OnChanged;
            // Metadata overlay colours are intentionally not surface-albedo colours.
            // Future appearance profiles can replace the stable fallback multipliers below.
            owner = this;
            Shader.SetGlobalFloat(EnabledId, 0f);
        }

        public void Tick(Camera camera, float elapsedWorldSeconds = 0f)
        {
            if (disposed) return;
            LastUploadPixelCount = 0;
            Rect nextBounds = CalculateResidentBounds(camera);
            if (nextBounds.width <= 0f || nextBounds.height <= 0f)
            {
                if (owner == this) Shader.SetGlobalFloat(EnabledId, 0f);
                return;
            }

            bool shifted = !initialized || nextBounds != residentBounds;
            if (shifted) residentBounds = nextBounds;

            var settings = session.EnvironmentSettings;
            if (settings.simulateWeather && Finite(elapsedWorldSeconds) && elapsedWorldSeconds > 0f)
            {
                weatherSeconds += elapsedWorldSeconds;
                if (weatherSeconds >= .25f)
                {
                    session.AdvanceLocalSnow(weatherSeconds, residentBounds);
                    weatherSeconds = 0f;
                }
            }
            else if (!settings.simulateWeather) weatherSeconds = 0f;

            // Normal edits use bounded notifications. Revision fallback covers a complete
            // field restore; it also works when restore did not emit individual field events.
            if (observedRevision != session.EnvironmentRevision)
            {
                observedRevision = session.EnvironmentRevision;
                refreshThemes = true;
                MarkDirty(residentBounds);
            }
            if (observedSnowAmount != settings.snowAmount ||
                observedWinterPreview != settings.winterSnowPreview)
            {
                observedSnowAmount = settings.snowAmount;
                observedWinterPreview = settings.winterSnowPreview;
                MarkDirty(residentBounds);
            }

            if (refreshThemes)
            {
                themeIds.Clear();
                foreach (var layer in session.EnvironmentThemes.Capture())
                    if (layer.themeId > 0) themeIds.Add(layer.themeId);
                refreshThemes = false;
            }

            if (shifted)
            {
                EnsureAtlases();
                UploadFull();
                initialized = true;
            }
            else if (dirtyTiles.Count > 0)
            {
                // Bounded tiles keep local feedback responsive without a full-atlas upload.
                long start=System.Diagnostics.Stopwatch.GetTimestamp();
                for(int count=0;count<8&&dirtyTiles.Count>0;count++)
                {
                    int tile = dirtyTiles.Dequeue();
                    queued[tile] = false;
                    UploadTile(tile);
                    if((System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency>=2)break;
                }
            }
            Publish();
        }

        void OnChanged(MapRegionChange change)
        {
            if ((change.kind & (MapChangeKind.Biomes | MapChangeKind.Snow)) == 0) return;
            observedRevision = session.EnvironmentRevision;
            if ((change.kind & MapChangeKind.Biomes) != 0) refreshThemes = true;
            if (!initialized) return;
            // The field and atlas both interpolate: include the neighbour footprint.
            float pad = Mathf.Max(session.EnvironmentThemes.CellSize,
                session.EnvironmentSnow.CellSize) + Mathf.Max(residentBounds.width,
                residentBounds.height) / resolution;
            Rect area = change.bounds;
            area.xMin -= pad; area.yMin -= pad;
            area.xMax += pad; area.yMax += pad;
            MarkDirty(area);
        }

        Rect CalculateResidentBounds(Camera camera)
        {
            Vector4 map = session.Bounds;
            if (!Finite(map.x) || !Finite(map.y) || !Finite(map.z) || !Finite(map.w) ||
                map.z <= 0f || map.w <= 0f) return default;
            Vector2 focus = new Vector2(map.x + map.z * .5f, map.y + map.w * .5f);
            if (camera)
            {
                Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
                var ground = new Plane(Vector3.up, Vector3.zero);
                Vector3 point = ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : camera.transform.position;
                // Refine the focus against the existing composed height field, without
                // a Physics query, object scan or terrain materialization.
                for (int iteration = 0; iteration < 2; iteration++)
                {
                    float sampledHeight = session.SampleHeight(Mathf.Clamp(point.x, map.x, map.x + map.z),
                        Mathf.Clamp(point.z, map.y, map.y + map.w));
                    if (!Finite(sampledHeight)) break;
                    ground = new Plane(Vector3.up, new Vector3(0f, sampledHeight, 0f));
                    if (!ground.Raycast(ray, out distance)) break;
                    point = ray.GetPoint(distance);
                }
                focus = new Vector2(point.x, point.z);
            }
            focus.x = Mathf.Clamp(focus.x, map.x, map.x + map.z);
            focus.y = Mathf.Clamp(focus.y, map.y, map.y + map.w);
            GroundFocus = new Vector3(focus.x, session.SampleHeight(focus.x, focus.y), focus.y);
            float width = Mathf.Min(ResidentSize, map.z);
            float height = Mathf.Min(ResidentSize, map.w);
            float x = Mathf.Clamp(Mathf.Floor((focus.x - width * .5f) / ResidentSnap) * ResidentSnap,
                map.x, map.x + map.z - width);
            float z = Mathf.Clamp(Mathf.Floor((focus.y - height * .5f) / ResidentSnap) * ResidentSnap,
                map.y, map.y + map.w - height);
            return new Rect(x, z, width, height);
        }

        void MarkDirty(Rect area)
        {
            if (residentBounds.width <= 0f || residentBounds.height <= 0f || !area.Overlaps(residentBounds)) return;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((area.xMin - residentBounds.xMin) / residentBounds.width * resolution / UploadTileSize), 0, tileColumns - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((area.yMin - residentBounds.yMin) / residentBounds.height * resolution / UploadTileSize), 0, tileColumns - 1);
            int x1 = Mathf.Clamp(Mathf.FloorToInt((area.xMax - residentBounds.xMin) / residentBounds.width * resolution / UploadTileSize), 0, tileColumns - 1);
            int z1 = Mathf.Clamp(Mathf.FloorToInt((area.yMax - residentBounds.yMin) / residentBounds.height * resolution / UploadTileSize), 0, tileColumns - 1);
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                int key = z * tileColumns + x;
                if (queued[key]) continue;
                queued[key] = true;
                dirtyTiles.Enqueue(key);
            }
        }

        void EnsureAtlases()
        {
            if (tintAtlas) return;
            tintAtlas = CreateAtlas("Resident biome tint");
            snowAtlas = CreateAtlas("Resident thin snow");
            tintUpload = CreateUpload("Biome upload tile", UploadTileSize);
            snowUpload = CreateUpload("Snow upload tile", UploadTileSize);
        }

        RenderTexture CreateAtlas(string name)
        {
            var texture = new RenderTexture(resolution, resolution, 0,
                RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = name, hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                useMipMap = false, autoGenerateMips = false
            };
            texture.Create();
            return texture;
        }

        static Texture2D CreateUpload(string name, int size) => new Texture2D(size, size,
            TextureFormat.RGBAHalf, false, true)
        {
            name = name, hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };

        void Sample(int pixelX, int pixelZ, out Color tint, out Color snow)
        {
            float x = residentBounds.xMin + (pixelX + .5f) / resolution * residentBounds.width;
            float z = residentBounds.yMin + (pixelZ + .5f) / resolution * residentBounds.height;
            tint = Color.white;
            foreach (int id in themeIds)
            {
                float weight = session.EnvironmentThemes.Sample(id, x, z);
                tint += (ThemeTint(id) - Color.white) * weight;
            }
            float coverage = session.SampleEnvironmentSnow(x,z);
            snow = new Color(coverage, 0f, 0f, 1f);
        }

        // Multipliers are already linear ratios, not sRGB overlay colours.
        static Color ThemeTint(int id)
        {
            switch (id)
            {
                case EnvironmentThemeIds.EnchantedForest: return new Color(.88f, .78f, 1.15f, 1f);
                case EnvironmentThemeIds.ElvenForest: return new Color(.88f, 1.10f, .93f, 1f);
                case EnvironmentThemeIds.Winter: return new Color(.86f, .94f, 1.08f, 1f);
                default: return Color.white;
            }
        }

        void UploadFull()
        {
            // Full resident uploads happen only on creation or a snapped camera shift.
            var tint = new Color[resolution * resolution];
            var snow = new Color[tint.Length];
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
                Sample(x, z, out tint[z * resolution + x], out snow[z * resolution + x]);
            Texture2D fullTint = CreateUpload("Initial biome upload", resolution);
            Texture2D fullSnow = CreateUpload("Initial snow upload", resolution);
            try
            {
                fullTint.SetPixels(tint); fullTint.Apply(false, false);
                fullSnow.SetPixels(snow); fullSnow.Apply(false, false);
                Graphics.CopyTexture(fullTint, tintAtlas);
                Graphics.CopyTexture(fullSnow, snowAtlas);
            }
            finally { Destroy(fullTint); Destroy(fullSnow); }
            dirtyTiles.Clear();
            Array.Clear(queued, 0, queued.Length);
            LastUploadPixelCount = tint.Length;
        }

        void UploadTile(int tile)
        {
            int originX = tile % tileColumns * UploadTileSize;
            int originZ = tile / tileColumns * UploadTileSize;
            for (int z = 0; z < UploadTileSize; z++)
            for (int x = 0; x < UploadTileSize; x++)
                Sample(originX + x, originZ + z, out tileTint[z * UploadTileSize + x],
                    out tileSnow[z * UploadTileSize + x]);
            tintUpload.SetPixels(tileTint); tintUpload.Apply(false, false);
            snowUpload.SetPixels(tileSnow); snowUpload.Apply(false, false);
            Graphics.CopyTexture(tintUpload, 0, 0, 0, 0, UploadTileSize, UploadTileSize,
                tintAtlas, 0, 0, originX, originZ);
            Graphics.CopyTexture(snowUpload, 0, 0, 0, 0, UploadTileSize, UploadTileSize,
                snowAtlas, 0, 0, originX, originZ);
            LastUploadPixelCount += UploadTileSize * UploadTileSize;
        }

        void Publish()
        {
            if (owner != this) return;
            Shader.SetGlobalTexture(TintId, tintAtlas);
            Shader.SetGlobalTexture(SnowId, snowAtlas);
            Shader.SetGlobalVector(BoundsId, new Vector4(residentBounds.xMin,
                residentBounds.yMin, 1f / residentBounds.width, 1f / residentBounds.height));
            Shader.SetGlobalVector(SnowColorId, new Vector4(.91f, .95f, 1f, 1f));
            Shader.SetGlobalFloat(EnabledId, 1f);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            session.Changed -= OnChanged;
            if (owner == this)
            {
                owner = null;
                Shader.SetGlobalFloat(EnabledId, 0f);
                Shader.SetGlobalTexture(TintId, null);
                Shader.SetGlobalTexture(SnowId, null);
            }
            if (tintAtlas) { tintAtlas.Release(); Destroy(tintAtlas); }
            if (snowAtlas) { snowAtlas.Release(); Destroy(snowAtlas); }
            Destroy(tintUpload); Destroy(snowUpload);
            dirtyTiles.Clear();
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void Destroy(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
