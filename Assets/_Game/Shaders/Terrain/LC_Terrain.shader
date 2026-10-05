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
        _LightResponse ("Continuous Diffuse", Range(0,1)) = 0
        _ContactShadeStrength ("Vertex B Contact Shade", Range(0,0.5)) = 0

        _RockSlopeStart ("Rock Slope Start", Range(0,1)) = 0.28
        _RockSlopeEnd ("Rock Slope End", Range(0,1)) = 0.62

        _HeightBlendStrength ("Lowland Dirt Strength", Range(0,1)) = 0
        _DirtHeightStart ("Lowland Dirt Start", Float) = 0
        _DirtHeightEnd ("Lowland Dirt End", Float) = 2

        _UseVertexMasks ("Use Vertex Masks", Range(0,1)) = 0
        _PathMaskStrength ("Vertex R Path Strength", Range(0,1)) = 1
        _PathEdgeSharpness ("Path Edge Sharpness", Range(0,1)) = 0
        _WetnessStrength ("Vertex G Wetness Strength", Range(0,1)) = 0.35

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
        }

        LOD 220

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "../Surface/LC_SoftShadow.cginc"
            #include "../Surface/LC_GroundPalette.cginc"

            sampler2D _GrassTex;
            sampler2D _DirtTex;
            sampler2D _RockTex;
            sampler2D _SoilTex, _GrassNormal, _DirtNormal, _GrassAO, _DirtAO, _GrassHeight;
            half4 _DryGrassColor, _SoilColor;
            half _GroundPalette, _GrassDetail, _DirtDetail, _SurfaceDetailStrength, _LightResponse;
            float _LC_LowTerrainDetail;

            fixed4 _GrassColor;
            fixed4 _DirtColor;
            fixed4 _RockColor;

            half _WorldTiling;
            half _TextureDetail;
            half _ContactShadeStrength;
            half _RockSlopeStart;
            half _RockSlopeEnd;

            half _HeightBlendStrength;
            float _DirtHeightStart;
            float _DirtHeightEnd;

            half _UseVertexMasks;
            half _PathMaskStrength;
            half _PathEdgeSharpness;
            half _WetnessStrength;

            half _MacroScale;
            half _MacroStrength;

            half _AmbientStrength;
            half _ShadowTintStrength;
            half _LightWrap;
            half _ShadowSoftness;

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
                fixed4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                half3 worldNormal : TEXCOORD1;
                fixed4 vertexColor : TEXCOORD2;

                SHADOW_COORDS(3)
                UNITY_FOG_COORDS(4)

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos =
                    UnityObjectToClipPos(
                        v.vertex);

                o.worldPosition =
                    mul(
                        unity_ObjectToWorld,
                        v.vertex).xyz;

                o.worldNormal =
                    UnityObjectToWorldNormal(
                        v.normal);

                o.vertexColor =
                    v.color;

                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(
                    o,
                    o.pos);

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

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half3 normal =
                    normalize(
                        i.worldNormal);

                float2 worldUv =
                    i.worldPosition.xz *
                    _WorldTiling;

                half3 grassSample=tex2D(_GrassTex,worldUv).rgb;
                half3 dirtSample=tex2D(_DirtTex,worldUv).rgb;
                half3 grass = grassSample * _GrassColor.rgb;

                half3 dirt =
                    dirtSample *
                    _DirtColor.rgb;

                half3 rock =
                    tex2D(
                        _RockTex,
                        worldUv).rgb *
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
                half nearDetail=1-smoothstep(28,65,distance(i.worldPosition,_WorldSpaceCameraPos));
                half surfaceDetail=_SurfaceDetailStrength*(1-_LC_LowTerrainDetail)*nearDetail;
                half meadowPatch=LCMeadowPatch(i.worldPosition.xz);
                [branch] if(_GroundPalette>.001h) {
                    half meadowValue=clamp(dot(grassSample,half3(.2126,.7152,.0722))/.38h,.65h,1.35h);
                    grass=lerp(_GrassColor.rgb,_DryGrassColor.rgb,meadowPatch*.5h)*
                        lerp(1,meadowValue,_GrassDetail);
                    dirt=_DirtColor.rgb*lerp(half3(1,1,1),clamp(dirtSample/half3(.53,.37,.22),.55,1.5),_DirtDetail);
                    half soilWeight=smoothstep(.24,.8,i.vertexColor.b)*.65h;
                    half soilValue=clamp(dot(tex2D(_SoilTex,worldUv).rgb,half3(.2126,.7152,.0722))/.38h,.7h,1.3h);
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

                pathMask = lerp(pathMask, smoothstep(.18h,.82h,pathMask), _PathEdgeSharpness);
                [branch] if(_GroundPalette>.001h) {
                    half edgeDetail=LCGroundNoise(i.worldPosition.xz*4.7)-.5h;
                    [branch] if(surfaceDetail>.001h)
                        edgeDetail+=(tex2D(_GrassHeight,worldUv).r-.5h)*surfaceDetail;
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
                    half3 grassN=UnpackNormal(tex2D(_GrassNormal,worldUv));
                    half3 dirtN=UnpackNormal(tex2D(_DirtNormal,worldUv));
                    half3 detailN=normalize(lerp(grassN,dirtN,dirtWeight));
                    detailN.xy*=surfaceDetail*lerp(.45h,1,dirtWeight);
                    detailN.z=sqrt(saturate(1-dot(detailN.xy,detailN.xy)));
                    half3 tangent=normalize(half3(1,0,0)-normal*normal.x);
                    half3 bitangent=cross(tangent,normal);
                    normal=normalize(tangent*detailN.x+bitangent*detailN.y+normal*detailN.z);
                    half ao=lerp(tex2D(_GrassAO,worldUv).r,tex2D(_DirtAO,worldUv).r,dirtWeight);
                    microAO=lerp(1,ao,surfaceDetail*.35h);
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

                half shadow =
                    LC_SHADOW_ATTENUATION(i);

                sunDiffuse *=
                    shadow;

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
                    microAO *
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

                fixed4 result =
                    fixed4(
                        finalColor,
                        1.0);

                UNITY_APPLY_FOG(
                    i.fogCoord,
                    result);

                return result;
            }

            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Back

            CGPROGRAM

            #pragma target 3.0
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;
            };

            v2f vertShadow(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);

                return o;
            }

            float4 fragShadow(
                v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }

            ENDCG
        }
    }

    Fallback "Diffuse"
}
