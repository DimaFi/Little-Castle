using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    public sealed partial class MapRiverPresentation
    {
        static void ConfigureWaveEnvelope(Mesh mesh,MapRiverPath path)
        {
            var vertices=mesh.vertices;var normals=mesh.normals;var data=new List<Vector4>();mesh.GetUVs(1,data);
            var envelope=new List<Vector4>(vertices.Length);
            for(int i=0;i<vertices.Length;i++)
            {
                float weight=0;
                if(normals[i].y>.5f&&path.Nearest(new Vector2(vertices[i].x,vertices[i].z),out var f,out _)
                    &&Mathf.Abs(vertices[i].y-f.water-.018f)<.01f)
                {
                    // Both free caps and connected ends remain exactly stationary.
                    float end=Mathf.Min(f.station,path.Length-f.station);
                    weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(end/2))*Mathf.SmoothStep(0,1,Mathf.Clamp01(data[i].x/.2f));
                }
                envelope.Add(new Vector4(0,1,0,weight));
            }
            mesh.SetUVs(2,envelope);var bounds=mesh.bounds;bounds.Expand(Vector3.up*(MapRiverWaves.MaximumDisplacement*2));mesh.bounds=bounds;
        }
    }
}
