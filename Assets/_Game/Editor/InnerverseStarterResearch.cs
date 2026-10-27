using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    // Isolated research tooling. Does not edit vendor assets, world presets or project settings.
    [InitializeOnLoad]
    public static class InnerverseStarterResearch
    {
        public static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../Little-Castle_Assets/Research/URP_2026-10-24/UnityStarter"));
        private static string Root => AssetDatabase.GetAllAssetPaths().FirstOrDefault(p =>
            p.StartsWith("Assets/InnerverseInteractive/") && p.EndsWith("/Documentation/UNS_Readme.txt"))
            ?.Replace("/Documentation/UNS_Readme.txt", "");
        private static double nextCheck;

        static InnerverseStarterResearch() { EditorApplication.update += CheckRequest; }
        private static void CheckRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 2;
            string request = Path.Combine(Output, "audit.request");
            if (!File.Exists(request)) return;
            File.Move(request, Path.Combine(Output, "audit.started." + DateTime.UtcNow.Ticks));
            Run();
        }

        [Serializable] public class AuditReport
        {
            public string timestampUtc, unityVersion, pipeline, packageRoot;
            public List<MeshInfo> meshes = new List<MeshInfo>();
            public List<PrefabInfo> prefabs = new List<PrefabInfo>();
            public List<MaterialInfo> materials = new List<MaterialInfo>();
            public List<string> errors = new List<string>();
            public List<string> validatorLog = new List<string>();
        }
        [Serializable] public class MeshInfo
        {
            public string asset, name, readError;
            public int vertices, triangles, subMeshes;
            public Vector3 boundsSize, boundsCenter;
            public bool normals, tangents, uv0, colors;
            public float uvYHeightCorrelation, bottomUvYMin, bottomUvYMax, topUvYMin, topUvYMax;
        }
        [Serializable] public class PrefabInfo
        {
            public string asset, name, preview;
            public List<int> lodTriangles = new List<int>();
            public int meshRenderers, colliders, triggerColliders;
        }
        [Serializable] public class MaterialInfo
        {
            public string asset, name, shader, baseMap;
            public Color color;
            public float cutoff;
            public bool instancing;
        }

        [MenuItem("Little Castle/Research/Innerverse Starter/Run Mesh Audit and Render Catalog")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory(Path.Combine(Output, "Previews"));
            var report = new AuditReport { timestampUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion, pipeline = GraphicsSettings.currentRenderPipeline == null
                ? "Built-in" : GraphicsSettings.currentRenderPipeline.GetType().FullName, packageRoot = Root };
            UnityEngine.Object oldSelection = Selection.activeObject;
            var transientMaterials = new List<Material>();
            string[] globals = { "_LC_SunDirection", "_LC_SunColor", "_LC_MoonDirection", "_LC_MoonColor",
                "_LC_AmbientColor", "_LC_AmbientSkyColor", "_LC_AmbientEquatorColor", "_LC_AmbientGroundColor",
                "_LC_ShadowTint", "_LC_Wind", "_LC_GameTime" };
            var previous = globals.Select(Shader.GetGlobalVector).ToArray();
            try
            {
                if (Root == null) throw new InvalidOperationException("Imported Starter package not found.");
                foreach (string path in AssetDatabase.FindAssets("t:Model", new[] { Root })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
                    foreach (Mesh mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                        report.meshes.Add(InspectMesh(path, mesh));
                foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { Root })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                    report.materials.Add(new MaterialInfo { asset = path, name = m.name, shader = m.shader?.name,
                        baseMap = tex == null ? "" : AssetDatabase.GetAssetPath(tex),
                        color = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color,
                        cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0, instancing = m.enableInstancing });
                }
                SetPreviewGlobals();
                foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { Root })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var item = new PrefabInfo { asset = path, name = prefab.name,
                        meshRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true).Length,
                        colliders = prefab.GetComponentsInChildren<Collider>(true).Length,
                        triggerColliders = prefab.GetComponentsInChildren<Collider>(true).Count(c => c.isTrigger) };
                    var group = prefab.GetComponentInChildren<LODGroup>(true);
                    if (group != null) foreach (var lod in group.GetLODs())
                        item.lodTriangles.Add(lod.renderers.Where(r => r != null).Sum(r => Triangles(r.GetComponent<MeshFilter>()?.sharedMesh)));
                    else item.lodTriangles.Add(prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f => Triangles(f.sharedMesh)));
                    report.prefabs.Add(item);
                    GameObject clone = null;
                    try
                    {
                        clone = UnityEngine.Object.Instantiate(prefab);
                        clone.hideFlags = HideFlags.HideAndDontSave;
                        if (path.Contains("/Grass/") || path.Contains("/Trees/"))
                            Validate(clone, report.validatorLog, "Original " + item.name);
                        foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
                            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => AdaptMaterial(m, path, transientMaterials)).ToArray();
                        if (path.Contains("/Grass/") || path.Contains("/Trees/"))
                            Validate(clone, report.validatorLog, "Adapted " + item.name);
                        OnlyFirstLod(clone);
                        item.preview = Path.Combine(Output, "Previews", item.name + ".png");
                        RenderObject(clone, item.preview);
                        clone = null; // PreviewRenderUtility owns and cleans its object.
                    }
                    catch (Exception e) { report.errors.Add(path + ": " + e); }
                    finally { if (clone != null) UnityEngine.Object.DestroyImmediate(clone); }
                }
            }
            catch (Exception e) { report.errors.Add(e.ToString()); }
            finally
            {
                for (int i = 0; i < globals.Length; i++) Shader.SetGlobalVector(globals[i], previous[i]);
                foreach (var m in transientMaterials) if (m != null) UnityEngine.Object.DestroyImmediate(m);
                Selection.activeObject = oldSelection;
                File.WriteAllText(Path.Combine(Output, "unity_mesh_audit.json"), JsonUtility.ToJson(report, true));
                File.WriteAllLines(Path.Combine(Output, "foliage_validator.txt"), report.validatorLog);
                File.WriteAllText(Path.Combine(Output, "audit.done"), DateTime.UtcNow.ToString("O"));
                Debug.Log("[Innerverse Research] Audit complete: " + report.meshes.Count + " meshes, " + report.prefabs.Count +
                    " prefabs, errors=" + report.errors.Count + ". " + Output);
            }
        }

        private static int Triangles(Mesh m)
        {
            if (m == null) return 0;
            long count = 0;
            for (int i = 0; i < m.subMeshCount; i++) if (m.GetTopology(i) == MeshTopology.Triangles) count += (long)m.GetIndexCount(i) / 3;
            return (int)count;
        }
        private static MeshInfo InspectMesh(string path, Mesh m)
        {
            var info = new MeshInfo { asset = path, name = m.name, vertices = m.vertexCount,
                triangles = Triangles(m), subMeshes = m.subMeshCount, boundsSize = m.bounds.size, boundsCenter = m.bounds.center,
                normals = m.HasVertexAttribute(VertexAttribute.Normal), tangents = m.HasVertexAttribute(VertexAttribute.Tangent),
                uv0 = m.HasVertexAttribute(VertexAttribute.TexCoord0), colors = m.HasVertexAttribute(VertexAttribute.Color) };
            try
            {
                var v = m.vertices; var uv = m.uv;
                if (uv.Length != v.Length || v.Length == 0) return info;
                double sy = 0, su = 0, syy = 0, suu = 0, syu = 0;
                info.bottomUvYMin = info.topUvYMin = float.PositiveInfinity;
                info.bottomUvYMax = info.topUvYMax = float.NegativeInfinity;
                foreach (var p in v) sy += p.y;
                for (int i = 0; i < v.Length; i++)
                {
                    double y = v[i].y, u = uv[i].y; su += u; syy += y*y; suu += u*u; syu += y*u;
                    float height = Mathf.InverseLerp(m.bounds.min.y, m.bounds.max.y, v[i].y);
                    if (height <= .15f) { info.bottomUvYMin = Mathf.Min(info.bottomUvYMin, uv[i].y); info.bottomUvYMax = Mathf.Max(info.bottomUvYMax, uv[i].y); }
                    if (height >= .85f) { info.topUvYMin = Mathf.Min(info.topUvYMin, uv[i].y); info.topUvYMax = Mathf.Max(info.topUvYMax, uv[i].y); }
                }
                double den = Math.Sqrt((v.Length*syy-sy*sy)*(v.Length*suu-su*su));
                info.uvYHeightCorrelation = den < 1e-12 ? 0 : (float)((v.Length*syu-sy*su)/den);
            }
            catch (Exception e) { info.readError = e.Message; }
            return info;
        }
        private static void Validate(GameObject clone, List<string> log, string label)
        {
            log.Add("--- " + label + " ---");
            void Capture(string condition, string stack, LogType type) { if (condition.Contains("[Little Castle Foliage]")) log.Add(type + ": " + condition); }
            Application.logMessageReceived += Capture;
            try { Selection.activeGameObject = clone; FoliageModelCompatibilityValidator.ValidateSelected(); }
            finally { Application.logMessageReceived -= Capture; }
        }
        public static void OnlyFirstLod(GameObject root)
        {
            foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                foreach (var lod in lods) foreach (var r in lod.renderers) if (r != null) r.enabled = false;
                if (lods.Length > 0) foreach (var r in lods[0].renderers) if (r != null) r.enabled = true;
                group.enabled = false;
            }
        }
        public static Material AdaptMaterial(Material source, string assetPath, List<Material> created)
        {
            if (source == null) return null;
            bool grass = assetPath.Contains("/Grass/");
            string key = source.name.ToLowerInvariant();
            bool foliage = key.Contains("leav") || key.Contains("branch") && assetPath.Contains("/Trees/") || key.Contains("flower") || grass;
            string shaderName = grass ? "Little Castle/Foliage/LC Grass" : foliage
                ? "Little Castle/Foliage/LC Foliage" : "Little Castle/Surface/LC Stylized Lit";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader " + shaderName);
            var m = new Material(shader) { name = "Study_" + source.name, enableInstancing = true };
            created.Add(m);
            Texture tex = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture;
            Color tint = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
            m.SetTexture("_MainTex", tex);
            m.SetColor("_Color", tint);
            if (grass) { m.SetColor("_RootColor", tint * new Color(.66f,.74f,.56f,1)); m.SetColor("_TipColor", tint * new Color(1.12f,1.08f,.92f,1)); }
            if (foliage && source.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", source.GetFloat("_Cutoff"));
            // No wind-readiness claim: actual mesh validation is recorded separately.
            return m;
        }
        public static void SetPreviewGlobals()
        {
            Shader.SetGlobalVector("_LC_SunDirection", new Vector4(.4f,.8f,-.3f,0));
            Shader.SetGlobalVector("_LC_SunColor", new Vector4(1.05f,1.01f,.9f,1));
            Shader.SetGlobalVector("_LC_MoonColor", Vector4.zero);
            Shader.SetGlobalVector("_LC_AmbientColor", new Vector4(.7f,.75f,.83f,1));
            Shader.SetGlobalVector("_LC_AmbientSkyColor", new Vector4(.72f,.79f,.9f,1));
            Shader.SetGlobalVector("_LC_AmbientEquatorColor", new Vector4(.63f,.68f,.7f,1));
            Shader.SetGlobalVector("_LC_AmbientGroundColor", new Vector4(.42f,.45f,.36f,1));
            Shader.SetGlobalVector("_LC_ShadowTint", new Vector4(.77f,.83f,.95f,1));
            Shader.SetGlobalVector("_LC_Wind", new Vector4(.94f,.34f,.25f,1));
            Shader.SetGlobalVector("_LC_GameTime", Vector4.zero);
        }
        private static void RenderObject(GameObject obj, string file)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(obj);
                var renderers = obj.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("No visible renderers");
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                var camera = preview.camera;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.18f,.21f,.24f,1);
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(.15f, bounds.extents.magnitude * .88f);
                camera.nearClipPlane = .01f; camera.farClipPlane = 10000;
                camera.transform.position = bounds.center + new Vector3(1,.7f,-1).normalized * Mathf.Max(10,bounds.size.magnitude*2);
                camera.transform.LookAt(bounds.center);
                preview.lights[0].intensity = 1.1f;
                preview.lights[0].transform.rotation = Quaternion.Euler(45,-35,0);
                preview.lights[1].intensity = .35f;
                preview.ambientColor = new Color(.58f,.62f,.68f,1);
                preview.BeginStaticPreview(new Rect(0,0,512,512));
                preview.Render(true);
                var texture = preview.EndStaticPreview();
                File.WriteAllBytes(file, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally { preview.Cleanup(); }
        }
    }
}
