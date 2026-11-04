#ifndef LC_CLOUD_NOISE_INCLUDED
#define LC_CLOUD_NOISE_INCLUDED

float LCCloudHash(float2 p)
{
    p = frac(p / 256.0) * 256.0;
    p = frac(p * float2(.1031, .1030));
    p += dot(p, p.yx + 33.33);
    return frac((p.x + p.y) * p.x);
}

float LCCloudNoise(float2 p)
{
    float2 cell = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(LCCloudHash(cell), LCCloudHash(cell + float2(1, 0)), f.x),
        lerp(LCCloudHash(cell + float2(0, 1)), LCCloudHash(cell + 1), f.x), f.y);
}

float LCCloudBody(float2 p, float quality)
{
    float body = LCCloudNoise(p) * .62 + LCCloudNoise(p * 2.0 + 29.0) * .26;
    if (quality > .5) body += LCCloudNoise(p * 4.0 + 71.0) * .12;
    else body /= .88;
    if (quality > 2.5) body = body * .94 + LCCloudNoise(p * 8.0 + 113.0) * .06;
    return body;
}

float LCCloudMask(float body, float coverage)
{
    // Coverage zero and one are exact endpoints, not remapped residual wisps.
    float threshold = lerp(1.03, -.18, saturate(coverage));
    return smoothstep(threshold, threshold + .19, body);
}

// Sky-only cloud field. Integer octave frequencies retain the 256-cell drift
// period, including when the presentation controller wraps its wind offset.
// The larger features hold clusters together while the smaller ones break up
// their silhouettes. Terrain cloud shadows keep the cheaper field above.
float LCSkyCloudBody(float2 p, float quality, out float broad)
{
    broad = LCCloudNoise(p);
    float body = broad * .54 + LCCloudNoise(p * 2.0 + 29.0) * .32;
    if (quality > .5)
        body += LCCloudNoise(p * 4.0 + 71.0) * .14;
    else
        body /= .86;
    if (quality > 2.5)
        body = body * .95 + LCCloudNoise(p * 8.0 + 113.0) * .05;
    return body;
}
#endif
