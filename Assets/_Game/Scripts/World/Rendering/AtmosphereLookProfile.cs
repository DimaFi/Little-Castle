using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>One artist-authored sRGB look; contains no simulation or weather state.</summary>
    [CreateAssetMenu(menuName = "Little Castle/Rendering/Atmosphere Look")]
    public sealed class AtmosphereLookProfile : ScriptableObject
    {
        public AtmosphereLook look = new AtmosphereLook
        {
            zenith = new Color(.29f, .56f, .79f), horizon = new Color(.78f, .86f, .88f),
            lowerSky = new Color(.51f, .65f, .68f), fog = new Color(.70f, .79f, .80f),
            ambientSky = new Color(.56f, .64f, .75f), ambientEquator = new Color(.45f, .51f, .57f),
            ambientGround = new Color(.31f, .34f, .25f), direct = new Color(1, .96f, .86f),
            cloudLight = new Color(.97f, .96f, .89f), cloudShade = new Color(.52f, .64f, .71f),
            fogStart = 180, fogEnd = 850, sunIntensity = .95f, moonIntensity = .18f,
            starBrightness = 1, transmission = .12f
        };
    }

    [Serializable]
    public struct AtmosphereLook
    {
        public Color zenith, horizon, lowerSky, fog;
        public Color ambientSky, ambientEquator, ambientGround, direct;
        public Color cloudLight, cloudShade;
        [Tooltip("Cheap artist-directed ground fill, not a GI solution.")]
        public Color groundBounce;
        [Range(0, 1)] public float groundBounceStrength;
        [Range(0, 1)] public float toneMapping;
        [Range(-2, 2)] public float exposure;
        [Min(0)] public float fogStart;
        [Min(1)] public float fogEnd;
        [Min(0)] public float sunIntensity, moonIntensity;
        [Range(0, 3)] public float starBrightness;
        [Range(0, .5f)] public float transmission;

        public static AtmosphereLook Lerp(AtmosphereLook a, AtmosphereLook b, float t)
        {
            return new AtmosphereLook
            {
                zenith = Color.Lerp(a.zenith, b.zenith, t), horizon = Color.Lerp(a.horizon, b.horizon, t),
                lowerSky = Color.Lerp(a.lowerSky, b.lowerSky, t), fog = Color.Lerp(a.fog, b.fog, t),
                ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t),
                ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t),
                ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t), direct = Color.Lerp(a.direct, b.direct, t),
                cloudLight = Color.Lerp(a.cloudLight, b.cloudLight, t), cloudShade = Color.Lerp(a.cloudShade, b.cloudShade, t),
                fogStart = Mathf.Lerp(a.fogStart, b.fogStart, t), fogEnd = Mathf.Lerp(a.fogEnd, b.fogEnd, t),
                sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
                moonIntensity = Mathf.Lerp(a.moonIntensity, b.moonIntensity, t),
                starBrightness = Mathf.Lerp(a.starBrightness, b.starBrightness, t),
                transmission = Mathf.Lerp(a.transmission, b.transmission, t),
                groundBounce = Color.Lerp(a.groundBounce, b.groundBounce, t),
                groundBounceStrength = Mathf.Lerp(a.groundBounceStrength, b.groundBounceStrength, t),
                toneMapping = Mathf.Lerp(a.toneMapping, b.toneMapping, t),
                exposure = Mathf.Lerp(a.exposure, b.exposure, t)
            };
        }
    }
}
