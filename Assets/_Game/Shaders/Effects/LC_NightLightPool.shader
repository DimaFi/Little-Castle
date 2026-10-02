Shader "Little Castle/Effects/LC Night Light Pool"
{
    Properties
    {
        [HDR] _Color ("Light Color", Color) = (1.4,0.65,0.18,1)
        _Intensity ("Intensity", Range(0,4)) = 1.0
        _InnerRadius ("Inner Radius", Range(0,0.95)) = 0.18
        _EdgeSoftness ("Edge Softness", Range(0.02,1)) = 0.62
        _NightThreshold ("Night Threshold", Range(0,1)) = 0.12
        _FlickerStrength ("Flicker Strength", Range(0,0.25)) = 0.025
        _FlickerSpeed ("Flicker Speed", Range(0,12)) = 2.6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Intensity;
            half _InnerRadius;
            half _EdgeSoftness;
            half _NightThreshold;
            half _FlickerStrength;
            half _FlickerSpeed;

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
                    v.uv;

                o.worldPosition =
                    mul(
                        unity_ObjectToWorld,
                        v.vertex).xyz;

                UNITY_TRANSFER_FOG(
                    o,
                    o.position);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float2 centered =
                    i.uv * 2.0 - 1.0;

                float radius =
                    length(
                        centered);

                half outerStart =
                    saturate(
                        _InnerRadius);

                half outerEnd =
                    max(
                        outerStart + 0.001h,
                        outerStart +
                        _EdgeSoftness);

                half radial =
                    1.0h -
                    smoothstep(
                        outerStart,
                        outerEnd,
                        radius);

                radial *=
                    1.0h -
                    smoothstep(
                        0.96h,
                        1.02h,
                        radius);

                half night =
                    smoothstep(
                        _NightThreshold,
                        1.0h,
                        _LC_NightAmount);

                float3 cell =
                    floor(
                        i.worldPosition *
                        0.2);

                half flicker =
                    1.0h +
                    sin(
                        _LC_GameTime.z *
                            _FlickerSpeed +
                        dot(
                            cell,
                            float3(
                                0.71,
                                1.33,
                                2.17))) *
                    _FlickerStrength;

                half strength =
                    radial *
                    night *
                    _Intensity *
                    max(
                        0.0h,
                        flicker);

                fixed4 result =
                    fixed4(
                        _Color.rgb *
                            strength,
                        _Color.a *
                            radial *
                            night);

                // Additive light should fade toward black under fog rather
                // than toward the fog color itself.
                UNITY_APPLY_FOG_COLOR(
                    i.fogCoord,
                    result,
                    fixed4(
                        0,
                        0,
                        0,
                        0));

                return result;
            }

            ENDCG
        }
    }

    Fallback Off
}
