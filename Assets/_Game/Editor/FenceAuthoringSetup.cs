using System;
using System.IO;
using LittleCastle.Rendering;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>Imports only the reviewed fence package; never rebuilds or saves the user's map.</summary>
    public static class FenceAuthoringSetup
    {
        public const string Art="Assets/_Game/Art/Imported/Fence_Wood_A/v002";
        public const string CatalogPath="Assets/_Game/Resources/MapAuthoring/FenceCatalog.asset";
        [InitializeOnLoadMethod] static void Schedule()
        {
            EditorApplication.delayCall+=()=>
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Art+"/Exports/Wood01/SM_FenceWood01_Post_A_LOD0.fbx"))return;
                var installed=AssetDatabase.LoadAssetAtPath<MapFenceCatalog>(CatalogPath);if(installed&&installed.meshAdapterVersion==2)return;
                try{Ensure();}catch(Exception e){Debug.LogError("Fence setup: "+e.Message);}
            };
        }
        [MenuItem("Little Castle/World Editor/Prepare Selected Fences")]
        static void Prepare()=>Ensure();
        public static MapFenceCatalog Ensure()
        {
            var existing=AssetDatabase.LoadAssetAtPath<MapFenceCatalog>(CatalogPath);if(existing&&existing.meshAdapterVersion==2)return existing;
            Directory.CreateDirectory(Art+"/Meshes");Directory.CreateDirectory(Art+"/Materials");
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));AssetDatabase.Refresh();
            var post0=ReadMesh("Post",0);var rail0=ReadMesh("Rail",0);var post1=ReadMesh("Post",1);var rail1=ReadMesh("Rail",1);
            var shader=Shader.Find("Little Castle/Surface/LC Stylized Lit");if(!shader)throw new InvalidOperationException("Shared LC Stylized Lit shader missing.");
            var catalog=existing?existing:ScriptableObject.CreateInstance<MapFenceCatalog>();catalog.entries=new MapFenceCatalog.Entry[2];
            for(int i=0;i<2;i++)
            {
                string key="Wood0"+(i+1),path=Art+"/Textures/T_Fence"+key+"_A_BaseColor.png";
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)throw new FileNotFoundException(path);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;
                importer.alphaSource=TextureImporterAlphaSource.None;importer.maxTextureSize=1024;importer.wrapMode=TextureWrapMode.Repeat;importer.SaveAndReimport();
                var material=new Material(shader){name="M_Fence"+key+"_A",enableInstancing=true};
                material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(path));material.SetColor("_Color",Color.white);
                material.SetFloat("_Roughness",.78f);material.SetFloat("_SpecularStrength",.05f);
                string materialPath=Art+"/Materials/"+material.name+".mat";
                var saved=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(saved){UnityEngine.Object.DestroyImmediate(material);material=saved;}else AssetDatabase.CreateAsset(material,materialPath);
                catalog.entries[i]=new MapFenceCatalog.Entry{id="fence.wood0"+(i+1),title="Дерево №"+(i+1),material=material,
                    postLOD0=post0,railLOD0=rail0,postLOD1=post1,railLOD1=rail1};
            }
            catalog.meshAdapterVersion=2;if(!existing)AssetDatabase.CreateAsset(catalog,CatalogPath);else EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);return catalog;
        }
        static Mesh ReadMesh(string kind,int lod)
        {
            string path=Art+"/Exports/Wood01/SM_FenceWood01_"+kind+"_A_LOD"+lod+".fbx";
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;if(!importer)throw new FileNotFoundException(path);
            importer.isReadable=true;importer.importCameras=false;importer.importLights=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var filters=model.GetComponentsInChildren<MeshFilter>(true);
            if(filters.Length!=1)throw new InvalidDataException("Expected one fence mesh: "+path);
            var mesh=new Mesh{name="Fence_"+kind+"_LOD"+lod};
            // Unity's left-handed FBX import points this reviewed Blender rail along -X.
            // Rotate the complete imported part once; runtime geometry then has a true +X length axis.
            var importAxis=Matrix4x4.Rotate(Quaternion.Euler(0,180,0));
            mesh.CombineMeshes(new[]{new CombineInstance{mesh=filters[0].sharedMesh,transform=importAxis*filters[0].transform.localToWorldMatrix}},true,true);
            mesh.RecalculateBounds();var b=mesh.bounds;
            bool axes=kind=="Rail"?Mathf.Abs(b.min.x)<.002f&&Mathf.Abs(b.max.x-1.6f)<.002f&&Mathf.Abs(b.center.y)<.01f:
                Mathf.Abs(b.min.y+.1f)<.002f&&Mathf.Abs(b.max.y-1.1f)<.002f;
            if(!axes)throw new InvalidDataException("Fence axis/scale mismatch "+path+" bounds="+b);
            int triangles=mesh.triangles.Length/3,expected=kind=="Post"?(lod==0?84:28):(lod==0?108:20);
            if(triangles!=expected)throw new InvalidDataException("Fence triangle mismatch "+path+": "+triangles);
            string target=Art+"/Meshes/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(target);
            if(existing){EditorUtility.CopySerialized(mesh,existing);AssetDatabase.SaveAssetIfDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;}
            AssetDatabase.CreateAsset(mesh,target);return mesh;
        }
    }
}
