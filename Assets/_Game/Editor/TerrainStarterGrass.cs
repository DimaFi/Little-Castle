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
            void AddClump(float x,float z)
            {
                if(x<cx*24 || x>=(cx+1)*24 || z<cz*24 || z>=(cz+1)*24) return;
                float path=plan.PathMask(x,z);
                if(path>.5f || (path>.06f && random.NextDouble()<path*1.5f)) return;
                // Any placed house uses the same rotated footprint and entrance.
                // The additional perimeter pass fills the former bare rectangle.
                if(plan.BuildingDistance(x,z)<.08f || plan.IsBuildingEntrance(x,z)) return;
                bool blocked=false;
                if(!plan.ArtStudy && Mathf.Abs(z+17)<.85f && Mathf.Abs(x)<19) blocked=true;
                if(plan.ArtStudy && ((Mathf.Abs(z+11.8f)<.6f && x>-17.5f && x<-2) ||
                    (Mathf.Abs(x+17.5f)<.6f && z>-11.8f && z<-3))) blocked=true;
                if(Vector2.Distance(new Vector2(x,z),plan.ArtStudy ? new Vector2(-2,-7) : new Vector2(-4,4))<1.3f) blocked=true;
                if(blocked) return;
                float contact=plan.ContactShade(x,z);
                float patch=(.82f+.18f*Mathf.Sin(x*.4f)*Mathf.Cos(z*.3f))*Mathf.Lerp(1,.82f,contact);
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
            for(int i=0;i<candidates;i++)
                AddClump(cx*24+(float)random.NextDouble()*24,
                    cz*24+(float)random.NextDouble()*24);
            int edgeCandidates=Mathf.RoundToInt(candidates*.035f);
            foreach(var stamp in plan.BuildingStamps)
            {
                Vector2 delta=stamp.center-new Vector2(cx*24+12,cz*24+12);
                if(Mathf.Abs(delta.x)>stamp.halfExtents.x+14 ||
                    Mathf.Abs(delta.y)>stamp.halfExtents.y+14) continue;
                for(int i=0;i<edgeCandidates;i++)
                {
                    Vector2 p=stamp.PerimeterPoint((i+(float)random.NextDouble())/edgeCandidates,
                        .14f+(float)random.NextDouble()*.65f);
                    AddClump(p.x,p.y);
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
