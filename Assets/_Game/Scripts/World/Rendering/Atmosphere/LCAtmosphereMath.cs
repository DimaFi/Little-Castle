using UnityEngine;

namespace LittleCastle.Rendering.Atmosphere
{
    /// <summary>Reference CPU math; kept pure for unit testing and quality checks.</summary>
    public static class LCAtmosphereMath
    {
        public static float Transmittance(float density, float meters)
        {
            return Mathf.Exp(-Mathf.Max(0f, density) * Mathf.Max(0f, meters));
        }

        public static float HeightDensity(float altitude, float baseHeight,
            float surfaceDensity, float falloff)
        {
            float aboveBase = Mathf.Max(0f, altitude - baseHeight);
            return Mathf.Max(0f, surfaceDensity) *
                Mathf.Exp(-aboveBase / Mathf.Max(0.1f, falloff));
        }

        public static float ZoneWeight(Vector3 focus, Transform zone, Vector2 size,
            float blendDistance)
        {
            Vector3 local = zone.InverseTransformPoint(focus);
            float dx = Mathf.Max(0f, Mathf.Abs(local.x) - size.x * 0.5f);
            float dz = Mathf.Max(0f, Mathf.Abs(local.z) - size.y * 0.5f);
            float outsideDistance = Mathf.Sqrt(dx * dx + dz * dz);
            if (outsideDistance <= 0f) return 1f;
            if (blendDistance <= 0f) return 0f;
            float t = Mathf.Clamp01(outsideDistance / blendDistance);
            return 1f - t * t * (3f - 2f * t);
        }
    }
}
