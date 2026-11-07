using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>Local triangle snapshot, populated by a scene adapter. No Physics or Unity objects in the solver.</summary>
    public sealed class MapWaterfallSupport
    {
        public readonly struct Hit
        {
            public readonly Vector3 point, normal, barycentric;
            public readonly string key;
            public readonly int triangle;
            internal Hit(Vector3 p, Vector3 n, Vector3 bary, string key, int triangle)
            { point = p; normal = n; barycentric = bary; this.key = key; this.triangle = triangle; }
        }
        struct Triangle { public Vector3 a, b, c, normal; public string key; public int index; }
        const float Cell = 2;
        readonly List<Triangle> triangles = new List<Triangle>();
        readonly Dictionary<ChunkCoordinate, List<int>> index = new Dictionary<ChunkCoordinate, List<int>>();
        string revision;
        public int TriangleCount => triangles.Count;
        public string Revision
        {
            get
            {
                if (revision != null) return revision;
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    var ordered = new List<Triangle>(triangles);
                    ordered.Sort((a, b) => { int c = string.CompareOrdinal(a.key, b.key); return c != 0 ? c : a.index.CompareTo(b.index); });
                    foreach (var t in ordered)
                    {
                        writer.Write(t.key); writer.Write(t.index);
                        foreach (var v in new[] { t.a, t.b, t.c }) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                    }
                    writer.Flush(); using (var sha = SHA256.Create())
                        revision = BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
                }
                return revision;
            }
        }
        public void Add(string key, int triangle, Vector3 a, Vector3 b, Vector3 c)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 256 || triangles.Count >= 1000000
                || !MapWaterfallRouteData.Finite(a) || !MapWaterfallRouteData.Finite(b) || !MapWaterfallRouteData.Finite(c))
                throw new InvalidDataException("Неверная или слишком сложная опора водопада.");
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f) return;
            int id = triangles.Count;
            triangles.Add(new Triangle { a = a, b = b, c = c, normal = normal.normalized, key = key, index = triangle });
            Vector3 lo = Vector3.Min(a, Vector3.Min(b, c)), hi = Vector3.Max(a, Vector3.Max(b, c));
            if ((hi.x - lo.x) * (hi.z - lo.z) > 65536)
                throw new InvalidDataException("Слишком крупный треугольник опоры. Ограничьте контактный меш.");
            for (int z = Mathf.FloorToInt(lo.z / Cell); z <= Mathf.FloorToInt(hi.z / Cell); z++)
                for (int x = Mathf.FloorToInt(lo.x / Cell); x <= Mathf.FloorToInt(hi.x / Cell); x++)
                {
                    var cell = new ChunkCoordinate(x, z);
                    if (!index.TryGetValue(cell, out var ids)) index.Add(cell, ids = new List<int>());
                    ids.Add(id);
                }
            revision = null;
        }
        public bool Height(Vector2 xz, float ceiling, out Hit hit)
            => Raycast(new Vector3(xz.x, ceiling, xz.y), Vector3.down, 1024, out hit, true);
        public bool Segment(Vector3 a, Vector3 b, out Hit hit)
        {
            Vector3 d = b - a; float length = d.magnitude;
            if (length < .00001f) { hit = default; return false; }
            return Raycast(a, d / length, length, out hit);
        }
        bool Raycast(Vector3 origin, Vector3 direction, float length, out Hit hit, bool upwardOnly = false)
        {
            hit = default; float best = length + .001f; string bestKey = null; int bestIndex = int.MaxValue;
            Vector3 end = origin + direction * length;
            int minX = Mathf.FloorToInt(Mathf.Min(origin.x, end.x) / Cell), maxX = Mathf.FloorToInt(Mathf.Max(origin.x, end.x) / Cell);
            int minZ = Mathf.FloorToInt(Mathf.Min(origin.z, end.z) / Cell), maxZ = Mathf.FloorToInt(Mathf.Max(origin.z, end.z) / Cell);
            var seen = new HashSet<int>();
            for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
            {
                if (!index.TryGetValue(new ChunkCoordinate(x, z), out var ids)) continue;
                foreach (int id in ids)
                {
                    if (!seen.Add(id)) continue; var t = triangles[id];
                    if (upwardOnly && t.normal.y <= .00001f) continue;
                    Vector3 e1 = t.b - t.a, e2 = t.c - t.a, p = Vector3.Cross(direction, e2);
                    float det = Vector3.Dot(e1, p); if (Mathf.Abs(det) < 1e-9f) continue;
                    float inv = 1 / det; Vector3 s = origin - t.a;
                    float u = Vector3.Dot(s, p) * inv; if (u < -1e-5f || u > 1.00001f) continue;
                    Vector3 q = Vector3.Cross(s, e1); float v = Vector3.Dot(direction, q) * inv;
                    if (v < -1e-5f || u + v > 1.00001f) continue;
                    float distance = Vector3.Dot(e2, q) * inv;
                    if (distance < .0001f || distance > length + .00001f || distance > best + .00001f) continue;
                    if (Mathf.Abs(distance - best) <= .00001f && bestKey != null)
                    { int compare = string.CompareOrdinal(t.key, bestKey); if (compare > 0 || compare == 0 && t.index >= bestIndex) continue; }
                    best = distance; bestKey = t.key; bestIndex = t.index;
                    hit = new Hit(origin + direction * distance, t.normal, new Vector3(1 - u - v, u, v), t.key, t.index);
                }
            }
            return bestKey != null;
        }
    }
}
