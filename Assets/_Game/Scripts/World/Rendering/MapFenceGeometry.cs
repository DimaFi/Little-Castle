using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    /// <summary>CPU authoring deformation of the real straight authored meshes, not generated substitute boxes.</summary>
    public static class MapFenceGeometry
    {
        public sealed class Source
        {
            public readonly Vector3[] vertices,normals;
            public readonly Vector2[] uv;
            public readonly int[] triangles;
            public Source(Mesh mesh){vertices=mesh.vertices;normals=mesh.normals;uv=mesh.uv;triangles=mesh.triangles;}
        }
        public sealed class Buffer
        {
            public readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            public readonly List<Vector2> uv=new List<Vector2>();
            public readonly List<int> triangles=new List<int>();
            public Mesh ToMesh(string name)
            {
                var mesh=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};
                if(vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
                mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
            }
            internal void Finish(Source source,int offset)
            {foreach(int i in source.triangles)triangles.Add(offset+i);uv.AddRange(source.uv);}
        }
        public static void Post(Buffer output,Source source,MapFenceFrame frame,float height,Vector3 origin,float scale=1)
        {
            int offset=output.vertices.Count;var t=new Vector3(frame.tangent.x,0,frame.tangent.y);var side=new Vector3(-t.z,0,t.x);
            var center=new Vector3(frame.position.x,height,frame.position.y)-origin;
            for(int i=0;i<source.vertices.Length;i++)
            {
                var p=source.vertices[i];var n=source.normals[i];
                output.vertices.Add(center+(t*p.x+Vector3.up*p.y+side*p.z)*scale);
                output.normals.Add((t*n.x+Vector3.up*n.y+side*n.z).normalized);
            }
            output.Finish(source,offset);
        }
        public static void Rail(Buffer output,Source source,MapFencePath path,float from,float to,float mountingHeight,Func<float,float,float> height,Vector3 origin)
        {
            int offset=output.vertices.Count;var a=path.At(from).position;var b=path.At(to).position;
            float y0=height(a.x,a.y),y1=height(b.x,b.y),length=to-from;
            float stretch=length/MapFencePath.NativeRailLength,grade=(y1-y0)/Mathf.Max(.0001f,length);
            float scale=path.record.scale;
            for(int i=0;i<source.vertices.Length;i++)
            {
                var p=source.vertices[i];var n=source.normals[i];float u=Mathf.Clamp01(p.x/MapFencePath.NativeRailLength);
                var frame=path.At(Mathf.Lerp(from,to,u));var t=new Vector3(frame.tangent.x,0,frame.tangent.y);var side=new Vector3(-t.z,0,t.x);
                var center=new Vector3(frame.position.x,Mathf.Lerp(y0,y1,u)+mountingHeight*scale,frame.position.y)-origin;
                output.vertices.Add(center+(Vector3.up*p.y+side*p.z)*scale);
                // Inverse-transpose of the local curve/stretch/grade Jacobian.
                output.normals.Add((t*(n.x/Mathf.Max(.0001f,stretch)-grade*n.y/scale)+(Vector3.up*n.y+side*n.z)/scale).normalized);
            }
            output.Finish(source,offset);
        }
    }
}
