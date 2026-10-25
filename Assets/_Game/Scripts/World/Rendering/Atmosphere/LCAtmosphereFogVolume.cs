using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Rendering.Atmosphere
{
    /// <summary>
    /// Sparse local fog. Supports four nearest spheres per camera in the first
    /// implementation; no colliders, per-volume Update or authoritative world data.
    /// </summary>
    public sealed class LCAtmosphereFogVolume : MonoBehaviour
    {
        private static readonly List<LCAtmosphereFogVolume> Registered =
            new List<LCAtmosphereFogVolume>();

        public static IReadOnlyList<LCAtmosphereFogVolume> Active => Registered;

        [Min(0.1f)] public float radius = 32f;
        [Min(0f)] public float density = 0.017f;
        [Min(0.1f)] public float edgeFeather = 15f;

        private void OnEnable()
        {
            if (!Registered.Contains(this)) Registered.Add(this);
        }

        private void OnDisable()
        {
            Registered.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.73f, 0.82f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
