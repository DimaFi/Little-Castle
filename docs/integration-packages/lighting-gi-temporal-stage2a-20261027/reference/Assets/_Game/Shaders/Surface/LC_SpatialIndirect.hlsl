#ifndef LC_SPATIAL_INDIRECT_INCLUDED
#define LC_SPATIAL_INDIRECT_INCLUDED

#if defined(_LC_SPATIAL_INDIRECT)
struct LCIndirectProbe { float4 c0; float4 cx; float4 cy; float4 cz; };
StructuredBuffer<LCIndirectProbe> _LC_IndirectProbes;
float4 _LC_IndirectOrigin;
float4 _LC_IndirectSize;
float4 _LC_IndirectParameters; // strength, spacing, surface normal bias, visibility guard
float4 _LC_IndirectBoxMin[8], _LC_IndirectBoxMax[8];
int _LC_IndirectBoxCount;

bool LCIndirectSegmentBlocked(float3 origin,float3 destination)
{
    float3 delta=destination-origin;
    float3 direction=lerp(-1.0,1.0,step(0,delta))*max(abs(delta),1e-7);
    float3 inv=rcp(direction);
    float3 segmentMin=min(origin,destination),segmentMax=max(origin,destination);
    [loop] for(int j=0;j<min(8,_LC_IndirectBoxCount);j++)
    {
        if(any(segmentMax<_LC_IndirectBoxMin[j].xyz)||any(segmentMin>_LC_IndirectBoxMax[j].xyz))continue;
        float3 a=(_LC_IndirectBoxMin[j].xyz-origin)*inv;
        float3 b=(_LC_IndirectBoxMax[j].xyz-origin)*inv;
        float3 lo=min(a,b),hi=max(a,b);
        float near=max(max(lo.x,lo.y),lo.z),far=min(min(hi.x,hi.y),hi.z);
        // Exclude the two endpoints: surfaces are queried after a small outward bias.
        if(near<=far && far>1e-4 && near<.9999)return true;
    }
    return false;
}
#endif

#if defined(_LC_SPATIAL_INDIRECT)
half3 LCSpatialIndirect(float3 positionWS,half3 normalWS,half3 fallbackAmbient)
{
    half3 result=fallbackAmbient;
    [branch] if(_LC_IndirectParameters.x>0)
    {
        float spacing=_LC_IndirectParameters.y;
        float3 samplePosition=positionWS+normalWS*_LC_IndirectParameters.z;
        float3 coord=(samplePosition-_LC_IndirectOrigin.xyz)/spacing;
        int3 size=(int3)_LC_IndirectSize.xyz;
        if(all(coord>=0)&&all(coord<=size-1))
        {
            int3 cell=min((int3)floor(coord),size-2);
            float3 f=saturate(coord-cell),value=0;float total=0;bool uninitialised=false;
            [unroll] for(int corner=0;corner<8;corner++)
            {
                int3 offset=int3(corner&1,(corner>>1)&1,(corner>>2)&1);
                int3 key=cell+offset;
                float3 factor=lerp(1-f,f,(float3)offset);float weight=factor.x*factor.y*factor.z;
                if(weight>=1e-5)
                {
                    uint index=(key.z*size.y+key.y)*size.x+key.x;
                    LCIndirectProbe p=_LC_IndirectProbes[index];
                    if(p.c0.w==0)uninitialised=true;
                    if(p.c0.w>=.5)
                    {
                        float3 probePosition=_LC_IndirectOrigin.xyz+key*spacing;
                        bool blocked=false;
                        if(_LC_IndirectParameters.w>.5)blocked=LCIndirectSegmentBlocked(samplePosition,probePosition);
                        if(!blocked){value+=(p.c0.rgb+p.cx.rgb*normalWS.x+p.cy.rgb*normalWS.y+p.cz.rgb*normalWS.z)*weight;total+=weight;}
                    }
                }
            }
            if(total>1e-5||!uninitialised)
            {
                value=total>1e-5?max(0,value/max(1e-5,total)):0;
                float edge=min(min(min(coord.x,size.x-1-coord.x),min(coord.z,size.z-1-coord.z)),size.y-1-coord.y);
                result=lerp(fallbackAmbient,(half3)value,saturate(edge)*saturate(_LC_IndirectParameters.x));
            }
        }
    }
    return result;
}
#else
// Keep the original expression and its precision when the experiment is absent.
// A half3 function argument would narrow a float3 fallback before its material tint.
#define LCSpatialIndirect(positionWS, normalWS, fallbackAmbient) (fallbackAmbient)
#endif
#endif
