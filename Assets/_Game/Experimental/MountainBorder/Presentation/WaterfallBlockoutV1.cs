using System;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    /// <summary>
    /// Disposable presentation-only fixture. Positions are authored in world space;
    /// no river, terrain, scene object, material or collider is created or changed.
    /// UV0 is (across 0..1, cumulative centerline distance in metres), UV2 is
    /// (across 0..1, normalized station), and vertex R fades longitudinal ends.
    /// </summary>
    public sealed class WaterfallGeometryV1 : IDisposable
    {
        public Mesh upper { get; private set; }
        public Mesh fall { get; private set; }
        public Mesh lower { get; private set; }
        public Vector3 lip { get; private set; }
        public Vector3 landing { get; private set; }

        private bool disposed;
        private const int Across = 16;
        private const int Rows = 96;

        private WaterfallGeometryV1() { }

        public static WaterfallGeometryV1 Build()
        {
            var result = new WaterfallGeometryV1
            {
                lip = new Vector3(-58f, 24.15f, 24f),
                landing = new Vector3(-58f, 1f, 8f)
            };
            try
            {
                result.upper = BuildStrip("Waterfall v1 upper channel", UpperCenter, UpperWidth);
                result.fall = BuildStrip("Waterfall v1 curved cascade", FallCenter, FallWidth);
                result.lower = BuildStrip("Waterfall v1 pool and outlet", LowerCenter, LowerWidth);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        /// <summary>Same curved construction between explicit local-space water levels.</summary>
        public static WaterfallGeometryV1 Between(Vector3 upperLip,Vector3 lowerLanding,float width=5.5f)
        {
            Vector3 direction=lowerLanding-upperLip;direction.y=0;float span=direction.magnitude;
            if(!Finite(upperLip)||!Finite(lowerLanding)||!float.IsFinite(width)||width<1||width>40||upperLip.y-lowerLanding.y<.5f||span<1)
                throw new ArgumentException("Waterfall requires finite separated water levels, a horizontal span and width 1–40 metres.");
            direction/=span;Vector3 across=Vector3.Cross(direction,Vector3.up);
            var result=Build();Vector3 sourceLip=result.lip,sourceLanding=result.landing;
            try
            {
                foreach(var mesh in new[]{result.upper,result.fall,result.lower})
                {
                    var vertices=mesh.vertices;var uv=mesh.uv;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        var p=vertices[i];float height=(p.y-sourceLanding.y)/(sourceLip.y-sourceLanding.y);
                        Vector3 placed=upperLip+direction*((sourceLip.z-p.z)*span/(sourceLip.z-sourceLanding.z))+across*((p.x-sourceLip.x)*width/5.5f);
                        placed.y=Mathf.LerpUnclamped(lowerLanding.y,upperLip.y,height);vertices[i]=placed;
                    }
                    float distance=0;const int columns=17;
                    for(int row=0;row<vertices.Length/columns;row++)
                    {
                        if(row>0)distance+=Vector3.Distance(vertices[row*columns+8],vertices[(row-1)*columns+8]);
                        for(int col=0;col<columns;col++)uv[row*columns+col].y=distance;
                    }
                    mesh.vertices=vertices;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                }
                result.lip=upperLip;result.landing=lowerLanding;return result;
            }
            catch{result.Dispose();throw;}
        }
        static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);

        private static Vector3 UpperCenter(float t)
        {
            // Shallow bend upstream, exactly aligned with the lip at the overlap.
            float x = -58f + 2.2f * Mathf.Pow(1f - t, 2f);
            return new Vector3(x, 24.15f, Mathf.Lerp(65f, 23f, t));
        }

        private static float UpperWidth(float t)
        {
            return Mathf.Lerp(6.2f, 5.5f, Smooth(t));
        }

        private static Vector3 FallCenter(float t)
        {
            // Horizontal overlap into the upper level, a cubic rounded lip/drop,
            // then a horizontal overlap into the landing surface.
            if (t <= 0.1f)
                return new Vector3(-58f, 24.15f, Mathf.Lerp(27f, 24f, t / 0.1f));
            if (t >= 0.9f)
                return new Vector3(-58f, 1f, Mathf.Lerp(8f, 5f, (t - 0.9f) / 0.1f));

            float u = (t - 0.1f) / 0.8f;
            float v = 1f - u;
            Vector3 a = new Vector3(-58f, 24.15f, 24f);
            Vector3 b = new Vector3(-58f, 24.15f, 20f);
            Vector3 c = new Vector3(-58f, 1f, 12f);
            Vector3 d = new Vector3(-58f, 1f, 8f);
            return v * v * v * a + 3f * v * v * u * b +
                3f * v * u * u * c + u * u * u * d;
        }

        private static float FallWidth(float t)
        {
            return Mathf.Lerp(5.5f, 6.5f, Smooth(t));
        }

        private static Vector3 LowerCenter(float t)
        {
            float z = Mathf.Lerp(11f, -55f, t);
            // Begin bending after the pool; make the outlet leave the frame.
            float bend = Smooth(Mathf.InverseLerp(-8f, -55f, z));
            return new Vector3(-58f + 23f * bend, Mathf.Lerp(1f, .8f, Smooth(t)), z);
        }

        private static float LowerWidth(float t)
        {
            float z = Mathf.Lerp(11f, -55f, t);
            float pool = Mathf.Exp(-Mathf.Pow((z - 2f) / 8f, 2f));
            return Mathf.Lerp(6.5f, 7.5f, Smooth(t)) + 9.5f * pool;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static Mesh BuildStrip(string name, Func<float, Vector3> centerAt,
            Func<float, float> widthAt)
        {
            int columns = Across + 1;
            var vertices = new Vector3[(Rows + 1) * columns];
            var uv = new Vector2[vertices.Length];
            var uv2 = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[Rows * Across * 6];
            var centers = new Vector3[Rows + 1];
            var distance = new float[Rows + 1];
            for (int row = 0; row <= Rows; row++)
            {
                centers[row] = centerAt((float)row / Rows);
                if (row > 0)
                    distance[row] = distance[row - 1] + Vector3.Distance(centers[row - 1], centers[row]);
            }

            for (int row = 0; row <= Rows; row++)
            {
                float t = (float)row / Rows;
                // A stable lateral basis follows the horizontal projected flow.
                Vector3 along = centers[Mathf.Min(row + 1, Rows)] - centers[Mathf.Max(row - 1, 0)];
                Vector3 lateral = Vector3.Cross(along, Vector3.up);
                if (lateral.sqrMagnitude < 1e-8f) lateral = Vector3.right;
                else lateral.Normalize();
                float fade = Smooth(Mathf.Min(t / .10f, (1f - t) / .10f));
                float halfWidth = widthAt(t) * .5f;
                for (int column = 0; column <= Across; column++)
                {
                    float u = (float)column / Across;
                    int index = row * columns + column;
                    vertices[index] = centers[row] + lateral * ((u * 2f - 1f) * halfWidth);
                    uv[index] = new Vector2(u, distance[row]);
                    uv2[index] = new Vector2(u, t);
                    colors[index] = new Color(fade, 1f, 1f, 1f);
                }
            }

            int k = 0;
            for (int row = 0; row < Rows; row++)
            for (int column = 0; column < Across; column++)
            {
                int a = row * columns + column;
                int b = a + 1;
                int c = a + columns;
                int d = c + 1;
                // Across cross downstream points up on a horizontal surface and
                // toward the lower/front side on the falling surface.
                triangles[k++] = a; triangles[k++] = b; triangles[k++] = c;
                triangles[k++] = b; triangles[k++] = d; triangles[k++] = c;
            }

            var mesh = new Mesh { name = name };
            try
            {
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.uv2 = uv2;
                mesh.colors = colors;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (upper) UnityEngine.Object.DestroyImmediate(upper);
            if (fall) UnityEngine.Object.DestroyImmediate(fall);
            if (lower) UnityEngine.Object.DestroyImmediate(lower);
            upper = fall = lower = null;
        }
    }
}
