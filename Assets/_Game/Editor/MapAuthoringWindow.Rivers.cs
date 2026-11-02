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
        [SerializeField] float upgradeFillLimit=2;
        void ConfigureRiverProfile()
        {
            var painter=FindAnyObjectByType<MeadowSurfacePainter>();
            riverTools.ConfigureNewRiverProfile(painter&&painter.source==source&&painter.waterOnlyRivers);
        }
        void DrawRiverTools()
        {
            ConfigureRiverProfile();
            bool direct=EditorGUILayout.Toggle("Прямое редактирование B · проверка",riverDirectEditingEnabled);
            if(direct!=riverDirectEditingEnabled){CancelRiverGesture();riverDirectEditingEnabled=direct;Repaint();GUIUtility.ExitGUI();}
            if(direct)EditorGUILayout.HelpBox("Этап B ещё не принят: данные и операции проверены тестами, живое управление и стоимость preview требуют отдельной проверки.",MessageType.Warning);
            EditorGUILayout.HelpBox("Рисуйте точки по течению. Готовая река проверяется и применяется при сохранении карты; незавершённые точки сохраняются отдельным черновиком.",MessageType.Info);
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
                riverTools.Draw(session,false,riverDirectEditingEnabled);
            }
            finally{GUI.skin.label=label;GUI.skin.button=button;GUI.skin.box=box;}
            if(riverTools.ModeRevision!=revision||riverTools.mode!=mode)
            {
                if(riverTools.mode!=0)SetTool("river.points");
                else {tools.Complete();pointerHeld=false;brush=false;}
                SceneView.RepaintAll();
            }
            if(riverTools.GuiTreeChanged){Repaint();GUIUtility.ExitGUI();}
            var selected=riverTools.draft;
            if(selected!=null&&selected.id!=0)
            {
                if(selected.layoutVersion==3&&selected.sectionProfile!=null)
                {
                    EditorGUILayout.LabelField("Мягкое русло · профиль всей реки",EditorStyles.boldLabel);
                    selected.sectionProfile.bankRise=EditorGUILayout.Slider("Борт над водой, м",selected.sectionProfile.bankRise,.03f,.4f);
                    selected.sectionProfile.maximumCut=EditorGUILayout.Slider("Предел выемки, м",selected.sectionProfile.maximumCut,.15f,12);
                    selected.sectionProfile.maximumFill=EditorGUILayout.Slider("Предел насыпи, м",selected.sectionProfile.maximumFill,0,4);
                }
                else if(!riverTools.HasUnappliedDraft(session))
                {
                    upgradeFillLimit=EditorGUILayout.Slider("Предел насыпи при переходе, м",upgradeFillLimit,0,4);
                    if(GUILayout.Button("Перейти на мягкое русло · предпросмотр / одна отмена"))
                    {
                        Run(()=>{session.UpgradeRiverProfiles(new[]{selected.id},new LittleCastle.World.MapRiverSectionProfile{maximumFill=upgradeFillLimit});riverTools.SelectRiver(session,selected.id);});
                        GUIUtility.ExitGUI();
                    }
                }
            }
            DrawWaterfallTools();
            if(riverDirectEditingEnabled)DrawDirectRiverInspector();
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
