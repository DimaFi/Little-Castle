#ifndef LC_FOLIAGE_URP_INCLUDED
#define LC_FOLIAGE_URP_INCLUDED

#include "LC_VegetationURPCommon.hlsl"
#include "../Surface/LC_EnvironmentAppearance.hlsl"

// Identical material layout in every pass (SRP Batcher).
CBUFFER_START(UnityPerMaterial)
    half _TextureDetail;
    half _CanopyNormalBlend;
    float4 _CanopyCenter;
    float4 _MainTex_ST;
    half _OpacityMapStrength;
    half4 _Color;
    half _SnowAcceptance;
    half _Cutoff;
    half _BumpScale;
    half _OcclusionStrength;
    half4 _TransmissionColor;
    half _TransmissionStrength;
    half _TransmissionPower;
    half _AmbientStrength;
    half _ShadowTintStrength;
    half _LightResponse;
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
CBUFFER_END

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(_OpacityMap); SAMPLER(sampler_OpacityMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);

struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float4 tangent : TANGENT;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 worldPosition : TEXCOORD1;
    half3 worldNormal : TEXCOORD2;
    half3 worldTangent : TEXCOORD3;
    half3 worldBitangent : TEXCOORD4;
    half variation : TEXCOORD5;
    half fogCoord : TEXCOORD6;
    half3 geometricNormalWS : TEXCOORD7; // unaffected by artistic canopy/normal-map lighting
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

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
        TransformObjectToWorld(float3(0, 0, 0));

    float3 worldPosition =
        TransformObjectToWorld(objectVertex);

    float3 worldNormal =
        normalize(
            TransformObjectToWorldNormal(
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
        TransformWorldToObjectDir(worldOffset, false);
}

// All passes evaluate the same optional height / Vertex Color R wind mask.
float3 LCDeformVertex(appdata v)
{
    return ApplyWind(v.vertex.xyz, v.normal, EvaluateWindMask(v.vertex.xyz, v.color.r));
}

v2f vert(appdata v)
{
    v2f o = (v2f)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.worldPosition = TransformObjectToWorld(LCDeformVertex(v));
    o.pos = TransformWorldToHClip(o.worldPosition);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    o.worldNormal = TransformObjectToWorldNormal(v.normal);
    o.geometricNormalWS = o.worldNormal;
    float3 canopyDirection = normalize(v.vertex.xyz - _CanopyCenter.xyz + float3(0, .0001, 0));
    o.worldNormal = normalize(lerp(o.worldNormal,
        TransformObjectToWorldNormal(canopyDirection), _CanopyNormalBlend));
    o.worldTangent = TransformObjectToWorldDir(v.tangent.xyz);
    o.worldBitangent = cross(o.worldNormal, o.worldTangent) * v.tangent.w * GetOddNegativeScale();
    o.variation = HashObject(TransformObjectToWorld(float3(0, 0, 0)));
    o.fogCoord = ComputeFogFactor(o.pos.z);
    return o;
}

v2f vertShadow(appdata v)
{
    v2f o = vert(v);
    UNITY_SETUP_INSTANCE_ID(v);
    o.pos = LCVegetationShadowPosition(o.worldPosition, TransformObjectToWorldNormal(v.normal));
    return o;
}

half3 EvaluateNormal(
    v2f i,
    half faceSign)
{
    half3 tangentNormal =
        UnpackNormalScale(
            SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv),
            _BumpScale);

    half3 normal =
        normalize(
            tangentNormal.x *
                normalize(i.worldTangent) +
            tangentNormal.y *
                normalize(i.worldBitangent) +
            tangentNormal.z *
                normalize(i.worldNormal));

    return normalize(lerp(normal * faceSign, normalize(i.worldNormal), _CanopyNormalBlend));
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

    return lerp(smoothstep(
        center - halfWidth,
        center + halfWidth,
        wrapped),wrapped,_LightResponse);
}

half LCVegetationAlpha(half baseAlpha, half opacity)
{
    return lerp(baseAlpha, opacity, _OpacityMapStrength) * _Color.a;
}

void LCVegetationClip(float2 uv)
{
    half alpha = LCVegetationAlpha(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a,
        SAMPLE_TEXTURE2D(_OpacityMap, sampler_OpacityMap, uv).r);
    clip(alpha - _Cutoff);
}

half4 frag(
    v2f i,
    FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    half4 baseSample =
        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

    half opacitySample =
        SAMPLE_TEXTURE2D(_OpacityMap, sampler_OpacityMap, i.uv).r;

    half alpha = LCVegetationAlpha(baseSample.a, opacitySample);

    clip(
        alpha -
        _Cutoff);

    half variation =
        lerp(
            1.0h - _ColorVariation,
            1.0h + _ColorVariation,
            i.variation);

    half3 albedo =
        lerp(half3(0.65h,0.65h,0.65h), baseSample.rgb, _TextureDetail) *
        _Color.rgb *
        variation;

    albedo = LCEnvironmentAlbedo(albedo, i.worldPosition,
        normalize(i.geometricNormalWS), _SnowAcceptance);

    half faceSign =
        IS_FRONT_VFACE(facing, 1.0h, -1.0h);

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
        LCVegetationMainShadow(i.worldPosition);

    sunDiffuse *=
        shadow;

    half occlusion =
        lerp(
            1.0h,
            SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, i.uv).r,
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

    half3 ambientNormal =
        normal;

    ambientNormal.y =
        abs(
            ambientNormal.y);

    half3 ambient =
        LCSpatialIndirect(i.worldPosition, normalize(ambientNormal), EvaluateHemisphereAmbient(normalize(ambientNormal))) *
        _AmbientStrength *
        lerp(
            shadowTint,
            half3(1, 1, 1),
            directPresence) *
        LCIndirectOcclusion(i.pos, occlusion);

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

    half4 result =
        half4(
            finalColor,
            1.0);


    result.rgb = MixFog(result.rgb, InitializeInputDataFog(float4(i.worldPosition, 1), i.fogCoord));

    return result;
}

half4 fragDepth(v2f i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    LCVegetationClip(i.uv);
    return 0;
}

half4 fragDepthNormals(v2f i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    LCVegetationClip(i.uv);
    return LCVegetationPackDepthNormal(EvaluateNormal(i, IS_FRONT_VFACE(facing, 1.0h, -1.0h)));
}

#endif // LC_FOLIAGE_URP_INCLUDED
