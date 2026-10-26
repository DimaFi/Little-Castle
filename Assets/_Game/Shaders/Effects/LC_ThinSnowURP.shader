Shader "Little Castle/Environment/URP Thin Snow Prototype"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Tint", Color) = (0.78, 0.83, 0.71, 1)
        _SnowColor ("Soft Snow Tint", Color) = (0.88, 0.92, 0.96, 1)
        _SnowAcceptance ("Accept Snow (per material)", Range(0, 1)) = 1
        _SnowSlopeStart ("Surface Upward Start", Range(-1, 1)) = 0.30
        _SnowSlopeEnd ("Surface Upward Full", Range(-1, 1)) = 0.85
        _SnowNoiseScale ("Snow Edge Noise Scale", Range(0.1, 8)) = 1.4
        _SnowEdgeBreakup ("Snow Edge Breakup", Range(0, 0.4)) = 0.09
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _SnowColor;
                float _SnowAcceptance;
                float _SnowSlopeStart;
                float _SnowSlopeEnd;
                float _SnowNoiseScale;
                float _SnowEdgeBreakup;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            // These are SCENE GLOBALS updated by SnowMaskTextureBridge, not per-material values.
            TEXTURE2D(_LC_SnowField);
            SAMPLER(sampler_LC_SnowField);
            float4 _LC_SnowFieldRect; // xy origin, zw = reciprocal size

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                float3 baseColor = SAMPLE_TEXTURE2D(
                    _BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;

                float2 worldUV = (input.positionWS.xz - _LC_SnowFieldRect.xy)
                    * _LC_SnowFieldRect.zw;
                float inBounds =
                    step(0.0, worldUV.x) * step(worldUV.x, 1.0) *
                    step(0.0, worldUV.y) * step(worldUV.y, 1.0);
                float snowFromField = SAMPLE_TEXTURE2D(
                    _LC_SnowField, sampler_LC_SnowField,
                    saturate(worldUV)).r * inBounds;

                // Never simply paint vertical walls entirely white.
                float upwards = smoothstep(
                    _SnowSlopeStart, max(_SnowSlopeStart + 0.001, _SnowSlopeEnd),
                    normalWS.y);

                float2 noiseCell = floor(input.positionWS.xz * _SnowNoiseScale);
                float noise = frac(sin(dot(noiseCell,
                    float2(127.1, 311.7))) * 43758.5453);
                float snowAmount = saturate(
                    snowFromField * upwards * _SnowAcceptance +
                    (noise - 0.5) * _SnowEdgeBreakup * step(0.001, snowFromField));
                snowAmount *= step(0.001, snowFromField);

                float3 albedo = lerp(baseColor, _SnowColor.rgb, snowAmount);

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float lambert = saturate(dot(normalWS, sun.direction));
                float3 ambient = max(SampleSH(normalWS), float3(0.12, 0.12, 0.13));
                float3 lightValue = ambient +
                    sun.color * (lambert * sun.distanceAttenuation * sun.shadowAttenuation);
                float3 lit = albedo * lightValue;
                lit = MixFog(lit, input.fogFactor);
                return half4(lit, 1);
            }
            ENDHLSL
        }

        // For a production foliage/wind material implement matched wind in all passes.
        // This demonstration shader is deliberately for static opaque props/ground.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
