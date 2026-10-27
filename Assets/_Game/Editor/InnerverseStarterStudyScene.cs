using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    [InitializeOnLoad]
    public static class InnerverseStarterStudyScene
    {
        private const string StudyRoot = "Assets/_Game/Art/Studies/InnerverseStarter_20261023";
        public const string ScenePath = "Assets/_Game/Scenes/InnerverseStarterStudy_20261023.unity";
        private static double nextCheck;
        static InnerverseStarterStudyScene() { EditorApplication.update += Check; }
        private static void Check()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 2;
            string request = Path.Combine(InnerverseStarterResearch.Output, "scene.request");
            if (!File.Exists(request)) return;
            string command = File.ReadAllText(request).Trim();
            File.Move(request, request + ".started." + DateTime.UtcNow.Ticks);
            try { if (command == "refresh") RefreshStudy(); else Build(); }
            catch (Exception e) { File.WriteAllText(Path.Combine(InnerverseStarterResearch.Output, "scene.error.txt"), e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Little Castle/Research/Innerverse Starter/Build and Open Grass Study Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before building a study scene.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene exists. Review is stopped to preserve it; save it manually before building the study.");
            if (File.Exists(ScenePath)) throw new IOException("Study scene already exists; preserving it. Open the existing review scene.");
            Directory.CreateDirectory(StudyRoot);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Innerverse Starter — Grass / Mesh Terrain Study";
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.38f,.48f,.6f);
            RenderSettings.ambientEquatorColor = new Color(.3f,.36f,.31f);
            RenderSettings.ambientGroundColor = new Color(.18f,.21f,.13f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            var sunGo = new GameObject("Study Sun");
            var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional;
            sun.color = new Color(1,.96f,.86f); sun.intensity = .85f; sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(48,-35,0); RenderSettings.sun = sun;
            var globals = new GameObject("Shared Little Castle Lighting").AddComponent<LittleCastle.Rendering.StylizedLightingGlobals>();
            globals.ApplyGlobals();

            string package = AssetDatabase.GetAllAssetPaths().First(p=>p.EndsWith("/Documentation/UNS_Readme.txt")).Replace("/Documentation/UNS_Readme.txt", "");
            string[] paths = AssetDatabase.FindAssets("t:Prefab",new[]{package}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(paths.First(p=>Path.GetFileNameWithoutExtension(p)==name));
            var materials = new List<Material>();
            var materialCache = new Dictionary<Material,Material>();
            Material Adapt(Material source, string path)
            {
                if (materialCache.TryGetValue(source, out var existing)) return existing;
                var adapted = InnerverseStarterResearch.AdaptMaterial(source,path,materials);
                AssetDatabase.CreateAsset(adapted, StudyRoot+"/"+adapted.name+".mat");
                materialCache.Add(source, adapted); return adapted;
            }
            Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAllAssetPaths().First(p=>p.StartsWith(package)&&Path.GetFileName(p)==name+".png"));
            var terrain = new Material(Shader.Find("Little Castle/Terrain/LC Terrain")) { name = "Study_UNS_MeshTerrain", enableInstancing = true };
            terrain.SetTexture("_GrassTex",Texture("UNS_Terrain_Grass")); terrain.SetTexture("_DirtTex",Texture("UNS_Terrain_Dirt"));
            terrain.SetTexture("_RockTex",Texture("UNS_Terrain_Dirt"));
            terrain.SetColor("_GrassColor",new Color(.85f,.88f,.8f)); terrain.SetColor("_DirtColor",new Color(1.12f,1.05f,.9f));
            terrain.SetFloat("_WorldTiling",.5f); terrain.SetFloat("_UseVertexMasks",1); terrain.SetFloat("_MacroStrength",.13f);
            AssetDatabase.CreateAsset(terrain,StudyRoot+"/Study_UNS_MeshTerrain.mat");
            var ground = new GameObject("Mesh terrain — fixed study geometry, grass/dirt masks");
            var mesh = BuildGround(); AssetDatabase.CreateAsset(mesh,StudyRoot+"/Study_Ground.asset");
            ground.AddComponent<MeshFilter>().sharedMesh=mesh; ground.AddComponent<MeshRenderer>().sharedMaterial=terrain;
            ground.AddComponent<MeshCollider>().sharedMesh=mesh;

            var sourceGrass=Prefab("UNS_Grass");
            var lods=sourceGrass.GetComponentInChildren<LODGroup>().GetLODs();
            var grassMaterial=Adapt(lods[0].renderers[0].sharedMaterial,paths.First(p=>p.EndsWith("/UNS_Grass.prefab")));
            grassMaterial.SetColor("_Color",new Color(.30f,.44f,.10f));
            grassMaterial.SetColor("_RootColor",new Color(.12f,.20f,.045f));
            grassMaterial.SetColor("_TipColor",new Color(.46f,.58f,.17f));
            grassMaterial.SetFloat("_WindAmplitude",.10f); grassMaterial.SetFloat("_MicroFlutter",.025f);
            var grass=new GameObject("Grass — fixed authored positions, GPU batches, 4 distance LODs").AddComponent<InnerverseGrassStudy>();
            grass.lodMeshes=lods.Select(l=>l.renderers[0].GetComponent<MeshFilter>().sharedMesh).ToArray();
            grass.grassMaterial=grassMaterial;
            var matrices=CreateGrassMatrices();
            grass.authoredMatrices=matrices.ToArray();
            void Place(string name, Vector3 point, float scale=1, float yaw=0)
            {
                var prefab=Prefab(name); var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                obj.transform.position=new Vector3(point.x,Height(point.x,point.z)+point.y,point.z);
                obj.transform.localScale=Vector3.one*scale; obj.transform.rotation=Quaternion.Euler(0,yaw,0);
                foreach(var renderer in obj.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>Adapt(m,paths.First(p=>p.EndsWith("/"+name+".prefab")))).ToArray();
                foreach(var collider in obj.GetComponentsInChildren<Collider>(true)) if(collider.isTrigger) UnityEngine.Object.DestroyImmediate(collider);
            }
            for(int i=0;i<12;i++) Place(i%3==0?"UNS_Spruce_01":"UNS_Spruce_02",new Vector3(-39+i*7,0,21+(i%3)*3),i%3==0?.7f:1.1f,i*37);
            for(int i=0;i<5;i++) Place("UNS_Rock_Cliff_0"+(i+1),new Vector3(-30+i*15,-.25f,30),1.3f,i*19);
            for(int i=0;i<10;i++)Place("UNS_Standard_Rock_0"+(i%5+1),new Vector3(-35+i*8,-.12f,15),.7f,i*43);
            for(int i=0;i<12;i++)Place("UNS_Bush",new Vector3(-37+i*6.5f,0,13+i%2*2),.8f,i*61);
            for(int i=0;i<9;i++)Place("UNS_Flower",new Vector3(-27+i*8,0,-14),1,i*23);
            var cameraGo=new GameObject("Study Strategy Camera"); var camera=cameraGo.AddComponent<Camera>(); camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.34f,.46f,.57f); camera.fieldOfView=50; camera.nearClipPlane=.1f;camera.farClipPlane=500;
            camera.transform.position=new Vector3(33,29,-43); camera.transform.LookAt(new Vector3(0,0,4));
            cameraGo.AddComponent<LittleCastle.CameraSystem.StrategyCameraController>();
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
            RefreshStudy();
        }
        private static List<Matrix4x4> CreateGrassMatrices()
        {
            var matrices=new List<Matrix4x4>();
            for(int plot=0;plot<3;plot++) for(int x=0;x<26;x++) for(int z=0;z<29;z++)
            {
                float density = new[]{.35f,.65f,.95f}[plot];
                if(DeterministicHash.Hash01(plot,x,z,1)>density)continue;
                float px=-35+plot*25+x*.78f+(DeterministicHash.Hash01(plot,x,z,2)-.5f)*.5f;
                float pz=-11+z*.78f+(DeterministicHash.Hash01(plot,x,z,3)-.5f)*.5f;
                if(PathMask(px,pz)>.1f)continue;
                float scale=Mathf.Lerp(.65f,.95f,DeterministicHash.Hash01(plot,x,z,4));
                matrices.Add(Matrix4x4.TRS(new Vector3(px,Height(px,pz)+.025f,pz),Quaternion.Euler(0,360*DeterministicHash.Hash01(plot,x,z,5),0),Vector3.one*scale));
            }
            return matrices;
        }
        [MenuItem("Little Castle/Research/Innerverse Starter/Refresh Study and Capture Images")]
        public static void RefreshStudy()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.sceneCount != 1)
                throw new InvalidOperationException("Refresh the study in Edit Mode with only this scene open, so shared lighting remains isolated.");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=ScenePath)throw new InvalidOperationException("Open the saved Innerverse study scene before refreshing it.");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(StudyRoot+"/Study_Ground.asset");
            mesh.colors=mesh.vertices.Select(p=>new Color(PathMask(p.x,p.z),0,0,1)).ToArray();
            EditorUtility.SetDirty(mesh);
            var terrain=AssetDatabase.LoadAssetAtPath<Material>(StudyRoot+"/Study_UNS_MeshTerrain.mat");
            terrain.SetColor("_GrassColor",new Color(.88f,.78f,1.5f));
            terrain.SetFloat("_MacroStrength",.18f);
            EditorUtility.SetDirty(terrain);
            var roots=scene.GetRootGameObjects();
            var grass=roots.SelectMany(r=>r.GetComponentsInChildren<InnerverseGrassStudy>(true)).Single();
            grass.authoredMatrices=CreateGrassMatrices().ToArray();EditorUtility.SetDirty(grass);
            var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Single(c=>c.name=="Study Strategy Camera");
            var globals=roots.SelectMany(r=>r.GetComponentsInChildren<LittleCastle.Rendering.StylizedLightingGlobals>(true)).Single();
            // Creating an SRP instance on its first request can reset shader state.
            // Initialize it before applying the shared LC globals and recording evidence.
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var warmup = RenderTexture.GetTemporary(64,64,24,RenderTextureFormat.ARGB32);
                var previousTarget = camera.targetTexture;
                try { camera.targetTexture = warmup; Render(camera, warmup); }
                finally { camera.targetTexture = previousTarget; RenderTexture.ReleaseTemporary(warmup); }
            }
            globals.ApplyGlobals();
            Capture(camera,new Vector3(33,29,-43),new Vector3(0,0,4),"study_overview.png");
            Capture(camera,new Vector3(11,4,-10),new Vector3(7,.2f,1),"study_grass_close.png");
            Capture(camera,new Vector3(34,52,-62),new Vector3(0,0,4),"study_far.png");
            camera.transform.position=new Vector3(33,29,-43); camera.transform.LookAt(new Vector3(0,0,4));
            EditorSceneManager.SaveScene(scene,ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject=grass.gameObject;
            File.WriteAllText(Path.Combine(InnerverseStarterResearch.Output,"scene.done"),"Scene="+ScenePath+"\nAuthored grass clumps="+grass.authoredMatrices.Length+"\nPipeline="+(GraphicsSettings.currentRenderPipeline==null?"Built-in":GraphicsSettings.currentRenderPipeline.name)+"\nThis is a study fixture, not integration or a benchmark.");
        }
        private static float Height(float x,float z) => .18f*Mathf.Sin(x*.13f)*Mathf.Cos(z*.16f)+Mathf.SmoothStep(0,2.2f,Mathf.InverseLerp(15,35,z));
        private static float PathMask(float x,float z)
        {
            float vertical=Mathf.Min(Mathf.Abs(x+11),Mathf.Abs(x-14));
            return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.2f,2.6f,Mathf.Min(vertical,Mathf.Abs(z+15))));
        }
        private static Mesh BuildGround()
        {
            int nx=96,nz=72;var v=new Vector3[(nx+1)*(nz+1)];var uv=new Vector2[v.Length];var color=new Color[v.Length];var tri=new int[nx*nz*6];int k=0;
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++) {int i=z*(nx+1)+x;float px=x-48,pz=z-32;v[i]=new Vector3(px,Height(px,pz),pz);uv[i]=new Vector2(x/(float)nx,z/(float)nz);color[i]=new Color(PathMask(px,pz),0,0,1);}
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;tri[k++]=a;tri[k++]=c;tri[k++]=b;tri[k++]=b;tri[k++]=c;tri[k++]=d;}
            var m=new Mesh{name="Study ground 96x72m"};m.vertices=v;m.uv=uv;m.colors=color;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        internal static void Capture(Camera camera,Vector3 eye,Vector3 target,string name)
        {
            Vector3 previousPosition=camera.transform.position;Quaternion previousRotation=camera.transform.rotation;
            RenderTexture previousTarget=camera.targetTexture;
            camera.transform.position=eye;camera.transform.LookAt(target);
            var rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;
                Render(camera, rt);
                RenderTexture.active=rt;
                var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();
                File.WriteAllBytes(Path.Combine(InnerverseStarterResearch.Output,name),tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally{camera.targetTexture=previousTarget;camera.transform.SetPositionAndRotation(previousPosition,previousRotation);RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);}
        }
        private static void Render(Camera camera, RenderTexture destination)
        {
            if (GraphicsSettings.currentRenderPipeline == null) { camera.Render(); return; }
            var request = new RenderPipeline.StandardRequest { destination = destination };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new InvalidOperationException("The active pipeline does not support StandardRequest capture.");
            RenderPipeline.SubmitRenderRequest(camera, request);
        }
    }
}
