using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Rendering.Atmosphere
{
    /// <summary>
    /// A horizontal biome rectangle. Its transform allows rotation, but does not
    /// affect gameplay biome data. A feathered boundary avoids color popping.
    /// </summary>
    public sealed class LCAtmosphereZone : MonoBehaviour
    {
        private static readonly List<LCAtmosphereZone> Registered =
            new List<LCAtmosphereZone>();

        public static IReadOnlyList<LCAtmosphereZone> Active => Registered;

        public LCAtmosphereProfile profile;
        [Min(1f)] public Vector2 size = new Vector2(220f, 220f);
        [Min(0f)] public float blendDistance = 60f;
        public int priority;

        public float WeightAt(Vector3 worldFocus)
        {
            return LCAtmosphereMath.ZoneWeight(worldFocus, transform, size, blendDistance);
        }

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
            Gizmos.color = new Color(0.55f, 0.78f, 1f, 0.7f);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 0f, size.y));
            Gizmos.matrix = previous;
        }
    }
}
