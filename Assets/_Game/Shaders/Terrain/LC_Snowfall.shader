Shader "Little Castle/Weather/LC Snowfall"
{
    Properties { _Color("Snow Color",Color)=(.86,.93,1,.75) _FallSpeed("Fall Speed",Float)=.065 _SnowfallDensity("Snowfall Density",Range(0,1))=.17 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "../Surface/LC_WeatherWet.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;float _FallSpeed;float _SnowfallDensity;
            CBUFFER_END
            float4 _LC_Wind;
            struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 seed:TEXCOORD1;};
            struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;half alpha:TEXCOORD1;float fog:TEXCOORD2;half snow:TEXCOORD3;};
            V vert(A a)
            {
                V o;float phase=frac(a.seed.x-_Time.y*_FallSpeed*(.7+a.seed.y));
                float3 center=TransformObjectToWorld(a.vertex.xyz);
                center.y+=phase*19;
                center.xz+=float2(sin(_Time.y*.35+a.seed.x*29),cos(_Time.y*.23+a.seed.x*37))*.6+_LC_Wind.xy*(1-phase)*_LC_Wind.z*3;
                float3 right=UNITY_MATRIX_I_V._m00_m10_m20;float3 up=UNITY_MATRIX_I_V._m01_m11_m21;
                float size=.06+a.seed.y*.07;
                center+=(right*(a.uv.x-.5)+up*(a.uv.y-.5))*size;
                o.pos=TransformWorldToHClip(center);o.uv=a.uv;
                // Continuous local biome transition and stable density dropout.
                float threshold=frac(a.seed.x*17.3+a.seed.y*13.7);
                o.snow=LCWeatherBiomeSnowCoverage(center)*step(threshold,_SnowfallDensity);
                o.alpha=smoothstep(0,.08,phase)*(1-smoothstep(.9,1,phase));
                o.fog=ComputeFogFactor(o.pos.z);return o;
            }
            half4 frag(V i):SV_Target{float d=length(i.uv-.5);half alpha=(1-smoothstep(.16,.5,d))*i.alpha*i.snow*_Color.a;return half4(MixFog(_Color.rgb,i.fog),alpha);}
            ENDHLSL
        }
    }
}
