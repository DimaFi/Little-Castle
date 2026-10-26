using UnityEngine;

namespace LittleCastle.World.Environment
{
    /// <summary>
    /// Editor palette metadata, independent of generated World.BiomeKind.
    /// Theme IDs are stable save keys; visuals/foliage rules will extend this
    /// asset when the existing renderer is integrated.
    /// </summary>
    [CreateAssetMenu(fileName = "BiomeTheme", menuName = "Little Castle/Environment/Biome Theme")]
    public sealed class BiomeThemeProfile : ScriptableObject
    {
        [Min(1)] [SerializeField] private int themeId = EnvironmentThemeIds.Winter;
        [SerializeField] private string displayName = "Winter";
        [SerializeField] private Color mapOverlayColor =
            new Color(0.72f, 0.85f, 1f, 0.55f);
        [TextArea] [SerializeField] private string description;

        public int ThemeId => themeId;
        public string DisplayName => string.IsNullOrEmpty(displayName)
            ? name : displayName;
        public Color MapOverlayColor => mapOverlayColor;
        public string Description => description;

#if UNITY_EDITOR
        private void OnValidate()
        {
            themeId = Mathf.Max(1, themeId);
        }
#endif
    }
}
