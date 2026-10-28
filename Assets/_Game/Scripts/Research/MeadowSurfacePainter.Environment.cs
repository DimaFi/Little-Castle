using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Research
{
    public sealed partial class MeadowSurfacePainter
    {
        [Tooltip("Optional existing world clock for local snow weather. No clock means no weather advancement.")]
        public WorldTimeSystem environmentTimeSource;
        MapSnowfallPresentation snowfallView;
        public MapEnvironmentPresentation EnvironmentView { get; private set; }
        public RiverShoreDecorationPresentation ShoreDecorationView { get; private set; }

        void InitializeEnvironmentPresentation()
        {
            DisposeEnvironmentPresentation();
            if (Session == null) return;
            EnvironmentView = new MapEnvironmentPresentation(Session);
            snowfallView = new MapSnowfallPresentation(Session);
            if (scatterRenderer && scatterRenderer.library)
                ShoreDecorationView = new RiverShoreDecorationPresentation(Session, scatterRenderer.library);
            // Resolve only the local hierarchy; never search the complete scene for a clock.
            if (!environmentTimeSource) environmentTimeSource = GetComponentInParent<WorldTimeSystem>();
            if (!environmentTimeSource) environmentTimeSource = FindAnyObjectByType<WorldTimeSystem>();
        }

        void TickEnvironmentPresentation()
        {
            if (EnvironmentView == null || !studyCamera) return;
            float elapsedWorldSeconds = environmentTimeSource
                ? (float)(environmentTimeSource.GameHoursAdvancedLastTick * 3600.0) : 0f;
            EnvironmentView.Tick(studyCamera, elapsedWorldSeconds);
            ShoreDecorationView?.Tick(EnvironmentView.GroundFocus);
            snowfallView?.Tick(EnvironmentView.GroundFocus);
        }

        void DisposeEnvironmentPresentation()
        {
            snowfallView?.Dispose(); snowfallView = null;
            ShoreDecorationView?.Dispose(); ShoreDecorationView = null;
            EnvironmentView?.Dispose(); EnvironmentView = null;
        }
    }
}
