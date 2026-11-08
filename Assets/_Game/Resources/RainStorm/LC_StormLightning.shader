Shader "Little Castle/Weather/Distant Storm Lightning"
{
    Properties
    {
        [HDR] _EmissionColor ("Lightning glow (HDR)", Color) = (3.5,4.4,6.0,1)
        _Opacity ("Opacity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent"
               "Queue"="Transparent-20" "IgnoreProjector"="True" }
        Pass
        {
            Name "Distant Lightning"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                half _Opacity;
            CBUFFER_END

            struct A { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            V Vert(A input)
            {
                V o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color;
                o.uv = input.uv;
                return o;
            }
            half4 Frag(V input):SV_Target
            {
                // Soft edges on the width of the line. With bloom this reads as a glow,
                // without bloom the core still remains visible in the sky.
                half across = input.uv.y;
                half edge = smoothstep(0.0h, 0.22h, across) *
                            (1.0h - smoothstep(0.78h, 1.0h, across));
                // Stretch UVs bury both ends in the bank; render before cloud
                // scattering so it can actually veil the channel.
                half along = smoothstep(0.0h, 0.18h, input.uv.x) *
                             (1.0h - smoothstep(0.70h, 1.0h, input.uv.x));
                return half4(_EmissionColor.rgb * input.color.rgb,
                    saturate(_Opacity * input.color.a * edge * along));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
