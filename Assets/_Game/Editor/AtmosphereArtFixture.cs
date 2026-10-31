using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using LittleCastle.World.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Disposable composition in the existing Play scene, using the game's actual assets/shaders.</summary>
    internal sealed class AtmosphereArtFixture : IDisposable
    {
        readonly GameObject root;
        readonly List<Object> owned = new List<Object>();
        readonly Vector3 origin = new Vector3(280, 0, 280);
        readonly MapEditSession environmentSession;
        readonly MapEnvironmentPresentation environment;
        readonly Camera camera;
        readonly bool snowVillage;
        readonly List<Bounds> buildingFootprints = new List<Bounds>();
        readonly List<Vector2> buildingCenters = new List<Vector2>();
        TerrainStarterSoftScene softScene;
        bool addedSoftScene, previousSoftEnabled;
        Material previousSoftMaterial;
        float previousBlur, previousFocusBand, previousContact, previousFilter;
        MapFencePresentation fences;
        public GameObject Root => root;
        public Vector3 Focus => origin + (snowVillage ? new Vector3(0, 4, 22) : new Vector3(0, 3.4f, 13));

        public AtmosphereArtFixture(Camera camera, MeadowSurfacePainter painter, bool snowVillage = false)
        {
            this.camera = camera;
            this.snowVillage = snowVillage;
            if (!snowVillage) painter.enabled = false;
            root = new GameObject(snowVillage ? "Temporary snowy mountain village" : "Temporary atmosphere reference garden") {hideFlags = HideFlags.HideAndDontSave};
            var document = new MapAuthoringDocument {mapId = "local-atmosphere-reference", bounds = new Vector4(152, 216, 256, 256)};
            environmentSession = new MapEditSession(document);
            environment = new MapEnvironmentPresentation(environmentSession);
            if (snowVillage)
            {
                VillageHouse(AtmosphereArchitectureAssets.TownHallL1, new Vector3(-4, 0, 13), 8, 180);
                VillageHouse(AtmosphereArchitectureAssets.TownHallL2, new Vector3(9, 0, 31), 12, 190);
                VillageHouse(AtmosphereArchitectureAssets.TownHallL1, new Vector3(-17, 0, 32), 7.5f, 160);
                VillageHouse(AtmosphereArchitectureAssets.TownHallL1, new Vector3(20, 0, 47), 7, 205);
                VillageHouse(AtmosphereArchitectureAssets.TownHallL2, new Vector3(-8, 0, 57), 10, 180);
            }
            Ground(painter.terrainMaterial);
            var trees = new[] {
                new Vector3(-7,0,-1), new Vector3(10,0,0),
                new Vector3(-9,0,13), new Vector3(11,0,16),
                new Vector3(-10,0,28), new Vector3(14,0,32)};
            for (int i = 0; i < 64; i++)
            {
                Vector3 position = i < trees.Length ? trees[i] : new Vector3(
                    ((i * 13.71f) % 74) - 37, 0, 32 + ((i * 7.19f) % 62));
                if(i >= trees.Length && position.z < 44 && Mathf.Abs(position.x) < 20) position.z += 23;
                if (snowVillage && (NearFoundation(position.x, position.z, 1)
                    || buildingCenters.Exists(p => Vector2.Distance(p, new Vector2(position.x, position.z)) < 8))) continue;
                var tree = Spawn("Assets/_Game/Art/Studies/ValleyArt_Pass03/Oak_" + (char)('A' + i % 4) + "_Study.prefab", position, 8.3f + (i % 4) * .75f, i * 47);
                foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int m = 0; m < materials.Length; m++)
                    {
                        if (!materials[m] || materials[m].shader.name != "Little Castle/Foliage/LC Foliage") continue;
                        var properties = new MaterialPropertyBlock();
                        // Per-instance art direction; source assets and authored biomes stay unchanged.
                        properties.SetColor("_Color", snowVillage
                            ? (i % 3 == 0 ? new Color(.56f,.64f,.49f) : i % 3 == 1 ? new Color(.49f,.59f,.48f) : new Color(.62f,.66f,.52f))
                            : (i % 3 == 0 ? new Color(1,.65f,.25f) : i % 3 == 1 ? new Color(.95f,.45f,.17f) : new Color(.94f,.57f,.27f)));
                        properties.SetColor("_TransmissionColor", snowVillage ? new Color(.77f,.86f,.58f) : new Color(1,.57f,.2f));
                        properties.SetFloat("_TextureDetail", snowVillage ? .3f : .15f);
                        renderer.SetPropertyBlock(properties, m);
                    }
                }
            }
            for (int i = 0; i < 34; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float z = -5 + (i * 1.11f) % 28;
                float x = side * (2.6f + (i * .618f % 1) * 2.4f) + PathX(z);
                if (snowVillage && NearFoundation(x, z, 1.5f)) continue;
                Spawn("Assets/_Game/Art/Imported/TerrainStarter_v001/Study/Prefabs/" + (i % 3 == 0 ? "GardenShrub_A" : i % 3 == 1 ? "GardenDaisies_A" : "GardenLupins_A") + ".prefab", new Vector3(x,0,z), i % 3 == 0 ? 1.2f : .7f + (i % 4) * .12f, i * 137);
            }
            Grass(painter.grassMaterial);
            document.fences.Add(new MapFenceRecord {id = 101, points = new List<Vector2> {XZ(-5,-9),XZ(-5,-3),XZ(-6,2),XZ(-7,6)}, scale = .85f});
            document.fences.Add(new MapFenceRecord {id = 102, points = new List<Vector2> {XZ(5,-8),XZ(5,-1),XZ(7,5)}, scale = .85f});
            var fenceSession = new MapEditSession(document);
            fences = new MapFencePresentation(fenceSession, MapFenceCatalog.Default);
            fences.Tick(Focus);
            // Reference beauty view only; production StrategyCamera pitch limits remain unchanged.
            camera.transform.position = origin + (snowVillage ? new Vector3(10, 14, -16) : new Vector3(5, 9.0f, -9));
            camera.transform.LookAt(Focus);
            camera.orthographic = false; camera.fieldOfView = snowVillage ? 48 : 42; camera.farClipPlane = 400;
            camera.clearFlags = CameraClearFlags.Skybox;
            ConfigureSoftScene();
            if (snowVillage) SetWinter(true); else Tick();
        }

        Vector2 XZ(float x, float z) => new Vector2(origin.x + x, origin.z + z);
        static float PathX(float z) => Mathf.Sin((z + 6) * .12f) * 1.6f;
        static float Height(float x, float z) => Mathf.Sin(x * .14f) * Mathf.Cos(z * .10f) * .18f +
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(28, 85, z)) * (2.5f + Mathf.Sin(x * .075f) * 1.8f + Mathf.Cos(z * .08f));

        void VillageHouse(string path, Vector3 local, float height, float yaw)
        {
            var house = Spawn(path, local, height, yaw);
            var renderers = house.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            bounds.center -= origin;
            buildingFootprints.Add(bounds);
            buildingCenters.Add(new Vector2(local.x, local.z));
        }

        bool NearFoundation(float x, float z, float margin)
        {
            foreach (var bounds in buildingFootprints)
                if (x >= bounds.min.x - margin && x <= bounds.max.x + margin
                    && z >= bounds.min.z - margin && z <= bounds.max.z + margin) return true;
            return false;
        }

        float PathDistance(float x, float z)
        {
            float distance = Mathf.Abs(x - PathX(z));
            if (!snowVillage) return distance;
            // Short branches leave the shared lane and meet the near edge of each foundation.
            foreach (var bounds in buildingFootprints)
            {
                float branchZ = bounds.min.z - .8f;
                var a = new Vector2(PathX(branchZ), branchZ);
                var b = new Vector2(bounds.center.x, branchZ);
                Vector2 delta = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(x, z) - a, delta) / Mathf.Max(.001f, delta.sqrMagnitude));
                distance = Mathf.Min(distance, Vector2.Distance(new Vector2(x, z), a + delta * t));
            }
            return distance;
        }

        static void WarmWindows(GameObject house)
        {
            // Tier 0 only: existing window submeshes glow; no local Light is added.
            foreach (var renderer in house.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    var material = materials[m];
                    if (!material || material.name.IndexOf("WindowGlass", StringComparison.OrdinalIgnoreCase) < 0
                        || !material.HasProperty("_EmissionColor")) continue;
                    var properties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(properties, m);
                    properties.SetTexture("_EmissionMap", Texture2D.whiteTexture);
                    properties.SetColor("_EmissionColor", new Color(1.25f, .49f, .12f, 1));
                    properties.SetFloat("_EmissionDayStrength", .02f);
                    properties.SetFloat("_EmissionNightStrength", 2.1f);
                    properties.SetFloat("_EmissionFlickerStrength", .015f);
                    renderer.SetPropertyBlock(properties, m);
                }
            }
        }

        void ConfigureSoftScene()
        {
            softScene = camera.GetComponent<TerrainStarterSoftScene>();
            addedSoftScene = !softScene;
            if (!softScene) softScene = camera.gameObject.AddComponent<TerrainStarterSoftScene>();
            previousSoftEnabled = softScene.enabled;
            previousSoftMaterial = softScene.effectMaterial;
            previousBlur = softScene.blurStrength;
            previousFocusBand = softScene.focusBand;
            previousContact = softScene.contactOcclusion;
            previousFilter = softScene.shadowFilterRadius;
            var source = previousSoftMaterial ? previousSoftMaterial : AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Art/Imported/TerrainStarter_v001/Study/Materials/SoftScene.mat");
            if (!source) throw new InvalidOperationException("Reference fixture SoftScene material missing.");
            var material = new Material(source) {name = "Reference focus (temporary)", hideFlags = HideFlags.HideAndDontSave};
            owned.Add(material);
            softScene.effectMaterial = material;
            softScene.blurStrength = .65f;
            softScene.focusBand = 5;
            softScene.contactOcclusion = 0; // Existing SSAO renderer owns contact; avoid a second AO layer.
            softScene.shadowFilterRadius = 3;
            softScene.enabled = true;
        }

        GameObject Spawn(string path, Vector3 local, float height, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset) throw new InvalidOperationException("Reference fixture asset missing: " + path);
            var instance = Object.Instantiate(asset, root.transform);
            instance.name = asset.name + " (atmosphere reference)";
            instance.transform.position = origin + local; instance.transform.rotation = Quaternion.Euler(0,yaw,0);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Reference fixture has no renderers: " + path);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            instance.transform.localScale *= height / Mathf.Max(.01f, bounds.size.y);
            bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            instance.transform.position += Vector3.up * (origin.y + Height(local.x,local.z) - bounds.min.y);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            return instance;
        }

        void Ground(Material source)
        {
            const int side = 193;
            var positions = new Vector3[side*side]; var colors = new Color[positions.Length]; var uv = new Vector2[positions.Length];
            var triangles = new int[(side-1)*(side-1)*6]; int index = 0;
            for(int z=0;z<side;z++) for(int x=0;x<side;x++)
            {
                // One-metre detail through the garden; a coarse apron extends beyond the frame.
                float Coordinate(int sample) { float d=sample-96, a=Mathf.Abs(d); return Mathf.Sign(d)*(a<=48?a:48+(a-48)*5); }
                float px=Coordinate(x),pz=Coordinate(z)+32; int v=z*side+x;
                positions[v]=new Vector3(px,Height(px,pz),pz); uv[v]=new Vector2(x/192f,z/192f);
                float path=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.25f,1.95f,PathDistance(px,pz)));
                colors[v]=new Color(path,0,0,1);
                if(x==side-1||z==side-1)continue;
                triangles[index++]=v;triangles[index++]=v+side;triangles[index++]=v+1;
                triangles[index++]=v+1;triangles[index++]=v+side;triangles[index++]=v+side+1;
            }
            var mesh=new Mesh{name="Reference garden ground"};owned.Add(mesh);mesh.vertices=positions;mesh.uv=uv;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            var material=new Material(source){name="Reference garden ground (temporary)",hideFlags=HideFlags.HideAndDontSave};owned.Add(material);
            material.DisableKeyword("_LC_SURFACE_PAINT");material.SetFloat("_UseSurfacePaint",0);material.SetFloat("_UseVertexMasks",1);material.SetFloat("_PathMaskStrength",1);
            var go=new GameObject("Reference garden ground");go.transform.SetParent(root.transform);go.transform.position=origin;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        void Grass(Material source)
        {
            if (!source) return;
            // Reuse production clumps, including curved five-vertex blades and UV1 root anchors.
            var data=ScriptableObject.CreateInstance<MeadowGrassData>();
            data.name="Reference meadow instances (temporary)";data.hideFlags=HideFlags.HideAndDontSave;owned.Add(data);
            data.lodMeshes=new Mesh[4];
            for(int lod=0;lod<4;lod++)
            {
                data.lodMeshes[lod]=AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/_Game/Art/Studies/Meadow_20261025/Meadow_Blades_LOD"+lod+".asset");
                if(!data.lodMeshes[lod])throw new InvalidOperationException("Native meadow clump missing: LOD"+lod);
            }
            var material=new Material(source){name="Reference grass (temporary)",hideFlags=HideFlags.HideAndDontSave};owned.Add(material);
            material.enableInstancing=true;material.EnableKeyword("_LC_MEADOW");material.DisableKeyword("_LC_SURFACE_PAINT");
            material.SetFloat("_UseSurfacePaint",0);material.SetFloat("_UseRootAnchor",1);material.SetFloat("_UseObjectAnchorFade",0);
            material.SetFloat("_DensityNear",18);material.SetFloat("_DensityFar",65);material.SetFloat("_DensityFloor",.35f);
            material.SetFloat("_FadeStart",150);material.SetFloat("_FadeEnd",190);
            material.SetFloat("_MeadowSoftStart",12);material.SetFloat("_MeadowSoftEnd",45);material.SetFloat("_MeadowSoftness",.8f);
            material.SetFloat("_MeadowRootOcclusion",.22f);material.SetFloat("_ColorVariation",.035f);
            var patches=new List<MeadowGrassData.Patch>();
            var random=new System.Random(193873);
            for(int cz=-3;cz<13;cz++)for(int cx=-6;cx<6;cx++)
            {
                var matrices=new List<Matrix4x4>();
                for(int z=0;z<12;z++)for(int x=0;x<12;x++)
                {
                    float px=cx*8+(x+.15f+(float)random.NextDouble()*.7f)*8/12;
                    float pz=cz*8+(z+.15f+(float)random.NextDouble()*.7f)*8/12;
                    float pathDistance=PathDistance(px,pz);
                    if(pathDistance<1.45f+(float)random.NextDouble()*.45f
                        || (snowVillage ? NearFoundation(px,pz,.6f) : Mathf.Abs(px)<4.1f&&pz>8.8f&&pz<18))continue;
                    float patch=Mathf.PerlinNoise(px*.08f+31,pz*.08f+19);
                    float scale=.72f+(float)random.NextDouble()*.34f;
                    float heightScale=Mathf.Lerp(.62f,1.12f,patch)*( .85f+(float)random.NextDouble()*.3f);
                    Vector3 normal=new Vector3(Height(px-.1f,pz)-Height(px+.1f,pz),.2f,Height(px,pz-.1f)-Height(px,pz+.1f)).normalized;
                    var rotation=Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                    matrices.Add(Matrix4x4.TRS(origin+new Vector3(px,Height(px,pz)-.025f,pz),rotation,new Vector3(scale,heightScale,scale)));
                }
                if(matrices.Count==0)continue;
                patches.Add(new MeadowGrassData.Patch{bounds=new Bounds(origin+new Vector3(cx*8+4,Height(cx*8+4,cz*8+4),cz*8+4),new Vector3(11,5,11)),matrices=matrices.ToArray()});
                data.authoredClumps+=matrices.Count;
            }
            data.patches=patches.ToArray();
            var go=new GameObject("Reference native meadow · instanced clumps");go.transform.SetParent(root.transform);
            var renderer=go.AddComponent<MeadowGrassRenderer>();renderer.data=data;renderer.material=material;
            renderer.maximumDistance=190;renderer.lodDistances=new Vector3(48,85,120);
        }

        /// <summary>
        /// Returns a separate scene object whose transient geometry/material/data references
        /// are saved under assetFolder. The live fixture remains independently disposable.
        /// Environment atlases and the external fence presentation are deliberately excluded.
        /// </summary>
        public GameObject ExportPersistentCopy(string assetFolder)
        {
            assetFolder = (assetFolder ?? "").Replace('\\', '/').TrimEnd('/');
            if (!assetFolder.StartsWith("Assets/", StringComparison.Ordinal) || assetFolder.Contains(".."))
                throw new ArgumentException("Choose a new local asset folder beneath Assets/.", nameof(assetFolder));
            Directory.CreateDirectory(assetFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var persisted = new Dictionary<Object, Object>();

            Object Persist(Object source)
            {
                if (!source || AssetDatabase.Contains(source)) return source;
                if (!(source is Mesh) && !(source is Material) && !(source is MeadowGrassData)) return source;
                if (persisted.TryGetValue(source, out var existing)) return existing;
                var copy = Object.Instantiate(source);
                copy.name = source.name.Replace(" (temporary)", "");
                copy.hideFlags = HideFlags.None;
                if (copy is Material material)
                    foreach (string textureProperty in material.GetTexturePropertyNames())
                        if (material.GetTexture(textureProperty) is RenderTexture) material.SetTexture(textureProperty, null);
                persisted.Add(source, copy);
                string safeName = copy.name;
                foreach (char invalid in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(invalid, '_');
                string extension = copy is Material ? ".mat" : ".asset";
                AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(assetFolder + "/" + safeName + extension));
                Rewire(copy);
                return copy;
            }

            void Rewire(Object value)
            {
                var serialized = new SerializedObject(value);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var before = property.objectReferenceValue;
                    var after = Persist(before);
                    if (after != before) property.objectReferenceValue = after;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            // Copy, never turn owned live objects themselves into project assets.
            foreach (var item in owned)
                if (item is Mesh || item is Material || item is MeadowGrassData) Persist(item);
            var result = Object.Instantiate(root);
            result.name = snowVillage ? "Snowy Mountain Village" : "Atmosphere Reference Garden";
            result.SetActive(false);
            try
            {
                foreach (var transform in result.GetComponentsInChildren<Transform>(true))
                {
                    transform.gameObject.hideFlags = HideFlags.None;
                    foreach (var component in transform.GetComponents<Component>())
                        if (component) { component.hideFlags = HideFlags.None; Rewire(component); }
                }
                if (snowVillage)
                {
                    // MPBs are not serialized. Persist three shared winter variants per
                    // source foliage material, rather than a material for every tree/LOD.
                    var originals = root.GetComponentsInChildren<Renderer>(true);
                    var copies = result.GetComponentsInChildren<Renderer>(true);
                    var variants = new Dictionary<string, Material>();
                    if (originals.Length != copies.Length) throw new InvalidOperationException("Export renderer hierarchy changed.");
                    for (int r = 0; r < originals.Length; r++)
                    {
                        var originalsMaterials = originals[r].sharedMaterials;
                        var materials = copies[r].sharedMaterials;
                        for (int slot = 0; slot < originalsMaterials.Length; slot++)
                        {
                            var source = originalsMaterials[slot];
                            if (!source || source.shader.name != "Little Castle/Foliage/LC Foliage") continue;
                            var properties = new MaterialPropertyBlock();
                            originals[r].GetPropertyBlock(properties, slot);
                            if (properties.isEmpty) continue;
                            Color tint = properties.GetColor("_Color");
                            Color transmission = properties.GetColor("_TransmissionColor");
                            float detail = properties.GetFloat("_TextureDetail");
                            string key = AssetDatabase.GetAssetPath(source) + "/" + tint.ToString("F4") + "/" + transmission.ToString("F4") + "/" + detail;
                            if (!variants.TryGetValue(key, out var variant))
                            {
                                variant = new Material(materials[slot]) { name = "Winter foliage " + variants.Count, hideFlags = HideFlags.None };
                                variant.SetColor("_Color", tint);
                                variant.SetColor("_TransmissionColor", transmission);
                                variant.SetFloat("_TextureDetail", detail);
                                AssetDatabase.CreateAsset(variant, AssetDatabase.GenerateUniqueAssetPath(assetFolder + "/Winter_Foliage_" + variants.Count + ".mat"));
                                variants.Add(key, variant);
                            }
                            materials[slot] = variant;
                            copies[r].SetPropertyBlock(null, slot);
                        }
                        copies[r].sharedMaterials = materials;
                    }
                }
                // Fail explicitly if any object would still reference a disposable resource.
                foreach (var component in result.GetComponentsInChildren<Component>(true))
                {
                    if (!component) continue;
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var value = property.objectReferenceValue;
                        if ((value is Mesh || value is Material || value is MeadowGrassData) && !AssetDatabase.Contains(value))
                            throw new InvalidOperationException("Unsaved fixture dependency: " + value.name);
                    }
                }
                foreach (string guid in AssetDatabase.FindAssets("", new[] {assetFolder}))
                    AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
                result.SetActive(true);
                return result;
            }
            catch { Object.DestroyImmediate(result); throw; }
        }

        public void SetWinter(bool enabled)
        {
            var settings=new MapEnvironmentSettings {snowAmount=enabled?1:0,winterSnowPreview=true};
            environmentSession.SetEnvironmentSettings(settings);
            if(enabled)
            {
                environmentSession.BeginEnvironmentStroke(EnvironmentThemeIds.Winter);
                environmentSession.ApplyEnvironmentBrush(XZ(0,32),120,1,0);
                environmentSession.CommitStroke();
            }
            Tick();
        }

        public void Tick() {environment.Tick(camera); fences?.Tick(Focus);}

        public void Dispose()
        {
            try {environment.Dispose();fences?.Dispose();}
            finally
            {
                if(softScene)
                {
                    if(addedSoftScene)Object.DestroyImmediate(softScene);
                    else
                    {
                        softScene.effectMaterial=previousSoftMaterial;softScene.blurStrength=previousBlur;
                        softScene.focusBand=previousFocusBand;softScene.contactOcclusion=previousContact;
                        softScene.shadowFilterRadius=previousFilter;softScene.enabled=previousSoftEnabled;
                    }
                }
                if(root)Object.DestroyImmediate(root);foreach(var item in owned)if(item)Object.DestroyImmediate(item);owned.Clear();
            }
        }
    }
}
