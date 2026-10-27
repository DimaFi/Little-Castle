Shader "Little Castle/Distance/LC Distant Simple"
{
    Properties
    {
        _MainTex ("Base Color", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TextureDetail ("Texture Detail Strength", Range(0,1)) = 1
        _CanopyNormalBlend ("Canopy Volume Normal Blend", Range(0,1)) = 0
        _CanopyCenter ("Local Canopy Center", Vector) = (0,0,0,0)

        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.4
        _UseAlphaClip ("Use Alpha Clip", Range(0,1)) = 0

        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull ("Cull", Float) = 2

        _TopLightStrength ("Cheap Sun Shape", Range(0,1)) = 0.28
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 1
        _NightDesaturate ("Night Desaturate", Range(0,1)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 80
        Cull [_Cull]
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            // Shared presentation globals stay outside the material buffer.
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _TextureDetail;
                half _CanopyNormalBlend;
                float4 _CanopyCenter;

                half _Cutoff;
                half _UseAlphaClip;
                half _TopLightStrength;
                half _AmbientStrength;
                half _NightDesaturate;
                half _Cull;
            CBUFFER_END

            half4 _LC_SunDirection;
            half4 _LC_SunColor;
            half4 _LC_AmbientColor;
            half4 _LC_AmbientSkyColor;
            half4 _LC_AmbientEquatorColor;
            half4 _LC_AmbientGroundColor;
            half _LC_NightAmount;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 worldNormal : TEXCOORD1;

                float fogCoord : TEXCOORD2;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs positions = GetVertexPositionInputs(v.vertex.xyz);
                o.position = positions.positionCS;

                o.uv =
                    TRANSFORM_TEX(
                        v.uv,
                        _MainTex);

                o.worldNormal =
                    TransformObjectToWorldNormal(
                        v.normal);
                float3 canopyDirection = normalize(v.vertex.xyz - _CanopyCenter.xyz + float3(0,0.0001,0));
                o.worldNormal = normalize(lerp(o.worldNormal,
                    TransformObjectToWorldNormal(canopyDirection), _CanopyNormalBlend));

                // URP 17.5 evaluates fog per fragment from interpolated view Z.
                // Keep one scalar varying instead of a world-position varying.
                o.fogCoord = positions.positionVS.z;

                return o;
            }

            half3 EvaluateCheapAmbient(
                half3 normal)
            {
                half up =
                    saturate(
                        normal.y);

                half down =
                    saturate(
                        -normal.y);

                half side =
                    saturate(
                        1.0h -
                        abs(normal.y));

                return
                    _LC_AmbientSkyColor.rgb *
                        up +
                    _LC_AmbientEquatorColor.rgb *
                        side +
                    _LC_AmbientGroundColor.rgb *
                        down;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 baseSample =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        i.uv) *
                    _Color;
                baseSample.rgb = lerp(_Color.rgb * 0.65h, baseSample.rgb, _TextureDetail);

                if (_UseAlphaClip > 0.5h)
                {
                    clip(
                        baseSample.a -
                        _Cutoff);
                }

                half3 normal =
                    normalize(
                        i.worldNormal);

                half3 ambient =
                    EvaluateCheapAmbient(
                        normal) *
                    _AmbientStrength;

                half cheapSun =
                    saturate(
                        dot(
                            normal,
                            normalize(
                                _LC_SunDirection.xyz)) *
                        0.5h +
                        0.5h);

                half3 lightColor =
                    ambient +
                    _LC_SunColor.rgb *
                    cheapSun *
                    _TopLightStrength;

                half luminance =
                    dot(
                        baseSample.rgb,
                        half3(
                            0.2126h,
                            0.7152h,
                            0.0722h));

                half3 nightColor =
                    lerp(
                        baseSample.rgb,
                        luminance.xxx,
                        _NightDesaturate *
                        _LC_NightAmount);

                half4 result =
                    half4(
                        nightColor *
                        lightColor,
                        1.0);

                float nearToFarZ = max(-i.fogCoord - _ProjectionParams.y, 0.0);
                half fogFactor = ComputeFogFactorZ0ToFar(nearToFarZ);
                result.rgb = MixFog(result.rgb, fogFactor);

                return result;
            }

            ENDHLSL
        }
    }

    // Intentionally no ShadowCaster and no ForwardAdd pass.
    Fallback Off
}
