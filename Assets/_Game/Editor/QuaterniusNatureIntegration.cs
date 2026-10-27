using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Rebuildable adapters only. Never modifies the delivered source kit or shared shaders.</summary>
    public static class QuaterniusNatureIntegration
    {
        public const string Source="Assets/_Game/Art/Imported/QuaterniusNature";
        public const string Root="Assets/_Game/Art/QuaterniusMeadow";
        public const string LibraryPath=Root+"/Quaternius_MeadowLibrary.asset";
        public static string Output=>Path.GetFullPath(Application.dataPath+"/../../Research/Flowers_20261026");
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();

        [MenuItem("Little Castle/Map/Nature/Integrate Quaternius Meadow")]
        public static void Integrate()
        {
            Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/Materials");
            Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            materials.Clear();
            var report=new StringBuilder("model,vertices,triangles,materials,sourceSize,adaptedSize,classification\n");
            var prototypes=new List<MeadowScatterLibrary.Prototype>();
            var treeCatalog=MapTreeCatalogBuilder.Ensure();
            var trees=treeCatalog.entries.Where(e=>!e.definition.archetypeId.StartsWith("quaternius.",StringComparison.Ordinal)).ToList();
            foreach(string path in Directory.GetFiles(Source+"/Models","*.fbx").OrderBy(p=>p,StringComparer.Ordinal))
            {
                string asset=path.Replace('\\','/');
                var importer=(ModelImporter)AssetImporter.GetAtPath(asset);
                // Only our imported copy: keep mesh readable for the one-time editor bake.
                importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                string name=Path.GetFileNameWithoutExtension(path);
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(asset);
                var instance=Object.Instantiate(source);instance.hideFlags=HideFlags.HideAndDontSave;
                instance.transform.position=Vector3.zero;
                try
                {
                    var filters=instance.GetComponentsInChildren<MeshFilter>(true);
                    if(filters.Length==0)throw new InvalidDataException("No mesh: "+name);
                    var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();
                    var slots=new List<Material>();var indices=new List<List<int>>();
                    foreach(var filter in filters)
                    {
                        Mesh mesh=filter.sharedMesh;var renderer=filter.GetComponent<Renderer>();
                        if(!mesh||!renderer)continue;
                        if(mesh.normals.Length!=mesh.vertexCount||mesh.uv.Length!=mesh.vertexCount)
                            throw new InvalidDataException("Missing normals/UV0: "+name);
                        var transform=filter.transform.localToWorldMatrix;int first=vertices.Count;
                        vertices.AddRange(mesh.vertices.Select(v=>transform.MultiplyPoint3x4(v)));
                        normals.AddRange(mesh.normals.Select(n=>transform.inverse.transpose.MultiplyVector(n).normalized));
                        uv.AddRange(mesh.uv);
                        for(int sub=0;sub<mesh.subMeshCount;sub++)
                        {
                            string materialName=renderer.sharedMaterials[sub].name.Replace(" (Instance)","");
                            var material=MaterialFor(materialName);int slot=slots.IndexOf(material);
                            if(slot<0){slot=slots.Count;slots.Add(material);indices.Add(new List<int>());}
                            var tris=mesh.GetTriangles(sub);
                            for(int i=0;i<tris.Length;i+=3)
                            {indices[slot].Add(first+tris[i]);indices[slot].Add(first+tris[i+(transform.determinant<0?2:1)]);indices[slot].Add(first+tris[i+(transform.determinant<0?1:2)]);}
                        }
                    }
                    var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);
                    bool tree=IsTree(name), flower=name.StartsWith("Flower_"), grass=name.StartsWith("Grass_");
                    float height=flower?.48f:grass?(name.Contains("Tall")?.72f:.36f):name.StartsWith("Clover")?.20f:name.StartsWith("Fern")?.58f:tree?7f:name.StartsWith("Bush")?1.15f:name.StartsWith("Plant")?.5f:name.StartsWith("Pebble")?.18f:name.StartsWith("Rock_Medium")?1.25f:name.StartsWith("Mushroom")?.25f:-1;
                    float scale=height>0?height/Mathf.Max(.001f,bounds.size.y):1;
                    Vector3 pivot=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                    for(int i=0;i<vertices.Count;i++)vertices[i]=(vertices[i]-pivot)*scale;
                    var adapted=new Mesh{name="Q_"+name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                    adapted.SetVertices(vertices);adapted.SetNormals(normals);adapted.SetUVs(0,uv);adapted.subMeshCount=slots.Count;
                    for(int i=0;i<slots.Count;i++)adapted.SetTriangles(indices[i],i,false);
                    adapted.RecalculateBounds();adapted.RecalculateTangents();
                    adapted=Save(adapted,Root+"/Meshes/"+name+".asset");
                    var root=new GameObject("Q_"+name);root.AddComponent<MeshFilter>().sharedMesh=adapted;
                    var rr=root.AddComponent<MeshRenderer>();rr.sharedMaterials=slots.ToArray();rr.shadowCastingMode=tree?ShadowCastingMode.On:ShadowCastingMode.Off;
                    rr.lightProbeUsage=LightProbeUsage.Off;
                    var previous=Selection.activeObject;
                    try{Selection.activeGameObject=root;FoliageModelCompatibilityValidator.ValidateSelected();}
                    finally{Selection.activeObject=previous;}
                    var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+name+".prefab");Object.DestroyImmediate(root);
                    report.AppendLine($"{name},{vertices.Count},{indices.Sum(i=>i.Count)/3},{string.Join("+",slots.Select(s=>s.name))},\"{bounds.size}\",\"{adapted.bounds.size}\",COMPATIBLE_STATIC_URP");
                    if(flower||grass||name.StartsWith("Clover")||name.StartsWith("Fern")||name.StartsWith("Plant_")||name.StartsWith("Pebble")||name=="Mushroom_Common")
                    {
                        bool group=name.EndsWith("Group");
                        prototypes.Add(new MeadowScatterLibrary.Prototype{stableId="quaternius."+name,sourceAssetPath=asset,
                            category=name.StartsWith("Pebble")?MeadowScatterCategory.Stone:name.StartsWith("Mushroom")||name.StartsWith("Fern")?MeadowScatterCategory.ForestDebris:MeadowScatterCategory.Flower,
                            lodMeshes=new[]{adapted,adapted,adapted,adapted},materials=slots.ToArray(),localBounds=adapted.bounds,
                            scaleRange=new Vector2(.78f,1.18f),weight=flower?(group?.16f:1):grass?.12f:name.StartsWith("Clover")?.22f:.06f,
                            groundInset=.004f,maximumSlope=group?3:flower?27:32,maximumTilt=group?3:flower?4:9,
                            clearanceRadius=Mathf.Max(.1f,Mathf.Max(adapted.bounds.extents.x,adapted.bounds.extents.z)*1.18f),
                            enabled=!name.Contains("Big")});
                    }
                    if(tree)
                    {
                        trees.Add(TreeEntry(name,prefab,adapted.bounds,false));
                        if(name.StartsWith("CommonTree"))
                        {
                            var autumn=Object.Instantiate(prefab);autumn.name="Q_"+name+"_Autumn";
                            var autumnRenderer=autumn.GetComponent<MeshRenderer>();
                            autumnRenderer.sharedMaterials=slots.Select(m=>m.name.Contains("Leaves")?Autumn(m):m).ToArray();
                            var autumnPrefab=PrefabUtility.SaveAsPrefabAsset(autumn,Root+"/Prefabs/"+name+"_Autumn.prefab");Object.DestroyImmediate(autumn);
                            trees.Add(TreeEntry(name,autumnPrefab,adapted.bounds,true));
                        }
                    }
                }
                finally{Object.DestroyImmediate(instance);}
            }
            var library=AssetDatabase.LoadAssetAtPath<MeadowScatterLibrary>(LibraryPath);
            if(!library){library=ScriptableObject.CreateInstance<MeadowScatterLibrary>();AssetDatabase.CreateAsset(library,LibraryPath);}
            var brush=AssetDatabase.LoadAssetAtPath<MeadowScatterLibrary>(MeadowScatterAssets.LibraryPath);
            // Preserve authored per-species settings when explicitly rebuilding adapters.
            if(library.prototypes!=null)foreach(var p in prototypes)
            {var old=brush.prototypes.FirstOrDefault(o=>o.stableId==p.stableId)??library.prototypes.FirstOrDefault(o=>o.stableId==p.stableId);if(old==null)continue;p.enabled=old.enabled;p.weight=old.weight;p.scaleRange=old.scaleRange;p.maximumSlope=old.maximumSlope;p.maximumTilt=old.maximumTilt;p.allowedBiomes=old.allowedBiomes;p.allowGrass=old.allowGrass;p.allowWoodlandFloor=old.allowWoodlandFloor;p.allowMud=old.allowMud;p.yawRange=old.yawRange;p.clearanceRadius=old.clearanceRadius;p.groundInset=old.groundInset;}
            library.prototypes=prototypes.ToArray();library.preflightNotes="Quaternius copied Unity FBX adapters; +Y up, root base at 0. Static URP LC Foliage for alpha plants, LC Stylized Lit for rocks/bark. UVY is texture atlas data: NOT accepted for LC Grass bending. No source LODs: four slots reference the same small mesh, distance culling remains active. Trees retained as gallery/brush options; original Oak entries unchanged. Full numerical audit: Research/Flowers_20261026/model-audit.csv.";
            EditorUtility.SetDirty(library);
            // Append to the existing brush library so every existing scene and save uses the same tools.
            var oldTypes=brush.prototypes.Where(p=>!p.stableId.StartsWith("quaternius.",StringComparison.Ordinal)).ToList();
            foreach(var p in oldTypes)if(p.category==MeadowScatterCategory.Flower)p.weight=Mathf.Min(p.weight,.08f);
            oldTypes.AddRange(library.prototypes);brush.prototypes=oldTypes.ToArray();EditorUtility.SetDirty(brush);
            treeCatalog.entries=trees.ToArray();
            var packs=treeCatalog.packs.Where(p=>!p.id.StartsWith("quaternius.",StringComparison.Ordinal)).ToList();
            var meadow=new MapTreePack{id="quaternius.meadow.mixed",authorOnly=true};
            foreach(var t in trees.Where(t=>t.definition.archetypeId.StartsWith("quaternius.CommonTree",StringComparison.Ordinal)))
                meadow.entries.Add(new MapTreePackEntry{archetypeId=t.definition.archetypeId,weight=t.definition.archetypeId.EndsWith(".autumn")?.3f:1});
            packs.Add(meadow);treeCatalog.packs=packs.ToArray();EditorUtility.SetDirty(treeCatalog);
            File.WriteAllText(Path.Combine(Output,"model-audit.csv"),report.ToString());
            ConfigureWorld(brush);
            RefreshBrushGroups();
            AssetDatabase.SaveAssets();Debug.Log("[Quaternius] Integrated "+prototypes.Count+" scatter types; "+trees.Count+" total tree choices. "+Output);
        }
        static void ConfigureWorld(MeadowScatterLibrary library)
        {
            foreach(string guid in AssetDatabase.FindAssets("t:WorldSpawnCatalog",new[]{"Assets/_Game/Settings/World","Assets/_Game/Models/Ready"}))
            {var catalog=AssetDatabase.LoadAssetAtPath<WorldSpawnCatalog>(AssetDatabase.GUIDToAssetPath(guid));catalog.groundCoverLibrary=library;EditorUtility.SetDirty(catalog);}
            foreach(string guid in AssetDatabase.FindAssets("t:ObjectScatterStage",new[]{"Assets/_Game/Settings/World"}))
            {
                var stage=AssetDatabase.LoadAssetAtPath<ObjectScatterStage>(AssetDatabase.GUIDToAssetPath(guid));
                var rules=(List<ScatterSpawnRule>)stage.Rules;rules.RemoveAll(r=>r!=null&&r.ruleId=="quaternius.meadow.flowers.v1");
                rules.Add(new ScatterSpawnRule{ruleId="quaternius.meadow.flowers.v1",archetypeId="quaternius.Flower_3_Single",category=SpawnCategory.Decoration,
                    flowerMeadow=true,spacing=.85f,borderJitter=.26f,baseChance=.7f,multiplyByForestDensity=false,
                    maxSlope=25,allowedTerrain=TerrainClassMask.Plains|TerrainClassMask.RollingHills,
                    allowedBiomes=BiomeMask.TemperateGrassland|BiomeMask.TemperateWoodland|BiomeMask.HighlandMeadow,
                    minScale=.78f,maxScale=1.18f,flowerVariants=new[]{"quaternius.Flower_3_Single","quaternius.Flower_4_Single"},flowerWeights=new[]{1f,.85f}});
                EditorUtility.SetDirty(stage);
            }
            foreach(string guid in AssetDatabase.FindAssets("t:GroundCoverStage",new[]{"Assets/_Game/Settings/World"}))
            {
                var stage=AssetDatabase.LoadAssetAtPath<GroundCoverStage>(AssetDatabase.GUIDToAssetPath(guid));var so=new SerializedObject(stage);
                so.FindProperty("respectGrassPlacementBlocks").boolValue=true;so.FindProperty("fadeGrassOnSteepSlopes").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        static bool IsTree(string n)=>n.StartsWith("CommonTree")||n.StartsWith("DeadTree")||n.StartsWith("TwistedTree")||n.StartsWith("Pine_");
        static MapTreeCatalog.Entry TreeEntry(string name,GameObject prefab,Bounds bounds,bool autumn)=>new MapTreeCatalog.Entry{
            title="Quaternius / "+name+(autumn?" / Осень":""),prefab=prefab,localBounds=bounds,
            definition=new MapTreeDefinition{archetypeId="quaternius."+name+(autumn?".autumn":""),minimumSpacing=4,minimumScale=.85f,maximumScale=1.15f,maxSlopeDegrees=32}};
        static Material Autumn(Material source)
        {
            string key=source.name+"_Autumn";if(materials.TryGetValue(key,out var result))return result;
            result=new Material(source){name=key};result.SetColor("_Color",new Color(1,.55f,.12f,1));
            // Shared foliage already supports tint with retained texture alpha/detail.
            result.SetFloat("_TextureDetail",.18f);
            result=Save(result,Root+"/Materials/"+key+".mat");materials[key]=result;return result;
        }
        public static void RefreshAutumnPalette()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(!material.name.EndsWith("_Autumn",StringComparison.Ordinal))continue;
                material.SetColor("_Color",new Color(1,.55f,.12f,1));material.SetFloat("_TextureDetail",.18f);EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }
        public static void UseSharedMeadowLibrary()
        {
            var shared=AssetDatabase.LoadAssetAtPath<MeadowScatterLibrary>(MeadowScatterAssets.LibraryPath);
            foreach(string guid in AssetDatabase.FindAssets("t:WorldSpawnCatalog",new[]{"Assets/_Game/Settings/World","Assets/_Game/Models/Ready"}))
            {var catalog=AssetDatabase.LoadAssetAtPath<WorldSpawnCatalog>(AssetDatabase.GUIDToAssetPath(guid));catalog.groundCoverLibrary=shared;EditorUtility.SetDirty(catalog);}
            AssetDatabase.SaveAssets();
        }
        public static void RefreshBrushGroups()
        {
            var library=AssetDatabase.LoadAssetAtPath<MeadowScatterLibrary>(MeadowScatterAssets.LibraryPath);
            var types=library.prototypes.ToList();
            foreach(var p in types)
            {
                if(!p.stableId.StartsWith("quaternius.",StringComparison.Ordinal))continue;
                string name=p.stableId.Substring("quaternius.".Length);
                if(name.StartsWith("Grass_")||name.StartsWith("Clover_"))p.category=MeadowScatterCategory.Grass;
                if(name.StartsWith("Plant_"))p.category=MeadowScatterCategory.Shrub;
            }
            foreach(string name in new[]{"Bush_Common","Bush_Common_Flowers","Rock_Medium_1","Rock_Medium_2","Rock_Medium_3"})
            {
                if(types.Any(p=>p.stableId=="quaternius."+name))continue;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+name+".prefab");
                var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;var mats=prefab.GetComponent<MeshRenderer>().sharedMaterials;
                types.Add(new MeadowScatterLibrary.Prototype{stableId="quaternius."+name,sourceAssetPath=Source+"/Models/"+name+".fbx",
                    category=name.StartsWith("Bush")?MeadowScatterCategory.Shrub:MeadowScatterCategory.Stone,
                    lodMeshes=new[]{mesh,mesh,mesh,mesh},materials=mats,localBounds=mesh.bounds,scaleRange=new Vector2(.8f,1.1f),
                    weight=.035f,groundInset=.015f,maximumSlope=18,maximumTilt=5,clearanceRadius=Mathf.Max(mesh.bounds.extents.x,mesh.bounds.extents.z)*1.1f});
            }
            library.prototypes=types.ToArray();EditorUtility.SetDirty(library);
            var catalog=MapTreeCatalogBuilder.Ensure();var packs=catalog.packs.ToList();
            string[] shortTitles={"Дуб","Луговое","Осеннее","Хвойное","Сухое","Изогнутое"};
            foreach(var entry in catalog.entries)
            {
                string id=entry.definition.archetypeId;if(!id.StartsWith("quaternius.",StringComparison.Ordinal))continue;
                string variant=id.Substring(id.LastIndexOf('_')+1).Replace(".autumn","");
                entry.title=shortTitles[MapTreeCatalog.Group(entry)]+" "+variant;
            }
            for(int group=1;group<MapTreeCatalog.GroupNames.Length;group++)
            {
                string id="quaternius.group."+group;if(packs.Any(p=>p.id==id))continue;
                var pack=new MapTreePack{id=id,authorOnly=true};
                foreach(int index in catalog.GroupIndices(group))pack.entries.Add(new MapTreePackEntry{archetypeId=catalog.entries[index].definition.archetypeId,weight=1});
                if(pack.entries.Count>0)packs.Add(pack);
            }
            catalog.packs=packs.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        }
        public static void ImportReviewThumbnails()
        {
            var catalog=MapTreeCatalogBuilder.Ensure();string folder=MapTreeCatalogBuilder.Root+"/Thumbnails";Directory.CreateDirectory(folder);
            foreach(var entry in catalog.entries)
            {
                if(!entry.definition.archetypeId.StartsWith("quaternius.",StringComparison.Ordinal))continue;
                string name=MapTreeCatalog.ThumbnailName(entry),source=Output+"/TreePreviews/"+name;
                if(!File.Exists(source))continue;string target=folder+"/"+name;File.Copy(source,target,true);
                AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(target);importer.mipmapEnabled=false;importer.maxTextureSize=256;importer.SaveAndReimport();entry.thumbnail=AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            }
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        }
        static Material MaterialFor(string name)
        {
            if(materials.TryGetValue(name,out var found))return found;
            string textureName=name;
            if(name=="PathRocks"||name=="Rocks"||name=="Rocks_Desert")textureName=name+"_Diffuse";
            if(name=="Leaves_Pine")textureName="Leaf_Pine_C";
            if(name=="Leaves_NormalTree"||name=="Leaves_TwistedTree")textureName=name+"_C";
            if(name!="Material"&&!File.Exists(Source+"/Textures/"+textureName+".png"))
                throw new InvalidDataException("Unmapped source material: "+name);
            bool foliage=name.Contains("Leaves")||name.Contains("Leaf")||name=="Flowers"||name=="Grass"||name=="Mushrooms";
            var material=new Material(Shader.Find(foliage?"Little Castle/Foliage/LC Foliage":"Little Castle/Surface/LC Stylized Lit")){name="Q_"+name,enableInstancing=true};
            if(name!="Material")material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"/Textures/"+textureName+".png"));
            material.SetFloat("_LightResponse",1);
            if(foliage){material.SetFloat("_WindAmplitude",0);material.SetFloat("_WindSecondary",0);material.SetFloat("_CrownSway",0);material.SetFloat("_VertexWave",0);material.SetFloat("_LeafFlutter",0);material.SetFloat("_Cutoff",.4f);}
            string normalPath=Source+"/Textures/"+name+"_Normal.png";
            if(File.Exists(normalPath))
            {var imp=(TextureImporter)AssetImporter.GetAtPath(normalPath);imp.textureType=TextureImporterType.NormalMap;imp.SaveAndReimport();material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));}
            material=Save(material,Root+"/Materials/Q_"+name+".mat");materials[name]=material;return material;
        }
        static T Save<T>(T value,string path) where T:Object
        {var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing){EditorUtility.CopySerialized(value,existing);Object.DestroyImmediate(value);EditorUtility.SetDirty(existing);return existing;}AssetDatabase.CreateAsset(value,path);return value;}
    }
}
