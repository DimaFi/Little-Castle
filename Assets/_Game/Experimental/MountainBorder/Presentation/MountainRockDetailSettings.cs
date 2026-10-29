using System;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    /// <summary>Plain presentation recipe, in the source mesh's world-metre coordinates.</summary>
    [Serializable]
    public sealed class MountainRockDetailSettings
    {
        [Min(.5f)] public float lod0Spacing = 2.8f;
        [Min(.5f)] public float lod1Spacing = 5.6f;
        [Min(.5f)] public float lod2Spacing = 11.2f;
        [Min(0f)] public float relief = 5.8f;
        [Min(4f)] public float rockWidth = 13f;
        [Min(2f)] public float ledgeHeight = 7f;
        [Range(1f, 89f)] public float normalCreaseAngle = 62f;
        [Min(0f)] public float controlPointRadius = 1.65f;
        public float macroControlMinimumZ = 72f;
        public Vector2 protectedWaterMinimum = new Vector2(-74f, -58f);
        public Vector2 protectedWaterMaximum = new Vector2(-24f, 72f);
        [Min(.1f)] public float protectedWaterFeather = 4f;
        public Vector3[] additionalLockedPoints = Array.Empty<Vector3>();
        [Min(1000)] public int maximumTriangles = 120000;

        public float Spacing(int lod)
        {
            if (lod < 0 || lod > 2) throw new ArgumentOutOfRangeException(nameof(lod));
            float value = lod == 0 ? lod0Spacing : lod == 1 ? lod1Spacing : lod2Spacing;
            if (!Finite(value) || value < .5f) throw new ArgumentException("LOD spacing must be finite and at least 0.5 metres.");
            return value;
        }

        internal void Validate()
        {
            Spacing(0); Spacing(1); Spacing(2);
            if (!Finite(relief) || relief < 0 || !Finite(rockWidth) || rockWidth < 4 ||
                !Finite(ledgeHeight) || ledgeHeight < 2 || !Finite(normalCreaseAngle) ||
                normalCreaseAngle <= 0 || normalCreaseAngle >= 90 || !Finite(controlPointRadius) ||
                controlPointRadius < 0 || !Finite(macroControlMinimumZ) ||
                !Finite(protectedWaterFeather) || protectedWaterFeather <= 0 || maximumTriangles < 1000 ||
                !Finite(protectedWaterMinimum.x) || !Finite(protectedWaterMinimum.y) ||
                !Finite(protectedWaterMaximum.x) || !Finite(protectedWaterMaximum.y) ||
                protectedWaterMinimum.x > protectedWaterMaximum.x || protectedWaterMinimum.y > protectedWaterMaximum.y)
                throw new ArgumentException("Invalid mountain detail recipe.");
            if (additionalLockedPoints == null) return;
            foreach (var p in additionalLockedPoints)
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z))
                    throw new ArgumentException("Locked points must be finite.");
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
