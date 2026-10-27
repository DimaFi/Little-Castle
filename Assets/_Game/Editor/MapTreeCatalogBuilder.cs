using System.IO;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public static class MapTreeCatalogBuilder
    {
        public const string Root="Assets/_Game/Resources/MapAuthoring";
        public const string CatalogPath=Root+"/MeadowTreeCatalog.asset";
        [MenuItem("Little Castle/Map/Prepare Oak Catalog")]
        public static void Prepare()=>Ensure();
        public static MapTreeCatalog Ensure()
        {
            var existing=AssetDatabase.LoadAssetAtPath<MapTreeCatalog>(CatalogPath);if(existing)return existing;
            Directory.CreateDirectory(Root+"/Thumbnails");AssetDatabase.Refresh();
            var catalog=ScriptableObject.CreateInstance<MapTreeCatalog>();catalog.entries=new MapTreeCatalog.Entry[4];
            var pack=new MapTreePack{id="oak.abcd",version=1,authorOnly=true};
            for(int i=0;i<4;i++)
            {
                char variant="ABCD"[i];string id="oak."+char.ToLowerInvariant(variant);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ValleyArtAssets.Root+"/Oak_"+variant+"_Study.prefab");
                if(!prefab)throw new FileNotFoundException("Approved Oak prefab missing: "+variant);
                var entry=new MapTreeCatalog.Entry{title="Дуб "+variant,prefab=prefab,definition=new MapTreeDefinition{archetypeId=id,version=1,minimumScale=.85f,maximumScale=1.2f,minimumSpacing=4,maxSlopeDegrees=35,playerPlaceable=true}};
                string thumbnail=Root+"/Thumbnails/Oak_"+variant+".png";entry.localBounds=MeasureBounds(prefab);
                if(File.Exists(thumbnail)){AssetDatabase.ImportAsset(thumbnail,ImportAssetOptions.ForceSynchronousImport);entry.thumbnail=AssetDatabase.LoadAssetAtPath<Texture2D>(thumbnail);}catalog.entries[i]=entry;
                pack.entries.Add(new MapTreePackEntry{archetypeId=id,weight=1});
            }
            catalog.packs=new[]{pack};AssetDatabase.CreateAsset(catalog,CatalogPath);AssetDatabase.SaveAssetIfDirty(catalog);return catalog;
        }
        [MenuItem("Little Castle/Map/Import Verified Oak Thumbnails")]
        public static void ImportPlayerPreviews()
        {
            var catalog=Ensure();string capture=Path.GetFullPath(Application.dataPath+"/../../Research/MapTrees_20261026/TreePreviews");
            for(int i=0;i<catalog.entries.Length;i++)
            {
                string name=MapTreeCatalog.ThumbnailName(catalog.entries[i]),target=Root+"/Thumbnails/"+name;File.Copy(Path.Combine(capture,name),target,true);
                AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(target);
                importer.mipmapEnabled=false;importer.maxTextureSize=256;importer.SaveAndReimport();catalog.entries[i].thumbnail=AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            }
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        }
        [MenuItem("Little Castle/Map/Refresh Tree Catalog Bounds")]
        public static void RefreshBounds()
        {
            var catalog=Ensure();foreach(var entry in catalog.entries)if(entry.prefab)entry.localBounds=MeasureBounds(entry.prefab);
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        }
        static Bounds MeasureBounds(GameObject prefab)
        {
            var root=Object.Instantiate(prefab);root.hideFlags=HideFlags.HideAndDontSave;
            try{root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;root.transform.localScale=Vector3.one;var renderers=root.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);return new Bounds(root.transform.InverseTransformPoint(bounds.center),bounds.size);}
            finally{Object.DestroyImmediate(root);}
        }
    }
}
