using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    /// <summary>
    /// Bounded, deterministic presentation refinement of an upward heightfield.
    /// Does not change the source mesh, hydrology, saved map, materials or physics.
    /// Build returns one transient mesh owned and disposed by its caller.
    /// All LODs evaluate the same world-coordinate relief at shared samples.
    /// </summary>
    public static class MountainRockDetailBuilder
    {
        const float WeldTolerance = .0001f;

        struct PointKey : IEquatable<PointKey>
        {
            readonly int x, y, z;
            public PointKey(Vector3 p)
            { x = Mathf.RoundToInt(p.x / WeldTolerance); y = Mathf.RoundToInt(p.y / WeldTolerance); z = Mathf.RoundToInt(p.z / WeldTolerance); }
            public bool Equals(PointKey other) => x == other.x && y == other.y && z == other.z;
            public override bool Equals(object obj) => obj is PointKey other && Equals(other);
            public override int GetHashCode() { unchecked { return (x * 397 ^ y) * 397 ^ z; } }
        }

        struct Edge : IEquatable<Edge>
        {
            public readonly int A, B;
            public Edge(int a, int b) { A = Mathf.Min(a, b); B = Mathf.Max(a, b); }
            public bool Equals(Edge other) => A == other.A && B == other.B;
            public override bool Equals(object obj) => obj is Edge other && Equals(other);
            public override int GetHashCode() { unchecked { return A * 397 ^ B; } }
        }

        sealed class Topology
        {
            public readonly List<Vector3> Points = new List<Vector3>();
            public List<int> Triangles = new List<int>();
            public readonly HashSet<Edge> Boundary = new HashSet<Edge>();
            public readonly HashSet<int> Locked = new HashSet<int>();
        }

        public static Mesh Build(Mesh source, int seed, int lod, MountainRockDetailSettings settings = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            settings = settings ?? new MountainRockDetailSettings();
            settings.Validate();
            float spacing = settings.Spacing(lod);
            Topology topology = ReadAndStitch(source, settings);
            if (topology.Triangles.Count / 3 > settings.maximumTriangles)
                throw new ArgumentException("Source already exceeds the detail triangle budget.", nameof(source));
            var sampler = new HeightSampler(topology.Points, topology.Triangles);
            var pins = new PinLookup(topology.Points, settings);
            Refine(topology, spacing, settings.maximumTriangles);

            var detailed = new Vector3[topology.Points.Count];
            for (int i = 0; i < detailed.Length; i++)
            {
                Vector3 p = topology.Points[i];
                if (!topology.Locked.Contains(i))
                    p.y += Displacement(p, seed, sampler, pins, settings);
                detailed[i] = p;
            }
            return CreateMesh(detailed, topology.Triangles, settings.normalCreaseAngle, settings.macroControlMinimumZ, lod);
        }

        static Topology ReadAndStitch(Mesh source, MountainRockDetailSettings settings)
        {
            Vector3[] input = source.vertices;
            int[] indices = source.triangles;
            if (input.Length < 3 || indices.Length == 0 || indices.Length % 3 != 0)
                throw new ArgumentException("Detail source must contain triangles.", nameof(source));
            var topology = new Topology();
            var weld = new Dictionary<PointKey, int>();
            var remap = new int[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                Vector3 p = input[i];
                if (!MountainRockDetailSettings.Finite(p.x) || !MountainRockDetailSettings.Finite(p.y) || !MountainRockDetailSettings.Finite(p.z))
                    throw new ArgumentException("Source contains nonfinite positions.", nameof(source));
                var key = new PointKey(p);
                if (!weld.TryGetValue(key, out int index))
                { index = topology.Points.Count; topology.Points.Add(p); weld.Add(key, index); }
                remap[i] = index;
                if (p.z >= settings.macroControlMinimumZ) topology.Locked.Add(index);
            }
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = remap[indices[i]], b = remap[indices[i + 1]], c = remap[indices[i + 2]];
                if (Vector3.Cross(topology.Points[b] - topology.Points[a], topology.Points[c] - topology.Points[a]).y <= 0)
                    throw new ArgumentException("Source must be an upward, nondegenerate heightfield.", nameof(source));
                topology.Triangles.Add(a); topology.Triangles.Add(b); topology.Triangles.Add(c);
            }

            // v1's densely sampled shelf and coarse far mass meet with collinear
            // T junctions. Stitch them before refinement; otherwise independently
            // displaced samples could open a seam. Only boundary edges can contain
            // this defect, so the dense interior is never searched quadratically.
            var edgeCounts = CountEdges(topology.Triangles);
            var candidates = new SortedSet<int>();
            foreach (var pair in edgeCounts) if (pair.Value == 1) { candidates.Add(pair.Key.A); candidates.Add(pair.Key.B); }
            var cuts = new Dictionary<Edge, List<int>>();
            foreach (var pair in edgeCounts)
            {
                if (pair.Value != 1) continue;
                Edge edge = pair.Key;
                Vector3 a = topology.Points[edge.A], b = topology.Points[edge.B], delta = b - a;
                float lengthSquared = delta.sqrMagnitude;
                var line = new List<int>();
                foreach (int index in candidates)
                {
                    if (index == edge.A || index == edge.B) continue;
                    float t = Vector3.Dot(topology.Points[index] - a, delta) / lengthSquared;
                    if (t > .000001f && t < .999999f &&
                        (a + delta * t - topology.Points[index]).sqrMagnitude < .000001f) line.Add(index);
                }
                if (line.Count == 0) continue;
                line.Sort((left, right) => Vector3.Dot(topology.Points[left] - a, delta).CompareTo(Vector3.Dot(topology.Points[right] - a, delta)));
                cuts.Add(edge, line);
            }
            if (cuts.Count > 0)
            {
                var stitched = new List<int>(topology.Triangles.Count + cuts.Count * 6);
                for (int i = 0; i < topology.Triangles.Count; i += 3)
                {
                    int a = topology.Triangles[i], b = topology.Triangles[i + 1], c = topology.Triangles[i + 2];
                    if (!cuts.ContainsKey(new Edge(a, b)) && !cuts.ContainsKey(new Edge(b, c)) && !cuts.ContainsKey(new Edge(c, a)))
                    { Add(stitched, a, b, c); continue; }
                    var ring = new List<int>();
                    AppendEdge(ring, a, b, cuts); AppendEdge(ring, b, c, cuts); AppendEdge(ring, c, a, cuts);
                    int center = topology.Points.Count;
                    topology.Points.Add((topology.Points[a] + topology.Points[b] + topology.Points[c]) / 3f);
                    for (int j = 0; j < ring.Count; j++) Add(stitched, center, ring[j], ring[(j + 1) % ring.Count]);
                }
                topology.Triangles = stitched;
            }
            foreach (var pair in CountEdges(topology.Triangles)) if (pair.Value == 1)
            {
                topology.Boundary.Add(pair.Key);
                topology.Locked.Add(pair.Key.A); topology.Locked.Add(pair.Key.B);
            }
            return topology;
        }

        static void AppendEdge(List<int> ring, int a, int b, Dictionary<Edge, List<int>> cuts)
        {
            ring.Add(a);
            var key = new Edge(a, b);
            if (!cuts.TryGetValue(key, out var line)) return;
            if (a == key.A) ring.AddRange(line);
            else for (int i = line.Count - 1; i >= 0; i--) ring.Add(line[i]);
        }

        static Dictionary<Edge, int> CountEdges(List<int> triangles)
        {
            var counts = new Dictionary<Edge, int>();
            for (int i = 0; i < triangles.Count; i += 3)
                for (int j = 0; j < 3; j++)
                {
                    var key = new Edge(triangles[i + j], triangles[i + (j + 1) % 3]);
                    counts.TryGetValue(key, out int count); counts[key] = count + 1;
                }
            return counts;
        }

        static void Refine(Topology topology, float spacing, int budget)
        {
            for (int pass = 0; pass < 12; pass++)
            {
                var split = new Dictionary<Edge, int>();
                var order = new List<Edge>();
                for (int i = 0; i < topology.Triangles.Count; i += 3)
                    for (int j = 0; j < 3; j++)
                    {
                        var edge = new Edge(topology.Triangles[i + j], topology.Triangles[i + (j + 1) % 3]);
                        if (split.ContainsKey(edge)) continue;
                        Vector3 a = topology.Points[edge.A], b = topology.Points[edge.B];
                        // Retain the already dense foreground instead of uniformly
                        // quadrupling every water/lowland triangle. Cliff shoulder
                        // edges still receive enough samples for medium rock forms.
                        float target = (a.z + b.z) * .5f < 72f ? Mathf.Max(spacing, 6f) : spacing;
                        float dx = a.x - b.x, dz = a.z - b.z;
                        if (dx * dx + dz * dz <= target * target) continue;
                        split.Add(edge, -1); order.Add(edge);
                    }
                if (order.Count == 0) break;
                int nextCount = topology.Triangles.Count / 3;
                for (int i = 0; i < topology.Triangles.Count; i += 3)
                    for (int j = 0; j < 3; j++)
                        if (split.ContainsKey(new Edge(topology.Triangles[i + j], topology.Triangles[i + (j + 1) % 3]))) nextCount++;
                // Whole conforming rounds only: stopping halfway would create cracks.
                if (nextCount > budget) break;
                foreach (var edge in order)
                {
                    int midpoint = topology.Points.Count;
                    topology.Points.Add((topology.Points[edge.A] + topology.Points[edge.B]) * .5f);
                    split[edge] = midpoint;
                    if (topology.Boundary.Remove(edge))
                    {
                        topology.Locked.Add(midpoint);
                        topology.Boundary.Add(new Edge(edge.A, midpoint)); topology.Boundary.Add(new Edge(midpoint, edge.B));
                    }
                }
                var refined = new List<int>(nextCount * 3);
                for (int i = 0; i < topology.Triangles.Count; i += 3)
                {
                    int a = topology.Triangles[i], b = topology.Triangles[i + 1], c = topology.Triangles[i + 2];
                    int ab = Midpoint(split, a, b), bc = Midpoint(split, b, c), ca = Midpoint(split, c, a);
                    int mask = (ab >= 0 ? 1 : 0) | (bc >= 0 ? 2 : 0) | (ca >= 0 ? 4 : 0);
                    switch (mask)
                    {
                        case 0: Add(refined, a, b, c); break;
                        case 1: Add(refined, a, ab, c); Add(refined, ab, b, c); break;
                        case 2: Add(refined, b, bc, a); Add(refined, bc, c, a); break;
                        case 4: Add(refined, c, ca, b); Add(refined, ca, a, b); break;
                        case 3: Add(refined, b, bc, ab); Add(refined, a, ab, c); Add(refined, ab, bc, c); break;
                        case 6: Add(refined, c, ca, bc); Add(refined, b, bc, a); Add(refined, bc, ca, a); break;
                        case 5: Add(refined, a, ab, ca); Add(refined, c, ca, b); Add(refined, ca, ab, b); break;
                        case 7: Add(refined, a, ab, ca); Add(refined, ab, b, bc); Add(refined, ca, bc, c); Add(refined, ab, bc, ca); break;
                    }
                }
                topology.Triangles = refined;
            }
        }

        static int Midpoint(Dictionary<Edge, int> split, int a, int b) => split.TryGetValue(new Edge(a, b), out int value) ? value : -1;
        static void Add(List<int> triangles, int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }

        static float Displacement(Vector3 p, int seed, HeightSampler sampler, PinLookup pins, MountainRockDetailSettings settings)
        {
            float dx = Mathf.Max(settings.protectedWaterMinimum.x - p.x, 0f, p.x - settings.protectedWaterMaximum.x);
            float dz = Mathf.Max(settings.protectedWaterMinimum.y - p.z, 0f, p.z - settings.protectedWaterMaximum.y);
            if (dx == 0f && dz == 0f) return 0f;
            float mask = Smooth(0f, settings.protectedWaterFeather, Mathf.Max(dx, dz));
            mask *= Smooth(8f, 24f, p.y) * Smooth(4f, 22f, p.z) * pins.Weight(p);
            if (mask <= 0f || settings.relief == 0f) return 0f;
            Vector3 slope = sampler.Normal(p);
            // A 45-degree mountain face has 1-normal.y ~= .29. The previous
            // .12--.62 ramp suppressed nearly three quarters of its relief,
            // leaving sub-metre texture bumps on a 135-metre massif.
            mask *= Smooth(.025f, .28f, 1f - slope.y);
            if (mask <= 0f) return 0f;

            // Broad angular plates, offset ledges and long descending fissures.
            // There is deliberately no octave stack or texture-scale displacement.
            float width = settings.rockWidth;
            float x = p.x + 2.3f * Mathf.Sin(p.z * .045f + Phase(seed, 19));
            float z = p.z * .74f;
            int cellX = Mathf.FloorToInt(x / width), cellZ = Mathf.FloorToInt(z / width);
            float first = float.MaxValue, second = float.MaxValue, phase = 0f;
            for (int iz = cellZ - 1; iz <= cellZ + 1; iz++)
                for (int ix = cellX - 1; ix <= cellX + 1; ix++)
                {
                    float sx = (ix + .2f + Hash(seed, ix, iz, 3) * .6f) * width;
                    float sz = (iz + .2f + Hash(seed, ix, iz, 5) * .6f) * width;
                    float u = (x - sx) / width, v = (z - sz) / width;
                    // A clipped diamond roof reads as stone planes rather than balls.
                    float distance = Mathf.Max(Mathf.Abs(u + v * .32f), Mathf.Abs(v - u * .23f));
                    if (distance < first)
                    { second = first; first = distance; phase = Hash(seed, ix, iz, 7); }
                    else if (distance < second) second = distance;
                }
            float joint = second - first;
            float plateInterior = Smooth(0f, .14f, joint);
            float roof = Mathf.Clamp01((.63f - first) / .38f);
            float band = p.y / settings.ledgeHeight + phase * 2f + x * .009f;
            float fraction = band - Mathf.Floor(band);
            float ledge = fraction * (1f - Smooth(.70f, .97f, fraction));
            float fractureX = (x + 3.2f * Mathf.Sin(p.z * .065f + phase * 2f)) / (width * .68f);
            float fissure = Mathf.Abs(fractureX - Mathf.Floor(fractureX) - .5f);
            float groove = 1f - Smooth(.025f, .12f, fissure);
            float relief = (.35f * roof - .56f * ledge) * plateInterior -
                           .38f * (1f - plateInterior) - .28f * groove * plateInterior;
            float alpine = Smooth(32f, 62f, p.y) * Smooth(66f, 88f, p.z);
            if (alpine > 0f)
            {
                // Long descending cuts remain legible when the far LOD samples
                // at eleven metres. This is one broad structural band, not an
                // octave noise stack or independent displacement of each face.
                // Two coherent families cover both north/south and east/west
                // faces. Their weight follows the underlying macro slope.
                float broadWidth = width * 2.65f;
                float crossX = p.x + p.z * .19f + broadWidth * .12f * Mathf.Sin(p.z * .024f + Phase(seed, 31));
                float crossZ = p.z - p.x * .17f + broadWidth * .12f * Mathf.Sin(p.x * .022f + Phase(seed, 37));
                float xGully = StructuralGully(crossX / broadWidth + Hash(seed, 0, 0, 41));
                float zGully = StructuralGully(crossZ / broadWidth + Hash(seed, 0, 0, 43));
                float alongX = Mathf.Abs(slope.x), alongZ = Mathf.Abs(slope.z);
                float gully = Mathf.Lerp(xGully, zGully, alongX / Mathf.Max(.001f, alongX + alongZ));
                float terraceBand = (p.y + crossX * .07f) / (settings.ledgeHeight * 2.6f);
                float terraceFraction = terraceBand - Mathf.Floor(terraceBand);
                float terrace = terraceFraction * (1f - Smooth(.76f, 1f, terraceFraction));
                // Excavation only: the added structural layer cannot raise a
                // new summit above the source controls. Broad shoulders remain
                // between the cuts, with smaller broken plates on their sides.
                float structure = -1.32f * gully - .48f * terrace + Mathf.Min(0f, relief) * .28f;
                relief = Mathf.Lerp(relief, structure, alpine);
            }
            // Height-only deformation guarantees upward winding and preserves
            // all source planimetric anchors. Positive relief is limited to 35%
            // of the recipe amplitude; original summit controls stay locked.
            return relief * settings.relief * mask;
        }

        static float StructuralGully(float coordinate)
        {
            float distance = Mathf.Abs(coordinate - Mathf.Floor(coordinate) - .5f);
            // Broad planar flanks and a narrow flat ravine floor. Rounded
            // sinusoidal roofs would read as inflated cones at this scale.
            return Mathf.Clamp01((.43f - distance) / .34f);
        }

        static float Smooth(float low, float high, float value)
        { float t = Mathf.Clamp01((value - low) / (high - low)); return t * t * (3f - 2f * t); }
        static float Phase(int seed, int salt) => Hash(seed, salt, 0, 11) * Mathf.PI * 2f;
        static float Hash(int seed, int x, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)x * 0x9e3779b9u ^ (uint)z * 0x85ebca6bu ^ (uint)salt * 0xc2b2ae35u;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return (h & 0xffffffu) / 16777215f;
            }
        }

        sealed class PinLookup
        {
            readonly Dictionary<Vector2Int, List<Vector3>> cells = new Dictionary<Vector2Int, List<Vector3>>();
            readonly float radius;
            public PinLookup(List<Vector3> source, MountainRockDetailSettings settings)
            {
                radius = Mathf.Max(.0001f, settings.controlPointRadius);
                foreach (var p in source) if (p.z >= settings.macroControlMinimumZ) Insert(p);
                if (settings.additionalLockedPoints != null) foreach (var p in settings.additionalLockedPoints) Insert(p);
            }
            void Insert(Vector3 p)
            {
                var key = Cell(p);
                if (!cells.TryGetValue(key, out var values)) { values = new List<Vector3>(); cells.Add(key, values); }
                values.Add(p);
            }
            Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / radius), Mathf.FloorToInt(p.z / radius));
            public float Weight(Vector3 p)
            {
                var cell = Cell(p); float distance = radius;
                for (int z = cell.y - 1; z <= cell.y + 1; z++) for (int x = cell.x - 1; x <= cell.x + 1; x++)
                    if (cells.TryGetValue(new Vector2Int(x, z), out var values)) foreach (var v in values)
                        distance = Mathf.Min(distance, new Vector2(v.x - p.x, v.z - p.z).magnitude);
                return Smooth(0f, radius, distance);
            }
        }

        public sealed class HeightSampler
        {
            const float CellSize = 16f;
            readonly Vector3[] points;
            readonly int[] triangles;
            readonly Dictionary<Vector2Int, List<int>> cells = new Dictionary<Vector2Int, List<int>>();
            readonly Vector2 minimum, maximum;
            public HeightSampler(List<Vector3> points, List<int> triangles)
            {
                this.points = points.ToArray(); this.triangles = triangles.ToArray();
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
                foreach (var p in points) { min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z)); }
                minimum = min; maximum = max;
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
                    int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x) / CellSize), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x, c.x) / CellSize);
                    int z0 = Mathf.FloorToInt(Mathf.Min(a.z, b.z, c.z) / CellSize), z1 = Mathf.FloorToInt(Mathf.Max(a.z, b.z, c.z) / CellSize);
                    for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                    {
                        var key = new Vector2Int(x, z);
                        if (!cells.TryGetValue(key, out var values)) { values = new List<int>(); cells.Add(key, values); }
                        values.Add(i);
                    }
                }
            }
            public Vector3 Normal(Vector3 p)
            {
                const float step = 2.5f;
                float left = Height(p.x - step, p.z, p.y), right = Height(p.x + step, p.z, p.y);
                float front = Height(p.x, p.z - step, p.y), back = Height(p.x, p.z + step, p.y);
                return new Vector3(left - right, step * 2f, front - back).normalized;
            }
            public float Height(float x, float z, float fallback=0)
            {
                x = Mathf.Clamp(x, minimum.x, maximum.x); z = Mathf.Clamp(z, minimum.y, maximum.y);
                if (!cells.TryGetValue(new Vector2Int(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize)), out var candidates)) return fallback;
                foreach (int i in candidates)
                {
                    Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
                    double d = ((double)b.z - c.z) * (a.x - c.x) + ((double)c.x - b.x) * (a.z - c.z);
                    double u = (((double)b.z - c.z) * (x - c.x) + ((double)c.x - b.x) * (z - c.z)) / d;
                    double v = (((double)c.z - a.z) * (x - c.x) + ((double)a.x - c.x) * (z - c.z)) / d;
                    if (u >= -.00001 && v >= -.00001 && u + v <= 1.00001) return (float)(u * a.y + v * b.y + (1 - u - v) * c.y);
                }
                return fallback;
            }
        }

        static Mesh CreateMesh(Vector3[] positions, List<int> triangles, float creaseAngle, float macroMinimumZ, int lod)
        {
            var normals = new Vector3[positions.Length];
            var faces = new Vector3[triangles.Count / 3];
            var areas = new Vector3[faces.Length];
            var adjacent = new List<int>[positions.Length];
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 area = Vector3.Cross(positions[triangles[i + 1]] - positions[triangles[i]], positions[triangles[i + 2]] - positions[triangles[i]]);
                faces[i / 3] = area.normalized; areas[i / 3] = area;
                normals[triangles[i]] += area; normals[triangles[i + 1]] += area; normals[triangles[i + 2]] += area;
                for (int corner = 0; corner < 3; corner++)
                {
                    int vertex = triangles[i + corner];
                    if (adjacent[vertex] == null) adjacent[vertex] = new List<int>(6);
                    adjacent[vertex].Add(i / 3);
                }
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = normals[i].normalized;
            var renderPositions = new List<Vector3>(positions.Length);
            var renderNormals = new List<Vector3>(positions.Length);
            var uv = new List<Vector2>(positions.Length);
            var indices = new int[triangles.Count];
            var smoothIndex = new int[positions.Length];
            for (int i = 0; i < positions.Length; i++) smoothIndex[i] = -1;
            for (int i = 0; i < triangles.Count; i++)
            {
                int source = triangles[i]; Vector3 normal = normals[source];
                // Keep the lowland's authored smooth shading. Alpine ledges and
                // gully walls need their medium creases retained; averaging up
                // to 62 degrees erased the geometry even after subdivision.
                float alpine = Smooth(32f, 62f, positions[source].y) * Smooth(macroMinimumZ, macroMinimumZ + 16f, positions[source].z);
                float localCrease = Mathf.Lerp(creaseAngle, Mathf.Min(creaseAngle, 30f), alpine);
                float cosine = Mathf.Cos(localCrease * Mathf.Deg2Rad);
                bool crease = false;
                foreach (int face in adjacent[source])
                    if (Vector3.Dot(faces[face], faces[i / 3]) < cosine) { crease = true; break; }
                if (crease)
                {
                    normal = Vector3.zero;
                    foreach (int face in adjacent[source])
                        if (Vector3.Dot(faces[face], faces[i / 3]) >= cosine) normal += areas[face];
                    normal.Normalize();
                }
                int index = crease ? -1 : smoothIndex[source];
                if (index < 0)
                {
                    index = renderPositions.Count; renderPositions.Add(positions[source]);
                    renderNormals.Add(normal);
                    uv.Add(new Vector2(positions[source].x, positions[source].z) * .025f);
                    if (!crease) smoothIndex[source] = index;
                }
                indices[i] = index;
            }
            var mesh = new Mesh { name = "Mountain structural rock detail LOD" + lod,
                indexFormat = renderPositions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(renderPositions); mesh.SetNormals(renderNormals); mesh.SetUVs(0, uv); mesh.triangles = indices;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
