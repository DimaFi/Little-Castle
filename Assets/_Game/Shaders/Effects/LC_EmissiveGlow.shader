Shader "Little Castle/Effects/LC Emissive Glow"
{
    Properties
    {
        _MainTex ("Glow Texture", 2D) = "white" {}
        [HDR] _Color ("Glow Color", Color) = (1.6,0.72,0.18,1)

        _DayStrength ("Day Strength", Range(0,4)) = 0.12
        _NightStrength ("Night Strength", Range(0,8)) = 2.2

        _FlickerStrength ("Flicker Strength", Range(0,0.3)) = 0.045
        _FlickerSpeed ("Flicker Speed", Range(0,12)) = 3.4

        _NightThreshold ("Night Threshold", Range(0,1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            // Keep shared presentation globals outside the material buffer.
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;

                half4 _Color;
                half _DayStrength;
                half _NightStrength;
                half _FlickerStrength;
                half _FlickerSpeed;
                half _NightThreshold;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            half _LC_NightAmount;
            float4 _LC_GameTime;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;

                half fogCoord : TEXCOORD2;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.position =
                    TransformObjectToHClip(
                        v.vertex.xyz);

                o.uv =
                    TRANSFORM_TEX(
                        v.uv,
                        _MainTex);

                o.worldPosition =
                    TransformObjectToWorld(
                        v.vertex.xyz);

                o.fogCoord = ComputeFogFactor(o.position.z);

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 sample =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        i.uv);

                half night =
                    smoothstep(
                        _NightThreshold,
                        1.0h,
                        _LC_NightAmount);

                half strength =
                    lerp(
                        _DayStrength,
                        _NightStrength,
                        night);

                float3 cell =
                    floor(
                        i.worldPosition *
                        0.31);

                half phase =
                    dot(
                        cell,
                        float3(
                            0.79,
                            1.41,
                            2.23));

                half wave =
                    sin(
                        _LC_GameTime.z *
                            _FlickerSpeed +
                        phase) *
                        0.68h +
                    sin(
                        _LC_GameTime.z *
                            (_FlickerSpeed * 1.73h) +
                        phase * 0.61h +
                        1.91h) *
                        0.32h;

                half flicker =
                    max(
                        0.0h,
                        1.0h +
                        wave *
                        _FlickerStrength);

                half alpha =
                    sample.a *
                    _Color.a;

                half4 result =
                    half4(
                        sample.rgb *
                            _Color.rgb *
                            strength *
                            flicker,
                        alpha);

                half fogFactor = InitializeInputDataFog(
                    float4(i.worldPosition, 1.0), i.fogCoord);
                result.rgb = MixFogColor(
                    result.rgb, half3(0, 0, 0), fogFactor);

                return result;
            }

            ENDHLSL
        }
    }

    Fallback Off
}
