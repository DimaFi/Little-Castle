using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using LittleCastle.Experimental.MountainBorder.Presentation;

namespace LittleCastle.Experimental.MountainBorder.Editor
{
    public enum MountainArtView { Composition, RTS42, RTS60, Side, Waterfall }

    /// <summary>Authored visual proof, entirely inside a disposable private scene.</summary>
    public sealed class MountainArtPreviewV1 : IDisposable
    {
        const string Root="Assets/_Game/Experimental/MountainBorder/";
        readonly List<Object> owned=new List<Object>();
        readonly WaterfallGeometryV1 water;
        readonly GameObject waterRoot;
        readonly GameObject treesRoot;
        public Scene Scene {get;}
        public Camera Camera {get;}
        public Material Land {get;}
        public Material Surface {get;}
        public Material Falls {get;}
        public Mesh Terrain {get;}
        public int TreeCount {get;private set;}
        bool disposed;

        public MountainArtPreviewV1(int seed=1729,bool foliage=false)
        {
            Scene=EditorSceneManager.NewPreviewScene();
            try
            {
                Land=Own(new Material(Require("Little Castle/Experimental/Mountain Border Preview")));
                Land.SetTexture("_RockTex",AssetDatabase.LoadAssetAtPath<Texture2D>(MountainBorderPreview.RockPath));
                Land.SetFloat("_UseSharedLighting",0);Land.SetFloat("_ArtMode",1);
                var groundSource=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Settings/Rendering/LightingMeadow/Ground.mat");
                if(groundSource)Land.SetTexture("_GroundTex",groundSource.GetTexture("_GrassTex"));
                Land.SetFloat("_GroundTextureStrength",.45f);Land.SetFloat("_GroundTiling",.2f);
                Land.SetColor("_RockTint",new Color(.40f,.34f,.27f));
                Land.SetColor("_ColdRockColor",new Color(.31f,.34f,.39f));
                Land.SetColor("_GrassColor",new Color(.20f,.38f,.07f));
                Land.SetColor("_DryGrassColor",new Color(.35f,.45f,.11f));
                Land.SetFloat("_RockTextureStrength",.16f);Land.SetFloat("_WorldTiling",.045f);
                Land.SetFloat("_SnowLine",77);Land.SetFloat("_SnowBlend",28);Land.SetFloat("_SnowNoiseAmplitude",12);
                Land.SetFloat("_NormalStrength",0);Land.SetFloat("_SpecularStrength",.03f);
                Terrain=Own(MountainMassBlockoutV1.Build(seed));
                MeshObject("V1 connected ridge, rock faces and shelves",Terrain,Land);

                water=WaterfallGeometryV1.Build();
                Surface=Own(new Material(Require("Little Castle/Experimental/Water V1")));
                LoadWaterTextures(Surface);Surface.SetFloat("_UseSharedLighting",0);
                Surface.SetColor("_WaterColor",new Color(.035f,.24f,.31f,.91f));
                Surface.SetColor("_FoamColor",new Color(.78f,.89f,.88f,1));
                Surface.SetFloat("_IsFall",0);Surface.SetFloat("_Opacity",.94f);
                Surface.SetVector("_ImpactPosition",water.landing);Surface.SetFloat("_ImpactRadius",8);
                Surface.SetFloat("_ImpactStrength",.75f);Surface.SetFloat("_FoamStrength",.35f);
                Surface.SetFloat("_FlowSpeed",1.2f);Surface.SetVector("_NormalScale",new Vector4(2,.10f,0,0));
                Surface.SetVector("_FoamScale",new Vector4(1,.11f,0,0));
                Falls=Own(new Material(Surface));Falls.SetFloat("_IsFall",1);
                Falls.SetFloat("_ImpactRadius",0);Falls.SetFloat("_FoamStrength",.8f);Falls.SetFloat("_FlowSpeed",4.4f);
                Falls.SetColor("_WaterColor",new Color(.16f,.40f,.45f,1));
                Falls.SetFloat("_Opacity",.90f);Falls.renderQueue=2997;
                waterRoot=NewObject("V1 linked water levels");
                MeshObject("Upper stream",water.upper,Surface,waterRoot.transform);
                MeshObject("Curved waterfall",water.fall,Falls,waterRoot.transform);
                MeshObject("Landing pool and lower river",water.lower,Surface,waterRoot.transform);
                treesRoot=NewObject("Optional static source-tree comparison");
                if(foliage)AddTrees(seed);

                var sun=NewObject("Private art sunlight").AddComponent<Light>();
                sun.type=LightType.Directional;sun.intensity=1.1f;sun.color=new Color(1,.94f,.84f);
                sun.transform.rotation=Quaternion.Euler(43,-35,0);sun.shadows=LightShadows.None;
                Camera=NewObject("Private art camera").AddComponent<Camera>();Camera.enabled=false;Camera.scene=Scene;
                Camera.fieldOfView=44;Camera.nearClipPlane=.2f;Camera.farClipPlane=900;
                Camera.clearFlags=CameraClearFlags.Skybox;Camera.backgroundColor=new Color(.48f,.62f,.76f);
                var sky=Camera.gameObject.AddComponent<Skybox>();
                sky.material=Own(new Material(Require("Little Castle/Sky/Stylized Day Night")));
                sky.material.SetFloat("_CloudStrength",.23f);sky.material.SetFloat("_Daylight",1);
                SetView(MountainArtView.Composition);
            }
            catch{Dispose();throw;}
        }

