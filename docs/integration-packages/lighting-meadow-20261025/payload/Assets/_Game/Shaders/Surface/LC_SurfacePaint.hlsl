#ifndef LC_SURFACE_PAINT_INCLUDED
#define LC_SURFACE_PAINT_INCLUDED
TEXTURE2D(_SurfacePaint); SAMPLER(sampler_SurfacePaint);
// Bounds belong to the per-material chunk/view, not shared authoritative simulation state.
float2 LCSurfaceCoverage(float3 worldPosition,float4 bounds)
{
    float2 uv=(worldPosition.xz-bounds.xy)*bounds.zw;
    float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
    return lerp(float2(0,1),SAMPLE_TEXTURE2D_LOD(_SurfacePaint,sampler_SurfacePaint,uv,0).rg,inside);
}
float LCSurfaceRoad(float3 worldPosition,float4 bounds){return LCSurfaceCoverage(worldPosition,bounds).r;}
#endif
