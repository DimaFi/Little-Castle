using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World.Weather
{
    /// <summary>
    /// Place on roof eaves, awnings, gutters or large branches to emit shared drip particles.
    /// No particles are simulated on this component; the single rain presenter owns the pool.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RainRoofDripSource : MonoBehaviour
    {
        private static readonly List<RainRoofDripSource> Registered = new List<RainRoofDripSource>(64);
        public static IReadOnlyList<RainRoofDripSource> Active => Registered;

        [Tooltip("Edge length in metres, centered at this object's transform along local X.")]
        [Range(0.25f, 16f)] public float edgeLength = 2f;
        [Tooltip("Relative source strength; zero disables without removing the marker.")]
        [Range(0f, 2f)] public float dripAmount = 1f;
        [Tooltip("Keep a small amount of runoff during the first part of the drying phase.")]
        public bool continueAfterRain = true;
        [Tooltip("Only emit under weather if the component is enabled and visible within the quality budget.")]
        public bool affectsPreview = true;

        private void OnEnable()
        {
            if (!Registered.Contains(this)) Registered.Add(this);
        }

        private void OnDisable()
        {
            Registered.Remove(this);
        }

        private void OnDestroy()
        {
            Registered.Remove(this);
        }
    }
}
