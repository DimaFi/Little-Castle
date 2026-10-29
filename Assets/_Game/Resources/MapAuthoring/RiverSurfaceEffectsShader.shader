Shader "Little Castle/Rivers/Surface Effects"
{
    Properties { _FoamColor("Foam",Color)=(.73,.85,.81,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _FoamColor;
            float4 _Effects,_Wave1,_Wave2,_WaveDirection1,_WaveDirection2;
            float _AnimationSpeed,_ObstacleCount,_UseWaves;
            float4 _Obstacles[8],_ObstacleFlows[8];
            CBUFFER_END
            float4 _LC_AmbientColor,_LC_SunColor,_LC_MoonColor;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 data:TEXCOORD1;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 data:TEXCOORD1;float3 world:TEXCOORD2;float2 original:TEXCOORD3;half fog:TEXCOORD4;};
            float3 Wave(float3 p,float4 wave,float3 direction)
            {
                float length=max(wave.x,.001), k=6.2831853/length;
                direction=normalize(direction+float3(1e-6,0,0));
                float phase=dot(p,direction)*k-sqrt(k*9.8)*_Time.y*wave.z;
                return float3(0,cos(phase)*wave.y,0)-direction*(sin(phase)*wave.w/k);
            }
            V vert(A i)
            {
                V o;float3 world=TransformObjectToWorld(i.positionOS.xyz);o.original=world.xz;
                if(_UseWaves>.5)world+=Wave(world,_Wave1,_WaveDirection1.xyz)+Wave(world,_Wave2,_WaveDirection2.xyz);
                world.y+=.025;o.world=world;o.positionCS=TransformWorldToHClip(world);o.uv=i.uv;o.data=i.data;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            // Value noise in river coordinates stays continuous at tile seams and
            // moves downstream with the same station/speed data as the water.
            float Hash21(float2 p)
            {
                p=frac(p*float2(123.34,456.21));
                p+=dot(p,p+45.32);
                return frac(p.x*p.y);
            }
            float ValueNoise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);
                f=f*f*(3.0-2.0*f);
                return lerp(lerp(Hash21(cell),Hash21(cell+float2(1,0)),f.x),
                            lerp(Hash21(cell+float2(0,1)),Hash21(cell+1),f.x),f.y);
            }
            float FoamNoise(float2 p)
            {
                return ValueNoise(p)*.7+ValueNoise(p*2.07+float2(13.7,27.1))*.3;
            }
            half4 frag(V i):SV_Target
            {
                float depth=max(0,i.data.x);float time=_Time.y*_AnimationSpeed;
                float s=i.uv.y-time*i.data.y;float cross=i.uv.x;
                float foamNoise=FoamNoise(float2(cross*4.5,s*.38));
                float breakup=smoothstep(.38,.7,foamNoise);
                float wet=smoothstep(.015,.12,depth);
                float shore=(1-smoothstep(.04,max(.08,_Effects.w),depth))*wet*breakup*_Effects.x;
                float rapidNoise=FoamNoise(float2(cross*6.1+7.2,s*.7+11.4));
                float turbulence=smoothstep(1.1,3.5,i.data.y)*smoothstep(.43,.75,rapidNoise)*_Effects.z;
                float wakes=0;
                [loop] for(int n=0;n<8;n++)
                {
                    if(n>=_ObstacleCount)break;
                    float2 delta=i.original-_Obstacles[n].xy;
                    float r=max(.1,_Obstacles[n].z);
                    float2 flow=normalize(_ObstacleFlows[n].xy+float2(1e-6,0));
                    float along=dot(delta,flow)/r;
                    float lateral=dot(delta,float2(-flow.y,flow.x))/r;
                    float distance=length(float2(along,lateral));
                    [branch] if(distance>6.5)continue;
                    float ring=exp(-abs(distance-1.15)*3.0);
                    // A widening, bounded downstream plume follows the rock's local
                    // tangent, with asymmetric eddies advected at its flow speed.
                    float downstream=smoothstep(.1,.9,along)*(1-smoothstep(3.0,5.5,along));
                    float width=.55+max(0,along)*.28;
                    float plume=exp(-abs(lateral)/width)*downstream;
                    float eddy=ValueNoise(float2(lateral*1.6+along*.27,
                        along*1.15-time*_ObstacleFlows[n].z/max(r,.5)));
                    wakes=max(wakes,ring*(.3+.4*breakup)+plume*smoothstep(.36,.68,eddy)*.65);
                }
                float alpha=saturate((shore+turbulence*.38+wakes*_Effects.y)*wet)*.65;
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 light=max(_LC_AmbientColor.rgb*.8+(_LC_SunColor.rgb+_LC_MoonColor.rgb)*.26,half3(.13,.16,.2));
                half3 color=_FoamColor.rgb*light*lerp(.68,1,sun.shadowAttenuation);
                return half4(MixFog(color,i.fog),alpha*_FoamColor.a);
            }
            ENDHLSL
        }
    }
}
