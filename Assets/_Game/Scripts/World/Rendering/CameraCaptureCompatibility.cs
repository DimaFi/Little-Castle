using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    /// <summary>Off-screen captures use a render request when a scriptable pipeline is active.</summary>
    public static class CameraCaptureCompatibility
    {
        public static void Render(Camera camera, RenderTexture destination)
        {
            if (GraphicsSettings.currentRenderPipeline == null) { camera.Render(); return; }
            var request = new RenderPipeline.StandardRequest { destination = destination };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new NotSupportedException("The active pipeline does not support camera capture requests.");
            RenderPipeline.SubmitRenderRequest(camera, request);
        }
    }
}
