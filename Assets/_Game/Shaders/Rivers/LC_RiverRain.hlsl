#ifndef LC_RIVER_RAIN_INCLUDED
#define LC_RIVER_RAIN_INCLUDED

#include "../Surface/LC_WeatherWet.hlsl"

float _LC_WeatherRainIntensity;
float4 _LC_WeatherRiverImpacts; // ripple speed, secondary layer weight, splash strength, seed

float3 LCRiverRainHash(float2 key)
{
    float3 p = frac(float3(key.xyx) * float3(.1031, .1030, .0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yzz) * p.zyx);
}

float LCRiverRainBand(float offset, float width, float blur)
{
    return (1 - smoothstep(width * .3, width + blur, abs(offset))) * width / (width + blur);
}

// A dry interval hides the reseed. Each footprint fits its cell so it never gets clipped
// by a cell boundary. The overlapping layer uses a different world-space grid.
float3 LCRiverRainDrop(float2 grid, float cellSize, float clock, float rain, float blur)
{
    float2 cell = floor(grid);
    float3 schedule = LCRiverRainHash(cell + _LC_WeatherRiverImpacts.w);
    float cycle = clock * lerp(1.12, 1.67, schedule.x) + schedule.y;
    float3 drop = LCRiverRainHash(cell + floor(cycle) * float2(11.29, -7.53) + schedule.z);
    float age = frac(cycle) / lerp(.59, .88, drop.z);
    float active = 1 - smoothstep(rain * .83, rain, schedule.z);
    float fade = smoothstep(0, .07, age) * (1 - smoothstep(.38, 1, age)) * active;
    float2 offset = (frac(grid) - lerp(.245, .755, drop.xy)) * cellSize;
    float distanceToDrop = length(offset);
    float radius = cellSize * lerp(.11, .2, drop.z) * saturate(age);
    float width = lerp(.012, .021, drop.z);
    float crest = LCRiverRainBand(distanceToDrop - radius, width, blur);
    float trough = LCRiverRainBand(distanceToDrop - radius + width * 1.5, width, blur);
    float sheen = lerp(.58, 1, saturate(.5 + .5 * dot(offset, float2(.61, .79))
        / max(distanceToDrop, .001)));
    float pin = (1 - smoothstep(.016, .041 + blur, distanceToDrop))
        * smoothstep(0, .035, age) * (1 - smoothstep(.07, .19, age)) * active;
    return float3(crest * sheen * fade, trough * fade, pin);
}

float3 LCApplyRiverRain(float3 color, float3 positionWS, half waterCoverage)
{
    // Evaluate derivatives before any per-pixel masking.
    float2 p = positionWS.xz;
    float footprint = max(length(ddx(p)), length(ddy(p)));
    float rain = saturate(_LC_WeatherRainIntensity);
    if (_LC_WeatherRiverImpacts.x <= 0 || rain <= .006 || waterCoverage <= 0 || footprint >= .25)
        return color;

    half strength = waterCoverage * smoothstep(.006, .17, rain)
        * (1 - smoothstep(.075, .25, footprint)) * (1 - LCWeatherShelterCoverage(positionWS));
    float clock = _Time.y * (_LC_WeatherRiverImpacts.x / .65);
    float blur = footprint * .45;
    float3 impact = LCRiverRainDrop(p / 1.68, 1.68, clock, rain, blur);
    if (_LC_WeatherRiverImpacts.y > 0)
    {
        float2 grid = float2(dot(p, float2(.8, .6)), dot(p, float2(-.6, .8)));
        impact += LCRiverRainDrop(grid / 2.58 + float2(5.17, 12.53), 2.58, clock, rain, blur)
            * _LC_WeatherRiverImpacts.y;
    }

    // Composite a narrow light rim and its dark trough over the already shaded water.
    half light = .036h + .55h * saturate(dot(color, half3(.2126, .7152, .0722)));
    half rim = saturate(impact.x + impact.z * _LC_WeatherRiverImpacts.z);
    return color * (1 - .23h * strength * saturate(impact.y))
        + half3(.85, .96, 1) * (light * rim * strength);
}

#endif
