Shader "Little Castle/Foliage/LC Foliage"
{
    Properties
    {
        _MainTex ("Base Color + Alpha", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.45

        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 0.65

        _OcclusionMap ("Ambient Occlusion", 2D) = "white" {}
        _OcclusionStrength ("AO Strength", Range(0,1)) = 0.65

        _TransmissionColor ("Sun Transmission", Color) = (0.68,0.92,0.30,1)
        _TransmissionStrength ("Transmission Strength", Range(0,2)) = 0.72
        _TransmissionPower ("Transmission Size", Range(0.5,8)) = 2.3

        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.92
        _ShadowTintStrength ("Shadow Tint", Range(0,1)) = 0.42
        _LightWrap ("Light Wrap", Range(0,0.8)) = 0.34
        _ShadowSoftness ("Light Softness", Range(0.03,0.8)) = 0.42

        _ColorVariation ("Per Object Variation", Range(0,0.25)) = 0.055

        _WindAmplitude ("Wind Amplitude", Range(0,1)) = 0.22
        _WindSecondary ("Secondary Motion", Range(0,1)) = 0.28
        _CrownSway ("Crown Sway", Range(0,1)) = 0.42
        _VertexWave ("Vertex Wave", Range(0,1)) = 0.46
        _LeafFlutter ("Leaf Flutter", Range(0,1)) = 0.16
        _FlutterScale ("Flutter Spatial Scale", Range(0.1,8)) = 2.4
        _FlutterSpeed ("Flutter Speed", Range(0.1,8)) = 2.7
        _UseHeightWindMask ("Use Automatic Height Wind Mask", Range(0,1)) = 0
        _WindAnchorHeight ("Wind Anchor Height", Float) = 0
        _WindHeightRange ("Wind Height Range", Float) = 5
        _UseVertexWindMask ("Use Vertex Color R Wind Mask", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        LOD 240
        Cull Off

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

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
            #include "UnityStandardUtils.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Cutoff;

            sampler2D _BumpMap;
            half _BumpScale;

            sampler2D _OcclusionMap;
            half _OcclusionStrength;

            half4 _TransmissionColor;
            half _TransmissionStrength;
            half _TransmissionPower;

            half _AmbientStrength;
            half _ShadowTintStrength;
            half _LightWrap;
            half _ShadowSoftness;
            half _ColorVariation;

            half _WindAmplitude;
            half _WindSecondary;
            half _CrownSway;
            half _VertexWave;
            half _LeafFlutter;
            half _FlutterScale;
            half _FlutterSpeed;
            half _UseHeightWindMask;
            float _WindAnchorHeight;
            float _WindHeightRange;
            half _UseVertexWindMask;

            half4 _LC_SunDirection;
            half4 _LC_SunColor;
            half4 _LC_MoonDirection;
            half4 _LC_MoonColor;
            half4 _LC_AmbientColor;
            half4 _LC_ShadowTint;
            half _LC_Daylight;
            half _LC_Twilight;
            half _LC_NightAmount;
            float4 _LC_Wind;
            float4 _LC_GameTime;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                half3 worldNormal : TEXCOORD2;
                half3 worldTangent : TEXCOORD3;
                half3 worldBitangent : TEXCOORD4;
                half variation : TEXCOORD5;

                SHADOW_COORDS(6)
                UNITY_FOG_COORDS(7)

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float HashObject(float3 objectOrigin)
            {
                return frac(
                    sin(
                        dot(
                            objectOrigin,
                            float3(
                                12.9898,
                                78.233,
                                37.719))) *
                    43758.5453);
            }

            float EvaluateWindMask(
                float3 objectVertex,
                float vertexColorMask)
            {
                float safeRange = max(0.001, abs(_WindHeightRange));
                float heightMask = saturate(
                    (objectVertex.y - _WindAnchorHeight) / safeRange);

                heightMask =
                    heightMask *
                    heightMask *
                    (3.0 - 2.0 * heightMask);

                float mask =
                    lerp(
                        1.0,
                        heightMask,
                        _UseHeightWindMask);

                return lerp(
                    mask,
                    saturate(vertexColorMask),
                    _UseVertexWindMask);
            }

            float3 ApplyWind(
                float3 objectVertex,
                float3 objectNormal,
                float vertexMask)
            {
                float3 objectOrigin =
                    float3(
                        unity_ObjectToWorld._m03,
                        unity_ObjectToWorld._m13,
                        unity_ObjectToWorld._m23);

                float3 worldPosition =
                    mul(
                        unity_ObjectToWorld,
                        float4(objectVertex, 1.0)).xyz;

                float3 worldNormal =
                    normalize(
                        mul(
                            (float3x3)unity_ObjectToWorld,
                            objectNormal));

                float objectSeed = HashObject(objectOrigin);

                float objectPhase =
                    dot(objectOrigin.xz, _LC_Wind.xy) * 0.035 +
                    _LC_GameTime.z * _LC_Wind.w +
                    objectSeed * 6.2831853;

                float vertexPhase =
                    dot(worldPosition.xz, _LC_Wind.xy) * 0.18 +
                    worldPosition.y * 0.11 +
                    _LC_GameTime.z * _LC_Wind.w * 1.27 +
                    objectSeed * 2.13;

                float crown =
                    sin(objectPhase) *
                    _CrownSway;

                float wave =
                    (sin(vertexPhase) +
                     sin(vertexPhase * 1.93 + 1.17) *
                     _WindSecondary) *
                    _VertexWave;

                float gustEnvelope =
                    0.72 +
                    sin(
                        _LC_GameTime.z * _LC_Wind.w * 0.23 +
                        dot(
                            objectOrigin.xz,
                            float2(0.013, 0.017))) *
                    0.28;

                float sway =
                    (crown + wave) *
                    gustEnvelope *
                    _LC_Wind.z *
                    _WindAmplitude *
                    vertexMask;

                float flutterPhase =
                    dot(
                        worldPosition,
                        float3(1.73, 2.31, 1.19)) *
                        _FlutterScale +
                    _LC_GameTime.z *
                        _LC_Wind.w *
                        _FlutterSpeed +
                    objectSeed * 4.71;

                float flutter =
                    (sin(flutterPhase) * 0.70 +
                     sin(flutterPhase * 2.17 + 0.61) * 0.30) *
                    _LeafFlutter *
                    _LC_Wind.z *
                    vertexMask;

                float3 worldOffset =
                    float3(
                        _LC_Wind.x,
                        0.0,
                        _LC_Wind.y) *
                        sway +
                    worldNormal *
                        flutter *
                        _WindAmplitude *
                        0.45;

                worldOffset.y +=
                    abs(sway) *
                    0.025;

                return
                    objectVertex +
                    mul(
                        (float3x3)unity_WorldToObject,
                        worldOffset);
            }

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float mask =
                    EvaluateWindMask(
                        v.vertex.xyz,
                        v.color.r);

                float3 deformedVertex =
                    ApplyWind(
                        v.vertex.xyz,
                        v.normal,
                        mask);

                float4 vertex =
                    float4(
                        deformedVertex,
                        1.0);

                o.position =
                    UnityObjectToClipPos(
                        vertex);

                o.uv =
                    TRANSFORM_TEX(
                        v.uv,
                        _MainTex);

                o.worldPosition =
                    mul(
                        unity_ObjectToWorld,
                        vertex).xyz;

                o.worldNormal =
                    UnityObjectToWorldNormal(
                        v.normal);

                o.worldTangent =
                    UnityObjectToWorldDir(
                        v.tangent.xyz);

                half tangentSign =
                    v.tangent.w *
                    unity_WorldTransformParams.w;

                o.worldBitangent =
                    cross(
                        o.worldNormal,
                        o.worldTangent) *
                    tangentSign;

                float3 objectOrigin =
                    float3(
                        unity_ObjectToWorld._m03,
                        unity_ObjectToWorld._m13,
                        unity_ObjectToWorld._m23);

                o.variation =
                    HashObject(
                        objectOrigin);

                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(
                    o,
                    o.position);

                return o;
            }

            half3 EvaluateNormal(
                v2f i,
                half faceSign)
            {
                half3 tangentNormal =
                    UnpackScaleNormal(
                        tex2D(
                            _BumpMap,
                            i.uv),
                        _BumpScale);

                half3 normal =
                    normalize(
                        tangentNormal.x *
                            normalize(i.worldTangent) +
                        tangentNormal.y *
                            normalize(i.worldBitangent) +
                        tangentNormal.z *
                            normalize(i.worldNormal));

                return
                    normal *
                    faceSign;
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
                    0.40h;

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

            fixed4 frag(
                v2f i,
                fixed facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                fixed4 sample =
                    tex2D(
                        _MainTex,
                        i.uv);

                clip(
                    sample.a *
                    _Color.a -
                    _Cutoff);

                half variation =
                    lerp(
                        1.0h - _ColorVariation,
                        1.0h + _ColorVariation,
                        i.variation);

                half3 albedo =
                    sample.rgb *
                    _Color.rgb *
                    variation;

                half faceSign =
                    facing >= 0
                        ? 1.0h
                        : -1.0h;

                half3 normal =
                    EvaluateNormal(
                        i,
                        faceSign);

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

                half occlusion =
                    lerp(
                        1.0h,
                        tex2D(
                            _OcclusionMap,
                            i.uv).r,
                        _OcclusionStrength);

                half directPresence =
                    saturate(
                        sunDiffuse +
                        moonDiffuse * 0.40h);

                half3 shadowTint =
                    lerp(
                        half3(1, 1, 1),
                        _LC_ShadowTint.rgb,
                        _ShadowTintStrength);

                half3 ambient =
                    _LC_AmbientColor.rgb *
                    _AmbientStrength *
                    lerp(
                        shadowTint,
                        half3(1, 1, 1),
                        directPresence) *
                    occlusion;

                half3 direct =
                    _LC_SunColor.rgb *
                    sunDiffuse +
                    _LC_MoonColor.rgb *
                    moonDiffuse;

                half sunBack =
                    pow(
                        saturate(
                            dot(
                                -normal,
                                sunDirection)),
                        _TransmissionPower);

                half moonBack =
                    pow(
                        saturate(
                            dot(
                                -normal,
                                moonDirection)),
                        _TransmissionPower);

                half3 sunsetTransmission =
                    lerp(
                        _TransmissionColor.rgb,
                        half3(
                            1.0h,
                            0.48h,
                            0.16h),
                        _LC_Twilight *
                        0.68h);

                half transmissionShadow =
                    lerp(
                        0.35h,
                        1.0h,
                        shadow);

                half3 transmission =
                    sunsetTransmission *
                    _LC_SunColor.rgb *
                    sunBack *
                    _TransmissionStrength *
                    transmissionShadow;

                transmission +=
                    half3(
                        0.34h,
                        0.48h,
                        0.72h) *
                    _LC_MoonColor.rgb *
                    moonBack *
                    (_TransmissionStrength *
                     0.18h);

                half3 finalColor =
                    albedo *
                    (ambient + direct) +
                    albedo *
                    transmission;

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
            Cull Off

            CGPROGRAM

            #pragma target 3.0
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Cutoff;

            half _WindAmplitude;
            half _WindSecondary;
            half _CrownSway;
            half _VertexWave;
            half _LeafFlutter;
            half _FlutterScale;
            half _FlutterSpeed;
            half _UseHeightWindMask;
            float _WindAnchorHeight;
            float _WindHeightRange;
            half _UseVertexWindMask;

            float4 _LC_Wind;
            float4 _LC_GameTime;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            float HashObject(float3 objectOrigin)
            {
                return frac(
                    sin(
                        dot(
                            objectOrigin,
                            float3(
                                12.9898,
                                78.233,
                                37.719))) *
                    43758.5453);
            }

            float EvaluateWindMask(
                float3 objectVertex,
                float vertexColorMask)
            {
                float safeRange = max(0.001, abs(_WindHeightRange));
                float heightMask = saturate(
                    (objectVertex.y - _WindAnchorHeight) / safeRange);

                heightMask =
                    heightMask *
                    heightMask *
                    (3.0 - 2.0 * heightMask);

                float mask =
                    lerp(
                        1.0,
                        heightMask,
                        _UseHeightWindMask);

                return lerp(
                    mask,
                    saturate(vertexColorMask),
                    _UseVertexWindMask);
            }

            float3 ApplyWind(
                float3 objectVertex,
                float3 objectNormal,
                float vertexMask)
            {
                float3 objectOrigin =
                    float3(
                        unity_ObjectToWorld._m03,
                        unity_ObjectToWorld._m13,
                        unity_ObjectToWorld._m23);

                float3 worldPosition =
                    mul(
                        unity_ObjectToWorld,
                        float4(objectVertex, 1.0)).xyz;

                float3 worldNormal =
                    normalize(
                        mul(
                            (float3x3)unity_ObjectToWorld,
                            objectNormal));

                float objectSeed = HashObject(objectOrigin);

                float objectPhase =
                    dot(objectOrigin.xz, _LC_Wind.xy) * 0.035 +
                    _LC_GameTime.z * _LC_Wind.w +
                    objectSeed * 6.2831853;

                float vertexPhase =
                    dot(worldPosition.xz, _LC_Wind.xy) * 0.18 +
                    worldPosition.y * 0.11 +
                    _LC_GameTime.z * _LC_Wind.w * 1.27 +
                    objectSeed * 2.13;

                float crown =
                    sin(objectPhase) *
                    _CrownSway;

                float wave =
                    (sin(vertexPhase) +
                     sin(vertexPhase * 1.93 + 1.17) *
                     _WindSecondary) *
                    _VertexWave;

                float gustEnvelope =
                    0.72 +
                    sin(
                        _LC_GameTime.z * _LC_Wind.w * 0.23 +
                        dot(
                            objectOrigin.xz,
                            float2(0.013, 0.017))) *
                    0.28;

                float sway =
                    (crown + wave) *
                    gustEnvelope *
                    _LC_Wind.z *
                    _WindAmplitude *
                    vertexMask;

                float flutterPhase =
                    dot(
                        worldPosition,
                        float3(1.73, 2.31, 1.19)) *
                        _FlutterScale +
                    _LC_GameTime.z *
                        _LC_Wind.w *
                        _FlutterSpeed +
                    objectSeed * 4.71;

                float flutter =
                    (sin(flutterPhase) * 0.70 +
                     sin(flutterPhase * 2.17 + 0.61) * 0.30) *
                    _LeafFlutter *
                    _LC_Wind.z *
                    vertexMask;

                float3 worldOffset =
                    float3(
                        _LC_Wind.x,
                        0.0,
                        _LC_Wind.y) *
                        sway +
                    worldNormal *
                        flutter *
                        _WindAmplitude *
                        0.45;

                worldOffset.y +=
                    abs(sway) *
                    0.025;

                return
                    objectVertex +
                    mul(
                        (float3x3)unity_WorldToObject,
                        worldOffset);
            }

            v2f vertShadow(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);

                float mask =
                    EvaluateWindMask(
                        v.vertex.xyz,
                        v.color.r);

                v.vertex.xyz =
                    ApplyWind(
                        v.vertex.xyz,
                        v.normal,
                        mask);

                o.uv =
                    TRANSFORM_TEX(
                        v.uv,
                        _MainTex);

                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);

                return o;
            }

            float4 fragShadow(
                v2f i) : SV_Target
            {
                fixed alpha =
                    tex2D(
                        _MainTex,
                        i.uv).a *
                    _Color.a;

                clip(
                    alpha -
                    _Cutoff);

                SHADOW_CASTER_FRAGMENT(i)
            }

            ENDCG
        }
    }

    Fallback "Transparent/Cutout/Diffuse"
}
