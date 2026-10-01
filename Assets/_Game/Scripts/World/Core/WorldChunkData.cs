using System;

namespace LittleCastle.World
{
    /// <summary>
    /// Authoritative generated data for a single chunk.
    /// Contains no Unity scene-object references.
    /// </summary>
    [Serializable]
    public sealed class WorldChunkData
    {
        public ChunkCoordinate Coordinate { get; }
        public int CellsPerSide { get; }

        /// <summary>
        /// Height samples live on grid vertices and therefore have
        /// (CellsPerSide + 1) samples per side.
        /// </summary>
        public float[,] Heights { get; }

        /// <summary>
        /// Derived metadata lives per terrain cell.
        /// </summary>
        public float[,] CellSlopes { get; }
        public TerrainClass[,] TerrainClasses { get; }

        public int SamplesPerSide => CellsPerSide + 1;

        public WorldChunkData(ChunkCoordinate coordinate, int cellsPerSide)
        {
            if (cellsPerSide < 1)
                throw new ArgumentOutOfRangeException(nameof(cellsPerSide));

            Coordinate = coordinate;
            CellsPerSide = cellsPerSide;

            Heights = new float[cellsPerSide + 1, cellsPerSide + 1];
            CellSlopes = new float[cellsPerSide, cellsPerSide];
            TerrainClasses = new TerrainClass[cellsPerSide, cellsPerSide];
        }

        public float GetHeight(int sampleX, int sampleZ) => Heights[sampleX, sampleZ];
        public void SetHeight(int sampleX, int sampleZ, float value) => Heights[sampleX, sampleZ] = value;

        public float GetCellSlope(int cellX, int cellZ) => CellSlopes[cellX, cellZ];
        public void SetCellSlope(int cellX, int cellZ, float value) => CellSlopes[cellX, cellZ] = value;

        public TerrainClass GetTerrainClass(int cellX, int cellZ) => TerrainClasses[cellX, cellZ];
        public void SetTerrainClass(int cellX, int cellZ, TerrainClass value) => TerrainClasses[cellX, cellZ] = value;
    }
}
