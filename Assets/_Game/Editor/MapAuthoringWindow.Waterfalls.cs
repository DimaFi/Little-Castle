using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        readonly MapWaterfallToolState waterfallTools = new MapWaterfallToolState();
        void DrawWaterfallTools()
        {
            int revision = waterfallTools.ModeRevision;
            waterfallTools.Draw(session);
            if (revision == waterfallTools.ModeRevision){if(waterfallTools.GuiTreeChanged){Repaint();GUIUtility.ExitGUI();}return;}
            if (waterfallTools.Active) SetTool("waterfall.points");
            else { tools.Complete(); pointerHeld = false; brush = false; }
            SceneView.RepaintAll();
            if(waterfallTools.GuiTreeChanged){Repaint();GUIUtility.ExitGUI();}
        }
        void DrawWaterfallDraft(MapToolPointer pointer)
        {
            if (CurrentToolId != "waterfall.points") return;
            if (pointer.hasSurface && waterfallTools.Active)
            {
                var p = pointer.position;
                Handles.color = new Color(.3f, .9f, 1, .7f);
                Handles.DrawWireDisc(new Vector3(p.x, session.SampleHeight(p.x, p.y) + .08f, p.y), Vector3.up, waterfallTools.radius);
                if (waterfallTools.UpperRiverId == 0)
                {
                    foreach (var endpoint in session.RiverNetwork.FindUpperEndpoints(p, waterfallTools.radius))
                        EndpointMarker(endpoint);
                }
                else foreach (var candidate in session.RiverNetwork.FindLowerCandidates(waterfallTools.UpperRiverId, p, waterfallTools.radius))
                    EndpointMarker(candidate.lower);
            }
            var route = waterfallTools.PreviewRoute;
            if (route == null && waterfallTools.SelectedWaterfallId != 0 && session.HasWaterfall(waterfallTools.SelectedWaterfallId))
                route = session.GetWaterfall(waterfallTools.SelectedWaterfallId).route;
            if (route == null || route.IsEmpty) return;
            var line = new Vector3[route.sections.Count];
            for (int i = 0; i < line.Length; i++) line[i] = route.sections[i].FrontCenter;
            Handles.color = Color.cyan; Handles.DrawAAPolyLine(3, line);
            Handles.Label(line[0], "Кромка"); Handles.Label(line[line.Length - 1], "Чаша");
        }
        static void EndpointMarker(MapRiverEndpoint endpoint)
        {
            Handles.SphereHandleCap(0, new Vector3(endpoint.position.x, endpoint.waterHeight + .1f, endpoint.position.y),
                Quaternion.identity, .35f, EventType.Repaint);
        }
    }
}
