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

        private readonly Dictionary<ChunkCoordinate, int> nonzeroCounts = new Dictionary<ChunkCoordinate, int>();
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
                FloorDivide(cellX, TileResolution),
                FloorDivide(cellZ, TileResolution));
        }

        public static int FloorDivide(int value, int divisor)
        {
            if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor));
            int quotient = value / divisor;
            return value % divisor < 0 ? quotient - 1 : quotient;
        }

        private int Index(int cellX, int cellZ, ChunkCoordinate key)
        {
            int x = (int)((long)cellX - (long)key.x * TileResolution);
            int z = (int)((long)cellZ - (long)key.z * TileResolution);
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
                nonzeroCounts.Add(key, 0);
            }

            int index = Index(cellX, cellZ, key);
            if (values[index] == amount) return false;
            int count = nonzeroCounts[key];
            if (values[index] == 0f) count++;
            if (amount == 0f) count--;
            values[index] = amount;
            if (count == 0) { tiles.Remove(key); nonzeroCounts.Remove(key); }
            else nonzeroCounts[key] = count;
            return true;
        }

        // Cell-centred bilinear filtering prevents hard seams at tile/chunk boundaries.
        public float Sample(float worldX, float worldZ)
        {
            double gx = (double)worldX / CellSize - 0.5;
            double gz = (double)worldZ / CellSize - 0.5;
            int x = EnvironmentFieldMath.CellIndex(gx, false);
            int z = EnvironmentFieldMath.CellIndex(gz, false);
            float tx = (float)(gx - x);
            float tz = (float)(gz - z);
            return Mathf.Lerp(
                Mathf.Lerp(GetCell(x, z), GetCell(x + 1, z), tx),
                Mathf.Lerp(GetCell(x, z + 1), GetCell(x + 1, z + 1), tx), tz);
        }

        /// <summary>Returns an owned clone, or null for an implicit zero tile.</summary>
        public ScalarTileSnapshot CaptureTile(int tileX, int tileZ)
        {
            return tiles.TryGetValue(new ChunkCoordinate(tileX, tileZ), out var values)
                ? new ScalarTileSnapshot { tileX = tileX, tileZ = tileZ,
                    values = (float[])values.Clone() } : null;
        }

        public void RestoreTile(int tileX, int tileZ, ScalarTileSnapshot source)
        {
            float[] replacement = null;
            if (source != null)
            {
                if (source.tileX != tileX || source.tileZ != tileZ)
                    throw new ArgumentException("Tile coordinate mismatch.");
                replacement = ValidateTile(source);
            }
            var key = new ChunkCoordinate(tileX, tileZ);
            int count = replacement == null ? 0 : Array.FindAll(replacement, value => value != 0f).Length;
            if (count == 0) { tiles.Remove(key); nonzeroCounts.Remove(key); }
            else { tiles[key] = replacement; nonzeroCounts[key] = count; }
        }

        private float[] ValidateTile(ScalarTileSnapshot tile)
        {
            if (tile == null || tile.values == null ||
                tile.values.Length != TileResolution * TileResolution)
                throw new ArgumentException("Invalid scalar field tile.");
            long left = (long)tile.tileX * TileResolution;
            long bottom = (long)tile.tileZ * TileResolution;
            if (left < int.MinValue || left + TileResolution - 1 > int.MaxValue ||
                bottom < int.MinValue || bottom + TileResolution - 1 > int.MaxValue)
                throw new ArgumentException("Tile exceeds supported cell coordinates.");
            var values = (float[])tile.values.Clone();
            foreach (float value in values)
                if (!EnvironmentFieldMath.Finite(value) || value < 0f || value > 1f)
                    throw new ArgumentException("Invalid scalar field value.");
            return values;
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
            var seen = new HashSet<ChunkCoordinate>();
            if (snapshots != null)
            {
                foreach (var tile in snapshots)
                {
                    float[] values = ValidateTile(tile);
                    var key = new ChunkCoordinate(tile.tileX, tile.tileZ);
                    if (!seen.Add(key)) throw new ArgumentException("Duplicate scalar tile.");
                    if (Array.Exists(values, value => value != 0f)) replacement.Add(key, values);
                }
            }

            tiles.Clear();
            nonzeroCounts.Clear();
            foreach (var tile in replacement)
            {
                tiles.Add(tile.Key, tile.Value);
                int count = 0;
                foreach (float value in tile.Value) if (value != 0f) count++;
                nonzeroCounts.Add(tile.Key, count);
            }
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
            if (themeId < 0) throw new ArgumentOutOfRangeException(nameof(themeId));
            EnvironmentFieldMath.CellIndex((double)worldX / CellSize - 0.5, false);
            EnvironmentFieldMath.CellIndex((double)worldZ / CellSize - 0.5, false);
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

        public float GetCell(int themeId, int cellX, int cellZ)
        {
            if (themeId < 0) throw new ArgumentOutOfRangeException(nameof(themeId));
            if (themeId != EnvironmentThemeIds.Summer)
                return layers.TryGetValue(themeId, out var layer) ? layer.GetCell(cellX, cellZ) : 0f;
            double total = 0;
            foreach (var pair in layers) total += pair.Value.GetCell(cellX, cellZ);
            return (float)Math.Max(0, 1 - total);
        }

        public bool Paint(Vector2 center, float radius, int themeId, float opacity) =>
            Paint(center, radius, themeId, opacity, 0f);

        public bool Paint(Vector2 center, float radius, int themeId, float opacity, float hardness) =>
            Paint(center, radius, themeId, opacity, hardness, null);

        public bool Paint(Vector2 center, float radius, int themeId, float opacity, float hardness,
            Rect? clipBounds)
        {
            ValidateBrush(center, radius, themeId, opacity, hardness);
            GetCells(center, radius, out int left, out int right, out int bottom, out int top, clipBounds);
            opacity = Mathf.Clamp01(opacity);
            hardness = Mathf.Clamp01(hardness);
            if (opacity == 0f || left > right || bottom > top) return false;
            var dirty = new EnvironmentDirtyCells(CellSize);
            var target = themeId == EnvironmentThemeIds.Summer ? null : Layer(themeId);
            for (int z = bottom; z <= top; z++)
            for (int x = left; x <= right; x++)
            {
                float strength = BrushWeight(center, radius, hardness, x, z) * opacity;
                if (strength <= 0f) continue;
                bool changed = false;
                foreach (var other in layers)
                {
                    if (other.Key == themeId) continue;
                    changed |= other.Value.SetCell(x, z, other.Value.GetCell(x, z) * (1f - strength));
                }
                if (target != null)
                    changed |= target.SetCell(x, z, Mathf.Lerp(target.GetCell(x, z), 1f, strength));
                if (changed) dirty.Add(x, z);
            }
            PruneLayers();
            BroadcastChanges(dirty);
            return dirty.HasChanges;
        }

        /// <summary>Locally smooths the complete normalized theme vector, including implicit
        /// summer. themeId is validated for API compatibility; it does not isolate one channel.
        /// Staged sampling prevents traversal bias and spurious summer between saturated themes.</summary>
        public bool Smooth(Vector2 center, float radius, int themeId, float opacity, float hardness = 0f) =>
            Smooth(center, radius, themeId, opacity, hardness, null);

        public bool Smooth(Vector2 center, float radius, int themeId, float opacity, float hardness,
            Rect? clipBounds)
        {
            ValidateBrush(center, radius, themeId, opacity, hardness);
            GetCells(center, radius, out int left, out int right, out int bottom, out int top, clipBounds);
            opacity = Mathf.Clamp01(opacity);
            hardness = Mathf.Clamp01(hardness);
            if (opacity == 0f || layers.Count == 0) return false;
            var updates = new List<(int x, int z, int id, float value)>();
            for (int z = bottom; z <= top; z++)
            for (int x = left; x <= right; x++)
            {
                float weight = BrushWeight(center, radius, hardness, x, z) * opacity;
                if (weight <= 0f) continue;
                foreach (var pair in layers)
                {
                    var field = pair.Value;
                    double old = field.GetCell(x, z);
                    double average = (old * 4 + field.GetCell(x - 1, z) + field.GetCell(x + 1, z)
                        + field.GetCell(x, z - 1) + field.GetCell(x, z + 1)) / 8;
                    updates.Add((x, z, pair.Key, (float)(old + (average - old) * weight)));
                }
            }
            var dirty = new EnvironmentDirtyCells(CellSize);
            foreach (var update in updates)
                if (layers[update.id].SetCell(update.x, update.z, update.value)) dirty.Add(update.x, update.z);
            PruneLayers();
            BroadcastChanges(dirty);
            return dirty.HasChanges;
        }

        private void PruneLayers()
        {
            var empty = new List<int>();
            foreach (var pair in layers) if (pair.Value.IsEmpty) empty.Add(pair.Key);
            foreach (int id in empty) layers.Remove(id);
        }

        private void BroadcastChanges(EnvironmentDirtyCells dirty)
        {
            if (!dirty.HasChanges) return;
            Revision++;
            Changed?.Invoke(dirty.Bounds);
        }

        private void GetCells(Vector2 center, float radius, out int left, out int right,
            out int bottom, out int top, Rect? clipBounds = null)
        {
            EnvironmentFieldMath.BrushCells(center, radius, CellSize, clipBounds,
                out left, out right, out bottom, out top);
        }

        public List<ChunkCoordinate> GetAffectedTiles(Vector2 center, float radius)
        {
            ValidateBrush(center, radius, 0, 1f, 0f);
            GetCells(center, radius, out int left, out int right, out int bottom, out int top);
            var result = new List<ChunkCoordinate>();
            if (left > right || bottom > top) return result;
            for (int z = SparseWorldFloatField.FloorDivide(bottom, TileResolution);
                z <= SparseWorldFloatField.FloorDivide(top, TileResolution); z++)
            for (int x = SparseWorldFloatField.FloorDivide(left, TileResolution);
                x <= SparseWorldFloatField.FloorDivide(right, TileResolution); x++)
                result.Add(new ChunkCoordinate(x, z));
            return result;
        }

        private float BrushWeight(Vector2 center, float radius, float hardness, int x, int z) =>
            EnvironmentFieldMath.BrushWeight(center, radius, hardness, x, z, CellSize);

        private static void ValidateBrush(Vector2 center, float radius, int themeId,
            float opacity, float hardness)
        {
            if (themeId < 0) throw new ArgumentOutOfRangeException(nameof(themeId));
            if (!EnvironmentFieldMath.Finite(center.x) || !EnvironmentFieldMath.Finite(center.y))
                throw new ArgumentOutOfRangeException(nameof(center));
            if (radius <= 0f || !EnvironmentFieldMath.Finite(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (!EnvironmentFieldMath.Finite(opacity)) throw new ArgumentOutOfRangeException(nameof(opacity));
            if (!EnvironmentFieldMath.Finite(hardness)) throw new ArgumentOutOfRangeException(nameof(hardness));
        }

        /// <summary>Owned, compact snapshot of all explicit themes in one tile.</summary>
        public List<ThemeLayerSnapshot> CaptureTile(int tileX, int tileZ)
        {
            var result = new List<ThemeLayerSnapshot>();
            foreach (var pair in layers)
            {
                var tile = pair.Value.CaptureTile(tileX, tileZ);
                if (tile != null) result.Add(new ThemeLayerSnapshot {
                    themeId = pair.Key, tiles = new List<ScalarTileSnapshot> { tile } });
            }
            result.Sort((a, b) => a.themeId.CompareTo(b.themeId));
            return result;
        }

        /// <summary>Atomically replaces one tile across all themes, leaving all other tiles untouched.</summary>
        public void RestoreTile(int tileX, int tileZ, List<ThemeLayerSnapshot> source)
        {
            var staged = new BiomeInfluenceField(CellSize, TileResolution);
            staged.Restore(source);
            if (source != null)
                foreach (var layer in source)
                    if (layer.tiles != null)
                        foreach (var tile in layer.tiles)
                            if (tile.tileX != tileX || tile.tileZ != tileZ)
                                throw new ArgumentException("Tile coordinate mismatch.");
            // All validation has completed before the first live write.
            foreach (var pair in layers) pair.Value.RestoreTile(tileX, tileZ, null);
            foreach (var pair in staged.layers)
                Layer(pair.Key).RestoreTile(tileX, tileZ, pair.Value.CaptureTile(tileX, tileZ));
            PruneLayers();
            Revision++;
            Changed?.Invoke(EnvironmentFieldMath.TileBounds(tileX, tileZ, TileResolution, CellSize));
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
                    if (replacement.ContainsKey(layer.themeId)) throw new ArgumentException("Duplicate theme ID.");
                    replacement.Add(layer.themeId, field);
                }
            }
            // Validate raw vector weights before committing any layer.
            var sums = new Dictionary<ChunkCoordinate, double[]>();
            foreach (var field in replacement.Values)
            foreach (var tile in field.Capture())
            {
                var key = new ChunkCoordinate(tile.tileX, tile.tileZ);
                if (!sums.TryGetValue(key, out var total))
                    sums.Add(key, total = new double[TileResolution * TileResolution]);
                for (int i = 0; i < total.Length; i++)
                    if ((total[i] += tile.values[i]) > 1.000001)
                        throw new ArgumentException("Theme weights exceed one.");
            }
            layers.Clear();
            foreach (var pair in replacement)
                if (!pair.Value.IsEmpty) layers.Add(pair.Key, pair.Value);
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

        /// <summary>Positive opacity blends toward snow, negative opacity toward cleared ground.
        /// Optional finite-map clipping applies to authored cell centres; filtering retains its halo.</summary>
        public bool Paint(Vector2 center, float radius, float signedOpacity, float hardness = 0f,
            Rect? clipBounds = null)
        {
            if (!EnvironmentFieldMath.Finite(center.x) || !EnvironmentFieldMath.Finite(center.y))
                throw new ArgumentOutOfRangeException(nameof(center));
            if (!EnvironmentFieldMath.Finite(radius) || radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (!EnvironmentFieldMath.Finite(signedOpacity))
                throw new ArgumentOutOfRangeException(nameof(signedOpacity));
            if (!EnvironmentFieldMath.Finite(hardness))
                throw new ArgumentOutOfRangeException(nameof(hardness));
            EnvironmentFieldMath.BrushCells(center, radius, CellSize, clipBounds,
                out int left, out int right, out int bottom, out int top);
            float opacity = Mathf.Clamp01(Math.Abs(signedOpacity));
            hardness = Mathf.Clamp01(hardness);
            if (opacity == 0f) return false;
            var dirty = new EnvironmentDirtyCells(CellSize);
            for (int z = bottom; z <= top; z++)
            for (int x = left; x <= right; x++)
            {
                float strength = EnvironmentFieldMath.BrushWeight(center, radius, hardness,
                    x, z, CellSize) * opacity;
                if (strength <= 0f) continue;
                float previous = amounts.GetCell(x, z);
                float next = signedOpacity > 0f ? Mathf.Lerp(previous, 1f, strength)
                    : previous * (1f - strength);
                if (amounts.SetCell(x, z, next)) dirty.Add(x, z);
            }
            if (dirty.HasChanges) { Revision++; Changed?.Invoke(dirty.Bounds); }
            return dirty.HasChanges;
        }

        public void Advance(Rect area, float dt, float snowRate, float meltRate,
            BiomeInfluenceField themes, int winterId)
        {
            if (themes == null) throw new ArgumentNullException(nameof(themes));
            if (winterId < 0) throw new ArgumentOutOfRangeException(nameof(winterId));
            if (!EnvironmentFieldMath.Finite(area.x) || !EnvironmentFieldMath.Finite(area.y) ||
                !EnvironmentFieldMath.Finite(area.width) || !EnvironmentFieldMath.Finite(area.height) ||
                !EnvironmentFieldMath.Finite(dt) || !EnvironmentFieldMath.Finite(snowRate) ||
                !EnvironmentFieldMath.Finite(meltRate))
                throw new ArgumentException("Non-finite weather parameter.");

            if (dt <= 0f || area.width <= 0f || area.height <= 0f) return;
            snowRate = Mathf.Max(0f, snowRate);
            meltRate = Mathf.Max(0f, meltRate);
            if (snowRate <= 0f && (meltRate <= 0f || amounts.IsEmpty)) return;
            float size = amounts.CellSize;
            int left = EnvironmentFieldMath.CellIndex((double)area.xMin / size - 0.5, true);
            int right = EnvironmentFieldMath.CellIndex(((double)area.x + area.width) / size - 0.5, false);
            int bottom = EnvironmentFieldMath.CellIndex((double)area.yMin / size - 0.5, true);
            int top = EnvironmentFieldMath.CellIndex(((double)area.y + area.height) / size - 0.5, false);
            EnvironmentFieldMath.ValidateWork(left, right, bottom, top);
            var dirty = new EnvironmentDirtyCells(size);

            for (int z = bottom; z <= top; z++)
            for (int x = left; x <= right; x++)
            {
                float worldX = (x + 0.5f) * size;
                float worldZ = (z + 0.5f) * size;
                float winter = themes.Sample(winterId, worldX, worldZ);
                float current = amounts.GetCell(x, z);
                float next = (float)Math.Max(0, Math.Min(1, current +
                    (double)snowRate * winter * dt -
                    (double)meltRate * (1 - 0.75 * winter) * dt));
                if (amounts.SetCell(x, z, next)) dirty.Add(x, z);
            }

            if (dirty.HasChanges)
            {
                Revision++;
                Changed?.Invoke(dirty.Bounds);
            }
        }

        public float CellSize => amounts.CellSize;
        public int TileResolution => amounts.TileResolution;
        public bool IsEmpty => amounts.IsEmpty;
        public float GetCell(int cellX, int cellZ) => amounts.GetCell(cellX, cellZ);
        public ScalarTileSnapshot CaptureTile(int tileX, int tileZ) => amounts.CaptureTile(tileX, tileZ);
        public void RestoreTile(int tileX, int tileZ, ScalarTileSnapshot source)
        {
            amounts.RestoreTile(tileX, tileZ, source);
            Revision++;
            Changed?.Invoke(EnvironmentFieldMath.TileBounds(tileX, tileZ, TileResolution, CellSize));
        }

        public List<ScalarTileSnapshot> Capture() => amounts.Capture();

        public void Restore(List<ScalarTileSnapshot> data)
        {
            amounts.Restore(data);
            Revision++;
            // Caller must invalidate the full displayed region after Restore.
        }
    }
    // Finite coordinates and bounded work are validated before an operation can allocate or mutate.
    internal static class EnvironmentFieldMath
    {
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static int CellIndex(double value, bool ceiling)
        {
            double index = ceiling ? Math.Ceiling(value) : Math.Floor(value);
            if (double.IsNaN(index) || double.IsInfinity(index) ||
                index <= int.MinValue || index >= int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), "Unsupported world coordinate.");
            return (int)index;
        }

        public static void BrushCells(Vector2 center, float radius, float size, Rect? clipBounds,
            out int left, out int right, out int bottom, out int top)
        {
            double minX = (double)center.x - radius, maxX = (double)center.x + radius;
            double minZ = (double)center.y - radius, maxZ = (double)center.y + radius;
            if (clipBounds.HasValue)
            {
                Rect clip = clipBounds.Value;
                if (!Finite(clip.x) || !Finite(clip.y) || !Finite(clip.width) || !Finite(clip.height) ||
                    clip.width < 0f || clip.height < 0f)
                    throw new ArgumentOutOfRangeException(nameof(clipBounds));
                minX = Math.Max(minX, clip.x);
                maxX = Math.Min(maxX, (double)clip.x + clip.width);
                minZ = Math.Max(minZ, clip.y);
                maxZ = Math.Min(maxZ, (double)clip.y + clip.height);
                if (minX > maxX || minZ > maxZ || clip.width == 0f || clip.height == 0f)
                {
                    left = bottom = 0; right = top = -1;
                    return;
                }
            }
            left = CellIndex(minX / size - 0.5, true);
            right = CellIndex(maxX / size - 0.5, false);
            bottom = CellIndex(minZ / size - 0.5, true);
            top = CellIndex(maxZ / size - 0.5, false);
            ValidateWork(left, right, bottom, top);
        }

        public static float BrushWeight(Vector2 center, float radius, float hardness,
            int x, int z, float size)
        {
            double dx = (x + 0.5) * size - center.x;
            double dz = (z + 0.5) * size - center.y;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            if (distance > radius) return 0f;
            if (hardness >= 1f || distance <= radius * hardness) return 1f;
            float progress = (float)((distance - radius * hardness) / (radius * (1.0 - hardness)));
            return 1f - Mathf.SmoothStep(0f, 1f, progress);
        }

        public static void ValidateWork(int left, int right, int bottom, int top)
        {
            if (left > right || bottom > top) return;
            long width = (long)right - left + 1;
            long height = (long)top - bottom + 1;
            if (width > 4194304 || height > 4194304 || width * height > 4194304)
                throw new ArgumentOutOfRangeException("area", "An operation supports at most 4,194,304 cells.");
        }

        public static Rect TileBounds(int x, int z, int resolution, float size) =>
            Rect.MinMaxRect((float)(((double)x * resolution - 0.5) * size),
                (float)(((double)z * resolution - 0.5) * size),
                (float)(((double)x * resolution + resolution + 0.5) * size),
                (float)(((double)z * resolution + resolution + 0.5) * size));
    }

    internal struct EnvironmentDirtyCells
    {
        private readonly float size;
        private int left, right, bottom, top;
        public bool HasChanges { get; private set; }
        public EnvironmentDirtyCells(float size)
        {
            this.size = size;
            left = right = bottom = top = 0;
            HasChanges = false;
        }
        public void Add(int x, int z)
        {
            if (!HasChanges) { left = right = x; bottom = top = z; HasChanges = true; }
            else { left = Math.Min(left, x); right = Math.Max(right, x);
                bottom = Math.Min(bottom, z); top = Math.Max(top, z); }
        }
        // One cell in each direction is the complete bilinear support of a changed centre.
        public Rect Bounds => Rect.MinMaxRect((float)((left - 0.5) * size),
            (float)((bottom - 0.5) * size), (float)((right + 1.5) * size),
            (float)((top + 1.5) * size));
    }

}
