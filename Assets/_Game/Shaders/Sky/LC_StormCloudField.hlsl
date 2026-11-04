#ifndef LC_STORM_CLOUD_FIELD_INCLUDED
#define LC_STORM_CLOUD_FIELD_INCLUDED

// View-direction 3D noise, not the flattened horizon-plane UVs used by the
// ordinary fair-weather clouds. Makes rounded storm forms at any camera pitch.
// This is deliberately an inexpensive 2/3-octave *impostor*, not raymarching.
float LCStormHash3(float3 p)
{
    p = frac(p * .1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float LCStormValue3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = LCStormHash3(i);
    float b = LCStormHash3(i + float3(1,0,0));
    float c = LCStormHash3(i + float3(0,1,0));
    float d = LCStormHash3(i + float3(1,1,0));
    float e = LCStormHash3(i + float3(0,0,1));
    float g = LCStormHash3(i + float3(1,0,1));
    float h = LCStormHash3(i + float3(0,1,1));
    float j = LCStormHash3(i + float3(1,1,1));

    return lerp(lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y),
                lerp(lerp(e,g,f.x),lerp(h,j,f.x),f.y),f.z);
}

float LCStormField(float3 p, float quality, out float broad, out float fine)
{
    broad = LCStormValue3(p * .64);
    fine = LCStormValue3(p * 1.46 + float3(7.3,19.7,35.1));
    float density = broad * .62 + fine * .38;
    if (quality > 1.5)
    {
        float micro = LCStormValue3(p * 2.96 + float3(43.7,9.2,57.1));
        density = density * .88 + micro * .12;
    }
    return density;
}

float LCStormFlashHalo(float3 viewDir, float4 flashDirection, float flash)
{
    float3 direction = normalize(flashDirection.xyz +
        float3(.00001, .00001, .00001));
    float aligned = saturate(dot(viewDir, direction));
    return saturate(flash) * pow(aligned, 14.0);
}

#endif
