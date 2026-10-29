using System;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder.Editor
{
    public sealed class MountainBorderPreviewWindow : EditorWindow
    {
        MountainPreviewSettings settings;
        MountainPreviewSettings sourceProfile;
        SerializedObject serialized;
        MountainBorderPreview preview;
        RenderTexture image;
        Vector2 scroll;
        bool closeup;
        string error;

        [MenuItem("Little Castle/Experimental/Mountain Border/Open Isolated Preview")]
        public static void Open()=>GetWindow<MountainBorderPreviewWindow>("Mountain preparation");

        void OnEnable()
        {
            settings=CreateInstance<MountainPreviewSettings>();settings.hideFlags=HideFlags.HideAndDontSave;
            serialized=new SerializedObject(settings);
            EditorApplication.playModeStateChanged+=PlayStateChanged;
        }
        void OnDisable()
        {
            EditorApplication.playModeStateChanged-=PlayStateChanged;
            Release();if(settings)DestroyImmediate(settings);serialized=null;
        }
        void Release()
        {preview?.Dispose();preview=null;if(image){image.Release();DestroyImmediate(image);image=null;}}
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Isolated Editor-only preparation. Build Preview does not change any map, scene or source material. Changes here are temporary.",MessageType.Info);
            if(EditorApplication.isPlayingOrWillChangePlaymode){Release();EditorGUILayout.HelpBox("Preview is available in Edit mode only.",MessageType.Info);return;}
            if(!settings)return;
            var selected=(MountainPreviewSettings)EditorGUILayout.ObjectField("Load saved profile",sourceProfile,typeof(MountainPreviewSettings),false);
            if(selected!=sourceProfile)
            {
                Release();sourceProfile=selected;DestroyImmediate(settings);
                settings=sourceProfile?Instantiate(sourceProfile):CreateInstance<MountainPreviewSettings>();
                settings.hideFlags=HideFlags.HideAndDontSave;serialized=new SerializedObject(settings);
            }
            if(GUILayout.Button("Save as new experimental profile..."))SaveProfile();
            scroll=EditorGUILayout.BeginScrollView(scroll);
            serialized.Update();var property=serialized.GetIterator();bool enter=true;
            while(property.NextVisible(enter)){enter=false;if(property.name!="m_Script")EditorGUILayout.PropertyField(property,true);}
            serialized.ApplyModifiedProperties();
            if(GUILayout.Button("Build / Rebuild private preview"))
            {
                Release();error=null;
                try{preview=new MountainBorderPreview(settings);image=new RenderTexture(960,540,24){hideFlags=HideFlags.HideAndDontSave};image.Create();}
                catch(Exception e){error=e.Message;Release();}
            }
            closeup=EditorGUILayout.Toggle("Waterfall close-up",closeup);
            if(preview!=null&&image)
            {
                try{if(Event.current.type==EventType.Repaint){preview.SetView(closeup);preview.Render(image);}}
                catch(Exception e){error=e.Message;}
                var area=GUILayoutUtility.GetAspectRect(16f/9);EditorGUI.DrawPreviewTexture(area,image);
            }
            if(!string.IsNullOrEmpty(error))EditorGUILayout.HelpBox(error,MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
        void OnInspectorUpdate(){if(preview!=null)Repaint();}
        void PlayStateChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingEditMode)Release();}
        void SaveProfile()
        {
            const string root="Assets/_Game/Experimental/MountainBorder/";
            string path=EditorUtility.SaveFilePanelInProject("Save new experimental profile","MountainPreview","asset","A new profile is saved only inside the experimental folder.",root);
            if(string.IsNullOrEmpty(path))return;
            string fullPath=System.IO.Path.GetFullPath(path);
            string allowed=System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar;
            if(!fullPath.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||System.IO.File.Exists(fullPath)||!path.EndsWith(".asset",StringComparison.OrdinalIgnoreCase))
            {error="Choose a NEW asset inside "+root+". Existing assets are never overwritten.";return;}
            path=FileUtil.GetProjectRelativePath(fullPath).Replace('\\','/');
            var saved=Instantiate(settings);saved.hideFlags=HideFlags.None;
            AssetDatabase.CreateAsset(saved,path);AssetDatabase.SaveAssetIfDirty(saved);sourceProfile=saved;
        }
    }
}
