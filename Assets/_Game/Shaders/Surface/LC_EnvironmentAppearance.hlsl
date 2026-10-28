#ifndef LC_ENVIRONMENT_APPEARANCE_INCLUDED
#define LC_ENVIRONMENT_APPEARANCE_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

// Presentation-only, outside UnityPerMaterial. The producer supplies linear
// palette multipliers (neutral summer = 1) and local snow coverage in R.
TEXTURE2D(_LC_BiomeTint); SAMPLER(sampler_LC_BiomeTint);
TEXTURE2D(_LC_EnvironmentSnow); SAMPLER(sampler_LC_EnvironmentSnow);
float _LC_EnvironmentEnabled;
float4 _LC_EnvironmentBounds; // min XZ, inverse world width/height
float4 _LC_EnvironmentSnowColor;

bool LCEnvironmentUV(float3 worldPos, out float2 uv)
{
    uv = (worldPos.xz - _LC_EnvironmentBounds.xy) * _LC_EnvironmentBounds.zw;
    return _LC_EnvironmentEnabled > 0.5
        && all(_LC_EnvironmentBounds.zw > 0.0)
        && all(uv >= 0.0) && all(uv <= 1.0);
}

half3 LCEnvironmentTint(float3 worldPos)
{
    float2 uv = float2(0,0);
    [branch] if (!LCEnvironmentUV(worldPos, uv))
        return half3(1, 1, 1);
    // Share URP's linear clamp state (also used by SSAO): terrain already has
    // fourteen authored texture samplers and must fit the hardware sampler budget.
    return SAMPLE_TEXTURE2D_LOD(_LC_BiomeTint, sampler_LinearClamp, uv, 0).rgb;
}

half LCEnvironmentSnow(float3 worldPos, half3 geometricNormal, half acceptance)
{
    float2 uv = float2(0,0);
    [branch] if (acceptance <= 0.0h || !LCEnvironmentUV(worldPos, uv))
        return 0.0h;

    half coverage = saturate(SAMPLE_TEXTURE2D_LOD(
        _LC_EnvironmentSnow, sampler_LinearClamp, uv, 0).r);
    // Bounded, low-frequency variation only modulates existing local coverage.
    // Multiply acceptance and slope AFTER noise: zero coverage, vertical walls,
    // undersides and opted-out materials must never acquire stray snow.
    half noise = 0.88h + 0.12h * sin(dot(worldPos.xz, float2(0.73, 1.17)));
    half slope = smoothstep(0.15h, 0.75h, normalize(geometricNormal).y);
    return coverage * noise * saturate(acceptance) * slope;
}

half3 LCEnvironmentAlbedo(half3 albedo, float3 worldPos, half3 normal, half acceptance)
{
    half snow = LCEnvironmentSnow(worldPos, normal, acceptance);
    return lerp(albedo * LCEnvironmentTint(worldPos), _LC_EnvironmentSnowColor.rgb, snow);
}

#endif // LC_ENVIRONMENT_APPEARANCE_INCLUDED
