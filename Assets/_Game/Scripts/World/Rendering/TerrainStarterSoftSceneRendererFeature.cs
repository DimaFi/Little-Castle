using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.World
{
    /// <summary>
    /// URP RenderGraph implementation of the camera's existing Soft Scene effect.
    /// Add once to the project-owned Universal Renderer; settings stay on the camera.
    /// </summary>
    [DisallowMultipleRendererFeature("Little Castle Soft Scene")]
    public sealed class TerrainStarterSoftSceneRendererFeature : ScriptableRendererFeature
    {
        private SoftScenePass pass;

        public override void Create()
        {
            pass = new SoftScenePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            var camera = cameraData.camera;
            if (!(renderer is UniversalRenderer) || cameraData.cameraType != CameraType.Game || camera.stereoEnabled ||
                cameraData.renderType != CameraRenderType.Base || !cameraData.resolveFinalTarget ||
                !camera.TryGetComponent<TerrainStarterSoftScene>(out var settings) ||
                !settings.isActiveAndEnabled || settings.effectMaterial == null)
                return;

            // A transient replacement cameraColor is safe only for standalone cameras.
            // Stacked cameras require a separate depth/ownership policy; never apply twice.
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalData) &&
                additionalData.cameraStack != null && additionalData.cameraStack.Count != 0)
                return;

            var material = settings.effectMaterial;
            if (material.shader == null || material.shader.name != "Little Castle/Presentation/Soft Scene" ||
                material.FindPass("SoftSceneBlur") != 0 || material.FindPass("SoftSceneComposite") != 1)
                return;

            pass.Setup(settings);
            renderer.EnqueuePass(pass);
        }

        private sealed class SoftScenePass : ScriptableRenderPass
        {
            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int SourceTexelSizeId = Shader.PropertyToID("_LCSoftSceneSourceTexelSize");
            private static readonly int ScreenTexelSizeId = Shader.PropertyToID("_LCSoftSceneScreenTexelSize");
            private static readonly int BlurTextureId = Shader.PropertyToID("_BlurTex");
            private static readonly int BlurScaleBiasId = Shader.PropertyToID("_LCSoftSceneBlurScaleBias");
            private static readonly int DepthTextureId = Shader.PropertyToID("_LCSoftSceneDepthTexture");
            private static readonly int DepthScaleBiasId = Shader.PropertyToID("_LCSoftSceneDepthScaleBias");
            private static readonly int BlurAxisId = Shader.PropertyToID("_BlurAxis");
            private static readonly int FocusDistanceId = Shader.PropertyToID("_FocusDistance");
            private static readonly int FocusBandId = Shader.PropertyToID("_FocusBand");
            private static readonly int BlurStrengthId = Shader.PropertyToID("_BlurStrength");
            private static readonly int ContactOcclusionId = Shader.PropertyToID("_ContactOcclusion");
            private static readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
            private Material material;
            private float focusDistance, focusBand, blurStrength, contactOcclusion;

            public SoftScenePass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public void Setup(TerrainStarterSoftScene settings)
            {
                material = settings.effectMaterial;
                focusDistance = settings.FocusDistance;
                focusBand = settings.focusBand;
                blurStrength = settings.blurStrength;
                contactOcclusion = settings.contactOcclusion;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (material == null || resources.isActiveTargetBackBuffer ||
                    !resources.activeColorTexture.IsValid() || !resources.cameraDepthTexture.IsValid())
                    return;

                var source = resources.activeColorTexture;
                var descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = "LC Soft Scene Color";
                descriptor.clearBuffer = false;
                descriptor.msaaSamples = MSAASamples.None;
                descriptor.bindTextureMS = false;
                descriptor.filterMode = FilterMode.Bilinear;
                descriptor.wrapMode = TextureWrapMode.Clamp;
                var destination = renderGraph.CreateTexture(descriptor);
                var blurred = source;

                if (blurStrength > 0)
                {
                    // Match the old full -> half horizontal, half -> half vertical blur.
                    descriptor.sizeMode = TextureSizeMode.Explicit;
                    descriptor.width = Mathf.Max(1, cameraData.cameraTargetDescriptor.width / 2);
                    descriptor.height = Mathf.Max(1, cameraData.cameraTargetDescriptor.height / 2);
                    descriptor.name = "LC Soft Scene Blur Horizontal";
                    var horizontal = renderGraph.CreateTexture(descriptor);
                    descriptor.name = "LC Soft Scene Blur Vertical";
                    blurred = renderGraph.CreateTexture(descriptor);
                    AddPass(renderGraph, "LC Soft Scene Horizontal", source, horizontal,
                        TextureHandle.nullHandle, TextureHandle.nullHandle, Vector2.right, 0, Vector4.zero);
                    AddPass(renderGraph, "LC Soft Scene Vertical", horizontal, blurred,
                        TextureHandle.nullHandle, TextureHandle.nullHandle, Vector2.up, 0, Vector4.zero);
                }

                var width = Mathf.Max(1, cameraData.cameraTargetDescriptor.width);
                var height = Mathf.Max(1, cameraData.cameraTargetDescriptor.height);
                AddPass(renderGraph, "LC Soft Scene Composite", source, destination, blurred,
                    resources.cameraDepthTexture, Vector2.zero, 1,
                    new Vector4(1f / width, 1f / height, width, height));

                // The renderer's following/final pass consumes this result. No in-place blit.
                resources.cameraColor = destination;
            }

            private void AddPass(RenderGraph renderGraph, string name, TextureHandle source,
                TextureHandle destination, TextureHandle blur, TextureHandle depth,
                Vector2 axis, int shaderPass, Vector4 screenTexelSize)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(name, out var data))
                {
                    data.source = source;
                    data.destination = destination;
                    data.blur = blur;
                    data.depth = depth;
                    data.axis = axis;
                    data.shaderPass = shaderPass;
                    data.material = material;
                    data.focusDistance = focusDistance;
                    data.focusBand = focusBand;
                    data.blurStrength = blurStrength;
                    data.contactOcclusion = contactOcclusion;
                    data.screenTexelSize = screenTexelSize;
                    builder.UseTexture(source, AccessFlags.Read);
                    if (blur.IsValid() && !blur.Equals(source))
                        builder.UseTexture(blur, AccessFlags.Read);
                    if (depth.IsValid())
                        builder.UseTexture(depth, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (PassData passData, RasterGraphContext context) =>
                    {
                        RTHandle sourceTexture = passData.source;
                        Properties.Clear();
                        Properties.SetTexture(BlitTextureId, sourceTexture);
                        Properties.SetVector(BlitScaleBiasId, ScaleBias(context, passData.source, passData.destination));
                        Properties.SetVector(SourceTexelSizeId, new Vector4(
                            1f / sourceTexture.rt.width, 1f / sourceTexture.rt.height,
                            sourceTexture.rt.width, sourceTexture.rt.height));
                        Properties.SetVector(BlurAxisId, passData.axis);
                        if (passData.shaderPass == 1)
                        {
                            RTHandle blurTexture = passData.blur;
                            RTHandle depthTexture = passData.depth;
                            Properties.SetTexture(BlurTextureId, blurTexture);
                            Properties.SetTexture(DepthTextureId, depthTexture);
                            Properties.SetVector(BlurScaleBiasId, ScaleBias(context, passData.blur, passData.destination));
                            Properties.SetVector(DepthScaleBiasId, ScaleBias(context, passData.depth, passData.destination));
                            Properties.SetVector(ScreenTexelSizeId, passData.screenTexelSize);
                            Properties.SetFloat(FocusDistanceId, passData.focusDistance);
                            Properties.SetFloat(FocusBandId, passData.focusBand);
                            Properties.SetFloat(BlurStrengthId, passData.blurStrength);
                            Properties.SetFloat(ContactOcclusionId, passData.contactOcclusion);
                        }
                        // Same procedural triangle + Blit.hlsl contract as URP's Full Screen
                        // Pass feature; the MPB snapshots each draw without mutating shared materials.
                        context.cmd.DrawProcedural(Matrix4x4.identity, passData.material,
                            passData.shaderPass, MeshTopology.Triangles, 3, 1, Properties);
                    });
                }
            }

            private static Vector4 ScaleBias(RasterGraphContext context, TextureHandle source, TextureHandle destination)
            {
                // Public RenderGraph orientation API, equivalent to URP's internal final-blit helper.
                RTHandle texture = source;
                var scale = texture.useScaling
                    ? new Vector2(texture.rtHandleProperties.rtHandleScale.x, texture.rtHandleProperties.rtHandleScale.y)
                    : Vector2.one;
                return context.GetTextureUVOrigin(source) != context.GetTextureUVOrigin(destination)
                    ? new Vector4(scale.x, -scale.y, 0, scale.y)
                    : new Vector4(scale.x, scale.y, 0, 0);
            }

            private sealed class PassData
            {
                public TextureHandle source, destination, blur, depth;
                public Material material;
                public int shaderPass;
                public Vector2 axis;
                public Vector4 screenTexelSize;
                public float focusDistance, focusBand, blurStrength, contactOcclusion;
            }
        }
    }
}
