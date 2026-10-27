using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>One camera-local presentation manager, fixed GPU budget, smoothly decaying source slots.</summary>
    public sealed class GrassInteractionManager : MonoBehaviour
    {
        public Camera focusCamera;
        [Min(1)] public float activeDistance = 28;
        public bool interactionEnabled = true;
        [Range(1,30)] public float updateRate = 15;
        readonly Vector4[] positions = new Vector4[4];
        readonly GrassInteractionSource[] slots = new GrassInteractionSource[4];
        readonly float[] nearest = new float[4];
        readonly GrassInteractionSource[] candidates = new GrassInteractionSource[4];
        Vector4 strengths;
        float nextSelection;
        static readonly int SourcesId=Shader.PropertyToID("_LC_GrassInteractors"),BendsId=Shader.PropertyToID("_LC_GrassInteractorBend"),CountId=Shader.PropertyToID("_LC_GrassInteractorCount");
        public int activeSources {get;private set;}
        void LateUpdate()
        {
            if(!focusCamera)return;
            if(Time.unscaledTime>=nextSelection)
            {
                nextSelection=Time.unscaledTime+1f/Mathf.Max(1,updateRate);
                for(int i=0;i<4;i++){nearest[i]=float.PositiveInfinity;candidates[i]=null;}
                if(interactionEnabled)foreach(var source in GrassInteractionSource.Active)
                {
                    if(!source || !source.isActiveAndEnabled)continue;
                    float distance=(focusCamera.transform.position-source.transform.position).sqrMagnitude;
                    if(distance>activeDistance*activeDistance)continue;
                    for(int n=0;n<4;n++)if(distance<nearest[n])
                    {for(int j=3;j>n;j--){nearest[j]=nearest[j-1];candidates[j]=candidates[j-1];}nearest[n]=distance;candidates[n]=source;break;}
                }
                // Keep occupied slots until they have faded out; no teleporting a bent area.
                for(int n=0;n<4;n++)
                {
                    bool keep=false;for(int j=0;j<4;j++)if(slots[n] && slots[n]==candidates[j])keep=true;
                    if(!keep && strengths[n]<.01f)slots[n]=null;
                }
                for(int n=0;n<4;n++)if(candidates[n])
                {
                    bool exists=false;for(int j=0;j<4;j++)if(slots[j]==candidates[n])exists=true;
                    if(!exists)for(int j=0;j<4;j++)if(!slots[j] && strengths[j]<.01f){slots[j]=candidates[n];break;}
                }
            }
            activeSources=0;int uploadedCount=0;
            for(int n=0;n<4;n++)
            {
                bool selected=false;for(int j=0;j<4;j++)if(slots[n] && candidates[j]==slots[n])selected=true;
                bool live=interactionEnabled && selected && slots[n] && slots[n].isActiveAndEnabled;
                strengths[n]=Mathf.MoveTowards(strengths[n],live?slots[n].bend:0,Time.unscaledDeltaTime*4);
                if(live){Vector3 p=slots[n].transform.position;p.y+=slots[n].heightOffset;positions[n]=new Vector4(p.x,p.y,p.z,slots[n].radius);}
                if(strengths[n]>.001f){activeSources++;uploadedCount=n+1;}
            }
            Shader.SetGlobalVectorArray(SourcesId,positions);Shader.SetGlobalVector(BendsId,strengths);Shader.SetGlobalInt(CountId,uploadedCount);
        }
        void OnDisable(){Shader.SetGlobalInt(CountId,0);activeSources=0;}
    }
}
