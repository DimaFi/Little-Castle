using UnityEngine;

namespace LittleCastle.Rendering.Fireflies
{
    /// <summary>
    /// Optional global opt-in. Create in Resources/Fireflies/GlobalFireflies.asset
    /// through Firefly Preview window. Absence or disabled state has zero runtime effect.
    /// Profile is shared READ-ONLY with the scene renderer.
    /// </summary>
    [CreateAssetMenu(menuName="Little Castle/Fireflies/Global Automatic Settings",
        fileName="GlobalFireflies")]
    public sealed class FireflyGlobalSettings : ScriptableObject
    {
        [Tooltip("One switch: automatic fireflies on all loaded playable maps.")]
        public bool enabledEverywhere;
        [Tooltip("Independent authored profile: ecology weights, appearance, areas, bounds.")]
        public FireflyPreviewProfile profile;
        [Tooltip("Direct shader reference prevents preview shader stripping in builds.")]
        public Shader previewShader;
        public FireflyPreviewBudget quality = new FireflyPreviewBudget();

        private void OnValidate()
        {
            if(quality==null)quality=new FireflyPreviewBudget();
        }
    }
}
