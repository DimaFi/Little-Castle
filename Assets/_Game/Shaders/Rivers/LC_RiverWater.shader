Shader "Little Castle/Rivers/Flow Water"
{
    Properties { _Shallow("Shallow",Color)=(.23,.64,.68,1) _Deep("Deep",Color)=(.035,.23,.39,1) _Foam("Foam",Color)=(.75,.88,.84,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
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
            half4 _Shallow,_Deep,_Foam;
            CBUFFER_END
            float4 _LC_AmbientColor,_LC_SunColor,_LC_MoonColor,_LC_GameTime;
            float _LC_Daylight,_LC_NightAmount;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 data:TEXCOORD1;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 data:TEXCOORD1;float3 world:TEXCOORD2;half fog:TEXCOORD3;};
            V vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=i.uv;o.data=i.data;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 frag(V i):SV_Target
            {
                // Longitudinal UV is actual arc length; flow follows every bend, never a world axis.
                float time=_LC_GameTime.z;
                float s=i.uv.y-time*i.data.y;
                float cross=i.uv.x;
                float wave=sin(s*2.8+sin(cross*13+s*.39)*1.15)*.5+.5;
                float fine=sin(s*8.1+cross*24)*.5+.5;
                float depth=max(0,i.data.x),deep=1-exp(-depth*.85);
                half3 color=lerp(_Shallow.rgb,_Deep.rgb,deep);
                float streak=smoothstep(.94-max(fwidth(wave),.01),.99,wave)*(.18+.18*fine);
                float edge=smoothstep(.80,.99,abs(cross));
                float broken=smoothstep(.32,.78,sin(s*1.9+cross*15)*.5+.5);
                float foam=edge*broken*.42+streak*(.12+.22*i.data.z)+i.data.w*streak*.35;
                color=lerp(color,_Foam.rgb,saturate(foam));
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 sharedLight=max(_LC_AmbientColor.rgb*.8+(_LC_SunColor.rgb+_LC_MoonColor.rgb)*.26,half3(.13,.16,.2));
                color*=sharedLight*lerp(.68,1,sun.shadowAttenuation);
                color+=streak*.028*(1-_LC_NightAmount);
                color=MixFog(color,i.fog);
                float alpha=lerp(.30,.96,deep);alpha=max(alpha,foam*.72);
                alpha*=smoothstep(0,.08,depth);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
