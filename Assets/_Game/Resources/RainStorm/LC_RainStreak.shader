Shader "Little Castle/Weather/Cozy Rain Streak"
{
    Properties
    {
        _Tint ("Rain Tint", Color) = (0.74,0.84,0.94,0.65)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Forward Rain"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "../../Shaders/Surface/LC_WeatherWet.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };
            Varyings Vert(Attributes a)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.worldPos = TransformObjectToWorld(a.positionOS.xyz);
                o.color = a.color;
                o.uv = a.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                // Soft transparent taper; no hard white rectangular particles.
                half edgeX = smoothstep(0.0h, 0.13h, i.uv.x) * (1.0h - smoothstep(0.87h, 1.0h, i.uv.x));
                half edgeY = smoothstep(0.0h, 0.22h, i.uv.y) * (1.0h - smoothstep(0.78h, 1.0h, i.uv.y));
                half alpha = _Tint.a * i.color.a * edgeX * edgeY *
                    (1.0h - LCWeatherShelterCoverage(i.worldPos)) *
                    (1.0h - LCWeatherBiomeSnowCoverage(i.worldPos));
                return half4(_Tint.rgb * i.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
