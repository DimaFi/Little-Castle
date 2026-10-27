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
    public static class ValleyArtAssets
    {
        public const string Root="Assets/_Game/Art/Studies/ValleyArt_Pass03";
        public static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../Research/Valley_Art_Pass03_2026-10-25"));
        public static T Save<T>(T value,string path)where T:UnityEngine.Object
        {
            value.name=Path.GetFileNameWithoutExtension(path);
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old){if(old is Mesh target && value is Mesh source){target.Clear();target.indexFormat=source.indexFormat;target.vertices=source.vertices;target.triangles=source.triangles;target.normals=source.normals;target.uv=source.uv;target.uv2=source.uv2;target.colors=source.colors;target.tangents=source.tangents;target.bounds=source.bounds;}else EditorUtility.CopySerialized(value,old);EditorUtility.SetDirty(old);AssetDatabase.SaveAssetIfDirty(old);UnityEngine.Object.DestroyImmediate(value);return old;}
            AssetDatabase.CreateAsset(value,path);return value;
        }
        public static Material Solid(Color color)
        {
            var mat=new Material(Shader.Find("Little Castle/Surface/LC Stylized Lit"));mat.SetColor("_Color",color);mat.SetFloat("_LightResponse",1);mat.SetFloat("_Roughness",.88f);mat.enableInstancing=true;return mat;
        }
        public static GameObject Prefab(string name)=>GroundBiomeVegetationPreflight.Source(name);
        public static GameObject SecondFir()
        {
            var root=new GameObject("Fir_UNS_Study");var obj=(GameObject)PrefabUtility.InstantiatePrefab(Prefab("UNS_Spruce_01"));obj.transform.SetParent(root.transform,false);
            var leaf=new Material(AssetDatabase.LoadAssetAtPath<Material>(GroundBiomeVegetationPreflight.Study+"/Study_UNS_Spruce_Tree_Branch.mat"));leaf.SetColor("_Color",new Color(.38f,.46f,.25f));leaf.SetFloat("_LightResponse",1);leaf.SetFloat("_WindAmplitude",.12f);leaf.SetFloat("_LeafFlutter",.045f);leaf=Save(leaf,Root+"/Fir_UNS_Leaves.mat");
            var bark=AssetDatabase.LoadAssetAtPath<Material>(GroundBiomeVegetationPreflight.Study+"/Study_UNS_Color_Palette.mat");
            foreach(var r in obj.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.Contains("Branch")?leaf:bark).ToArray();
            Selection.activeGameObject=root;FoliageModelCompatibilityValidator.ValidateSelected();var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Fir_UNS_Study.prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        public static GameObject[] Oaks()
        {
            string folder=Root+"/OakKit";
            foreach(var path in Directory.GetFiles(folder+"/Textures","*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.textureType=path.Contains("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=path.Contains("BaseColor");importer.mipmapEnabled=true;importer.anisoLevel=8;importer.SaveAndReimport();
            }
            Texture2D Tex(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Textures/"+name+".png");
            var bark=Solid(new Color(.95f,.87f,.73f));bark.SetTexture("_MainTex",Tex("T_Oak_Bark_A_BaseColor"));bark.SetTexture("_BumpMap",Tex("T_Oak_Bark_A_Normal"));bark.SetFloat("_BumpScale",.35f);bark=Save(bark,Root+"/Oak_Bark.mat");
            var leaves=new Material(Shader.Find("Little Castle/Foliage/LC Foliage"));leaves.SetTexture("_MainTex",Tex("T_Foliage_BaseColor"));leaves.SetTexture("_OpacityMap",Tex("T_Foliage_Opacity"));leaves.SetFloat("_OpacityMapStrength",1);leaves.SetTexture("_BumpMap",Tex("T_Foliage_Normal"));leaves.SetTexture("_OcclusionMap",Tex("T_Foliage_AO"));leaves.SetFloat("_BumpScale",.25f);leaves.SetColor("_Color",new Color(.56f,.67f,.40f));leaves.SetFloat("_LightResponse",1);leaves.SetFloat("_WindAmplitude",.12f);leaves.SetFloat("_LeafFlutter",.045f);leaves.SetFloat("_TransmissionStrength",.30f);leaves.enableInstancing=true;leaves=Save(leaves,Root+"/Oak_Leaves.mat");
            var prefabs=new GameObject[4];int variant=0;
            foreach(char letter in "ABCD")
            {
                var root=new GameObject("Oak_"+letter+"_Study");var lods=new LOD[3];
                for(int level=0;level<3;level++)
                {
                    string path=folder+"/SM_Tree_Oak_"+letter+(level==0?"":"_LOD"+level)+".fbx";
                    var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.bakeAxisConversion=true;importer.importAnimation=false;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));model.transform.SetParent(root.transform,false);
                    var renderers=model.GetComponentsInChildren<Renderer>(true);
                    foreach(var r in renderers)r.sharedMaterials=Enumerable.Range(0,r.sharedMaterials.Length).Select(i=>i==0?bark:leaves).ToArray();
                    var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                    if(bounds.center.z>bounds.center.y+1)model.transform.localRotation=Quaternion.Euler(-90,0,0)*model.transform.localRotation;
                    lods[level]=new LOD(new[]{.16f,.055f,.009f}[level],renderers);
                }
                var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
                prefabs[variant++]=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Oak_"+letter+"_Study.prefab");UnityEngine.Object.DestroyImmediate(root);
            }
            return prefabs;
        }
        public static GameObject Spruce()
        {
            string path=Root+"/Spruce_A/LittleCastle_Spruce_LODs.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
            importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=false;importer.SaveAndReimport();
            Texture2D Tex(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Spruce_A/Textures/"+name+".png");
            foreach(var texPath in Directory.GetFiles(Root+"/Spruce_A/Textures","*.png"))
            {
                var t=(TextureImporter)AssetImporter.GetAtPath(texPath.Replace('\\','/'));bool normal=texPath.Contains("Normal");t.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;t.sRGBTexture=texPath.Contains("BaseColor");t.mipmapEnabled=true;t.anisoLevel=4;t.SaveAndReimport();
            }
            var bark=Solid(Color.white);bark.SetTexture("_MainTex",Tex("Spruce_Bark_BaseColor"));bark.SetTexture("_BumpMap",Tex("Spruce_Bark_Normal"));bark.SetTexture("_RoughnessMap",Tex("Spruce_Bark_Roughness"));bark.SetFloat("_RoughnessMapStrength",1);bark=Save(bark,Root+"/Spruce_Bark.mat");
            var leaves=new Material(Shader.Find("Little Castle/Foliage/LC Foliage"));leaves.SetTexture("_MainTex",Tex("Spruce_Foliage_BaseColor"));leaves.SetTexture("_BumpMap",Tex("Spruce_Foliage_Normal"));leaves.SetColor("_Color",new Color(.70f,.78f,.55f));leaves.SetFloat("_LightResponse",1);leaves.SetFloat("_UseVertexWindMask",1);leaves.SetFloat("_WindAmplitude",.13f);leaves.SetFloat("_LeafFlutter",.05f);leaves.SetFloat("_BumpScale",.45f);leaves.SetFloat("_TransmissionStrength",.3f);leaves.enableInstancing=true;leaves=Save(leaves,Root+"/Spruce_Leaves.mat");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);
            foreach(var group in instance.GetComponentsInChildren<LODGroup>())UnityEngine.Object.DestroyImmediate(group);
            var renderers=instance.GetComponentsInChildren<MeshRenderer>(true);
            var levels=new LOD[3];
            for(int i=0;i<3;i++){var r=renderers.Single(x=>x.name=="Spruce_LOD"+i);r.sharedMaterials=new[]{bark,leaves};r.gameObject.SetActive(true);levels[i]=new LOD(new[]{.28f,.11f,.018f}[i],new Renderer[]{r});}
            var lod=instance.AddComponent<LODGroup>();lod.SetLODs(levels);lod.RecalculateBounds();
            var prefab=PrefabUtility.SaveAsPrefabAsset(instance,Root+"/Spruce_A_Study.prefab");UnityEngine.Object.DestroyImmediate(instance);return prefab;
        }
        [MenuItem("Little Castle/Research/Valley/Inspect Tree Candidates")]
        public static void Preflight()
        {
            Directory.CreateDirectory(Output);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene preserved.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var authoredSpruce=Spruce();var oaks=Oaks();
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.38f,.46f,.55f);RenderSettings.ambientEquatorColor=new Color(.30f,.34f,.27f);RenderSettings.ambientGroundColor=new Color(.17f,.19f,.12f);
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1;sun.color=new Color(1,.94f,.82f);sun.transform.rotation=Quaternion.Euler(45,-35,0);sun.shadows=LightShadows.Soft;RenderSettings.sun=sun;
            var globals=new GameObject("LC Globals").AddComponent<LittleCastle.Rendering.StylizedLightingGlobals>();
            var camera=new GameObject("Gallery Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.48f,.57f,.62f);camera.fieldOfView=42;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.localScale=Vector3.one*5;ground.GetComponent<Renderer>().sharedMaterial=Solid(new Color(.33f,.42f,.18f));
            var log=new StringBuilder();Application.LogCallback collect=(m,s,t)=>{if(m.Contains("[Little Castle Foliage]"))log.AppendLine(t+": "+m);};Application.logMessageReceived+=collect;
            try
            {
                foreach(string name in new[]{"Oak_A_Study","Oak_B_Study","Oak_C_Study","Oak_D_Study","Spruce_A_Study"})
                {
                    var source=name=="Spruce_A_Study"?authoredSpruce:Prefab(name);
                    var obj=(GameObject)PrefabUtility.InstantiatePrefab(source);Selection.activeGameObject=obj;
                    FoliageModelCompatibilityValidator.ValidateSelected();
                    var bounds=obj.GetComponentsInChildren<Renderer>(true).First().bounds;foreach(var r in obj.GetComponentsInChildren<Renderer>(true))bounds.Encapsulate(r.bounds);
                    obj.transform.localScale*=8/Mathf.Max(.1f,bounds.size.y);obj.transform.position-=Vector3.up*bounds.min.y;
                    camera.transform.position=new Vector3(11,8,-15);camera.transform.LookAt(new Vector3(0,4,0));globals.ApplyGlobals();
                    GroundBiomeVegetationPreflight.Capture(camera,Path.Combine(Output,"tree_"+name+".png"));
                    log.AppendLine("ASSET="+name+" raw bounds="+bounds+" renderers="+obj.GetComponentsInChildren<Renderer>(true).Length);
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
            finally{Application.logMessageReceived-=collect;File.WriteAllText(Path.Combine(Output,"tree_preflight.txt"),log.ToString());}
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
    }
}
