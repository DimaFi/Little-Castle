using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Integer coordinate of a procedural world chunk.
    /// Unity's ground plane uses X/Z.
    /// </summary>
    [Serializable]
    public struct ChunkCoordinate : IEquatable<ChunkCoordinate>
    {
        public int x;
        public int z;

        public ChunkCoordinate(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public Vector3 GetWorldOrigin(float chunkWorldSize)
        {
            return new Vector3(x * chunkWorldSize, 0f, z * chunkWorldSize);
        }

        public bool Equals(ChunkCoordinate other) => x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is ChunkCoordinate other && Equals(other);

        public override int GetHashCode()
        {
            unchecked { return (x * 397) ^ z; }
        }

        public override string ToString() => $"({x}, {z})";

        public static bool operator ==(ChunkCoordinate left, ChunkCoordinate right) => left.Equals(right);
        public static bool operator !=(ChunkCoordinate left, ChunkCoordinate right) => !left.Equals(right);
    }
}
