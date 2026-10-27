using LittleCastle.Rendering;
using LittleCastle.Research;
using UnityEditor;
using UnityEngine;
namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        MapRiverPresentation riverPreview;
        readonly MapRiverToolState riverTools=new MapRiverToolState();
        bool showRivers;
        void DrawRiverTools()
        {
            showRivers=EditorGUILayout.Foldout(showRivers,"Реки, песчаные спуски, броды и скалы",true);
            if(showRivers){riverTools.Draw(session);if(riverTools.mode!=0){brush=false;treeTool=0;}}
        }
        bool RiverSceneGUI()
        {
            if(session==null||EditorApplication.isPlaying||!showRivers||riverTools.mode==0)return false;
            var points=riverTools.DraftPoints;Handles.color=Color.cyan;
            for(int i=0;i<points.Count;i++)
            {
                var p=points[i];var v=new Vector3(p.x,session.SampleHeight(p.x,p.y)+.15f,p.y);Handles.SphereHandleCap(0,v,Quaternion.identity,.25f,EventType.Repaint);
                if(i>0){var last=points[i-1];Handles.DrawLine(new Vector3(last.x,session.SampleHeight(last.x,last.y)+.15f,last.y),v);}
            }
            var e=Event.current;if(e.keyCode==KeyCode.Escape&&e.type==EventType.KeyDown){riverTools.mode=0;e.Use();return true;}
            if(e.alt)return true;HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if(e.type==EventType.MouseDown&&e.button==0)
            {
                var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if(terrainPreview!=null&&terrainPreview.Raycast(ray,out var hit)){riverTools.Click(session,new Vector2(hit.point.x,hit.point.z));e.Use();Repaint();}
            }
            SceneView.RepaintAll();return true;
        }
    }
}
