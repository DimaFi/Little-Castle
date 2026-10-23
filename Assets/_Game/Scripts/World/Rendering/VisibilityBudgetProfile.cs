using UnityEngine;

namespace LittleCastle.Rendering
{
    [CreateAssetMenu(
        fileName = "VisibilityBudgetProfile",
        menuName = "Little Castle/Rendering/Visibility Budget Profile")]
    public sealed class VisibilityBudgetProfile : ScriptableObject
    {
        [SerializeField]
        private VisibilityBudgetSettings settings =
            VisibilityBudgetSettings.Default;

        public VisibilityBudgetSettings Settings =>
            settings.Sanitized();

        private void Reset()
        {
            settings =
                VisibilityBudgetSettings.Default;
        }

        private void OnValidate()
        {
            settings =
                settings.Sanitized();
        }
    }
}
