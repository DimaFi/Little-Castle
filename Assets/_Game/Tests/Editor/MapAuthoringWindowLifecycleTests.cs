using System;
using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapAuthoringWindowLifecycleTests
    {
        const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static Type WindowType=>Type.GetType("LittleCastle.Editor.MapAuthoringWindow, LittleCastle.Editor",true);
        static object Get(EditorWindow window,string name)=>WindowType.GetField(name,Members).GetValue(window);
        static void Invoke(EditorWindow window,string name,params object[] arguments)=>WindowType.GetMethod(name,Members).Invoke(window,arguments);
        static SurfacePaintData Source()
        {
            var data=ScriptableObject.CreateInstance<SurfacePaintData>();data.mapId="editor-lifecycle";
            data.bounds=new Vector4(-32,-32,64,64);data.hasHeightBase=true;data.heightField=new MapHeightFieldData();return data;
        }
        static void Destroy(EditorWindow window)
        {
            if(!window)return;typeof(EditorWindow).GetProperty("hasUnsavedChanges",Members).GetSetMethod(true).Invoke(window,new object[]{false});UnityEngine.Object.DestroyImmediate(window);
        }

        [Test] public void SerializedWindowCheckpoint_RestoresHeightToolSettingsAndUnpublishedRiver()
        {
            var source=Source();string sourcePath=AssetDatabase.GenerateUniqueAssetPath("Assets/WorldEditorLifecycleTest.asset");
            AssetDatabase.CreateAsset(source,sourcePath);AssetDatabase.SaveAssetIfDirty(source);
            var original=(EditorWindow)ScriptableObject.CreateInstance(WindowType);EditorWindow restored=null;
            try
            {
                Invoke(original,"Connect",source);Invoke(original,"SetTool","terrain.raise");
                Invoke(original,"VerifySettings",5f,.8f,.6f);Invoke(original,"ConfigureTools");
                var session=(MapEditSession)Get(original,"session");var tools=(MapToolController)Get(original,"tools");
                tools.Begin(new MapToolPointer(default,true,Vector2.zero),0);
                Assert.That(session.HasPending,Is.True);
                var river=(MapRiverToolState)Get(original,"riverTools");river.StartRiver();
                river.Click(session,new Vector2(-10,-10));river.Click(session,new Vector2(-10,10));long id=river.draft.id;
                Invoke(original,"CheckpointSession");float height=session.SampleHeight(0,0);
                Assert.That(session.HasPending,Is.False);Assert.That(height,Is.GreaterThan(0));
                string serialized=EditorJsonUtility.ToJson(original);
                restored=(EditorWindow)ScriptableObject.CreateInstance(WindowType);EditorJsonUtility.FromJsonOverwrite(serialized,restored);
                Assert.That(Get(restored,"source"),Is.SameAs(source),"An Editor reload restores a persistent authoring asset reference.");
                Assert.That(Get(restored,"sessionCheckpoint"),Is.Not.Null.And.Not.Empty,"The completed stroke must survive window serialization.");
                Invoke(restored,"RestoreWindowSession");
                var restoredSession=(MapEditSession)Get(restored,"session");var restoredRiver=(MapRiverToolState)Get(restored,"riverTools");
                Assert.That(restoredSession,Is.Not.Null);Assert.That(restoredSession.SampleHeight(0,0),Is.EqualTo(height).Within(.00001f));
                Assert.That(Get(restored,"activeToolId"),Is.EqualTo("terrain.raise"));Assert.That(Get(restored,"radius"),Is.EqualTo(5f));
                Assert.That(Get(restored,"softness"),Is.EqualTo(.6f));Assert.That(Get(restored,"preview"),Is.Not.Null);
                Assert.That(restoredRiver.draft.id,Is.EqualTo(id));Assert.That(restoredRiver.draft.nodes.Count,Is.EqualTo(2));
                Assert.That(restoredSession.RiverCount,Is.Zero);Assert.That(restoredRiver.IsCreatingRiver,Is.True);
                Assert.That(restored.hasUnsavedChanges,Is.True);
            }
            finally{Destroy(restored);Destroy(original);AssetDatabase.DeleteAsset(sourcePath);}
        }

        [Test] public void FlowerToolWithoutTreeCatalog_AuthorsScatterAndSupportsUndoAndJson()
        {
            var source=Source();var window=(EditorWindow)ScriptableObject.CreateInstance(WindowType);
            try
            {
                Invoke(window,"Connect",source);
                WindowType.GetField("treeCatalog",Members).SetValue(window,null);
                Invoke(window,"InitializeTools");
                WindowType.GetField("scatter",Members).SetValue(window,false);
                Invoke(window,"SetTool","vegetation.flowers");Invoke(window,"ConfigureTools");
                Assert.That(Get(window,"toolCategory"),Is.EqualTo(2));
                var session=(MapEditSession)Get(window,"session");var tools=(MapToolController)Get(window,"tools");
                var originalBase=session.ExportMap();
                Assert.That(tools.Selected,Is.InstanceOf<MapSurfaceTool>());
                tools.Begin(new MapToolPointer(default,true,Vector2.zero),0);tools.Release();
                var stroke=session.ExportEdits().strokes[0];
                Assert.That(stroke.scatter,Is.True);Assert.That(stroke.scatterProfile,Is.EqualTo(MeadowScatterProfile.Flowers));
                Assert.That(stroke.road,Is.False);Assert.That(stroke.erase,Is.False);
                Assert.That(source.authored.strokes,Is.Empty,"Choosing and painting flowers must not publish the source asset.");
                var loaded=new MapEditSession(originalBase);loaded.LoadJson(session.ExportJson());
                Assert.That(loaded.ExportEdits().strokes[0].scatterProfile,Is.EqualTo(MeadowScatterProfile.Flowers));
                tools.Undo();Assert.That(session.ExportEdits().strokes,Is.Empty);
                tools.Redo();Assert.That(session.ExportEdits().strokes.Count,Is.EqualTo(1));
            }
            finally{Destroy(window);UnityEngine.Object.DestroyImmediate(source);}
        }

        [Test] public void SwitchingCategoryStopsRiverInputAndReturningResumesSameDraft()
        {
            var source=Source();var window=(EditorWindow)ScriptableObject.CreateInstance(WindowType);
            try
            {
                Invoke(window,"Connect",source);var river=(MapRiverToolState)Get(window,"riverTools");
                river.StartRiver();Invoke(window,"SetTool","river.points");long id=river.draft.id;
                Invoke(window,"SetTool","surface.road");
                Assert.That(Get(window,"activeToolId"),Is.EqualTo("surface.road"));Assert.That(river.mode,Is.Zero);
                Invoke(window,"ConfigureTools");Assert.That(((MapToolController)Get(window,"tools")).Selected.Id,Is.EqualTo("surface.road"));
                Invoke(window,"SetTool","river.points");Assert.That(river.mode,Is.EqualTo(1));Assert.That(river.draft.id,Is.EqualTo(id));
            }
            finally{Destroy(window);UnityEngine.Object.DestroyImmediate(source);}
        }
        [Test] public void SceneViewFlowerPreview_RendersSessionWithoutRuntimePainterAndRestoresTemplate()
        {
            var source=Source();var window=(EditorWindow)ScriptableObject.CreateInstance(WindowType);
            var root=new GameObject("Isolated flower preview source");var library=ScriptableObject.CreateInstance<MeadowScatterLibrary>();
            var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},triangles=new[]{0,1,2}};mesh.RecalculateBounds();
            var material=new Material(Shader.Find("Unlit/Color")){enableInstancing=true};
            library.prototypes=new[]{new MeadowScatterLibrary.Prototype{stableId="preview-flower",category=MeadowScatterCategory.Flower,
                lodMeshes=new[]{mesh,mesh,mesh,mesh},materials=new[]{material},localBounds=mesh.bounds,scaleRange=Vector2.one,weight=1}};
            try
            {
                var painter=root.AddComponent<MeadowSurfacePainter>();painter.source=source;
                var template=root.AddComponent<MeadowScatterRenderer>();template.library=library;painter.scatterRenderer=template;
                Invoke(window,"Connect",source);Invoke(window,"RestoreScatterPreview");Invoke(window,"BindScatterPreview",painter);
                Assert.That(painter.Session,Is.Null,"SceneView does not start or replace the runtime session.");
                Assert.That(template.enabled,Is.False);
                Invoke(window,"SetTool","vegetation.flowers");Invoke(window,"VerifySettings",14f,1f,.38f);Invoke(window,"ConfigureTools");
                var tools=(MapToolController)Get(window,"tools");tools.Begin(new MapToolPointer(default,true,Vector2.zero),0);tools.Complete();
                var preview=(MeadowScatterRenderer)Get(window,"scatterPreview");Assert.That(preview.authoredInstances,Is.GreaterThan(0));
                ulong digest=preview.placementDigest;tools.Undo();Assert.That(preview.authoredInstances,Is.Zero);
                tools.Redo();Assert.That(preview.placementDigest,Is.EqualTo(digest));
                Invoke(window,"RestoreScatterPreview");Assert.That(template.enabled,Is.True);Assert.That(source.authored.strokes,Is.Empty);
            }
            finally{Destroy(window);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(library);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);}
        }

        [Test] public void EditModeReturnWithSurvivingSession_RecreatesPreviewAndKeepsUndo()
        {
            var source=Source();var window=(EditorWindow)ScriptableObject.CreateInstance(WindowType);
            try
            {
                Invoke(window,"Connect",source);var session=(MapEditSession)Get(window,"session");
                session.BeginHeightStroke(4,1);session.AppendHeightPoint(Vector2.zero);session.CommitStroke();
                Invoke(window,"PlayState",PlayModeStateChange.ExitingEditMode);
                UnityEngine.Object.DestroyImmediate((Texture2D)Get(window,"preview"));
                Invoke(window,"PlayState",PlayModeStateChange.EnteredEditMode);
                Assert.That(Get(window,"session"),Is.SameAs(session));Assert.That(session.CanUndo,Is.True);
                Assert.That((Texture2D)Get(window,"preview"),Is.Not.Null);session.Undo();Assert.That(session.SampleHeight(0,0),Is.Zero);
            }
            finally{Destroy(window);UnityEngine.Object.DestroyImmediate(source);}
        }
    }
}
