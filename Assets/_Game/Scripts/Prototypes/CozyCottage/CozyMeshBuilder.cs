using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Prototypes
{
    // A small combined mesh, not hundreds of temporary cube GameObjects.
    // Every face has its own vertices so bevel-like faceting remains crisp.
    internal sealed class CozyMeshBuilder
    {
        internal const int MaterialCount = 10;
        private readonly List<Vector3> vertices = new List<Vector3>(16000);
        private readonly List<Vector2> uvs = new List<Vector2>(16000);
        private readonly List<int>[] triangles = new List<int>[MaterialCount];

        internal CozyMeshBuilder()
        {
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = new List<int>();
        }

        internal void Triangle(int material, Vector3 a, Vector3 b, Vector3 c)
        {
            int index = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            uvs.Add(Vector2.zero);
            uvs.Add(Vector2.right);
            uvs.Add(Vector2.up);
            triangles[material].Add(index);
            triangles[material].Add(index + 1);
            triangles[material].Add(index + 2);
        }

        internal void Quad(int material, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int index = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            var list = triangles[material];
            list.Add(index);
            list.Add(index + 1);
            list.Add(index + 2);
            list.Add(index);
            list.Add(index + 2);
            list.Add(index + 3);
        }

        internal void Box(int material, Vector3 center, Vector3 size)
        {
            Box(material, center, size, Quaternion.identity);
        }

        internal void Box(int material, Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 half = size * 0.5f;
            Vector3[] p = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var local = new Vector3(
                    (i & 1) == 0 ? -half.x : half.x,
                    (i & 2) == 0 ? -half.y : half.y,
                    (i & 4) == 0 ? -half.z : half.z);
                p[i] = center + rotation * local;
            }

            // Winding of outward-facing normals for each cube face.
            Quad(material, p[0], p[2], p[3], p[1]); // -Z
            Quad(material, p[5], p[7], p[6], p[4]); // +Z
            Quad(material, p[4], p[6], p[2], p[0]); // -X
            Quad(material, p[1], p[3], p[7], p[5]); // +X
            Quad(material, p[2], p[6], p[7], p[3]); // +Y
            Quad(material, p[4], p[0], p[1], p[5]); // -Y
        }

        internal void Beam(int material, Vector3 start, Vector3 end, float thickness)
        {
            Vector3 difference = end - start;
            if (difference.sqrMagnitude < 0.00001f) return;
            Box(material, (start + end) * 0.5f,
                new Vector3(thickness, difference.magnitude, thickness),
                Quaternion.FromToRotation(Vector3.up, difference.normalized));
        }

        internal Mesh ToMesh()
        {
            var mesh = new Mesh { name = "Cozy cottage prototype (generated)" };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++)
                mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }
    }
}
