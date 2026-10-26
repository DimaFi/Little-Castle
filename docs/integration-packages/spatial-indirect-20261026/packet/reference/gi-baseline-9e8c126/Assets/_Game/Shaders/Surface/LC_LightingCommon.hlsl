#ifndef LC_LIGHTING_COMMON_INCLUDED
#define LC_LIGHTING_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"

// Screen AO describes missing indirect light. Do not multiply emission, fog,
// direct sunlight or the final composited colour. Use the stronger occlusion
// instead of stacking texture AO and SSAO into a black outline.
half LCIndirectOcclusion(float4 positionCS, half materialOcclusion)
{
#if defined(_SCREEN_SPACE_OCCLUSION)
    half screenOcclusion = GetScreenSpaceAmbientOcclusion(
        GetNormalizedScreenSpaceUV(positionCS)).indirectAmbientOcclusion;
    return min(materialOcclusion, screenOcclusion);
#else
    return materialOcclusion;
#endif
}

#endif
