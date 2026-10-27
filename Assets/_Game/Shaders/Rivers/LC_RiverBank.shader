Shader "Little Castle/Rivers/Bank Closure"
{
    Properties { }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+5" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float4 _LC_AmbientColor,_LC_SunColor,_LC_MoonColor;
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;half4 color:COLOR;half fog:TEXCOORD2;};
            V vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);o.color=i.color;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 frag(V i):SV_Target
            {
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));float diffuse=saturate(dot(normalize(i.normal),light.direction))*.65+.35;
                half3 illumination=max(_LC_AmbientColor.rgb*.85+(_LC_SunColor.rgb+_LC_MoonColor.rgb)*diffuse*.55,half3(.16,.19,.22));
                float grain=sin(i.world.x*3.4+sin(i.world.z*4))*sin(i.world.z*2.7)*.025;
                return half4(MixFog((i.color.rgb+grain)*illumination*lerp(.65,1,light.shadowAttenuation),i.fog),i.color.a);
            }
            ENDHLSL
        }
    }
}
