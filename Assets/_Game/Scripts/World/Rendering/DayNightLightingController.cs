using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.World
{
    [DefaultExecutionOrder(-50)]
    public sealed class DayNightLightingController : MonoBehaviour
    {
        [SerializeField] private WorldTimeSystem timeSystem;
        [SerializeField] private Light sun;
        [SerializeField] private Light moon;
        [SerializeField] private float sunAzimuth = -30f;
        [SerializeField, Min(0f)] private float maxSunIntensity = 1.1f;
        [SerializeField, Min(0f)] private float maxMoonIntensity = 0.16f;
        [SerializeField] private Color sunDayColor = new Color(1f, 0.96f, 0.84f, 1f);
        [SerializeField] private Color sunHorizonColor = new Color(1f, 0.58f, 0.34f, 1f);
        [SerializeField] private Color moonColor = new Color(0.62f, 0.72f, 1f, 1f);
        [SerializeField, Min(0.05f)] private float dawnTransitionGameHours = 2f;
        [SerializeField, Min(0.05f)] private float duskTransitionGameHours = 2f;

        [SerializeField] private Color dayAmbientSky = new Color(0.78f, 0.82f, 0.86f, 1f);
        [SerializeField] private Color dayAmbientEquator = new Color(0.62f, 0.66f, 0.70f, 1f);
        [SerializeField] private Color dayAmbientGround = new Color(0.42f, 0.44f, 0.40f, 1f);
        [SerializeField] private Color nightAmbientSky = new Color(0.34f, 0.40f, 0.54f, 1f);
        [SerializeField] private Color nightAmbientEquator = new Color(0.26f, 0.31f, 0.42f, 1f);
        [SerializeField] private Color nightAmbientGround = new Color(0.18f, 0.21f, 0.29f, 1f);

        private void Awake()
        {
            if (timeSystem == null)
                timeSystem = FindFirstObjectByType<WorldTimeSystem>();

            if (sun == null)
                sun = RenderSettings.sun;

            if (sun != null)
                RenderSettings.sun = sun;
        }

        private void Start() => ApplyLighting();
        private void LateUpdate() => ApplyLighting();

        public void ApplyLighting()
        {
            if (timeSystem == null || timeSystem.Clock == null || timeSystem.Settings == null)
                return;

            float hour = timeSystem.CurrentHour;
            float sunrise = timeSystem.Settings.DayStartHour;
            float sunset = timeSystem.Settings.NightStartHour;
            float daylight = GetDaylight(hour, sunrise, sunset);
            float angle = GetSolarAngle(hour, sunrise, sunset);

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(angle, sunAzimuth, 0f);
                sun.intensity = maxSunIntensity * daylight;
                sun.color = Color.Lerp(sunHorizonColor, sunDayColor, daylight);
            }

            if (moon != null)
            {
                moon.transform.rotation = Quaternion.Euler(Mathf.Repeat(angle + 180f, 360f), sunAzimuth, 0f);
                moon.intensity = maxMoonIntensity * (1f - daylight);
                moon.color = moonColor;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(nightAmbientSky, dayAmbientSky, daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(nightAmbientEquator, dayAmbientEquator, daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(nightAmbientGround, dayAmbientGround, daylight);
        }

        private float GetDaylight(float hour, float sunrise, float sunset)
        {
            if (hour < sunrise || hour >= sunset)
                return 0f;

            float fullDay = Mathf.Min(sunset, sunrise + dawnTransitionGameHours);
            float duskStart = Mathf.Max(sunrise, sunset - duskTransitionGameHours);

            if (hour < fullDay)
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sunrise, fullDay, hour));

            if (hour > duskStart)
                return Mathf.SmoothStep(0f, 1f, 1f - Mathf.InverseLerp(duskStart, sunset, hour));

            return 1f;
        }

        private static float GetSolarAngle(float hour, float sunrise, float sunset)
        {
            if (hour >= sunrise && hour < sunset)
                return Mathf.Lerp(0f, 180f, Mathf.InverseLerp(sunrise, sunset, hour));

            float nightHours = 24f - sunset + sunrise;
            float afterSunset = hour >= sunset ? hour - sunset : 24f - sunset + hour;
            return Mathf.Lerp(180f, 360f, afterSunset / nightHours);
        }
    }
}
