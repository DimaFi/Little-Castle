using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    public static class MeadowGrassPreflight
    {
        public static string Output => Path.GetFullPath(Application.dataPath + "/../../Research/Meadow_Grass_2026-10-25");
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var log = new StringBuilder();
            Application.LogCallback collect = (m,s,t) => { if(m.Contains("[Little Castle Foliage]")) log.AppendLine(t+": "+m); };
            Application.logMessageReceived += collect;
            try
            {
                foreach(string name in new[]{"UNS_Grass","VEG_Grass_Short_A","VEG_Grass_Short_B","VEG_Grass_Tall_A","VEG_FlowerCluster_A","VEG_FlowerCluster_B"})
                {
                    var source = GroundBiomeVegetationPreflight.Source(name);
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    Selection.activeGameObject = obj;
                    FoliageModelCompatibilityValidator.ValidateSelected();
                    log.AppendLine("ASSET="+AssetDatabase.GetAssetPath(source));
                    foreach(var mf in obj.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var mesh = mf.sharedMesh;
                        // Editor-only readback. Source import settings stay unchanged.
                        using(var data = Mesh.AcquireReadOnlyMeshData(mesh))
                        {
                            var vertices = new Unity.Collections.NativeArray<Vector3>(mesh.vertexCount,Unity.Collections.Allocator.Temp);
                            data[0].GetVertices(vertices);
                            var actual=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)actual.Encapsulate(v);
                            log.AppendLine("  Actual local bounds="+actual+"; transform="+mf.transform.localToWorldMatrix);
                            var uvs = new Unity.Collections.NativeArray<Vector2>(mesh.vertexCount,Unity.Collections.Allocator.Temp);
                            bool hasUV = mesh.HasVertexAttribute(VertexAttribute.TexCoord0);
                            if(hasUV) data[0].GetUVs(0,uvs);
                            float sumY=0,sumU=0;
                            for(int i=0;i<vertices.Length;i++){sumY+=vertices[i].y;sumU+=uvs[i].y;}
                            float yMean=sumY/vertices.Length,uMean=sumU/vertices.Length,cov=0,vy=0,vu=0;
                            for(int i=0;i<vertices.Length;i++){float y=vertices[i].y-yMean,u=uvs[i].y-uMean;cov+=y*u;vy+=y*y;vu+=u*u;}
                            log.AppendLine($"{mf.name}: vertices={mesh.vertexCount}; tris={mesh.GetIndexCount(0)/3}; bounds={mesh.bounds}; normals={mesh.HasVertexAttribute(VertexAttribute.Normal)}; uv0={hasUV}; UVY/height correlation={cov/Mathf.Sqrt(Mathf.Max(1e-8f,vy*vu)):F3}");
                            vertices.Dispose();uvs.Dispose();
                        }
                    }
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
            finally { Application.logMessageReceived -= collect; File.WriteAllText(Path.Combine(Output,"source_preflight.txt"),log.ToString()); }
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
    }
}
