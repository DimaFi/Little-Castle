using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Integer chunk bounds with inclusive minimum and exclusive maximum.
    /// Used for deterministic finite session maps.
    /// </summary>
    [Serializable]
    public struct WorldChunkBounds
    {
        [SerializeField] private int minX;
        [SerializeField] private int minZ;
        [SerializeField] private int sizeX;
        [SerializeField] private int sizeZ;

        public int MinX => minX;
        public int MinZ => minZ;
        public int SizeX => Mathf.Max(1, sizeX);
        public int SizeZ => Mathf.Max(1, sizeZ);
        public int MaxXExclusive => minX + SizeX;
        public int MaxZExclusive => minZ + SizeZ;

        public WorldChunkBounds(
            int minX,
            int minZ,
            int sizeX,
            int sizeZ)
        {
            this.minX = minX;
            this.minZ = minZ;
            this.sizeX = Mathf.Max(1, sizeX);
            this.sizeZ = Mathf.Max(1, sizeZ);
        }

        public bool Contains(ChunkCoordinate coordinate)
        {
            return
                coordinate.x >= MinX &&
                coordinate.x < MaxXExclusive &&
                coordinate.z >= MinZ &&
                coordinate.z < MaxZExclusive;
        }

        public WorldChunkBounds Expand(int chunks)
        {
            int padding =
                Mathf.Max(0, chunks);

            return new WorldChunkBounds(
                MinX - padding,
                MinZ - padding,
                SizeX + padding * 2,
                SizeZ + padding * 2);
        }

        public Rect ToWorldRect(float chunkWorldSize)
        {
            float size =
                Mathf.Max(
                    0.0001f,
                    chunkWorldSize);

            return new Rect(
                MinX * size,
                MinZ * size,
                SizeX * size,
                SizeZ * size);
        }

        public Vector3 ClampWorldPosition(
            Vector3 worldPosition,
            float chunkWorldSize,
            float inset = 0f)
        {
            Rect rect =
                ToWorldRect(
                    chunkWorldSize);

            float safeInset =
                Mathf.Max(0f, inset);

            float minWorldX =
                rect.xMin +
                safeInset;

            float exclusiveEdgeInset =
                Mathf.Max(
                    safeInset,
                    0.001f);

            float maxWorldX =
                Mathf.Max(
                    minWorldX,
                    rect.xMax -
                    exclusiveEdgeInset);

            float minWorldZ =
                rect.yMin +
                safeInset;

            float maxWorldZ =
                Mathf.Max(
                    minWorldZ,
                    rect.yMax -
                    exclusiveEdgeInset);

            worldPosition.x =
                Mathf.Clamp(
                    worldPosition.x,
                    minWorldX,
                    maxWorldX);

            worldPosition.z =
                Mathf.Clamp(
                    worldPosition.z,
                    minWorldZ,
                    maxWorldZ);

            return worldPosition;
        }

        public override string ToString()
        {
            return
                "[" +
                MinX + "," +
                MinZ +
                " -> " +
                MaxXExclusive + "," +
                MaxZExclusive +
                ")";
        }
    }
}
