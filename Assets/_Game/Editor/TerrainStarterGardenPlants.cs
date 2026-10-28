using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>Small authored garden fixture; never participates in match generation.</summary>
    internal static class TerrainStarterGardenPlants
    {
        private static readonly string[] Keys = { "GardenShrub_A", "GardenDaisies_A", "GardenLupins_A" };

        internal static void BuildAndPlace(string root, TerrainStarterPlan plan)
        {
            var near = Material(root, false, false);
            var far = Material(root, true, false);
            var shrubNear = Material(root, false, true);
            var shrubFar = Material(root, true, true);
            var prefabs = new Dictionary<string, GameObject>();
            foreach (string key in Keys)
            {
                var obj = new GameObject(key);
                var levels = new LOD[3];
                for (int i = 0; i < 3; i++)
                {
                    string path = root + "/Models/" + key + "_LOD" + i + ".fbx";
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!asset) throw new FileNotFoundException(path);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    model.transform.SetParent(obj.transform, false);
                    var renderers = model.GetComponentsInChildren<Renderer>();
                    foreach (var renderer in renderers)
                    {
                        renderer.sharedMaterial = key == Keys[0] ? (i == 2 ? shrubFar : shrubNear) : (i == 2 ? far : near);
                        if(key == Keys[0]) {
                            // FBX may retain an axis-conversion transform: use mesh-local bounds.
                            Vector3 centre=renderer.GetComponent<MeshFilter>().sharedMesh.bounds.center;
                            renderer.sharedMaterial.SetVector("_CanopyCenter",new Vector4(centre.x,centre.y,centre.z,0));
                        }
                        renderer.lightProbeUsage = LightProbeUsage.Off;
                        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        renderer.shadowCastingMode = i == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        renderer.receiveShadows = i < 2;
                    }
                    levels[i] = new LOD(i == 0 ? .075f : i == 1 ? .025f : .008f, renderers);
                }
                var group = obj.AddComponent<LODGroup>();
                group.SetLODs(levels);
                group.RecalculateBounds();
                // No colliders, scripts, lights, or per-leaf objects on decorative plants.
                prefabs[key] = PrefabUtility.SaveAsPrefabAsset(obj, root + "/Prefabs/" + key + ".prefab");
                UnityEngine.Object.DestroyImmediate(obj);
                Selection.activeObject = prefabs[key];
                FoliageModelCompatibilityValidator.ValidateSelected();
                ProductionAssetValidator.ValidateSelected();
            }
            Selection.activeObject = null;
            var parent = new GameObject("Garden planting • foundation and wall transitions");
            // x/z anchors are intentionally authored for this one cottage fixture.
            // Open doorway, bench, well access and the centre of paths stay clear.
            var anchors = new[] {
                new Vector2(-12.6f,-7.5f), new Vector2(-11.5f,-7.6f),
                new Vector2(-5.1f,-7.2f), new Vector2(-4.8f,-5.8f),
                new Vector2(-13.2f,-5.4f), new Vector2(-13.3f,-3.3f),
                new Vector2(-16.0f,-12.5f), new Vector2(-13.3f,-12.4f),
                new Vector2(-6.2f,-12.45f), new Vector2(-3.4f,-12.4f),
                new Vector2(-18.1f,-9.1f), new Vector2(-18.15f,-6.4f),
                new Vector2(-18.0f,-3.8f), new Vector2(-1.0f,-6.0f)
            };
            int count = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                Vector2 p = anchors[i];
                Plant(prefabs[Keys[0]], p, .90f + i%4*.10f, i*137.5f, plan, parent.transform, ref count);
                Plant(prefabs[Keys[2]], p + new Vector2(i%2 == 0 ? -.42f : .42f, .15f),
                    .85f + i%3*.09f, i*83, plan, parent.transform, ref count);
                Plant(prefabs[Keys[1]], p + new Vector2(-.24f, -.45f),
                    .80f + i%3*.12f, i*61, plan, parent.transform, ref count);
                if (i%3 != 1)
                    Plant(prefabs[Keys[1]], p + new Vector2(.39f, -.30f),
                        .69f + i%2*.14f, i*41, plan, parent.transform, ref count);
            }
            // A few low clumps extend planting into the meadow; no uniform carpet.
            foreach (var p in new[] { new Vector2(-20,-10), new Vector2(-16,-15),
                new Vector2(-13,-17), new Vector2(-5,-16), new Vector2(1,-10) })
                Plant(prefabs[Keys[1]], p, .85f, count*137.5f, plan, parent.transform, ref count);
            // The cottage is one instance of the building-edge rule. An
            // additional house at any position/yaw gets the same perimeter
            // planting without adding authored scene coordinates.
            foreach (var stamp in plan.BuildingStamps)
            {
                var random=new System.Random(plan.Seed ^ unchecked((int)stamp.stableId*486187739));
                for(int i=0;i<14;i++)
                {
                    bool shrub=i%4==0;
                    float u=(i+.18f+(float)random.NextDouble()*.45f)/14;
                    float offset=shrub ? 1.05f+(float)random.NextDouble()*.4f :
                        .68f+(float)random.NextDouble()*.55f;
                    Vector2 p=stamp.PerimeterPoint(u,offset);
                    string key=shrub ? Keys[0] : i%3==0 ? Keys[2] : Keys[1];
                    Plant(prefabs[key],p,.72f+(float)random.NextDouble()*.27f,
                        (float)random.NextDouble()*360,plan,parent.transform,ref count);
                }
            }
            Debug.Log("[Garden Plants] Placed " + count + " clusters; shared Oak atlas/flower palette, authored LODs, no colliders.");
        }

        private static void Plant(GameObject prefab, Vector2 p, float scale, float yaw,
            TerrainStarterPlan plan, Transform parent, ref int count)
        {
            // Test the full flower radius against the path, not just the stem centre.
            float radius = (prefab.name == Keys[0] ? .82f : .57f)*scale;
            for (int i=0; i<9; i++)
            {
                float a=i*Mathf.PI*.25f;
                Vector2 q=i==8 ? p : p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                if (plan.PathMask(q.x,q.y)>.18f || plan.BuildingDistance(q.x,q.y)<.08f ||
                    plan.IsBuildingEntrance(q.x,q.y)) return;
            }
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            obj.transform.SetParent(parent, false);
            obj.transform.position = new Vector3(p.x, plan.Height(p.x,p.y)-.025f, p.y);
            obj.transform.rotation = Quaternion.Euler(0,yaw,0);
            obj.transform.localScale = Vector3.one*scale;
            count++;
        }

        private static Material Material(string root, bool far, bool shrub)
        {
            string path = root + "/Materials/" + (shrub ? "GardenShrub" : "GardenPlants") + (far ? "_Far" : "") + ".mat";
            var shader = Shader.Find(far ? "Little Castle/Distance/LC Distant Simple" : "Little Castle/Foliage/LC Foliage");
            if (!shader) throw new InvalidOperationException("Missing shared plant shader");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.shader = shader;
            material.enableInstancing = true;
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(shrub ?
                TerrainStarterSceneBuilder.Root + "/Textures/T_Foliage_BaseColor.png" : root + "/Textures/GardenPalette.png");
            if (!palette) throw new FileNotFoundException("GardenPalette.png");
            material.SetTexture("_MainTex",palette);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_TextureDetail",shrub ? .62f : 1);
            material.SetFloat("_CanopyNormalBlend",shrub ? .85f : 0);
            if(shrub) {
                material.SetVector("_CanopyCenter",new Vector4(0,.30f,0,0));
                material.SetColor("_Color",new Color(.74f,.85f,.57f));
                material.SetFloat("_Cutoff",.45f);
            }
            if (far)
            {
                material.SetFloat("_UseAlphaClip",shrub ? 1 : 0);
                material.SetFloat("_Cull",0);
                material.SetFloat("_TopLightStrength",.95f);
                material.SetFloat("_AmbientStrength",.85f);
            }
            else
            {
                material.SetFloat("_BumpScale",0);
                material.SetFloat("_OcclusionStrength",0);
                material.SetFloat("_LightResponse",.8f);
                material.SetFloat("_ShadowSoftness",.75f);
                material.SetFloat("_TransmissionStrength",.10f);
                material.SetFloat("_ColorVariation",.06f);
                material.SetFloat("_UseVertexWindMask",1);
                material.SetFloat("_UseHeightWindMask",0);
                material.SetFloat("_WindAmplitude",.035f);
                material.SetFloat("_LeafFlutter",.009f);
                material.SetFloat("_VertexWave",.18f);
                material.SetFloat("_CrownSway",.25f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
