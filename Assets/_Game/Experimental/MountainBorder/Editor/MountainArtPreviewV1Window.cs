using System;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder.Editor
{
    public sealed class MountainArtPreviewV1Window:EditorWindow
    {
        MountainArtPreviewV1 preview;RenderTexture target;MountainArtView view;int seed=1729;bool clay=true,trees;
        bool appearance;
        Color rock=new Color(.40f,.34f,.27f),alpineRock=new Color(.31f,.34f,.39f);
        float snowLine=77,snowBlend=28,groundDetail=.45f;
        string error;
        [MenuItem("Little Castle/Experimental/Mountain Border/Open Art V1")]
        public static void Open()=>GetWindow<MountainArtPreviewV1Window>("Mountain art v1");
        void OnEnable(){EditorApplication.playModeStateChanged+=PlayChanged;}
        void OnDisable(){EditorApplication.playModeStateChanged-=PlayChanged;Release();}
        void PlayChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingEditMode)Release();}
        void OnInspectorUpdate(){if(preview!=null)Repaint();}
        void Release(){preview?.Dispose();preview=null;if(target){target.Release();DestroyImmediate(target);target=null;}}
        void OnGUI()
        {
            EditorGUILayout.HelpBox("V1 authored art proof in a private scene. Map, river records and production materials remain untouched.",MessageType.Info);
            if(EditorApplication.isPlayingOrWillChangePlaymode){Release();return;}
            seed=EditorGUILayout.IntField("Seed",seed);view=(MountainArtView)EditorGUILayout.EnumPopup("Camera",view);
            clay=EditorGUILayout.Toggle("Clay geometry diagnostic",clay);trees=EditorGUILayout.Toggle("Static spruce comparison",trees);
            appearance=EditorGUILayout.Foldout(appearance,"Stone / snow / ground appearance");
            if(appearance)
            {
                rock=EditorGUILayout.ColorField("Lower rock",rock);
                alpineRock=EditorGUILayout.ColorField("High mountain rock",alpineRock);
                snowLine=EditorGUILayout.Slider("Permanent snow altitude",snowLine,35,135);
                snowBlend=EditorGUILayout.Slider("Snow transition",snowBlend,1,45);
                groundDetail=EditorGUILayout.Slider("Existing ground texture",groundDetail,0,1);
            }
            if(GUILayout.Button("Build / Rebuild V1")){Release();try{preview=new MountainArtPreviewV1(seed,trees);target=new RenderTexture(1280,800,24){hideFlags=HideFlags.HideAndDontSave};target.Create();error=null;}catch(Exception e){error=e.Message;Release();}}
            if(preview!=null&&target){preview.SetView(view);preview.SetClay(clay);
                preview.Land.SetColor("_RockTint",rock);preview.Land.SetColor("_ColdRockColor",alpineRock);
                preview.Land.SetFloat("_SnowLine",snowLine);preview.Land.SetFloat("_SnowBlend",snowBlend);
                preview.Land.SetFloat("_GroundTextureStrength",groundDetail);
                try{if(Event.current.type==EventType.Repaint)preview.Render(target);}catch(Exception e){error=e.Message;}
                EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetAspectRect(1.6f),target);}
            if(!string.IsNullOrEmpty(error))EditorGUILayout.HelpBox(error,MessageType.Error);
        }
    }
}
