using System;
using System.IO;
using System.Collections.Generic;
using LittleCastle.Rendering;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Explicit local library build. No scene creation, map regeneration or global material mutation.</summary>
    public static class PathSurfaceLibraryBuilder
    {
        const string Root="Assets/_Game/Art/PathSurfaces";
        const string CatalogPath="Assets/_Game/Resources/MapAuthoring/PathCatalog.asset";
        const int Size=512;
        static readonly string[] Names={"Грунтовая · исходная","Мшистый камень","Тёплая галька","Лесной камень","Старая мостовая","Мелкая брусчатка","Каменные ряды","Крупная брусчатка","Грунт 2 · с зеленью","Грунт 5 · плиты"};
        [MenuItem("Little Castle/Map/Paths/Rebuild material library")]
        public static void Build()
        {
            Directory.CreateDirectory(Root+"/Arrays");Directory.CreateDirectory(Root+"/Thumbnails");
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));AssetDatabase.Refresh();
            var catalog=AssetDatabase.LoadAssetAtPath<MapPathCatalog>(CatalogPath);
            if(!catalog){catalog=ScriptableObject.CreateInstance<MapPathCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
            EnsureInitialEntries(catalog);
            if(catalog.entries.Length>MapPathCatalog.MaximumMaterials)throw new InvalidOperationException("Path catalog supports up to 32 entries.");
            var ids=new HashSet<string>();foreach(var e in catalog.entries)if(e==null||string.IsNullOrWhiteSpace(e.id)||!ids.Add(e.id))throw new InvalidOperationException("Every path needs a unique stable ID.");
            var colors=new Texture2DArray(Size,Size,catalog.Count,TextureFormat.RGBA32,true,false){name="Path albedo",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var normals=new Texture2DArray(Size,Size,catalog.Count,TextureFormat.RGBA32,true,true){name="Path normals RGB",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var surfaces=new Texture2DArray(Size,Size,catalog.Count,TextureFormat.RGBA32,true,true){name="Path AO Height Smoothness",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            for(int index=0;index<catalog.Count;index++)
            {
                var entry=catalog.entries[index];
                Color[] albedo,normal,ao,height,smooth;
                if(index==0)
                {
                    albedo=Fill(new Color(.55f,.4f,.25f));normal=Fill(new Color(.5f,.5f,1));ao=Fill(Color.white);height=Fill(Color.gray);smooth=Fill(Color.black);
                }
                else
                {
                    if(!entry.baseColor)throw new InvalidOperationException("Assign Base Color for "+entry.id);
                    albedo=Read(AssetDatabase.GetAssetPath(entry.baseColor));normal=entry.normalMap?Read(AssetDatabase.GetAssetPath(entry.normalMap)):Fill(new Color(.5f,.5f,1));
                    ao=entry.ambientOcclusion?Read(AssetDatabase.GetAssetPath(entry.ambientOcclusion)):Fill(Color.white);
                    height=entry.height?Read(AssetDatabase.GetAssetPath(entry.height)):Fill(Color.gray);smooth=entry.smoothness?Read(AssetDatabase.GetAssetPath(entry.smoothness)):Fill(Color.black);
                    ConfigureImporter(AssetDatabase.GetAssetPath(entry.baseColor),"diffuseOriginal");
                    if(entry.normalMap)ConfigureImporter(AssetDatabase.GetAssetPath(entry.normalMap),"normal");
                    foreach(var data in new[]{entry.ambientOcclusion,entry.height,entry.smoothness})if(data)ConfigureImporter(AssetDatabase.GetAssetPath(data),"data");
                }
                var packed=new Color[Size*Size];for(int p=0;p<packed.Length;p++)packed[p]=new Color(ao[p].r,height[p].r,smooth[p].r,1);
                colors.SetPixels(albedo,index);normals.SetPixels(normal,index);surfaces.SetPixels(packed,index);
                var thumb=new Texture2D(Size,Size,TextureFormat.RGB24,false);thumb.SetPixels(albedo);thumb.Apply();
                string thumbnail=Root+"/Thumbnails/"+index+".png";File.WriteAllBytes(thumbnail,thumb.EncodeToPNG());Object.DestroyImmediate(thumb);
                AssetDatabase.ImportAsset(thumbnail);ConfigureImporter(thumbnail,"thumbnail");entry.thumbnail=AssetDatabase.LoadAssetAtPath<Texture2D>(thumbnail);
            }
            colors.Apply(true,false);normals.Apply(true,false);surfaces.Apply(true,false);
            catalog.albedo=SaveArray(colors,"Albedo");catalog.normal=SaveArray(normals,"Normal");catalog.surface=SaveArray(surfaces,"Surface");
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            Debug.Log("PATH_LIBRARY: "+catalog.Count+" entries. Source maps preserved; no scene changed.");
        }
        static void EnsureInitialEntries(MapPathCatalog catalog)
        {
            // Seed once. Subsequent builds preserve artist additions, IDs, order and tuning.
            if(catalog.entries.Length>0)
            {
                if(catalog.entries[0].id!=MapPathCatalog.LegacyId)throw new InvalidOperationException("Keep the original dirt path in slot zero.");
                // Upgrade the first imported library with source references; artist entries stay intact.
                foreach(var e in catalog.entries)
                {
                    if(e.baseColor||string.IsNullOrEmpty(e.id))continue;
                    string stem=e.id.StartsWith("cobble.",StringComparison.Ordinal)?"Cobble"+e.id.Substring(7):e.id=="ground.2"?"Ground2":e.id=="ground.5"?"Ground5":null;
                    if(stem!=null)AssignSources(e,stem);
                }
                return;
            }
            var entries=new List<MapPathCatalog.Entry>();
            for(int i=0;i<10;i++)
            {
                var e=new MapPathCatalog.Entry{id=i==0?MapPathCatalog.LegacyId:i<=7?"cobble."+i:"ground."+(i==8?2:5),title=Names[i],
                    worldTiling=i==9?.37f:i==8?.44f:.55f,normalStrength=.58f,heightBlend=.65f,aoStrength=.38f,snowRetention=.72f};
                if(i>0)
                {
                    string stem=i<=7?"Cobble"+i:"Ground"+(i==8?2:5);AssignSources(e,stem);
                }
                entries.Add(e);
            }
            catalog.entries=entries.ToArray();
        }
        static void AssignSources(MapPathCatalog.Entry e,string stem)
        {
            string path=Root+"/Sources/"+stem+"/"+stem;
            e.baseColor=AssetDatabase.LoadAssetAtPath<Texture2D>(path+"_diffuseOriginal.png");e.normalMap=AssetDatabase.LoadAssetAtPath<Texture2D>(path+"_normal.png");
            e.ambientOcclusion=AssetDatabase.LoadAssetAtPath<Texture2D>(path+"_ao.png");e.height=AssetDatabase.LoadAssetAtPath<Texture2D>(path+"_height.png");e.smoothness=AssetDatabase.LoadAssetAtPath<Texture2D>(path+"_smoothness.png");
        }
        static Texture2DArray SaveArray(Texture2DArray array,string name)
        {
            string path=Root+"/Arrays/Path"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if(existing){EditorUtility.CopySerialized(array,existing);Object.DestroyImmediate(array);EditorUtility.SetDirty(existing);return existing;}
            AssetDatabase.CreateAsset(array,path);return array;
        }
        static Color[] Fill(Color color){var values=new Color[Size*Size];for(int i=0;i<values.Length;i++)values[i]=color;return values;}
        static Color[] Read(string path)
        {
            // Read original PNG bytes, independent of platform normal-map swizzling/compression.
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false,true);source.LoadImage(File.ReadAllBytes(path));
            var values=new Color[Size*Size];for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)values[y*Size+x]=source.GetPixelBilinear((x+.5f)/Size,(y+.5f)/Size);
            Object.DestroyImmediate(source);return values;
        }
        static void ConfigureImporter(string path,string suffix)
        {
            if(!(AssetImporter.GetAtPath(path) is TextureImporter importer))return;
            importer.textureType=suffix=="normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=suffix=="diffuseOriginal"||suffix=="thumbnail";importer.isReadable=false;
            importer.mipmapEnabled=suffix!="thumbnail";importer.wrapMode=suffix=="thumbnail"?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
            importer.maxTextureSize=suffix=="thumbnail"?128:1024;importer.anisoLevel=4;importer.SaveAndReimport();
        }
    }
}
