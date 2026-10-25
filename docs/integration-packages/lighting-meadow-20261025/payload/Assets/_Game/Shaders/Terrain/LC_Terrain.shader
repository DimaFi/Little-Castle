Shader "Little Castle/Terrain/LC Terrain"
{
    Properties
    {
        _GrassTex ("Grass / Ground", 2D) = "white" {}
        _GrassColor ("Grass Color", Color) = (0.34,0.56,0.26,1)

        _DirtTex ("Dirt / Path", 2D) = "white" {}
        _DirtColor ("Dirt Color", Color) = (0.48,0.34,0.20,1)

        _RockTex ("Rock / Steep Slope", 2D) = "white" {}
        _RockColor ("Rock Color", Color) = (0.45,0.46,0.40,1)

        _WorldTiling ("World Texture Tiling", Range(0.005,0.5)) = 0.075
        _TextureDetail ("Texture Detail Strength", Range(0,1)) = 1
        _GroundPalette ("Meadow Surface Palette", Range(0,1)) = 0
        _DryGrassColor ("Dry Meadow Color", Color) = (.43,.46,.24,1)
        _GrassDetail ("Meadow Texture Contrast", Range(0,1)) = .22
        _DirtDetail ("Path Texture Contrast", Range(0,1)) = .65
        _SoilTex ("Exposed Soil", 2D) = "white" {}
        _SoilColor ("Exposed Soil Color", Color) = (.39,.34,.20,1)
        _GrassNormal ("Meadow Normal", 2D) = "bump" {}
        _DirtNormal ("Path Normal", 2D) = "bump" {}
        _GrassAO ("Meadow Ambient Occlusion", 2D) = "white" {}
        _DirtAO ("Path Ambient Occlusion", 2D) = "white" {}
        _GrassHeight ("Meadow Edge Height", 2D) = "gray" {}
        _SurfaceDetailStrength ("Surface Normal Strength", Range(0,1)) = 0
        _GrassNormalScale ("Meadow Normal Multiplier", Range(0,1)) = .45
        _SurfaceAOStrength ("Surface AO Multiplier", Range(0,1)) = .35
        _DetailFadeStart ("Surface Detail Fade Start", Float) = 28
        _DetailFadeEnd ("Surface Detail Fade End", Float) = 65
        _LightResponse ("Continuous Diffuse", Range(0,1)) = 0
        _ContactShadeStrength ("Vertex B Contact Shade", Range(0,0.5)) = 0

        _RockSlopeStart ("Rock Slope Start", Range(0,1)) = 0.28
        _RockSlopeEnd ("Rock Slope End", Range(0,1)) = 0.62

        _HeightBlendStrength ("Lowland Dirt Strength", Range(0,1)) = 0
        _DirtHeightStart ("Lowland Dirt Start", Float) = 0
        _DirtHeightEnd ("Lowland Dirt End", Float) = 2

        _UseVertexMasks ("Use Vertex Masks", Range(0,1)) = 0
        [Toggle(_LC_SURFACE_PAINT)] _UseSurfacePaint ("Authored / Player Road Mask", Float) = 0
        _SurfacePaint ("Derived Road Coverage", 2D) = "black" {}
        _SurfacePaintBounds ("Road Mask Origin XZ / Inverse Size", Vector) = (0,0,.01,.01)
        _PathMaskStrength ("Vertex R Path Strength", Range(0,1)) = 1
        _PathEdgeSharpness ("Path Edge Sharpness", Range(0,1)) = 0
        _WetnessStrength ("Vertex G Wetness Strength", Range(0,1)) = 0.35

        [Toggle] _UseSnow ("Use Authored Snow Coverage", Float) = 0
        _SnowControl ("Snow Coverage (R, Linear)", 2D) = "black" {}
        _SnowMapBounds ("Snow Map Origin XZ / Inverse Size XZ", Vector) = (0,0,0.01,0.01)
        _SnowTex ("Snow Base Color", 2D) = "white" {}
        _SnowColor ("Snow Tint", Color) = (0.88,0.94,1,1)
        _SnowTiling ("Snow World Tiling", Float) = 0.14
        _SnowEdgeNoise ("Snow Edge Breakup", Range(0,0.4)) = 0.18
        _SnowPathClearance ("Snow Removal On Paths", Range(0,1)) = 0.75

        _MacroScale ("Macro Variation Scale", Range(0.001,0.08)) = 0.012
        _MacroStrength ("Macro Variation Strength", Range(0,0.35)) = 0.10

        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.90
        _ShadowTintStrength ("Shadow Tint", Range(0,1)) = 0.45
        _LightWrap ("Light Wrap", Range(0,0.8)) = 0.22
        _ShadowSoftness ("Light Softness", Range(0.03,0.8)) = 0.36
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 220

        // Shared by every pass so UnityPerMaterial has one SRP Batcher layout.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "../Surface/LC_LightingCommon.hlsl"
        #include "../Surface/LC_GroundPalette.cginc"
        #include "../Surface/LC_SurfacePaint.hlsl"

        TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
        TEXTURE2D(_DirtTex); SAMPLER(sampler_DirtTex);
        TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex);
        TEXTURE2D(_SoilTex); SAMPLER(sampler_SoilTex);
        TEXTURE2D(_GrassNormal); SAMPLER(sampler_GrassNormal);
        TEXTURE2D(_DirtNormal); SAMPLER(sampler_DirtNormal);
        TEXTURE2D(_GrassAO); SAMPLER(sampler_GrassAO);
        TEXTURE2D(_DirtAO); SAMPLER(sampler_DirtAO);
        TEXTURE2D(_GrassHeight); SAMPLER(sampler_GrassHeight);
        TEXTURE2D(_SnowControl); SAMPLER(sampler_SnowControl);
        TEXTURE2D(_SnowTex); SAMPLER(sampler_SnowTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _GrassColor;
            float4 _DirtColor;
            float4 _RockColor;
            float _WorldTiling;
            float _TextureDetail;
            float _GroundPalette;
            float4 _DryGrassColor;
            float _GrassDetail;
            float _DirtDetail;
            float4 _SoilColor;
            float _SurfaceDetailStrength;
            float _GrassNormalScale;
            float _SurfaceAOStrength;
            float _DetailFadeStart;
            float _DetailFadeEnd;
            float _LightResponse;
            float _ContactShadeStrength;
            float _RockSlopeStart;
            float _RockSlopeEnd;
            float _HeightBlendStrength;
            float _DirtHeightStart;
            float _DirtHeightEnd;
            float _UseVertexMasks;
            float4 _SurfacePaintBounds;
            float _PathMaskStrength;
            float _PathEdgeSharpness;
            float _WetnessStrength;
            float _UseSnow;
            float4 _SnowMapBounds;
            float4 _SnowColor;
            float _SnowTiling;
            float _SnowEdgeNoise;
            float _SnowPathClearance;
            float _MacroScale;
            float _MacroStrength;
            float _AmbientStrength;
            float _ShadowTintStrength;
            float _LightWrap;
            float _ShadowSoftness;
        CBUFFER_END
        float _LC_LowTerrainDetail;

        // Presentation globals stay outside the per-material constant buffer.
        half4 _LC_SunDirection;
        half4 _LC_SunColor;
        half4 _LC_MoonDirection;
        half4 _LC_MoonColor;
        half4 _LC_AmbientColor;
        half4 _LC_AmbientSkyColor;
        half4 _LC_AmbientEquatorColor;
        half4 _LC_AmbientGroundColor;
        half4 _LC_ShadowTint;


        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f
        {
            float4 pos : SV_POSITION;
            float3 worldPosition : TEXCOORD0;
            half3 worldNormal : TEXCOORD1;
            half4 vertexColor : TEXCOORD2;
            float fogCoord : TEXCOORD3;
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
            o.pos = positions.positionCS;
            o.worldPosition = positions.positionWS;
            o.worldNormal = TransformObjectToWorldNormal(v.normal);
            o.vertexColor = v.color;
            o.fogCoord = ComputeFogFactor(o.pos.z);
            return o;
        }

        half MacroVariation(
            float3 worldPosition)
        {
            float x =
                worldPosition.x *
                _MacroScale;

            float z =
                worldPosition.z *
                _MacroScale;

            half value =
                sin(x * 1.37 + z * 0.61) *
                    0.48h +
                sin(z * 2.11 - x * 0.43 + 1.8) *
                    0.32h +
                sin((x + z) * 0.77 + 4.1) *
                    0.20h;

            return
                value;
        }

        half EvaluateWrappedDiffuse(
            half3 normal,
            half3 lightDirection)
        {
            half ndotl =
                dot(
                    normal,
                    lightDirection);

            half wrapped =
                saturate(
                    (ndotl + _LightWrap) /
                    (1.0h + _LightWrap));

            half center =
                0.42h;

            half halfWidth =
                max(
                    0.015h,
                    _ShadowSoftness *
                    0.5h);

            return lerp(smoothstep(
                center - halfWidth,
                center + halfWidth,
                wrapped),wrapped,_LightResponse);
        }

        half3 EvaluateHemisphereAmbient(
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

            half3 normal =
                normalize(
                    i.worldNormal);

            float2 worldUv =
                i.worldPosition.xz *
                _WorldTiling;

            half3 grassSample=SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, worldUv).rgb;
            half3 dirtSample=SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, worldUv).rgb;
            half3 grass = grassSample * _GrassColor.rgb;

            half3 dirt =
                dirtSample *
                _DirtColor.rgb;

            half3 rock =
                SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, worldUv).rgb *
                _RockColor.rgb;

            half slope =
                1.0h -
                saturate(
                    normal.y);

            grass = lerp(_GrassColor.rgb, grass, _TextureDetail);
            dirt = lerp(_DirtColor.rgb, dirt, _TextureDetail);
            rock = lerp(_RockColor.rgb, rock, _TextureDetail);

            // Keep the authored palette stable instead of multiplying it dark.
            // Normal/AO maps share these UVs; no extra meshes or decals.
            half nearDetail=1-smoothstep(_DetailFadeStart,max(_DetailFadeStart+.01,_DetailFadeEnd),distance(i.worldPosition,_WorldSpaceCameraPos));
            half surfaceDetail=_SurfaceDetailStrength*(1-_LC_LowTerrainDetail)*nearDetail;
            half snowWeight = 0;
            half3 snowAlbedo = _SnowColor.rgb;
            [branch] if (_UseSnow > .5h)
            {
                float2 controlUV = (i.worldPosition.xz - _SnowMapBounds.xy) * _SnowMapBounds.zw;
                half coverage = SAMPLE_TEXTURE2D(_SnowControl, sampler_SnowControl, controlUV).r;
                half edge = (LCGroundNoise(i.worldPosition.xz * .52) - .5h) * .7h +
                    (LCGroundNoise(i.worldPosition.xz * 1.83 + 9.7) - .5h) * .3h;
                // Endpoints remain clean. Existing RGBA vertex-mask contracts are untouched.
                coverage += edge * _SnowEdgeNoise * 4 * coverage * (1 - coverage);
                snowWeight = smoothstep(.23h, .77h, coverage) * smoothstep(.35h, .82h, normal.y);
                half3 snowSample = SAMPLE_TEXTURE2D(_SnowTex, sampler_SnowTex, i.worldPosition.xz * _SnowTiling).rgb;
                half snowValue = clamp(dot(snowSample, half3(.2126,.7152,.0722)) / .76h, .82h, 1.12h);
                snowAlbedo *= lerp(1, snowValue, .42h);
                surfaceDetail *= 1 - snowWeight * .85h;
            }
            half meadowPatch=LCMeadowPatch(i.worldPosition.xz);
            [branch] if(_GroundPalette>.001h) {
                half meadowValue=clamp(dot(grassSample,half3(.2126,.7152,.0722))/.38h,.65h,1.35h);
                grass=lerp(_GrassColor.rgb,_DryGrassColor.rgb,meadowPatch*.5h)*
                    lerp(1,meadowValue,_GrassDetail);
                dirt=_DirtColor.rgb*lerp(half3(1,1,1),clamp(dirtSample/half3(.53,.37,.22),.55,1.5),_DirtDetail);
                half soilWeight=smoothstep(.24,.8,i.vertexColor.b)*.65h;
                half soilValue=clamp(dot(SAMPLE_TEXTURE2D(_SoilTex, sampler_SoilTex, worldUv).rgb,half3(.2126,.7152,.0722))/.38h,.7h,1.3h);
                grass=lerp(grass,_SoilColor.rgb*soilValue,soilWeight);
            }

            half rockWeight =
                smoothstep(
                    _RockSlopeStart,
                    max(
                        _RockSlopeStart +
                        0.001h,
                        _RockSlopeEnd),
                    slope);

            half lowland =
                1.0h -
                smoothstep(
                    _DirtHeightStart,
                    max(
                        _DirtHeightStart +
                        0.001,
                        _DirtHeightEnd),
                    i.worldPosition.y);

            lowland *=
                _HeightBlendStrength;

            half pathMask =
                lerp(
                    0.0h,
                    saturate(
                        i.vertexColor.r *
                        _PathMaskStrength),
                    _UseVertexMasks);

