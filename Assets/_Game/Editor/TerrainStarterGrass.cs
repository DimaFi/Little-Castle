using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace LittleCastle.Editor
{
    public static class TerrainStarterGrass
    {
        // Five tapered geometric blades per clump, UV Y 0 at root -> 1 at tip.
        // Combined per patch: no per-blade objects, colliders or shadow casters.
        public static Mesh Build(TerrainStarterPlan plan,int cx,int cz,int candidates=6400)
        {
            var random=new System.Random(plan.Seed ^ (cx*73856093) ^ (cz*19349663));
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var normals=new List<Vector3>(); var triangles=new List<int>();
            var roots=new List<Vector3>();
            for(int i=0;i<candidates;i++)
            {
                float x=cx*24+(float)random.NextDouble()*24;
                float z=cz*24+(float)random.NextDouble()*24;
                float path=plan.PathMask(x,z);
                if(path>.5f || (path>.06f && random.NextDouble()<path*1.5f)) continue;
                bool blocked=false;
                foreach(var h in plan.Houses) if(Mathf.Abs(x-h.x)<4.1f && Mathf.Abs(z-h.z)<3.5f) blocked=true;
                if(!plan.ArtStudy && Mathf.Abs(z+17)<.85f && Mathf.Abs(x)<19) blocked=true;
                if(plan.ArtStudy && ((Mathf.Abs(z+11.8f)<.6f && x>-17.5f && x<-2) ||
                    (Mathf.Abs(x+17.5f)<.6f && z>-11.8f && z<-3))) blocked=true;
                if(Vector2.Distance(new Vector2(x,z),plan.ArtStudy ? new Vector2(-2,-7) : new Vector2(-4,4))<1.3f) blocked=true;
                if(blocked) continue;
                float contact=plan.ContactShade(x,z);
                float patch=(.82f+.18f*Mathf.Sin(x*.4f)*Mathf.Cos(z*.3f))*Mathf.Lerp(1,.55f,contact);
                for(int b=0;b<5;b++)
                {
                    float angle=(float)random.NextDouble()*Mathf.PI*2;
                    float height=(.22f+(float)random.NextDouble()*.24f)*patch;
                    Vector3 side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.055f;
                    Vector3 p=new Vector3(x+(float)random.NextDouble()*.35f,0,z+(float)random.NextDouble()*.35f);
                    p.y=plan.Height(p.x,p.z)-.015f;
                    int n=vertices.Count;
                    vertices.Add(p-side); vertices.Add(p+side); vertices.Add(p+Vector3.up*height+side*.9f);
                    roots.Add(p); roots.Add(p); roots.Add(p);
                    uv.Add(new Vector2(0,0)); uv.Add(new Vector2(1,0)); uv.Add(new Vector2(.5f,1));
                    normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up);
                    triangles.Add(n); triangles.Add(n+1); triangles.Add(n+2);
                }
            }
            var mesh=new Mesh {name=$"Grass_{cx}_{cz}",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetNormals(normals); mesh.SetTriangles(triangles,0);
            mesh.SetUVs(1,roots);
            mesh.RecalculateBounds(); var bounds=mesh.bounds; bounds.Expand(.6f); mesh.bounds=bounds;
            return mesh;
        }
    }
}
