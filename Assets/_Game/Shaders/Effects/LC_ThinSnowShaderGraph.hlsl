#ifndef LITTLE_CASTLE_THIN_SNOW_SG_INCLUDED
#define LITTLE_CASTLE_THIN_SNOW_SG_INCLUDED

// Include through a Shader Graph Custom Function node (File mode).
// This function does not replace existing LC_StylizedLit / LC_Foliage lighting,
// shadow, or vertex wind logic. Render settings own the world-space mask texture.
// Requires Shader Graph/URP texture sampler macros to be provided by the graph.
TEXTURE2D(_LC_SnowField);
SAMPLER(sampler_LC_SnowField);
float4 _LC_SnowFieldRect; // xy=min worldXZ, zw=1/(worldXZ size)

void LC_ThinSnow_float(
    float3 PositionWS,
    float3 NormalWS,
    float3 BaseAlbedo,
    float3 SnowAlbedo,
    float SnowAcceptance,
    float SlopeStart,
    float SlopeEnd,
    float NoiseScale,
    float EdgeBreakup,
    out float3 ResultAlbedo,
    out float SnowAmount)
{
    float2 uv = (PositionWS.xz - _LC_SnowFieldRect.xy) * _LC_SnowFieldRect.zw;
    float inside = step(0, uv.x) * step(uv.x, 1) *
                   step(0, uv.y) * step(uv.y, 1);
    float cover = SAMPLE_TEXTURE2D(_LC_SnowField,
        sampler_LC_SnowField, saturate(uv)).r * inside;
    float upwards = smoothstep(SlopeStart, max(SlopeStart + 0.001, SlopeEnd),
        normalize(NormalWS).y);
    float2 noiseCell = floor(PositionWS.xz * max(0.01, NoiseScale));
    float noise = frac(sin(dot(noiseCell, float2(127.1, 311.7))) *
                       43758.5453);
    SnowAmount = saturate(cover * upwards * saturate(SnowAcceptance) +
        (noise - 0.5) * max(0, EdgeBreakup) * step(0.001, cover));
    SnowAmount *= step(0.001, cover);
    ResultAlbedo = lerp(BaseAlbedo, SnowAlbedo, SnowAmount);
}

#endif
