using System;
using System.Reflection;
using LittleCastle.Research;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class CurrentMapGameInspectionTests
    {
        const string Key="LC.CurrentMapGameInspection.";
        [Test]
        public void UnsupportedReloadOptionsAndMismatchedBoundsAreRejectedBeforePlay()
        {
            var type=Type.GetType("LittleCastle.Editor.CurrentMapGameInspection, LittleCastle.Editor",true);
            var validate=type.GetMethod("ValidatePlayConfiguration",BindingFlags.Public|BindingFlags.Static);
            var bounds=new Vector4(-32,-32,64,64);
            Assert.That(validate.Invoke(null,new object[]{false,EnterPlayModeOptions.DisableSceneReload,bounds,bounds}),Is.Null);
            Assert.That(validate.Invoke(null,new object[]{true,EnterPlayModeOptions.None,bounds,bounds}),Is.Null);
            Assert.That(validate.Invoke(null,new object[]{true,EnterPlayModeOptions.DisableDomainReload,bounds,bounds}),Is.Not.Null);
            Assert.That(validate.Invoke(null,new object[]{true,EnterPlayModeOptions.DisableSceneReload,bounds,bounds}),Is.Not.Null);
            Assert.That(validate.Invoke(null,new object[]{false,EnterPlayModeOptions.None,bounds,new Vector4(0,0,32,32)}),Is.Not.Null);
        }

        [Test]
        public void AbortedLiveInspectionKeepsIsolatedSaveUntilSceneTeardown()
        {
            var type=Type.GetType("LittleCastle.Editor.CurrentMapGameInspection, LittleCastle.Editor",true);
            var flags=BindingFlags.NonPublic|BindingFlags.Static;
            var target=type.GetField("targetPainter",flags);var originalId=type.GetField("originalDiagnosticSaveId",flags);
            var root=new GameObject("Reload save isolation test");var painter=root.AddComponent<MeadowSurfacePainter>();
            bool armed=SessionState.GetBool(Key+"armed",false),consumed=SessionState.GetBool(Key+"consumed",false);
            string document=SessionState.GetString(Key+"document","");
            try
            {
                painter.diagnosticSaveId="current-map-game-inspection";
                target.SetValue(null,painter);originalId.SetValue(null,"");
                SessionState.SetBool(Key+"armed",true);SessionState.SetBool(Key+"consumed",true);
                type.GetMethod("ReleaseInspection",flags).Invoke(null,new object[]{false});
                Assert.That(painter.diagnosticSaveId,Is.EqualTo("current-map-game-inspection"),
                    "An injected Session still alive while exiting/reloading must never regain the ordinary map-save path.");
                Assert.That(SessionState.GetBool(Key+"armed",true),Is.False);
                Assert.That(SessionState.GetBool(Key+"consumed",true),Is.False);
                Assert.That(target.GetValue(null),Is.Null,"Ownership is released before managed references are lost.");
            }
            finally
            {
                target.SetValue(null,null);originalId.SetValue(null,null);
                UnityEngine.Object.DestroyImmediate(root);
                SessionState.SetBool(Key+"armed",armed);SessionState.SetBool(Key+"consumed",consumed);SessionState.SetString(Key+"document",document);
            }
        }

        [Test]
        public void InspectionInjectsExactCurrentDocumentOnceWithoutChangingSource()
        {
            var type=Type.GetType("LittleCastle.Editor.CurrentMapGameInspection, LittleCastle.Editor",true);
            var source=ScriptableObject.CreateInstance<SurfacePaintData>();source.mapId="inspection-source";
            source.bounds=new Vector4(-32,-32,64,64);
            string path=AssetDatabase.GenerateUniqueAssetPath("Assets/CurrentMapInspectionTest.asset");
            AssetDatabase.CreateAsset(source,path);
            var root=new GameObject("Current map injection test");
            var painter=root.AddComponent<MeadowSurfacePainter>();painter.source=source;painter.diagnosticSaveId="previous-diagnostic";
            string before=EditorJsonUtility.ToJson(source);
            var document=new MapAuthoringDocument{mapId="CURRENT unpublished document",bounds=source.bounds};
            string json=JsonUtility.ToJson(document);
            var provide=type.GetMethod("ProvideDocument",BindingFlags.NonPublic|BindingFlags.Static);
            // Preserve any explicitly armed author workflow if this test is called manually.
            bool armed=SessionState.GetBool(Key+"armed",false),consumed=SessionState.GetBool(Key+"consumed",false);
            string oldDocument=SessionState.GetString(Key+"document",""),oldSource=SessionState.GetString(Key+"source",""),oldStatus=SessionState.GetString(Key+"status","");
            try
            {
                SessionState.SetBool(Key+"armed",false);
                Assert.That(provide.Invoke(null,new object[]{painter}),Is.Null,"No opt-in means normal scene source.");
                SessionState.SetBool(Key+"armed",true);SessionState.SetBool(Key+"consumed",false);
                SessionState.SetString(Key+"document",json);SessionState.SetString(Key+"source","wrong-guid");
                Assert.That(provide.Invoke(null,new object[]{painter}),Is.Null,"Do not replace an unrelated map.");
                SessionState.SetString(Key+"source",AssetDatabase.AssetPathToGUID(path));
                var reload=type.GetMethod("BeforeAssemblyReload",BindingFlags.NonPublic|BindingFlags.Static);
                reload.Invoke(null,null);
                Assert.That(SessionState.GetBool(Key+"armed",false),Is.True,"Normal entering-Play reload must preserve an unconsumed request.");
                var actual=(MapAuthoringDocument)provide.Invoke(null,new object[]{painter});
                Assert.That(JsonUtility.ToJson(actual),Is.EqualTo(json),"Use the whole current document, not a source-based delta save.");
                Assert.That(provide.Invoke(null,new object[]{painter}),Is.Null,"Never reuse a snapshot on a later scene load.");
                Assert.That(EditorJsonUtility.ToJson(source),Is.EqualTo(before));
                Assert.That(painter.diagnosticSaveId,Is.EqualTo("current-map-game-inspection"));
                reload.Invoke(null,null);
                Assert.That(SessionState.GetBool(Key+"armed",true),Is.False,"Mid-inspection reload must disarm before losing references.");
                Assert.That(SessionState.GetBool(Key+"consumed",true),Is.False);
                Assert.That(painter.diagnosticSaveId,Is.EqualTo("previous-diagnostic"));
                Assert.That(SessionState.GetString(Key+"status",""),Does.Contain("заново"));
                Assert.That(type.GetField("targetPainter",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null),Is.Null);
            }
            finally
            {
                type.GetMethod("Cleanup",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
                SessionState.SetBool(Key+"armed",armed);SessionState.SetBool(Key+"consumed",consumed);
                SessionState.SetString(Key+"document",oldDocument);SessionState.SetString(Key+"source",oldSource);SessionState.SetString(Key+"status",oldStatus);
                UnityEngine.Object.DestroyImmediate(root);AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
