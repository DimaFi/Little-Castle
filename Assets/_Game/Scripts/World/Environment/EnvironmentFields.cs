using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World.Environment
{
    // Intentionally separate from World.BiomeKind: ecological generator data is immutable.
    // Zero is the implicit summer background; additional theme IDs are data, not hardcoded shaders.
    public static class EnvironmentThemeIds
    {
        public const int Summer = 0;
        public const int EnchantedForest = 1;
        public const int ElvenForest = 2;
        public const int Winter = 3;
    }

    [Serializable]
    public sealed class ScalarTileSnapshot
    {
        public int tileX;
        public int tileZ;
        public float[] values;
    }

    [Serializable]
    public sealed class ThemeLayerSnapshot
    {
        public int themeId;
        public List<ScalarTileSnapshot> tiles = new List<ScalarTileSnapshot>();
    }

    [Serializable]
    public sealed class EnvironmentFieldSnapshot
    {
        public int version = 1;
        public float cellSize;
        public int tileResolution;
        public List<ThemeLayerSnapshot> themes = new List<ThemeLayerSnapshot>();
        public List<ScalarTileSnapshot> snow = new List<ScalarTileSnapshot>();
    }

    /// <summary>
    /// Sparse world-aligned float grid. Works with negative world coordinates and tile borders.
    /// Does not change existing generated chunks or persistent gameplay entities.
    /// </summary>
    public sealed class SparseWorldFloatField
    {
        private readonly Dictionary<ChunkCoordinate, float[]> tiles =
            new Dictionary<ChunkCoordinate, float[]>();

        public float CellSize { get; }
        public int TileResolution { get; }
        public bool IsEmpty => tiles.Count == 0;

        public SparseWorldFloatField(float cellSize, int tileResolution)
        {
            if (cellSize <= 0f || float.IsNaN(cellSize) || float.IsInfinity(cellSize))
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (tileResolution < 2 || tileResolution > 256)
                throw new ArgumentOutOfRangeException(nameof(tileResolution));
            CellSize = cellSize;
            TileResolution = tileResolution;
        }

        private ChunkCoordinate Key(int cellX, int cellZ)
        {
            return new ChunkCoordinate(
                Mathf.FloorToInt((float)cellX / TileResolution),
                Mathf.FloorToInt((float)cellZ / TileResolution));
        }

        private int Index(int cellX, int cellZ, ChunkCoordinate key)
        {
            int x = cellX - key.x * TileResolution;
            int z = cellZ - key.z * TileResolution;
            return z * TileResolution + x;
        }

        public float GetCell(int cellX, int cellZ)
        {
            var key = Key(cellX, cellZ);
            return tiles.TryGetValue(key, out var values)
                ? values[Index(cellX, cellZ, key)] : 0f;
        }

        public bool SetCell(int cellX, int cellZ, float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount));

            amount = Mathf.Clamp01(amount);
            var key = Key(cellX, cellZ);
            if (!tiles.TryGetValue(key, out var values))
            {
                if (amount <= 0f) return false;
                values = new float[TileResolution * TileResolution];
                tiles.Add(key, values);
            }

            int index = Index(cellX, cellZ, key);
            if (Mathf.Abs(values[index] - amount) < 0.00001f) return false;
            values[index] = amount;
            return true;
        }

        // Cell-centred bilinear filtering prevents hard seams at tile/chunk boundaries.
        public float Sample(float worldX, float worldZ)
        {
            float gx = worldX / CellSize - 0.5f;
            float gz = worldZ / CellSize - 0.5f;
            int x = Mathf.FloorToInt(gx);
            int z = Mathf.FloorToInt(gz);
            float tx = gx - x;
            float tz = gz - z;
            return Mathf.Lerp(
                Mathf.Lerp(GetCell(x, z), GetCell(x + 1, z), tx),
                Mathf.Lerp(GetCell(x, z + 1), GetCell(x + 1, z + 1), tx),
                tz);
        }

        public List<ScalarTileSnapshot> Capture()
        {
            var result = new List<ScalarTileSnapshot>(tiles.Count);
            foreach (var tile in tiles)
                result.Add(new ScalarTileSnapshot
                {
                    tileX = tile.Key.x,
                    tileZ = tile.Key.z,
                    values = (float[])tile.Value.Clone()
                });
            result.Sort((a, b) =>
            {
                int x = a.tileX.CompareTo(b.tileX);
                return x != 0 ? x : a.tileZ.CompareTo(b.tileZ);
            });
            return result;
        }

        public void Restore(List<ScalarTileSnapshot> snapshots)
        {
            var replacement = new Dictionary<ChunkCoordinate, float[]>();
            if (snapshots != null)
            {
                foreach (var tile in snapshots)
                {
                    if (tile == null || tile.values == null ||
                        tile.values.Length != TileResolution * TileResolution)
                        throw new ArgumentException("Invalid scalar field tile.");

                    var values = (float[])tile.values.Clone();
                    for (int i = 0; i < values.Length; i++)
                    {
                        if (float.IsNaN(values[i]) || float.IsInfinity(values[i]) ||
                            values[i] < 0f || values[i] > 1f)
                            throw new ArgumentException("Invalid scalar field value.");
                    }
                    replacement.Add(new ChunkCoordinate(tile.tileX, tile.tileZ), values);
                }
            }

            tiles.Clear();
            foreach (var tile in replacement)
                tiles.Add(tile.Key, tile.Value);
        }
    }

    /// <summary>
    /// Authored environment-theme overlay. Base ecology stays in the existing WorldChunkData.
    /// Each painting operation emits one bounded dirty region (not one event per cell).
    /// </summary>
    public sealed class BiomeInfluenceField
    {
        private readonly Dictionary<int, SparseWorldFloatField> layers =
            new Dictionary<int, SparseWorldFloatField>();

        public float CellSize { get; }
        public int TileResolution { get; }
        public uint Revision { get; private set; }
        public event Action<Rect> Changed;

        public BiomeInfluenceField(float cellSize, int tileResolution)
        {
            var validator = new SparseWorldFloatField(cellSize, tileResolution);
            CellSize = validator.CellSize;
            TileResolution = validator.TileResolution;
        }

        private SparseWorldFloatField Layer(int id)
        {
            if (!layers.TryGetValue(id, out var field))
            {
                field = new SparseWorldFloatField(CellSize, TileResolution);
                layers.Add(id, field);
            }
            return field;
        }

        public float Sample(int themeId, float worldX, float worldZ)
        {
            float total = 0f;
            float selected = 0f;
            foreach (var pair in layers)
            {
                float sample = pair.Value.Sample(worldX, worldZ);
                total += sample;
                if (pair.Key == themeId) selected = sample;
            }

            // Summer is the default where no explicit theme has been painted.
            if (themeId == EnvironmentThemeIds.Summer)
                return Mathf.Clamp01(1f - total);
            return total > 1f ? selected / total : selected;
        }

        public bool Paint(Vector2 center, float radius, int themeId, float opacity)
        {
            if (themeId < 0 || radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (float.IsNaN(opacity) || float.IsInfinity(opacity))
                throw new ArgumentOutOfRangeException(nameof(opacity));
            opacity = Mathf.Clamp01(opacity);
            if (opacity == 0f) return false;

            int left = Mathf.FloorToInt((center.x - radius) / CellSize - 0.5f);
            int right = Mathf.CeilToInt((center.x + radius) / CellSize - 0.5f);
            int bottom = Mathf.FloorToInt((center.y - radius) / CellSize - 0.5f);
            int top = Mathf.CeilToInt((center.y + radius) / CellSize - 0.5f);
            bool changed = false;

            for (int z = bottom; z <= top; z++)
            for (int x = left; x <= right; x++)
            {
                float dx = (x + 0.5f) * CellSize - center.x;
                float dz = (z + 0.5f) * CellSize - center.y;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance > radius) continue;

                float fade = 1f - Mathf.SmoothStep(0f, 1f, distance / radius);
                float t = opacity * fade;
                // Painting one theme gradually suppresses other influences.
                foreach (var other in layers)
                {
                    if (other.Key == themeId) continue;
                    float previous = other.Value.GetCell(x, z);
                    if (previous > 0f)
                        changed |= other.Value.SetCell(x, z, previous * (1f - t));
                }
                if (themeId != EnvironmentThemeIds.Summer)
                {
                    var field = Layer(themeId);
                    changed |= field.SetCell(x, z,
                        Mathf.Lerp(field.GetCell(x, z), 1f, t));
                }
            }

            if (changed)
            {
                Revision++;
                Changed?.Invoke(new Rect(
                    center.x - radius, center.y - radius, radius * 2f, radius * 2f));
            }
            return changed;
        }

        public List<ThemeLayerSnapshot> Capture()
        {
            var result = new List<ThemeLayerSnapshot>();
            foreach (var pair in layers)
                result.Add(new ThemeLayerSnapshot
                {
                    themeId = pair.Key,
                    tiles = pair.Value.Capture()
                });
            result.Sort((a, b) => a.themeId.CompareTo(b.themeId));
            return result;
        }

        public void Restore(List<ThemeLayerSnapshot> source)
        {
            var replacement = new Dictionary<int, SparseWorldFloatField>();
            if (source != null)
            {
                foreach (var layer in source)
                {
                    if (layer == null || layer.themeId <= 0)
                        throw new ArgumentException("Theme ID must be positive.");
                    var field = new SparseWorldFloatField(CellSize, TileResolution);
                    field.Restore(layer.tiles);
                    replacement.Add(layer.themeId, field);
                }
            }
            layers.Clear();
            foreach (var pair in replacement)
                layers.Add(pair.Key, pair.Value);
            Revision++;
            // Caller must invalidate the full displayed region after Restore.
        }
    }

    /// <summary>
    /// Independent thin-snow coverage: snowfall may stop while snow remains.
    /// No terrain vertex displacement, object replacement or per-footprint physics.
    /// </summary>
    public sealed class SnowAccumulationField
    {
        private readonly SparseWorldFloatField amounts;
        public event Action<Rect> Changed;
        public uint Revision { get; private set; }

        public SnowAccumulationField(float cellSize, int tileResolution)
        {
            amounts = new SparseWorldFloatField(cellSize, tileResolution);
        }

        public float Sample(float worldX, float worldZ) =>
            amounts.Sample(worldX, worldZ);

        public void Advance(Rect area, float dt, float snowRate, float meltRate,
            BiomeInfluenceField themes, int winterId)
        {
            if (themes == null) throw new ArgumentNullException(nameof(themes));
            if (dt <= 0f || area.width <= 0f || area.height <= 0f) return;
            if (float.IsNaN(dt) || float.IsInfinity(dt) ||
                float.IsNaN(snowRate) || float.IsInfinity(snowRate) ||
                float.IsNaN(meltRate) || float.IsInfinity(meltRate))
                throw new ArgumentException("Non-finite weather parameter.");

            snowRate = Mathf.Max(0f, snowRate);
            meltRate = Mathf.Max(0f, meltRate);
            if (snowRate <= 0f && (meltRate <= 0f || amounts.IsEmpty)) return;
            float size = amounts.CellSize;
            int left = Mathf.CeilToInt(area.xMin / size - 0.5f);
            int right = Mathf.FloorToInt(area.xMax / size - 0.5f);
            int bottom = Mathf.CeilToInt(area.yMin / size - 0.5f);
            int top = Mathf.FloorToInt(area.yMax / size - 0.5f);
            bool changed = false;

            for (int z = bottom; z <= top; z++)
            for (int x = left; x <= right; x++)
            {
                float worldX = (x + 0.5f) * size;
                float worldZ = (z + 0.5f) * size;
                float winter = themes.Sample(winterId, worldX, worldZ);
                float current = amounts.GetCell(x, z);
                float next = Mathf.Clamp01(current +
                    snowRate * winter * dt -
                    meltRate * (1f - 0.75f * winter) * dt);
                changed |= amounts.SetCell(x, z, next);
            }

            if (changed)
            {
                Revision++;
                Changed?.Invoke(area);
            }
        }

        public List<ScalarTileSnapshot> Capture() => amounts.Capture();

        public void Restore(List<ScalarTileSnapshot> data)
        {
            amounts.Restore(data);
            Revision++;
            // Caller must invalidate the full displayed region after Restore.
        }
    }
}
