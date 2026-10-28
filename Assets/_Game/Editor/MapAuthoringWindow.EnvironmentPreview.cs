using LittleCastle.Rendering;
using LittleCastle.Research;
using UnityEditor;

namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        MapEnvironmentPresentation environmentPreview;
        RiverShoreDecorationPresentation shoreDecorationPreview;

        void BindEnvironmentPreview(MeadowSurfacePainter painter)
        {
            RestoreEnvironmentPreview();
            if (session == null) return;
            environmentPreview = new MapEnvironmentPresentation(session);
            var scatter = painter.scatterRenderer;
            if (scatter && scatter.library)
                shoreDecorationPreview = new RiverShoreDecorationPresentation(session, scatter.library);
            TickEnvironmentPreview();
        }

        void TickEnvironmentPreview()
        {
            var view = SceneView.lastActiveSceneView;
            if (environmentPreview == null || !view || !view.camera) return;
            // Edit Mode has no advancing simulation clock. Local weather advances in Play.
            environmentPreview.Tick(view.camera);
            shoreDecorationPreview?.Tick(environmentPreview.GroundFocus);
            if (environmentPreview.LastUploadPixelCount > 0 ||
                (shoreDecorationPreview != null && shoreDecorationPreview.LastRebuiltRegions > 0))
                SceneView.RepaintAll();
        }

        void RestoreEnvironmentPreview()
        {
            shoreDecorationPreview?.Dispose(); shoreDecorationPreview = null;
            environmentPreview?.Dispose(); environmentPreview = null;
        }
    }
}
