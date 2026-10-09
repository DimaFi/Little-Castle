Shader "Little Castle/Terrain/LC River Water"
{
    Properties
    {
        _DayColor ("Day River Tint", Color) = (0.21,0.43,0.48,0.72)
        _NightColor ("Night River Tint", Color) = (0.11,0.22,0.31,0.79)
        _FlowDirection ("Decorative Flow XZ", Vector) = (1,0,0,0)
        _RippleScale ("Large Ripple Spatial Frequency", Range(0.02,2)) = 0.42
        _FlowSpeed ("Gentle Flow Speed", Range(0,1)) = 0.12
        _RippleStrength ("Gentle Ripple Strength", Range(0,0.18)) = 0.055
        _ShoreSlopeStart ("Shore Grade Response Start", Range(0,1)) = 0.08
        _ShoreSlopeGain ("Shore Grade Response Gain", Range(0,24)) = 8
        _ShoreContrast ("Subtle Shore Contrast", Range(0,0.25)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "IgnoreProjector" = "True"
        }

        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Back

        Pass
        {
            Name "RIVER_FORWARD"

            CGPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            half4 _DayColor;
            half4 _NightColor;
            float4 _FlowDirection;
            half _RippleScale;
            half _FlowSpeed;
            half _RippleStrength;
            half _ShoreSlopeStart;
            half _ShoreSlopeGain;
            half _ShoreContrast;

            // Existing client-side atmospheric presentation global.
            // This shader changes no world time/gameplay rules.
            half _LC_NightAmount;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 clipPosition : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.clipPosition = UnityObjectToClipPos(input.vertex);
                output.worldPosition =
                    mul(unity_ObjectToWorld, input.vertex).xyz;

                UNITY_TRANSFER_FOG(output, output.clipPosition);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half night = saturate(_LC_NightAmount);
                half4 tint = lerp(_DayColor, _NightColor, night);

                // One transparent pass; no depth/opaque texture, refraction,
                // tessellation or normal-texture lookup. Very low-amplitude
                // world-anchored movement prevents UV seams across chunks.
                float2 flow = _FlowDirection.xy;
                flow *= rsqrt(max(dot(flow, flow), 0.0001));
                float2 uv =
                    input.worldPosition.xz * _RippleScale -
                    flow * (_Time.y * _FlowSpeed);

                half ripple = (
                    sin(dot(uv, float2(0.87, 0.41)) * 2.1) +
                    sin(dot(uv, float2(-0.34, 1.04)) * 1.5 + 1.7)
                ) * 0.5h;

                // Approximate gentle near-bank brightening using existing
                // water-height slope. NOT an exact shoreline mask.
                // No depth buffer is sampled, including near bridge stamps.
                half apparentGrade =
                    fwidth(input.worldPosition.y) * _ShoreSlopeGain;
                half softShore = smoothstep(
                    _ShoreSlopeStart,
                    _ShoreSlopeStart + 0.15h,
                    apparentGrade);

                half shade =
                    1.0h +
                    ripple * _RippleStrength +
                    softShore * _ShoreContrast;

                fixed4 result = fixed4(
                    saturate(tint.rgb * shade),
                    saturate(tint.a));

                UNITY_APPLY_FOG(input.fogCoord, result);
                return result;
            }

            ENDCG
        }
    }

    Fallback Off
}
