using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    [Serializable]
    public sealed class LightweightRockSettings
    {
        [Range(.4f, 12f)] public float width = 3.2f;
        [Range(.3f, 10f)] public float height = 2.6f;
        [Range(.4f, 12f)] public float length = 3.8f;

        internal void Validate()
        {
            if (!Range(width, .4f, 12f) || !Range(height, .3f, 10f) || !Range(length, .4f, 12f))
                throw new ArgumentException("Rock dimensions must be finite and inside their bounded ranges.");
        }

        static bool Range(float value, float minimum, float maximum) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
    }

    /// <summary>
    /// Small reusable, closed stone shells: 76 / 44 / 26 triangles. Broad fractured
    /// faces, clipped corners and bevel shoulders replace sphere-like noisy detail.
    /// Base Y=0, length along Z, nominal dimensions before the slight seeded lean.
    /// Each call returns a caller-owned Mesh; creates no material, collider or scene object.
    /// LC Terrain uses world-space textures; zero RGBA leaves its authored masks clear.
    /// Supply the shared stone material/palette externally (including stone-only slope
    /// settings if horizontal caps must remain rock rather than terrain grass).
    /// </summary>
    public static class LightweightRockBuilder
    {
        static readonly float[] Heights = { 0f, .12f, .64f, .84f, 1f };
        static readonly float[] Radii = { .78f, 1f, .96f, .82f, .56f };
        static readonly int[] DetailedRings = { 0, 1, 2, 3, 4 };
        static readonly int[] CoarseRings = { 0, 2, 4 };
        static readonly int[] DetailedCorners = { 0, 1, 2, 3, 4, 5, 6, 7 };
        static readonly int[] DistantCorners = { 0, 1, 3, 4, 6 };

        public static Mesh Build(int seed, int lod, LightweightRockSettings settings = null)
        {
            if (lod < 0 || lod > 2) throw new ArgumentOutOfRangeException(nameof(lod));
            settings = settings ?? new LightweightRockSettings();
            settings.Validate();
            // Clockwise in XZ. Independently clipped rectangle corners remain
            // convex for every seed, unlike unconstrained radial vertex jitter.
            float Cut(int key) => Mathf.Lerp(.30f, .68f, Hash(seed, key));
            var footprint = new[] {
                new Vector2(-1, -1 + Cut(0)), new Vector2(-1, 1 - Cut(1)),
                new Vector2(-1 + Cut(2), 1), new Vector2(1 - Cut(3), 1),
                new Vector2(1, 1 - Cut(4)), new Vector2(1, -1 + Cut(5)),
                new Vector2(1 - Cut(6), -1), new Vector2(-1 + Cut(7), -1)
            };
            var selectedRings = lod == 0 ? DetailedRings : CoarseRings;
            var selectedCorners = lod == 2 ? DistantCorners : DetailedCorners;
            float leanX = (Hash(seed, 8) - .5f) * .16f;
            float leanZ = (Hash(seed, 9) - .5f) * .16f;
            float slopeX = (Hash(seed, 10) - .5f) * .09f;
            float slopeZ = (Hash(seed, 11) - .5f) * .09f;
            // Use the full footprint for a shared vertical normalization in all
            // LODs. A small oblique cap is a persistent part of this seed's cut.
            float topMaximum = 1;
            foreach (Vector2 point in footprint)
                topMaximum = Mathf.Max(topMaximum, 1 + slopeX * point.x + slopeZ * point.y);
            var rings = new Vector3[selectedRings.Length][];
            for (int ring = 0; ring < rings.Length; ring++)
            {
                int sourceRing = selectedRings[ring];
                float t = Heights[sourceRing], radius = Radii[sourceRing];
                rings[ring] = new Vector3[selectedCorners.Length];
                for (int corner = 0; corner < selectedCorners.Length; corner++)
                {
                    Vector2 p = footprint[selectedCorners[corner]];
                    float y = t * (1 + slopeX * p.x + slopeZ * p.y) / topMaximum;
                    rings[ring][corner] = new Vector3(
                        (p.x * radius * .5f + leanX * t) * settings.width,
                        y * settings.height,
                        (p.y * radius * .5f + leanZ * t) * settings.length);
                }
            }
            var output = new Output();
            for (int ring = 0; ring < rings.Length - 1; ring++)
                for (int side = 0; side < selectedCorners.Length; side++)
                {
                    int next = (side + 1) % selectedCorners.Length;
                    Vector3 a = rings[ring][side], b = rings[ring][next];
                    Vector3 c = rings[ring + 1][side], d = rings[ring + 1][next];
                    output.Triangle(a, b, c); output.Triangle(c, b, d);
                }
            for (int corner = 1; corner < selectedCorners.Length - 1; corner++)
            {
                output.Triangle(rings[rings.Length - 1][0], rings[rings.Length - 1][corner], rings[rings.Length - 1][corner + 1]);
                output.Triangle(rings[0][0], rings[0][corner + 1], rings[0][corner]);
            }
            return output.Mesh(seed, lod);
        }

        static float Hash(int seed, int key)
        {
            unchecked
            {
                uint value = (uint)seed ^ (uint)key * 0x9e3779b9u;
                value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15;
                value *= 0x846ca68bu; value ^= value >> 16;
                return (value & 0xffffffu) / 16777215f;
            }
        }

        sealed class Output
        {
            readonly List<Vector3> vertices = new List<Vector3>(228);
            readonly List<Vector3> normals = new List<Vector3>(228);
            readonly List<Vector2> uv = new List<Vector2>(228);
            readonly List<Color32> colors = new List<Color32>(228);
            readonly List<int> indices = new List<int>(228);

            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude < 1e-12f) throw new InvalidOperationException("Degenerate rock face.");
                Vector3 normal = cross.normalized;
                Add(a, normal); Add(b, normal); Add(c, normal);
            }

            void Add(Vector3 p, Vector3 normal)
            {
                indices.Add(vertices.Count); vertices.Add(p); normals.Add(normal);
                // Dominant-axis UV fallback for ordinary opaque materials; LC
                // Terrain itself samples world coordinates and needs no tangents.
                Vector3 n = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                uv.Add(n.y >= n.x && n.y >= n.z ? new Vector2(p.x, p.z) :
                    n.x >= n.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
                colors.Add(new Color32(0, 0, 0, 0));
            }

            public Mesh Mesh(int seed, int lod)
            {
                var mesh = new Mesh { name = "Lightweight fractured stone " + seed + " LOD" + lod };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
                mesh.SetColors(colors); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
