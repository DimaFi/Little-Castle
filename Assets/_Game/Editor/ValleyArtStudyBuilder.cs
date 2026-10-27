using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LittleCastle.World;
using LittleCastle.Research;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.Editor
{
    public static class ValleyArtStudyBuilder
    {
        const string Root=ValleyArtAssets.Root;
        public const string ScenePath="Assets/_Game/Scenes/ValleyArt_Pass03.unity";
        static string Output=>ValleyArtAssets.Output;
        const float WaterY=.30f;
        static float S(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        static float Center(float z)=>-6+9*Mathf.Sin(z*.035f)+2*Mathf.Sin(z*.09f);
        static float Width(float z)=>10.4f+1.4f*Mathf.Sin(z*.06f+.8f)+.6f*Mathf.Sin(z*.24f)+.3f*Mathf.Sin(z*.49f);
        static float Ford(float z)=>1-S(5,12,Mathf.Abs(z-45));
        static float Land(float x,float z)=>5.6f+.35f*Mathf.Sin(x*.06f)*Mathf.Cos(z*.05f)+
            5.5f*Mathf.Exp(-((x+53)*(x+53)/490+(z-43)*(z-43)/700))+
            6.0f*Mathf.Exp(-((x-58)*(x-58)/620+(z-57)*(z-57)/690))+
            S(65,115,z)*(8+7*Mathf.Sin(x*.043f)*Mathf.Sin(x*.043f));
        static float Height(float x,float z)
        {
            float d=Mathf.Abs(x-Center(z)),w=Width(z),ford=Ford(z);
            float bed=Mathf.Lerp(-2.1f+.12f*Mathf.Sin(z*.18f)*Mathf.Cos(x*.3f),.07f,ford);
            float cliff=S(w-.65f,w+1.05f,d),ramp=S(w-1.5f,w+13,d);
            return Mathf.Lerp(bed,Land(x,z),Mathf.Lerp(cliff,ramp,ford));
        }
        static float Snow(float x,float z)=>S(29,48,x+z*.16f+Mathf.Sin(z*.09f)*2.3f);
        static float RoadZ(float x)=>S(16,38,Mathf.Abs(x+6))*4*Mathf.Sin(x*.033f);
        static float Road(float x,float z)
        {
            float main=1-S(2.5f,3.8f,Mathf.Abs(z-RoadZ(x)));
            float trail=(1-S(1.3f,2.3f,Mathf.Abs(x-(-43+6*Mathf.Sin(z*.035f)))))*(1-S(65,80,Mathf.Abs(z)));
            float ford=(1-S(1.5f,2.8f,Mathf.Abs(z-45)))*(1-S(30,53,Mathf.Abs(x)));
            return Mathf.Max(main,Mathf.Max(trail,ford));
        }
        static float WaterEdge(float z,int side)
        {
            float low=0,high=Width(z)+20;
            for(int i=0;i<24;i++){float mid=(low+high)*.5f;if(Height(Center(z)+side*mid,z)<WaterY)low=mid;else high=mid;}
            return (low+high)*.5f;
        }
        static Texture2D Tex(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+name+".png");
        static T Save<T>(T obj,string name)where T:UnityEngine.Object=>ValleyArtAssets.Save(obj,Root+"/"+name);
        static GameObject MeshObject(string name,Mesh mesh,Material material,bool collider=false)
        {
            var obj=new GameObject(name);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;if(collider)obj.AddComponent<MeshCollider>().sharedMesh=mesh;return obj;
        }
        [MenuItem("Little Castle/Research/Valley/Build Valley Art Sample")]
        public static void Build()
        {
            if(!Path.GetFileName(Path.GetDirectoryName(Application.dataPath)).StartsWith("Little-Castle_URP_"))throw new Exception("Restricted to the URP study copy.");
            for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene preserved.");
            Directory.CreateDirectory(Output);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var path in Directory.GetFiles(Root+"/Textures","*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));bool normal=path.Contains("normal")||path.Contains("Normal");importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=path.Contains("diffuseOriginal")||path.Contains("BaseColor");importer.mipmapEnabled=true;importer.anisoLevel=8;importer.isReadable=false;if(normal)importer.flipGreenChannel=!path.Contains("Rock_");importer.SaveAndReimport();
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var secondFir=ValleyArtAssets.SecondFir();
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.29f,.38f,.51f);RenderSettings.ambientEquatorColor=new Color(.23f,.27f,.22f);RenderSettings.ambientGroundColor=new Color(.12f,.15f,.10f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.57f,.68f,.77f);RenderSettings.fogDensity=.0021f;
            RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/StylizedDayNightSky.mat");
            var sun=new GameObject("Warm valley sunlight").AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.94f,.82f);sun.intensity=1.2f;sun.shadows=LightShadows.Soft;sun.shadowBias=.045f;sun.shadowNormalBias=.15f;sun.transform.rotation=Quaternion.Euler(43,-38,0);RenderSettings.sun=sun;
            var globals=new GameObject("Shared LC lighting and wind").AddComponent<LittleCastle.Rendering.StylizedLightingGlobals>();globals.SetWindPresentation(new Vector2(.9f,.32f),.65f,1);
            var sourcePipeline=(UniversalRenderPipelineAsset)(QualitySettings.renderPipeline??GraphicsSettings.defaultRenderPipeline);
            var pipeline=UnityEngine.Object.Instantiate(sourcePipeline);pipeline.shadowDistance=190;pipeline.mainLightShadowmapResolution=4096;pipeline.shadowCascadeCount=4;pipeline.cascade4Split=new Vector3(.09f,.24f,.5f);pipeline.msaaSampleCount=4;pipeline.supportsHDR=true;pipeline=Save(pipeline,"Valley_URP_Profile.asset");
            var volumeProfile=ScriptableObject.CreateInstance<VolumeProfile>();
            var tone=volumeProfile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
            var color=volumeProfile.Add<ColorAdjustments>(true);color.contrast.Override(8);color.saturation.Override(3);color.postExposure.Override(.15f);
            var balance=volumeProfile.Add<WhiteBalance>(true);balance.temperature.Override(4);
            volumeProfile=Save(volumeProfile,"Valley_ColorProfile.asset");
            // Volume components must be persisted as sub-assets, not transient references.
            foreach(var component in volumeProfile.components)if(!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,volumeProfile);EditorUtility.SetDirty(volumeProfile);AssetDatabase.SaveAssetIfDirty(volumeProfile);
            var volume=new GameObject("Subtle color grade · no blur").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=volumeProfile;
            var terrain=new Material(Shader.Find("Little Castle/Terrain/LC Terrain"));
            terrain.SetTexture("_GrassTex",Tex("Ground_diffuseOriginal"));terrain.SetTexture("_GrassNormal",Tex("Ground_normal"));terrain.SetTexture("_GrassAO",Tex("Ground_ao"));terrain.SetTexture("_GrassHeight",Tex("Ground_height"));
            terrain.SetTexture("_DirtTex",Tex("Path_diffuseOriginal"));terrain.SetTexture("_DirtNormal",Tex("Path_normal"));terrain.SetTexture("_DirtAO",Tex("Path_ao"));terrain.SetTexture("_RockTex",Tex("Rock_BaseColor"));
            terrain.SetFloat("_GroundPalette",1);terrain.SetColor("_GrassColor",new Color(.36f,.46f,.20f));terrain.SetColor("_DryGrassColor",new Color(.47f,.49f,.24f));terrain.SetColor("_DirtColor",new Color(.66f,.48f,.28f));terrain.SetColor("_RockColor",new Color(.88f,.91f,.94f));
            terrain.SetFloat("_GrassDetail",.17f);terrain.SetFloat("_DirtDetail",.37f);terrain.SetFloat("_SurfaceDetailStrength",.52f);terrain.SetFloat("_GrassNormalScale",.5f);terrain.SetFloat("_SurfaceAOStrength",.42f);terrain.SetFloat("_DetailFadeStart",70);terrain.SetFloat("_DetailFadeEnd",180);
            terrain.SetFloat("_WorldTiling",.17f);terrain.SetFloat("_UseVertexMasks",1);terrain.SetFloat("_PathEdgeSharpness",.35f);terrain.SetFloat("_WetnessStrength",.32f);terrain.SetFloat("_MacroStrength",.08f);terrain.SetFloat("_LightResponse",1);terrain.SetFloat("_RockSlopeStart",.16f);terrain.SetFloat("_RockSlopeEnd",.42f);terrain.SetFloat("_RockTriplanar",1);
            var snowMask=Save(SnowMask(),"Valley_SnowCoverage.asset");terrain.SetFloat("_UseSnow",1);terrain.SetTexture("_SnowControl",snowMask);terrain.SetVector("_SnowMapBounds",new Vector4(-130,-120,1f/260,1f/240));terrain.SetTexture("_SnowTex",Tex("Snow_diffuseOriginal"));terrain.SetTexture("_SnowNormal",Tex("Snow_normal"));terrain.SetTexture("_SnowAO",Tex("Snow_ao"));terrain.SetFloat("_SnowTiling",.095f);terrain.SetFloat("_SnowDetailStrength",.65f);terrain.SetFloat("_SnowSurfaceFlattening",.12f);terrain.SetColor("_SnowColor",new Color(.69f,.76f,.83f));terrain.SetFloat("_SnowPathClearance",.84f);terrain=Save(terrain,"Valley_Terrain.mat");
            var groundMesh=Save(Ground(),"Valley_Ground.asset");MeshObject("Fixed authored valley terrain",groundMesh,terrain,true);
            var rock=ValleyArtAssets.Solid(new Color(.87f,.89f,.88f));rock.SetTexture("_MainTex",Tex("Rock_BaseColor"));rock.SetTexture("_BumpMap",Tex("Rock_Normal"));rock.SetFloat("_BumpScale",.26f);rock.SetTextureScale("_MainTex",new Vector2(.35f,.35f));rock.SetTextureScale("_BumpMap",new Vector2(.35f,.35f));rock=Save(rock,"Valley_Limestone.mat");
            for(int side=-1;side<=1;side+=2){var cliff=Save(Bank(side),"Bank_"+side+".asset");MeshObject("Rock bank · "+side,cliff,rock);}
            var water=new Material(Shader.Find("Little Castle/Water/LC Water"));water.SetTexture("_DetailTex",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundBiomeStudyBuilder.Root+"/Textures/Water_04_BaseColor.png"));water.SetColor("_ShallowColor",new Color(.15f,.43f,.44f));water.SetColor("_DeepColor",new Color(.045f,.24f,.35f));water.SetFloat("_WaveScale",.19f);water.SetFloat("_Speed",.7f);water.SetFloat("_FoamStrength",.7f);water.SetFloat("_CurrentHighlights",.65f);water=Save(water,"Valley_Water.mat");MeshObject("Wide river · 20–26m · separate flow surface",Save(River(),"Valley_River.asset"),water);
            var masonry=ValleyArtAssets.Solid(new Color(.83f,.81f,.74f));masonry.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Imported/Bridge_Stone_A/v002/Textures/T_WallStoneSurface_A_BaseColor.png"));masonry=Save(masonry,"Study_Bridge_Stone.mat");
            var bridge=MeshObject("Study-only wide two-arch bridge",Save(Bridge(),"Study_WideBridge.asset"),masonry);bridge.transform.position=new Vector3(Center(0),0,0);var deck=bridge.AddComponent<BoxCollider>();deck.center=new Vector3(0,5.85f,0);deck.size=new Vector3(32,.35f,5.2f);
            var bridgeRoad=ValleyArtAssets.Solid(new Color(.85f,.72f,.52f));bridgeRoad.SetTexture("_MainTex",Tex("Path_diffuseOriginal"));bridgeRoad=Save(bridgeRoad,"Study_Deck.mat");var deckMesh=new Work();deckMesh.Box(new Vector3(Center(0),6.05f,0),new Vector3(31.8f,.08f,5.0f));var walkingMesh=deckMesh.Finish("Deck");walkingMesh.colors=Enumerable.Repeat(new Color(1,0,0,1),walkingMesh.vertexCount).ToArray();MeshObject("Bridge walking surface",Save(walkingMesh,"Study_Deck.asset"),terrain);
            var treesRoot=new GameObject("Authored forest groups · Oak A B C D and conifers");
            var oaks="ABCD".Select(c=>AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Oak_"+c+"_Study.prefab")).ToArray();var spruce=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Spruce_A_Study.prefab");
            Vector2[] anchors={new Vector2(-65,-40),new Vector2(-50,-23),new Vector2(-55,12),new Vector2(-44,38),new Vector2(-67,58),new Vector2(-29,61),new Vector2(19,-38),new Vector2(32,-27),new Vector2(29,19),new Vector2(12,70)};
            int treeCount=0;
            for(int cluster=0;cluster<anchors.Length;cluster++)for(int member=0;member<7;member++)
            {
                float angle=member*2.4f+cluster,radius=member==0?0:5+(member%3)*3;Vector2 p=anchors[cluster]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                if(Snow(p.x,p.y)>.43f||Road(p.x,p.y)>.1f||Mathf.Abs(p.x-Center(p.y))<Width(p.y)+5)continue;
                var source=member%4==0?spruce:member%4==1?secondFir:oaks[(member+cluster)%4];var obj=(GameObject)PrefabUtility.InstantiatePrefab(source);obj.transform.SetParent(treesRoot.transform);obj.transform.position=new Vector3(p.x,Height(p.x,p.y),p.y);obj.transform.rotation=Quaternion.Euler(0,cluster*53+member*71,0)*source.transform.rotation;
                var bounds=BoundsOf(obj);float desired=member%3==0?8.3f+member*.55f:7.0f+(member%4)*.9f;obj.transform.localScale*=desired/Mathf.Max(.1f,bounds.size.y);bounds=BoundsOf(obj);obj.transform.position+=Vector3.up*(Height(p.x,p.y)-bounds.min.y-.05f);treeCount++;
            }
            int grassCount=Grass();
            var rockRoot=new GameObject("Temporary bank rocks · art replacement later");
            for(int i=0;i<30;i++)
            {
                float z=-68+i*4.8f;int side=i%2==0?-1:1;if(Ford(z)>.3f)continue;float x=Center(z)+side*(Width(z)-.4f);var obj=MeshObject("Bank rock "+i,Save(Boulder(i),"Rock_"+i+".asset"),rock);obj.transform.SetParent(rockRoot.transform);obj.transform.position=new Vector3(x,-.15f,z);obj.transform.rotation=Quaternion.Euler(0,i*47,0);
            }
            var snowfall=MeshObject("Local winter snowfall",Save(Snowflakes(),"Snowfall_Field.asset"),Save(new Material(Shader.Find("Little Castle/Weather/LC Snowfall")),"Snowfall.mat"));snowfall.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            var camera=new GameObject("Valley strategy camera").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=45;camera.nearClipPlane=.15f;camera.farClipPlane=650;camera.allowHDR=true;camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=RenderSettings.fogColor;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;var controller=camera.gameObject.AddComponent<LittleCastle.CameraSystem.StrategyCameraController>();controller.enabled=false;var cameraFields=new SerializedObject(controller);cameraFields.FindProperty("enableEdgeScroll").boolValue=false;cameraFields.ApplyModifiedPropertiesWithoutUndo();
            var preview=camera.gameObject.AddComponent<ValleyArtPreview>();preview.profile=pipeline;preview.studyCamera=camera;preview.sun=sun;preview.globals=globals;preview.snowfall=snowfall;preview.water=water;preview.SetView(0);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            var previous=QualitySettings.renderPipeline;QualitySettings.renderPipeline=pipeline;
            try{GroundBiomeVegetationPreflight.Capture(camera,Path.Combine(Output,"editor_overview.png"));}finally{QualitySettings.renderPipeline=previous;}
            var diagnostics=new StringBuilder();foreach(var shader in new[]{terrain.shader,water.shader,masonry.shader,snowfall.GetComponent<Renderer>().sharedMaterial.shader})foreach(var m in ShaderUtil.GetShaderMessages(shader))diagnostics.AppendLine(shader.name+": "+m.severity+": "+m.message);
            File.WriteAllText(Path.Combine(Output,"shader_messages.txt"),diagnostics.ToString());if(diagnostics.ToString().Contains(": Error:"))throw new Exception("Shader errors.");
            File.WriteAllText(Path.Combine(Output,"scene_result.txt"),"Scene="+ScenePath+"\nTrees="+treeCount+" (Oak A/B/C/D + Spruce A + UNS fir; rejected Tree01-04 excluded)\nGrass clumps="+grassCount+"\nRiver width at bridge="+(Width(0)*2)+" m\nBank height="+Land(Width(0),0)+" m\nFord water depth="+(WaterY-Height(Center(45),45))+" m\nBridge is an isolated study mesh, not a modification of immutable Bridge_Stone_A.\n");
        }
        public static void BuildAndPlayer(){Build();var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=Path.Combine(Output,"Player/ValleyArt.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});File.WriteAllText(Path.Combine(Output,"build_result.txt"),"Result="+report.summary.result+"\nErrors="+report.summary.totalErrors+"\nWarnings="+report.summary.totalWarnings+"\nTime="+report.summary.totalTime);if(Application.isBatchMode)EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:2);}
        static Bounds BoundsOf(GameObject obj){var rs=obj.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        static Texture2D SnowMask(){const int n=512;var tex=new Texture2D(n,n,TextureFormat.R8,true,true){wrapMode=TextureWrapMode.Clamp};var c=new Color[n*n];for(int z=0;z<n;z++)for(int x=0;x<n;x++)c[z*n+x]=new Color(Snow(-130+260*x/(n-1f),-120+240*z/(n-1f)),0,0,1);tex.SetPixels(c);tex.Apply(true,false);return tex;}
        static Mesh Ground()
        {
            const int nx=224,nz=320;var v=new Vector3[(nx+1)*(nz+1)];var colors=new Color[v.Length];var uv=new Vector2[v.Length];var triangles=new int[nx*nz*6];
            for(int z=0;z<=nz;z++){float wz=-120+240*z/(float)nz,w=Width(wz);for(int x=0;x<=nx;x++){float d=x<=80?Mathf.Lerp(-130,-w-2,x/80f):x<=96?Mathf.Lerp(-w-2,-w+1,(x-80)/16f):x<=128?Mathf.Lerp(-w+1,w-1,(x-96)/32f):x<=144?Mathf.Lerp(w-1,w+2,(x-128)/16f):Mathf.Lerp(w+2,130,(x-144)/80f);float wx=Center(wz)+d;int at=z*(nx+1)+x;v[at]=new Vector3(wx,Height(wx,wz),wz);uv[at]=new Vector2(wx*.1f,wz*.1f);float wet=1-S(.25f,1.3f,Mathf.Abs(v[at].y-WaterY));colors[at]=new Color(Road(wx,wz)*(1-wet),wet,0,1);}}
            int k=0;for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;triangles[k++]=a;triangles[k++]=c;triangles[k++]=b;triangles[k++]=b;triangles[k++]=c;triangles[k++]=d;}
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.vertices=v;mesh.colors=colors;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh River()
        {
            const int nz=320,nx=32;var v=new Vector3[(nz+1)*(nx+1)];var c=new Color[v.Length];var uv=new Vector2[v.Length];var tri=new int[nz*nx*6];
            for(int z=0;z<=nz;z++){float wz=-120+240*z/(float)nz,left=WaterEdge(wz,-1),right=WaterEdge(wz,1);for(int x=0;x<=nx;x++){int i=z*(nx+1)+x;float cross=Mathf.Lerp(-left,right,x/(float)nx),wx=Center(wz)+cross;v[i]=new Vector3(wx,WaterY,wz);uv[i]=new Vector2(wz,cross);c[i]=new Color(Mathf.Clamp01((WaterY-Height(wx,wz))/3),S(.82f,1,Mathf.Abs(cross)/(cross<0?left:right)),0,1);}}
            int k=0;for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x,b=a+1,c0=a+nx+1,d=c0+1;tri[k++]=a;tri[k++]=c0;tri[k++]=b;tri[k++]=b;tri[k++]=c0;tri[k++]=d;}
            var mesh=new Mesh();mesh.vertices=v;mesh.colors=c;mesh.uv=uv;mesh.triangles=tri;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Bank(int side)
        {
            var work=new Work();for(int i=0;i<62;i++){float z=-90+i*3,z2=z+3;if(Ford(z)>.15f||Ford(z2)>.15f)continue;Vector3[] a=new Vector3[3],b=new Vector3[3];for(int ring=0;ring<3;ring++){float y=ring==0?-.4f:ring==1?2.5f:Height(Center(z)+side*(Width(z)+.7f),z)-.08f;float y2=ring==0?-.4f:ring==1?2.5f:Height(Center(z2)+side*(Width(z2)+.7f),z2)-.08f;float offset=ring==0?-1.05f:ring==1?-.60f:.60f;a[ring]=new Vector3(Center(z)+side*(Width(z)+offset+.48f*Mathf.Sin(i*2.1f+ring)),y,z);b[ring]=new Vector3(Center(z2)+side*(Width(z2)+offset+.48f*Mathf.Sin((i+1)*2.1f+ring)),y2,z2);}for(int ring=0;ring<2;ring++){if(side<0)work.Quad(b[ring],a[ring],a[ring+1],b[ring+1]);else work.Quad(a[ring],b[ring],b[ring+1],a[ring+1]);}}return work.Finish("Rock banks");
        }
        static Mesh Bridge()
        {
            var w=new Work();w.Box(new Vector3(0,5.8f,0),new Vector3(32,.5f,6.4f));
            for(int side=-1;side<=1;side+=2)
            {
                for(float x=-15.5f;x<16;x+=1.05f)for(float y=.4f;y<5.6f;y+=.63f){float local=Mathf.Min(Mathf.Abs(x+7.7f),Mathf.Abs(x-7.7f));float ceiling=local<6.3f?.3f+4.35f*Mathf.Sqrt(1-local*local/(6.3f*6.3f)):0;if(y-.31f>ceiling)w.Box(new Vector3(x,y,side*2.9f),new Vector3(1.01f,.60f,.7f));}
                for(int arch=-1;arch<=1;arch+=2)for(int i=0;i<20;i++){float a=i*Mathf.PI/20,b=(i+1)*Mathf.PI/20;Vector3 P(float angle,float r)=>new Vector3(arch*7.7f+Mathf.Cos(angle)*6.3f*r,.3f+Mathf.Sin(angle)*4.35f*r,side*2.92f);w.Prism(P(a,1),P(b,1),P(b,1.14f),P(a,1.14f),.78f);}
                for(int i=0;i<32;i++)w.Box(new Vector3(-15.5f+i,6.53f,side*2.95f),new Vector3(.96f,.95f,.64f));
                w.Box(new Vector3(0,7.07f,side*2.95f),new Vector3(32,.18f,.82f));
                for(int i=0;i<5;i++)w.Box(new Vector3(-16+i*8,6.80f,side*3.02f),new Vector3(.85f,1.5f,.85f));
            }
            foreach(float x in new[]{-15.8f,0,15.8f})w.Box(new Vector3(x,2.6f,0),new Vector3(1.8f,6.4f,7.0f));return w.Finish("Study two-arch bridge");
        }
        static Mesh Boulder(int seed){var w=new Work();var rings=new Vector3[2,9];float width=.9f+(seed%4)*.45f,height=.8f+(seed%3)*.7f;for(int r=0;r<2;r++)for(int i=0;i<9;i++){float angle=i*Mathf.PI*2/9,rad=width*(r==0?1:.65f)*(1+.12f*Mathf.Sin(i*4.1f+seed));rings[r,i]=new Vector3(Mathf.Cos(angle)*rad,r==0?0:height*(.9f+.15f*Mathf.Sin(i*3+seed)),Mathf.Sin(angle)*rad*.8f);}for(int i=0;i<9;i++){int j=(i+1)%9;w.Quad(rings[0,j],rings[0,i],rings[1,i],rings[1,j]);w.Tri(rings[1,j],rings[1,i],new Vector3(0,height*1.1f,0));}return w.Finish("Temporary bank boulder");}
        static int Grass()
        {
            var mat=new Material(AssetDatabase.LoadAssetAtPath<Material>(GroundBiomeVegetationPreflight.Study+"/Study_UNS_Grass.mat"));mat.SetColor("_Color",new Color(.31f,.42f,.14f));mat.SetColor("_RootColor",new Color(.17f,.26f,.065f));mat.SetColor("_TipColor",new Color(.39f,.51f,.19f));mat.SetFloat("_TransmissionStrength",.25f);mat.SetFloat("_WindAmplitude",.16f);mat.SetFloat("_MicroFlutter",.025f);mat=Save(mat,"Valley_Grass.mat");mat.enableInstancing=true;
            var obj=new GameObject("Authored grass · four mesh LODs").AddComponent<InnerverseGrassStudy>();obj.maximumDistance=190;obj.receiveGroundShadows=true;obj.grassMaterial=mat;obj.lodMeshes=ValleyArtAssets.Prefab("UNS_Grass").GetComponentInChildren<LODGroup>().GetLODs().Select(l=>l.renderers[0].GetComponent<MeshFilter>().sharedMesh).ToArray();
            var list=new List<Matrix4x4>();for(int ix=0;ix<125;ix++)for(int iz=0;iz<123;iz++){float x=-78+ix*1.25f+(DeterministicHash.Hash01(31,ix,iz,0)-.5f),z=-74+iz*1.25f+(DeterministicHash.Hash01(31,ix,iz,1)-.5f);if(Snow(x,z)>.25f||Road(x,z)>.05f||Mathf.Abs(x-Center(z))<Width(z)+2.1f||DeterministicHash.Hash01(31,ix,iz,2)>.36f)continue;float scale=Mathf.Lerp(.4f,.75f,DeterministicHash.Hash01(31,ix,iz,3));list.Add(Matrix4x4.TRS(new Vector3(x,Height(x,z),z),Quaternion.Euler(0,ix*71+iz*53,0),Vector3.one*scale));}obj.authoredMatrices=list.ToArray();return list.Count;
        }
        static Mesh Snowflakes(){var v=new List<Vector3>();var uv=new List<Vector2>();var seeds=new List<Vector2>();var tri=new List<int>();for(int i=0;i<2200;i++){float x=36+DeterministicHash.Hash01(41,i,0,0)*66,z=8+DeterministicHash.Hash01(41,i,0,1)*86;if(Snow(x,z)<.65f)continue;Vector3 center=new Vector3(x,Height(x,z)+.2f,z);int at=v.Count;for(int j=0;j<4;j++){v.Add(center);uv.Add(new Vector2(j==1||j==2?1:0,j>=2?1:0));seeds.Add(new Vector2(DeterministicHash.Hash01(41,i,0,2),DeterministicHash.Hash01(41,i,0,3)));}tri.AddRange(new[]{at,at+1,at+2,at,at+2,at+3});}var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetUVs(1,seeds);m.SetTriangles(tri,0);m.bounds=new Bounds(new Vector3(70,20,55),new Vector3(95,70,125));return m;}
        sealed class Work
        {
            readonly List<Vector3> v=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();readonly List<int> t=new List<int>();
            void Vertex(Vector3 p,Vector3 normal){v.Add(p);uv.Add(Mathf.Abs(normal.y)>.7f?new Vector2(p.x,p.z)*.21f:Mathf.Abs(normal.x)>.7f?new Vector2(p.z,p.y)*.21f:new Vector2(p.x,p.y)*.21f);}
            public void Tri(Vector3 a,Vector3 b,Vector3 c){int at=v.Count;Vector3 n=Vector3.Cross(b-a,c-a).normalized;Vertex(a,n);Vertex(b,n);Vertex(c,n);t.AddRange(new[]{at,at+1,at+2});}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int at=v.Count;Vector3 n=Vector3.Cross(b-a,c-a).normalized;Vertex(a,n);Vertex(b,n);Vertex(c,n);Vertex(d,n);t.AddRange(new[]{at,at+1,at+2,at,at+2,at+3});}
            public void Box(Vector3 c,Vector3 size){Vector3 a=c-size*.5f,b=c+size*.5f;Quad(new Vector3(a.x,a.y,b.z),new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z));Quad(new Vector3(b.x,a.y,a.z),new Vector3(a.x,a.y,a.z),new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z));Quad(new Vector3(a.x,a.y,a.z),new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z));Quad(new Vector3(b.x,a.y,b.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,b.y,b.z));Quad(new Vector3(a.x,b.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,b.y,a.z),new Vector3(a.x,b.y,a.z));Quad(new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z),new Vector3(a.x,a.y,b.z));}
            public void Prism(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float depth){Vector3 off=Vector3.forward*depth*.5f;Quad(a-off,b-off,c-off,d-off);Quad(d+off,c+off,b+off,a+off);Quad(a+off,b+off,b-off,a-off);Quad(b+off,c+off,c-off,b-off);Quad(c+off,d+off,d-off,c-off);Quad(d+off,a+off,a-off,d-off);}
            public Mesh Finish(string name){var m=new Mesh{name=name,indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();m.RecalculateTangents();return m;}
        }
    }
}
