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
        }

        LOD 80
        Cull [_Cull]
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM

            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _TextureDetail;
            half _CanopyNormalBlend;
            float4 _CanopyCenter;

            half _Cutoff;
            half _UseAlphaClip;
            half _TopLightStrength;
            half _AmbientStrength;
            half _NightDesaturate;

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

                UNITY_FOG_COORDS(2)

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.position =
                    UnityObjectToClipPos(
                        v.vertex);

                o.uv =
                    TRANSFORM_TEX(
                        v.uv,
                        _MainTex);

                o.worldNormal =
                    UnityObjectToWorldNormal(
                        v.normal);
                float3 canopyDirection = normalize(v.vertex.xyz - _CanopyCenter.xyz + float3(0,0.0001,0));
                o.worldNormal = normalize(lerp(o.worldNormal,
                    UnityObjectToWorldNormal(canopyDirection), _CanopyNormalBlend));

                UNITY_TRANSFER_FOG(
                    o,
                    o.position);

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

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                fixed4 sample =
                    tex2D(
                        _MainTex,
                        i.uv) *
                    _Color;
                sample.rgb = lerp(_Color.rgb * 0.65h, sample.rgb, _TextureDetail);

                if (_UseAlphaClip > 0.5h)
                {
                    clip(
                        sample.a -
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
                        sample.rgb,
                        half3(
                            0.2126h,
                            0.7152h,
                            0.0722h));

                half3 nightColor =
                    lerp(
                        sample.rgb,
                        luminance.xxx,
                        _NightDesaturate *
                        _LC_NightAmount);

                fixed4 result =
                    fixed4(
                        nightColor *
                        lightColor,
                        1.0);

                UNITY_APPLY_FOG(
                    i.fogCoord,
                    result);

                return result;
            }

            ENDCG
        }
    }

    // Intentionally no ShadowCaster and no ForwardAdd pass.
    Fallback Off
}
