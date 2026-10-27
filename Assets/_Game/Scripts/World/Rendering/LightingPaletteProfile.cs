using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>Artist-authored sRGB palette. The atmosphere owns interpolation; LC globals own working-space conversion.</summary>
    [CreateAssetMenu(menuName = "Little Castle/Rendering/Lighting Palette", fileName = "LightingPalette")]
    public sealed class LightingPaletteProfile : ScriptableObject
    {
        [Min(0)] public float sunIntensity = 1;
        [Min(0)] public float moonIntensity = .2f;
        public Color sunDay = new Color(1, .96f, .86f);
        public Color sunHorizon = new Color(1, .60f, .34f);
        public bool sunColorFromElevation;
        [Range(0, 80)] public float horizonColorElevation = 5;
        [Range(1, 90)] public float dayColorElevation = 32;
        public float SunColorBlend(float daylight, float elevationDegrees)
        {
            return sunColorFromElevation
                ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(horizonColorElevation,
                    Mathf.Max(horizonColorElevation + .1f, dayColorElevation), elevationDegrees))
                : Mathf.SmoothStep(0, 1, daylight);
        }
        public Color moon = new Color(.58f, .68f, 1);
        public Color daySky = new Color(.52f, .60f, .72f);
        public Color dayEquator = new Color(.44f, .50f, .58f);
        public Color dayGround = new Color(.30f, .31f, .24f);
        public Color nightSky = new Color(.40f, .49f, .70f);
        public Color nightEquator = new Color(.34f, .42f, .58f);
        public Color nightGround = new Color(.25f, .30f, .43f);
    }
}
