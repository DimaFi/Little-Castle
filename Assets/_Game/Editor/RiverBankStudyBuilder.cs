using System;
using System.IO;
using System.Linq;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Owned assets and scene only. Never regenerates the accepted Meadow scene.</summary>
    public static class RiverBankStudyBuilder
    {
        public const string Root="Assets/_Game/Art/Studies/RiverBanks_20261026";
        const string WaterRoot="Assets/_Game/Art/Studies/RiverWater_20261026";
        static string assetRoot=Root;
        public const string ScenePath="Assets/_Game/Scenes/RiverBankStudy_20261026.unity";
        public const string WaterScenePath="Assets/_Game/Scenes/RiverWaterStudy_20261026.unity";
        public static string Output=>Path.GetFullPath(Application.dataPath+"/../../Research/RiverBankGeneration_20261026");
        static T Save<T>(T value,string name) where T:Object
        {
            string path=assetRoot+"/"+name;var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old){EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}
            AssetDatabase.CreateAsset(value,path);return value;
        }
        static Material Copy(Material material,string name)=>Save(new Material(material),name);
        [MenuItem("Little Castle/Map/River Bank Study/Prepare Isolated Scene %#F10")]
        public static void Prepare()=>Prepare(false);
        [MenuItem("Little Castle/Map/River Water/Prepare Water On Meadow %#F12")]
        public static void PrepareWater()=>Prepare(true);
        public static void PrepareAndBuildWater(){PrepareWater();BuildWaterPlayer();}
        static void Prepare(bool waterOnly)
        {
            if (!waterOnly) throw new InvalidOperationException("Unapproved cliff art remains in the art library. Use the isolated water study.");
            OwnedStudyWorkflow.Require(WaterRoot, WaterScenePath);
            assetRoot=waterOnly?WaterRoot:Root;
            for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)
            {
                throw new Exception("Save the open scene before preparing the isolated study.");
            }
            Directory.CreateDirectory(assetRoot);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene(MeadowStudyBuilder.ScenePath,OpenSceneMode.Single);
            string targetScene=waterOnly?WaterScenePath:ScenePath;EditorSceneManager.SaveScene(scene,targetScene);
            var catalog=waterOnly?ScriptableObject.CreateInstance<MapRockCatalog>():MapRiverCatalogBuilder.Ensure();
            var preview=Object.FindAnyObjectByType<MeadowPreview>();var painter=preview.painter;var camera=preview.studyCamera;
            var ground=MeadowSurfacePainter.FindGround(painter.terrainMaterial);
            var pipeline=Object.Instantiate(preview.profile);pipeline.name="River Bank Study URP";pipeline.supportsCameraDepthTexture=true;pipeline.supportsCameraOpaqueTexture=true;
            pipeline.shadowDistance=100;pipeline=Save(pipeline,"RiverBanks_URP.asset");
            var camData=camera.GetUniversalAdditionalCameraData();camData.requiresDepthOption=CameraOverrideOption.On;camData.requiresColorOption=CameraOverrideOption.On;
            var material=Copy(painter.terrainMaterial,"RiverBanks_Ground.mat");painter.terrainMaterial=material;ground.GetComponent<MeshRenderer>().sharedMaterial=material;
            var mesh=Object.Instantiate(ground.sharedMesh);var vertices=mesh.vertices;
            if(!waterOnly)for(int i=0;i<vertices.Length;i++)vertices[i].y=RiverBankStudy.Height(vertices[i].x,vertices[i].z);
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();mesh=Save(mesh,"RiverBanks_Ground.asset");ground.sharedMesh=mesh;ground.GetComponent<MeshCollider>().sharedMesh=mesh;
            var grass=preview.grass;var grassData=Object.Instantiate(grass.data);
            foreach(var patch in grassData.patches)
            {
                patch.matrices=(Matrix4x4[])patch.matrices.Clone();
                if(!waterOnly)for(int i=0;i<patch.matrices.Length;i++)patch.matrices[i]=Project(patch.matrices[i]);
                patch.bounds=new Bounds(new Vector3(patch.bounds.center.x,4,patch.bounds.center.z),new Vector3(patch.bounds.size.x,22,patch.bounds.size.z));
            }
            grass.data=Save(grassData,"RiverBanks_Grass.asset");grass.material=Copy(grass.material,"RiverBanks_Blades.mat");painter.grassMaterial=grass.material;
            var accents=Object.FindAnyObjectByType<InnerverseGrassStudy>();if(!waterOnly)accents.authoredMatrices=accents.authoredMatrices.Select(Project).ToArray();accents.grassMaterial=Copy(accents.grassMaterial,"RiverBanks_Accents.mat");painter.accentMaterial=accents.grassMaterial;
            if(!waterOnly)foreach(var tree in MapTreeCatalog.Default.SceneSources()){var p=tree.transform.position;p.y+=RiverBankStudy.Height(p.x,p.z)-MeadowPreview.SampleHeight(p.x,p.z);tree.transform.position=p;}
            var surface=Object.Instantiate(painter.source);surface.mapId="river-bank-study-20261026";surface.revision=1;surface.hasHeightBase=false;surface.hasTreeBase=false;surface.heightField=null;surface.rivers.Clear();surface.rockSections.Clear();surface.treeInstances.Clear();surface.placementExclusions.Clear();surface.authored=new SurfacePaintDocument();
            var road=new SurfacePaintStroke{id="bridge-approach",road=true,radius=1.55f};
            foreach(float z in new[]{-26f,-16,-6,6,16,26})road.points.Add(FixedBridgeSiteProfile.LocalToWorld(new Vector2(0,z),RiverBankStudy.BridgeOrigin,RiverBankStudy.BridgeYaw));surface.authored.strokes.Add(road);
            if(waterOnly)surface.mapId="uber-water-on-meadow-20261026";
            var document=MeadowSurfacePainter.CreateDocument(surface,ground,MapTreeCatalog.Default);var session=new MapEditSession(document,MapEditAccess.All);if(!waterOnly)session.RegisterRockCatalog(catalog.Definitions);session.UpsertRiver(waterOnly?RiverBankStudy.WaterRiver():RiverBankStudy.River());surface.rivers=session.ExportRivers();
            painter.source=Save(surface,"RiverBanks_Surface.asset");painter.authoredPreviewMask=null;
            var bridgePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Models/Concept/Bridge_Stone_A/Bridge_Stone_A_v002.prefab");
            if(!bridgePrefab)throw new Exception("Approved stone bridge v002 is missing.");
            var bridge=(GameObject)PrefabUtility.InstantiatePrefab(bridgePrefab);bridge.name="Approved stone bridge v002 · river crossing";bridge.transform.SetPositionAndRotation(new Vector3(RiverBankStudy.BridgeOrigin.x,0,RiverBankStudy.BridgeOrigin.y),Quaternion.Euler(0,RiverBankStudy.BridgeYaw,0));
            foreach(var collider in bridge.GetComponentsInChildren<MeshCollider>(true))
            {
                var source=collider.sharedMesh;if(!source)continue;
                var readable=new Mesh{name=source.name+" navigation copy"};readable.vertices=source.vertices;readable.triangles=source.triangles;readable.RecalculateBounds();collider.sharedMesh=Save(readable,source.name+"_Readable.asset");
            }
            File.WriteAllLines(Path.Combine(Output,"water-bridge-audit.txt"),bridge.GetComponentsInChildren<MeshRenderer>(true).Select(r=>$"{r.name}: center={r.bounds.center} size={r.bounds.size} materials={string.Join(",",r.sharedMaterials.Select(m=>m?m.shader.name:"MISSING"))}"));
            SetupMaterials(catalog,waterOnly);if(waterOnly){catalog=Save(catalog,"RiverWater_Catalog.asset");painter.riverCatalogOverride=catalog;painter.waterOnlyRivers=true;painter.diagnosticSaveId="uber-water-on-meadow";}
            var study=camera.gameObject.AddComponent<RiverBankStudy>();study.waterOnlyFixture=waterOnly;study.painter=painter;study.studyCamera=camera;study.profile=pipeline;study.sun=preview.sun;study.globals=preview.globals;study.bridge=bridge;
            if(waterOnly)painter.gameObject.AddComponent<UberRiverReflectionAdapter>().painter=painter;
            if(preview.demonstrator)preview.demonstrator.gameObject.SetActive(false);
            Object.DestroyImmediate(preview);camera.transform.position=new Vector3(30,28,-32);camera.transform.LookAt(new Vector3(-4,1,10));
            EditorSceneManager.SaveScene(scene,targetScene);AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Output,"scene-preparation.txt"),$"Scene={targetScene}\nUnity={Application.unityVersion}\nURP={AssetDatabase.GetAssetPath(pipeline)}\nDepth={pipeline.supportsCameraDepthTexture}; opaque={pipeline.supportsCameraOpaqueTexture}\nWater={catalog.waterMaterial.shader.name}\nWaterOnly={waterOnly}; cliff catalog entries={catalog.entries.Length}\n"+(waterOnly?"Full Uber Tropical preset with original MIT normal/noise/foam maps; only external caustic images replaced by an owned procedural map.\n":"Owned procedural water maps; experimental bank fixture.\n"));
        }
        static Matrix4x4 Project(Matrix4x4 matrix){var p=matrix.GetColumn(3);p.y+=RiverBankStudy.Height(p.x,p.z)-MeadowPreview.SampleHeight(p.x,p.z);matrix.SetColumn(3,p);return matrix;}
        static void SetupMaterials(MapRockCatalog catalog,bool waterOnly)
        {
            string third="Assets/_Game/ThirdParty/RiverBanks/";
            var waterShader=AssetDatabase.LoadAssetAtPath<Shader>(third+"UberWater/Shader/UberStylizedWater.shader");
            if(waterOnly){catalog.waterMaterial=FullUberWater(waterShader);return;}
            var rockShader=AssetDatabase.LoadAssetAtPath<Shader>(third+"VertexBlendTriplanar/VertexBlendTriplanar.shadergraph");
            if(!waterShader||(!waterOnly&&!rockShader))throw new Exception("Selectively imported MIT shaders are missing.");
            var water=new Material(waterShader){name="Uber water · owned maps",enableInstancing=true};
            water.SetColor("_Color_Shallow",new Color(.08f,.48f,.48f,.12f));water.SetColor("_Color_Deep",new Color(.025f,.20f,.34f,.78f));water.SetFloat("_Water_Depth",1.6f);water.SetFloat("_WorldSpaceDepth",1);water.SetFloat("_ShoreFade",1);water.SetFloat("_ShoreFade_Smoothness",.4f);
            water.SetFloat("_Enable_SurfaceFoam",1);water.SetTexture("_SurfFoam_Map",Procedural("Owned_Foam.png",0));water.SetColor("_SurfFoam_Color",new Color(.8f,.92f,.94f,.23f));water.SetFloat("_SurfFoam_Scale",1.4f);water.SetFloat("_SurfFoam_Edge",.67f);water.SetFloat("_SurfFoam_EdgeSmooth",.12f);water.SetVector("_SurfFoam_Pan",new Vector4(0,-.12f,0,0));water.SetFloat("_SurfFoam_AlphaBlend",.05f);
            water.SetFloat("_Enable_Intersection",1);water.SetTexture("_InterSec_Foam_Mask",water.GetTexture("_SurfFoam_Map"));water.SetColor("_InterSec_Color",new Color(.8f,.91f,.93f,.8f));water.SetFloat("_InterSec_Width",.35f);water.SetFloat("_InterSec_Foam_Scale",3);water.SetFloat("_InterSec_Dissolve",.75f);
            water.SetFloat("_SurfFoam_Edge",.53f);water.SetFloat("_SurfFoam_EdgeSmooth",.06f);water.SetColor("_SurfFoam_Color",new Color(.8f,.92f,.94f,.12f));water.SetFloat("_InterSec_Dissolve",.4f);water.SetFloat("_InterSec_Width",.22f);water.SetFloat("_InterSec_Foam_Smooth",.1f);water.SetTexture("_SurfaceDistortion_Map",water.GetTexture("_SurfFoam_Map"));water.SetFloat("_SurfaceDistortion_Strength",.08f);
            water.EnableKeyword("_ENABLENORMAL");water.SetFloat("_ENABLENORMAL",1);water.SetTexture("_Normal_Map",Procedural("Owned_WaterNormal.png",1));water.SetFloat("_Normal_Strength",.35f);water.SetFloat("_Normal_Scale",1.8f);water.SetFloat("_Normal_Pan",.12f);water.SetFloat("_Normal_DistanceStrength",.12f);water.SetFloat("_DistanceMask_Start",18);water.SetFloat("_DistanceMask_Fade",55);
            water.EnableKeyword("_ENABLEREFRACTION");water.SetFloat("_ENABLEREFRACTION",1);water.SetFloat("_Refraction_Strength",.045f);
            water.EnableKeyword("_ENABLECAUSTICS");water.SetFloat("_ENABLECAUSTICS",1);water.SetTexture("_Caustics_Map",Procedural("Owned_Caustics.png",2));water.SetFloat("_Caustics_Strength",.35f);water.SetFloat("_Caustics_Scale",3);water.SetFloat("_Caustics_Depth",-4);
            water.DisableKeyword("_ENABLEWAVE");water.DisableKeyword("_ENABLEPLANERREFLECTION");catalog.waterMaterial=Save(water,"RiverBanks_Water.mat");
            water=catalog.waterMaterial;water.SetColor("_ShadowColor",new Color(.08f,.2f,.28f,.25f));EditorUtility.SetDirty(water);if(waterOnly)return;
            var bank=new Material(rockShader){name="MIT vertex blend · local rock soil grass",enableInstancing=true};string tex="Assets/_Game/Art/Studies/ValleyArt_Pass03/Textures/";
            string[] baseNames={"Rock_BaseColor.png","Path_diffuseOriginal.png","Ground_diffuseOriginal.png"},normalNames={"Rock_Normal.png","Path_normal.png","Ground_normal.png"},channels={"R","G","B"};
            var packed=Procedural("Owned_BlendMask.png",3);
            for(int i=0;i<3;i++){string c=channels[i];bank.SetTexture("_Albedo_"+c,AssetDatabase.LoadAssetAtPath<Texture2D>(tex+baseNames[i]));bank.SetTexture("_Normal_"+c,AssetDatabase.LoadAssetAtPath<Texture2D>(tex+normalNames[i]));bank.SetTexture("_Mask_"+c,packed);bank.SetFloat("_Scale_"+c,.22f);bank.SetFloat("_NormalStrength_"+c,.28f);}
            bank.SetFloat("_Falloff_G",.25f);bank.SetFloat("_Falloff_B",.3f);catalog.bankMaterial=Save(bank,"RiverBanks_CliffBlend.mat");EditorUtility.SetDirty(catalog);
        }
        static Material FullUberWater(Shader shader)
        {
            const string uber="Assets/_Game/ThirdParty/RiverBanks/UberWater/";
            var template=AssetDatabase.LoadAssetAtPath<Material>(uber+"Template Materials/UWa-Template-Tropical.mat");if(!template||!shader)throw new Exception("Full Uber Tropical package is missing.");
            foreach(string path in Directory.GetFiles(uber+"Textures","*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.sRGBTexture=false;if(Path.GetFileName(path).StartsWith("Normal"))importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();
            }
            var caustics=Procedural("Owned_Caustics.png",2);
            Directory.CreateDirectory(assetRoot+"/UberPresets");AssetDatabase.Refresh();
            foreach(string path in Directory.GetFiles(uber+"Template Materials","*.mat"))
            {
                var source=AssetDatabase.LoadAssetAtPath<Material>(path);var preset=new Material(source){shader=shader,enableInstancing=true};preset.SetTexture("_Caustics_Map",caustics);Save(preset,"UberPresets/"+Path.GetFileName(path));
            }
            var water=new Material(template){name="Uber Tropical · Little Castle river",shader=shader,enableInstancing=true};
            water.SetTexture("_Normal_Map",AssetDatabase.LoadAssetAtPath<Texture2D>(uber+"Textures/Normal 2.png"));water.SetTexture("_SurfFoam_Map",AssetDatabase.LoadAssetAtPath<Texture2D>(uber+"Textures/Foam 1.png"));water.SetTexture("_SL_Dissolve_Mask",AssetDatabase.LoadAssetAtPath<Texture2D>(uber+"Textures/Noise 5.png"));water.SetTexture("_SurfaceDistortion_Map",AssetDatabase.LoadAssetAtPath<Texture2D>(uber+"Textures/Noise 4.png"));water.SetTexture("_Caustics_Distortion_Map",AssetDatabase.LoadAssetAtPath<Texture2D>(uber+"Textures/Noise 4.png"));
            water.SetTexture("_Caustics_Map",caustics);water.SetFloat("_Caustics_Strength",.75f);
            water.EnableKeyword("_ENABLESHORELINE");water.SetFloat("_ENABLESHORELINE",1);water.EnableKeyword("_ENABLEWAVE");water.SetFloat("_ENABLEWAVE",1);
            water.SetFloat("_1st_Wave_Height",.045f);water.SetFloat("_2nd_Wave_Height",.025f);water.SetFloat("_1st_Wave_Sharpness",.08f);water.SetFloat("_2nd_Wave_Sharpness",.06f);water.SetFloat("_1st_Wave_Speed",.5f);water.SetFloat("_2nd_Wave_Speed",.6f);
            water.SetColor("_Wave_Top_Color",new Color(.29829118f,.9207434f,.9245283f,.12f));
            return Save(water,"RiverWater_Uber_Tropical.mat");
        }
        static Texture2D Procedural(string filename,int kind)
        {
            const int size=256;var pixels=new Color[size*size];
            Func<float,float,float> wave=(x,y)=>Mathf.Sin(x*2*Mathf.PI*3+Mathf.Sin(y*2*Mathf.PI*2)*.6f)+.5f*Mathf.Sin((x+y)*2*Mathf.PI*7);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size,w=wave(u,v);
                Color c;
                if(kind==1){float dx=(wave(u+.001f,v)-wave(u-.001f,v))*.7f,dy=(wave(u,v+.001f)-wave(u,v-.001f))*.7f;var n=new Vector3(-dx,-dy,1).normalized;c=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);}
                else if(kind==3)c=new Color(0,.95f,.5f,.12f);
                else {float noise=.55f*TileNoise(u,v,5)+.3f*TileNoise(u+.31f,v+.17f,13)+.15f*TileNoise(u+.7f,v+.4f,29);float a=kind==2?Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs(noise-.5f)*15),4):noise;c=new Color(a,a,a,1);}
                pixels[y*size+x]=c;
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true);texture.SetPixels(pixels);texture.Apply();string path=assetRoot+"/"+filename;File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Repeat;importer.sRGBTexture=false;importer.mipmapEnabled=true;if(kind==1)importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static float TileNoise(float u,float v,float scale)
        {u=Mathf.Repeat(u,1);v=Mathf.Repeat(v,1);float x=u*scale+41,y=v*scale+73;return Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(x,y),Mathf.PerlinNoise(x-scale,y),u),Mathf.Lerp(Mathf.PerlinNoise(x,y-scale),Mathf.PerlinNoise(x-scale,y-scale),u),v);}
        [MenuItem("Little Castle/Map/River Bank Study/Build Study Player %#F11")]
        public static void BuildPlayer()=>BuildPlayer(false);
        [MenuItem("Little Castle/Map/River Water/Build Water Player &F11")]
        public static void BuildWaterPlayer()=>BuildPlayer(true);
        static void BuildPlayer(bool waterOnly)
        {
            PlayerSettings.enableFrameTimingStats=true;Directory.CreateDirectory(Output);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{waterOnly?WaterScenePath:ScenePath},locationPathName=Path.Combine(Output,waterOnly?"WaterPlayer/LittleCastle_RiverWater.exe":"Player/RiverBankStudy.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText(Path.Combine(Output,"build_result.txt"),$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nTime={report.summary.totalTime}\n");
            string notices="Little Castle river integration: third-party notices. Uber code and MIT normal/noise/foam maps are included. External demo terrain and CC BY-SA caustic images are excluded; caustics are generated locally.\n\n";
            foreach(string path in new[]{"UberWater/LICENSE.txt","UberWater/Third Party/URP_ShaderGraphCustomLighting-main/LICENSE.txt"})notices+=path+"\n"+File.ReadAllText("Assets/_Game/ThirdParty/RiverBanks/"+path)+"\n\n";
            File.WriteAllText(Path.Combine(Output,waterOnly?"WaterPlayer/THIRD_PARTY_NOTICES.txt":"Player/THIRD_PARTY_NOTICES.txt"),notices);
            if(Application.isBatchMode)EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:2);
        }
    }
}
