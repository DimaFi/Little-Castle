// Owned experimental implementation informed by Shader Graph 17.5 Production Ready
// WaterStream/WaterStreamFalls: two-phase flowing ripples, texture foam, slope foam
// and falls endpoint fade. Not an exported graph, refraction or hydraulic simulation.
// Unity sample textures and original license/provenance: ../Textures/UnitySampleV1/.
Shader "Little Castle/Experimental/Water V1"
{
    Properties
    {
        [NoScaleOffset] _NormalTex ("Packed Linear Ripple Normal (Default Importer)", 2D) = "bump" {}
        [NoScaleOffset] _FoamTex ("Packed Linear Foam", 2D) = "gray" {}
        [NoScaleOffset] _PhaseTex ("Linear Flow Phase Noise", 2D) = "gray" {}
        [Toggle] _IsFall ("Waterfall Surface", Float) = 0
        _WaterColor ("Water Color", Color) = (0.07,0.31,0.37,1)
        _FoamColor ("Foam Color", Color) = (0.75,0.85,0.86,1)
        _Opacity ("Opacity", Range(0,1)) = 0.88
        _FlowSpeed ("Downstream Speed (Metres Per Second)", Float) = 1.4
        _FlowDistance ("Two Phase Flow Distance (Metres)", Float) = 3
        _FlowOffset ("Continuous Arc Length Offset (Metres)", Float) = 0
        _NormalScale ("Normal Scale (Across / Per Metre)", Vector) = (2.2,0.3,0,0)
        _FoamScale ("Foam Scale (Across / Per Metre)", Vector) = (2.8,0.23,0,0)
        _NormalStrength ("Ripple Normal Strength", Range(0,1)) = 0.22
        _FoamStrength ("Foam Strength", Range(0,2)) = 0.8
        _FoamCutoff ("Foam Texture Threshold", Range(0,1)) = 0.33
        _EdgeSoftness ("Broad Edge Fade (Across Fraction)", Range(0.01,0.35)) = 0.13
        _EndFade ("Fall End Fade (Station Fraction)", Range(0.001,0.3)) = 0.075
        _ImpactPosition ("Landing Impact World Position", Vector) = (0,0,0,0)
        _ImpactRadius ("Impact Foam Radius (Zero Disables)", Float) = 0
        _ImpactStrength ("Impact Foam Strength", Range(0,2)) = 1
        [Toggle] _UseDepthFade ("Optional Camera Depth Fade", Float) = 0
        _DepthFadeDistance ("Depth Intersection Fade (Metres)", Float) = 0.35
        [Toggle] _UseSharedLighting ("Use Existing LC Lighting Globals", Float) = 1
        [HideInInspector] _PreviewTime ("Absolute Preview Time (-1 Uses Game Time)", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "WaterV1"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVert
            #pragma fragment WaterFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);
            TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);
            TEXTURE2D(_PhaseTex); SAMPLER(sampler_PhaseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _WaterColor, _FoamColor, _NormalScale, _FoamScale, _ImpactPosition;
                float _IsFall, _Opacity, _FlowSpeed, _FlowDistance, _FlowOffset;
                float _NormalStrength, _FoamStrength, _FoamCutoff, _EdgeSoftness, _EndFade;
                float _ImpactRadius, _ImpactStrength, _UseDepthFade, _DepthFadeDistance;
                float _UseSharedLighting, _PreviewTime;
            CBUFFER_END

            // Read-only consumers of existing presentation state. Never set globals.
            half4 _LC_SunDirection, _LC_SunColor, _LC_MoonDirection, _LC_MoonColor;
            half4 _LC_AmbientColor, _LC_AmbientSkyColor, _LC_AmbientEquatorColor, _LC_AmbientGroundColor;

            struct WaterAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 station : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct WaterVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float2 station : TEXCOORD3;
                float viewZ : TEXCOORD4;
                half endMask : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            WaterVaryings WaterVert(WaterAttributes input)
            {
                WaterVaryings output = (WaterVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.viewZ = p.positionVS.z;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.station = input.station;
                output.endMask = input.color.r;
                return output;
            }

            // Texture data is sampled as Default/linear, blended before decoding.
            half2 FlowNormal(float2 uv, float2 velocity, float phaseA, float phaseB, half weightA)
            {
                half2 a = SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv - velocity * phaseA).rg;
                half2 b = SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv - velocity * phaseB).rg;
                return (lerp(b, a, weightA) * 2 - 1) * _NormalStrength;
            }
            half FoamValue(half3 packed)
            {
                // Green supplies coherent foam islands, blue adds fine bubbles.
                return saturate(packed.g * 1.25h + packed.b * 0.3h);
            }
            half FlowFoam(float2 uv, float2 velocity, float phaseA, float phaseB, half weightA)
            {
                half a = FoamValue(SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv - velocity * phaseA).rgb);
                half b = FoamValue(SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv - velocity * phaseB).rgb);
                return lerp(b, a, weightA);
            }
            half3 WaterLighting(half3 albedo, half3 n)
            {
                float signal = dot(_LC_SunDirection.xyz, _LC_SunDirection.xyz)
                    + dot(_LC_MoonDirection.xyz, _LC_MoonDirection.xyz)
                    + dot(abs(_LC_AmbientColor.rgb) + abs(_LC_AmbientSkyColor.rgb), half3(1,1,1));
                if (_UseSharedLighting > 0.5 && signal > 0.0001)
                {
                    half3 ambient = _LC_AmbientSkyColor.rgb * saturate(n.y)
                        + _LC_AmbientEquatorColor.rgb * saturate(1 - abs(n.y))
                        + _LC_AmbientGroundColor.rgb * saturate(-n.y);
                    half hemisphere = dot(abs(_LC_AmbientSkyColor.rgb) + abs(_LC_AmbientEquatorColor.rgb)
                        + abs(_LC_AmbientGroundColor.rgb), half3(1,1,1));
                    if (hemisphere < 0.0001h) ambient = _LC_AmbientColor.rgb;
                    half sun = 0.35h + 0.65h * saturate(dot(n, SafeNormalize(_LC_SunDirection.xyz)));
                    half moon = 0.35h + 0.65h * saturate(dot(n, SafeNormalize(_LC_MoonDirection.xyz)));
                    return albedo * (max(ambient, half3(0.025,0.03,0.035))
                        + _LC_SunColor.rgb * sun + _LC_MoonColor.rgb * moon);
                }
                Light light = GetMainLight();
                half diffuse = 0.4h + 0.6h * saturate(dot(n, light.direction));
                return albedo * (max(SampleSH(n), half3(0.18,0.2,0.22))
                    + light.color * diffuse * light.distanceAttenuation);
            }

            half4 WaterFrag(WaterVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float time = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                half isFall = saturate(_IsFall);
                float2 riverUV = float2(input.uv.x, input.uv.y + _FlowOffset);
                half crossSection = saturate(1 - abs(riverUV.x * 2 - 1));
                // Slower edges and faster centre, same velocity convention on all pieces.
                float flowDistance = max(abs(_FlowDistance), 0.05);
                half phaseNoise = SAMPLE_TEXTURE2D(_PhaseTex, sampler_PhaseTex,
                    float2(riverUV.x * 0.71, riverUV.y * 0.025)).r;
                float clock = time * _FlowSpeed * lerp(0.55, 1.0, crossSection) / flowDistance;
                float phaseA = frac(clock + phaseNoise * 0.65);
                float phaseB = frac(phaseA + 0.5);
                half weightA = 1 - abs(phaseA * 2 - 1);
                float2 drift = float2((phaseNoise - 0.5) * 0.15, 1);
                half2 ripple = FlowNormal(riverUV * _NormalScale.xy,
                    drift * flowDistance * _NormalScale.xy, phaseA, phaseB, weightA);
                half detailFoam = FlowFoam(riverUV * _FoamScale.xy + ripple * 0.28,
                    drift * flowDistance * _FoamScale.xy, phaseA, phaseB, weightA);
                half coarse = SAMPLE_TEXTURE2D(_PhaseTex, sampler_PhaseTex,
                    float2(riverUV.x * 1.37 + ripple.x, riverUV.y * 0.081 - time * _FlowSpeed * 0.033)).r;

                // Derivative frame follows actual U/across and V/arc-length geometry.
                // Tangents are deliberately not required on procedurally built meshes.
                half3 geometric = SafeNormalize(input.normalWS);
                float3 px = ddx(input.positionWS), py = ddy(input.positionWS);
                float2 ux = ddx(input.uv), uy = ddy(input.uv);
                float determinant = ux.x * uy.y - ux.y * uy.x;
                float orientation = determinant < 0 ? -1 : 1;
                half3 tangent = SafeNormalize((px * uy.y - py * ux.y) * orientation);
                half3 bitangent = SafeNormalize((py * ux.x - px * uy.x) * orientation);
                half3 normalWS = SafeNormalize(geometric + tangent * ripple.x + bitangent * ripple.y);
                half vertical = saturate(1 - abs(geometric.y));

                // Broad broken margins, never regularly spaced stripe/sine emission.
                float edgeDistance = min(riverUV.x, 1 - riverUV.x);
                float edgeWidth = max(_EdgeSoftness, 0.01) * lerp(0.8, 1.65, isFall);
                float edgeBreak = (coarse - 0.5) * edgeWidth * lerp(0.3, 0.8, isFall);
                half edgeMask = smoothstep(0, edgeWidth, edgeDistance + edgeBreak);
                half bankFoam = 1 - smoothstep(edgeWidth * 0.65, edgeWidth * 2.5, edgeDistance);
                half foamPattern = smoothstep(_FoamCutoff, min(_FoamCutoff + 0.3, 1.01), detailFoam);
                half coverage = lerp(bankFoam * 0.32h + 0.035h,
                    0.28h + vertical * 0.7h + coarse * 0.2h, isFall);
                half foam = saturate(foamPattern * coverage * _FoamStrength);

                // Local landing stamp is depth-independent and altitude-gated so it
                // cannot accidentally paint the upstream pool at the same XZ.
                if (_ImpactRadius > 0.001 && isFall < 0.5)
                {
                    float2 delta = (input.positionWS.xz - _ImpactPosition.xz) / _ImpactRadius;
                    float radius = length(delta);
                    half stampNoise = FoamValue(SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex,
                        delta * 1.25 + float2(time * 0.035, -time * 0.023)).rgb);
                    half envelope = 1 - smoothstep(0.3 + stampNoise * 0.12, 1.0, radius);
                    half altitude = 1 - smoothstep(0.35, 2.5, abs(input.positionWS.y - _ImpactPosition.y));
                    half impact = envelope * altitude * smoothstep(0.12, 0.52, stampNoise) * _ImpactStrength;
                    foam = saturate(max(foam, impact));
                }

                half3 water = _WaterColor.rgb * lerp(0.92h, 1.08h, saturate(ripple.y * 1.5 + 0.5));
                half3 albedo = lerp(water, _FoamColor.rgb, foam);
                half3 color = WaterLighting(albedo, normalWS);
                half alpha = saturate(_Opacity + foam * 0.08h) * edgeMask;
                if (isFall > 0.5)
                {
                    // UV2 = (across, normalized station); vertex R carries the
                    // geometry-authored fade. Horizontal pieces stay continuous.
                    float station = saturate(input.station.y);
                    float fade = max(_EndFade, 0.001);
                    half endpoint = smoothstep(0, fade, station) * (1 - smoothstep(1 - fade, 1, station));
                    alpha *= endpoint * saturate(input.endMask);
                }
                if (_UseDepthFade > 0.5)
                {
                    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float waterDepth = max(-input.viewZ, 0);
                    alpha *= saturate((sceneDepth - waterDepth) / max(_DepthFadeDistance, 0.01));
                }
                half fog = ComputeFogFactorZ0ToFar(max(-input.viewZ - _ProjectionParams.y, 0));
                return half4(MixFog(color, fog), alpha);
            }
            ENDHLSL
        }
    }
    // No SceneColor, VFX, global writes, displacement, depth/shadow writer or emission.
    Fallback Off
}
