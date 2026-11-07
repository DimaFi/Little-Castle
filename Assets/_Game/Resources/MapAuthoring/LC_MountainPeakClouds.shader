Shader "Little Castle/Weather/Mountain Peak Storm Clouds"
{
    Properties
    {
        _CloudWidth ("Billboard Width (Metres)", Float) = 36
        _CloudHeight ("Billboard Height (Metres)", Float) = 25
        _Opacity ("Peak Cloud Opacity", Range(0,1)) = .66
        _FrontOffset ("View Offset Towards Camera", Range(0,40)) = 16
        _MinimumDistance ("Camera Safety Distance", Float) = 115
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline"
               "RenderType"="Transparent" "Queue"="Transparent+4" }
        Pass
        {
            Name "Distant storm peak cloud"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../Shaders/Sky/LC_StormCloudField.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CloudWidth, _CloudHeight;
                float _Opacity, _FrontOffset, _MinimumDistance;
            CBUFFER_END

            float _LC_MountainStormCloud;
            float _LC_StormFlash;
            float4 _LC_StormFlashDirection;

            struct A
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 seed : TEXCOORD1;
            };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 seed : TEXCOORD1;
                float distanceFade : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
            };

            V vert(A input)
            {
                V o;
                float3 center = TransformObjectToWorld(input.positionOS.xyz);
                float3 view = _WorldSpaceCameraPos.xyz - center;
                float distance = length(view);
                float3 toward = view / max(distance, .01);
                // Camera-facing cards are strictly attached to distant peaks,
                // not parented to / floating in front of the gameplay camera.
                center += toward * _FrontOffset;
                float3 right = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up = UNITY_MATRIX_I_V._m01_m11_m21;
                float variable = .78 + input.seed.x * .44;
                float wind = sin(_Time.y * .09 + input.seed.x * 23) * 1.1;
                float3 position = center + right *
                    ((input.uv.x - .5) * _CloudWidth * variable + wind) +
                    up * ((input.uv.y - .5) * _CloudHeight * variable);
                o.positionCS = TransformWorldToHClip(position);
                o.uv = input.uv;
                o.seed = input.seed;
                o.distanceFade = smoothstep(_MinimumDistance,
                    _MinimumDistance + 65, distance);
                o.viewDir = position - _WorldSpaceCameraPos.xyz;
                return o;
            }

            half4 frag(V input) : SV_Target
            {
                // Three joined puffy heads per card give a scalloped silhouette,
                // unlike the previous one smooth ellipsoid with a dark stripe.
                float2 p = (input.uv - .5) * 2.0;
                float3 volumeP = float3(input.uv * 5.3, input.seed.x * 13.7) +
                    float3(_Time.y * .015, _Time.y * .006, .0);
                float broad, fine;
                float3 slope;
                LCStormField(volumeP, 1.0, broad, fine, slope);
                float erosion = (broad - .5) * .35 + (fine - .5) * .18;
                float r0 = length((p - float2(-.39,-.07)) * float2(.96,1.17));
                float r1 = length((p - float2(.02,.15)) * float2(.88,1.00));
                float r2 = length((p - float2(.40,-.08)) * float2(.95,1.23));
                float lobeDistance = min(r0, min(r1, r2)) + erosion;
                float silhouette = 1.0 - smoothstep(.56,.98,lobeDistance);

                // No rectangular texture-card edges, even if the noise happens
                // to extend its silhouette beyond the underlying quad.
                float feather = smoothstep(.0,.13,input.uv.x) *
                    (1 - smoothstep(.87,1.0,input.uv.x)) *
                    smoothstep(.0,.13,input.uv.y) *
                    (1 - smoothstep(.87,1.0,input.uv.y));
                float storm = saturate(_LC_MountainStormCloud);
                float alpha = silhouette * feather * storm * _Opacity *
                    input.distanceFade * lerp(.45,.78,broad) * (1 - .55 * input.uv.y);

                float relief = saturate(.43 - dot(slope, float3(-.38,.88,.28)) * 1.5);
                float lightSide = saturate(relief * .44 + input.uv.y * .25);
                float3 dark = float3(.023,.035,.060);
                float3 lit = float3(.105,.151,.224);
                float3 col = lerp(dark, lit, lightSide);
                float flash = LCStormFlashHalo(normalize(input.viewDir),
                    _LC_StormFlashDirection, _LC_StormFlash);
                col += flash * (.14 + relief * .62) * float3(.60,.69,1.0);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
