using UnityEngine;

namespace LittleCastle.Rendering
{
    public enum RiverEffectsQuality { Off, Low, Medium, High }

    [CreateAssetMenu(menuName="Little Castle/Rendering/River Visual Quality")]
    public sealed class RiverVisualQualityProfile : ScriptableObject
    {
        public RiverEffectsQuality quality=RiverEffectsQuality.Medium;
        [Range(0,1)] public float shorelineFoam=.6f, obstacleFoam=.7f, rapids=.35f, wetRock=.55f;
        [Min(.05f)] public float shorelineDepth=.5f;
        [Range(.1f,3)] public float animationSpeed=1;
        [ColorUsage(false,false)] public Color foamColor=new Color(.73f,.85f,.81f,1);
        [Tooltip("Maximum overlay triangles per tile. Water remains unchanged when exceeded.")]
        [Min(0)] public int maximumTrianglesPerTile=8192;
        [Tooltip("Maximum rock wakes per tile, additionally limited by quality.")]
        [Range(0,8)] public int maximumObstaclesPerTile=8;
        public int ObstacleLimit => quality==RiverEffectsQuality.Off?0:Mathf.Min(Mathf.Clamp(maximumObstaclesPerTile,0,8),quality==RiverEffectsQuality.Low?2:quality==RiverEffectsQuality.Medium?4:8);
        public int RevisionHash()
        {
            unchecked { int h=(int)quality; h=h*31+shorelineFoam.GetHashCode();h=h*31+obstacleFoam.GetHashCode();h=h*31+rapids.GetHashCode();h=h*31+wetRock.GetHashCode();h=h*31+shorelineDepth.GetHashCode();h=h*31+animationSpeed.GetHashCode();h=h*31+foamColor.GetHashCode();h=h*31+maximumTrianglesPerTile;return h*31+maximumObstaclesPerTile; }
        }
    }
}
