using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World.Weather
{
    /// <summary>
    /// Authored once on a building PREFAB, inherited by all instances.
    /// This marker is a soft roof footprint (not a collider and not a new rain manager).
    /// Place at the underside/edge height of the roof; local X/Z set footprint orientation.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class RainShelterVolume : MonoBehaviour
    {
        private static readonly List<RainShelterVolume> Registered = new List<RainShelterVolume>(128);
        public static IReadOnlyList<RainShelterVolume> Active => Registered;

        [Header("Roof footprint (local metres)")]
        [Min(.25f)] public float halfWidth = 3.5f;
        [Min(.25f)] public float halfDepth = 3.5f;
        [Header("Soft rain transition")]
        [Tooltip("Metres of gentle falloff inward from the roof eaves. Avoids a straight hard line.")]
        [Range(.15f, 8f)] public float edgeFade = 2f;
        [Tooltip("A gentle transition immediately below the roof; roof tiles stay wet.")]
        [Range(.1f, 4f)] public float verticalFade = .8f;
        [Tooltip("How much rainfall is blocked under the roof. 1 = dry centre, 0 = no shelter.")]
        [Range(0f, 1f)] public float protection = .98f;
        [Tooltip("Irregularity of the eave rain boundary (0 = straight soft line).")]
        [Range(0f, .45f)] public float irregularity = .15f;

        public struct Shape
        {
            public Vector2 center, right, forward, halfSize;
            public float roofY, edgeFade, verticalFade, protection, irregularity;
        }

        public Shape WorldShape
        {
            get
            {
                Vector3 right = transform.right, forward = transform.forward;
                Vector3 scale = transform.lossyScale;
                Vector2 r = new Vector2(right.x, right.z).normalized;
                Vector2 f = new Vector2(forward.x, forward.z).normalized;
                if (r.sqrMagnitude < .1f) r = Vector2.right;
                if (f.sqrMagnitude < .1f) f = new Vector2(-r.y, r.x);
                Vector3 p = transform.position;
                return new Shape
                {
                    center = new Vector2(p.x, p.z), right = r, forward = f,
                    halfSize = new Vector2(Mathf.Max(.1f, halfWidth * Mathf.Abs(scale.x)),
                        Mathf.Max(.1f, halfDepth * Mathf.Abs(scale.z))),
                    roofY = p.y, edgeFade = Mathf.Max(.1f, edgeFade), verticalFade = Mathf.Max(.05f, verticalFade),
                    protection = Mathf.Clamp01(protection), irregularity = Mathf.Clamp(irregularity, 0f, .45f)
                };
            }
        }

        // Shared CPU math for visual test and mask rasterizer; no Physics and no per-drop allocations.
        public static float HorizontalCoverage(float x, float z, Shape s)
        {
            Vector2 d = new Vector2(x - s.center.x, z - s.center.y);
            float alongX = Mathf.Abs(Vector2.Dot(d, s.right));
            float alongZ = Mathf.Abs(Vector2.Dot(d, s.forward));
            float inside = Mathf.Min(s.halfSize.x - alongX, s.halfSize.y - alongZ);
            // World anchored (not per-frame): irregular, but still smooth over a wide feather.
            float stagger = (Mathf.Sin(x * .73f + z * .19f) * Mathf.Cos(z * .58f - x * .24f)) *
                s.edgeFade * s.irregularity;
            float t = Mathf.Clamp01((inside + stagger) / Mathf.Max(.1f, s.edgeFade));
            return (t * t * (3f - 2f * t)) * s.protection;
        }

        public static float CoverageAt(Vector3 position, Shape s)
        {
            float vertical = Mathf.Clamp01((s.roofY - position.y) / Mathf.Max(.05f, s.verticalFade));
            vertical = vertical * vertical * (3f - 2f * vertical);
            return HorizontalCoverage(position.x, position.z, s) * vertical;
        }

        public static float CoverageAt(Vector3 position)
        {
            float max = 0f;
            for (int i = 0; i < Registered.Count; i++)
            {
                var marker = Registered[i];
                if (!marker || !marker.isActiveAndEnabled) continue;
                float c = CoverageAt(position, marker.WorldShape);
                if (c > max) max = c;
                if (max >= .999f) break;
            }
            return max;
        }

        private void OnEnable()
        {
            if (!Registered.Contains(this)) Registered.Add(this);
        }
        private void OnDisable() { Registered.Remove(this); }
        private void OnDestroy() { Registered.Remove(this); }
        private void OnValidate()
        {
            halfWidth = Mathf.Max(.25f, halfWidth);
            halfDepth = Mathf.Max(.25f, halfDepth);
            edgeFade = Mathf.Clamp(edgeFade, .15f, 8f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.25f, .75f, 1f, .85f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(halfWidth * 2f, .03f, halfDepth * 2f));
            Gizmos.color = new Color(.25f, 1f, .6f, .85f);
            Gizmos.DrawWireCube(Vector3.down * .02f,
                new Vector3(Mathf.Max(.05f, halfWidth - edgeFade) * 2f, .03f,
                    Mathf.Max(.05f, halfDepth - edgeFade) * 2f));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
