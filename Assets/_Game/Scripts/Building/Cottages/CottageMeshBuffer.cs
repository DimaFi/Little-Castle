using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Building.Cottages
{
    // Meshes derived from a plan are owned by its presenter, never save data.
    internal sealed class CottageMeshBuffer
    {
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector3> normals=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<int> triangles=new List<int>();
        public int Count=>vertices.Count;
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 n,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 ud)
        {
            int k=vertices.Count;
            vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            for(int i=0;i<4;i++) normals.Add(n);
            uv.Add(ua);uv.Add(ub);uv.Add(uc);uv.Add(ud);
            // Unity surface winding matches the authored outward normal.
            bool reverse=Vector3.Dot(Vector3.Cross(b-a,c-a),n)<0;
            if(Vector3.Cross(b-a,c-a).sqrMagnitude>1e-12f){triangles.Add(k);triangles.Add(k+(reverse?2:1));triangles.Add(k+(reverse?1:2));}
            if(Vector3.Cross(c-a,d-a).sqrMagnitude>1e-12f){triangles.Add(k);triangles.Add(k+(reverse?3:2));triangles.Add(k+(reverse?2:3));}
        }
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
        { float w=(b-a).magnitude,h=(d-a).magnitude; Quad(a,b,c,d,normal,new Vector2(0,0),new Vector2(w,0),new Vector2(w,h),new Vector2(0,h)); }
        public void Box(Vector3 centre,Vector3 size,Quaternion rotation)
        {
            Vector3 half=size*.5f;
            Vector3[] v={new Vector3(-half.x,-half.y,-half.z),new Vector3(half.x,-half.y,-half.z),new Vector3(half.x,half.y,-half.z),new Vector3(-half.x,half.y,-half.z),new Vector3(-half.x,-half.y,half.z),new Vector3(half.x,-half.y,half.z),new Vector3(half.x,half.y,half.z),new Vector3(-half.x,half.y,half.z)};
            for(int i=0;i<8;i++)v[i]=centre+rotation*v[i];
            Quad(v[0],v[1],v[2],v[3],rotation*Vector3.back);
            Quad(v[5],v[4],v[7],v[6],rotation*Vector3.forward);
            Quad(v[4],v[0],v[3],v[7],rotation*Vector3.left);
            Quad(v[1],v[5],v[6],v[2],rotation*Vector3.right);
            Quad(v[3],v[2],v[6],v[7],rotation*Vector3.up);
            Quad(v[4],v[5],v[1],v[0],rotation*Vector3.down);
        }
        public void Beam(Vector3 a,Vector3 b,float width)
        { Vector3 delta=b-a; Box((a+b)*.5f,new Vector3(width,delta.magnitude,width),Quaternion.FromToRotation(Vector3.up,delta.normalized)); }
        public void Append(Mesh mesh,Matrix4x4 matrix)
        {
            int offset=vertices.Count; var vv=mesh.vertices;var nn=mesh.normals;var uu=mesh.uv;
            Matrix4x4 normalMatrix=matrix.inverse.transpose;
            for(int i=0;i<vv.Length;i++){vertices.Add(matrix.MultiplyPoint3x4(vv[i]));normals.Add(nn.Length==vv.Length?normalMatrix.MultiplyVector(nn[i]).normalized:Vector3.up);uv.Add(uu.Length==vv.Length?uu[i]:Vector2.zero);}
            var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3){triangles.Add(offset+indices[i]);triangles.Add(offset+indices[i+(matrix.determinant<0?2:1)]);triangles.Add(offset+indices[i+(matrix.determinant<0?1:2)]);}
        }
        public Mesh Build(string name)
        {
            var mesh=new Mesh{name=name,indexFormat=vertices.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
    }
}
