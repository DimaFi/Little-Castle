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
        DayNightLightingController environmentAtmosphere;
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
            environmentAtmosphere = FindAnyObjectByType<DayNightLightingController>();
        }

        void TickEnvironmentPresentation()
        {
            if (EnvironmentView == null || !studyCamera) return;
            float elapsedWorldSeconds = environmentTimeSource
                ? (float)(environmentTimeSource.GameHoursAdvancedLastTick * 3600.0) : 0f;
            EnvironmentView.Tick(studyCamera, elapsedWorldSeconds);
            ShoreDecorationView?.Tick(EnvironmentView.GroundFocus);
            snowfallView?.Tick(EnvironmentView.GroundFocus);
            if (environmentAtmosphere)
            {
                Vector3 p = EnvironmentView.GroundFocus;
                // A small footprint prevents a single snow brush cell recolouring the whole night.
                float snow = Session.SampleEnvironmentSnow(p.x, p.z);
                snow += Session.SampleEnvironmentSnow(p.x - 8, p.z) + Session.SampleEnvironmentSnow(p.x + 8, p.z);
                snow += Session.SampleEnvironmentSnow(p.x, p.z - 8) + Session.SampleEnvironmentSnow(p.x, p.z + 8);
                var settings = Session.EnvironmentSettings;
                float snowfall = settings.simulateWeather ? settings.snowfallIntensity *
                    Session.EnvironmentThemes.Sample(LittleCastle.World.Environment.EnvironmentThemeIds.Winter, p.x, p.z) : 0;
                environmentAtmosphere.SetLocalEnvironmentPresentation(snow * .2f, snowfall);
            }
        }

        void DisposeEnvironmentPresentation()
        {
            if (environmentAtmosphere) environmentAtmosphere.SetLocalEnvironmentPresentation(0, 0);
            environmentAtmosphere = null;
            snowfallView?.Dispose(); snowfallView = null;
            ShoreDecorationView?.Dispose(); ShoreDecorationView = null;
            EnvironmentView?.Dispose(); EnvironmentView = null;
        }
    }
}
