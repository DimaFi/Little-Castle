using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.World
{
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class TerrainStarterSoftScene : MonoBehaviour
    {
        public Material effectMaterial;
        [Range(0,1)] public float blurStrength=.85f;
        [Range(0,1)] public float contactOcclusion=.28f;
        [Min(1)] public float focusBand=4;
        [Range(0,4)] public float shadowFilterRadius=3;
        private DepthTextureMode previousDepthMode;
        internal float FocusDistance => transform.position.y/Mathf.Max(.1f,-transform.forward.y)-3;
        private void OnEnable() { var camera=GetComponent<Camera>(); previousDepthMode=camera.depthTextureMode;
            camera.depthTextureMode |= DepthTextureMode.Depth;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering; }
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == GetComponent<Camera>()) Shader.SetGlobalFloat("_LC_ShadowFilterRadius",shadowFilterRadius);
        }
        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == GetComponent<Camera>()) Shader.SetGlobalFloat("_LC_ShadowFilterRadius",0);
        }
        private void OnPreCull() { Shader.SetGlobalFloat("_LC_ShadowFilterRadius",shadowFilterRadius); }
        private void OnPostRender() { Shader.SetGlobalFloat("_LC_ShadowFilterRadius",0); }
        private void OnDisable() { Shader.SetGlobalFloat("_LC_ShadowFilterRadius",0);
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            GetComponent<Camera>().depthTextureMode=previousDepthMode; }
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // URP uses TerrainStarterSoftSceneRendererFeature. Keep the Built-in fallback
            // available in this isolated copy without a second post-effect application.
            if(GraphicsSettings.currentRenderPipeline != null || effectMaterial == null)
            { Graphics.Blit(source,destination); return; }
            RenderTexture a=null, b=null;
            try
            {
                if(blurStrength>0) {
                a=RenderTexture.GetTemporary(Mathf.Max(1,source.width/2),Mathf.Max(1,source.height/2),0,source.format);
                b=RenderTexture.GetTemporary(a.width,a.height,0,source.format);
                effectMaterial.SetVector("_BlurAxis",Vector2.right);
                Graphics.Blit(source,a,effectMaterial,0);
                effectMaterial.SetVector("_BlurAxis",Vector2.up);
                Graphics.Blit(a,b,effectMaterial,0);
                effectMaterial.SetTexture("_BlurTex",b);
                } else effectMaterial.SetTexture("_BlurTex",source);
                effectMaterial.SetFloat("_FocusDistance",FocusDistance);
                effectMaterial.SetFloat("_FocusBand",focusBand);
                effectMaterial.SetFloat("_BlurStrength",blurStrength);
                effectMaterial.SetFloat("_ContactOcclusion",contactOcclusion);
                Graphics.Blit(source,destination,effectMaterial,1);
            }
            finally { if(a) RenderTexture.ReleaseTemporary(a); if(b) RenderTexture.ReleaseTemporary(b); }
        }
    }
}
