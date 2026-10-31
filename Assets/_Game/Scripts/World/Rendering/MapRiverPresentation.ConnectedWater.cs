using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    public sealed partial class MapRiverPresentation
    {
        void ConfigureConnectedRibbon(Mesh mesh, MapRiverPath path, int start, int end)
        {
            var vertices = new List<Vector3>(mesh.vertices); var normals = new List<Vector3>(mesh.normals);
            var uv = new List<Vector2>(mesh.uv); var colors = new List<Color>(mesh.colors); var data = new List<Vector4>();
            mesh.GetUVs(1, data); var triangles = new List<int>(mesh.triangles);
            float offset = session.RiverFlowTimeOffset(path.record.id);
            for (int i = 0; i < vertices.Count; i++)
            {
                path.Nearest(new Vector2(vertices[i].x, vertices[i].z), out var frame, out _);
                uv[i] = new Vector2((uv[i].x + 1) * .5f, (offset + path.TravelTime(frame.station)) * 4.5f);
                colors[i] = new Color(1, .17f, 1, 0);
            }
            // Explicit side walls follow the same sampled bed. Opaque banks hide their buried parts.
            const int across = 32;
            foreach (int edge in new[] { 0, across })
            {
                int first = vertices.Count;
                for (int row = 0; row <= end - start; row++)
                {
                    int top = row * (across + 1) + edge; Vector3 point = vertices[top]; var frame = path.frames[start + row];
                    Vector3 normal = new Vector3(frame.Left.x, 0, frame.Left.y) * (edge == 0 ? -1 : 1);
                    float bed = Mathf.Min(point.y, session.SampleHeight(point.x, point.z) + .018f);
                    for (int bottom = 0; bottom < 2; bottom++)
                    {
                        var p = point; if (bottom == 1) p.y = bed;
                        vertices.Add(p); normals.Add(normal); data.Add(data[top]); colors.Add(colors[top]);
                        uv.Add(new Vector2(edge == 0 ? .045f + bottom * .04f : .955f - bottom * .04f, uv[top].y));
                    }
                    if (row == 0) continue;
                    int a = first + (row - 1) * 2, b = a + 2;
                    if (edge == 0) { triangles.Add(a); triangles.Add(b); triangles.Add(a + 1); triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1); }
                    else { triangles.Add(a); triangles.Add(a + 1); triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b); }
                }
            }
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(1, data); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        }
    }
}
