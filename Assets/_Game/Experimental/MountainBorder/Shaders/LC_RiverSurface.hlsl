// Compact river adaptation of Uber Water's world-space normals, depth shoreline
// generator, absorption, refraction and projected caustics. MatrixRex, MIT.
// Source subgraphs and full license remain vendored; no external caustic image.
// Included after Shade(), sharing the waterfall landing's exact final evaluation.
half4 LCRiverSurface(float3 position,float3 geometricNormal,float2 screenUV,
    float eyeDepth,float depth,float speed,float time,half front,float2 flow)
{
    flow=normalize(flow+float2(.000001,0));
    // Two advected phases: resetting one sample never resets the visible pattern.
    float phase=time*.12,a=frac(phase),b=frac(phase+.5),blend=1-abs(a*2-1);
    float2 p=position.xz*.48;
    half3 na=UnpackNormal(SAMPLE_TEXTURE2D(_RiverNormalMap,sampler_RiverNormalMap,p-flow*a*speed*.8));
    half3 nb=UnpackNormal(SAMPLE_TEXTURE2D(_RiverNormalMap,sampler_RiverNormalMap,p-flow*b*speed*.8));
    half2 ripple=lerp(nb.xy,na.xy,blend)*.23*_DetailFade;
    half3 n=SafeNormalize(geometricNormal*front+float3(ripple.x,0,ripple.y));
    float dissolve=SAMPLE_TEXTURE2D(_RiverShoreMap,sampler_RiverShoreMap,position.xz*.55+time*float2(.016,.009)).r;
    // Uber shoreline's depth ramp, moving fractional bands and dissolving trail.
    // Use the common physical section instead of view-dependent screen depth.
    float shoreDepth=max(0,depth+LCRiverWave(position.xz,time)*.5)*(.5/max(.05,_Effects.w));
    float band=frac(shoreDepth*3.4-time*.34+dissolve*.16);
    half shore=(1-smoothstep(.12,.65,shoreDepth))*smoothstep(.04,.14,shoreDepth)
        *smoothstep(.62,.83,band)*(1-smoothstep(.9,1,band))*smoothstep(.18,.65,dissolve);
    float foamNoise=SAMPLE_TEXTURE2D(_RiverFoamMap,sampler_RiverFoamMap,position.xz*.34-flow*time*speed*.11).r;
    half rapids=smoothstep(.65,2.5,speed)*smoothstep(.72,.94,foamNoise)*_Effects.z;
    half wake=0;
    [unroll] for(int i=0;i<8;i++)if(i<_ObstacleCount)
    {
        float2 delta=position.xz-_Obstacles[i].xy,direction=normalize(_ObstacleFlows[i].xy+float2(.00001,0));
        float along=dot(delta,direction),across=abs(dot(delta,float2(-direction.y,direction.x))),radius=_Obstacles[i].z;
        float wakeWidth=radius*.7+max(0,along)*.22;
        wake+=smoothstep(-radius*.3,radius*.3,along)*(1-smoothstep(radius*1.5,radius*5,along))
            *(1-smoothstep(wakeWidth*.5,wakeWidth,across))*smoothstep(.35,.8,foamNoise);
    }
    half foam=saturate(shore*_Effects.x+rapids+wake*_Effects.y);
    half deep=1-exp2(-max(0,depth)*.9);
    half3 pigment=lerp(_RiverShallowColor.rgb,_RiverDeepColor.rgb,deep);
    half3 lit=Shade(lerp(pigment,_FoamColor.rgb,foam),n,position,foam);
    float3 view=SafeNormalize(GetWorldSpaceViewDir(position));
    half fresnel=.035+.65*pow(1-saturate(dot(n,view)),5);
    half3 environment=GlossyEnvironmentReflection(reflect(-view,n),.24h,1.0h);
    float4 reflected=mul(_RiverReflectionVP,float4(position,1));
    float2 reflectionUV=reflected.xy/max(.0001,reflected.w)*.5+.5+ripple*.012;
    if(_RiverReflectionValid>.5&&front>0&&all(reflectionUV>.002)&&all(reflectionUV<.998)&&reflected.w>0)
        environment=SAMPLE_TEXTURE2D(_RiverReflection,sampler_RiverReflection,reflectionUV).rgb;
    lit=lerp(lit,environment,fresnel*(1-foam));
    // The opaque buffer is optional; reject out-of-screen and foreground samples.
    if(_RiverOpaqueAvailable>.5&&_CameraOpaqueTexture_TexelSize.z>2&&front>0)
    {
        float2 refractUV=screenUV+ripple*.012*saturate(depth);
        float sampleRaw=SampleSceneDepth(refractUV);
        float sampleEye=IsPerspectiveProjection()?LinearEyeDepth(sampleRaw,_ZBufferParams):LinearDepthToEyeDepth(sampleRaw);
        if(all(refractUV>.002)&&all(refractUV<.998)&&sampleEye>eyeDepth+.025)
        {
            half3 background=SampleSceneColor(refractUV);
            half caustic=SAMPLE_TEXTURE2D(_RiverCausticMap,sampler_RiverCausticMap,position.xz*1.1+time*.035).r;
            background+=caustic*caustic*.12*(1-deep)*saturate(depth*5);
            // The bed remains visible without overwhelming the authored water palette
            // on pale/snowy terrain. Shared by rivers and the waterfall landing.
            lit=lerp(lit,background,(1-saturate(.28+deep*.68+fresnel+foam))*saturate(_RiverBackgroundContribution));
        }
    }
    return half4(lit,_Opacity);
}
