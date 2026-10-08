using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.World
{
    /// <summary>
    /// Presentation helper that converts height data into a debug/runtime mesh.
    /// </summary>
    public static class ChunkMeshBuilder
    {
        public static Mesh Build(WorldChunkData chunk, float chunkWorldSize)
        {
            return Build(chunk, chunkWorldSize, null, false);
        }

        /// <summary>
        /// Optional concept-only vertex masks. The existing two-argument
        /// path keeps its old behavior and avoids extra allocations.
        /// </summary>
        public static Mesh Build(
            WorldChunkData chunk,
            float chunkWorldSize,
            MacroWorldPlan plan,
            bool useVertexMasks)
        {
            int samples = chunk.SamplesPerSide;
            int cells = chunk.CellsPerSide;
            float cellSize = chunkWorldSize / cells;

            int vertexCount = samples * samples;
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[cells * cells * 6];

            for (int z = 0; z < samples; z++)
            {
                for (int x = 0; x < samples; x++)
                {
                    int index = z * samples + x;
                    vertices[index] = new Vector3(
                        x * cellSize,
                        chunk.GetHeight(x, z),
                        z * cellSize);

                    uvs[index] = new Vector2(x / (float)cells, z / (float)cells);
                }
            }

            int ti = 0;

            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    int a = z * samples + x;
                    int b = a + 1;
                    int c = a + samples;
                    int d = c + 1;

                    triangles[ti++] = a;
                    triangles[ti++] = c;
                    triangles[ti++] = b;

                    triangles[ti++] = b;
                    triangles[ti++] = c;
                    triangles[ti++] = d;
                }
            }

            var mesh = new Mesh
            {
                name = $"WorldChunk_{chunk.Coordinate.x}_{chunk.Coordinate.z}"
            };

            if (vertexCount > 65535)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (useVertexMasks)
                mesh.colors32 = ChunkTerrainVertexMasks.Build(
                    chunk, chunkWorldSize, plan);

            return mesh;
        }
    }
}
