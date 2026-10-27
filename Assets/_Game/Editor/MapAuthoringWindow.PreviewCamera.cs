using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        struct WaterCameraRequest
        {
            public UniversalAdditionalCameraData data;
            public CameraOverrideOption depth,color;
        }
        bool previewNeedsWaterBuffers;
        readonly Dictionary<Camera,WaterCameraRequest> waterCameraRequests=new Dictionary<Camera,WaterCameraRequest>();
        void ConfigureWaterPreviewCameras()
        {
            if(!previewNeedsWaterBuffers||EditorApplication.isPlaying)return;
            foreach(SceneView view in SceneView.sceneViews)
            {
                var camera=view.camera;if(!camera||waterCameraRequests.ContainsKey(camera))continue;
                var data=camera.GetUniversalAdditionalCameraData();
                waterCameraRequests.Add(camera,new WaterCameraRequest{data=data,depth=data.requiresDepthOption,color=data.requiresColorOption});
                data.requiresDepthOption=CameraOverrideOption.On;data.requiresColorOption=CameraOverrideOption.On;
            }
        }
        void RestoreWaterPreviewCameras()
        {
            foreach(var request in waterCameraRequests.Values)if(request.data)
            {request.data.requiresDepthOption=request.depth;request.data.requiresColorOption=request.color;}
            waterCameraRequests.Clear();previewNeedsWaterBuffers=false;
        }
    }
}
