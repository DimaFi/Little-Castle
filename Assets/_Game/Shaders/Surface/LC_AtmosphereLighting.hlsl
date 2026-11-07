#ifndef LC_ATMOSPHERE_LIGHTING_INCLUDED
#define LC_ATMOSPHERE_LIGHTING_INCLUDED
#include "../Sky/LC_CloudNoise.hlsl"

// Presentation only. Zero globals retain the original material response.
float4 _LC_CloudShadow, _LC_CloudShadowDrift;
half _LC_AtmosphereTransmission;
half4 _LC_AtmosphereGroundBounce;

half3 LCAtmosphereGroundFill(half3 normalWS)
{
    // Local colour fill only: no probes, rays or new render pass. Apply AO at the caller.
    return _LC_AtmosphereGroundBounce.rgb * (.35h + .65h * saturate(.5h - normalWS.y * .5h));
}

half LCAtmosphereCloudShadow(float3 positionWS)
{
    if (_LC_CloudShadow.x <= .001) return 1;
    float2 p = positionWS.xz * _LC_CloudShadow.y - _LC_CloudShadowDrift.xy;
    // Broad, deliberately soft approximation; no light cookie or additional render pass.
    float mask = LCCloudMask(LCCloudBody(p, 0), _LC_CloudShadow.z);
    return 1 - mask * _LC_CloudShadow.x;
}

half LCAtmosphereForwardScatter(half3 normalWS, half3 lightDir, half3 viewDir)
{
    // Keep the broad forward lobe on the thin/backlit side of a card. Geometric
    // normals at the caller avoid sparkling normal-map detail in the sun halo.
    half thinSide = saturate(.35h - dot(normalWS, lightDir) * .65h);
    half throughLeaf = saturate(dot(-lightDir, viewDir) * .5h + .5h);
    throughLeaf = throughLeaf * throughLeaf * throughLeaf;
    return thinSide * (.18h + .82h * throughLeaf) * saturate(_LC_AtmosphereTransmission);
}

half LCAtmosphereTransmissionVisibility(half shadow)
{
    // Optional art response removes the old 35% direct-light floor in shadow.
    // Fade in over the first .1 of the existing 0-.5 control; zero is legacy.
    return lerp(lerp(.35h, 1.0h, shadow), saturate(shadow),
        saturate(_LC_AtmosphereTransmission * 10.0h));
}

// SPDX-License-Identifier: MIT
// Clustered InputData / LIGHT_LOOP pattern adapted from Cyanilux,
// URP_ShaderGraphCustomLighting/CustomLighting.hlsl, AdditionalLights_float,
// commit 19fac6a6126a9c25d8f226b38bb12c7360557ff6. Copyright (c) 2020 Cyanilux.
// Full permission notice: Assets/ThirdParty/Cyanilux_CustomLighting/LICENSE.txt.
// LC's point/spot filter and diffuse response follow its existing solid shader.
half3 LCAtmosphereAdditionalDiffuse(float3 positionWS, float4 positionCS,
    half3 normalWS, half wrap)
{
    half3 diffuse = 0;
#if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
    // URP owns culling and per-object / clustered light budgets. Do not add a
    // second fixed loop or the extra-directional loop: LC already lights sun/moon.
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    uint lightCount = GetAdditionalLightsCount();
    wrap = clamp(wrap, 0.0h, .2h);
    LIGHT_LOOP_BEGIN(lightCount)
        #if USE_CLUSTER_LIGHT_LOOP
            int perObjectIndex = (int)lightIndex;
        #else
            int perObjectIndex = GetPerObjectLightIndex(lightIndex);
        #endif
        #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
            bool isPunctual = _AdditionalLightsBuffer[perObjectIndex].position.w != 0.0;
        #else
            bool isPunctual = _AdditionalLightsPosition[perObjectIndex].w != 0.0;
        #endif
        if (isPunctual)
        {
            Light light = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
            #if defined(_LIGHT_LAYERS)
            if (IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer()))
            #endif
            {
                half response = saturate((dot(normalWS, light.direction) + wrap) / (1.0h + wrap));
                diffuse += light.color * (light.distanceAttenuation * light.shadowAttenuation) * response;
            }
        }
    LIGHT_LOOP_END
#endif
    return diffuse;
}
#endif
