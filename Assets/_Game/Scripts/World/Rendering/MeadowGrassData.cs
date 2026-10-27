using System;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>Derived presentation data baked by an editor tool; no gameplay or save state.</summary>
    public sealed class MeadowGrassData : ScriptableObject
    {
        [Serializable] public sealed class Patch
        {
            public Bounds bounds;
            public Matrix4x4[] matrices;
        }
        public Mesh[] lodMeshes;
        public Patch[] patches;
        public int authoredClumps;
    }
}