        static void LoadWaterTextures(Material material)
        {
            material.SetTexture("_NormalTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Textures/UnitySampleV1/Water_NormalPacked.png"));
            material.SetTexture("_FoamTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Textures/UnitySampleV1/Water_FoamPacked.png"));
            material.SetTexture("_PhaseTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Textures/UnitySampleV1/Water_PhaseNoise.tga"));
        }
        public void SetClay(bool clay)
        {Land.SetFloat("_ArtMode",clay?2:1);waterRoot.SetActive(!clay);treesRoot.SetActive(!clay);}
        public void SetView(MountainArtView view)
        {
            Vector3 target=new Vector3(-4,48,66);float distance=370,pitch=26,yaw=12;
            switch(view)
            {
                case MountainArtView.RTS42:pitch=42;distance=390;break;
                case MountainArtView.RTS60:pitch=60;distance=410;break;
                case MountainArtView.Side:pitch=12;yaw=70;distance=400;break;
                case MountainArtView.Waterfall:target=new Vector3(-58,16,20);pitch=24;yaw=25;distance=87;break;
            }
            Camera.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            Camera.transform.position=target-Camera.transform.forward*distance;
        }
        public void Render(RenderTexture destination,float time=-1)
        {
            if(disposed)throw new ObjectDisposedException(nameof(MountainArtPreviewV1));
            if(!destination)throw new ArgumentNullException(nameof(destination));
            float t=time<0?(float)(EditorApplication.timeSinceStartup%3600):time;
            Surface.SetFloat("_PreviewTime",t);Falls.SetFloat("_PreviewTime",t);
            Camera.aspect=(float)destination.width/destination.height;
            var request=new RenderPipeline.StandardRequest{destination=destination};
            if(!RenderPipeline.SupportsRenderRequest(Camera,request))throw new NotSupportedException("Private preview requires URP render requests.");
            RenderPipeline.SubmitRenderRequest(Camera,request);
        }
        public void SaveCapture(string path,MountainArtView view,bool clay,float time=12)
        {
            SetView(view);SetClay(clay);
            var rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;Texture2D pixels=null;
            try{Render(rt,time);Render(rt,time);RenderTexture.active=rt;pixels=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;if(pixels)Object.DestroyImmediate(pixels);RenderTexture.ReleaseTemporary(rt);}
        }

        void AddTrees(int seed)
        {
            const string path="Assets/_Game/Art/Studies/ValleyArt_Pass03/Spruce_A_Study.prefab";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!source)throw new InvalidOperationException("Source spruce missing.");
            // Run existing source-asset validator, without coupling the prototype assembly to production Editor code.
            var validator=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LittleCastle.Editor.FoliageModelCompatibilityValidator")).FirstOrDefault(t=>t!=null);
            var selection=Selection.activeObject;
            try{Selection.activeObject=source;validator?.GetMethod("ValidateSelected")?.Invoke(null,null);}
            finally{Selection.activeObject=selection;}
            var lod=source.GetComponentInChildren<LODGroup>();
            var renderers=lod&&lod.GetLODs().Length>1?lod.GetLODs()[1].renderers:source.GetComponentsInChildren<MeshRenderer>();
            var leaf=Own(new Material(Land));leaf.SetFloat("_ArtMode",0);leaf.SetFloat("_SnowEnabled",0);
            leaf.SetTexture("_RockTex",Texture2D.whiteTexture);leaf.SetColor("_RockTint",new Color(.12f,.32f,.055f));
            var bark=Own(new Material(leaf));bark.SetColor("_RockTint",new Color(.16f,.10f,.055f));
            for(int z=-35;z<105;z+=9)for(int x=-138;x<138;x+=9)
            {
                float key=Hash(seed,x,z);if(key<.25f)continue;
                var p=new Vector2(x+(key-.5f)*7,z+(Hash(seed+1,x,z)-.5f)*7);
                float h=MountainMassBlockoutV1.Height(seed,p);
                float dx=MountainMassBlockoutV1.Height(seed,p+Vector2.right)-MountainMassBlockoutV1.Height(seed,p-Vector2.right);
                float dz=MountainMassBlockoutV1.Height(seed,p+Vector2.up)-MountainMassBlockoutV1.Height(seed,p-Vector2.up);
                if(h<1||h>54||Mathf.Abs(dx)>1.7f||Mathf.Abs(dz)>1.7f)continue;
                float riverX=p.y>=8?-58:Mathf.Lerp(-58,-35,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-8,-55,p.y)));
                if(Mathf.Abs(p.x-riverX)<(p.y<15?20:10))continue;
                var tree=NewObject("Static source spruce "+TreeCount);tree.transform.SetParent(treesRoot.transform,false);
                tree.transform.position=new Vector3(p.x,h,p.y);tree.transform.rotation=Quaternion.Euler(0,key*360,0);
                float scale=2.2f+key*1.7f;tree.transform.localScale=Vector3.one*scale;
                foreach(var original in renderers)
                {
                    var filter=original.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                    var obj=NewObject("Source LOD1 mesh");obj.transform.SetParent(tree.transform,false);
                    obj.transform.localPosition=original.transform.position;obj.transform.localRotation=original.transform.rotation;obj.transform.localScale=original.transform.lossyScale;
                    obj.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;var mr=obj.AddComponent<MeshRenderer>();
                    mr.sharedMaterials=original.sharedMaterials.Select(m=>m&&m.shader.name.Contains("Foliage")?leaf:bark).ToArray();SetRenderer(mr);
                }
                TreeCount++;
            }
        }
        static float Hash(int seed,int x,int z){unchecked{uint n=(uint)(seed+x*73856093+z*19349663);n^=n>>16;n*=0x7feb352d;n^=n>>15;return(n&0xffffff)/16777215f;}}
        public void Dispose(){if(disposed)return;disposed=true;if(Scene.IsValid())EditorSceneManager.ClosePreviewScene(Scene);water?.Dispose();
            for(int i=owned.Count-1;i>=0;i--)if(owned[i])Object.DestroyImmediate(owned[i]);owned.Clear();}
        GameObject NewObject(string name){var obj=new GameObject(name){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(obj,Scene);return obj;}
        GameObject MeshObject(string name,Mesh mesh,Material material,Transform parent=null){var obj=NewObject(name);if(parent)obj.transform.SetParent(parent,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;SetRenderer(renderer);return obj;}
        static void SetRenderer(MeshRenderer renderer){renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;}
        T Own<T>(T asset)where T:Object{asset.hideFlags=HideFlags.HideAndDontSave;owned.Add(asset);return asset;}
        static Shader Require(string name){var shader=Shader.Find(name);if(!shader)throw new InvalidOperationException("Missing shader "+name);return shader;}
    }
}
