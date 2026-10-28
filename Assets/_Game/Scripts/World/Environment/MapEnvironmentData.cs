using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World.Environment
{
    [Serializable] public sealed class MapEnvironmentSettings
    {
        public float snowAmount=1;
        public bool winterSnowPreview=true;
        public bool simulateWeather;
        public float snowfallIntensity;
        public float accumulationPerSecond=.04f;
        public float meltingPerSecond=.001f;
    }
    [Serializable] public sealed class MapEnvironmentData
    {
        public int version=1;
        public float cellSize=.5f;
        public int tileResolution=16;
        public List<ThemeLayerSnapshot> themes=new List<ThemeLayerSnapshot>();
        public List<ScalarTileSnapshot> snow=new List<ScalarTileSnapshot>();
        public MapEnvironmentSettings settings=new MapEnvironmentSettings();
    }
    /// <summary>Transport data only. Local snow and weather never enter a biome replica.</summary>
    [Serializable] public sealed class MapBiomeSnapshot
    {
        public int version=1;
        public string mapId;
        public Vector4 bounds;
        public long revision;
        public float cellSize;
        public int tileResolution;
        public List<ThemeLayerSnapshot> themes=new List<ThemeLayerSnapshot>();
    }
}
