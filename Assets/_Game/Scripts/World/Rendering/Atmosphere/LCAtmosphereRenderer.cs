using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering.Atmosphere
{
    /// <summary>
    /// Optional Built-in image effect for the strategy camera. Reuses the existing
    /// sun, fog color and _LC_* globals. Does NOT implement Tiny Glade's renderer,
    /// actual volumetric shadowing or traced indirect lighting.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Little Castle/Rendering/Atmosphere Renderer (Built-in)")]
    public sealed class LCAtmosphereRenderer : MonoBehaviour
    {
        private const int MaxVolumes = 4;
        private const string ShaderName = "Hidden/Little Castle/Atmosphere Fog";

        [Header("References")]
        [SerializeField] private LCAtmosphereProfile defaultProfile;
        [Tooltip("Usually the strategy camera's TestFocus (not its elevated position).")]
        [SerializeField] private Transform worldFocus;
        [Header("Safety switches")]
        [SerializeField] private bool replaceLegacyDistanceFog = true;
        [SerializeField] private bool enablePass = true;

        private Camera _camera;
        private Material _material;
        private LCAtmosphereProfile _temporaryProfile;
        private bool _restoreFog;
        private readonly Vector3[] _corners = new Vector3[4];
        private readonly Vector4[] _spheres = new Vector4[MaxVolumes];
        private readonly Vector4[] _volumeParams = new Vector4[MaxVolumes];
        private readonly LCAtmosphereFogVolume[] _selected = new LCAtmosphereFogVolume[MaxVolumes];
        private readonly float[] _selectedDistance = new float[MaxVolumes];
        private static bool _warnedUnsupported;

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            _camera.depthTextureMode |= DepthTextureMode.Depth;
            CreateMaterialIfNeeded();
        }

        private void CreateMaterialIfNeeded()
        {
            if (_material != null) return;
            // A Resources material makes this Hidden shader survive player-build stripping.
            Material template = Resources.Load<Material>("Atmosphere/LC_AtmosphereFog");
            Shader shader = template != null ? template.shader : Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported) return;
            _material = template != null ? new Material(template) : new Material(shader);
            _material.hideFlags = HideFlags.HideAndDontSave;
        }

        private LCAtmosphereProfile GetDefaultProfile()
        {
            if (defaultProfile != null) return defaultProfile;
            if (_temporaryProfile == null)
            {
                _temporaryProfile = ScriptableObject.CreateInstance<LCAtmosphereProfile>();
                _temporaryProfile.hideFlags = HideFlags.HideAndDontSave;
            }
            return _temporaryProfile;
        }

        private bool CanRun()
        {
            if (!enablePass || _camera == null || !_camera.enabled ||
                _camera.orthographic || GraphicsSettings.currentRenderPipeline != null ||
                !SystemInfo.supportsImageEffects) return false;
            CreateMaterialIfNeeded();
            return _material != null;
        }

        private void OnPreCull()
        {
            _restoreFog = false;
            // Only the currently rendered camera should suppress the legacy fog.
            // This avoids applying RenderSettings.fog AND our depth fog twice.
            if (CanRun() && replaceLegacyDistanceFog && RenderSettings.fog)
            {
                RenderSettings.fog = false;
                _restoreFog = true;
            }
        }

        private void OnPostRender()
        {
            RestoreFog();
        }

        private void RestoreFog()
        {
            if (!_restoreFog) return;
            RenderSettings.fog = true;
            _restoreFog = false;
        }

        private void OnDisable()
        {
            RestoreFog();
            DestroyOwned(_material);
            DestroyOwned(_temporaryProfile);
            _material = null;
            _temporaryProfile = null;
        }

        private static void DestroyOwned(Object asset)
        {
            if (asset == null) return;
            if (Application.isPlaying) Destroy(asset);
            else DestroyImmediate(asset);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!CanRun())
            {
                if (!_warnedUnsupported && enablePass)
                {
                    Debug.LogWarning(
                        "LC Atmosphere: requires a perspective Built-in camera and a supported Hidden/Little Castle/Atmosphere Fog shader. Falling back to unchanged image.",
                        this);
                    _warnedUnsupported = true;
                }
                Graphics.Blit(source, destination);
                return;
            }

            LCAtmosphereProfile baseline = GetDefaultProfile();
            LCAtmosphereProfile biome = baseline;
            float biomeWeight = 0f;
            int bestPriority = int.MinValue;
            Vector3 focus = worldFocus != null ? worldFocus.position : transform.position;

            var zones = LCAtmosphereZone.Active;
            for (int i = 0; i < zones.Count; i++)
            {
                LCAtmosphereZone zone = zones[i];
                if (zone == null || zone.profile == null) continue;
                float weight = zone.WeightAt(focus);
                if (weight <= 0f) continue;
                if (zone.priority > bestPriority ||
                    (zone.priority == bestPriority && weight > biomeWeight))
                {
                    biome = zone.profile;
                    biomeWeight = weight;
                    bestPriority = zone.priority;
                }
            }

            BindProfiles(baseline, biome, biomeWeight);
            BindFrustum();
            BindVolumes();
            Graphics.Blit(source, destination, _material, 0);
        }

        private void BindProfiles(LCAtmosphereProfile a, LCAtmosphereProfile b, float t)
        {
            float day = Mathf.Clamp01(Shader.GetGlobalFloat("_LC_Daylight") +
                Shader.GetGlobalFloat("_LC_Twilight") * 0.18f);
            Color dayTint = Color.Lerp(a.dayTint, b.dayTint, t);
            Color nightTint = Color.Lerp(a.nightTint, b.nightTint, t);
            Color tint = Color.Lerp(nightTint, dayTint, day);
            Color fogColor = RenderSettings.fogColor * tint;

            SetFloat("_LC_AF_Start", Mathf.Lerp(a.startDistance, b.startDistance, t));
            SetFloat("_LC_AF_DistanceDensity", Mathf.Lerp(a.distanceDensity, b.distanceDensity, t));
            SetFloat("_LC_AF_MaxDistance", Mathf.Lerp(a.maxRayDistance, b.maxRayDistance, t));
            SetFloat("_LC_AF_HeightBase", Mathf.Lerp(a.heightBase, b.heightBase, t));
            SetFloat("_LC_AF_HeightDensity", Mathf.Lerp(a.heightDensity, b.heightDensity, t));
            SetFloat("_LC_AF_HeightFalloff", Mathf.Lerp(a.heightFalloff, b.heightFalloff, t));
            SetFloat("_LC_AF_NoiseScale", Mathf.Lerp(a.noiseScale, b.noiseScale, t));
            SetFloat("_LC_AF_NoiseStrength", Mathf.Lerp(a.noiseStrength, b.noiseStrength, t));
            SetFloat("_LC_AF_NoiseSpeed", Mathf.Lerp(a.noiseSpeed, b.noiseSpeed, t));
            SetFloat("_LC_AF_SunScatter", Mathf.Lerp(a.sunScatter, b.sunScatter, t));
            SetFloat("_LC_AF_ForwardExponent", Mathf.Lerp(a.forwardExponent, b.forwardExponent, t));
            SetFloat("_LC_AF_Opacity", Mathf.Lerp(a.fogOpacity, b.fogOpacity, t));
            SetFloat("_LC_AF_Saturation", Mathf.Lerp(a.saturation, b.saturation, t));
            SetFloat("_LC_AF_Contrast", Mathf.Lerp(a.contrast, b.contrast, t));
            SetFloat("_LC_AF_Steps", Mathf.RoundToInt(Mathf.Lerp(a.sampleCount, b.sampleCount, t)));
            _material.SetColor("_LC_AF_FogColor", fogColor);
        }

        private void SetFloat(string id, float value) => _material.SetFloat(id, value);

        private void BindFrustum()
        {
            _camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f),
                _camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, _corners);
            // Unity order: bottom-left, top-left, top-right, bottom-right.
            Matrix4x4 rays = Matrix4x4.identity;
            Transform cameraTransform = _camera.transform;
            rays.SetRow(0, cameraTransform.TransformVector(_corners[0]));
            rays.SetRow(1, cameraTransform.TransformVector(_corners[3]));
            rays.SetRow(2, cameraTransform.TransformVector(_corners[2]));
            rays.SetRow(3, cameraTransform.TransformVector(_corners[1]));
            _material.SetMatrix("_LC_AF_FrustumCorners", rays);
            _material.SetVector("_LC_AF_CameraPosition", cameraTransform.position);
            _material.SetVector("_LC_AF_CameraForward", cameraTransform.forward);
        }

        private void BindVolumes()
        {
            var active = LCAtmosphereFogVolume.Active;
            Vector3 cameraPosition = transform.position;
            for (int n = 0; n < MaxVolumes; n++)
            {
                _selected[n] = null;
                _selectedDistance[n] = float.PositiveInfinity;
                _spheres[n] = Vector4.zero;
                _volumeParams[n] = Vector4.zero;
            }

            for (int i = 0; i < active.Count; i++)
            {
                LCAtmosphereFogVolume volume = active[i];
                if (volume == null || volume.density <= 0f) continue;
                float distance = Mathf.Max(0f,
                    Vector3.Distance(cameraPosition, volume.transform.position) - volume.radius);
                for (int slot = 0; slot < MaxVolumes; slot++)
                {
                    if (distance >= _selectedDistance[slot]) continue;
                    for (int shift = MaxVolumes - 1; shift > slot; shift--)
                    {
                        _selected[shift] = _selected[shift - 1];
                        _selectedDistance[shift] = _selectedDistance[shift - 1];
                    }
                    _selected[slot] = volume;
                    _selectedDistance[slot] = distance;
                    break;
                }
            }

            int count = 0;
            for (int i = 0; i < MaxVolumes; i++)
            {
                var v = _selected[i];
                if (v == null) continue;
                Vector3 center = v.transform.position;
                _spheres[i] = new Vector4(center.x, center.y, center.z, v.radius);
                _volumeParams[i] = new Vector4(v.density, v.edgeFeather, 0f, 0f);
                count++;
            }
            _material.SetFloat("_LC_AF_VolumeCount", count);
            _material.SetVectorArray("_LC_AF_Spheres", _spheres);
            _material.SetVectorArray("_LC_AF_VolumeParams", _volumeParams);
        }
    }
}
