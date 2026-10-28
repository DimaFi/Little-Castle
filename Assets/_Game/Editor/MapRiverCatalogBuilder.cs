using System.IO;
using System.Text;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    public static class MapRiverCatalogBuilder
    {
        const string Root="Assets/_Game/Resources/MapAuthoring";
        const string Art="Assets/_Game/Art/RiverCliffs";
        [MenuItem("Little Castle/Map/Prepare River Cliff Catalog")]
        public static void Prepare(){Ensure();}
        public static MapRockCatalog Ensure()
        {
            var existing=AssetDatabase.LoadAssetAtPath<MapRockCatalog>(Root+"/MeadowRockCatalog.asset");if(existing)return existing;
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();var report=new StringBuilder();
            var catalog=ScriptableObject.CreateInstance<MapRockCatalog>();catalog.entries=new MapRockCatalog.Entry[3];
            string[] keys={"long.a","narrow.b","double.c"};string[] titles={"Длинная A","Узкая B","Двусторонняя C"};
            string[] sources={"E:/Games_Develop/Персонажи/Скалы_берег/1_длинная скала/Optimized_3k_v001/RiverCliff_Long_A_3k.fbx","E:/Games_Develop/Персонажи/Скалы_берег/2_узкая/Стилизованный скалистый утёс с травой_узкий.fbx","E:/Games_Develop/Персонажи/Скалы_берег/3_длинная_с_двух_сторон/Модульная скала с травянистыми террасами_прямая с обоих сторон.fbx"};
            for(int i=0;i<keys.Length;i++)
            {
                string folder=Art+"/"+keys[i];
                foreach(string png in Directory.GetFiles(folder,"*.png"))
                {
                    var importer=(TextureImporter)AssetImporter.GetAtPath(png);importer.maxTextureSize=1024;importer.mipmapEnabled=true;
                    if(png.Contains("Normal"))importer.textureType=TextureImporterType.NormalMap;else importer.sRGBTexture=png.Contains("BaseColor");importer.SaveAndReimport();
                }
                var material=new Material(Shader.Find("Little Castle/Surface/LC Stylized Lit")){name="RiverCliff "+keys[i],enableInstancing=true};
                material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/BaseColor.png"));material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Normal.png"));material.SetFloat("_BumpScale",.35f);material.SetFloat("_Roughness",.85f);
                AssetDatabase.CreateAsset(material,folder+"/StaticCliff.mat");
                var root=new GameObject("RiverCliff "+keys[i]);var lods=new LOD[3];Bounds bounds=default;
                for(int lod=0;lod<3;lod++)
                {
                    string modelPath=folder+"/LOD"+lod+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=true;importer.SaveAndReimport();
                    var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);var child=Object.Instantiate(model,root.transform);child.name="LOD"+lod;
                    var renderers=child.GetComponentsInChildren<MeshRenderer>();var raw=renderers[0].bounds;foreach(var r in renderers)raw.Encapsulate(r.bounds);
                    report.AppendLine(keys[i]+" LOD"+lod+" imported bounds="+raw+" pivot="+child.transform.position+" rotation="+child.transform.eulerAngles);
                    // All actual sources are Z-up in Blender; Unity imports Y-up and longitudinal +Z.
                    float scale=6/raw.size.z;child.transform.localScale*=scale;child.transform.localPosition=new Vector3(-raw.center.x*scale,-raw.min.y*scale,-raw.center.z*scale);
                    var normalized=renderers[0].bounds;foreach(var r in renderers){r.sharedMaterial=material;r.lightProbeUsage=LightProbeUsage.Off;normalized.Encapsulate(r.bounds);if(lod==2){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}}
                    if(lod==0)bounds=normalized;
                    foreach(var filter in child.GetComponentsInChildren<MeshFilter>()){var mesh=filter.sharedMesh;report.AppendLine($"  {mesh.name}: vertices={mesh.vertexCount}, triangles={mesh.GetIndexCount(0)/3}, normals={mesh.normals.Length}, uv={mesh.uv.Length}, tangents={mesh.tangents.Length}");}
                    lods[lod]=new LOD(new[]{.12f,.045f,.009f}[lod],renderers);
                }
                var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
                string prefabPath=folder+"/RiverCliff.prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);Object.DestroyImmediate(root);
                catalog.entries[i]=new MapRockCatalog.Entry{title=titles[i],sourcePath=sources[i],prefab=prefab,bounds=bounds,definition=new MapRockDefinition{id="river.cliff."+keys[i],height=bounds.size.y,length=bounds.size.z,thickness=bounds.size.x,embed=.22f,overlap=.85f,maxTurn=25,minimumScale=.94f,maximumScale=1.06f}};
                report.AppendLine("Normalized "+keys[i]+": "+bounds+"; closed irregular module, no planar sockets. Static baked plants. No model wind.");
            }
            catalog.waterMaterial=new Material(Shader.Find("Little Castle/Rivers/Flow Water")){name="River Flow Water"};AssetDatabase.CreateAsset(catalog.waterMaterial,Root+"/RiverWater.mat");
            catalog.bankMaterial=new Material(Shader.Find("Little Castle/Rivers/Bank Closure")){name="River Bank Closure"};AssetDatabase.CreateAsset(catalog.bankMaterial,Root+"/RiverBank.mat");
            AssetDatabase.CreateAsset(catalog,Root+"/MeadowRockCatalog.asset");AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../Research/MapRivers_20261026");File.WriteAllText("../Research/MapRivers_20261026/unity-cliff-audit.txt",report.ToString());return catalog;
        }
        public static void PrepareBatch(){Ensure();EditorApplication.Exit(0);}
    }
}
