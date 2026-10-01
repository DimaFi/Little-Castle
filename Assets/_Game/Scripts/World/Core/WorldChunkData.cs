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
        public float[,] Heights { get; }

        public int SamplesPerSide => CellsPerSide + 1;

        public WorldChunkData(ChunkCoordinate coordinate, int cellsPerSide)
        {
            if (cellsPerSide < 1)
                throw new ArgumentOutOfRangeException(nameof(cellsPerSide));

            Coordinate = coordinate;
            CellsPerSide = cellsPerSide;
            Heights = new float[cellsPerSide + 1, cellsPerSide + 1];
        }

        public float GetHeight(int sampleX, int sampleZ) => Heights[sampleX, sampleZ];
        public void SetHeight(int sampleX, int sampleZ, float value) => Heights[sampleX, sampleZ] = value;
    }
}
