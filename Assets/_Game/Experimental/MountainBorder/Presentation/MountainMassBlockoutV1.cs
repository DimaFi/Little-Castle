using System;
using System.Collections.Generic;
using UnityEngine;
using LittleCastle.World;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    /// <summary>
    /// One authored composition, not a general mountain generator. Shared control
    /// rows describe meadow, cliff shelves, branching buttresses and a broken
    /// three-peak skyline. Height queries use the actual rendered triangles.
    /// All coordinates and heights are absolute world metres.
    /// </summary>
    public static class MountainMassBlockoutV1
    {
        public static readonly Vector2 Minimum = new Vector2(-150f, -70f);
        public static readonly Vector2 Maximum = new Vector2(150f, 180f);
        public static readonly Vector3 UpperLip = new Vector3(-58f, 24f, 24f);
        public static readonly Vector3 LowerLanding = new Vector3(-58f, 1f, 8f);

        sealed class Row
        {
            public readonly float Z;
            public readonly float[] Controls;
            public Row(float z, params float[] controls) { Z = z; Controls = controls; }
        }

        sealed class Surface
        {
            public readonly Vector3[] Vertices;
            public readonly int[] Triangles;
            public readonly float[] XLines;
            public readonly float[] ZLines;
            public readonly Surface FarMacro;
            public Surface(Vector3[] vertices, int[] triangles, float[] xLines = null, float[] zLines = null, Surface farMacro = null)
            { Vertices = vertices; Triangles = triangles; XLines = xLines; ZLines = zLines; FarMacro=farMacro; }
        }

        static readonly object CacheLock = new object();
        static int cachedSeed;
        static Surface cachedSurface;

        /// <summary>
        /// New caller-owned transient mesh. The foreground uses shared smooth
        /// normals; distant mountain and steep cliff faces retain their creases.
        /// Dispose it with the private preview owner.
        /// A duplicated render vertex at a crease is the same surface position.
        /// </summary>
        public static Mesh Build(int seed)
        {
            var surface = GetSurface(seed);
            var vertices = new Vector3[surface.Triangles.Length];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var indices = new int[vertices.Length];
            // The refinement contains narrow cells at authored X constraints.
            // Area-averaging those triangles creates visible vertical bands at
            // macro-face crossings. Sample the SAME surface over a fixed world
            // footprint so shading is independent of these incidental cell widths.
            var smoothNormals = new Vector3[surface.Vertices.Length];
            for (int i = 0; i < smoothNormals.Length; i++)
            {
                Vector3 p = surface.Vertices[i];
                const float step = 3f;
                float left = SurfaceHeight(surface, new Vector2(Mathf.Max(Minimum.x, p.x-step), p.z));
                float right = SurfaceHeight(surface, new Vector2(Mathf.Min(Maximum.x, p.x+step), p.z));
                float front = SurfaceHeight(surface, new Vector2(p.x, Mathf.Max(Minimum.y, p.z-step)));
                float back = SurfaceHeight(surface, new Vector2(p.x, Mathf.Min(Maximum.y, p.z+step)));
                smoothNormals[i] = new Vector3(left-right, step*2, front-back).normalized;
            }
            for (int i = 0; i < surface.Triangles.Length; i += 3)
            {
                Vector3 a = surface.Vertices[surface.Triangles[i]];
                Vector3 b = surface.Vertices[surface.Triangles[i + 1]];
                Vector3 c = surface.Vertices[surface.Triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                vertices[i] = a; vertices[i + 1] = b; vertices[i + 2] = c;
                for (int j = 0; j < 3; j++)
                {
                    int k = i + j;
                    float foreground = 1f - SmoothRange(72f, 100f, vertices[k].z);
                    float gentleFace = SmoothRange(.5f, .85f, normal.y);
                    float smoothing = foreground*gentleFace;
                    normals[k] = Vector3.Lerp(normal, smoothNormals[surface.Triangles[k]], smoothing).normalized;
                    uv[k] = new Vector2(vertices[k].x, vertices[k].z) * .025f;
                    indices[k] = k;
                }
            }
            var mesh = new Mesh
            {
                name = "Mountain mass v1 - authored continuous blockout",
                indexFormat = vertices.Length > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = indices;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Barycentric height of the very same macro triangles Build renders.
        /// Outside this finite fixture returns the quiet meadow datum (zero).
        /// The authored lip and pool are additionally cut beneath the actual
        /// curved water strips; those protected heights do not depend on seed.
        /// </summary>
        public static float Height(int seed, Vector2 p)
        {
            if (float.IsNaN(p.x) || float.IsInfinity(p.x) ||
                float.IsNaN(p.y) || float.IsInfinity(p.y))
                throw new ArgumentException("Height query must be finite.", nameof(p));
            if (p.x < Minimum.x || p.x > Maximum.x || p.y < Minimum.y || p.y > Maximum.y)
                return 0f;
            return SurfaceHeight(GetSurface(seed),p);
        }

        static float SurfaceHeight(Surface surface, Vector2 p)
        {
            if(p.y>=72f)return MacroHeight(surface.FarMacro,p);
            int x = Cell(surface.XLines, p.x), z = Cell(surface.ZLines, p.y);
            int columns = surface.XLines.Length;
            int a = z * columns + x, b = a + 1, c = a + columns, d = c + 1;
            float u = (p.x - surface.XLines[x]) / (surface.XLines[x + 1] - surface.XLines[x]);
            float v = (p.y - surface.ZLines[z]) / (surface.ZLines[z + 1] - surface.ZLines[z]);
            // Exactly the same fixed diagonal as RefineForWater renders.
            if (u + v <= 1f)
                return surface.Vertices[a].y * (1f - u - v) +
                    surface.Vertices[b].y * u + surface.Vertices[c].y * v;
            return surface.Vertices[b].y * (1f - v) + surface.Vertices[c].y * (1f - u) +
                surface.Vertices[d].y * (u + v - 1f);
        }

        static float MacroHeight(Surface surface, Vector2 p)
        {
            for (int i = 0; i < surface.Triangles.Length; i += 3)
            {
                Vector3 a = surface.Vertices[surface.Triangles[i]];
                Vector3 b = surface.Vertices[surface.Triangles[i + 1]];
                Vector3 c = surface.Vertices[surface.Triangles[i + 2]];
                if (p.x < Mathf.Min(a.x, b.x, c.x) - .0001f ||
                    p.x > Mathf.Max(a.x, b.x, c.x) + .0001f ||
                    p.y < Mathf.Min(a.z, b.z, c.z) - .0001f ||
                    p.y > Mathf.Max(a.z, b.z, c.z) + .0001f) continue;
                double denominator = ((double)b.z - c.z) * (a.x - c.x) +
                                     ((double)c.x - b.x) * (a.z - c.z);
                double u = (((double)b.z - c.z) * (p.x - c.x) +
                            ((double)c.x - b.x) * (p.y - c.z)) / denominator;
                double v = (((double)c.z - a.z) * (p.x - c.x) +
                            ((double)a.x - c.x) * (p.y - c.z)) / denominator;
                double w = 1d - u - v;
                // Includes shared boundaries, deterministically in author order.
                if (u >= -.000001d && v >= -.000001d && w >= -.000001d)
                    return (float)(u * a.y + v * b.y + w * c.y);
            }
            throw new InvalidOperationException("Authored mountain surface has a planimetric hole.");
        }

        static Surface GetSurface(int seed)
        {
            lock (CacheLock)
            {
                if (cachedSurface == null || cachedSeed != seed)
                { cachedSurface = Compile(seed); cachedSeed = seed; }
                return cachedSurface;
            }
        }
        public static Vector3[] AuthoredControlPoints(int seed)=>(Vector3[])GetSurface(seed).FarMacro.Vertices.Clone();

        static Surface Compile(int seed)
        {
            // Pairs are (world X, absolute Y). Rows are shared patch boundaries:
            // additional channel controls exist only where needed, not skinny
            // vertical grid columns continuing all the way up the mountains.
            var rows = new[]
            {
                Lowland(-70, .1f, .25f),
                Lowland(-50, .2f, .35f),
                Lowland(-24, .5f, .35f),
                Lowland(-5, .8f, .35f),
                // Landing banks open a basin below the left-hand escarpment.
                new Row(8, -150,0, -125,1, -100,2, -78,2.8f, -68,2,
                    -63,1.2f, -61,.35f, -58,.35f, -55,.35f, -53,1.2f,
                    -47,2.4f, -28,3, 0,1.3f, 32,.8f, 65,2, 90,3, 122,1, 150,0),
                // Beneath the fall, the open bed stays below the top-to-bottom
                // water line. The surrounding shoulders form the cliff, not a box.
                new Row(12, -150,0, -129,3, -108,7, -88,9, -73,9,
                    -65,8.2f, -62,6.1f, -58,6.1f, -54,6.1f, -51,8.8f,
                    -42,10, -22,6, 3,2, 35,1, 64,3, 85,8, 113,7, 137,2, 150,0),
                new Row(20, -150,0, -136,7, -122,20, -105,22.5f, -87,21,
                    -73,23, -65,23.2f, -62,17.6f, -58,17.6f, -54,17.6f,
                    -51,23.5f, -42,24, -25,22, -12,10, 8,3, 35,2,
                    60,5, 78,13, 101,14, 125,9, 144,2, 150,0),
                new Row(24, -150,0, -137,9, -125,24.6f, -106,25.1f,
                    -88,24.9f, -73,25.2f, -65,24.8f, -62,23.45f,
                    -58,23.45f, -54,23.45f, -51,24.8f, -42,25,
                    -25,24.7f, -12,11, 8,3, 35,2, 58,6,
                    76,15, 103,15.8f, 126,10, 144,2, 150,0),
                // Broad walkable/forested shelf, cut by one explicit upper bed.
                Shelf(28, 25.3f, 26, 10, 16),
                Shelf(42, 26.2f, 28, 12, 18),
                Shelf(55, 27.5f, 30, 15, 20),
                Shelf(65, 29, 34, 17, 24),
                // Transition from shelves into split primary buttresses. Low
                // controls between them are gullies, not randomly dented cones.
                new Row(72, -150,0, -137,13, -121,37, -104,39, -88,33,
                    -73,34, -65,27, -62,23.45f, -58,23.45f, -54,23.45f,
                    -49,29, -36,41, -20,38, -7,23, 11,18, 29,26,
                    43,35, 61,26, 75,20, 91,34, 111,38, 130,21, 145,5, 150,0),
                new Row(82, -150,0, -137,17, -122,49, -109,54, -96,48,
                    -83,36, -70,46, -59,30, -45,45, -30,56, -17,44,
                    -4,29, 13,39, 29,53, 43,43, 58,30, 72,39,
                    89,49, 105,46, 124,37, 141,14, 150,0),
                // Three branching crest fingers and their descending ravines.
                new Row(95, -150,0, -139,20, -125,58, -111,73, -98,62,
                    -85,49, -73,61, -61,41, -47,59, -32,74, -18,56,
                    -4,40, 11,54, 27,76, 41,58, 56,42, 71,58,
                    88,68, 102,59, 119,49, 137,22, 150,0),
                new Row(108, -150,0, -138,31, -122,80, -110,98,
                    -100,94, -88,74, -74,67, -60,52, -45,73,
                    -30,88, -17,72, -1,54, 15,77, 29,96, 43,72,
                    58,53, 73,71, 90,89, 106,77, 121,60, 138,28, 150,0),
                // Left peak is forward of the main summit, with a short steep
                // western face and longer broken eastern shoulder.
                new Row(121, -150,0, -139,28, -124,77, -111,99,
                    -102,117, -95,109, -84,85, -70,75, -56,62,
                    -42,76, -29,96, -15,85, 0,70, 16,96, 30,110,
                    44,85, 58,65, 75,81, 91,99, 104,94, 119,66, 138,27, 150,0),
                // Two saddles connect the masses; neither cuts to the datum.
                new Row(133, -150,0, -137,26, -122,64, -108,92,
                    -98,99, -85,79, -71,69, -56,65, -43,73,
                    -29,91, -13,94, 3,88, 17,110, 31,121,
                    44,99, 59,72, 73,76, 87,96, 101,106,
                    109,99, 123,65, 141,22, 150,0),
                new Row(147, -150,0, -135,19, -117,51, -99,76,
                    -83,68, -66,62, -48,69, -31,85, -14,104,
                    1,113, 13,124, 24,132, 35,118, 49,94,
                    64,68, 78,75, 92,88, 108,84, 125,57, 141,18, 150,0),
                // Highest jagged summit is displaced in depth and X. Unequal
                // shoulders make a long crest rather than a radial cone cap.
                new Row(160, -150,0, -133,14, -113,39, -93,58,
                    -72,51, -51,60, -31,78, -15,99, -2,118,
                    9,129, 17,135, 23,128, 33,111, 49,84,
                    67,64, 85,72, 103,67, 122,45, 140,13, 150,0),
                new Row(172, -150,0, -130,8, -109,27, -86,41,
                    -64,38, -42,47, -22,64, -4,82, 14,103,
                    27,92, 44,70, 64,49, 84,54, 106,43, 128,26, 145,6, 150,0),
                new Row(180, -150,0, -128,5, -104,20, -80,27,
                    -57,24, -34,32, -12,48, 11,65, 31,55,
                    54,35, 79,30, 105,25, 130,12, 150,0)
            };

            var vertices = new List<Vector3>(600);
            var starts = new int[rows.Length];
            var counts = new int[rows.Length];
            for (int r = 0; r < rows.Length; r++)
            {
                Row row = rows[r];
                if (row.Controls.Length < 4 || (row.Controls.Length & 1) != 0)
                    throw new InvalidOperationException("Invalid authored control row.");
                starts[r] = vertices.Count;
                counts[r] = row.Controls.Length / 2;
                for (int i = 0; i < counts[r]; i++)
                {
                    float x = row.Controls[i * 2], y = row.Controls[i * 2 + 1];
                    if (i > 0 && x <= row.Controls[i * 2 - 2])
                        throw new InvalidOperationException("Authored controls must have ascending X.");
                    // Small control displacement only: no texture-scale noise,
                    // no erosion of protected stream/shelf/landing anchors.
                    if (row.Z >= 82f && y > 5f && Mathf.Abs(x) < 145f)
                        y += Signed(seed, r, i) * 1.15f;
                    vertices.Add(new Vector3(x, y, row.Z));
                }
            }

            var triangles = new List<int>(3000);
            for (int r = 0; r < rows.Length - 1; r++)
            {
                int a = 0, b = 0;
                while (a < counts[r] - 1 || b < counts[r + 1] - 1)
                {
                    int ia = starts[r] + a, ib = starts[r + 1] + b;
                    // A monotone zipper joins the two authored boundaries.
                    // Every row edge is shared; no disconnected cliff patches.
                    bool advanceA = b == counts[r + 1] - 1 ||
                        (a < counts[r] - 1 && vertices[ia + 1].x <= vertices[ib + 1].x);
                    triangles.Add(ia);
                    triangles.Add(ib);
                    triangles.Add(advanceA ? ia + 1 : ib + 1);
                    if (advanceA) a++; else b++;
                }
            }
            if (vertices.Count > 2000 || triangles.Count > 12000)
                throw new InvalidOperationException("Authored blockout exceeded its fixed geometry budget.");
            return RefineForWater(new Surface(vertices.ToArray(), triangles.ToArray()),seed);
        }

        static Surface RefineForWater(Surface macro,int seed)
        {
            // Retain every authored critical X/Z (including peaks and shelf lips)
            // and refine the complete water corridor to at most two metres.
            // No new noise: the refinement samples the authored macro surface.
            var xs = new SortedSet<float>();
            var zs = new SortedSet<float>();
            foreach (Vector3 p in macro.Vertices) if(p.z<=72f) { xs.Add(p.x); zs.Add(p.z); }
            for (int x = -74; x <= -24; x += 2) xs.Add(x);
            for (int z = -58; z <= 72; z += 2) zs.Add(z);
            var xLines = new float[xs.Count]; xs.CopyTo(xLines);
            var zLines = new float[zs.Count]; zs.CopyTo(zLines);
            int columns = xLines.Length, rows = zLines.Length;
            int triangleIndexCount = (columns - 1) * (rows - 1) * 6;
            if (columns * rows > 30000 || triangleIndexCount > 150000)
                throw new InvalidOperationException("Refined mountain fixture exceeded its bounded mesh budget.");
            var vertices = new Vector3[columns * rows];
            // Reuse the real existing rounded-hill sampler as lowland context.
            // This local preset is only for the private fixture: no project
            // settings, authored height samples or production sampler are edited.
            var hills=JsonUtility.FromJson<StylizedLandformSettings>(
                "{\"baseHeight\":3,\"regionSize\":256,\"plainCoverage\":0.35,\"hillHeight\":18,\"highlandHeight\":8,\"scarpCoverage\":0,\"detailAmplitude\":0.15}");
            for (int z = 0; z < rows; z++) for (int x = 0; x < columns; x++)
            {
                var p = new Vector2(xLines[x], zLines[z]);
                float hill=StylizedLandformSampler.SampleHeight(seed,p.x+850f,p.y-370f,hills);
                float context=(1f-SmoothRange(16f,72f,p.y))*
                    (1f-SmoothRange(125f,150f,Mathf.Abs(p.x)))*SmoothRange(-70f,-50f,p.y);
                vertices[z * columns + x] = new Vector3(p.x, MacroHeight(macro, p)+Mathf.Max(0,hill)*context, p.y);
            }

            // Read the actual rendered strips, including cubic fall, bent outlet,
            // transverse bases and variable-width landing pool. Do not duplicate
            // or approximate WaterfallGeometryV1's center/width functions here.
            // This creates no scene objects; the temporary mesh owner is disposed.
            using (var water = WaterfallGeometryV1.Build())
            {
                CarveWaterMesh(water.upper, vertices, xLines, zLines);
                CarveWaterMesh(water.fall, vertices, xLines, zLines);
                CarveWaterMesh(water.lower, vertices, xLines, zLines);
            }

            // Preserve the original constrained macro faces in the distant
            // massif. Re-rasterizing those faces into skinny rectangular cells
            // changes their diagonal crossings and produces shading stripes.
            // Both meshes share the exact height along their z=72 boundary.
            int farIndexCount=0;
            for(int i=0;i<macro.Triangles.Length;i+=3)
                if(macro.Vertices[macro.Triangles[i]].z>=72f&&macro.Vertices[macro.Triangles[i+1]].z>=72f&&macro.Vertices[macro.Triangles[i+2]].z>=72f)farIndexCount+=3;
            var combinedVertices=new Vector3[vertices.Length+macro.Vertices.Length];
            Array.Copy(vertices,combinedVertices,vertices.Length);
            Array.Copy(macro.Vertices,0,combinedVertices,vertices.Length,macro.Vertices.Length);
            var triangles = new int[triangleIndexCount+farIndexCount];
            int k = 0;
            for (int z = 0; z < rows - 1; z++) for (int x = 0; x < columns - 1; x++)
            {
                int a = z * columns + x, b = a + 1, c = a + columns, d = c + 1;
                triangles[k++] = a; triangles[k++] = c; triangles[k++] = b;
                triangles[k++] = b; triangles[k++] = c; triangles[k++] = d;
            }
            for(int i=0;i<macro.Triangles.Length;i+=3)
            {
                int a=macro.Triangles[i],b=macro.Triangles[i+1],c=macro.Triangles[i+2];
                if(macro.Vertices[a].z<72f||macro.Vertices[b].z<72f||macro.Vertices[c].z<72f)continue;
                triangles[k++]=vertices.Length+a;triangles[k++]=vertices.Length+b;triangles[k++]=vertices.Length+c;
            }
            return new Surface(combinedVertices, triangles, xLines, zLines, macro);
        }

        static void CarveWaterMesh(Mesh water, Vector3[] terrain, float[] xs, float[] zs)
        {
            const float footprintMargin = .65f;
            const float minimumClearance = .55f;
            Vector3[] waterVertices = water.vertices;
            int[] waterTriangles = water.triangles;
            int columns = xs.Length;
            for (int i = 0; i < waterTriangles.Length; i += 3)
            {
                Vector3 a = waterVertices[waterTriangles[i]];
                Vector3 b = waterVertices[waterTriangles[i + 1]];
                Vector3 c = waterVertices[waterTriangles[i + 2]];
                int x0 = Cell(xs, Mathf.Min(a.x, b.x, c.x) - footprintMargin);
                int x1 = Cell(xs, Mathf.Max(a.x, b.x, c.x) + footprintMargin);
                int z0 = Cell(zs, Mathf.Min(a.z, b.z, c.z) - footprintMargin);
                int z1 = Cell(zs, Mathf.Max(a.z, b.z, c.z) + footprintMargin);
                double denominator = ((double)b.z - c.z) * (a.x - c.x) +
                    ((double)c.x - b.x) * (a.z - c.z);
                // Enforce each actual water triangle's PLANE at every corner of
                // covered terrain cells. Both surfaces are affine per triangle:
                // this proves clearance throughout their overlap, not just at
                // water vertices. Using a triangle's global minimum Y instead
                // would unnecessarily dig the lip several metres too low.
                // The expanded footprint plus one covered cell protects banks.
                for (int z = z0; z <= z1 + 1; z++) for (int x = x0; x <= x1 + 1; x++)
                {
                    int index = z * columns + x;
                    float bed;
                    if (Math.Abs(denominator) < 1e-10d)
                        bed = Mathf.Min(a.y, b.y, c.y) - minimumClearance;
                    else
                    {
                        double u = (((double)b.z - c.z) * (xs[x] - c.x) +
                            ((double)c.x - b.x) * (zs[z] - c.z)) / denominator;
                        double v = (((double)c.z - a.z) * (xs[x] - c.x) +
                            ((double)a.x - c.x) * (zs[z] - c.z)) / denominator;
                        bed = (float)(u * a.y + v * b.y + (1d - u - v) * c.y) - minimumClearance;
                    }
                    if (terrain[index].y > bed)
                        terrain[index] = new Vector3(xs[x], bed, zs[z]);
                }
            }
        }

        static int Cell(float[] lines, float value)
        {
            int low = 0, high = lines.Length - 1;
            while (low + 1 < high)
            {
                int middle = (low + high) / 2;
                if (lines[middle] <= value) low = middle; else high = middle;
            }
            return Mathf.Clamp(low, 0, lines.Length - 2);
        }

        static float SmoothRange(float minimum, float maximum, float value)
        {
            float t = Mathf.Clamp01((value - minimum) / (maximum - minimum));
            return t * t * (3f - 2f * t);
        }

        static Row Lowland(float z, float meadowHeight, float bed)
        {
            return new Row(z, -150,0, -125,meadowHeight, -98,meadowHeight + .2f,
                -76,meadowHeight + .1f, -65,1.1f, -62,bed, -58,bed,
                -54,bed, -51,1.1f, -35,meadowHeight + .1f,
                -8,meadowHeight, 23,meadowHeight + .15f, 55,meadowHeight,
                89,meadowHeight + .25f, 122,meadowHeight, 150,0);
        }

        static Row Shelf(float z, float left, float rear, float center, float right)
        {
            return new Row(z, -150,0, -137,10, -125,left, -108,left + .3f,
                -91,left + .6f, -76,left, -65,24.8f, -62,23.45f,
                -58,23.45f, -54,23.45f, -51,24.8f, -41,rear,
                -25,rear - .8f, -13,center, 6,center - 5,
                28,center - 6, 50,center - 3, 74,right, 96,right + .7f,
                117,right, 135,7, 150,0);
        }

        static float Signed(int seed, int row, int control)
        {
            unchecked
            {
                uint h = (uint)seed ^ ((uint)row * 0x9e3779b9u) ^ ((uint)control * 0x85ebca6bu);
                h ^= h >> 16; h *= 0x7feb352du;
                h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return (h & 65535u) / 32767.5f - 1f;
            }
        }
    }
}
