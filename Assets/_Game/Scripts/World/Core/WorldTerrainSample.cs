using UnityEngine;

namespace LittleCastle.World
{
    public struct WorldTerrainSample
    {
        public float height;
        public float slope;
        public TerrainClass terrainClass;

        public WorldTerrainSample(
            float height,
            float slope,
            TerrainClass terrainClass)
        {
            this.height = height;
            this.slope = slope;
            this.terrainClass = terrainClass;
        }
    }
}
