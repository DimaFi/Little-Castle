using UnityEngine;

namespace LittleCastle.Rendering.ChimneySmoke
{
    /// <summary>Visual-only chimney smoke. No gameplay or world-state dependencies.</summary>
    [CreateAssetMenu(menuName = "Little Castle/Rendering/Chimney Smoke Profile")]
    public sealed class ChimneySmokeProfile : ScriptableObject
    {
        [Header("Shape (metres)")]
        [Min(0.2f)] public float height = 3.6f;
        [Range(0.02f, 0.5f)] public float baseWidth = 0.12f;
        [Range(0.05f, 1.2f)] public float topWidth = 0.53f;
        [Range(8, 40)] public int segments = 20;

        [Header("Motion")]
        [Min(0.05f)] public float riseSpeed = 0.88f;
        [Range(0f, 1f)] public float curlAmplitude = 0.20f;
        [Range(0f, 2f)] public float curlFrequency = 0.75f;
        [Range(0f, 3f)] public float windInfluence = 0.60f;
        [Range(0f, 2f)] public float windSpeed = 1f;

        [Header("Appearance")]
        public Color color = new Color(0.91f, 0.92f, 0.91f, 1f);
        [Range(0f, 1f)] public float opacity = 0.32f;
        [Range(0f, 1f)] public float haloOpacity = 0.18f;
        [Range(0f, 1f)] public float duskWarmth = 0.14f;
        [Range(0f, 1f)] public float nightDim = 0.30f;

        [Header("Budget")]
        [Min(5f)] public float maxDistance = 85f;
        [Range(5f, 60f)] public float targetUpdatesPerSecond = 24f;
        [Range(0f, 1f)] public float beautyBias = 0.65f;
    }
}
