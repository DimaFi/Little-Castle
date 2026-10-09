using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Pure, world-space profile geometry shared by future river consumers.
    /// This version exactly preserves the Q04 baseline of terrain wetness:
    /// two interpolated half-widths, closest point on each valid segment,
    /// SmoothStep shoreline falloff and inclusive chunk AABB broad phase.
    /// No scene, chunk, terrain editing, random state or allocations.
    /// </summary>
    public static class RiverEnvelopeUtility
    {
        private const float MinimumHalfWidth = 0.1f;
        private const float MinimumLengthSquared = 0.000001f;
        private const float MinimumFalloff = 0.01f;

        public readonly struct Segment
        {
            public readonly Vector2 a;
            public readonly Vector2 b;
            public readonly float halfStartWidth;
            public readonly float halfEndWidth;

            public Segment(
                Vector2 a,
                Vector2 b,
                float halfStartWidth,
                float halfEndWidth)
            {
                this.a = a;
                this.b = b;
                this.halfStartWidth = halfStartWidth;
                this.halfEndWidth = halfEndWidth;
            }
        }

        public static bool TryCreateSegment(
            WorldRiverData river,
            int index,
            out Segment segment)
        {
            segment = default;
            if (river == null ||
                river.centerline == null ||
                index < 0 ||
                index >= river.centerline.Count - 1)
            {
                return false;
            }

            Vector2 a = river.centerline[index];
            Vector2 b = river.centerline[index + 1];
            if ((b - a).sqrMagnitude <= MinimumLengthSquared)
                return false;

            segment = new Segment(
                a, b,
                Mathf.Max(
                    MinimumHalfWidth,
                    river.GetWidthAtPoint(index) * 0.5f),
                Mathf.Max(
                    MinimumHalfWidth,
                    river.GetWidthAtPoint(index + 1) * 0.5f));
            return true;
        }

        /// <summary>
        /// Inclusive AABB, including falloff. Caller uses absolute X/Z
        /// extents of the current chunk, including its boundary vertices.
        /// </summary>
        public static bool OverlapsChunk(
            Segment segment,
            float edgeMeters,
            float minX,
            float minZ,
            float maxX,
            float maxZ)
        {
            float padding =
                Mathf.Max(segment.halfStartWidth,
                    segment.halfEndWidth) + edgeMeters;

            return Mathf.Max(segment.a.x, segment.b.x) + padding >= minX &&
                   Mathf.Min(segment.a.x, segment.b.x) - padding <= maxX &&
                   Mathf.Max(segment.a.y, segment.b.y) + padding >= minZ &&
                   Mathf.Min(segment.a.y, segment.b.y) - padding <= maxZ;
        }

        public static float DistanceToSegment(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            out float t)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            t = lengthSquared > MinimumLengthSquared
                ? Mathf.Clamp01(
                    Vector2.Dot(point - a, ab) / lengthSquared)
                : 0f;
            return (point - (a + ab * t)).magnitude;
        }

        /// <summary>
        /// Intensity for one segment: union of multiple segments is
        /// Mathf.Max at the callsite, not closest-segment arbitration.
        /// This matters at sharp bends and confluences.
        /// </summary>
        public static float WetnessAt(
            Vector2 point,
            Segment segment,
            float edgeMeters)
        {
            float distance = DistanceToSegment(
                point, segment.a, segment.b, out float t);
            float halfWidth = Mathf.Lerp(
                segment.halfStartWidth,
                segment.halfEndWidth,
                t);
            return SoftCorridor(
                distance, halfWidth, edgeMeters);
        }

        /// <summary>
        /// Preserve the current smooth ramp and zero-width edge floor.
        /// This does not represent hydrology, exact shore polygons or
        /// physical water depth: it is a scalar mask contribution.
        /// </summary>
        public static float SoftCorridor(
            float distance,
            float innerRadius,
            float falloff)
        {
            if (distance <= innerRadius)
                return 1f;

            float fraction = Mathf.Clamp01(
                (distance - innerRadius) /
                Mathf.Max(MinimumFalloff, falloff));
            return 1f - Mathf.SmoothStep(
                0f, 1f, fraction);
        }
    }
}
