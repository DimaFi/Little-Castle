using System;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    public enum WaterfallVisualQuality { Off, Low, Medium, High }

    /// <summary>Local presentation only. No field participates in river state or terrain carving.</summary>
    [Serializable]
    public sealed class WaterfallVisualSettings
    {
        public WaterfallVisualQuality quality = WaterfallVisualQuality.Medium;
        [Range(.08f, .9f)] public float thickness = .48f;
        [Range(.2f, 2f)] public float normalStrength = .52f;
        [Range(1f, 12f)] public float flowSpeed = 5.2f;
        [Range(0f, 1.5f)] public float foamStrength = .9f;
        [Range(0f, 1f)] public float sprayAmount = .75f;
        [Range(0f, .5f)] public float mistOpacity = .13f;
        [Min(20f)] public float nearDistance = 75f;
        [Min(30f)] public float farDistance = 150f;
        [Range(1f, 8f)] public float wetRockRadius = 4.5f;
        public Color waterColor = new Color(.13f, .35f, .38f, 1f);
        public Color foamColor = new Color(.76f, .85f, .82f, 1f);

        public WaterfallVisualBudget Budget
        {
            get
            {
                switch (quality)
                {
                    case WaterfallVisualQuality.Low: return new WaterfallVisualBudget(3, 32, 8, 24, 16, 0);
                    case WaterfallVisualQuality.Medium: return new WaterfallVisualBudget(5, 56, 8, 64, 40, 10);
                    case WaterfallVisualQuality.High: return new WaterfallVisualBudget(7, 80, 10, 96, 64, 16);
                    default: return new WaterfallVisualBudget(0, 0, 0, 0, 0, 0);
                }
            }
        }
    }

    public readonly struct WaterfallVisualBudget
    {
        public readonly int strands, rows, radialSegments, sprayParticles, droplets, mistParticles;
        public int MaxTriangles => strands * rows * radialSegments * 2;
        public int MaxParticles => sprayParticles + droplets + mistParticles;
        public WaterfallVisualBudget(int strands, int rows, int radialSegments, int spray, int drops, int mist)
        {
            this.strands = strands; this.rows = rows; this.radialSegments = radialSegments;
            sprayParticles = spray; droplets = drops; mistParticles = mist;
        }
    }
}
