using System;
using System.IO;
using System.Linq;
using System.Text;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    [InitializeOnLoad]
    public static class UrpWorldVerification
    {
        const string Prefix = "LC_URP_WorldVerification_";
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Little-Castle_Assets/Research/URP_2026-10-24/Gameplay"));
        static bool Active => SessionState.GetBool(Prefix + "active", false);
        static int Phase { get => SessionState.GetInt(Prefix + "phase", 0); set => SessionState.SetInt(Prefix + "phase", value); }
        static double Next { get => SessionState.GetFloat(Prefix + "next", 0); set => SessionState.SetFloat(Prefix + "next", (float)value); }
        static UrpWorldVerification()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }

        public static void Run()
        {
            if (!Path.GetFileName(Path.GetDirectoryName(Application.dataPath)).StartsWith("Little-Castle_URP_"))
                throw new InvalidOperationException("Verification is restricted to the isolated migration project.");
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Prefix + "active", true);
            SessionState.SetString(Prefix + "errors", "");
            try
            {
                var materials = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(p => new { path = p, material = AssetDatabase.LoadAssetAtPath<Material>(p) }).ToArray();
                File.WriteAllLines(Path.Combine(Output, "material_audit.tsv"),
                    new[] { "path\tshader\trenderPipelineTag" }.Concat(materials.Select(x => x.path + "\t" + x.material.shader?.name + "\t" + x.material.GetTag("RenderPipeline", false))));
                EditorSceneManager.OpenScene(TerrainStarterSceneBuilder.StudyPath);
                var camera = Camera.main;
                SetTime(12);
                Capture(camera, "garden_warmup.png");
                SetTime(12);
                var effect = camera.GetComponent<TerrainStarterSoftScene>();
                bool enabled = effect != null && effect.enabled;
                if (effect != null) effect.enabled = false;
                Capture(camera, "garden_noon_effect_off.png");
                if (effect != null) effect.enabled = enabled;
                Capture(camera, "garden_noon.png");
                if (effect != null)
                {
                    float contact = effect.contactOcclusion;
                    effect.contactOcclusion = 0;
                    Capture(camera, "garden_contact_off.png");
                    effect.contactOcclusion = contact;
                }
                AuditActiveMaterials("garden");
                EditorSceneManager.OpenScene("Assets/_Game/Scenes/ConceptProceduralWorld.unity");
                Phase = 1;
                Next = EditorApplication.timeSinceStartup + 45;
                EditorApplication.isPlaying = true;
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void SetTime(float hour)
        {
            var clock = UnityEngine.Object.FindAnyObjectByType<WorldTimeSystem>();
            if (clock != null)
            {
                clock.TryInitialize(); clock.RunAutomatically = false;
                clock.RestoreState(new WorldTimeState(1, hour * 60));
            }
            UnityEngine.Object.FindAnyObjectByType<DayNightLightingController>()?.ApplyLighting();
            UnityEngine.Object.FindAnyObjectByType<StylizedLightingGlobals>()?.ApplyGlobals();
        }
        static void Capture(Camera camera, string name)
        {
            if (camera == null) throw new InvalidOperationException("Scene camera is missing.");
            InnerverseStarterStudyScene.Capture(camera, camera.transform.position,
                camera.transform.position + camera.transform.forward * 10, "../Gameplay/" + name);
        }
        static void AuditActiveMaterials(string name)
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            var used = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            File.WriteAllLines(Path.Combine(Output, name + "_active_materials.tsv"), new[] { "material\tshader\tsupported" }
                .Concat(used.Select(m => m.name + "\t" + m.shader?.name + "\t" + (m.shader != null && m.shader.isSupported))));
            foreach (var material in used)
                if (material.shader == null || !material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader" || material.shader.name == "Standard" || material.shader.name.StartsWith("Legacy Shaders/"))
                    RecordError("Unsupported active material: " + material.name);
        }
        static void Tick()
        {
            if (!Active || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (Phase == 1 && EditorApplication.isPlaying)
                {
                    var streamer = UnityEngine.Object.FindAnyObjectByType<WorldStreamer>();
                    if (streamer == null) throw new InvalidOperationException("World streamer is missing.");
                    if (EditorApplication.timeSinceStartup < Next && (streamer.ActiveChunkCount == 0 || streamer.MissingDesiredChunkCount > 0)) return;
                    if (streamer.ActiveChunkCount == 0) throw new InvalidOperationException("No streamed chunks after 45s.");
                    File.WriteAllText(Path.Combine(Output, "streaming.txt"), "Active=" + streamer.ActiveChunkCount + "\nDesired=" + streamer.DesiredChunkCount +
                        "\nMissing=" + streamer.MissingDesiredChunkCount + "\nPending generation=" + streamer.PendingGenerationCount);
                    SetTime(12); Phase = 2; Next = EditorApplication.timeSinceStartup + 2;
                }
                else if (Phase == 2 && EditorApplication.timeSinceStartup >= Next)
                {
                    Capture(Camera.main, "world_noon.png"); CaptureSky(Camera.main, "sky_noon.png"); AuditActiveMaterials("world");
                    SetTime(18.5f); Phase = 3; Next = EditorApplication.timeSinceStartup + 2;
                }
                else if (Phase == 3 && EditorApplication.timeSinceStartup >= Next)
                {
                    Capture(Camera.main, "world_sunset.png"); CaptureSky(Camera.main, "sky_sunset.png");
                    SetTime(1); Phase = 4; Next = EditorApplication.timeSinceStartup + 2;
                }
                else if (Phase == 4 && EditorApplication.timeSinceStartup >= Next)
                {
                    Capture(Camera.main, "world_night.png"); CaptureSky(Camera.main, "sky_night.png");
                    var shaders = AssetDatabase.FindAssets("t:Shader", new[] { "Assets/_Game/Shaders" }).Select(AssetDatabase.GUIDToAssetPath);
                    var messages = new StringBuilder();
                    foreach (var path in shaders)
                        foreach (var message in ShaderUtil.GetShaderMessages(AssetDatabase.LoadAssetAtPath<Shader>(path)))
                        {
                            messages.AppendLine(path + ": " + message.severity + ": " + message.message);
                            if (message.severity.ToString() == "Error") RecordError(message.message);
                        }
                    File.WriteAllText(Path.Combine(Output, "shader_messages.txt"), messages.ToString());
                    Phase = 5; EditorApplication.isPlaying = false;
                }
                else if (Phase == 5 && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    var errors = SessionState.GetString(Prefix + "errors", "");
                    File.WriteAllText(Path.Combine(Output, "result.txt"), "Unity=" + Application.unityVersion + "\nPipeline=" + GraphicsSettings.currentRenderPipeline +
                        "\nGarden static lighting effect off/on + ConceptProceduralWorld Play Mode + noon/sunset/night and sky captures completed.\nErrors:\n" + errors);
                    Phase = 6; Next = EditorApplication.timeSinceStartup + 5;
                }
                else if (Phase == 6 && EditorApplication.timeSinceStartup >= Next && !ShaderUtil.anythingCompiling)
                {
                    SessionState.SetBool(Prefix + "active", false);
                    if (Application.isBatchMode) EditorApplication.Exit(string.IsNullOrEmpty(SessionState.GetString(Prefix + "errors", "")) ? 0 : 2);
                }
            }
            catch (Exception ex) { Fail(ex); }
        }
        static void CaptureSky(Camera camera, string name)
        {
            var clear = camera.clearFlags;
            try
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                InnerverseStarterStudyScene.Capture(camera, camera.transform.position,
                    camera.transform.position + new Vector3(0, .3f, 1) * 100, "../Gameplay/" + name);
            }
            finally { camera.clearFlags = clear; }
        }
        static void Log(string message, string stack, LogType type)
        {
            if (Active && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) RecordError(message);
        }
        static void RecordError(string message)
        {
            SessionState.SetString(Prefix + "errors", SessionState.GetString(Prefix + "errors", "") + message + "\n");
        }
        static void Fail(Exception ex)
        {
            RecordError(ex.ToString());
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "failed.txt"), ex.ToString());
            SessionState.SetBool(Prefix + "active", false);
            Debug.LogException(ex);
            if (Application.isBatchMode) EditorApplication.Exit(3);
        }
    }
}
