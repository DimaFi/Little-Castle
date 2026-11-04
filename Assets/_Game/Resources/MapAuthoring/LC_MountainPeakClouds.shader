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
            #include "../../Shaders/Sky/LC_CloudNoise.hlsl"

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
                o.viewDir = normalize(center - _WorldSpaceCameraPos.xyz);
                return o;
            }

            half4 frag(V input) : SV_Target
            {
                float2 p = (input.uv - .5) * 2;
                float2 noiseUV = input.uv * 3.6 + input.seed * 8.3
                    + float2(_Time.y * .009, _Time.y * .005);
                float broad = LCCloudNoise(noiseUV);
                float small = LCCloudNoise(noiseUV * 2.16 + 31.7);
                float distortion = (broad - .5) * .27 +
                                   (small - .5) * .11;
                float shape = length(float2(p.x * .86, p.y * 1.17))
                    + distortion;
                float boundary = 1 - smoothstep(.52, 1.04, shape);
                float soft = saturate(boundary * (.76 + broad * .30));
                float storm = saturate(_LC_MountainStormCloud);
                float alpha = soft * storm * _Opacity * input.distanceFade;

                float underside = saturate(1 - input.uv.y);
                float3 dark = float3(.13,.18,.24);
                float3 lit = float3(.35,.40,.48);
                float3 col = lerp(dark, lit,
                    saturate(.17 + input.uv.y * .39 + small * .18));

                float3 flashDir = normalize(
                    _LC_StormFlashDirection.xyz + float3(.00001,.00001,.00001));
                float flash = pow(saturate(dot(input.viewDir, flashDir)), 14);
                col = lerp(col, float3(.72,.82,1), saturate(_LC_StormFlash) * flash);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
