using System;
using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class MapNewDocumentTests
    {
        [Test] public void MapWeatherButtonConnectsCurrentPainterAndReusesOneController()
        {
            if(Object.FindObjectsByType<LittleCastle.World.Weather.RainStormController>(FindObjectsInactive.Include).Length!=0)
                Assert.Ignore("Connection fixture requires a scene without authored weather; user controllers must stay untouched.");
            var assets=Create(Options());GameObject root=null,weatherRoot=null;
            try
            {
                root=(GameObject)Factory.GetMethod("CreateObjects").Invoke(null,new object[]{assets,false});
                var painter=root.GetComponent<MeadowSurfacePainter>();
                var settings=Type.GetType("LittleCastle.Editor.MapSystemsWindow, LittleCastle.Editor",true);
                var connect=settings.GetMethod("EnsureWeather");
                var first=(Component)connect.Invoke(null,new object[]{painter});weatherRoot=first.gameObject;
                var second=connect.Invoke(null,new object[]{painter});
                Assert.That(second,Is.SameAs(first));
                var data=new SerializedObject(first);
                Assert.That(data.FindProperty("mapPainter").objectReferenceValue,Is.SameAs(painter));
                Assert.That(data.FindProperty("gameplayCamera").objectReferenceValue,Is.SameAs(painter.studyCamera));
                Assert.That(data.FindProperty("atmosphere").objectReferenceValue,Is.SameAs(root.GetComponent<DayNightLightingController>()));
                Assert.That(data.FindProperty("mode").enumValueIndex,Is.Zero,"Newly connected rain starts Off.");
                Assert.That(first.gameObject.scene,Is.EqualTo(painter.gameObject.scene));
            }
            finally{if(weatherRoot)Object.DestroyImmediate(weatherRoot);if(root)Object.DestroyImmediate(root);((IDisposable)assets).Dispose();}
        }
        static Type Factory=>Type.GetType("LittleCastle.Editor.MapNewSceneFactory, LittleCastle.Editor",true);
        static object Create(MapCreationOptions options)=>Factory.GetMethod("CreateAssets").Invoke(null,new object[]{options});
        static T Get<T>(object assets,string field)=> (T)assets.GetType().GetField(field).GetValue(assets);
        static MapCreationOptions Options()=>new MapCreationOptions{widthMetres=32,lengthMetres=48,baseHeight=2,mapId="new-scene-fixture"};

        [Test] public void FlatSceneSourceAndLatticeOwnTheSameMapBoundsAndHeight()
        {
            var assets=Create(Options());
            try
            {
                var source=Get<SurfacePaintData>(assets,"source");var mesh=Get<Mesh>(assets,"groundMesh");
                Assert.That(source.hasHeightBase&&source.hasTreeBase&&source.hasEnvironmentBase,Is.True);
                var document=MapEditSession.FromSurface(source);var session=new MapEditSession(document);
                Assert.That(source.bounds,Is.EqualTo(new Vector4(-16,-24,32,48)));Assert.That(session.TreeCount,Is.Zero);Assert.That(session.RiverCount,Is.Zero);
                Assert.That(source.heightField.tiles,Is.Empty,"The authoritative flat document remains sparse.");
                Assert.That(mesh.vertexCount,Is.EqualTo(33*49));Assert.That(mesh.vertices[0],Is.EqualTo(new Vector3(-16,2,-24)));
                Assert.That(mesh.vertices[mesh.vertexCount-1],Is.EqualTo(new Vector3(16,2,24)));
                Assert.That(mesh.triangles[0],Is.EqualTo(0));Assert.That(mesh.triangles[1],Is.EqualTo(33));Assert.That(mesh.triangles[2],Is.EqualTo(1));
                var vertices=mesh.vertices;var normals=mesh.normals;
                for(int i=0;i<vertices.Length;i++){var p=vertices[i];Assert.That(p.y,Is.EqualTo(session.SampleHeight(p.x,p.z)));Assert.That(normals[i],Is.EqualTo(Vector3.up));}
                foreach(string field in new[]{"terrain","grass","accent","brush","sky"})Assert.That(EditorUtility.IsPersistent(Get<Material>(assets,field)),Is.False,"No shared source material is reused.");
                foreach(string field in new[]{"terrain","grass","accent"})Assert.That(Get<Material>(assets,field).HasProperty("_UseSurfacePaint"),Is.True);
            }
            finally{((IDisposable)assets).Dispose();}
        }
        [Test] public void MaximumSceneLatticeKeepsOneMetreSpacingAndUses32BitIndices()
        {
            var document=MapCreationFactory.Create(new MapCreationOptions{widthMetres=512,lengthMetres=512});
            var mesh=(Mesh)Factory.GetMethod("CreateGroundMesh").Invoke(null,new object[]{document});
            try{Assert.That(mesh.vertexCount,Is.EqualTo(513*513));Assert.That(mesh.indexFormat,Is.EqualTo(IndexFormat.UInt32));Assert.That(mesh.vertices[1].x-mesh.vertices[0].x,Is.EqualTo(1));}
            finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void InactiveSceneObjectsWirePainterClockLightingAndOwnedAssetsWithoutReplacingScene()
        {
            var assets=Create(Options());GameObject root=null;var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();int sceneCount=UnityEngine.SceneManagement.SceneManager.sceneCount;
            var previousSun=RenderSettings.sun;var previousSky=RenderSettings.skybox;
            try
            {
                root=(GameObject)Factory.GetMethod("CreateObjects").Invoke(null,new object[]{assets,false});
                Assert.That(root.activeSelf,Is.False);Assert.That(UnityEngine.SceneManagement.SceneManager.sceneCount,Is.EqualTo(sceneCount));Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Is.EqualTo(scene));
                Assert.That(RenderSettings.sun,Is.SameAs(previousSun));Assert.That(RenderSettings.skybox,Is.SameAs(previousSky));
                var painter=root.GetComponent<MeadowSurfacePainter>();var source=Get<SurfacePaintData>(assets,"source");
                Assert.That(painter.source,Is.SameAs(source));Assert.That(painter.studyCamera,Is.Not.Null);Assert.That(painter.brushMaterial,Is.SameAs(Get<Material>(assets,"brush")));
                Assert.That(painter.terrainMaterial,Is.SameAs(Get<Material>(assets,"terrain")));Assert.That(painter.waterOnlyRivers,Is.True);
                Assert.That(painter.diagnosticSaveId,Is.EqualTo("map-"+source.mapId));Assert.That(painter.allowedTools,Is.EqualTo(MapEditAccess.All));
                var ui=painter.studyCamera.GetComponent<MeadowPreview>();Assert.That(ui.worldEditorMode,Is.True);Assert.That(ui.painter,Is.SameAs(painter));Assert.That(ui.globals,Is.Not.Null);Assert.That(new SerializedObject(ui).FindProperty("profile").objectReferenceValue,Is.Null,"The scene does not change project-wide pipeline quality.");
                var filter=root.GetComponentInChildren<MeshFilter>(true);Assert.That(filter.sharedMesh,Is.SameAs(Get<Mesh>(assets,"groundMesh")));Assert.That(filter.GetComponent<MeshRenderer>().sharedMaterial,Is.SameAs(painter.terrainMaterial));
                Assert.That(filter.GetComponent<MeshCollider>().sharedMesh,Is.SameAs(filter.sharedMesh));Assert.That(filter.transform.lossyScale,Is.EqualTo(Vector3.one));
                var clock=root.GetComponent<WorldTimeSystem>();var cycle=root.GetComponent<DayNightLightingController>();var globals=root.GetComponent<StylizedLightingGlobals>();
                Assert.That(new SerializedObject(clock).FindProperty("timeSettingsOverride").objectReferenceValue,Is.SameAs(Get<WorldTimeSettings>(assets,"timeSettings")));
                Assert.That(new SerializedObject(cycle).FindProperty("timeSystem").objectReferenceValue,Is.SameAs(clock));Assert.That(new SerializedObject(cycle).FindProperty("skyboxMaterial").objectReferenceValue,Is.SameAs(Get<Material>(assets,"sky")));
                Assert.That(new SerializedObject(globals).FindProperty("atmosphere").objectReferenceValue,Is.SameAs(cycle));
            }
            finally{if(root)Object.DestroyImmediate(root);((IDisposable)assets).Dispose();}
        }
    }
}
