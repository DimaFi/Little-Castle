using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LittleCastle.Rendering;
using LittleCastle.Research;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.Editor
{
    public static class MeadowStudyBuilder
    {
        public const string Root="Assets/_Game/Art/Studies/Meadow_20261025";
        public const string ScenePath="Assets/_Game/Scenes/Meadow_20261025.unity";
        public static string Output=>Path.Combine(MeadowGrassPreflight.Output,"Pass03_LushMeadow");
        public static float Height(float x,float z)=>.25f+.22f*Mathf.Sin(x*.085f)*Mathf.Cos(z*.068f)+
            3.0f*Mathf.Exp(-((x+22)*(x+22)/240+(z-25)*(z-25)/380))+
            2.2f*Mathf.Exp(-((x-25)*(x-25)/310+(z-43)*(z-43)/260));
        static float PathX(float z)=>2+5*Mathf.Sin(z*.05f)+1.2f*Mathf.Sin(z*.11f);
        static float PathDistance(float x,float z)=>Mathf.Abs(x-PathX(z));
        static T Save<T>(T obj,string file)where T:UnityEngine.Object=>ValleyArtAssets.Save(obj,Root+"/"+file);
        static MeadowGrassData PreserveCoverageBaseline()
        {
            const string file="Pass03_BaselineGrass.asset";
            var saved=AssetDatabase.LoadAssetAtPath<MeadowGrassData>(Root+"/"+file);if(saved)return saved;
            var current=AssetDatabase.LoadAssetAtPath<MeadowGrassData>(Root+"/Meadow_InstanceData.asset");
            if(!current)throw new InvalidOperationException("Existing meadow data required for before/after coverage check.");
            var copy=UnityEngine.Object.Instantiate(current);copy.lodMeshes=new Mesh[current.lodMeshes.Length];
            for(int i=0;i<copy.lodMeshes.Length;i++)copy.lodMeshes[i]=SaveMesh(UnityEngine.Object.Instantiate(current.lodMeshes[i]),"Pass03_Before_LOD"+i+".asset");
            return Save(copy,file);
        }
        static Mesh SaveMesh(Mesh mesh,string file)
        {
            string path=Root+"/"+file;var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!old){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            old.Clear();old.indexFormat=mesh.indexFormat;old.vertices=mesh.vertices;old.normals=mesh.normals;old.uv=mesh.uv;old.colors=mesh.colors;old.triangles=mesh.triangles;
            var roots=new List<Vector4>();mesh.GetUVs(1,roots);old.SetUVs(1,roots);old.bounds=mesh.bounds;EditorUtility.SetDirty(old);AssetDatabase.SaveAssetIfDirty(old);UnityEngine.Object.DestroyImmediate(mesh);return old;
        }
        [MenuItem("Little Castle/Research/Meadow/Build Green Meadow")]
        public static void Build()
        {
            if(!Path.GetFileName(Path.GetDirectoryName(Application.dataPath)).StartsWith("Little-Castle_URP_"))throw new Exception("URP study copy only.");
            for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene preserved.");
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var baseline=PreserveCoverageBaseline();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.33f,.40f,.49f);RenderSettings.ambientEquatorColor=new Color(.25f,.30f,.20f);RenderSettings.ambientGroundColor=new Color(.13f,.17f,.08f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.63f,.74f,.79f);RenderSettings.fogDensity=.0032f;
            RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/StylizedDayNightSky.mat");
            var sun=new GameObject("Meadow warm sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(39,-38,0);sun.intensity=1.05f;sun.color=new Color(1,.95f,.82f);sun.shadows=LightShadows.Soft;sun.shadowBias=.035f;sun.shadowNormalBias=.13f;RenderSettings.sun=sun;
            var globals=new GameObject("Shared LC meadow light and wind").AddComponent<StylizedLightingGlobals>();globals.SetWindPresentation(new Vector2(.86f,.5f),.6f,.65f);
            var pipeline=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ValleyArtAssets.Root+"/Valley_URP_Profile.asset"));pipeline.shadowDistance=90;pipeline.mainLightShadowmapResolution=2048;pipeline.cascade4Split=new Vector3(.09f,.25f,.52f);pipeline.msaaSampleCount=4;pipeline=Save(pipeline,"Meadow_URP.asset");
            var grade=ScriptableObject.CreateInstance<VolumeProfile>();grade.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);var colors=grade.Add<ColorAdjustments>(true);colors.contrast.Override(5);colors.saturation.Override(1);colors.postExposure.Override(.12f);grade=Save(grade,"Meadow_Grade.asset");foreach(var c in grade.components)if(!AssetDatabase.Contains(c))AssetDatabase.AddObjectToAsset(c,grade);EditorUtility.SetDirty(grade);AssetDatabase.SaveAssetIfDirty(grade);
            var volume=new GameObject("Meadow restrained color grade").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=grade;
            var ground=new Material(AssetDatabase.LoadAssetAtPath<Material>(ValleyArtAssets.Root+"/Valley_Terrain.mat"));ground.SetFloat("_UseSnow",0);ground.SetColor("_GrassColor",new Color(.32f,.42f,.20f));ground.SetColor("_DryGrassColor",new Color(.415f,.455f,.255f));ground.SetFloat("_GrassDetail",.16f);ground.SetFloat("_GrassNormalScale",.35f);ground.SetFloat("_SurfaceDetailStrength",.4f);ground.SetFloat("_SurfaceAOStrength",.3f);ground.SetFloat("_MacroStrength",.04f);ground.SetFloat("_ContactShadeStrength",.14f);ground.SetFloat("_WorldTiling",.2f);ground=Save(ground,"Meadow_Ground.mat");
            ground.EnableKeyword("_LC_SURFACE_PAINT");ground.SetFloat("_UseSurfacePaint",1);ground.SetFloat("_DirtDetail",.20f);EditorUtility.SetDirty(ground);
            var groundObject=new GameObject("Hand-authored meadow terrain and path");groundObject.AddComponent<MeshFilter>().sharedMesh=SaveMesh(Ground(),"Meadow_Ground.asset");groundObject.AddComponent<MeshRenderer>().sharedMaterial=ground;groundObject.AddComponent<MeshCollider>().sharedMesh=groundObject.GetComponent<MeshFilter>().sharedMesh;
            var grass=new Material(Shader.Find("Little Castle/Foliage/LC Grass")){enableInstancing=true};grass.SetFloat("_MeadowMode",1);grass.EnableKeyword("_LC_MEADOW");grass.SetFloat("_UseRootAnchor",1);grass.SetFloat("_MeadowPaletteStrength",.7f);grass.SetColor("_Color",new Color(.32f,.43f,.205f));grass.SetColor("_RootColor",new Color(.205f,.30f,.125f));grass.SetColor("_TipColor",new Color(.405f,.505f,.26f));grass.SetColor("_DryColor",new Color(.415f,.455f,.255f));grass.SetFloat("_TransmissionStrength",.3f);grass.SetFloat("_UpNormalBias",.48f);grass.SetFloat("_MeadowRootOcclusion",.30f);grass.SetFloat("_WindAmplitude",.14f);grass.SetFloat("_WindBend",1.6f);grass.SetFloat("_MicroFlutter",.014f);grass.SetFloat("_GustStrength",.7f);grass.SetFloat("_GustScale",.08f);grass.SetFloat("_GustSpeed",.4f);grass.SetFloat("_InteractionStrength",1);grass.SetFloat("_InteractionDistance",28);grass.SetFloat("_DensityNear",16);grass.SetFloat("_DensityFar",90);grass.SetFloat("_DensityFloor",.12f);grass.SetFloat("_MeadowSoftness",.9f);grass.SetFloat("_ColorVariation",.045f);grass.SetFloat("_FadeStart",108);grass.SetFloat("_FadeEnd",128);grass=Save(grass,"Meadow_Blades.mat");
            var data=ScriptableObject.CreateInstance<MeadowGrassData>();data.lodMeshes=new Mesh[4];for(int i=0;i<4;i++)data.lodMeshes[i]=SaveMesh(Blades(i),"Meadow_Blades_LOD"+i+".asset");
            grass.EnableKeyword("_LC_SURFACE_PAINT");grass.SetFloat("_UseSurfacePaint",1);EditorUtility.SetDirty(grass);
            var patches=new List<MeadowGrassData.Patch>();var accents=new List<Matrix4x4>();
            for(int cz=-5;cz<8;cz++)for(int cx=-6;cx<6;cx++)
            {
                var random=new System.Random(9281^(cx*73856093)^(cz*19349663));var matrices=new List<Matrix4x4>();
                for(int z=0;z<11;z++)for(int x=0;x<11;x++)
                {
                    float px=cx*8+(x+.12f+(float)random.NextDouble()*.76f)*8/11,pz=cz*8+(z+.12f+(float)random.NextDouble()*.76f)*8/11;
                    float distance=PathDistance(px,pz);
                    float height=(.78f+(float)random.NextDouble()*.45f)*Mathf.Lerp(.72f,1.15f,Mathf.PerlinNoise(px*.085f+35,pz*.085f+19));
                    var scale=new Vector3(.88f,height,.88f);var normal=new Vector3(Height(px-.1f,pz)-Height(px+.1f,pz),.2f,Height(px,pz-.1f)-Height(px,pz+.1f)).normalized;var rotation=Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.Euler(0,(float)random.NextDouble()*360,0);var matrix=Matrix4x4.TRS(new Vector3(px,Height(px,pz)-.025f,pz),rotation,scale);matrices.Add(matrix);
                    if(random.NextDouble()<.007 && distance>2.4f)accents.Add(Matrix4x4.TRS(new Vector3(px,Height(px,pz),pz),Quaternion.Euler(0,(float)random.NextDouble()*360,0),Vector3.one*.8f));
                }
                if(matrices.Count==0)continue;
                var bounds=new Bounds(new Vector3(cx*8+4,Height(cx*8+4,cz*8+4),cz*8+4),new Vector3(8,0,8));foreach(var m in matrices)bounds.Encapsulate(m.GetColumn(3));bounds.Expand(new Vector3(3.2f,3.2f,3.2f));
                patches.Add(new MeadowGrassData.Patch{bounds=bounds,matrices=matrices.ToArray()});data.authoredClumps+=matrices.Count;
            }
            data.patches=patches.ToArray();data=Save(data,"Meadow_InstanceData.asset");var renderer=new GameObject("Meadow batched GPU grass · 8m culling patches").AddComponent<MeadowGrassRenderer>();renderer.data=data;renderer.material=grass;
            var imported=new GameObject("Existing UNS grass accents · original authored LODs").AddComponent<InnerverseGrassStudy>();imported.authoredMatrices=accents.ToArray();imported.maximumDistance=60;imported.receiveGroundShadows=true;imported.lodMeshes=ValleyArtAssets.Prefab("UNS_Grass").GetComponentInChildren<LODGroup>().GetLODs().Select(l=>l.renderers[0].GetComponent<MeshFilter>().sharedMesh).ToArray();
            var accent=new Material(AssetDatabase.LoadAssetAtPath<Material>(ValleyArtAssets.Root+"/Valley_Grass.mat"));accent.SetColor("_Color",new Color(.24f,.33f,.15f));accent.SetColor("_TipColor",new Color(.32f,.415f,.205f));accent.SetFloat("_UseObjectAnchorFade",1);accent.SetFloat("_FadeStart",28);accent.SetFloat("_FadeEnd",58);accent.EnableKeyword("_LC_SURFACE_PAINT");accent.SetFloat("_UseSurfacePaint",1);imported.grassMaterial=Save(accent,"Meadow_ExistingGrass.mat");
            Vector2[] trees={new Vector2(-19,17),new Vector2(23,28),new Vector2(-32,39),new Vector2(33,49),new Vector2(-39,52),new Vector2(15,54)};
            for(int i=0;i<trees.Length;i++){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ValleyArtAssets.Root+"/Oak_"+"ABCD"[i%4]+"_Study.prefab");var tree=(GameObject)PrefabUtility.InstantiatePrefab(prefab);tree.transform.position=new Vector3(trees[i].x,Height(trees[i].x,trees[i].y),trees[i].y);tree.transform.rotation=Quaternion.Euler(0,i*61,0);tree.transform.localScale*=1.1f;}
            var camera=new GameObject("Meadow strategy camera").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=44;camera.nearClipPlane=.1f;camera.farClipPlane=300;camera.allowHDR=true;camera.clearFlags=CameraClearFlags.Skybox;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            var controls=camera.gameObject.AddComponent<LittleCastle.CameraSystem.StrategyCameraController>();controls.enabled=false;var serialized=new SerializedObject(controls);serialized.FindProperty("enableEdgeScroll").boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();
            var interaction=new GameObject("Nearby grass interaction · four shader sources maximum").AddComponent<GrassInteractionManager>();interaction.focusCamera=camera;
            var mover=GameObject.CreatePrimitive(PrimitiveType.Capsule);mover.name="Interaction demonstrator · visual proxy, no physics";mover.transform.localScale=new Vector3(.55f,.7f,.55f);UnityEngine.Object.DestroyImmediate(mover.GetComponent<Collider>());var moverMat=ValleyArtAssets.Solid(new Color(.82f,.61f,.29f));mover.GetComponent<Renderer>().sharedMaterial=Save(moverMat,"Interaction_Proxy.mat");var source=mover.AddComponent<GrassInteractionSource>();source.radius=1.05f;source.heightOffset=-.7f;
            var preview=camera.gameObject.AddComponent<MeadowPreview>();preview.profile=pipeline;preview.studyCamera=camera;preview.globals=globals;preview.sun=sun;preview.grass=renderer;preview.interaction=interaction;preview.demonstrator=mover.transform;preview.SetView(0);
            var surface=ScriptableObject.CreateInstance<SurfacePaintData>();
            var pathStroke=new SurfacePaintStroke{id="authored-meadow-path-001",road=true,radius=1.85f};for(int z=-40;z<=64;z+=2)pathStroke.points.Add(new Vector2(PathX(z),z));surface.authored.strokes.Add(pathStroke);
            Vector2[] patchesForPlants={new Vector2(-8,4),new Vector2(9,8),new Vector2(-12,15),new Vector2(14,19),new Vector2(-22,23),new Vector2(21,27),new Vector2(-6,-8),new Vector2(11,-12)};
            for(int i=0;i<patchesForPlants.Length;i++)surface.authored.strokes.Add(new SurfacePaintStroke{id="authored-meadow-plants-"+i,road=false,scatter=true,scatterProfile=MeadowScatterProfile.Flowers,radius=4.5f,points=new List<Vector2>{patchesForPlants[i]}});
            surface.authored.strokes.Add(new SurfacePaintStroke{id="authored-tree-floor-001",road=false,scatter=true,scatterProfile=MeadowScatterProfile.ForestFloor,radius=4.8f,points=new List<Vector2>{new Vector2(-19,17)}});
            surface.authored.strokes.Remove(pathStroke);surface.authored.strokes.Add(pathStroke);
            surface=Save(surface,"Meadow_AuthoredSurface.asset");var scatter=new GameObject("Meadow flowers and clutter · instanced brush library").AddComponent<MeadowScatterRenderer>();scatter.library=MeadowScatterAssets.Build();scatter.candidateSpacing=.9f;
            var painter=new GameObject("Shared author and player meadow brush").AddComponent<MeadowSurfacePainter>();painter.source=surface;painter.studyCamera=camera;painter.terrainMaterial=ground;painter.grassMaterial=grass;painter.accentMaterial=imported.grassMaterial;painter.scatterRenderer=scatter;painter.road=false;painter.currentProfile=MeadowScatterProfile.Flowers;painter.radius=3;preview.painter=painter;preview.baselineGrass=baseline;
            var cursorMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));cursorMaterial.SetColor("_BaseColor",new Color(1,.9f,.4f));painter.brushMaterial=Save(cursorMaterial,"Meadow_BrushPreview.mat");
            string maskPath=Root+"/Meadow_AuthoredMask.asset";var mask=AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            if(!mask){mask=new Texture2D(MeadowSurfacePainter.Resolution,MeadowSurfacePainter.Resolution,TextureFormat.RG16,false,true){name="Meadow authored surface preview",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};AssetDatabase.CreateAsset(mask,maskPath);}
            mask.SetPixelData(MeadowSurfacePainter.BuildAuthoredPixels(surface),0);mask.Apply(false,false);EditorUtility.SetDirty(mask);
            painter.authoredPreviewMask=mask;
            foreach(var material in new[]{ground,grass,imported.grassMaterial}){MeadowSurfacePainter.BindMask(material,mask,surface.bounds);EditorUtility.SetDirty(material);}
            // Validate exact geometry and material, including UV0 bend and UV1 root data.
            var test=new GameObject("Meadow exact mesh preflight");test.AddComponent<MeshFilter>().sharedMesh=data.lodMeshes[0];test.AddComponent<MeshRenderer>().sharedMaterial=grass;Selection.activeGameObject=test;FoliageModelCompatibilityValidator.ValidateSelected();UnityEngine.Object.DestroyImmediate(test);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            var messages=new StringBuilder();foreach(var shader in new[]{grass.shader,ground.shader})foreach(var m in ShaderUtil.GetShaderMessages(shader))messages.AppendLine(shader.name+": "+m.severity+": "+m.message);File.WriteAllText(Path.Combine(Output,"shader_messages.txt"),messages.ToString());
            File.WriteAllText(Path.Combine(Output,"scene_result.txt"),$"Scene={ScenePath}\nPatches={patches.Count}\nClumps={data.authoredClumps}\nMaximum blades={data.authoredClumps*48}\nExisting UNS accents={accents.Count}\nLOD triangles={string.Join(",",data.lodMeshes.Select(m=>m.GetIndexCount(0)/3))}\nDensity16..90m; interaction28m; fade108..128m; max4sources.\n");
            if(messages.ToString().Contains(": Error:"))throw new Exception("Meadow shader error.");
        }
        public static void BuildAndPlayer()
        {
            Build();PlayerSettings.enableFrameTimingStats=true;var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=Path.Combine(Output,"Player/Meadow.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});File.WriteAllText(Path.Combine(Output,"build_result.txt"),$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nTime={report.summary.totalTime}");if(Application.isBatchMode)EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:2);
        }
        public static void BuildInteractivePlayer()
        {
            Directory.CreateDirectory(Output);
            bool previousTiming=PlayerSettings.enableFrameTimingStats;BuildReport report;
            try{PlayerSettings.enableFrameTimingStats=false;report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=Path.Combine(Output,"Interactive/Meadow.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});}
            finally{PlayerSettings.enableFrameTimingStats=previousTiming;}
            File.WriteAllText(Path.Combine(Output,"interactive_build_result.txt"),$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nDevelopment=false\nFrameTimingStats=false\nTime={report.summary.totalTime}");
            if(Application.isBatchMode)EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:2);
        }
        static Mesh Ground()
        {
            const int n=192;var v=new Vector3[(n+1)*(n+1)];var colors=new Color[v.Length];var uv=new Vector2[v.Length];var tri=new int[n*n*6];
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){int i=z*(n+1)+x;float px=-100+x*200f/n,pz=-85+z*200f/n;v[i]=new Vector3(px,Height(px,pz),pz);float d=PathDistance(px,pz);float path=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.95f,1.9f,d+.1f*Mathf.Sin(pz*2+px*3)));colors[i]=new Color(path,0,0,1);uv[i]=new Vector2(px,pz);}
            int t=0;for(int z=0;z<n;z++)for(int x=0;x<n;x++){int i=z*(n+1)+x;tri[t++]=i;tri[t++]=i+n+1;tri[t++]=i+1;tri[t++]=i+1;tri[t++]=i+n+1;tri[t++]=i+n+2;}
            var mesh=new Mesh{name="Meadow ground",indexFormat=IndexFormat.UInt32,vertices=v,colors=colors,uv=uv,triangles=tri};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Blades(int lod)
        {
            var random=new System.Random(761234);var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var roots=new List<Vector4>();var triangles=new List<int>();int count=lod<2?48:lod==2?20:8;
            for(int i=0;i<48;i++)
            {
                float a=(float)random.NextDouble()*Mathf.PI*2,r=Mathf.Sqrt((float)random.NextDouble())*.48f;var p=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);float height=.22f+Mathf.Pow((float)random.NextDouble(),1.6f)*.28f,width=.038f+(float)random.NextDouble()*.030f;float angle=(float)random.NextDouble()*Mathf.PI*2;Vector3 side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*width;Vector3 lean=new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle))*(.10f+(float)random.NextDouble()*.14f);if(i>=count)continue;
                int first=vertices.Count;var root=new Vector4(p.x,0,p.z,(i+.5f)/48);
                void Add(Vector3 point,float u,float h){vertices.Add(point);normals.Add(Vector3.Lerp(Vector3.up,new Vector3(-Mathf.Sin(angle),.3f,Mathf.Cos(angle)).normalized,.45f));uv.Add(new Vector2(u,h));roots.Add(root);}
                Add(p-side,0,0);Add(p+side,1,0);
                if(lod==0){Add(p+Vector3.up*height*.55f+lean*.38f-side*.48f,0,.55f);Add(p+Vector3.up*height*.55f+lean*.38f+side*.48f,1,.55f);Add(p+Vector3.up*height+lean,.5f,1);triangles.AddRange(new[]{first,first+2,first+1,first+1,first+2,first+3,first+2,first+4,first+3});}
                else{Add(p+Vector3.up*height+lean,.5f,1);triangles.AddRange(new[]{first,first+2,first+1});}
            }
            var mesh=new Mesh{name="Meadow blades LOD"+lod};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetUVs(1,roots);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(1.4f);mesh.bounds=bounds;return mesh;
        }
    }
}
