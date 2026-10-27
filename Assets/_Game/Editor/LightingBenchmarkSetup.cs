using System;
using System.IO;
using System.Linq;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Authors only the dedicated lighting study; existing game scenes and quality assets stay owned by MAIN.</summary>
    public static class LightingBenchmarkSetup
    {
        public const string Root = "Assets/_Game/Settings/Rendering/LightingStudy";
        public const string ScenePath = "Assets/_Game/Scenes/LightingBenchmark.unity";
        public static string EvidenceRoot => Path.GetFullPath(Environment.GetEnvironmentVariable("LC_LIGHTING_EVIDENCE") ?? "../Research/Lighting_2026-10-25");

        [MenuItem("Little Castle/Rendering/Lighting Study/Create Reference Scene")]
        public static void Run()
        {
            try
            {
                BuildScene();
                if (Application.isBatchMode) UrpBatchExit.WhenIdle(0);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory(EvidenceRoot);
                File.WriteAllText(Path.Combine(EvidenceRoot, "setup-error.txt"), exception.ToString());
                Debug.LogException(exception);
                if (Application.isBatchMode) UrpBatchExit.WhenIdle(2);
                else throw;
            }
        }

        static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before creating the lighting study.");
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(EvidenceRoot);
            AssetDatabase.Refresh();
            var baselineRenderer = Copy<UniversalRendererData>(UrpQualityProfileSetup.SettingsPath + "/LittleCastle_URP_Renderer.asset", Root + "/Baseline_Renderer.asset");
            var aoRenderer = Copy<UniversalRendererData>(UrpQualityProfileSetup.SettingsPath + "/LittleCastle_URP_Renderer.asset", Root + "/AO_Renderer.asset");
            foreach (var renderer in new[] { baselineRenderer, aoRenderer })
            {
                var data = new SerializedObject(renderer);
                data.FindProperty("postProcessData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderer);
            }
            ConfigureAO(aoRenderer);
            var baseline = MakePipeline("Baseline", baselineRenderer, false);
            var hdr = MakePipeline("HDR", baselineRenderer, true);
            var ao = MakePipeline("AO", aoRenderer, true);
            var scene = EditorSceneManager.OpenScene(TerrainStarterSceneBuilder.StudyPath, OpenSceneMode.Single);
            var camera = Camera.main;
            if (!camera) throw new InvalidOperationException("The existing CottageGarden study camera is missing.");
            var oldControls = camera.GetComponent<TerrainStarterPreviewControls>();
            if (oldControls) Object.DestroyImmediate(oldControls);
            var strategy = camera.GetComponent<LittleCastle.CameraSystem.StrategyCameraController>();
            if (strategy) Object.DestroyImmediate(strategy);

            var globals = Object.FindAnyObjectByType<StylizedLightingGlobals>();
            var foliage = Object.FindObjectsByType<MeshRenderer>().Where(r =>
                r.GetComponent<MeshFilter>() && r.sharedMaterials.Any(m => m && m.shader.name == "Little Castle/Foliage/LC Foliage"))
                .OrderByDescending(r => r.GetComponent<MeshFilter>().sharedMesh.bounds.size.y).FirstOrDefault();
            if (!foliage) throw new InvalidOperationException("The real study foliage must be present before modifying vegetation lighting.");
            var previousSelection = Selection.activeObject;
            try { Selection.activeGameObject = foliage.gameObject; FoliageModelCompatibilityValidator.ValidateSelected(); }
            finally { Selection.activeObject = previousSelection; }
            var foliageMesh = foliage.GetComponent<MeshFilter>().sharedMesh;
            File.WriteAllText(Path.Combine(EvidenceRoot, "foliage-preflight.txt"),
                "Object=" + foliage.name + "\nMesh=" + AssetDatabase.GetAssetPath(foliageMesh) +
                "\nVertices=" + foliageMesh.vertexCount + "\nBounds=" + foliageMesh.bounds +
                "\nNormals=" + foliageMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal) +
                "\nTangents=" + foliageMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent) +
                "\nUV0=" + foliageMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0) +
                "\nColors=" + foliageMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color) +
                "\nShaders=" + string.Join(";", foliage.sharedMaterials.Select(m => m.shader.name)) +
                "\nTechnical validator results are in setup.log; close/far animated review is separate.\n");
            var clock = new GameObject("Lighting study - fixed authoritative clock").AddComponent<WorldTimeSystem>();
            Set(clock, "timeSettingsOverride", AssetDatabase.LoadAssetAtPath<WorldTimeSettings>("Assets/_Game/Settings/World/MainWorldTimeSettings.asset"));
            Set(clock, "runAutomatically", false);
            var sun = RenderSettings.sun;
            var moon = new GameObject("Lighting study moon").AddComponent<Light>();
            moon.type = LightType.Directional; moon.shadows = LightShadows.None;
            var atmosphere = new GameObject("Lighting study atmosphere").AddComponent<DayNightLightingController>();
            Set(atmosphere, "timeSystem", clock); Set(atmosphere, "sun", sun); Set(atmosphere, "moon", moon);
            Set(atmosphere, "skyboxMaterial", Copy<Material>("Assets/_Game/Materials/StylizedDayNightSky.mat", Root + "/StudySky.mat"));
            Set(atmosphere, "maxSunIntensity", .9f);
            Set(atmosphere, "dayAmbientSky", new Color(.50f, .57f, .68f));
            Set(atmosphere, "dayAmbientEquator", new Color(.42f, .46f, .53f));
            Set(atmosphere, "dayAmbientGround", new Color(.25f, .27f, .19f));
            Set(atmosphere, "dayFogStart", 85f); Set(atmosphere, "dayFogEnd", 140f);
            Set(atmosphere, "nightFogStart", 65f); Set(atmosphere, "nightFogEnd", 125f);
            Set(globals, "timeSystem", clock); Set(globals, "atmosphere", atmosphere);
            Set(globals, "sun", sun); Set(globals, "moon", moon);
            camera.clearFlags = CameraClearFlags.Skybox;

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/NeutralFinishing.asset");
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, Root + "/NeutralFinishing.asset"); }
            if (!profile.TryGet<Tonemapping>(out var tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
                AssetDatabase.AddObjectToAsset(tonemapping, profile);
            }
            tonemapping.mode.Override(TonemappingMode.Neutral);
            var volume = new GameObject("Lighting study - Neutral tone mapping only").AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = profile;

            BuildReferenceGeometry();
            var review = camera.gameObject.AddComponent<LightingBenchmarkController>();
            review.reviewCamera = camera; review.clock = clock; review.atmosphere = atmosphere;
            review.lightingGlobals = globals; review.softScene = camera.GetComponent<TerrainStarterSoftScene>();
            review.finishingVolume = volume; review.baselinePipeline = baseline; review.hdrPipeline = hdr; review.aoPipeline = ao;
            var palette = AssetDatabase.LoadAssetAtPath<LightingPaletteProfile>(Root + "/LinearPalette.asset");
            if (!palette)
            {
                palette = ScriptableObject.CreateInstance<LightingPaletteProfile>();
                AssetDatabase.CreateAsset(palette, Root + "/LinearPalette.asset");
            }
            review.linearPalette = palette;
            review.softScene.enabled = false;
            review.softScene.contactOcclusion = 0;
            clock.TryInitialize(); review.SetHour(12); review.SetView(0);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            File.WriteAllText(Path.Combine(EvidenceRoot, "setup.txt"),
                "Scene=" + ScenePath + "\nUnity=" + Application.unityVersion + "\nColorSpace=" + PlayerSettings.colorSpace +
                "\nModes=Baseline (LDR, no AO), HDR (Neutral, no AO), AO (HDR Neutral + independent SSAO)" +
                "\nThe existing CottageGarden scene and its source assets are reused, never regenerated.\n");
            Debug.Log("LC_LIGHTING_STUDY scene=" + ScenePath);
        }

        static UniversalRenderPipelineAsset MakePipeline(string name, UniversalRendererData renderer, bool hdr)
        {
            var asset = Copy<UniversalRenderPipelineAsset>(UrpQualityProfileSetup.SettingsPath + "/LittleCastle_URP_Medium.asset", Root + "/" + name + ".asset");
            asset.supportsHDR = hdr;
            var serialized = new SerializedObject(asset);
            var renderers = serialized.FindProperty("m_RendererDataList"); renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static T Copy<T>(string source, string destination) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(destination);
            if (asset) return asset;
            if (!AssetDatabase.CopyAsset(source, destination)) throw new IOException("Could not copy " + source);
            return AssetDatabase.LoadAssetAtPath<T>(destination) ?? throw new IOException(destination);
        }

        static void ConfigureAO(UniversalRendererData renderer)
        {
            var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion", true);
            var feature = renderer.rendererFeatures.FirstOrDefault(f => f && f.GetType() == type);
            if (!feature)
            {
                // Reuse the inactive orphan if the adopted renderer carried one.
                feature = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(renderer)).OfType<ScriptableRendererFeature>().FirstOrDefault(f => f.GetType() == type);
                if (!feature)
                {
                    feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                    feature.name = "LightingStudy SSAO";
                    AssetDatabase.AddObjectToAsset(feature, renderer);
                }
                renderer.rendererFeatures.Insert(0, feature);
            }
            var data = new SerializedObject(feature);
            var settings = data.FindProperty("m_Settings");
            settings.FindPropertyRelative("Source").enumValueIndex = 0; // Depth reconstructed normals, no new geometry prepass.
            settings.FindPropertyRelative("NormalSamples").enumValueIndex = 2;
            settings.FindPropertyRelative("AfterOpaque").boolValue = false;
            settings.FindPropertyRelative("Downsample").boolValue = true;
            settings.FindPropertyRelative("Intensity").floatValue = .65f;
            settings.FindPropertyRelative("Radius").floatValue = .45f;
            settings.FindPropertyRelative("DirectLightingStrength").floatValue = 0;
            settings.FindPropertyRelative("Samples").enumValueIndex = 1;
            settings.FindPropertyRelative("BlurQuality").enumValueIndex = 0;
            settings.FindPropertyRelative("Falloff").floatValue = 75;
            data.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            renderer.SetDirty();
            EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer);
        }

        static void BuildReferenceGeometry()
        {
            var root = new GameObject("Lighting reference - occluded courtyard and material response").transform;
            var stone = Material("ReferenceStone", new Color(.64f, .60f, .50f));
            var green = Material("ReferenceGround", new Color(.22f, .39f, .12f));
            var white = Material("ReferenceWhite", new Color(.8f, .8f, .8f));
            var red = Material("ReferenceWarm", new Color(.52f, .13f, .05f));
            Cube("Courtyard floor", new Vector3(24, .15f, -8), new Vector3(15, .3f, 13), green, root);
            Cube("Back wall", new Vector3(24, 2.6f, -2), new Vector3(15, 5, .45f), stone, root);
            Cube("Side wall", new Vector3(17, 2.6f, -8), new Vector3(.45f, 5, 12), stone, root);
            Cube("Roof covering half the courtyard", new Vector3(21, 5.2f, -7), new Vector3(8, .45f, 10), red, root);
            foreach (float z in new[] { -11f, -3f }) Cube("Roof support", new Vector3(24.5f, 2.6f, z), new Vector3(.45f, 5, .45f), stone, root);
            foreach (float x in new[] { 21f, 27f })
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Neutral sphere - " + (x < 24 ? "covered" : "open sky");
                sphere.transform.SetParent(root); sphere.transform.position = new Vector3(x, 1.2f, -8);
                sphere.transform.localScale = Vector3.one * 1.8f;
                sphere.GetComponent<MeshRenderer>().sharedMaterial = white;
            }
            var lamp = new GameObject("Compact warm reference light").AddComponent<Light>();
            lamp.transform.SetParent(root); lamp.transform.position = new Vector3(22, 3.5f, -6);
            lamp.type = LightType.Point; lamp.color = new Color(1, .52f, .17f); lamp.intensity = 2.6f;
            lamp.range = 7; lamp.shadows = LightShadows.None;
            Cube("Visible reference lamp", lamp.transform.position, Vector3.one * .22f,
                GlowMaterial(), root);
            // Existing audited production topology; this fixture does not alter the spawn catalog.
            const string study = "Assets/_Game/Art/Studies/InnerverseStarter_20261023/";
            var spruceMesh = AssetDatabase.LoadAllAssetsAtPath("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Trees/Fir/Models/UNS_Spruce_02.fbx")
                .OfType<Mesh>().Single(m => m.name == "Spruce 2_LOD0");
            var spruce = new GameObject("Lighting reference - audited UNS Spruce 02", typeof(MeshFilter), typeof(MeshRenderer));
            spruce.transform.SetParent(root); spruce.transform.position = new Vector3(30, .3f, -6);
            spruce.GetComponent<MeshFilter>().sharedMesh = spruceMesh;
            spruce.GetComponent<MeshRenderer>().sharedMaterials = new[] {
                AssetDatabase.LoadAssetAtPath<Material>(study + "Study_UNS_Spruce_Tree_Branch.mat"),
                AssetDatabase.LoadAssetAtPath<Material>(study + "Study_UNS_Color_Palette.mat") };
            var bridgeAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Models/Concept/Bridge_Stone_A/Bridge_Stone_A_v002.prefab");
            if (!bridgeAsset) throw new InvalidOperationException("Existing bridge is missing.");
            var bridge = (GameObject)PrefabUtility.InstantiatePrefab(bridgeAsset);
            bridge.transform.SetParent(root); bridge.transform.position = new Vector3(24, .8f, 15);
            var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Models/Concept/BridgeValidation/Water.mat");
            Cube("Existing bridge water material", new Vector3(24, .1f, 15), new Vector3(22, .1f, 6), water, root);
        }

        static Material Material(string name, Color color)
        {
            var path = Root + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Little Castle/Surface/LC Stylized Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_Color", color); material.SetFloat("_LightResponse", .8f);
            material.SetFloat("_RimStrength", 0); material.SetFloat("_SpecularStrength", .04f);
            material.enableInstancing = true; EditorUtility.SetDirty(material);
            return material;
        }

        static Material GlowMaterial()
        {
            var material = Material("ReferenceLamp", new Color(.7f, .4f, .12f));
            material.SetColor("_EmissionColor", new Color(3, 1.4f, .3f));
            material.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            material.SetFloat("_EmissionDayStrength", .4f); material.SetFloat("_EmissionNightStrength", 2);
            return material;
        }

        static void Cube(string name, Vector3 position, Vector3 size, Material material, Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name; cube.transform.SetParent(parent); cube.transform.position = position;
            cube.transform.localScale = size; cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void Set(Object target, string field, Object value) { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        static void Set(Object target, string field, float value) { var data = new SerializedObject(target); data.FindProperty(field).floatValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        static void Set(Object target, string field, bool value) { var data = new SerializedObject(target); data.FindProperty(field).boolValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        static void Set(Object target, string field, Color value) { var data = new SerializedObject(target); data.FindProperty(field).colorValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }

        public static void BuildGamma() => Build(ColorSpace.Gamma);
        public static void BuildLinear() => Build(ColorSpace.Linear);

        public static void InspectLastBuild()
        {
            var report = BuildReport.GetLatestReport();
            Directory.CreateDirectory(EvidenceRoot);
            File.WriteAllLines(Path.Combine(EvidenceRoot, "last-build-warnings.txt"), report.steps
                .SelectMany(step => step.messages).Where(message => message.type == LogType.Warning || message.type == LogType.Error)
                .Select(message => message.type + ": " + message.content));
            if (Application.isBatchMode) UrpBatchExit.WhenIdle(0);
        }

        static void Build(ColorSpace colorSpace)
        {
            var previousSpace = PlayerSettings.colorSpace;
            bool previousTiming = PlayerSettings.enableFrameTimingStats;
            // Build/reimport can invalidate loaded Unity object references. Persist
            // paths and exact settings bytes, then reload after restoring colour space.
            string previousDefaultPath = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);
            int previousQuality = QualitySettings.GetQualityLevel();
            var previousPipelinePaths = Enumerable.Range(0, QualitySettings.names.Length)
                .Select(level => AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(level))).ToArray();
            string[] settingsPaths = { "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset", "ProjectSettings/ProjectSettings.asset" };
            var settingsBytes = settingsPaths.ToDictionary(path => path, File.ReadAllBytes);
            try
            {
                PlayerSettings.colorSpace = colorSpace;
                PlayerSettings.enableFrameTimingStats = true;
                // URP strips by the graphics/quality build configuration, not arbitrary
                // MonoBehaviour references. Include every study mode for this build only.
                var studyPipelines = new[] { "Baseline", "HDR", "AO" }.Select(name =>
                    AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root + "/" + name + ".asset")).ToArray();
                GraphicsSettings.defaultRenderPipeline = studyPipelines[2];
                for (int level = 0; level < previousPipelinePaths.Length; level++)
                {
                    QualitySettings.SetQualityLevel(level, false);
                    QualitySettings.renderPipeline = studyPipelines[level % studyPipelines.Length];
                }
                QualitySettings.SetQualityLevel(previousQuality, false);
                string folder = Path.Combine(EvidenceRoot, "Player_" + colorSpace);
                Directory.CreateDirectory(folder);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, target = BuildTarget.StandaloneWindows64,
                    locationPathName = Path.Combine(folder, "LittleCastle_Lighting_" + colorSpace + ".exe"),
                    options = BuildOptions.Development
                });
                File.WriteAllText(Path.Combine(EvidenceRoot, "build-" + colorSpace + ".txt"),
                    "Result=" + report.summary.result + "\nErrors=" + report.summary.totalErrors +
                    "\nWarnings=" + report.summary.totalWarnings + "\nDuration=" + report.summary.totalTime);
                File.WriteAllLines(Path.Combine(EvidenceRoot, "build-" + colorSpace + "-warnings.txt"), report.steps
                    .SelectMany(step => step.messages).Where(message => message.type == LogType.Warning || message.type == LogType.Error)
                    .Select(message => message.type + ": " + message.content));
                if (Application.isBatchMode) UrpBatchExit.WhenIdle(report.summary.result == BuildResult.Succeeded ? 0 : 2);
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(EvidenceRoot, "build-" + colorSpace + "-error.txt"), exception.ToString());
                Debug.LogException(exception);
                if (Application.isBatchMode) UrpBatchExit.WhenIdle(2);
            }
            finally
            {
                PlayerSettings.colorSpace = previousSpace;
                PlayerSettings.enableFrameTimingStats = previousTiming;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                for (int level = 0; level < previousPipelinePaths.Length; level++)
                {
                    QualitySettings.SetQualityLevel(level, false);
                    QualitySettings.renderPipeline = LoadPipeline(previousPipelinePaths[level]);
                }
                QualitySettings.SetQualityLevel(previousQuality, false);
                GraphicsSettings.defaultRenderPipeline = LoadPipeline(previousDefaultPath);
                AssetDatabase.SaveAssets();
                // Shader prefiltering/build code can rewrite project settings. Keep
                // the dedicated build fully reversible even across a full reimport.
                foreach (var pair in settingsBytes) File.WriteAllBytes(pair.Key, pair.Value);
            }
        }

        static RenderPipelineAsset LoadPipeline(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path)
                ?? throw new InvalidOperationException("Could not restore pipeline after build: " + path);
        }
    }
}
