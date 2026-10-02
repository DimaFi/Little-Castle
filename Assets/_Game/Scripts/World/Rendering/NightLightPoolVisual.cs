using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Cheap additive ground glow paired with a lantern/window/fire emitter.
    ///
    /// Uses one shared material plus MaterialPropertyBlock; it never clones
    /// materials at runtime. Day/night fade and subtle flicker remain in the
    /// LC Night Light Pool shader.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NightLightPoolVisual : MonoBehaviour
    {
        private static readonly int ColorId =
            Shader.PropertyToID("_Color");

        private static readonly int IntensityId =
            Shader.PropertyToID("_Intensity");

        private static readonly int InnerRadiusId =
            Shader.PropertyToID("_InnerRadius");

        private static readonly int EdgeSoftnessId =
            Shader.PropertyToID("_EdgeSoftness");

        private static readonly int FlickerStrengthId =
            Shader.PropertyToID("_FlickerStrength");

        private static readonly int FlickerSpeedId =
            Shader.PropertyToID("_FlickerSpeed");

        [SerializeField]
        private Renderer targetRenderer;

        [Header("Appearance")]
        [SerializeField]
        [ColorUsage(true, true)]
        private Color lightColor =
            new Color(
                1.35f,
                0.62f,
                0.16f,
                1f);

        [SerializeField]
        [Range(0f, 4f)]
        private float intensity = 1f;

        [SerializeField]
        [Range(0f, 0.95f)]
        private float innerRadius = 0.18f;

        [SerializeField]
        [Range(0.02f, 1f)]
        private float edgeSoftness = 0.62f;

        [SerializeField]
        [Range(0f, 0.25f)]
        private float flickerStrength = 0.025f;

        [SerializeField]
        [Min(0f)]
        private float flickerSpeed = 2.6f;

        [Header("Optional quad sizing")]
        [Tooltip(
            "If enabled, this dedicated glow child is uniformly sized from " +
            "diameterMeters. Leave off when the authored mesh already has its " +
            "final dimensions.")]
        [SerializeField]
        private bool autoScaleDedicatedGlowObject = false;

        [SerializeField]
        [Min(0.1f)]
        private float diameterMeters = 7f;

        private MaterialPropertyBlock propertyBlock;

        public bool IsPresentationVisible =>
            targetRenderer != null &&
            targetRenderer.enabled;

        public Renderer TargetRenderer =>
            targetRenderer;

        private void Awake()
        {
            ResolveRenderer();
            ApplyProperties();
        }

        private void OnEnable()
        {
            ResolveRenderer();
            ApplyProperties();
        }

        public void SetPresentationVisible(
            bool visible)
        {
            ResolveRenderer();

            if (targetRenderer != null &&
                targetRenderer.enabled != visible)
            {
                targetRenderer.enabled =
                    visible;
            }
        }

        public void ApplyProperties()
        {
            ResolveRenderer();

            if (targetRenderer == null)
                return;

            propertyBlock ??=
                new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(
                propertyBlock);

            propertyBlock.SetColor(
                ColorId,
                lightColor);

            propertyBlock.SetFloat(
                IntensityId,
                intensity);

            propertyBlock.SetFloat(
                InnerRadiusId,
                innerRadius);

            propertyBlock.SetFloat(
                EdgeSoftnessId,
                edgeSoftness);

            propertyBlock.SetFloat(
                FlickerStrengthId,
                flickerStrength);

            propertyBlock.SetFloat(
                FlickerSpeedId,
                flickerSpeed);

            targetRenderer.SetPropertyBlock(
                propertyBlock);

            if (autoScaleDedicatedGlowObject)
            {
                float diameter =
                    Mathf.Max(
                        0.1f,
                        diameterMeters);

                transform.localScale =
                    Vector3.one *
                    diameter;
            }
        }

        private void ResolveRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer =
                    GetComponent<Renderer>();
            }
        }

        private void OnValidate()
        {
            intensity =
                Mathf.Clamp(
                    intensity,
                    0f,
                    4f);

            innerRadius =
                Mathf.Clamp(
                    innerRadius,
                    0f,
                    0.95f);

            edgeSoftness =
                Mathf.Clamp(
                    edgeSoftness,
                    0.02f,
                    1f);

            flickerStrength =
                Mathf.Clamp(
                    flickerStrength,
                    0f,
                    0.25f);

            flickerSpeed =
                Mathf.Max(
                    0f,
                    flickerSpeed);

            diameterMeters =
                Mathf.Max(
                    0.1f,
                    diameterMeters);

            ResolveRenderer();

            if (!Application.isPlaying)
                ApplyProperties();
        }
    }
}
