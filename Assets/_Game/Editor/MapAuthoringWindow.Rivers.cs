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
            if(showRivers){bool previous=GUI.changed;GUI.changed=false;riverTools.Draw(session);bool changed=GUI.changed;GUI.changed|=previous;if(riverTools.mode!=0&&changed){extensionToolId=null;brush=true;treeTool=0;}}
        }
        void DrawRiverDraft()
        {
            if(!showRivers||riverTools.mode==0)return;
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