#if defined(_LC_SURFACE_PAINT)
            float2 paintUV=(i.worldPosition.xz-_SurfacePaintBounds.xy)*_SurfacePaintBounds.zw;
            if(all(paintUV>=0)&&all(paintUV<=1))pathMask=LCSurfaceRoad(i.worldPosition,_SurfacePaintBounds);
#endif
            pathMask = lerp(pathMask, smoothstep(.18h,.82h,pathMask), _PathEdgeSharpness);
            [branch] if(_GroundPalette>.001h) {
                half edgeDetail=LCGroundNoise(i.worldPosition.xz*4.7)-.5h;
                [branch] if(surfaceDetail>.001h)
                    edgeDetail+=(SAMPLE_TEXTURE2D(_GrassHeight, sampler_GrassHeight, worldUv).r-.5h)*surfaceDetail;
                pathMask=saturate(pathMask+edgeDetail*pathMask*(1-pathMask)*1.15h);
            }

            half wetness =
                lerp(
                    0.0h,
                    saturate(
                        i.vertexColor.g *
                        _WetnessStrength),
                    _UseVertexMasks);

            half dirtWeight =
                saturate(
                    max(
                        lowland,
                        pathMask) *
                    (1.0h -
                     rockWeight));

            half grassWeight =
                saturate(
                    1.0h -
                    rockWeight -
                    dirtWeight);

            half totalWeight =
                max(
                    0.001h,
                    grassWeight +
                    dirtWeight +
                    rockWeight);

            grassWeight /=
                totalWeight;

            dirtWeight /=
                totalWeight;

            rockWeight /=
                totalWeight;

            half microAO=1;
            [branch] if(surfaceDetail>.001h) {
                half3 grassN=UnpackNormal(SAMPLE_TEXTURE2D(_GrassNormal, sampler_GrassNormal, worldUv));
                half3 dirtN=UnpackNormal(SAMPLE_TEXTURE2D(_DirtNormal, sampler_DirtNormal, worldUv));
                half3 detailN=normalize(lerp(grassN,dirtN,dirtWeight));
                detailN.xy*=surfaceDetail*lerp(_GrassNormalScale,1,dirtWeight);
                detailN.z=sqrt(saturate(1-dot(detailN.xy,detailN.xy)));
                // Project world X onto the surface; use Z on near vertical X faces.
                half3 tangentAxis = abs(normal.x) > .99h ? half3(0,0,1) : half3(1,0,0);
                half3 tangent=normalize(tangentAxis-normal*dot(tangentAxis,normal));
                half3 bitangent=cross(tangent,normal);
                normal=normalize(tangent*detailN.x+bitangent*detailN.y+normal*detailN.z);
                half ao=lerp(SAMPLE_TEXTURE2D(_GrassAO, sampler_GrassAO, worldUv).r,SAMPLE_TEXTURE2D(_DirtAO, sampler_DirtAO, worldUv).r,dirtWeight);
                microAO=lerp(1,ao,surfaceDetail*_SurfaceAOStrength);
            }

            half3 albedo =
                grass *
                    grassWeight +
                dirt *
                    dirtWeight +
                rock *
                    rockWeight;

            half macro =
                MacroVariation(
                    i.worldPosition);

            albedo *= 1.0h - saturate(i.vertexColor.b) * _ContactShadeStrength;

            albedo *=
                1.0h +
                macro *
                _MacroStrength;

            albedo *=
                lerp(
                    1.0h,
                    0.72h,
                    wetness);

            // A used path remains visible through winter; damp margins expose the soil.
            snowWeight *= 1 - pathMask * _SnowPathClearance;
            snowWeight *= 1 - wetness * .65h;
            albedo = lerp(albedo, snowAlbedo, snowWeight);

            half3 sunDirection =
                normalize(
                    _LC_SunDirection.xyz);

            half3 moonDirection =
                normalize(
                    _LC_MoonDirection.xyz);

            half sunDiffuse =
                EvaluateWrappedDiffuse(
                    normal,
                    sunDirection);

            half moonDiffuse =
                EvaluateWrappedDiffuse(
                    normal,
                    moonDirection);

            // The shared palette owns sun/moon color. URP owns the selected
            // directional light's shadow map, cascades, filtering and distance fade.
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(i.worldPosition));
            #else
                float4 shadowCoord = TransformWorldToShadowCoord(i.worldPosition);
            #endif
            Light mainLight = GetMainLight(shadowCoord, i.worldPosition, half4(1,1,1,1));
            half sunIsMain = step(.99h, dot(sunDirection, mainLight.direction));
            half moonIsMain = step(.99h, dot(moonDirection, mainLight.direction));
            sunDiffuse *= lerp(1.0h, mainLight.shadowAttenuation, sunIsMain);
            moonDiffuse *= lerp(1.0h, mainLight.shadowAttenuation, moonIsMain);

            half directPresence =
                saturate(
                    sunDiffuse +
                    moonDiffuse *
                    0.35h);

            half3 shadowTint =
                lerp(
                    half3(1, 1, 1),
                    _LC_ShadowTint.rgb,
                    _ShadowTintStrength);

            half3 ambient =
                EvaluateHemisphereAmbient(
                    normal) *
                LCIndirectOcclusion(i.pos, microAO) *
                _AmbientStrength *
                lerp(
                    shadowTint,
                    half3(1, 1, 1),
                    directPresence);

            half3 direct =
                _LC_SunColor.rgb *
                sunDiffuse +
                _LC_MoonColor.rgb *
                moonDiffuse;

            half3 finalColor =
                albedo *
                (ambient + direct);

            half4 result =
                half4(
                    finalColor,
                    1.0);

            result.rgb = MixFog(result.rgb, InitializeInputDataFog(float4(i.worldPosition, 1), i.fogCoord));

            return result;
        }


        float3 _LightDirection;
        float3 _LightPosition;

        struct TerrainDepthVaryings
        {
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        TerrainDepthVaryings vertShadow(appdata v)
        {
            TerrainDepthVaryings o = (TerrainDepthVaryings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
            float3 normalWS = TransformObjectToWorldNormal(v.normal);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
            o.positionCS = ApplyShadowClamping(TransformWorldToHClip(
                ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
            return o;
        }

        TerrainDepthVaryings vertDepth(appdata v)
        {
            TerrainDepthVaryings o = (TerrainDepthVaryings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionCS = TransformObjectToHClip(v.vertex.xyz);
            return o;
        }

        half4 fragDepth(TerrainDepthVaryings i) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(i);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            return 0;
        }
        ENDHLSL

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ _LC_SURFACE_PAINT
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertShadow
            #pragma fragment fragDepth
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    Fallback Off
}
