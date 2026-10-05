using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>Finite, deterministic art-integration fixture. Not the match generator.
    /// The same surface drives meshes, grounding, colliders and path masks.</summary>
    public sealed class TerrainStarterPlan
    {
        public const float HalfSize = 48f;
        public readonly int Seed;
        public readonly bool ArtStudy;
        public readonly List<Vector3> Houses = new List<Vector3>();
        public readonly List<Vector3> Trees = new List<Vector3>();

        public TerrainStarterPlan(int seed, bool artStudy = false)
        {
            Seed = seed;
            ArtStudy = artStudy;
            Houses.Add(new Vector3(-9f, 0f, -4f));
            if (artStudy)
            {
                foreach (var p in new[] {new Vector2(-18,3),new Vector2(-12,8),new Vector2(-5,7),
                    new Vector2(2,4),new Vector2(4,-3),new Vector2(-23,-4),new Vector2(-23,7),
                    new Vector2(-17,12),new Vector2(-8,14),new Vector2(0,13),new Vector2(8,7)})
                    Trees.Add(new Vector3(p.x,Height(p.x,p.y),p.y));
                return;
            }
            Houses.Add(new Vector3(8f, 0f, 3f));
            Houses.Add(new Vector3(-11f, 0f, 10f));
            Houses.Add(new Vector3(10f, 0f, 16f));
            var random = new System.Random(seed);
            for (int attempt = 0; attempt < 600 && Trees.Count < 65; attempt++)
            {
                float x = (float)random.NextDouble() * 86f - 43f;
                float z = (float)random.NextDouble() * 86f - 43f;
                // Preserve a broad settlement reservation, paths and inspection foreground.
                if ((Mathf.Abs(x) < 19 && z > -24 && z < 26) || PathMask(x, z) > .01f)
                    continue;
                bool overlaps = false;
                foreach (Vector3 tree in Trees)
                    if (new Vector2(tree.x - x, tree.z - z).sqrMagnitude < 30f)
                        overlaps = true;
                if (!overlaps) Trees.Add(new Vector3(x, Height(x, z), z));
            }
        }

        public float Height(float x, float z)
        {
            // Flat village core blends continuously into gently rolling outskirts.
            float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(20, 42,
                Mathf.Max(Mathf.Abs(x), Mathf.Abs(z - 2))));
            float phase = (Seed % 997) * .013f;
            return edge * (2.8f + 1.2f * Mathf.Sin(x * .085f + phase)
                               + .8f * Mathf.Cos(z * .11f - phase));
        }

        public Vector3 Normal(float x, float z)
        {
            const float step = .1f;
            return new Vector3(Height(x - step, z) - Height(x + step, z),
                step * 2f, Height(x, z - step) - Height(x, z + step)).normalized;
        }

        public float PathMask(float x, float z)
        {
            float d = Mathf.Abs(x - RoadAt(z));
            // Footpaths continue to the front of each cottage, never under its floor.
            foreach (Vector3 house in Houses)
            {
                Vector2 a = new Vector2(RoadAt(house.z - 4.2f), house.z - 4.2f);
                Vector2 b = new Vector2(house.x - .8f, house.z - 4.2f);
                Vector2 p = new Vector2(x, z);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                d = Mathf.Min(d, Vector2.Distance(p, a + ab * t) + .6f);
            }
            d += .13f*Mathf.Sin(x*2.1f+z*.8f)+.06f*Mathf.Sin(z*3.7f-x);
            if(ArtStudy && z > -7.5f) d += (z+7.5f)*3;
            return 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.85f, 1.4f, d));
        }

        private float RoadAt(float z) => RoadCenter(z) - (ArtStudy ? 9.8f : 0);

        public static float RoadCenter(float z) => Mathf.Sin((z + 17) * .10f) * .8f;

        public float ContactShade(float x, float z)
        {
            float shade = 0;
            foreach(var h in Houses)
            {
                float dx=Mathf.Max(0,Mathf.Abs(x-h.x)-3.7f);
                float dz=Mathf.Max(0,Mathf.Abs(z-h.z)-3.1f);
                shade=Mathf.Max(shade,1-Mathf.Clamp01(Mathf.Sqrt(dx*dx+dz*dz)/1.5f));
            }
            foreach(var t in Trees)
                shade=Mathf.Max(shade,.95f*(1-Mathf.Clamp01(Vector2.Distance(new Vector2(x,z),new Vector2(t.x,t.z))/3.4f)));
            return shade;
        }

        public bool CanPlaceFootprint(Vector2 center, Vector2 size, float maxRise = .3f)
        {
            if (Mathf.Abs(center.x) + size.x * .5f > HalfSize ||
                Mathf.Abs(center.y) + size.y * .5f > HalfSize) return false;
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            // Full footprint grid, not just a center ray.
            for (int z = 0; z <= 8; z++) for (int x = 0; x <= 8; x++)
            {
                float h = Height(center.x + (x / 8f - .5f) * size.x,
                    center.y + (z / 8f - .5f) * size.y);
                low = Mathf.Min(low, h); high = Mathf.Max(high, h);
            }
            return high - low <= maxRise;
        }

        public Mesh BuildChunk(int cx, int cz)
        {
            const int cells = 32;
            const float spacing = .75f;
            var vertices = new Vector3[1089];
            var normals = new Vector3[1089];
            var colors = new Color[1089];
            var triangles = new int[cells * cells * 6];
            int ti = 0;
            for (int z = 0; z <= cells; z++) for (int x = 0; x <= cells; x++)
            {
                int i = z * 33 + x;
                float wx = cx * 24 + x * spacing, wz = cz * 24 + z * spacing;
                vertices[i] = new Vector3(x * spacing, Height(wx, wz), z * spacing);
                normals[i] = Normal(wx, wz);
                colors[i] = new Color(PathMask(wx, wz), 0, ContactShade(wx,wz), 1);
                if (x == cells || z == cells) continue;
                triangles[ti++] = i; triangles[ti++] = i + 33; triangles[ti++] = i + 1;
                triangles[ti++] = i + 1; triangles[ti++] = i + 33; triangles[ti++] = i + 34;
            }
            var mesh = new Mesh { name = $"Meadow_{cx}_{cz}" };
            mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors;
            mesh.triangles = triangles; mesh.RecalculateBounds();
            return mesh;
        }
    }
}
