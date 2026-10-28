#ifndef LC_SOFT_SHADOW_INCLUDED
#define LC_SOFT_SHADOW_INCLUDED
float _LC_ShadowFilterRadius;
#if defined(SHADOWS_SCREEN) && !defined(UNITY_NO_SCREENSPACE_SHADOWS)
half LCFilteredScreenShadow(float4 coord)
{
    half center=unitySampleShadow(coord);
    if(_LC_ShadowFilterRadius<=0) return center;
    float2 step=_LC_ShadowFilterRadius*coord.w/_ScreenParams.xy;
    return center*.25h + .125h*(unitySampleShadow(coord+float4(step.x,0,0,0))+
        unitySampleShadow(coord-float4(step.x,0,0,0))+
        unitySampleShadow(coord+float4(0,step.y,0,0))+
        unitySampleShadow(coord-float4(0,step.y,0,0))) + .0625h*(
        unitySampleShadow(coord+float4(step.x,step.y,0,0))+
        unitySampleShadow(coord+float4(-step.x,step.y,0,0))+
        unitySampleShadow(coord+float4(step.x,-step.y,0,0))+
        unitySampleShadow(coord+float4(-step.x,-step.y,0,0)));
}
#define LC_SHADOW_ATTENUATION(a) LCFilteredScreenShadow(a._ShadowCoord)
#else
#define LC_SHADOW_ATTENUATION(a) SHADOW_ATTENUATION(a)
#endif
#endif
