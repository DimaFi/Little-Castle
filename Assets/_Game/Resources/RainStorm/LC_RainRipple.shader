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
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../Shaders/Surface/LC_WeatherWet.hlsl"

            // Global storm strength, driven by the existing weather controller.
            float _LC_WeatherRainIntensity;

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor;
                float _Wetness;
                float _Seed;
                float _RippleRate;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fog : TEXCOORD2;
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            float SoftUnion(float a, float b)
            {
                float overlap = max(.09 - abs(a - b), 0);
                return min(a, b) - overlap * overlap / .36;
            }
            float Ring(float2 uv, float2 center, float phase, float radius, float blur)
            {
                float age = frac(phase) * 1.22;
                float dist = length((uv - center) * float2(1, .82));
                float offset = dist - saturate(age) * radius;
                float width = .009 + blur;
                float ring = 1 - smoothstep(.004, width, abs(offset));
                return ring * (.009 / width) * smoothstep(.02, .13, age) * (1 - smoothstep(.35, 1, age));
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv - .5;
                float2 warp = .024 * sin(p.yx * float2(15, 18) + _Seed * float2(7, 11));
                float2 shape = p + warp;
                float middle = length(shape * float2(1, 1.13)) - .32;
                float left = length((shape + float2(.21, -.065 + .09 * _Seed)) * float2(1, 1.25)) - .197;
                float right = length((shape - float2(.225, -.11 + .14 * _Seed)) * float2(1, 1.13)) - .202;
                float outline = SoftUnion(SoftUnion(middle, left), right);
                half mask = 1 - smoothstep(-.065, .014, outline);
                half lip = smoothstep(-.12, -.05, outline) * (1 - smoothstep(-.05, -.01, outline));
                half sheen = smoothstep(-.28, .32, p.y + p.x * .3 + warp.y);

                float t = _Time.y * _RippleRate * 1.6;
                float blur = length(fwidth(i.uv)) * .45;
                float storm = saturate(_LC_WeatherRainIntensity);
                float ripple = Ring(p, float2(-.115, -.08 + _Seed * .09), t * .67 + _Seed, .145, blur);
                ripple += Ring(p, float2(.13, .05 - _Seed * .12), t * .93 + _Seed * 4.7, .19, blur);
                ripple += Ring(p, float2(.01, -.135), t * .78 + _Seed * 7.1, .12, blur) * storm * .55;
                ripple *= smoothstep(.006, .18, storm) * (1 - smoothstep(-.095, -.035, outline));

                half3 color = _WaterColor.rgb * (.58h + .42h * sheen);
                color += half3(.10, .14, .16) * (lip * sheen + saturate(ripple) * .65h);
                half exposure = 1 - LCWeatherShelterCoverage(i.positionWS);
                half alpha = saturate(_Wetness) * mask * exposure * (.62h + .22h * sheen + .13h * saturate(ripple));
                return half4(MixFog(color, i.fog), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
