Shader "Little Castle/Effects/Chimney Smoke Ribbon"
{
    Properties
    {
        _SmokeTint ("Smoke Tint", Color) = (0.92,0.94,0.93,1)
        _SmokeOpacity ("Smoke Opacity", Range(0,1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Smoke"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SmokeTint;
                half _SmokeOpacity;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; half fog : TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS);
                o.positionCS = p.positionCS;
                o.uv = i.uv;
                o.color = i.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                // No sharp ribbon outline; center density is higher than the edges.
                half horizontal = saturate(1.0h - abs(i.uv.x * 2.0h - 1.0h));
                half feather = horizontal * horizontal * (3.0h - 2.0h * horizontal);
                half alpha = saturate(_SmokeOpacity * i.color.a * _SmokeTint.a * feather);
                half3 color = MixFog(_SmokeTint.rgb, i.fog);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
