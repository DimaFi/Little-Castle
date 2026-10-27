Shader "Little Castle/Surface/LC Stylized Lit"
{
    Properties
    {
        _LightResponse ("Continuous Diffuse", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _MainTex ("Base Color", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1

        _OcclusionMap ("Ambient Occlusion", 2D) = "white" {}
        _OcclusionStrength ("AO Strength", Range(0,1)) = 0.75

        _RoughnessMap ("Roughness Map", 2D) = "white" {}
        _Roughness ("Base Roughness", Range(0,1)) = 0.68
        _RoughnessMapStrength ("Roughness Map Strength", Range(0,1)) = 0

        _EmissionMap ("Emission", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,0)
        _EmissionDayStrength ("Emission Day Strength", Range(0,4)) = 0
        _EmissionNightStrength ("Emission Night Strength", Range(0,8)) = 2.4
        _EmissionFlickerStrength ("Emission Flicker", Range(0,0.3)) = 0.035
        _EmissionFlickerSpeed ("Emission Flicker Speed", Range(0,12)) = 3.2

        _LightWrap ("Light Wrap", Range(0,0.8)) = 0.24
        _ShadowSoftness ("Light / Shadow Softness", Range(0.03,0.8)) = 0.34
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.85
        _ShadowTintStrength ("Shadow Tint Strength", Range(0,1)) = 0.48

        _SpecularStrength ("Soft Specular", Range(0,1)) = 0.08
        _SpecularPower ("Specular Size", Range(4,128)) = 28

        _RimStrength ("Soft Rim", Range(0,1)) = 0.05
        _RimPower ("Rim Size", Range(1,8)) = 3.5

        _LocalLightStrength ("Local Light Strength", Range(0,2)) = 0.82
        _LocalLightSpecular ("Local Light Specular", Range(0,1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 250

        // One identical layout for every pass. Presentation globals are intentionally
        // outside UnityPerMaterial so Shader.SetGlobal* remains authoritative.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "LC_LightingCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half4 _EmissionColor;
            half _Cull;
            half _BumpScale;
            half _OcclusionStrength;
            half _Roughness;
            half _RoughnessMapStrength;
            half _EmissionDayStrength;
            half _EmissionNightStrength;
            half _EmissionFlickerStrength;
            half _EmissionFlickerSpeed;
            half _LightResponse;
            half _LightWrap;
            half _ShadowSoftness;
            half _AmbientStrength;
            half _ShadowTintStrength;
            half _SpecularStrength;
            half _SpecularPower;
            half _RimStrength;
            half _RimPower;
            half _LocalLightStrength;
            half _LocalLightSpecular;
        CBUFFER_END

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

        half4 _LC_SunDirection;
        half4 _LC_SunColor;
        half4 _LC_MoonDirection;
        half4 _LC_MoonColor;
        half4 _LC_AmbientColor;
        half4 _LC_AmbientSkyColor;
        half4 _LC_AmbientEquatorColor;
        half4 _LC_AmbientGroundColor;
        half4 _LC_ShadowTint;
        half _LC_Daylight;
        half _LC_Twilight;
        half _LC_NightAmount;
        float4 _LC_GameTime;
        ENDHLSL

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LCVert
            #pragma fragment LCFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LC_SPATIAL_INDIRECT
            #pragma target 4.5 _LC_SPATIAL_INDIRECT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            struct LCAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct LCVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 tangentWS : TEXCOORD3;
                half3 bitangentWS : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            LCVaryings LCVert(LCAttributes input)
            {
                LCVaryings output = (LCVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.tangentWS = normals.tangentWS;
                output.bitangentWS = normals.bitangentWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half3 LCEvaluateNormal(LCVaryings input)
            {
                half3 tangentNormal = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                return normalize(tangentNormal.x * normalize(input.tangentWS)
                    + tangentNormal.y * normalize(input.bitangentWS)
                    + tangentNormal.z * normalize(input.normalWS));
            }

            half LCEvaluateDiffuse(half3 normal, half3 lightDirection)
            {
                half wrapped = saturate((dot(normal, lightDirection) + _LightWrap) / (1.0h + _LightWrap));
                half halfWidth = max(0.015h, _ShadowSoftness * 0.5h);
                return lerp(smoothstep(0.42h - halfWidth, 0.42h + halfWidth, wrapped), wrapped, _LightResponse);
            }

            half3 LCEvaluateAmbient(half3 normal)
            {
                return _LC_AmbientSkyColor.rgb * saturate(normal.y)
                    + _LC_AmbientEquatorColor.rgb * saturate(1.0h - abs(normal.y))
                    + _LC_AmbientGroundColor.rgb * saturate(-normal.y);
            }

            // LC already evaluates its sun and moon from the shared globals.
            // Restrict the local-light loop to Point/Spot to avoid counting them twice.
            bool LCIsPunctualLight(uint index)
            {
                #if USE_CLUSTER_LIGHT_LOOP
                    int perObjectIndex = (int)index;
                #else
                    int perObjectIndex = GetPerObjectLightIndex(index);
                #endif
                #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                    return _AdditionalLightsBuffer[perObjectIndex].position.w != 0.0;
                #else
                    return _AdditionalLightsPosition[perObjectIndex].w != 0.0;
                #endif
            }

            half3 LCEvaluateLocalLight(Light light, half3 normal, half3 viewDirection,
                half3 albedo, half smoothness)
            {
                half wrap = _LightWrap * 0.35h;
                half wrapped = saturate((dot(normal, light.direction) + wrap) / (1.0h + wrap));
                half halfWidth = max(0.015h, _ShadowSoftness * 0.34h);
                half diffuse = smoothstep(0.38h - halfWidth, 0.38h + halfWidth, wrapped);
                half specularPower = lerp(6.0h, _SpecularPower, smoothness * smoothness);
                half specularStrength = _LocalLightSpecular * lerp(0.28h, 1.0h, smoothness);
                half specular = pow(saturate(dot(normal, SafeNormalize(light.direction + viewDirection))),
                    specularPower) * specularStrength;
                half3 localColor = light.color * (light.distanceAttenuation * light.shadowAttenuation)
                    * _LocalLightStrength;
                return (albedo + specular) * localColor * diffuse;
            }

            half4 LCFrag(LCVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                half3 normal = LCEvaluateNormal(input);
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 sunDirection = SafeNormalize(_LC_SunDirection.xyz);
                half3 moonDirection = SafeNormalize(_LC_MoonDirection.xyz);

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                // URP can choose sun or moon as its main directional light. Apply its
                // shadow only to the matching LC light; the other remains unshadowed.
                half sunShadow = dot(mainLight.direction, sunDirection) > 0.95h
                    ? mainLight.shadowAttenuation : 1.0h;
                half moonShadow = dot(mainLight.direction, moonDirection) > 0.95h
                    ? mainLight.shadowAttenuation : 1.0h;
                half sunDiffuse = LCEvaluateDiffuse(normal, sunDirection) * sunShadow;
                half moonDiffuse = LCEvaluateDiffuse(normal, moonDirection) * moonShadow;

                half occlusion = lerp(1.0h,
                    SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).r, _OcclusionStrength);
                half directPresence = saturate(sunDiffuse + moonDiffuse * 0.45h);
                half3 shadowTint = lerp(half3(1, 1, 1), _LC_ShadowTint.rgb, _ShadowTintStrength);
                half3 ambientTint = lerp(shadowTint, half3(1, 1, 1), directPresence);
                half3 ambient = LCSpatialIndirect(input.positionWS, normal, LCEvaluateAmbient(normal)) * _AmbientStrength * ambientTint
                    * LCIndirectOcclusion(input.positionCS, occlusion);
                half directOcclusion = lerp(occlusion, 1.0h, 0.72h);
                half3 lighting = ambient + (_LC_SunColor.rgb * sunDiffuse
                    + _LC_MoonColor.rgb * moonDiffuse) * directOcclusion;

                half roughness = saturate(lerp(_Roughness,
                    SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, input.uv).r, _RoughnessMapStrength));
                half smoothness = 1.0h - roughness;
                half specularPower = lerp(6.0h, _SpecularPower, smoothness * smoothness);
                half specularStrength = _SpecularStrength * lerp(0.32h, 1.0h, smoothness);
                half sunSpecular = pow(saturate(dot(normal, SafeNormalize(sunDirection + viewDirection))),
                    specularPower) * specularStrength * sunDiffuse;
                half moonSpecular = pow(saturate(dot(normal, SafeNormalize(moonDirection + viewDirection))),
                    specularPower) * (specularStrength * 0.35h) * moonDiffuse;
                half fresnel = pow(1.0h - saturate(dot(normal, viewDirection)), _RimPower);
                half3 rimColor = lerp(_LC_AmbientColor.rgb,
                    _LC_SunColor.rgb + _LC_MoonColor.rgb, directPresence);
                half3 finalColor = albedo.rgb * (lighting + rimColor * fresnel * _RimStrength)
                    + _LC_SunColor.rgb * sunSpecular + _LC_MoonColor.rgb * moonSpecular;

                // Evaluate the same local-light shape in both Forward and clustered
                // Forward+. The vertex-light setting still evaluates LC's shape per pixel.
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        if (LCIsPunctualLight(lightIndex))
                        {
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                            finalColor += LCEvaluateLocalLight(light, normal, viewDirection, albedo.rgb, smoothness);
                        }
                    LIGHT_LOOP_END
                #endif

                half emissionStrength = lerp(_EmissionDayStrength, _EmissionNightStrength, _LC_NightAmount);
                float3 flickerCell = floor(input.positionWS * 0.35);
                half flicker = 1.0h + sin(_LC_GameTime.z * _EmissionFlickerSpeed
                    + dot(flickerCell, float3(0.73, 1.37, 2.11))) * _EmissionFlickerStrength;
                finalColor += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb
                    * _EmissionColor.rgb * emissionStrength * max(0.0h, flicker);
                half fogFactor = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                return half4(MixFog(finalColor, fogFactor), albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LCShadowVert
            #pragma fragment LCShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            float3 _LightDirection;
            float3 _LightPosition;

            struct LCShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct LCShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            LCShadowVaryings LCShadowVert(LCShadowAttributes input)
            {
                LCShadowVaryings output = (LCShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }

            half4 LCShadowFrag(LCShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LCDepthVert
            #pragma fragment LCDepthFrag
            #pragma multi_compile_instancing

            struct LCDepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct LCDepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            LCDepthVaryings LCDepthVert(LCDepthAttributes input)
            {
                LCDepthVaryings output = (LCDepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half LCDepthFrag(LCDepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
