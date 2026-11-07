using System;
using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Owned assets for one empty authored map. No scene/template or material is copied.</summary>
    public sealed class MapNewSceneAssets:IDisposable
    {
        public SurfacePaintData source;
        public Mesh groundMesh;
        public Material terrain,grass,accent,brush,sky;
        public WorldTimeSettings timeSettings;
        public void Dispose()
        {
            foreach(var value in new Object[]{source,groundMesh,terrain,grass,accent,brush,sky,timeSettings})
                if(value&&!EditorUtility.IsPersistent(value))Object.DestroyImmediate(value);
        }
    }

    public static class MapNewSceneFactory
    {
        public static MapNewSceneAssets CreateAssets(MapCreationOptions options)
        {
            var document=MapCreationFactory.Create(options);var assets=new MapNewSceneAssets();
            try
            {
                var data=assets.source=ScriptableObject.CreateInstance<SurfacePaintData>();data.name="Map source";
                data.mapId=document.mapId;data.revision=document.revision;data.bounds=document.bounds;
                data.hasHeightBase=data.hasTreeBase=data.hasEnvironmentBase=true;
                data.heightField=document.heightField;data.authored=document.surface;data.environment=document.environment;
                data.rivers=document.rivers;data.riverNetwork=document.riverNetwork;data.rockSections=document.rockSections;
                data.treeInstances=document.trees;data.fences=document.fences;data.placementExclusions=document.placementExclusions;
                assets.groundMesh=CreateGroundMesh(document);
                assets.terrain=Material("Little Castle/Terrain/LC Terrain","Map ground");
                assets.terrain.SetColor("_GrassColor",new Color(.34f,.46f,.23f));
                assets.terrain.SetColor("_DirtColor",new Color(.48f,.34f,.20f));
                assets.terrain.SetFloat("_UseSurfacePaint",1);assets.terrain.EnableKeyword("_LC_SURFACE_PAINT");
                assets.terrain.SetFloat("_GroundPalette",1);assets.terrain.SetFloat("_LightResponse",.55f);
                assets.grass=Material("Little Castle/Foliage/LC Grass","Map grass");
                assets.accent=Material("Little Castle/Foliage/LC Grass","Map accents");
                foreach(var material in new[]{assets.grass,assets.accent})
                {material.SetFloat("_UseSurfacePaint",1);material.EnableKeyword("_LC_SURFACE_PAINT");}
                assets.brush=Material("Universal Render Pipeline/Unlit","Map brush");
                assets.brush.SetColor("_BaseColor",new Color(1,.9f,.4f));
                assets.sky=Material("Little Castle/Sky/Stylized Day Night","Map sky");
                assets.timeSettings=ScriptableObject.CreateInstance<WorldTimeSettings>();assets.timeSettings.name="Map time";
                return assets;
            }
            catch{assets.Dispose();throw;}
        }
        static Material Material(string shaderName,string name)
        {
            var shader=Shader.Find(shaderName);if(!shader)throw new InvalidOperationException("Не найден shader: "+shaderName);
            return new Material(shader){name=name,enableInstancing=true};
        }
        public static Mesh CreateGroundMesh(MapAuthoringDocument document)
        {
            var b=document.bounds;int width=Mathf.RoundToInt(b.z),depth=Mathf.RoundToInt(b.w),columns=width+1;
            if(document.heightField==null||document.heightField.spacing!=1||width<1||depth<1||width>512||depth>512||b.z!=width||b.w!=depth)
                throw new ArgumentException("Новая сцена требует конечную целочисленную сетку 1 м до 512 м.");
            int count=columns*(depth+1);var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];
            for(int z=0;z<=depth;z++)for(int x=0;x<=width;x++)
            {int i=z*columns+x;vertices[i]=new Vector3(b.x+x,document.heightField.baseHeight,b.y+z);normals[i]=Vector3.up;uv[i]=new Vector2(x/(float)width,z/(float)depth);}
            var triangles=new int[width*depth*6];int t=0;
            for(int z=0;z<depth;z++)for(int x=0;x<width;x++)
            {int a=z*columns+x;triangles[t++]=a;triangles[t++]=a+columns;triangles[t++]=a+1;triangles[t++]=a+1;triangles[t++]=a+columns;triangles[t++]=a+columns+1;}
            var mesh=new Mesh{name="Map ground lattice",indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
        static GameObject Child(GameObject parent,string name)
        {var child=new GameObject(name);child.transform.SetParent(parent.transform,false);return child;}
        static void Reference(Object target,string field,Object value)
        {var serialized=new SerializedObject(target);serialized.FindProperty(field).objectReferenceValue=value;serialized.ApplyModifiedPropertiesWithoutUndo();}
        /// <summary>Tests use activate=false: no scene replacement, file writes, clock tick or global lighting changes.</summary>
        public static GameObject CreateObjects(MapNewSceneAssets assets,bool activate=true)
        {
            if(assets==null||!assets.source||!assets.groundMesh||!assets.terrain||!assets.brush||!assets.timeSettings)
                throw new ArgumentException("Неполный набор ресурсов новой карты.");
            var root=new GameObject("Authored map "+assets.source.mapId);root.SetActive(false);
            try
            {
                var ground=Child(root,"Map ground");ground.AddComponent<MeshFilter>().sharedMesh=assets.groundMesh;
                ground.AddComponent<MeshRenderer>().sharedMaterial=assets.terrain;ground.AddComponent<MeshCollider>().sharedMesh=assets.groundMesh;
                var camera=Child(root,"Map camera").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=50;camera.nearClipPlane=.1f;
                float size=Mathf.Max(assets.source.bounds.z,assets.source.bounds.w);camera.farClipPlane=Mathf.Max(300,size*3);camera.allowHDR=true;camera.clearFlags=CameraClearFlags.Skybox;
                var target=new Vector3(0,assets.source.heightField.baseHeight,0);camera.transform.position=target+new Vector3(0,size*.6f,-size*.6f);camera.transform.LookAt(target);
                var cameraData=camera.GetUniversalAdditionalCameraData();cameraData.requiresDepthTexture=true;cameraData.requiresColorTexture=true;
                camera.gameObject.AddComponent<AudioListener>();camera.gameObject.AddComponent<StrategyCameraController>().ConfigureAuthoringRange(size*2);
                var sun=Child(root,"Map sun").AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.96f,.84f);sun.intensity=1.1f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(45,-30,0);
                var moon=Child(root,"Map moon").AddComponent<Light>();moon.type=LightType.Directional;moon.color=new Color(.58f,.68f,1);moon.intensity=0;moon.shadows=LightShadows.None;
                var clock=root.AddComponent<WorldTimeSystem>();Reference(clock,"timeSettingsOverride",assets.timeSettings);
                var cycle=root.AddComponent<DayNightLightingController>();Reference(cycle,"timeSystem",clock);Reference(cycle,"sun",sun);Reference(cycle,"moon",moon);Reference(cycle,"skyboxMaterial",assets.sky);
                var globals=root.AddComponent<StylizedLightingGlobals>();Reference(globals,"timeSystem",clock);Reference(globals,"atmosphere",cycle);Reference(globals,"sun",sun);Reference(globals,"moon",moon);
                var painter=root.AddComponent<MeadowSurfacePainter>();painter.source=assets.source;painter.studyCamera=camera;painter.terrainMaterial=assets.terrain;
                painter.grassMaterial=assets.grass;painter.accentMaterial=assets.accent;painter.brushMaterial=assets.brush;painter.allowedTools=MapEditAccess.All;
                painter.treeCatalog=MapTreeCatalog.Default;painter.riverCatalogOverride=MapRockCatalog.Default;painter.waterOnlyRivers=true;
                painter.diagnosticSaveId="map-"+assets.source.mapId;
                var ui=camera.gameObject.AddComponent<MeadowPreview>();ui.worldEditorMode=true;ui.studyCamera=camera;ui.sun=sun;ui.globals=globals;ui.painter=painter;
                var library=AssetDatabase.LoadAssetAtPath<MeadowScatterLibrary>(MeadowScatterAssets.LibraryPath);
                if(library){var scatter=Child(root,"Painted map scatter").AddComponent<MeadowScatterRenderer>();scatter.library=library;scatter.painter=painter;painter.scatterRenderer=scatter;}
                if(activate)
                {
                    root.SetActive(true);RenderSettings.sun=sun;RenderSettings.skybox=assets.sky;RenderSettings.ambientMode=AmbientMode.Trilight;
                    clock.TryInitialize();cycle.ApplyLighting();globals.ApplyGlobals();
                }
                return root;
            }
            catch{Object.DestroyImmediate(root);throw;}
        }
    }
}
