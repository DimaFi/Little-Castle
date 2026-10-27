using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    [InitializeOnLoad]
    public static class GroundBiomeVegetationPreflight
    {
        public const string Study = "Assets/_Game/Art/Studies/InnerverseStarter_20261023";
        public const string Root = "Assets/_Game/Art/Studies/GroundBiomes_Pass02";
        public static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Research/Ground_Biomes_URP_2026-10-25/Pass02_PNG"));
        const string Key = "LC_GroundPass02_Preflight";
        static int phase;
        static double next;
        static GroundBiomeVegetationPreflight() { EditorApplication.update += Tick; }
        public static GameObject Source(string name) => AssetDatabase.FindAssets(name + " t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileNameWithoutExtension(p) == name)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>).First();
        public static void Run()
        {
            if (!Path.GetFileName(Path.GetDirectoryName(Application.dataPath)).StartsWith("Little-Castle_URP_")) throw new Exception("URP copy only.");
            for(int i=0;i<EditorSceneManager.sceneCount;i++) if(EditorSceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene preserved.");
            Directory.CreateDirectory(Output); Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            var log = new StringBuilder();
            Application.LogCallback collect=(m,s,t)=> { if(m.Contains("[Little Castle Foliage]"))log.AppendLine(t+": "+m); };
            Application.logMessageReceived += collect;
            try
            {
                var production=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Models/Ready/Test_Vegetation/VEG_Tree_01/VEG_Tree_01.prefab");
                Selection.activeGameObject=production; FoliageModelCompatibilityValidator.ValidateSelected(); Audit(production,log);
                EditorSceneManager.OpenScene(InnerverseStarterStudyScene.ScenePath);
                var tree=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).First(t=>t.name=="UNS_Spruce_01").gameObject;
                Selection.activeGameObject=tree; FoliageModelCompatibilityValidator.ValidateSelected(); Audit(tree,log);
                var grass=(GameObject)PrefabUtility.InstantiatePrefab(Source("UNS_Grass"));
                foreach(var renderer in grass.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Study+"/Study_UNS_Grass.mat");
                Selection.activeGameObject=grass;FoliageModelCompatibilityValidator.ValidateSelected();Audit(grass,log);
                UnityEngine.Object.DestroyImmediate(grass);
                // Reopen the original study to discard temporary inspection objects.
                EditorSceneManager.OpenScene(InnerverseStarterStudyScene.ScenePath);
                File.WriteAllText(Path.Combine(Output,"foliage_preflight.txt"),log.ToString());
                SessionState.SetBool(Key,true);phase=0;next=EditorApplication.timeSinceStartup+8;EditorApplication.isPlaying=true;
            }
            finally {Application.logMessageReceived-=collect;}
        }
        static void Audit(GameObject obj,StringBuilder log)
        {
            log.AppendLine("ACTUAL ASSET="+obj.name);
            foreach(var filter in obj.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;if(!mesh)continue;
                log.AppendLine(filter.name+": vertices="+mesh.vertexCount+" submeshes="+mesh.subMeshCount+" bounds="+mesh.bounds+" normals="+mesh.HasVertexAttribute(VertexAttribute.Normal)+" tangents="+mesh.HasVertexAttribute(VertexAttribute.Tangent)+" uv0="+mesh.HasVertexAttribute(VertexAttribute.TexCoord0)+" colors="+mesh.HasVertexAttribute(VertexAttribute.Color));
                var renderer=filter.GetComponent<Renderer>();
                if(renderer) for(int sub=0;sub<renderer.sharedMaterials.Length;sub++)
                {
                    var m=renderer.sharedMaterials[sub];if(!m)continue;
                    log.AppendLine("  submesh "+sub+" -> "+m.name+" / "+m.shader.name+" instancing="+m.enableInstancing);
                }
                try
                {
                    var vertices=mesh.vertices;var uv=mesh.uv;var colors=mesh.colors;
                    if(colors.Length>0)log.AppendLine("  wind R range="+colors.Min(c=>c.r)+".."+colors.Max(c=>c.r));
                    if(uv.Length>0)log.AppendLine("  UV0.y range="+uv.Min(u=>u.y)+".."+uv.Max(u=>u.y));
                    if(vertices.Length>0)log.AppendLine("  actual Y range="+vertices.Min(v=>v.y)+".."+vertices.Max(v=>v.y));
                }
                catch(Exception e){log.AppendLine("  CPU data unavailable: "+e.Message);}
            }
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(phase<3 && EditorApplication.isPlaying)
                {
                    var camera=Camera.main; camera.GetComponent<LittleCastle.CameraSystem.StrategyCameraController>().enabled=false;
                    var eye=phase<2?new Vector3(-34,7,9):new Vector3(33,43,-58);
                    var target=phase<2?new Vector3(-36,4,22):new Vector3(0,2,16);
                    camera.transform.position=eye;camera.transform.LookAt(target);
                    UnityEngine.Object.FindAnyObjectByType<LittleCastle.Rendering.StylizedLightingGlobals>()?.ApplyGlobals();
                    Capture(camera,Path.Combine(Output,new[]{"preflight_close.png","preflight_close_later.png","preflight_far.png"}[phase]));
                    phase++;next=EditorApplication.timeSinceStartup+2;
                    if(phase==3)EditorApplication.isPlaying=false;
                }
                else if(phase==3&&!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetBool(Key,false);File.WriteAllText(Path.Combine(Output,"preflight_playmode.txt"),"Play Mode captures completed; inspect close, later and far. No foliage shader/model source was changed.");
                    if(Application.isBatchMode)EditorApplication.Exit(0);
                }
            }
            catch(Exception ex){SessionState.SetBool(Key,false);File.WriteAllText(Path.Combine(Output,"preflight_failed.txt"),ex.ToString());Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(2);}
        }
        public static void Capture(Camera camera,string path)
        {
            var rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            try{for(int frame=0;frame<2;frame++){UnityEngine.Object.FindAnyObjectByType<LittleCastle.Rendering.StylizedLightingGlobals>()?.ApplyGlobals();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});}RenderTexture.active=rt;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        }
    }
}
