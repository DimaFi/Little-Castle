using System.Linq;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    // Explicit scene-builder step, never a live authoring-map or asset-import hook.
    internal static class SnowVillageArtPass
    {
        public static void Apply(Scene scene, SnowVillagePresentation p, string generated)
        {
            var cycle = p.atmosphere.UnifiedProfile;
            cycle.artDirectedMoonOrbit = true;
            cycle.moonElevationRange = new Vector2(-12, 6);
            cycle.moonAzimuthOffset = 21;
            cycle.moonAngularDiameter = 3.2f;
            cycle.moonPhase = .21f;
            cycle.moonPhaseRotation = -25;
            cycle.cloudCoverage = .34f;
            cycle.highCloudCoverage = .12f;
            Save(cycle);
            var night = cycle.snowyMoonlight;
            var look = night.look;
            look.zenith = new Color(.10f, .13f, .34f);
            look.horizon = new Color(.30f, .33f, .57f);
            look.fog = new Color(.31f, .34f, .57f);
            look.ambientSky = new Color(.44f, .48f, .75f);
            look.ambientEquator = new Color(.46f, .47f, .66f);
            look.ambientGround = new Color(.36f, .39f, .60f);
            look.fogStart = 48; look.fogEnd = 175;
            look.moonIntensity = .27f;
            look.starBrightness = 1.7f;
            night.look = look; Save(night);

            var soft = p.sceneCamera.GetComponent<TerrainStarterSoftScene>();
            if (soft && soft.effectMaterial)
            {
                var material = soft.effectMaterial;
                material.SetFloat("_HazeDensity", .006f);
                material.SetFloat("_HazeStart", 34);
                material.SetFloat("_HazeBaseHeight", p.villageOrigin.y + 1);
                material.SetFloat("_HazeHeightFalloff", .17f);
                material.SetFloat("_HazeMaxOpacity", .18f);
                material.SetFloat("_WarmGlow", .16f);
                soft.blurStrength = .38f;
                Save(material);
            }

            var groups = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LODGroup>(true)).ToArray();
            foreach (var group in groups.Where(g => g.name.Contains("TownHall")))
            {
                foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int m = 0; m < materials.Length; m++)
                    {
                        if (!materials[m] || (materials[m].shader.name != "Little Castle/Surface/LC Stylized Lit" &&
                            materials[m].shader.name != "Little Castle/Distance/LC Distant Simple")) continue;
                        string baseName = materials[m].name;
                        while (baseName.StartsWith("WindowLit_")) baseName = baseName.Substring("WindowLit_".Length);
                        string path = generated + "/Generated/WindowLit_" + baseName + ".mat";
                        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!material)
                        {
                            material = new Material(materials[m]) {name = "WindowLit_" + materials[m].name, enableInstancing = true};
                            AssetDatabase.CreateAsset(material, path);
                        }
                        bool windowAtlas = !baseName.StartsWith("TH1_Atlas_0");
                        material.SetTexture("_EmissionMap", windowAtlas ? WindowMask(material.GetTexture("_MainTex") as Texture2D, baseName, generated) : Texture2D.blackTexture);
                        material.SetFloat("_EmissionDayStrength", 0);
                        material.SetFloat("_EmissionNightStrength", windowAtlas ? .72f : 0);
                        material.SetColor("_EmissionColor", new Color(1.65f, .72f, .16f));
                        if (material.HasProperty("_EmissionFlickerStrength")) material.SetFloat("_EmissionFlickerStrength", .015f);
                        if (material.HasProperty("_LocalLightStrength")) material.SetFloat("_LocalLightStrength", .9f);
                        material.SetFloat("_AmbientStrength", 1.12f);
                        Save(material); materials[m] = material;
                    }
                    renderer.sharedMaterials = materials;
                }
                var facadeEmitter = group.GetComponentsInChildren<NightLightEmitter>(true).FirstOrDefault();
                if (facadeEmitter)
                {
                    var emitterSettings = new SerializedObject(facadeEmitter);
                    emitterSettings.FindProperty("baseIntensity").floatValue = 5;
                    emitterSettings.ApplyModifiedPropertiesWithoutUndo();
                }
                // One soft source per facade cluster, rather than one per window.
                var source = group.transform.Find("Budgeted warm facade light");
                if (!source)
                {
                    var go = new GameObject("Budgeted warm facade light");
                    go.transform.SetParent(group.transform, false);
                    var renderers = group.GetLODs()[0].renderers;
                    Bounds b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    Vector3 facing = p.sceneCamera.transform.position - b.center;
                    facing.y = 0; facing.Normalize();
                    float surface = 1 / Mathf.Max(Mathf.Abs(facing.x) / Mathf.Max(.1f, b.extents.x), Mathf.Abs(facing.z) / Mathf.Max(.1f, b.extents.z));
                    go.transform.position = b.center + facing * (surface + .5f);
                    go.transform.position = new Vector3(go.transform.position.x, b.min.y + b.size.y * .34f, go.transform.position.z);
                    AddEmitter(go, b.size.y * .85f, 5, 3);
                }
            }
            foreach (var group in groups.Where(g => g.name == "Warm path lantern"))
            {
                if (group.GetComponentInChildren<NightLightEmitter>(true)) continue;
                var go = new GameObject("Budgeted warm lantern light");
                go.transform.SetParent(group.transform, false);
                var renderer = group.GetLODs()[0].renderers;
                Bounds b = renderer[0].bounds;
                foreach (var r in renderer) b.Encapsulate(r.bounds);
                go.transform.position = b.center;
                AddEmitter(go, 7, 2, 1);
            }
            var manager = p.GetComponent<NightLightBudgetManager>();
            if (!manager) manager = p.gameObject.AddComponent<NightLightBudgetManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("targetCamera").objectReferenceValue = p.sceneCamera;
            serialized.FindProperty("lightingGlobals").objectReferenceValue = p.lightingGlobals;
            serialized.FindProperty("restrictToOwnScene").boolValue = true;
            serialized.FindProperty("maxActiveRealtimeLights").intValue = 6;
            serialized.FindProperty("globalMaxDistance").floatValue = 85;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            foreach (var emitter in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NightLightEmitter>(true)))
                if (emitter.TargetLight) emitter.TargetLight.color = new Color(1, .70f, .25f);

            foreach (var renderer in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if (renderer.name != "Reference garden ground") continue;
                var ground = renderer.sharedMaterial;
                ground.SetFloat("_SnowDetailStrength", .24f);
                ground.SetFloat("_SnowSurfaceFlattening", .85f);
                ground.SetFloat("_MacroStrength", .015f);
                Save(ground);
            }
        }

        static void AddEmitter(GameObject go, float range, float intensity, int priority)
        {
            var light = go.AddComponent<Light>();
            light.type = LightType.Point; light.color = new Color(1, .70f, .25f);
            light.range = Mathf.Clamp(range, 5, 15); light.shadows = LightShadows.None;
            light.intensity = 0; light.enabled = false;
            var emitter = go.AddComponent<NightLightEmitter>();
            var serialized = new SerializedObject(emitter);
            serialized.FindProperty("targetLight").objectReferenceValue = light;
            serialized.FindProperty("baseIntensity").floatValue = intensity;
            serialized.FindProperty("maxDistance").floatValue = 85;
            serialized.FindProperty("priority").intValue = priority;
            serialized.FindProperty("realtimeFlickerStrength").floatValue = .015f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Save(Object asset) {EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);}

        static Texture2D WindowMask(Texture2D atlas, string name, string generated)
        {
            if (!atlas) return Texture2D.blackTexture;
            // All authored LODs of these two releases share the same atlas.
            string atlasName = name.Replace("_Near", "").Replace("_Distant", "");
            string path = generated + "/Generated/Windows_v4_" + atlasName + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing) return existing;
            // Reviewed UV islands in these exact ready-model atlases. This is
            // a scene-local derived emission map; source textures/importers stay
            // untouched. Use an authored mask for future architecture assets.
            Vector4[] regions = name.StartsWith("TH1_Atlas_1") ? new[] {
                new Vector4(.176f,.472f,.258f,.508f), new Vector4(.282f,.472f,.316f,.509f)
            } : new[] {
                new Vector4(.940f,.010f,.973f,.101f), new Vector4(.947f,.128f,.971f,.152f),
                new Vector4(.490f,.643f,.510f,.684f),
                new Vector4(.826f,.439f,.850f,.465f),
                new Vector4(.610f,.694f,.639f,.735f), new Vector4(.228f,.846f,.257f,.884f),
                new Vector4(.055f,.822f,.081f,.850f), new Vector4(.104f,.900f,.124f,.944f),
                new Vector4(.176f,.913f,.208f,.958f)
            };
            var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(2048,2048,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            Texture2D pixels = null;
            try
            {
                Graphics.Blit(atlas,target); RenderTexture.active=target;
                pixels=new Texture2D(2048,2048,TextureFormat.RGBA32,false,true);
                pixels.ReadPixels(new Rect(0,0,2048,2048),0,0); pixels.Apply();
                var data=pixels.GetPixels32();
                for(int y=0;y<2048;y++) for(int x=0;x<2048;x++)
                {
                    float u=(x+.5f)/2048,v=(y+.5f)/2048;
                    bool inside=false;
                    foreach(var region in regions) if(u>=region.x&&u<=region.z&&v>=region.y&&v<=region.w) {inside=true;break;}
                    var c=data[y*2048+x]; float r=c.r/255f,g=c.g/255f,b=c.b/255f;
                    float mask=inside ? Smooth(.02f,.12f,Mathf.Min(r,g*1.65f))*Smooth(.03f,.12f,r-b)*Smooth(.015f,.07f,g-b) : 0;
                    byte value=(byte)Mathf.RoundToInt(mask*255);
                    data[y*2048+x]=new Color32(value,value,value,255);
                }
                pixels.SetPixels32(data); pixels.Apply();
                System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally {RenderTexture.active=active; RenderTexture.ReleaseTemporary(target);if(pixels)Object.DestroyImmediate(pixels);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture=false; importer.mipmapEnabled=true; importer.isReadable=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static float Smooth(float a,float b,float value) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,value));
    }
}
