using System;
using UnityEngine;

namespace LittleCastle.Rendering
{
    public enum MeadowScatterCategory
    {
        Flower = 0,
        Stone = 1,
        ForestDebris = 2,
        Grass = 3,
        Shrub = 4
    }

    /// <summary>Reusable presentation prototypes. Instance positions belong to painted meadow data.</summary>
    public sealed class MeadowScatterLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Prototype
        {
            public bool enabled = true;
            public string stableId;
            public string sourceAssetPath;
            public MeadowScatterCategory category = MeadowScatterCategory.Flower;
            public Mesh[] lodMeshes = new Mesh[4];
            public Material[] materials;
            public Bounds localBounds;
            public Vector2 scaleRange = new Vector2(.7f, 1f);
            [Min(0)] public float weight = 1;
            [Min(0)] public float groundInset;
            [Range(0, 60)] public float maximumSlope = 28;
            [Range(0, 20)] public float maximumTilt = 5;
            public Vector2 yawRange = new Vector2(0, 360);
            [Min(0)] public float clearanceRadius = .15f;
            public LittleCastle.World.BiomeMask allowedBiomes = LittleCastle.World.BiomeMask.All;
            public bool allowGrass = true, allowWoodlandFloor = true, allowMud;
        }

        public Prototype[] prototypes;
        [TextArea(4, 14)] public string preflightNotes;
    }
}
