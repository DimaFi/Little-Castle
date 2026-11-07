using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    /// <summary>Scene-owned presentation for ordinary Play and Player startup.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(150)]
    public sealed class MapAtmospherePresentation : MonoBehaviour
    {
        [SerializeField] MeadowSurfacePainter painter;
        [SerializeField] DayNightLightingController atmosphere;
        [SerializeField] LandscapeBorderPresentation mountainBorder;
        [SerializeField] bool nearWeatherClouds = true;
        [SerializeField] bool distantValley = true;
        [SerializeField] LittleCastleOptionalAtmosphereStack.SkySource sky;
        [SerializeField] bool volumetricFog;
        [SerializeField] ScriptableRendererData experimentalRenderer;

        GameObject runtimeOwner;
        Camera presentationCamera;
        UniversalAdditionalCameraData cameraData;
        float savedFarClip;
        int savedRendererIndex;
        bool savedPostProcessing;
        LayerMask savedVolumeLayerMask;
        bool usingExperimentalRenderer;
        bool started;

        void Start()
        {
            started = true;
            CreatePresentation();
        }

        void OnEnable()
        {
            if (started) CreatePresentation();
        }

        void CreatePresentation()
        {
            if (!Application.isPlaying || !painter || !painter.source || !atmosphere ||
                painter.gameObject.scene != gameObject.scene || atmosphere.gameObject.scene != gameObject.scene)
                return;

            presentationCamera = painter.studyCamera;
            if (presentationCamera && presentationCamera.gameObject.scene != gameObject.scene)
                presentationCamera = null;

            runtimeOwner = new GameObject("Map atmosphere presentation") { hideFlags = HideFlags.DontSave };
            runtimeOwner.transform.SetParent(transform, false);
            runtimeOwner.SetActive(false);

            if (presentationCamera)
            {
                savedFarClip = presentationCamera.farClipPlane;
                if (nearWeatherClouds || distantValley)
                    presentationCamera.farClipPlane = Mathf.Max(savedFarClip, 960f);
            }

            if (nearWeatherClouds)
                runtimeOwner.AddComponent<NearWeatherCloudPreview>().Connect(painter, atmosphere);
            if (distantValley)
                runtimeOwner.AddComponent<DistantValleyPreview>().Connect(painter,
                    mountainBorder && mountainBorder.gameObject.scene == gameObject.scene ? mountainBorder : null);

            bool wantsExperimental = volumetricFog ||
                sky == LittleCastleOptionalAtmosphereStack.SkySource.PhysicallyBased;
            if (wantsExperimental && TryUseExperimentalRenderer())
                runtimeOwner.AddComponent<LittleCastleOptionalAtmosphereStack>().Configure(atmosphere, sky, volumetricFog);
            else if (wantsExperimental)
                Debug.LogWarning("Optional map sky/fog needs the atmosphere renderer in the active URP asset.", this);

            runtimeOwner.SetActive(true);
        }

        bool TryUseExperimentalRenderer()
        {
            if (!presentationCamera ||
                !presentationCamera.TryGetComponent(out cameraData) ||
                cameraData.renderType != CameraRenderType.Base ||
                GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset pipeline)
                return false;

            int experimentalIndex = FindRendererIndex(pipeline, experimentalRenderer);
            if (experimentalIndex < 0) return false;

            var originalRenderer = cameraData.scriptableRenderer;
            savedRendererIndex = -1;
            if (originalRenderer != pipeline.GetRenderer(-1))
            {
                var renderers = pipeline.rendererDataList;
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] && originalRenderer == pipeline.GetRenderer(i))
                    {
                        savedRendererIndex = i;
                        break;
                    }
            }
            savedPostProcessing = cameraData.renderPostProcessing;
            savedVolumeLayerMask = cameraData.volumeLayerMask;
            cameraData.SetRenderer(experimentalIndex);
            if (volumetricFog) cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = savedVolumeLayerMask.value | (1 << runtimeOwner.layer);
            usingExperimentalRenderer = true;
            return true;
        }

        public static int FindRendererIndex(UniversalRenderPipelineAsset pipeline, ScriptableRendererData renderer)
        {
            if (!pipeline || !renderer) return -1;
            var renderers = pipeline.rendererDataList;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] == renderer) return i;
            return -1;
        }

        void OnDisable()
        {
            if (runtimeOwner)
            {
                runtimeOwner.SetActive(false);
                Destroy(runtimeOwner);
                runtimeOwner = null;
            }
            if (presentationCamera)
                presentationCamera.farClipPlane = savedFarClip;
            if (usingExperimentalRenderer && cameraData)
            {
                cameraData.SetRenderer(savedRendererIndex);
                cameraData.renderPostProcessing = savedPostProcessing;
                cameraData.volumeLayerMask = savedVolumeLayerMask;
            }
            presentationCamera = null;
            cameraData = null;
            usingExperimentalRenderer = false;
        }
    }
}
