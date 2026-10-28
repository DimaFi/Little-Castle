using System;
using System.IO;
using System.Linq;
using System.Text;
using LittleCastle.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    public static class WorldIntegrationPreflight
    {
        public const string Evidence = "E:/Games_Develop/Research/WorldIntegration_20261027/Preflight";
        public const string Meadow = "Assets/_Game/Art/Studies/Meadow_20261025";
        public const string Valley = "Assets/_Game/Art/Studies/ValleyArt_Pass03";
        public static void Preflight()
        {
            Directory.CreateDirectory(Evidence);
            var log = new StringBuilder();
            Application.LogCallback collect = (m,s,t) => { if(m.Contains("[Little Castle Foliage]")) log.AppendLine(t+": "+m); };
            Application.logMessageReceived += collect;
            try
            {
                foreach(char letter in "ABCD")
                {
                    var path=Valley+"/Oak_"+letter+"_Study.prefab";
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    Selection.activeGameObject=instance;
                    FoliageModelCompatibilityValidator.ValidateSelected();
                    log.AppendLine("OAK="+path);
                    foreach(var mf in instance.GetComponentsInChildren<MeshFilter>(true))Describe(log,mf.sharedMesh,mf.GetComponent<Renderer>());
                    UnityEngine.Object.DestroyImmediate(instance);
                }
                var data=AssetDatabase.LoadAssetAtPath<MeadowGrassData>(Meadow+"/Meadow_InstanceData.asset");
                foreach(var mesh in data.lodMeshes)
                {
                    var obj=new GameObject("Exact native Meadow preflight",typeof(MeshFilter),typeof(MeshRenderer));
                    obj.GetComponent<MeshFilter>().sharedMesh=mesh;
                    obj.GetComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Meadow+"/Meadow_Blades.mat");
                    Selection.activeGameObject=obj;FoliageModelCompatibilityValidator.ValidateSelected();Describe(log,mesh,obj.GetComponent<Renderer>());
                    UnityEngine.Object.DestroyImmediate(obj);
                }
                File.WriteAllText(Evidence+"/preflight.txt",log.ToString());
                Debug.Log("LIGHTING_MEADOW_PREFLIGHT_COMPLETE");
                if(Application.isBatchMode)UrpBatchExit.WhenIdle(0);
            }
            catch(Exception e){File.WriteAllText(Evidence+"/preflight-error.txt",e.ToString());Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(2);}
            finally{Application.logMessageReceived-=collect;}
        }
        static void Describe(StringBuilder log,Mesh mesh,Renderer renderer)
        {
            log.AppendLine($"Mesh={AssetDatabase.GetAssetPath(mesh)} / {mesh.name}; vertices={mesh.vertexCount}; submeshes={mesh.subMeshCount}; bounds={mesh.bounds}");
            foreach(VertexAttribute attr in new[]{VertexAttribute.Normal,VertexAttribute.Tangent,VertexAttribute.Color,VertexAttribute.TexCoord0,VertexAttribute.TexCoord1})log.AppendLine(attr+"="+mesh.HasVertexAttribute(attr));
            log.AppendLine("Materials="+string.Join(";",renderer.sharedMaterials.Select(m=>m.name+":"+m.shader.name)));
            using(var read=Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var vertices=new Unity.Collections.NativeArray<Vector3>(mesh.vertexCount,Unity.Collections.Allocator.Temp);
                var uv=new Unity.Collections.NativeArray<Vector2>(mesh.vertexCount,Unity.Collections.Allocator.Temp);
                read[0].GetVertices(vertices);read[0].GetUVs(0,uv);
                float meanY=0,meanUV=0;for(int i=0;i<vertices.Length;i++){meanY+=vertices[i].y;meanUV+=uv[i].y;}meanY/=vertices.Length;meanUV/=vertices.Length;
                float cov=0,vy=0,vu=0;for(int i=0;i<vertices.Length;i++){float y=vertices[i].y-meanY,u=uv[i].y-meanUV;cov+=y*u;vy+=y*y;vu+=u*u;}
                log.AppendLine("UVY-height correlation="+cov/Mathf.Sqrt(Mathf.Max(1e-8f,vy*vu)));
                vertices.Dispose();uv.Dispose();
            }
        }
    }
}
