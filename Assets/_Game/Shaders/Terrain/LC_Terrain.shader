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

        _RockSlopeStart ("Rock Slope Start", Range(0,1)) = 0.28
        _RockSlopeEnd ("Rock Slope End", Range(0,1)) = 0.62

        _HeightBlendStrength ("Lowland Dirt Strength", Range(0,1)) = 0
        _DirtHeightStart ("Lowland Dirt Start", Float) = 0
        _DirtHeightEnd ("Lowland Dirt End", Float) = 2

        _UseVertexMasks ("Use Vertex Masks", Range(0,1)) = 0
        _PathMaskStrength ("Vertex R Path Strength", Range(0,1)) = 1
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

            sampler2D _GrassTex;
            sampler2D _DirtTex;
            sampler2D _RockTex;

            fixed4 _GrassColor;
            fixed4 _DirtColor;
            fixed4 _RockColor;

            half _WorldTiling;
            half _RockSlopeStart;
            half _RockSlopeEnd;

            half _HeightBlendStrength;
            float _DirtHeightStart;
            float _DirtHeightEnd;

            half _UseVertexMasks;
            half _PathMaskStrength;
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

                return smoothstep(
                    center - halfWidth,
                    center + halfWidth,
                    wrapped);
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

                half3 grass =
                    tex2D(
                        _GrassTex,
                        worldUv).rgb *
                    _GrassColor.rgb;

                half3 dirt =
                    tex2D(
                        _DirtTex,
                        worldUv).rgb *
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
                    SHADOW_ATTENUATION(i);

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
