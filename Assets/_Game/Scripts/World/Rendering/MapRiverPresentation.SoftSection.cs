using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    public sealed partial class MapRiverPresentation
    {
        void ConfigureSoftRibbon(Mesh mesh,MapRiverPath path,int start,int end)
        {
            var vertices=new List<Vector3>(mesh.vertices);var normals=new List<Vector3>(mesh.normals);
            var uv=new List<Vector2>(mesh.uv);var colors=new List<Color>(mesh.colors);var data=new List<Vector4>();mesh.GetUVs(1,data);
            int count=vertices.Count;var top=mesh.triangles;var indices=new List<int>(top);
            float offset=session.RiverFlowTimeOffset(path.record.id);
            for(int i=0;i<count;i++)
            {
                var p=vertices[i];path.Nearest(new Vector2(p.x,p.z),out var f,out _);
                uv[i]=new Vector2((uv[i].x+1)*.5f,(offset+path.TravelTime(f.station))*4.5f);
                colors[i]=new Color(1,.17f,1,0);
                p.y=Mathf.Min(p.y,session.SampleHeight(p.x,p.z));
                vertices.Add(p);normals.Add(-session.SampleNormal(p.x,p.z));uv.Add(uv[i]);colors.Add(colors[i]);data.Add(data[i]);
            }
            var edges=new Dictionary<(int,int),(int a,int b,int count)>();
            void Edge(int a,int b)
            {var key=(Math.Min(a,b),Math.Max(a,b));if(edges.TryGetValue(key,out var e))edges[key]=(e.a,e.b,e.count+1);else edges.Add(key,(a,b,1));}
            for(int i=0;i<top.Length;i+=3)
            {
                int a=top[i],b=top[i+1],c=top[i+2];indices.Add(a+count);indices.Add(c+count);indices.Add(b+count);
                Edge(a,b);Edge(b,c);Edge(c,a);
            }
            int lastRow=(end-start)*33;
            foreach(var e in edges.Values)if(e.count==1)
            {
                // Adjacent resident tiles meet without a second internal transparent end face.
                bool first=e.a<33&&e.b<33,last=e.a>=lastRow&&e.a<lastRow+33&&e.b>=lastRow&&e.b<lastRow+33;
                if(first&&start!=0||last&&end!=path.frames.Count-1)continue;
                if(first&&start==0&&session.RiverNetwork.HasRoutedEndpoint(path.record.id,true)||last&&end==path.frames.Count-1&&session.RiverNetwork.HasRoutedEndpoint(path.record.id,false))continue;
                indices.Add(e.a);indices.Add(e.a+count);indices.Add(e.b);
                indices.Add(e.b);indices.Add(e.a+count);indices.Add(e.b+count);
            }
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetUVs(1,data);mesh.SetColors(colors);mesh.SetTriangles(indices,0);
        }
    }
}
