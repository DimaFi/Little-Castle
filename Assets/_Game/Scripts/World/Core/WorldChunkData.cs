using System;
using System.Collections.Generic;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class WorldChunkData
    {
        private readonly List<WorldSpawnData> spawns =
            new List<WorldSpawnData>();

        private readonly List<WorldResourceDepositData> resourceDeposits =
            new List<WorldResourceDepositData>();

        public ChunkCoordinate Coordinate { get; }
        public int CellsPerSide { get; }

        public float[,] Heights { get; }
        public float[,] CellSlopes { get; }
        public TerrainClass[,] TerrainClasses { get; }

        public float[,] Temperature { get; }
        public float[,] Moisture { get; }
        public BiomeKind[,] Biomes { get; }

        public float[,] ForestDensity { get; }
        public PlacementBlockFlags[,] PlacementBlocks { get; }

        public int SamplesPerSide => CellsPerSide + 1;

        public IReadOnlyList<WorldSpawnData> Spawns =>
            spawns;

        public IReadOnlyList<WorldResourceDepositData> ResourceDeposits =>
            resourceDeposits;

        public WorldChunkData(
            ChunkCoordinate coordinate,
            int cellsPerSide)
        {
            if (cellsPerSide < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(cellsPerSide));

            Coordinate = coordinate;
            CellsPerSide = cellsPerSide;

            Heights =
                new float[
                    cellsPerSide + 1,
                    cellsPerSide + 1];

            CellSlopes =
                new float[
                    cellsPerSide,
                    cellsPerSide];

            TerrainClasses =
                new TerrainClass[
                    cellsPerSide,
                    cellsPerSide];

            Temperature =
                new float[
                    cellsPerSide,
                    cellsPerSide];

            Moisture =
                new float[
                    cellsPerSide,
                    cellsPerSide];

            Biomes =
                new BiomeKind[
                    cellsPerSide,
                    cellsPerSide];

            ForestDensity =
                new float[
                    cellsPerSide,
                    cellsPerSide];

            PlacementBlocks =
                new PlacementBlockFlags[
                    cellsPerSide,
                    cellsPerSide];
        }

        public float GetHeight(
            int sampleX,
            int sampleZ) =>
            Heights[sampleX, sampleZ];

        public void SetHeight(
            int sampleX,
            int sampleZ,
            float value) =>
            Heights[sampleX, sampleZ] = value;

        public float GetCellSlope(
            int cellX,
            int cellZ) =>
            CellSlopes[cellX, cellZ];

        public void SetCellSlope(
            int cellX,
            int cellZ,
            float value) =>
            CellSlopes[cellX, cellZ] = value;

        public TerrainClass GetTerrainClass(
            int cellX,
            int cellZ) =>
            TerrainClasses[cellX, cellZ];

        public void SetTerrainClass(
            int cellX,
            int cellZ,
            TerrainClass value) =>
            TerrainClasses[cellX, cellZ] = value;

        public float GetTemperature(
            int cellX,
            int cellZ) =>
            Temperature[cellX, cellZ];

        public void SetTemperature(
            int cellX,
            int cellZ,
            float value) =>
            Temperature[cellX, cellZ] = value;

        public float GetMoisture(
            int cellX,
            int cellZ) =>
            Moisture[cellX, cellZ];

        public void SetMoisture(
            int cellX,
            int cellZ,
            float value) =>
            Moisture[cellX, cellZ] = value;

        public BiomeKind GetBiome(
            int cellX,
            int cellZ) =>
            Biomes[cellX, cellZ];

        public void SetBiome(
            int cellX,
            int cellZ,
            BiomeKind value) =>
            Biomes[cellX, cellZ] = value;

        public float GetForestDensity(
            int cellX,
            int cellZ) =>
            ForestDensity[cellX, cellZ];

        public void SetForestDensity(
            int cellX,
            int cellZ,
            float value) =>
            ForestDensity[cellX, cellZ] = value;

        public PlacementBlockFlags GetPlacementBlocks(
            int cellX,
            int cellZ) =>
            PlacementBlocks[cellX, cellZ];

        public void AddPlacementBlocks(
            int cellX,
            int cellZ,
            PlacementBlockFlags flags)
        {
            PlacementBlocks[cellX, cellZ] |= flags;
        }

        public bool IsPlacementBlocked(
            int cellX,
            int cellZ,
            PlacementBlockFlags flags)
        {
            return
                (PlacementBlocks[cellX, cellZ] &
                 flags) != 0;
        }

        public void AddSpawn(
            WorldSpawnData spawn) =>
            spawns.Add(spawn);

        public void AddResourceDeposit(
            WorldResourceDepositData deposit) =>
            resourceDeposits.Add(deposit);
    }
}
