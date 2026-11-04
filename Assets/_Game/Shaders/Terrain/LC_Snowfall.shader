Shader "Little Castle/Weather/LC Snowfall"
{
    Properties
    {
        _Color ("Snow Color", Color) = (.86,.93,1,.75)
        _FallSpeed ("Fall Speed", Float) = .065
        _SnowfallDensity ("Snowfall Density", Range(0,1)) = .17
        [HideInInspector] _FallHeight ("Fall height", Float) = 48
        [HideInInspector] _BaseHeight ("Bottom offset", Float) = -5
        [HideInInspector] _FieldRadius ("Field radius", Float) = 34
        [HideInInspector] _MinDistance ("Inner fade", Float) = 0
        [HideInInspector] _FlakeScale ("Flake scale", Float) = 1
        [HideInInspector] _LayerOpacity ("Layer opacity", Float) = .85
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent"
               "RenderType"="Transparent" }
        Pass
        {
            Name "Falling Snow"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "../Surface/LC_WeatherWet.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _FallSpeed;
                float _SnowfallDensity;
                float _FallHeight;
                float _BaseHeight;
                float _FieldRadius;
                float _MinDistance;
                float _FlakeScale;
                float _LayerOpacity;
            CBUFFER_END

            float4 _LC_Wind;
            struct A
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 seed : TEXCOORD1;
            };
            struct V
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half alpha : TEXCOORD1;
                float fog : TEXCOORD2;
                half snow : TEXCOORD3;
            };

            V vert(A a)
            {
                V o;
                // A wrapped falling cycle starts at the TOP of each column.
                // Fade on re-entry and near the floor, never at a mid-screen
                // horizontal spawning plane.
                float phase = frac(a.seed.x - _Time.y * _FallSpeed * (.7 + a.seed.y));
                float3 center = TransformObjectToWorld(a.vertex.xyz);
                center.y += _BaseHeight + phase * _FallHeight;
                center.xz += float2(
                    sin(_Time.y * .35 + a.seed.x * 29),
                    cos(_Time.y * .23 + a.seed.x * 37)) * .6
                    + _LC_Wind.xy * (1 - phase) * _LC_Wind.z * 3;

                // Fade both concentric layers together, hiding their finite
                // world-space bounds even when the camera looks to the horizon.
                float radius = length(a.vertex.xz);
                float outer = 1 - smoothstep(_FieldRadius * .80,
                    max(1, _FieldRadius), radius);
                float inner = _MinDistance > .1 ?
                    smoothstep(_MinDistance - 6, _MinDistance + 9, radius) : 1;
                float vertical = smoothstep(0, .10, phase) *
                    (1 - smoothstep(.90, 1, phase));
                float threshold = frac(a.seed.x * 17.3 + a.seed.y * 13.7);
                o.snow = LCWeatherBiomeSnowCoverage(center) *
                    step(threshold, _SnowfallDensity);
                o.alpha = vertical * inner * outer * _LayerOpacity;

                float3 right = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up = UNITY_MATRIX_I_V._m01_m11_m21;
                float size = (.06 + a.seed.y * .07) * _FlakeScale;
                center += (right * (a.uv.x - .5) +
                    up * (a.uv.y - .5)) * size;
                o.pos = TransformWorldToHClip(center);
                o.uv = a.uv;
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float d = length(i.uv - .5);
                half alpha = (1 - smoothstep(.16, .5, d)) *
                    i.alpha * i.snow * _Color.a;
                return half4(MixFog(_Color.rgb, i.fog), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
