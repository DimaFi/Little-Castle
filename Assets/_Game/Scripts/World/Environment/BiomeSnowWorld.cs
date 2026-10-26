using System;
using UnityEngine;

namespace LittleCastle.World.Environment
{
    /// <summary>
    /// Non-destructive authored biome overlay and independent snowfall simulation.
    /// Editor tools and future server-authoritative gameplay can call the same API.
    /// This is intentionally not a replacement for the existing world generator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BiomeSnowWorld : MonoBehaviour
    {
        [Header("World-space storage")]
        [Min(0.1f)] [SerializeField] private float cellSize = 2f;
        [Range(8, 64)] [SerializeField] private int cellsPerTile = 32;
        [SerializeField] private Vector2 simulationOrigin = new Vector2(-128f, -128f);
        [SerializeField] private Vector2 simulationSize = new Vector2(256f, 256f);
        [Min(1)] [SerializeField] private int winterThemeId = EnvironmentThemeIds.Winter;

        [Header("Weather - cover is independent from biome membership")]
        [SerializeField] private bool simulateWeather = true;
        [Range(0f, 1f)] [SerializeField] private float snowfallIntensity = 0f;
        [Min(0f)] [SerializeField] private float accumulationPerSecond = 0.04f;
        [Min(0f)] [SerializeField] private float meltingPerSecond = 0.001f;
        [Range(0.05f, 2f)] [SerializeField] private float simulationInterval = 0.25f;

        private BiomeInfluenceField themes;
        private SnowAccumulationField snow;
        private float accumulatedTime;

        public BiomeInfluenceField Themes
        {
            get { EnsureInitialized(); return themes; }
        }

        public SnowAccumulationField Snow
        {
            get { EnsureInitialized(); return snow; }
        }

        public int WinterThemeId => winterThemeId;
        public Rect SimulationBounds => new Rect(simulationOrigin, simulationSize);
        public event Action<Rect> Changed;

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (themes != null) return;
            themes = new BiomeInfluenceField(Mathf.Max(0.1f, cellSize),
                Mathf.Clamp(cellsPerTile, 8, 64));
            snow = new SnowAccumulationField(themes.CellSize, themes.TileResolution);
            themes.Changed += OnFieldChanged;
            snow.Changed += OnFieldChanged;
        }

        private void OnFieldChanged(Rect dirtyBounds) => Changed?.Invoke(dirtyBounds);

        public bool PaintTheme(Vector3 worldPoint, float radius, int themeId, float opacity)
        {
            // Game commands and developer brushes can share this entry point.
            return Themes.Paint(new Vector2(worldPoint.x, worldPoint.z),
                radius, themeId, opacity);
        }

        public void SetSnowfall(float intensity) =>
            snowfallIntensity = Mathf.Clamp01(intensity);

        public void AdvanceWeather(float seconds)
        {
            if (seconds <= 0f) return;
            Snow.Advance(SimulationBounds, seconds,
                snowfallIntensity * accumulationPerSecond, meltingPerSecond,
                Themes, winterThemeId);
        }

        private void Update()
        {
            if (!simulateWeather) return;
            accumulatedTime += Time.deltaTime;
            if (accumulatedTime < simulationInterval) return;
            float delta = accumulatedTime;
            accumulatedTime = 0f;
            AdvanceWeather(delta);
        }

        public EnvironmentFieldSnapshot Capture()
        {
            return new EnvironmentFieldSnapshot
            {
                version = 1,
                cellSize = Themes.CellSize,
                tileResolution = Themes.TileResolution,
                themes = Themes.Capture(),
                snow = Snow.Capture()
            };
        }

        public string ExportJson() => JsonUtility.ToJson(Capture());

        public void ImportJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Empty environment snapshot.", nameof(json));
            var state = JsonUtility.FromJson<EnvironmentFieldSnapshot>(json);
            if (state == null || state.version != 1 ||
                Mathf.Abs(state.cellSize - Themes.CellSize) > 0.0001f ||
                state.tileResolution != Themes.TileResolution)
                throw new InvalidOperationException(
                    "Environment snapshot version/grid is incompatible.");

            // Stage BOTH fields before committing to avoid partially corrupt saves.
            var nextThemes = new BiomeInfluenceField(Themes.CellSize, Themes.TileResolution);
            var nextSnow = new SnowAccumulationField(SnowCellSize, Themes.TileResolution);
            nextThemes.Restore(state.themes);
            nextSnow.Restore(state.snow);
            themes.Changed -= OnFieldChanged;
            snow.Changed -= OnFieldChanged;
            themes = nextThemes;
            snow = nextSnow;
            themes.Changed += OnFieldChanged;
            snow.Changed += OnFieldChanged;
            Changed?.Invoke(SimulationBounds);
        }

        private float SnowCellSize => Themes.CellSize;

        private void OnDestroy()
        {
            if (themes != null) themes.Changed -= OnFieldChanged;
            if (snow != null) snow.Changed -= OnFieldChanged;
        }

        [ContextMenu("Paint winter at this GameObject (prototype)")]
        private void PaintWinterAtTransform() =>
            PaintTheme(transform.position, 15f, winterThemeId, 1f);

        [ContextMenu("Restore summer at this GameObject (prototype)")]
        private void RestoreSummerAtTransform() =>
            PaintTheme(transform.position, 15f, EnvironmentThemeIds.Summer, 1f);
    }
}
