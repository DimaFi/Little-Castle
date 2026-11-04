Shader "Little Castle/Experimental/Mountain Border Preview"
{
    Properties
    {
        [NoScaleOffset] _RockTex ("Rock Base Color", 2D) = "white" {}
        _RockTint ("Rock Tint", Color) = (0.62,0.64,0.66,1)
        _RockSaturation ("Rock Saturation", Range(0,1)) = 0
        _WorldTiling ("Shared World Tiling (Repeats Per Metre)", Float) = 0.075
        [Normal] [NoScaleOffset] _RockNormal ("Rock Normal (Optional)", 2D) = "bump" {}
        _NormalStrength ("Rock Normal Strength", Range(0,2)) = 0
        [NoScaleOffset] _RockAO ("Rock AO (Optional)", 2D) = "white" {}
        _AOStrength ("AO On Indirect Light Only", Range(0,1)) = 0
        [NoScaleOffset] _RockRoughness ("Rock Roughness (Optional, Linear)", 2D) = "white" {}
        _RoughnessMapStrength ("Roughness Map Strength", Range(0,1)) = 0
        _Roughness ("Rock Roughness", Range(0,1)) = 0.85
        _SpecularStrength ("Cheap Dielectric Specular", Range(0,1)) = 0.12
        [Toggle] _UseSharedLighting ("Use Existing LC Lighting Globals", Float) = 1
        [Enum(Legacy,0,Landscape,1,Clay,2)] _ArtMode ("Art Preview Mode", Float) = 0
        _ClayColor ("Clay Color", Color) = (0.45,0.45,0.45,1)
        _ColdRockColor ("High Rock Color", Color) = (0.32,0.36,0.42,1)
        _GrassColor ("Low Shelves Grass", Color) = (0.16,0.30,0.055,1)
        _DryGrassColor ("Dry Grass Variation", Color) = (0.31,0.34,0.09,1)
        _RockTextureStrength ("Art Rock Texture Contrast", Range(0,1)) = 0.18
        _BareRockHeight ("Tree Line / Bare Rock Height", Float) = 58
        [NoScaleOffset] _GroundTex ("Existing Grass Ground (Art Context Only)", 2D) = "white" {}
        _GroundTextureStrength ("Art Ground Texture Contrast", Range(0,1)) = 0
        _GroundTiling ("Art Ground Repeats Per Metre", Float) = 0.2

        [Toggle] _SnowEnabled ("Permanent Alpine Snow", Float) = 1
        _SnowLine ("Snow Line (World Y Metres)", Float) = 120
        _SnowBlend ("Snow Line Transition (Metres)", Float) = 25
        _SnowSlopeMin ("Snow Up Normal Minimum", Range(0,1)) = 0.2
        _SnowSlopeMax ("Snow Up Normal Full Coverage", Range(0,1)) = 0.75
        _SnowColor ("Snow Color", Color) = (0.86,0.9,0.94,1)
        _SnowNoiseAmplitude ("Snow Line Noise (Metres)", Float) = 6
        _SnowNoiseScale ("Snow Line Noise Scale", Float) = 0.025
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 150
        Cull Back
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex);
        TEXTURE2D(_RockNormal); SAMPLER(sampler_RockNormal);
        TEXTURE2D(_RockAO); SAMPLER(sampler_RockAO);
        TEXTURE2D(_RockRoughness); SAMPLER(sampler_RockRoughness);
        TEXTURE2D(_GroundTex); SAMPLER(sampler_GroundTex);

        // Identical layout for both passes. No existing LC material is modified.
        CBUFFER_START(UnityPerMaterial)
            float4 _RockTint;
            float4 _SnowColor;
            float _RockSaturation;
            float _WorldTiling;
            float _NormalStrength;
            float _AOStrength;
            float _RoughnessMapStrength;
            float _Roughness;
            float _SpecularStrength;
            float _UseSharedLighting;
            float _ArtMode;
            float4 _ClayColor;
            float4 _ColdRockColor;
            float4 _GrassColor;
            float4 _DryGrassColor;
            float _RockTextureStrength;
            float _BareRockHeight;
            float _GroundTextureStrength;
            float _GroundTiling;
            float _SnowEnabled;
            float _SnowLine;
            float _SnowBlend;
            float _SnowSlopeMin;
            float _SnowSlopeMax;
            float _SnowNoiseAmplitude;
            float _SnowNoiseScale;
        CBUFFER_END

        // Owned by the existing presentation system, never set by this shader.
        half4 _LC_SunDirection;
        half4 _LC_SunColor;
        half4 _LC_MoonDirection;
        half4 _LC_MoonColor;
        half4 _LC_AmbientColor;
        half4 _LC_AmbientSkyColor;
        half4 _LC_AmbientEquatorColor;
        half4 _LC_AmbientGroundColor;

        struct MountainAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct MountainVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float viewZ : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        MountainVaryings MountainVert(MountainAttributes input)
        {
            MountainVaryings output = (MountainVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.viewZ = position.positionVS.z;
            return output;
        }

        struct MountainProjection
        {
            float2 uvX;
            float2 uvY;
            float2 uvZ;
            half3 weights;
            half3 axisSign;
        };

        MountainProjection MountainProject(float3 positionWS, half3 normalWS)
        {
            MountainProjection projection;
            projection.axisSign = half3(normalWS.x < 0 ? -1 : 1,
                normalWS.y < 0 ? -1 : 1, normalWS.z < 0 ? -1 : 1);
            float tiling = max(abs(_WorldTiling), 0.00001);
            // Right-handed tangent frames: cross(T, B) = signed projection axis.
            // Albedo, normal and masks all use these exact mirrored coordinates.
            projection.uvX = float2(-projection.axisSign.x * positionWS.z, positionWS.y) * tiling;
            projection.uvY = float2(positionWS.x, -projection.axisSign.y * positionWS.z) * tiling;
            projection.uvZ = float2(projection.axisSign.z * positionWS.x, positionWS.y) * tiling;
            projection.weights = pow(abs(normalWS), 4);
            projection.weights /= max(dot(projection.weights, half3(1,1,1)), 0.0001h);
            return projection;
        }

        // Whiteout blend in the projection's tangent basis, followed by the
        // explicit basis-to-world transform. A neutral map returns the original
        // geometric normal in EVERY projection, including negative-facing sides.
        half3 MountainReorient(half3 mapNormal, half3 geometricNormal,
            half3 tangent, half3 bitangent, half3 axis)
        {
            half3 surface = half3(dot(geometricNormal, tangent),
                dot(geometricNormal, bitangent), dot(geometricNormal, axis));
            half3 combined = half3(mapNormal.xy + surface.xy, mapNormal.z * surface.z);
            return SafeNormalize(tangent * combined.x + bitangent * combined.y + axis * combined.z);
        }

        half3 MountainNormal(MountainProjection projection, half3 geometricNormal)
        {
            half3 x = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormal, sampler_RockNormal, projection.uvX), _NormalStrength);
            half3 y = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormal, sampler_RockNormal, projection.uvY), _NormalStrength);
            half3 z = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormal, sampler_RockNormal, projection.uvZ), _NormalStrength);
            half sx = projection.axisSign.x, sy = projection.axisSign.y, sz = projection.axisSign.z;
            x = MountainReorient(x, geometricNormal, half3(0,0,-sx), half3(0,1,0), half3(sx,0,0));
            y = MountainReorient(y, geometricNormal, half3(1,0,0), half3(0,0,-sy), half3(0,sy,0));
            z = MountainReorient(z, geometricNormal, half3(sz,0,0), half3(0,1,0), half3(0,0,sz));
            return SafeNormalize(x * projection.weights.x + y * projection.weights.y + z * projection.weights.z);
        }

        half MountainSnow(float3 positionWS, half3 geometricNormal)
        {
            // Permanent, deterministic world-space appearance: no resident atlas,
            // biome ownership, time, vertex colors, UV masks or local snow clearing.
            float2 p = positionWS.xz * max(abs(_SnowNoiseScale), 0.00001);
            float noise = sin(p.x * 1.17 + sin(p.y * 0.73)) * 0.62
                + sin(p.y * 1.91 - p.x * 0.47 + 2.3) * 0.38;
            float height = positionWS.y + noise * max(_SnowNoiseAmplitude, 0);
            float blend = max(abs(_SnowBlend), 0.01);
            half heightMask = smoothstep(_SnowLine - blend * 0.5, _SnowLine + blend * 0.5, height);
            half slopeMask = smoothstep(_SnowSlopeMin,
                max(_SnowSlopeMin + 0.001, _SnowSlopeMax), geometricNormal.y);
            return saturate(_SnowEnabled) * heightMask * slopeMask;
        }

        half3 MountainDirect(half3 albedo, half3 normalWS, half3 viewDirection,
            half3 lightDirection, half3 lightColor, half roughness)
        {
            half ndotl = saturate(dot(normalWS, lightDirection));
            half3 halfway = SafeNormalize(lightDirection + viewDirection);
            half smoothness = 1 - saturate(roughness);
            // Deliberately reduced dielectric Blinn-Phong, NOT full URP PBR/GGX:
            // roughness broadens the lobe and reduces its peak. No metal workflow.
            half exponent = lerp(8.0h, 128.0h, smoothness * smoothness);
            half specular = pow(saturate(dot(normalWS, halfway)), exponent)
                * smoothness * smoothness * _SpecularStrength * ndotl;
            return lightColor * (albedo * ndotl + specular);
        }

        half4 MountainFrag(MountainVaryings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half3 geometricNormal = normalize(input.normalWS);
            MountainProjection projection = MountainProject(input.positionWS, geometricNormal);
            half3 rock = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, projection.uvX).rgb * projection.weights.x
                + SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, projection.uvY).rgb * projection.weights.y
                + SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, projection.uvZ).rgb * projection.weights.z;
            half luminance = dot(rock, half3(0.2126, 0.7152, 0.0722));
            rock = lerp(luminance.xxx, rock, saturate(_RockSaturation)) * _RockTint.rgb;

            half snow = MountainSnow(input.positionWS, geometricNormal);
            half3 albedo = lerp(rock, _SnowColor.rgb, snow);
            [branch] if (_ArtMode > 0.5)
            {
                float2 p=input.positionWS.xz;
                half patch=sin(p.x*.025+sin(p.y*.021))*0.30+sin(p.y*.067-p.x*.018)*0.18;
                half altitude=smoothstep(35,80,input.positionWS.y);
                half3 artRock=lerp(_RockTint.rgb,_ColdRockColor.rgb,altitude);
                artRock*=lerp(1,clamp(luminance/.45h,.65h,1.35h),_RockTextureStrength);
                artRock*=1+patch*.15;
                half grass=smoothstep(.70h,.94h,geometricNormal.y)*(1-smoothstep(_BareRockHeight-12,_BareRockHeight+8,input.positionWS.y));
                half3 ground=lerp(_GrassColor.rgb,_DryGrassColor.rgb,saturate(patch+.25));
                half3 groundSample=SAMPLE_TEXTURE2D(_GroundTex,sampler_GroundTex,p*max(_GroundTiling,.00001)).rgb;
                half groundValue=dot(groundSample,half3(.2126,.7152,.0722));
                ground*=lerp(1,clamp(groundValue/.35h,.6h,1.4h),_GroundTextureStrength);
                albedo=lerp(artRock,ground,grass);
                albedo=lerp(albedo,_SnowColor.rgb,snow);
                if (_ArtMode > 1.5) { albedo=_ClayColor.rgb; snow=0; }
            }
            half3 normalWS = geometricNormal;
            [branch] if (_NormalStrength > 0.0001)
                normalWS = normalize(lerp(MountainNormal(projection, geometricNormal), geometricNormal, snow));

            half occlusion = 1;
            [branch] if (_AOStrength > 0.0001)
            {
                half ao = SAMPLE_TEXTURE2D(_RockAO, sampler_RockAO, projection.uvX).r * projection.weights.x
                    + SAMPLE_TEXTURE2D(_RockAO, sampler_RockAO, projection.uvY).r * projection.weights.y
                    + SAMPLE_TEXTURE2D(_RockAO, sampler_RockAO, projection.uvZ).r * projection.weights.z;
                occlusion = lerp(1.0h, saturate(ao), saturate(_AOStrength) * (1 - snow));
            }

            half roughness = saturate(_Roughness);
            [branch] if (_RoughnessMapStrength > 0.0001)
            {
                half map = SAMPLE_TEXTURE2D(_RockRoughness, sampler_RockRoughness, projection.uvX).r * projection.weights.x
                    + SAMPLE_TEXTURE2D(_RockRoughness, sampler_RockRoughness, projection.uvY).r * projection.weights.y
                    + SAMPLE_TEXTURE2D(_RockRoughness, sampler_RockRoughness, projection.uvZ).r * projection.weights.z;
                roughness = lerp(roughness, saturate(map), saturate(_RoughnessMapStrength));
            }
            roughness = lerp(roughness, 0.95h, snow);

            half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
            half3 color;
            // Directions remain nonzero during a dark LC night, so zero light
            // color never accidentally activates the fallback. No explicit shared
            // globals ownership flag exists; external stale globals remain a limit.
            float sharedSignal = dot(_LC_SunDirection.xyz, _LC_SunDirection.xyz)
                + dot(_LC_MoonDirection.xyz, _LC_MoonDirection.xyz)
                + dot(abs(_LC_AmbientSkyColor.rgb) + abs(_LC_AmbientEquatorColor.rgb)
                    + abs(_LC_AmbientGroundColor.rgb) + abs(_LC_AmbientColor.rgb), float3(1,1,1));
            [branch] if (_UseSharedLighting > 0.5 && sharedSignal > 0.0001)
            {
                half3 ambient = _LC_AmbientSkyColor.rgb * saturate(normalWS.y)
                    + _LC_AmbientEquatorColor.rgb * saturate(1 - abs(normalWS.y))
                    + _LC_AmbientGroundColor.rgb * saturate(-normalWS.y);
                half hemisphereSignal = dot(abs(_LC_AmbientSkyColor.rgb) + abs(_LC_AmbientEquatorColor.rgb)
                    + abs(_LC_AmbientGroundColor.rgb), half3(1,1,1));
                if (hemisphereSignal < 0.0001h) ambient = _LC_AmbientColor.rgb;
                color = albedo * ambient * occlusion;
                color += MountainDirect(albedo, normalWS, viewDirection, SafeNormalize(_LC_SunDirection.xyz), _LC_SunColor.rgb, roughness);
                color += MountainDirect(albedo, normalWS, viewDirection, SafeNormalize(_LC_MoonDirection.xyz), _LC_MoonColor.rgb, roughness);
            }
            else
            {
                Light mainLight = GetMainLight();
                color = albedo * max(SampleSH(normalWS), half3(0.14,0.16,0.2)) * occlusion;
                color += MountainDirect(albedo, normalWS, viewDirection, mainLight.direction,
                    mainLight.color * mainLight.distanceAttenuation, roughness);
            }
            float nearToFarZ = max(-input.viewZ - _ProjectionParams.y, 0.0);
            half fog = ComputeFogFactorZ0ToFar(nearToFarZ);
            return half4(MixFog(color, fog), 1);
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MountainVert
            #pragma fragment MountainFrag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MountainDepthVert
            #pragma fragment MountainDepthFrag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            struct MountainDepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            MountainDepthVaryings MountainDepthVert(MountainAttributes input)
            {
                MountainDepthVaryings output = (MountainDepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half MountainDepthFrag(MountainDepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
    // Presentation-only prototype. No ShadowCaster, local lights or atlas state.
    Fallback Off
}
