using LittleCastle.Rendering;
using LittleCastle.Research;
using UnityEditor;
using UnityEngine;
namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        MapRiverPresentation riverPreview;
        MapRiverToolState riverTools=new MapRiverToolState();
        GUIStyle riverWrappedLabel,riverWrappedButton,riverWrappedBox;
        void ConfigureRiverProfile()
        {
            var painter=FindAnyObjectByType<MeadowSurfacePainter>();
            riverTools.ConfigureNewRiverProfile(painter&&painter.source==source&&painter.waterOnlyRivers);
        }
        void DrawRiverTools()
        {
            ConfigureRiverProfile();
            EditorGUILayout.HelpBox("1. Новая река → 2. Точки по земле в SceneView по течению → 3. Применить реку. Только после применения река входит в JSON и авторскую базу.",MessageType.Info);
            if(riverTools.HasUnappliedDraft(session)&&GUILayout.Button("Сбросить неприменённый черновик…"))
            {
                if(EditorUtility.DisplayDialog("Сбросить черновик реки?","Удалить неприменённые точки и настройки? Уже применённые реки останутся в карте.","Сбросить черновик","Отмена"))
                {riverTools=new MapRiverToolState();ConfigureRiverProfile();brush=false;SceneView.RepaintAll();}
            }
            int revision=riverTools.ModeRevision,mode=riverTools.mode;
            riverWrappedLabel??=new GUIStyle(GUI.skin.label){wordWrap=true};
            riverWrappedButton??=new GUIStyle(GUI.skin.button){wordWrap=true};
            riverWrappedBox??=new GUIStyle(GUI.skin.box){wordWrap=true};
            var label=GUI.skin.label;var button=GUI.skin.button;var box=GUI.skin.box;
            try
            {
                GUI.skin.label=riverWrappedLabel;GUI.skin.button=riverWrappedButton;GUI.skin.box=riverWrappedBox;
                riverTools.Draw(session,false);
            }
            finally{GUI.skin.label=label;GUI.skin.button=button;GUI.skin.box=box;}
            if(riverTools.ModeRevision!=revision||riverTools.mode!=mode)
            {
                if(riverTools.mode!=0)SetTool("river.points");
                else {tools.Complete();pointerHeld=false;brush=false;}
                SceneView.RepaintAll();
            }
        }
        void DrawRiverDraft()
        {
            if(CurrentToolId!="river.points"||riverTools.mode==0)return;
            var points=riverTools.DraftPoints;Handles.color=Color.cyan;
            for(int i=0;i<points.Count;i++)
            {
                var p=points[i];var v=new Vector3(p.x,session.SampleHeight(p.x,p.y)+.15f,p.y);
                Handles.SphereHandleCap(0,v,Quaternion.identity,.25f,EventType.Repaint);
                if(i>0){var last=points[i-1];Handles.DrawLine(new Vector3(last.x,session.SampleHeight(last.x,last.y)+.15f,last.y),v);}
            }
        }

    }
}
