Shader "Little Castle/Weather/Cozy Puddle Ripples"
{
    Properties
    {
        _WaterColor ("Wet stone / water tint", Color) = (0.23,0.35,0.43,1)
        _Wetness ("Wetness", Range(0,1)) = 0.4
        _Seed ("World-cell seed", Range(0,1)) = 0
        _RippleRate ("Ripple speed", Range(0.05,2)) = 0.65
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "Puddle Overlay"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Global storm strength, driven by the existing weather controller.
            float _LC_WeatherRainIntensity;

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor;
                float _Wetness;
                float _Seed;
                float _RippleRate;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            float Ring(float2 uv, float2 p, float phase)
            {
                float r = frac(phase);
                float radius = r * 0.42;
                float dist = length((uv - p) * float2(1.0, 0.88));
                float ring = 1.0 - smoothstep(0.0, 0.015, abs(dist - radius));
                return ring * (1.0 - r) * smoothstep(0.02, 0.12, r);
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 centered = (i.uv - 0.5) * float2(1.0, 1.12);
                float roundness = 1.0 - smoothstep(0.33, 0.49, length(centered));
                float irregular = 0.91 + 0.09 * sin(i.uv.x * 19 + _Seed * 17) * cos(i.uv.y * 16 + _Seed * 9);
                float mask = saturate(roundness * irregular);
                float t = _Time.y * _RippleRate;
                float ripple = Ring(i.uv, float2(.36, .41), t * .49 + _Seed);
                ripple += Ring(i.uv, float2(.63, .57), t * .61 + _Seed * 4.7);
                ripple += Ring(i.uv, float2(.53, .25), t * .39 + _Seed * 3.3);
                // Several staggered drops remain cheap and avoid one lonely repeating ring.
                float storm = saturate(_LC_WeatherRainIntensity);
                ripple += Ring(i.uv, float2(.27, .66), t * .57 + _Seed * 7.1) * storm;
                ripple += Ring(i.uv, float2(.74, .29), t * .69 + _Seed * 5.2) * storm;
                ripple += Ring(i.uv, float2(.43, .72), t * .42 + _Seed * 9.3) * storm;
                float3 color = lerp(_WaterColor.rgb, float3(.76, .86, .95),
                    saturate(ripple * .77));
                float alpha = saturate(_Wetness) * mask * (.30 + saturate(ripple) * .56)
                    * lerp(.9, 1.2, storm);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
