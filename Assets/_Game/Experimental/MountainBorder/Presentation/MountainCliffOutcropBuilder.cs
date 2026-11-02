using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    [Serializable]
    public sealed class MountainCliffOutcropSettings
    {
        [Range(.5f, 5f)] public float projection = 2.8f;
        [Range(.2f, 3f)] public float embedDepth = 1.25f;
        [Range(.1f, 2f)] public float capInset = .4f;

        internal void Validate()
        {
            if (!Finite(projection) || projection < .5f || projection > 5f ||
                !Finite(embedDepth) || embedDepth < .2f || embedDepth > 3f ||
                !Finite(capInset) || capInset < .1f || capInset > 2f)
                throw new ArgumentException("Cliff projection, embedding or cap inset is outside its bounded range.");
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// One bounded shoulder composition for the v1 sector: thirteen actual stone
    /// ribs with stepped ledges, tapered planar sides and clipped polygonal caps.
    /// Coordinates remain local to the sector. Does not mutate or own its input.
    /// The caller owns the returned transient sibling mesh and supplies material/LOD.
    /// </summary>
    public static class MountainCliffOutcropBuilder
    {
        // Clockwise in XZ, hence upward on a top cap. The forward corner is
        // deliberately asymmetric; these are fractured ribs rather than cubes.
        static readonly Vector2[] Pentagon = {
            new Vector2(-.10f, -1f), new Vector2(-.83f, -.56f), new Vector2(-1f, 1f),
            new Vector2(.96f, .82f), new Vector2(.78f, -.62f)
        };
        static readonly Vector2[] Triangle = {
            new Vector2(-.10f, -1f), new Vector2(-1f, 1f), new Vector2(.96f, .82f)
        };

        public static Mesh Build(Mesh detailedTerrain, int seed, int lod, MountainCliffOutcropSettings settings = null)
        {
            if (!detailedTerrain) throw new ArgumentNullException(nameof(detailedTerrain));
            if (lod < 0 || lod > 2) throw new ArgumentOutOfRangeException(nameof(lod));
            settings = settings ?? new MountainCliffOutcropSettings(); settings.Validate();
            var terrain = new TerrainQuery(detailedTerrain);
            var output = new Output();
            for (int rib = 0; rib < 13; rib++)
            {
                bool left = rib < 9; int local = left ? rib : rib - 9;
                float xMinimum = left ? -124f : -23f, xMaximum = left ? -77f : 0f;
                float centerX = left ? -121f + local * 5.15f : -20.2f + local * 5.5f;
                centerX += (Hash(seed, rib, 2) - .5f) * .25f;
                float halfWidth = 2.55f + Hash(seed, rib, 3) * .4f;
                float baseHeight = terrain.Height(centerX, 12f) - settings.embedDepth;
                // Cap below the lowest sampled part of the original rear shelf,
                // not below a new arbitrary altitude or a map-heightfield clamp.
                float cap = Mathf.Min(terrain.Height(centerX, 27.5f),
                    terrain.Height(Mathf.Max(xMinimum, centerX - halfWidth * .65f), 27.5f),
                    terrain.Height(Mathf.Min(xMaximum, centerX + halfWidth * .65f), 27.5f)) - settings.capInset;
                if (cap - baseHeight < 2f) continue;
                int courses = lod == 0 ? 3 : lod == 1 ? 2 : 1;
                for (int course = 0; course < courses; course++)
                {
                    float start = course / (float)courses, end = (course + 1f) / courses;
                    float lower = Mathf.Lerp(baseHeight, cap, start) - (course > 0 ? .24f : 0f);
                    float upper = Mathf.Lerp(baseHeight, cap, end);
                    float width = halfWidth * (1f - start * .12f);
                    float projection = settings.projection * (.88f + Hash(seed, rib, 10 + course) * .22f);
                    BuildCourse(output, terrain, centerX, xMinimum, xMaximum, width, lower, upper,
                        projection, lod, Hash(seed, rib, 29));
                }
            }
            return output.Mesh(lod);
        }

        static void BuildCourse(Output output, TerrainQuery terrain, float centerX, float xMinimum,
            float xMaximum, float halfWidth, float lower, float upper, float projection, int lod, float shape)
        {
            Vector2[] polygon = lod == 2 ? Triangle : Pentagon;
            int ringCount = lod == 0 ? 4 : 2;
            var rings = new Vector3[ringCount][];
            float bevel = Mathf.Min(.55f, (upper - lower) * .12f);
            for (int ring = 0; ring < ringCount; ring++)
            {
                float y = ringCount == 2 ? (ring == 0 ? lower : upper) :
                    ring == 0 ? lower : ring == 1 ? lower + bevel : ring == 2 ? upper - bevel : upper;
                float inset = ringCount == 2 ? (ring == 0 ? 1f : .88f) :
                    ring == 0 ? .94f : ring == 3 ? .84f : 1f;
                float groundFront = terrain.FrontAtHeight(centerX, y);
                float front = Mathf.Clamp(groundFront - projection, 12f, 24f);
                float back = Mathf.Min(28f, groundFront + 3.6f);
                // Back vertices sit inside the cliff. The forward ring actually
                // projects from it; intermediate cap courses make visible ledges.
                float centerZ = (front + back) * .5f, halfDepth = (back - front) * .5f;
                rings[ring] = new Vector3[polygon.Length];
                for (int point = 0; point < polygon.Length; point++)
                {
                    Vector2 p = polygon[point];
                    float x = centerX + p.x * halfWidth * inset;
                    x = Mathf.Clamp(x, xMinimum, xMaximum);
                    float z = centerZ + p.y * halfDepth * inset;
                    // A persistent oblique cut, shared through all rings. No
                    // per-vertex noise and no displacement of the underlying land.
                    z += (shape - .5f) * .22f * (1f - Mathf.Abs(p.y));
                    rings[ring][point] = new Vector3(x, y, Mathf.Clamp(z, 12f, 28f));
                }
            }
            for (int ring = 0; ring < ringCount - 1; ring++)
                for (int side = 0; side < polygon.Length; side++)
                {
                    int next = (side + 1) % polygon.Length;
                    output.Quad(rings[ring][side], rings[ring][next], rings[ring + 1][side], rings[ring + 1][next]);
                }
            for (int point = 1; point < polygon.Length - 1; point++)
            {
                output.Triangle(rings[ringCount - 1][0], rings[ringCount - 1][point], rings[ringCount - 1][point + 1]);
                output.Triangle(rings[0][0], rings[0][point + 1], rings[0][point]);
            }
        }

        static float Hash(int seed, int rib, int key)
        {
            unchecked
            {
                uint value = (uint)seed ^ (uint)rib * 0x9e3779b9u ^ (uint)key * 0x85ebca6bu;
                value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15;
                value *= 0x846ca68bu; value ^= value >> 16;
                return (value & 0xffffffu) / 16777215f;
            }
        }

        sealed class Output
        {
            readonly List<Vector3> positions = new List<Vector3>(4500);
            readonly List<Vector3> normals = new List<Vector3>(4500);
            readonly List<Vector2> uv = new List<Vector2>(4500);
            readonly List<Color32> masks = new List<Color32>(4500);
            readonly List<int> indices = new List<int>(4500);
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Vector3 normal = (Vector3.Cross(b - a, c - a) + Vector3.Cross(b - c, d - c)).normalized;
                Emit(a, b, c, normal); Emit(c, b, d, normal);
            }
            public void Triangle(Vector3 a, Vector3 b, Vector3 c) => Emit(a, b, c, Vector3.Cross(b - a, c - a).normalized);
            void Emit(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
            {
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-10f)
                    throw new InvalidOperationException("Cliff recipe produced a degenerate face.");
                foreach (var p in new[] { a, b, c })
                {
                    indices.Add(positions.Count); positions.Add(p); normals.Add(normal);
                    uv.Add(new Vector2(p.x, p.z) * .025f); masks.Add(new Color32(0, 0, 0, 0));
                }
            }
            public Mesh Mesh(int lod)
            {
                var mesh = new Mesh { name = "Mountain cliff shoulder outcrops LOD" + lod, indexFormat = IndexFormat.UInt16 };
                mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetColors(masks);
                mesh.SetTriangles(indices, 0); mesh.RecalculateBounds(); return mesh;
            }
        }

        sealed class TerrainQuery
        {
            const float CellSize = 8f;
            readonly Vector3[] positions;
            readonly int[] triangles;
            readonly Dictionary<Vector2Int, List<int>> cells = new Dictionary<Vector2Int, List<int>>();
            public TerrainQuery(Mesh mesh)
            {
                positions = mesh.vertices; triangles = mesh.triangles;
                if (positions.Length < 3 || triangles.Length == 0) throw new ArgumentException("Terrain must contain a heightfield.", nameof(mesh));
                // Index only the two shoulder strips and their rear shelf.
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = positions[triangles[i]], b = positions[triangles[i + 1]], c = positions[triangles[i + 2]];
                    float minX = Mathf.Min(a.x, b.x, c.x), maxX = Mathf.Max(a.x, b.x, c.x);
                    float minZ = Mathf.Min(a.z, b.z, c.z), maxZ = Mathf.Max(a.z, b.z, c.z);
                    if (maxZ < 12f || minZ > 28f || maxX < -124f || minX > 0f || (minX > -77f && maxX < -23f)) continue;
                    for (int z = Mathf.FloorToInt(minZ / CellSize); z <= Mathf.FloorToInt(maxZ / CellSize); z++)
                        for (int x = Mathf.FloorToInt(minX / CellSize); x <= Mathf.FloorToInt(maxX / CellSize); x++)
                        {
                            var key = new Vector2Int(x, z);
                            if (!cells.TryGetValue(key, out var values)) { values = new List<int>(); cells.Add(key, values); }
                            values.Add(i);
                        }
                }
            }
            public float Height(float x, float z)
            {
                if (cells.TryGetValue(new Vector2Int(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize)), out var candidates))
                    foreach (int i in candidates)
                    {
                        Vector3 a = positions[triangles[i]], b = positions[triangles[i + 1]], c = positions[triangles[i + 2]];
                        double denominator = ((double)b.z - c.z) * (a.x - c.x) + ((double)c.x - b.x) * (a.z - c.z);
                        if (Math.Abs(denominator) < 1e-12) continue;
                        double u = (((double)b.z - c.z) * (x - c.x) + ((double)c.x - b.x) * (z - c.z)) / denominator;
                        double v = (((double)c.z - a.z) * (x - c.x) + ((double)a.x - c.x) * (z - c.z)) / denominator;
                        if (u >= -.00001 && v >= -.00001 && u + v <= 1.00001)
                            return (float)(u * a.y + v * b.y + (1 - u - v) * c.y);
                    }
                throw new ArgumentException("Terrain does not cover the fixed mountain cliff shoulder at " + x + ", " + z + ".");
            }
            public float FrontAtHeight(float x, float height)
            {
                float previous = Height(x, 12f);
                if (height <= previous) return 12f;
                for (int step = 1; step <= 32; step++)
                {
                    float z = 12f + step * .5f, current = Height(x, z);
                    if (current >= height && current > previous + .0001f)
                        return z - .5f + Mathf.Clamp01((height - previous) / (current - previous)) * .5f;
                    previous = current;
                }
                return 28f;
            }
        }
    }
}
