#ifndef LC_RIVER_WAVES_INCLUDED
#define LC_RIVER_WAVES_INCLUDED
// Vertical component / dispersion from MatrixRex GersterWaveGenerator,
// MIT, Uber-Stylized-Water @ 1fdd16c. Full notice: RiverWater-Licenses.txt.
// Horizontal chop is deliberately omitted: authored banks and queries stay fixed.
// Two bounded waves: at most 0.048 m; the mesh envelope fixes wet edges and joins.
float LCRiverWave(float2 p,float time)
{
    float k1=6.283185/3.0,k2=6.283185/1.7;
    return .030*cos(dot(p,float2(1,0))*k1-sqrt(9.8*k1)*time*.5)
         + .018*cos(dot(p,normalize(float2(.34,.94)))*k2-sqrt(9.8*k2)*time*.6);
}
float2 LCRiverWaveSlope(float2 p,float time)
{
    float k1=6.283185/3.0,k2=6.283185/1.7;
    float2 direction=normalize(float2(.34,.94));
    return -float2(1,0)*.030*k1*sin(p.x*k1-sqrt(9.8*k1)*time*.5)
        -direction*.018*k2*sin(dot(p,direction)*k2-sqrt(9.8*k2)*time*.6);
}
#endif
