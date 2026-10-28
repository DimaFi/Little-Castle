using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Optional visual-only normal weld for ALREADY-LOADED heightfield chunks.
    /// Mesh.RecalculateNormals normally treats every chunk border as a mesh
    /// boundary, producing inconsistent one-sided normals. This helper uses
    /// both adjacent chunks' authoritative height samples for centered
    /// differences and assigns identical normals to their shared vertices.
    ///
    /// Does not alter heights, UVs, triangles, colliders, mesh identity,
    /// world seed or neighboring chunk generation. Main thread only.
    /// </summary>
    public static class ChunkTerrainNormalSeams
    {
        private struct Entry
        {
            public WorldChunkData data;
            public Mesh mesh;
            public bool playable;

            public int Cells => data.CellsPerSide;
            public int Stride => data.CellsPerSide + 1;
        }

        // Reused across successive chunk builds: no 4K-vector allocation for
        // every mesh rewritten. Never call from a worker thread.
        private static readonly List<Vector3> ScratchNormals =
            new List<Vector3>(4225);

        public static int RefreshAround(
            ChunkCoordinate newlyLoaded,
            IReadOnlyDictionary<ChunkCoordinate, StreamedChunkView> views,
            float chunkWorldSize,
            Func<ChunkCoordinate, WorldChunkData> getData)
        {
            if (views == null || getData == null ||
                chunkWorldSize <= 0f)
                return 0;

            var entries = new Dictionary<ChunkCoordinate, Entry>(9);
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var coordinate = new ChunkCoordinate(
                        newlyLoaded.x + dx, newlyLoaded.z + dz);
                    if (!views.TryGetValue(coordinate, out StreamedChunkView view) ||
                        view == null)
                        continue;

                    MeshFilter filter = view.GetComponent<MeshFilter>();
                    Mesh mesh = filter != null ? filter.sharedMesh : null;
                    WorldChunkData data = getData(coordinate);
                    if (mesh == null || data == null || !mesh.isReadable ||
                        mesh.vertexCount != data.SamplesPerSide * data.SamplesPerSide)
                        continue;

                    entries.Add(coordinate, new Entry
                    {
                        data = data,
                        mesh = mesh,
                        playable = view.IsPlayableChunk
                    });
                }
            }

            if (!entries.TryGetValue(newlyLoaded, out Entry center))
                return 0;

            var updates = new Dictionary<Mesh, Dictionary<int, Vector3>>();
            float step = chunkWorldSize / center.Cells;
            if (step <= 0f)
                return 0;

            // Only stitch pairs involving the newly loaded chunk, avoiding
            // O(radius^2) work on each incremental chunk activation.
            for (int direction = 0; direction < 4; direction++)
            {
                int dx = direction == 0 ? -1 : direction == 1 ? 1 : 0;
                int dz = direction == 2 ? -1 : direction == 3 ? 1 : 0;
                var neighbourCoordinate = new ChunkCoordinate(
                    newlyLoaded.x + dx, newlyLoaded.z + dz);
                if (!entries.TryGetValue(neighbourCoordinate, out Entry neighbour) ||
                    !Compatible(center, neighbour))
                    continue;

                if (dx < 0)
                    AddEastWest(updates, neighbour, center, step);
                else if (dx > 0)
                    AddEastWest(updates, center, neighbour, step);
                else if (dz < 0)
                    AddNorthSouth(updates, neighbour, center, step);
                else
                    AddNorthSouth(updates, center, neighbour, step);
            }

            // A four-way corner needs both axes' central derivatives.
            // Once all four adjacent chunks are loaded, set the SAME normal
            // in all four border vertices, regardless of load order.
            for (int z = -1; z <= 0; z++)
            {
                for (int x = -1; x <= 0; x++)
                {
                    var lowerLeft = new ChunkCoordinate(
                        newlyLoaded.x + x, newlyLoaded.z + z);
                    var lowerRight = new ChunkCoordinate(
                        lowerLeft.x + 1, lowerLeft.z);
                    var upperLeft = new ChunkCoordinate(
                        lowerLeft.x, lowerLeft.z + 1);
                    var upperRight = new ChunkCoordinate(
                        lowerLeft.x + 1, lowerLeft.z + 1);

                    if (!entries.TryGetValue(lowerLeft, out Entry bl) ||
                        !entries.TryGetValue(lowerRight, out Entry br) ||
                        !entries.TryGetValue(upperLeft, out Entry tl) ||
                        !entries.TryGetValue(upperRight, out Entry tr) ||
                        !Compatible(bl, br) || !Compatible(bl, tl) ||
                        !Compatible(bl, tr))
                        continue;

                    int n = bl.Cells;
                    if (!Close(bl.data.GetHeight(n, n),
                            br.data.GetHeight(0, n)) ||
                        !Close(bl.data.GetHeight(n, n),
                            tl.data.GetHeight(n, 0)) ||
                        !Close(bl.data.GetHeight(n, n),
                            tr.data.GetHeight(0, 0)))
                        continue;

                    float dxSlope =
                        (br.data.GetHeight(1, n) -
                         bl.data.GetHeight(n - 1, n)) / (2f * step);
                    float dzSlope =
                        (tl.data.GetHeight(n, 1) -
                         bl.data.GetHeight(n, n - 1)) / (2f * step);
                    Vector3 normal = Normal(dxSlope, dzSlope);
                    Assign(updates, bl, n * (n + 1) + n, normal);
                    Assign(updates, br, n * (n + 1), normal);
                    Assign(updates, tl, n, normal);
                    Assign(updates, tr, 0, normal);
                }
            }

            // Write each touched mesh once; do not rebuild its vertices,
            // topology, mesh collider, UV, bounds or vertex-color masks.
            int changedMeshes = 0;
            foreach (KeyValuePair<Mesh, Dictionary<int, Vector3>> update in updates)
            {
                Mesh mesh = update.Key;
                ScratchNormals.Clear();
                mesh.GetNormals(ScratchNormals);
                if (ScratchNormals.Count != mesh.vertexCount)
                    continue;

                foreach (KeyValuePair<int, Vector3> vertex in update.Value)
                    ScratchNormals[vertex.Key] = vertex.Value;
                mesh.SetNormals(ScratchNormals);
                changedMeshes++;
            }
            ScratchNormals.Clear();
            return changedMeshes;
        }

        private static bool Compatible(Entry a, Entry b)
        {
            // Visual padding can be only TerrainAnalysis while playable
            // chunks have stamped river/bridge terrain; never hide a REAL
            // geometry mismatch by welding those different data profiles.
            return a.playable == b.playable &&
                a.Cells == b.Cells &&
                a.Cells >= 2;
        }

        private static void AddEastWest(
            Dictionary<Mesh, Dictionary<int, Vector3>> updates,
            Entry west, Entry east, float step)
        {
            int n = west.Cells;
            int stride = west.Stride;
            // A normal weld must never conceal an actual height crack.
            for (int z = 0; z <= n; z++)
                if (!Close(west.data.GetHeight(n, z),
                        east.data.GetHeight(0, z)))
                    return;

            for (int z = 1; z < n; z++)
            {
                float dxSlope =
                    (east.data.GetHeight(1, z) -
                     west.data.GetHeight(n - 1, z)) / (2f * step);
                float dzSlope =
                    (west.data.GetHeight(n, z + 1) -
                     west.data.GetHeight(n, z - 1)) / (2f * step);
                Vector3 normal = Normal(dxSlope, dzSlope);
                Assign(updates, west, z * stride + n, normal);
                Assign(updates, east, z * stride, normal);
            }
        }

        private static void AddNorthSouth(
            Dictionary<Mesh, Dictionary<int, Vector3>> updates,
            Entry south, Entry north, float step)
        {
            int n = south.Cells;
            int stride = south.Stride;
            for (int x = 0; x <= n; x++)
                if (!Close(south.data.GetHeight(x, n),
                        north.data.GetHeight(x, 0)))
                    return;

            for (int x = 1; x < n; x++)
            {
                float dxSlope =
                    (south.data.GetHeight(x + 1, n) -
                     south.data.GetHeight(x - 1, n)) / (2f * step);
                float dzSlope =
                    (north.data.GetHeight(x, 1) -
                     south.data.GetHeight(x, n - 1)) / (2f * step);
                Vector3 normal = Normal(dxSlope, dzSlope);
                Assign(updates, south, n * stride + x, normal);
                Assign(updates, north, x, normal);
            }
        }

        private static bool Close(float a, float b)
        {
            return !float.IsNaN(a) && !float.IsNaN(b) &&
                !float.IsInfinity(a) && !float.IsInfinity(b) &&
                Mathf.Abs(a - b) <= 0.001f;
        }

        private static Vector3 Normal(float dx, float dz)
        {
            return new Vector3(-dx, 1f, -dz).normalized;
        }

        private static void Assign(
            Dictionary<Mesh, Dictionary<int, Vector3>> updates,
            Entry chunk, int index, Vector3 normal)
        {
            if (!updates.TryGetValue(chunk.mesh, out Dictionary<int, Vector3> normals))
            {
                normals = new Dictionary<int, Vector3>();
                updates.Add(chunk.mesh, normals);
            }

            normals[index] = normal;
        }
    }
}
