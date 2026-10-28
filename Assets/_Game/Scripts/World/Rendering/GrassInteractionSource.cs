using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>Optional visual source on a unit/prop; no grass colliders or simulation ownership.</summary>
    public sealed class GrassInteractionSource : MonoBehaviour
    {
        internal static readonly List<GrassInteractionSource> Active = new List<GrassInteractionSource>();
        [Min(.01f)] public float radius = .9f;
        [Range(0,1)] public float bend = 1;
        public float heightOffset;
        void OnEnable() { if(!Active.Contains(this))Active.Add(this); }
        void OnDisable() { Active.Remove(this); }
    }
}
