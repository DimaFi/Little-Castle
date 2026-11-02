using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>Decorative envelope above the authoritative mean level; not a fluid simulation.</summary>
    public static class MapRiverWaves
    {
        public const float MaximumDisplacement=.048f;
        // Same vertical Gerstner components as LC_RiverWaves.hlsl. No horizontal chop.
        public static float Displacement(Vector2 p,float seconds)
        {
            float k1=6.283185f/3,k2=6.283185f/1.7f;
            return .030f*Mathf.Cos(p.x*k1-Mathf.Sqrt(9.8f*k1)*seconds*.5f)
                +.018f*Mathf.Cos(Vector2.Dot(p,new Vector2(.34f,.94f).normalized)*k2-Mathf.Sqrt(9.8f*k2)*seconds*.6f);
        }
    }
}
