#ifndef LC_WEATHER_WET_INCLUDED
#define LC_WEATHER_WET_INCLUDED

// Presentation contract, deliberately outside UnityPerMaterial.
// Only the opt-in rain controller drives this global; zero preserves existing looks.
float _LC_WeatherWetness;

// Small world-anchored variation: no extra textures, UVs, samplers or allocations.
// Snow is evaluated with existing LCEnvironmentSnow and masks rain on the visible snow layer.
half LCWeatherWetAmount(float3 positionWS, half3 geometricNormal, half snowCoverage)
{
    half overall = saturate(_LC_WeatherWetness);
    if (overall <= 0.0001h) return 0.0h;
    float2 p = positionWS.xz;
    float variation = sin(p.x * .83 + sin(p.y * .31)) * cos(p.y * 1.11 + sin(p.x * .47));
    half broad = .83h + .17h * variation;
    half snowFree = 1.0h - saturate(snowCoverage);
    return saturate(overall * broad * snowFree);
}

// Only relatively horizontal dirt/stone depressions pool water.
// This is a cheap artistic approximation, not a simulated fluid elevation field.
half LCWeatherShallowPool(float3 positionWS, half3 geometricNormal, half wetness)
{
    if (wetness < .04h) return 0.0h;
    half slope = smoothstep(.90h, .992h, geometricNormal.y);
    float2 p = positionWS.xz;
    float low = sin(p.x * .34 + sin(p.y * .17)) * cos(p.y * .39 + p.x * .08);
    low += .34 * sin(p.x * 1.12 - p.y * .81);
    half pool = smoothstep(.25h, .77h, low);
    return pool * slope * smoothstep(.26h, .91h, wetness);
}

half3 LCWeatherDarken(half3 color, half wetness, half factor)
{
    return color * (1.0h - saturate(wetness) * factor);
}

#endif
