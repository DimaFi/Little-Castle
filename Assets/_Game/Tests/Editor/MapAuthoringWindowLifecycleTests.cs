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
