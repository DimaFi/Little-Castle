#ifndef LC_GROUND_PALETTE_INCLUDED
#define LC_GROUND_PALETTE_INCLUDED
// World-space patches shared by terrain and blade roots; no chunk/object identity.
float LCGroundHash(float2 p)
{
    p=frac(p*float2(.1031,.11369));
    p+=dot(p,p.yx+19.19);
    return frac((p.x+p.y)*p.x);
}
float LCGroundNoise(float2 p)
{
    float2 cell=floor(p), f=frac(p);
    f=f*f*(3-2*f);
    return lerp(lerp(LCGroundHash(cell),LCGroundHash(cell+float2(1,0)),f.x),
        lerp(LCGroundHash(cell+float2(0,1)),LCGroundHash(cell+1),f.x),f.y);
}
half LCMeadowPatch(float2 worldXZ)
{
    return smoothstep(.25,.78,LCGroundNoise(worldXZ*.075+17.3)*.7+
        LCGroundNoise(worldXZ*.19-8.1)*.3);
}
#endif
