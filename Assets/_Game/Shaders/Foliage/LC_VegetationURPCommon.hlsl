#ifndef LC_VEGETATION_URP_COMMON_INCLUDED
#define LC_VEGETATION_URP_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "../Surface/LC_LightingCommon.hlsl"

// Shared presentation globals stay outside UnityPerMaterial.
half4 _LC_SunDirection, _LC_SunColor, _LC_MoonDirection, _LC_MoonColor;
half4 _LC_AmbientColor, _LC_AmbientSkyColor, _LC_AmbientEquatorColor, _LC_AmbientGroundColor;
half4 _LC_ShadowTint;
half _LC_Daylight, _LC_Twilight, _LC_NightAmount;
float4 _LC_Wind, _LC_GameTime;

// URP sets these for each directional / punctual shadow caster.
float3 _LightDirection, _LightPosition;

float4 LCVegetationShadowPosition(float3 positionWS, float3 normalWS)
{
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    return ApplyShadowClamping(TransformWorldToHClip(
        ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
}

half LCVegetationMainShadow(float3 positionWS)
{
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(positionWS));
#else
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
#endif
    return GetMainLight(shadowCoord, positionWS, half4(1, 1, 1, 1)).shadowAttenuation;
}

half4 LCVegetationPackDepthNormal(half3 normalWS)
{
    normalWS = NormalizeNormalPerPixel(normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormal = PackNormalOctQuadEncode(normalWS);
    return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0);
#else
    return half4(normalWS, 0);
#endif
}

float HashObject(float3 objectOrigin)
{
    return frac(
        sin(
            dot(
                objectOrigin,
                float3(
                    12.9898,
                    78.233,
                    37.719))) *
        43758.5453);
}

half3 EvaluateHemisphereAmbient(
    half3 normal)
{
    half up =
        saturate(
            normal.y);

    half down =
        saturate(
            -normal.y);

    half side =
        saturate(
            1.0h -
            abs(normal.y));

    return
        _LC_AmbientSkyColor.rgb *
            up +
        _LC_AmbientEquatorColor.rgb *
            side +
        _LC_AmbientGroundColor.rgb *
            down;
}

#endif
