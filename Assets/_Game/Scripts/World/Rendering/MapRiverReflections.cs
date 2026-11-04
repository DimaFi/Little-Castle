using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace LittleCastle.Rendering
{
    /// <summary>Bounded, per-plane reflection slots. No shared-height global texture.</summary>
    public sealed class MapRiverReflections:IDisposable
    {
        sealed class Slot {public Camera camera;public RenderTexture texture;public float height;public Matrix4x4 vp;public int frame=-1;}
        readonly MapRiverPresentation view;
        readonly Slot[] slots={new Slot(),new Slot()};
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        readonly Dictionary<Camera,(CameraOverrideOption depth,CameraOverrideOption color)> cameraOptions=new Dictionary<Camera,(CameraOverrideOption,CameraOverrideOption)>();
        static bool rendering;
        public int RenderedSlots {get;private set;}
        public int LastFrame {get;private set;}=-1;
        public int Generation {get;private set;}
        public bool IndependentSlots=>slots[0].camera&&slots[1].camera&&slots[0].camera!=slots[1].camera&&slots[0].texture!=slots[1].texture;
        public RenderTexture[] Textures=>slots.Take(RenderedSlots).Select(s=>s.texture).ToArray();
        public float[] Heights=>slots.Take(RenderedSlots).Select(s=>s.height).ToArray();
        public MapRiverReflections(MapRiverPresentation view){this.view=view;RenderPipelineManager.beginCameraRendering+=Begin;}
        static bool IsSurface(Renderer r)=>r&&r.sharedMaterial&&r.sharedMaterial.HasProperty("_RiverWaveStrength");
        public static int Budget(RiverEffectsQuality quality)=>quality>=RiverEffectsQuality.High?2:quality==RiverEffectsQuality.Medium?1:0;
        // Capturing only some visible planes makes adjacent river tiles alternate between
        // planar reflections and a sky fallback. Do not expose that hard boundary.
        public static bool CanCaptureCoherently(int visiblePlanes, int budget, bool requireAll)
        {
            return visiblePlanes > 0 && budget > 0 && (!requireAll || visiblePlanes <= budget);
        }
        void Begin(ScriptableRenderContext context,Camera camera)
        {
            if(rendering||!camera||camera.cameraType==CameraType.Reflection||camera.cameraType==CameraType.Preview||(camera.cullingMask&(1<<4))==0)return;
            var renderers=view.WaterRenderers.Where(IsSurface).ToArray();
            if(renderers.Length==0)
            {
                // A resident fall can outlive its river tiles at the distance boundary.
                // Neither end may keep advertising a capture from the previous camera/frame.
                RenderedSlots=0;
                foreach(var renderer in view.Waterfalls.Renderers.Where(IsSurface))
                {renderer.GetPropertyBlock(block);block.SetFloat("_RiverReflectionValid",0);block.SetFloat("_RiverLipReflectionValid",0);renderer.SetPropertyBlock(block);}
                return;
            }
            var data=camera.GetUniversalAdditionalCameraData();
            if(!cameraOptions.ContainsKey(camera))cameraOptions.Add(camera,(data.requiresDepthOption,data.requiresColorOption));
            // Camera-local requests; shared URP pipeline assets are never modified.
            data.requiresDepthTexture=true;data.requiresColorTexture=true;
            int budget=Budget(view.EffectsProfile.quality);RenderedSlots=0;LastFrame=Time.frameCount;Generation++;
            var frustum=GeometryUtility.CalculateFrustumPlanes(camera);var candidates=new List<(float height,float priority)>();
            foreach(var renderer in renderers)
            {
                if(!GeometryUtility.TestPlanesAABB(frustum,renderer.bounds)||!TryPlane(renderer,out float height)||camera.transform.position.y<=height+.02f)continue;
                float distance=renderer.bounds.SqrDistance(camera.transform.position);
                int index=candidates.FindIndex(c=>Mathf.Abs(c.height-height)<.06f);
                if(index<0)candidates.Add((height,distance));else if(distance<candidates[index].priority)candidates[index]=(height,distance);
            }
            try
            {
                rendering=true;
                // Avoid a conspicuous rectangular patchwork when the scene has more
                // distinct visible water levels than our bounded reflection slots.
                // In that case every tile shares the same consistent fallback.
                if (CanCaptureCoherently(candidates.Count, budget, view.EffectsProfile.coherentPlanarReflections))
                {
                    foreach(var candidate in candidates.OrderBy(c=>c.priority).Take(budget))
                    {
                        var slot=slots[RenderedSlots];Render(slot,camera,candidate.height);slot.frame=Time.frameCount;RenderedSlots++;
                    }
                }
            }
            catch(Exception e){RenderedSlots=0;Debug.LogException(e);}
            finally{rendering=false;}
            foreach(var renderer in renderers)
            {
                bool planar=TryPlane(renderer,out float height);Apply(renderer,planar?height:float.NaN,true);
            }
            foreach(var renderer in view.Waterfalls.Renderers.Where(IsSurface))
            {
                renderer.GetPropertyBlock(block);float landing=block.GetVector("_ImpactPosition").y;
                float lip=block.GetVector("_UpstreamFlowDirection").w>.5f?block.GetVector("_LipPosition").y:float.NaN;
                Apply(renderer,landing,true,lip);
            }
        }
        // The bounds include the bed. Derive the plane from exposed top vertices only.
        bool TryPlane(Renderer renderer,out float height)
        {
            renderer.GetPropertyBlock(block);var plane=block.GetVector("_RiverPlane");height=plane.x;return plane.y>.5f;
        }
        void Apply(Renderer renderer,float height,bool opaque,float lipHeight=float.NaN)
        {
            renderer.GetPropertyBlock(block);block.SetFloat("_RiverReflectionValid",0);block.SetFloat("_RiverOpaqueAvailable",opaque?1:0);
            block.SetFloat("_RiverLipReflectionValid",0);
            for(int i=0;i<RenderedSlots;i++)if(slots[i].frame==Time.frameCount&&Mathf.Abs(slots[i].height-height)<.08f)
            {block.SetFloat("_RiverReflectionValid",1);block.SetTexture("_RiverReflection",slots[i].texture);block.SetMatrix("_RiverReflectionVP",slots[i].vp);break;}
            for(int i=0;i<RenderedSlots;i++)if(slots[i].frame==Time.frameCount&&Mathf.Abs(slots[i].height-lipHeight)<.08f)
            {block.SetFloat("_RiverLipReflectionValid",1);block.SetTexture("_RiverLipReflection",slots[i].texture);block.SetMatrix("_RiverLipReflectionVP",slots[i].vp);break;}
            var p=view.EffectsProfile;block.SetVector("_Effects",p.quality==RiverEffectsQuality.Off?Vector4.zero:new Vector4(p.shorelineFoam,p.obstacleFoam,p.rapids,p.shorelineDepth));
            block.SetFloat("_RiverBackgroundContribution",Mathf.Clamp01(p.refractionBackground));
            renderer.SetPropertyBlock(block);
        }
        static void Render(Slot slot,Camera source,float height)
        {
            if(!slot.camera)
            {
                var root=new GameObject("Bounded river reflection slot"){hideFlags=HideFlags.HideAndDontSave};slot.camera=root.AddComponent<Camera>();slot.camera.enabled=false;
            }
            int width=Mathf.Clamp(source.pixelWidth/2,128,1024),h=Mathf.Clamp(source.pixelHeight/2,128,1024);
            if(!slot.texture||slot.texture.width!=width||slot.texture.height!=h)
            {
                if(slot.texture){slot.texture.Release();Destroy(slot.texture);}
                slot.texture=new RenderTexture(width,h,24,RenderTextureFormat.ARGBHalf){name="River plane reflection",hideFlags=HideFlags.HideAndDontSave};slot.texture.Create();
            }
            var camera=slot.camera;camera.CopyFrom(source);camera.enabled=false;camera.cameraType=CameraType.Reflection;camera.useOcclusionCulling=false;camera.cullingMask=source.cullingMask&~(1<<4);camera.targetTexture=slot.texture;
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.requiresColorTexture=false;data.requiresDepthTexture=false;data.renderShadows=false;
            Matrix4x4 reflection=Matrix4x4.identity;reflection.m11=-1;reflection.m13=2*height;
            camera.transform.position=reflection.MultiplyPoint(source.transform.position);
            camera.transform.rotation=Quaternion.LookRotation(reflection.MultiplyVector(source.transform.forward),reflection.MultiplyVector(source.transform.up));
            camera.worldToCameraMatrix=source.worldToCameraMatrix*reflection;
            var point=camera.worldToCameraMatrix.MultiplyPoint(new Vector3(0,height+.025f,0));var normal=camera.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
            var plane=new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(point,normal));
            camera.projectionMatrix=source.projectionMatrix;camera.projectionMatrix=camera.CalculateObliqueMatrix(plane);
            slot.height=height;slot.vp=GL.GetGPUProjectionMatrix(camera.projectionMatrix,true)*camera.worldToCameraMatrix;
            bool previous=GL.invertCulling;
            try{GL.invertCulling=!previous;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=slot.texture});}
            finally{GL.invertCulling=previous;}
        }
        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering-=Begin;
            foreach(var pair in cameraOptions)if(pair.Key&&pair.Key.TryGetComponent<UniversalAdditionalCameraData>(out var data))
            {if(data.requiresDepthOption==CameraOverrideOption.On)data.requiresDepthOption=pair.Value.depth;if(data.requiresColorOption==CameraOverrideOption.On)data.requiresColorOption=pair.Value.color;}
            cameraOptions.Clear();
            foreach(var slot in slots){if(slot.camera)Destroy(slot.camera.gameObject);if(slot.texture){slot.texture.Release();Destroy(slot.texture);}}
        }
        static void Destroy(Object obj){if(Application.isPlaying)Object.Destroy(obj);else Object.DestroyImmediate(obj);}
    }
}
