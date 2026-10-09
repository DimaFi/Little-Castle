using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>Explicit immutable release intake. Owns ONLY CliffKit_v001 imports and a separate catalog.</summary>
    public static class CliffKitAssetIntake
    {
        public const string Destination = "Assets/_Game/Art/Imported/CliffKit/v001";
        public const string CatalogPath = Destination + "/CliffKitCatalog.asset";
        [Serializable] public class Entry
        {
            public string id, kind, profile, archetypeId;
            public float width, height, depth, nominal_grade_degrees, clear_walk_width_m;
            public int[] lod_triangle_counts; public string[] lod_fbx;
        }
        [Serializable] public class Catalog { public Entry[] assets; }
        [Serializable] private class FileRecord { public string path, sha256; public long bytes; }
        [Serializable] private class Release { public string asset_id, version; public FileRecord[] files; }
        [Serializable] private class MeshResult { public string id; public int[] triangles; public Vector3 boundsSize; public Vector3 boundsCenter; public int materials, colliders; }
        [Serializable] private class Report
        {
            public string unity, status = "PASS_IMPORT_CONTRACT_ONLY", nav = "NOT_RUN", gpuPlayer = "NOT_RUN";
            public int prefabs; public List<MeshResult> assets = new List<MeshResult>();
        }

        [MenuItem("Little Castle/Assets/CliffKit/Import Verified v001 Release")]
        public static void Import()
        {
            string source = Environment.GetEnvironmentVariable("LITTLE_CASTLE_CLIFF_RELEASE");
            if (string.IsNullOrEmpty(source)) throw new InvalidOperationException("Set LITTLE_CASTLE_CLIFF_RELEASE to the hydrated release directory.");
            if (Directory.Exists(Destination)) throw new InvalidOperationException("Immutable intake exists. Use a new version; refusing overwrite.");
            var release = JsonUtility.FromJson<Release>(File.ReadAllText(Path.Combine(source,"release.json")));
            if (release.asset_id != "ENV_CliffKit" || release.version != "v001" || release.files == null || release.files.Length < 43)
                throw new InvalidDataException("Invalid release header.");
            // Verify EVERY payload before copying anything. Paths must remain within supplied release.
            foreach (var file in release.files)
            {
                if (string.IsNullOrWhiteSpace(file.path) || file.path.Contains("..") || file.path.Contains(":") || file.path.Contains("\\") || file.path.StartsWith("/"))
                    throw new InvalidDataException("Unsafe release path.");
                var path = Path.Combine(source,file.path); byte[] bytes = File.ReadAllBytes(path);
                using (var sha = SHA256.Create())
                    if (bytes.LongLength != file.bytes || BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant() != file.sha256)
                        throw new InvalidDataException("Payload digest mismatch: " + file.path);
            }
            foreach (var file in release.files)
            {
                string target = Destination + "/" + file.path;
                Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(Path.Combine(source,file.path),target);
            }
            File.Copy(Path.Combine(source,"release.json"),Destination+"/release.json");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string file in Directory.GetFiles(Destination+"/Textures","*.png",SearchOption.AllDirectories))
            {
                string path=file.Replace('\\','/');var ti=(TextureImporter)AssetImporter.GetAtPath(path);
                bool normal=path.EndsWith("Normal_OpenGL.png");ti.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                ti.sRGBTexture=path.EndsWith("BaseColor.png");ti.mipmapEnabled=true;ti.isReadable=false;
                ti.wrapMode=TextureWrapMode.Repeat;ti.filterMode=FilterMode.Trilinear;ti.anisoLevel=4;
                ti.maxTextureSize=1024;ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();
            }
            var materials=new Dictionary<string,Material>();
            foreach(string family in new[]{"RockA","RockB","GrassCap"})
            {
                string tex=family=="RockA"?"Rock_Limestone_A":family=="RockB"?"Rock_Limestone_B":"GrassCap_A";
                materials[family]=MaterialFor(family,tex,false);
                materials[family+"Far"]=MaterialFor(family,tex,true);
            }
            var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Destination+"/ASSET_CATALOG.json"));
            var spawnCatalog=ScriptableObject.CreateInstance<WorldSpawnCatalog>();
            var state=new SerializedObject(spawnCatalog);var entries=state.FindProperty("entries");entries.arraySize=catalog.assets.Length;
            var report=new Report{unity=Application.unityVersion};
            for(int index=0;index<catalog.assets.Length;index++)
            {
                Entry entry=catalog.assets[index];var root=new GameObject(entry.id);var group=root.AddComponent<LODGroup>();
                var levels=new LOD[3];var result=new MeshResult{id=entry.id,triangles=new int[3]};
                for(int lod=0;lod<3;lod++)
                {
                    string path=Destination+"/"+entry.lod_fbx[lod];var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                    importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;
                    importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
                    importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
                    importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=false;
                    importer.meshCompression=ModelImporterMeshCompression.Off;importer.SaveAndReimport();
                    var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);instance.transform.SetParent(root.transform,false);instance.name="LOD"+lod;
                    // Unity's FBX handedness conversion maps this Blender recipe's forward to -Z.
                    // Normalize once on the imported child; the public prefab basis is +Z uphill.
                    instance.transform.localRotation=Quaternion.Euler(0,180,0)*instance.transform.localRotation;
                    var renderers=instance.GetComponentsInChildren<MeshRenderer>();
                    if(renderers.Length!=1)throw new InvalidDataException("Expected one mesh per LOD: "+path);
                    foreach(var renderer in renderers)
                    {
                        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                        int triangles=0;for(int sm=0;sm<mesh.subMeshCount;sm++)triangles+=(int)mesh.GetIndexCount(sm)/3;
                        result.triangles[lod]+=triangles;
                        if(mesh.normals.Length!=mesh.vertexCount || mesh.uv.Length!=mesh.vertexCount || mesh.tangents.Length!=mesh.vertexCount)
                            throw new InvalidDataException("Missing mesh normals/UV/tangents: "+path);
                        var assigned=new Material[mesh.subMeshCount];string rock=entry.profile=="boulder"?"RockB":"RockA";
                        assigned[0]=materials[rock+(lod==2?"Far":"")];
                        if(assigned.Length>1)assigned[1]=materials["GrassCap"+(lod==2?"Far":"")];
                        renderer.sharedMaterials=assigned;renderer.shadowCastingMode=lod==2?ShadowCastingMode.Off:ShadowCastingMode.On;
                        renderer.receiveShadows=lod!=2;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                        if(lod==0){result.boundsSize=renderer.bounds.size;result.boundsCenter=renderer.bounds.center;result.materials=assigned.Length;}
                    }
                    if(result.triangles[lod]!=entry.lod_triangle_counts[lod])throw new InvalidDataException("Triangle count mismatch: "+path);
                    // Only one socket set on prefab root. FBX duplicate socket empties are stripped from each tier.
                    var transforms=instance.GetComponentsInChildren<Transform>();
                    foreach(var t in transforms)if(t.name.StartsWith("Socket_"))UnityEngine.Object.DestroyImmediate(t.gameObject);
                    levels[lod]=new LOD(new[]{.5f,.18f,.025f}[lod],renderers);
                }
                if(Mathf.Abs(result.boundsSize.x-entry.width)>entry.width*.35f || result.boundsSize.y<entry.height*.6f || result.boundsSize.y>entry.height*1.35f)
                    throw new InvalidDataException("Metre scale or basis mismatch: "+entry.id+" "+result.boundsSize);
                if(entry.kind=="ramp")
                {
                    Socket(root,"Socket_Entry",Vector3.zero);Socket(root,"Socket_Exit",new Vector3(0,entry.height,entry.depth));
                    if(result.boundsCenter.z<entry.depth*.4f)throw new InvalidDataException("Ramp must ascend along +Z.");
                }
                else if(entry.kind=="cliff")
                {Socket(root,"Socket_Left",new Vector3(-entry.width/2,0,0));Socket(root,"Socket_Right",new Vector3(entry.width/2,0,0));}
                group.SetLODs(levels);group.fadeMode=LODFadeMode.None;group.RecalculateBounds();
                result.colliders=root.GetComponentsInChildren<Collider>().Length;
                if(result.colliders!=0)throw new InvalidDataException("Decorative art must not create navigation collision.");
                Directory.CreateDirectory(Destination+"/Prefabs");
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Destination+"/Prefabs/"+entry.id+".prefab");
                UnityEngine.Object.DestroyImmediate(root);
                var e=entries.GetArrayElementAtIndex(index);e.FindPropertyRelative("archetypeId").stringValue=entry.archetypeId;
                e.FindPropertyRelative("scaleMultiplier").floatValue=1;var prefabs=e.FindPropertyRelative("prefabs");prefabs.arraySize=1;prefabs.GetArrayElementAtIndex(0).objectReferenceValue=prefab;
                report.assets.Add(result);report.prefabs++;
            }
            state.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(spawnCatalog,CatalogPath);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CliffKitImport.json",JsonUtility.ToJson(report,true));
            Debug.Log("CLIFFKIT_IMPORT_PASS prefabs="+report.prefabs+"; separate catalog; navigation/Player acceptance pending.");
        }

        private static void Socket(GameObject parent,string name,Vector3 position)
        {var socket=new GameObject(name);socket.transform.SetParent(parent.transform,false);socket.transform.localPosition=position;}
        private static Material MaterialFor(string family,string tex,bool far)
        {
            Shader shader=Shader.Find(far?"Little Castle/Distance/LC Distant Simple":"Little Castle/Surface/LC Stylized Lit");
            if(shader==null)throw new InvalidOperationException("Existing shared shader missing.");
            var mat=new Material(shader){name="CliffKit_"+family+(far?"_Far":""),enableInstancing=true};
            string path=Destination+"/Textures/"+tex+"/";
            mat.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(path+"BaseColor.png"));
            if(far){mat.SetFloat("_TopLightStrength",.9f);mat.SetFloat("_AmbientStrength",1.25f);}
            if(!far)
            {
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path+"Normal_OpenGL.png"));mat.SetFloat("_BumpScale",.5f);
                mat.SetTexture("_RoughnessMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path+"Roughness.png"));mat.SetFloat("_RoughnessMapStrength",1);
                mat.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path+"AO_Approx.png"));mat.SetFloat("_OcclusionStrength",.35f);
            }
            Directory.CreateDirectory(Destination+"/Materials");AssetDatabase.CreateAsset(mat,Destination+"/Materials/"+mat.name+".mat");return mat;
        }
    }
}
