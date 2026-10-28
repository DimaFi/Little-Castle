using LittleCastle.Rendering;
using UnityEngine;

namespace LittleCastle.Research
{
    /// <summary>Feeds resident river planes to the original Uber reflection manager and volume.</summary>
    [DefaultExecutionOrder(-5)]
    public sealed class UberRiverReflectionAdapter : MonoBehaviour
    {
        public MeadowSurfacePainter painter;
        [Range(.1f,1)] public float reflectionScale=.5f;
        GameObject plane;
        MeshRenderer target;
        PlanarReflectionVolume volume;
        MaterialPropertyBlock properties;
        void Awake(){properties=new MaterialPropertyBlock();}
        void LateUpdate()
        {
            if(!painter||painter.RiverView==null||!painter.studyCamera){DisableReflection();return;}
            var material=painter.RiverView.WaterMaterial;
            if(!material||!material.IsKeywordEnabled("_ENABLEPLANERREFLECTION")){DisableReflection();return;}
            MeshRenderer nearest=null;float best=float.PositiveInfinity;var eye=painter.studyCamera.transform.position;
            foreach(var renderer in painter.RiverView.WaterRenderers)
            {
                var b=renderer.bounds;float x=Mathf.Clamp(eye.x,b.min.x,b.max.x),z=Mathf.Clamp(eye.z,b.min.z,b.max.z),distance=(new Vector2(x,z)-new Vector2(eye.x,eye.z)).sqrMagnitude;
                if(distance<best){nearest=renderer;best=distance;}
            }
            if(!nearest){DisableReflection();return;}
            if(!plane)
            {
                plane=new GameObject("Uber resident reflection plane"){hideFlags=HideFlags.DontSave,layer=4};plane.SetActive(false);
                target=plane.AddComponent<MeshRenderer>();target.enabled=false;target.sharedMaterial=material;
                volume=plane.AddComponent<PlanarReflectionVolume>();volume.isGlobal=true;volume.renderScale=reflectionScale;volume.reflectSkybox=true;volume.hideReflectionCamera=true;volume.reflectionTargets.Add(plane);
                plane.SetActive(true);
            }
            if(target.sharedMaterial!=material){target.sharedMaterial=material;volume.UpdateTargetMaterials();}
            plane.transform.position=new Vector3(eye.x,nearest.bounds.center.y,eye.z);volume.renderScale=reflectionScale;volume.enabled=true;
            foreach(var renderer in painter.RiverView.WaterRenderers)
            {
                renderer.GetPropertyBlock(properties);properties.SetFloat("_PlannerReflectionBlend",Mathf.Abs(renderer.bounds.center.y-plane.transform.position.y)<.1f?0:1);renderer.SetPropertyBlock(properties);
            }
        }
        void DisableReflection()
        {
            if(volume)volume.enabled=false;
            if(!painter||painter.RiverView==null)return;
            foreach(var renderer in painter.RiverView.WaterRenderers)
            {
                renderer.GetPropertyBlock(properties);properties.SetFloat("_PlannerReflectionBlend",1);renderer.SetPropertyBlock(properties);
            }
        }
        void OnDisable(){DisableReflection();}
        void OnDestroy(){if(plane){if(Application.isPlaying)Destroy(plane);else DestroyImmediate(plane);}}
    }
}
