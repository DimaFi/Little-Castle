using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    /// <summary>Reusable scene-authored link between two water levels. Never edits terrain or river records.</summary>
    [ExecuteAlways,DisallowMultipleComponent,AddComponentMenu("Little Castle/Water/Waterfall Link")]
    public sealed class WaterfallLinkPresentation:MonoBehaviour
    {
        public MountainBorderProfile visualProfile;
        [Tooltip("Positions in this component's local coordinates. Align these with existing river ends.")]
        public Vector3 upperLip=new Vector3(0,15,8),lowerLanding=Vector3.zero;
        [Range(1,40)] public float width=5.5f;
        public WaterfallVisualSettings settings=new WaterfallVisualSettings();
        [NonSerialized] public string lastError;
        WaterfallGeometryV1 geometry;WaterfallVisualEffects effects;bool pending;
        void OnEnable(){RenderPipelineManager.beginCameraRendering+=Draw;Rebuild();}
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=Draw;Release();}
        void OnValidate(){pending=true;}
        void Update(){if(pending){pending=false;Rebuild();}}
        [ContextMenu("Refresh waterfall")]
        public void Rebuild()
        {
            Release();lastError=null;if(!visualProfile||!visualProfile.waterfallMaterial)return;
            try
            {
                geometry=WaterfallGeometryV1.Between(upperLip,lowerLanding,width);
                effects=new WaterfallVisualEffects(transform,geometry,settings,visualProfile.waterfallMaterial,visualProfile.mistMaterial,
                    visualProfile.normalTexture,visualProfile.foamTexture,visualProfile.phaseTexture,visualProfile.seed);
            }
            catch(Exception e){lastError=e.Message;Release();Debug.LogException(e,this);}
        }
        void Draw(ScriptableRenderContext context,Camera camera){effects?.UpdatePresentation(camera,Time.realtimeSinceStartup);}
        public float Wetness(Vector3 worldPosition)=>effects?.QueryWetness(worldPosition)??0;
        public void Release(){effects?.Dispose();effects=null;geometry?.Dispose();geometry=null;}
    }
}
