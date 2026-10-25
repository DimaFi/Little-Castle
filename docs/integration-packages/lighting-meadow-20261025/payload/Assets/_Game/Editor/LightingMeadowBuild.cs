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
    public static class LightingMeadowBuild
    {
        public const string Root="Assets/_Game/Settings/Rendering/LightingMeadow";
        public const string Scene="Assets/_Game/Scenes/LightingMeadow_20261025.unity";
        public static string Output=>LightingMeadowIntegration.Evidence;
        [MenuItem("Little Castle/Rendering/Lighting Meadow/Create Joint Fixture")]
        public static void Setup()
        {
            if(!Path.GetFileName(Path.GetDirectoryName(Application.dataPath)).Equals("Little-Castle_LightingIntegration_20261025"))throw new InvalidOperationException("Integration copy only.");
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/Meadow_20261025.unity",OpenSceneMode.Single);
            var preview=Object.FindAnyObjectByType<MeadowPreview>();
            if(!preview)throw new InvalidOperationException("Frozen Meadow fixture is missing.");
            var camera=preview.studyCamera;var sun=preview.sun;var globals=preview.globals;var grass=preview.grass;var painter=preview.painter;var interaction=preview.interaction;var demonstrator=preview.demonstrator;
            Object.DestroyImmediate(preview);
            foreach(var volume in Object.FindObjectsByType<Volume>())Object.DestroyImmediate(volume.gameObject);
            var hdr=Pipeline("HDR",false);var ao=Pipeline("AO",true);
            var palette=Copy<LightingPaletteProfile>(LightingBenchmarkSetup.Root+"/LinearPalette.asset",Root+"/MeadowPalette.asset");
            palette.sunIntensity=.95f;palette.daySky=new Color(.56f,.64f,.75f);palette.dayEquator=new Color(.45f,.51f,.57f);palette.dayGround=new Color(.31f,.34f,.25f);
            palette.sunColorFromElevation=true;palette.horizonColorElevation=5;palette.dayColorElevation=32;palette.sunHorizon=new Color(1,.52f,.23f);
            palette.nightSky=new Color(.50f,.60f,.78f);palette.nightEquator=new Color(.42f,.51f,.63f);palette.nightGround=new Color(.31f,.38f,.47f);EditorUtility.SetDirty(palette);
            var clock=new GameObject("Lighting Meadow fixed clock").AddComponent<WorldTimeSystem>();
            Set(clock,"timeSettingsOverride",AssetDatabase.LoadAssetAtPath<WorldTimeSettings>("Assets/_Game/Settings/World/MainWorldTimeSettings.asset"));clock.RunAutomatically=false;
            var moon=new GameObject("Lighting Meadow moon").AddComponent<Light>();moon.type=LightType.Directional;moon.shadows=LightShadows.None;
            var atmosphere=new GameObject("Lighting Meadow atmosphere").AddComponent<DayNightLightingController>();
            Set(atmosphere,"timeSystem",clock);Set(atmosphere,"sun",sun);Set(atmosphere,"moon",moon);Set(atmosphere,"skyboxMaterial",Copy<Material>("Assets/_Game/Materials/StylizedDayNightSky.mat",Root+"/Sky.mat"));
            Set(atmosphere,"dayFogStart",85f);Set(atmosphere,"dayFogEnd",180f);Set(atmosphere,"nightFogStart",65f);Set(atmosphere,"nightFogEnd",150f);Set(atmosphere,"sunAzimuth",-38f);atmosphere.Palette=palette;
            Set(globals,"timeSystem",clock);Set(globals,"atmosphere",atmosphere);Set(globals,"sun",sun);Set(globals,"moon",moon);
            globals.SetWindPresentation(new Vector2(.86f,.5f),.6f,.65f);
            var ground=Copy<Material>(LightingMeadowIntegration.Meadow+"/Meadow_Ground.mat",Root+"/Ground.mat");
            var blades=Copy<Material>(LightingMeadowIntegration.Meadow+"/Meadow_Blades.mat",Root+"/Blades.mat");
            var accent=Copy<Material>(LightingMeadowIntegration.Meadow+"/Meadow_ExistingGrass.mat",Root+"/Accents.mat");
            blades.SetFloat("_MeadowRootOcclusion",.22f);EditorUtility.SetDirty(blades);
            foreach(var renderer in Object.FindObjectsByType<MeshRenderer>())if(renderer.sharedMaterial==painter.terrainMaterial)renderer.sharedMaterial=ground;
            grass.material=blades;Object.FindAnyObjectByType<InnerverseGrassStudy>().grassMaterial=accent;
            painter.terrainMaterial=ground;painter.grassMaterial=blades;painter.accentMaterial=accent;painter.painting=false;
            var grade=ScriptableObject.CreateInstance<VolumeProfile>();grade.name="Meadow Neutral only";grade.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            string gradePath=Root+"/Neutral.asset";var saved=AssetDatabase.LoadAssetAtPath<VolumeProfile>(gradePath);
            if(saved){Object.DestroyImmediate(grade);grade=saved;}else{AssetDatabase.CreateAsset(grade,gradePath);foreach(var c in grade.components)AssetDatabase.AddObjectToAsset(c,grade);}
            var finishing=new GameObject("Meadow single Neutral transform").AddComponent<Volume>();finishing.isGlobal=true;finishing.sharedProfile=grade;
            var review=camera.gameObject.AddComponent<LightingMeadowReview>();review.reviewCamera=camera;review.clock=clock;review.atmosphere=atmosphere;review.globals=globals;review.palette=palette;review.hdr=hdr;review.ao=ao;review.grass=grass;review.painter=painter;review.interaction=interaction;review.demonstrator=demonstrator;
            camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            sun.shadowBias=.035f;sun.shadowNormalBias=.13f;sun.shadows=LightShadows.Soft;
            clock.TryInitialize();review.SetHour(12);review.SetView(0);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,Scene);
            string errors=string.Join("\n",new[]{blades.shader,ground.shader,Shader.Find("Little Castle/Foliage/LC Foliage"),Shader.Find("Little Castle/Surface/LC Stylized Lit")}.SelectMany(s=>ShaderUtil.GetShaderMessages(s).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(m=>s.name+": "+m.message)));
            File.WriteAllText(Output+"/shader-errors.txt",errors);if(errors.Length>0)throw new Exception(errors);
            File.WriteAllText(Output+"/setup.txt","Scene="+Scene+"\nFrozen Meadow source assets retained. Owned materials, palettes and profiles only.\nHDR, Neutral only, MSAA4, 2 cascades, 2048 shadow map, 90m shadow distance.\nSSAO Depth reconstruction, downsample, radius .28m, intensity .45, direct strength 0.\nNo SoftScene, auto exposure, decorative blur or spatial GI.\n");
        }
        static UniversalRenderPipelineAsset Pipeline(string name,bool useAO)
        {
            var renderer=Copy<UniversalRendererData>(LightingBenchmarkSetup.Root+(useAO?"/AO_Renderer.asset":"/Baseline_Renderer.asset"),Root+"/"+name+"_Renderer.asset");
            foreach(var feature in renderer.rendererFeatures)
            {
                if(feature.GetType().Name=="ScreenSpaceAmbientOcclusion")
                {var serialized=new SerializedObject(feature);var settings=serialized.FindProperty("m_Settings");settings.FindPropertyRelative("Intensity").floatValue=.45f;settings.FindPropertyRelative("Radius").floatValue=.28f;settings.FindPropertyRelative("DirectLightingStrength").floatValue=0;serialized.ApplyModifiedPropertiesWithoutUndo();feature.SetActive(useAO);EditorUtility.SetDirty(feature);}
            }
            renderer.SetDirty();EditorUtility.SetDirty(renderer);
            var asset=Copy<UniversalRenderPipelineAsset>(LightingBenchmarkSetup.Root+"/HDR.asset",Root+"/"+name+".asset");asset.supportsHDR=true;asset.msaaSampleCount=4;asset.shadowDistance=90;asset.shadowCascadeCount=2;asset.mainLightShadowmapResolution=2048;
            var data=new SerializedObject(asset);var list=data.FindProperty("m_RendererDataList");list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=renderer;data.FindProperty("m_DefaultRendererIndex").intValue=0;data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(asset);return asset;
        }
        static T Copy<T>(string src,string dst)where T:Object{var item=AssetDatabase.LoadAssetAtPath<T>(dst);if(item)return item;if(!AssetDatabase.CopyAsset(src,dst))throw new IOException(src);return AssetDatabase.LoadAssetAtPath<T>(dst);}
        static void Set(Object target,string field,Object value){var s=new SerializedObject(target);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        static void Set(Object target,string field,float value){var s=new SerializedObject(target);s.FindProperty(field).floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        public static void BuildLinear()=>Build(ColorSpace.Linear);
        public static void BuildGamma()=>Build(ColorSpace.Gamma);
        static void Build(ColorSpace space)
        {
            Directory.CreateDirectory(Output);
            string[] paths={"ProjectSettings/ProjectSettings.asset","ProjectSettings/GraphicsSettings.asset","ProjectSettings/QualitySettings.asset"};var bytes=paths.ToDictionary(p=>p,File.ReadAllBytes);
            var previousSpace=PlayerSettings.colorSpace;bool timing=PlayerSettings.enableFrameTimingStats;int quality=QualitySettings.GetQualityLevel();
            var qualityPaths=Enumerable.Range(0,QualitySettings.names.Length).Select(i=>AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(i))).ToArray();string defaultPath=AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);
            int exit=2;
            try
            {
                Setup();PlayerSettings.colorSpace=space;PlayerSettings.enableFrameTimingStats=true;
                var profiles=new[]{"HDR","AO"}.Select(n=>AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/"+n+".asset")).ToArray();GraphicsSettings.defaultRenderPipeline=profiles[1];
                for(int i=0;i<qualityPaths.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=profiles[i%2];}QualitySettings.SetQualityLevel(quality,false);
                string folder=Output+"/Player_"+space;Directory.CreateDirectory(folder);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=folder+"/LittleCastle_LightingMeadow.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                File.WriteAllText(Output+"/build-"+space+".txt",$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nDuration={report.summary.totalTime}\nDevelopment=false; FrameTimingStats=true\n");
                File.WriteAllLines(Output+"/build-"+space+"-warnings.txt",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning||m.type==LogType.Error).Select(m=>m.type+": "+m.content));exit=report.summary.result==BuildResult.Succeeded?0:2;
            }
            catch(Exception e){File.WriteAllText(Output+"/build-error-"+space+".txt",e.ToString());Debug.LogException(e);}
            finally
            {
                PlayerSettings.colorSpace=previousSpace;PlayerSettings.enableFrameTimingStats=timing;AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                for(int i=0;i<qualityPaths.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(qualityPaths[i]);}QualitySettings.SetQualityLevel(quality,false);GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(defaultPath);AssetDatabase.SaveAssets();foreach(var p in paths)File.WriteAllBytes(p,bytes[p]);
            }
            if(Application.isBatchMode)UrpBatchExit.WhenIdle(exit);
        }
    }
}
