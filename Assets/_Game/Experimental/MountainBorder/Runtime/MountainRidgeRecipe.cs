using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder
{
    /// <summary>
    /// Experimental decorative ridge inputs. Heights are absolute world Y;
    /// positions and widths are world X/Z metres. No gameplay state or scene references.
    /// </summary>
    [Serializable]
    public sealed class MountainRidgeRecipe
    {
        public int version = 1;
        public string id;
        public int seed = 1729;
        public float baseHeight;
        public float roughnessAmplitude;
        public float roughnessFrequency = 0.02f;
        public List<RidgeNode> nodes = new List<RidgeNode>();
        // Optional finite corridor which only lowers the decorative ridge mass.
        public MountainPass pass;
    }

    [Serializable]
    public sealed class RidgeNode
    {
        public string id;
        public Vector2 position;
        public float height = 30f;
        public float halfWidth = 20f;
    }

    [Serializable]
    public sealed class MountainPass
    {
        public string id;
        public Vector2 start;
        public Vector2 end;
        public float halfWidth = 4f;
        public float floorHeight;
        public float shoulderBlend = 8f;
    }
}
