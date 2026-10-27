#ifndef LC_GRASS_URP_INCLUDED
#define LC_GRASS_URP_INCLUDED

#include "LC_VegetationURPCommon.hlsl"
#include "../Surface/LC_GroundPalette.cginc"
#include "../Surface/LC_SurfacePaint.hlsl"

// Identical material layout in every pass (SRP Batcher).
CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half _OpacityMapStrength;
    half4 _Color;
    half4 _RootColor;
    half4 _TipColor;
    half _Cutoff;
    half _UseRootAnchor;
    half _UseObjectAnchorFade;
    float _FadeStart, _FadeEnd;
    float _MeadowMode, _DensityNear, _DensityFar, _DensityFloor;
    float _MeadowSoftness, _MeadowSoftStart, _MeadowSoftEnd;
    float _MeadowRootOcclusion;
    float _InteractionStrength, _InteractionDistance;
    float4 _SurfacePaintBounds;
    half _ColorVariation;
    half _MeadowPaletteStrength;
    half4 _DryColor;
    half _UpNormalBias;
    half4 _TransmissionColor;
    half _TransmissionStrength;
    half _AmbientStrength;
    half _ShadowTintStrength;
    half _WindAmplitude;
    half _WindBend;
    half _GustStrength;
    half _GustScale;
    half _GustSpeed;
    half _MicroFlutter;
    half _MicroFlutterScale;
    half _MicroFlutterSpeed;
CBUFFER_END

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(_OpacityMap); SAMPLER(sampler_OpacityMap);

// Presentation-only: one active interaction manager, at most four nearby sources.
float4 _LC_GrassInteractors[4]; // world XYZ, radius
float4 _LC_GrassInteractorBend; // one strength per source
int _LC_GrassInteractorCount;

struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float4 rootAnchor : TEXCOORD1; // XYZ root, W stable blade density rank (meadow only)
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 worldPosition : TEXCOORD1;
    half3 worldNormal : TEXCOORD2;
    half3 bladeData : TEXCOORD3; // X=root-to-tip, YZ=authored ground normal XZ
    half variation : TEXCOORD4;
    half meadowPatch : TEXCOORD5;
    half2 fogCoord : TEXCOORD6; // X=fog, Y=meadow distance softening
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

float3 ApplyGrassWind(
    float3 objectVertex,
    float bladeHeight,
    float distanceSoftening)
{
    float3 result = objectVertex;
    [branch] if (_LC_Wind.z > .00001)
    {
    float3 objectOrigin =
        TransformObjectToWorld(float3(0, 0, 0));

    float3 worldPosition =
        TransformObjectToWorld(objectVertex);

    float seed =
        HashObject(
            objectOrigin);
#if defined(_LC_MEADOW)
    // Large gusts remain coherent across instances and patch boundaries.
    seed *= .06;
#endif

    float phase =
        dot(
            worldPosition.xz,
            _LC_Wind.xy) *
        0.085 +
        _LC_GameTime.z *
        _LC_Wind.w *
        1.18 +
        seed *
        6.2831853;

    float baseWave =
        sin(phase) * 0.72 +
        sin(
            phase * 2.14 +
            0.8) *
        0.28;

    float gustPhase =
        dot(
            worldPosition.xz,
            _LC_Wind.xy) *
            _GustScale +
        _LC_GameTime.z *
            _LC_Wind.w *
            _GustSpeed +
        seed * 2.41;

    float gust01 =
        0.5 +
        0.5 *
        sin(gustPhase);

    float gustEnvelope =
        lerp(
            1.0,
            lerp(
                0.56,
                1.34,
                gust01),
            _GustStrength);

    float tipWeight =
        pow(
            saturate(bladeHeight),
            max(
                0.25,
                _WindBend));

    float sway =
        baseWave *
        gustEnvelope *
        _LC_Wind.z *
        _WindAmplitude *
        tipWeight;
    // Keep the broad wave; suppress distant sub-pixel lateral movement.
    sway *= 1-distanceSoftening*.35;

    float microPhase =
        dot(
            worldPosition.xz,
            float2(
                1.71,
                2.37)) *
            _MicroFlutterScale +
        _LC_GameTime.z *
            _LC_Wind.w *
            _MicroFlutterSpeed +
        seed * 5.17;

    float micro =
        (sin(microPhase) * 0.72 +
         sin(
            microPhase * 2.31 +
            0.47) * 0.28) *
        _MicroFlutter *
        _LC_Wind.z *
        tipWeight *
        tipWeight;
    micro *= 1-distanceSoftening;

    float2 perpendicular =
        float2(
            -_LC_Wind.y,
            _LC_Wind.x);

    float3 worldOffset =
        float3(
            _LC_Wind.x,
            0.0,
            _LC_Wind.y) *
            sway +
        float3(
            perpendicular.x,
            0.0,
            perpendicular.y) *
            micro *
            _WindAmplitude;

    worldOffset.y -=
        abs(sway) *
        0.08 *
        tipWeight;

    result += TransformWorldToObjectDir(worldOffset, false);
    }
    return result;
}

// This complete deformation is shared by forward, shadow and depth passes.
// UV0.y remains the authored root-to-tip mask; UV1 is optional root-anchor data.
float3 LCDeformVertex(appdata v)
{
    float3 rootWorld = TransformObjectToWorld(lerp(float3(0,0,0),v.rootAnchor.xyz,_UseRootAnchor));
    float cameraDistance = distance(rootWorld, GetCameraPositionWS());
    float distanceSoftening=0;
    float fade = 1 - max(_UseRootAnchor,_UseObjectAnchorFade) * smoothstep(_FadeStart,
        max(_FadeStart + .01, _FadeEnd), cameraDistance);
#if defined(_LC_MEADOW)
    distanceSoftening=_MeadowSoftness*smoothstep(_MeadowSoftStart,max(_MeadowSoftStart+.01,_MeadowSoftEnd),cameraDistance);
    float density = lerp(1, _DensityFloor, smoothstep(_DensityNear, max(_DensityNear+.01,_DensityFar), cameraDistance));
    fade *= smoothstep(v.rootAnchor.w-.045, v.rootAnchor.w+.045, density);
#endif
#if defined(_LC_SURFACE_PAINT)
    float2 coverage=LCSurfaceCoverage(rootWorld,_SurfacePaintBounds);
    fade *= smoothstep(0,.95,coverage.g)*(1-smoothstep(.10,.8,coverage.r));
#endif
    float weight = saturate(v.uv.y)*fade;
    float3 anchor=lerp(v.rootAnchor.xyz,float3(0,0,0),_UseObjectAnchorFade);
    float3 position = ApplyGrassWind(lerp(anchor, v.vertex.xyz, fade), weight, distanceSoftening);
#if defined(_LC_MEADOW)
    [branch] if (_LC_GrassInteractorCount > 0 && _InteractionStrength > 0 && cameraDistance < _InteractionDistance && weight > 0)
    {
        float3 offset = 0;
        [loop] for(int n=0;n<min(_LC_GrassInteractorCount,4);n++)
        {
            float4 source = _LC_GrassInteractors[n];
            float2 delta = rootWorld.xz-source.xz;
            float radius = max(.01,source.w);
            float horizontal = length(delta);
            float influence = 1-smoothstep(0,radius,horizontal);
            influence *= saturate(1-abs(rootWorld.y-source.y)/max(.3,radius));
            influence *= _LC_GrassInteractorBend[n] * weight * weight * _InteractionStrength;
            influence *= 1-smoothstep(_InteractionDistance*.8,_InteractionDistance,cameraDistance);
            offset.xz += delta/max(.08,horizontal) * influence * .42;
            offset.y -= influence*.22;
        }
        // Bound total displacement so four overlapping units cannot tear the grass.
        offset *= min(1,.6/max(.001,length(offset)));
        // Short meadow blades must not receive a full metre-scale unit bend.
        float reach=length(TransformObjectToWorldDir(v.vertex.xyz-v.rootAnchor.xyz,false));
        offset *= min(1,reach/.5);
        position += TransformWorldToObjectDir(offset,false);
    }
    [branch] if (_LC_Wind.z>.00001 || _LC_GrassInteractorCount>0)
    {
        float3 originalWS=TransformObjectToWorldDir(v.vertex.xyz-v.rootAnchor.xyz,false)*fade;
        float3 deformedWS=TransformObjectToWorldDir(position-v.rootAnchor.xyz,false);
        float limitSquared=dot(originalWS,originalWS),deformedSquared=dot(deformedWS,deformedWS);
        // A bend can shorten a blade's reach; it cannot stretch it past the authored length.
        float ratio=min(1,sqrt(limitSquared/max(1e-8,deformedSquared)));
        position=v.rootAnchor.xyz+(position-v.rootAnchor.xyz)*ratio;
    }
#endif
    return position;
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
    half3 groundNormal=TransformObjectToWorldNormal(float3(0,1,0));
    o.bladeData = half3(saturate(v.uv.y),groundNormal.x,groundNormal.z);
    float3 rootWorld = TransformObjectToWorld(v.rootAnchor.xyz);
    float3 objectOrigin = TransformObjectToWorld(float3(0, 0, 0));
    o.variation = HashObject(lerp(objectOrigin, rootWorld, _UseRootAnchor));
    o.meadowPatch = LCMeadowPatch(lerp(objectOrigin,rootWorld,_UseRootAnchor).xz) * _MeadowPaletteStrength;
    o.fogCoord.x = ComputeFogFactor(o.pos.z);
#if defined(_LC_MEADOW)
    o.fogCoord.y = _MeadowSoftness*smoothstep(_MeadowSoftStart,max(_MeadowSoftStart+.01,_MeadowSoftEnd),distance(rootWorld,GetCameraPositionWS()));
#endif
    return o;
}

v2f vertShadow(appdata v)
{
    v2f o = vert(v);
    UNITY_SETUP_INSTANCE_ID(v);
    o.pos = LCVegetationShadowPosition(o.worldPosition, TransformObjectToWorldNormal(v.normal));
    return o;
}

half3 EvaluateNormal(v2f i, half faceSign)
{
#if defined(_LC_MEADOW)
    // Native meadow normals share the upward hemisphere on both sides of a blade.
    // The instance up-axis is the authored terrain slope, not always world up.
    half2 slope=i.bladeData.yz;
    half3 ground=normalize(half3(slope.x,sqrt(saturate(1-dot(slope,slope))),slope.y));
    return normalize(lerp(normalize(i.worldNormal),ground,lerp(_UpNormalBias,.98h,i.fogCoord.y)));
#else
    half3 normal = normalize(i.worldNormal * faceSign);
    return normalize(lerp(normal, half3(0, 1, 0), lerp(_UpNormalBias,.98h,i.fogCoord.y)));
#endif
}


half LCVegetationAlpha(half baseAlpha, half opacity)
{
    return lerp(baseAlpha, opacity, _OpacityMapStrength);
}

void LCVegetationClip(float2 uv)
{
#if !defined(_LC_MEADOW)
    half alpha = LCVegetationAlpha(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a,
        SAMPLE_TEXTURE2D(_OpacityMap, sampler_OpacityMap, uv).r);
    clip(alpha - _Cutoff);
#endif
}

half4 frag(
    v2f i,
    FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    // Meadow geometry supplies the silhouette: avoid two texture reads and alpha overdraw.
#if defined(_LC_MEADOW)
    half4 alphaSample = half4(1,1,1,1);
#else
    half4 alphaSample =
        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

    half opacitySample =
        SAMPLE_TEXTURE2D(_OpacityMap, sampler_OpacityMap, i.uv).r;

    half alpha = LCVegetationAlpha(alphaSample.a, opacitySample);

    clip(
        alpha -
        _Cutoff);
#endif

    half rootToTip =
        smoothstep(
            0.0h,
            1.0h,
            i.bladeData.x);

    half3 gradient =
        lerp(
            _RootColor.rgb,
            _Color.rgb,
            smoothstep(
                0.0h,
                0.55h,
                rootToTip));

    gradient =
        lerp(
            gradient,
            _TipColor.rgb,
            smoothstep(
                0.58h,
                1.0h,
                rootToTip));
    gradient=lerp(gradient,_Color.rgb,i.fogCoord.y*.88h);

    half variation =
        lerp(
            1.0h - _ColorVariation,
            1.0h + _ColorVariation,
            i.variation);
    variation=lerp(variation,1,i.fogCoord.y*.8h);

    half3 albedo =
        gradient *
        alphaSample.rgb *
        variation;
    albedo*=lerp(half3(1,1,1),_DryColor.rgb/max(_Color.rgb,.01h),i.meadowPatch*.5h);

    half faceSign =
        IS_FRONT_VFACE(facing, 1.0h, -1.0h);

    half3 normal = EvaluateNormal(i, faceSign);

    half3 sunDirection =
        normalize(
            _LC_SunDirection.xyz);

    half3 moonDirection =
        normalize(
            _LC_MoonDirection.xyz);

    half sunNdotL =
        saturate(
            dot(
                normal,
                sunDirection) *
            0.72h +
            0.28h);

    half moonNdotL =
        saturate(
            dot(
                normal,
                moonDirection) *
            0.68h +
            0.32h);

    half shadow =
        LCVegetationMainShadow(i.worldPosition);

    half sunDiffuse =
        smoothstep(
            0.12h,
            0.82h,
            sunNdotL) *
        shadow;

    half moonDiffuse =
        smoothstep(
            0.08h,
            0.88h,
            moonNdotL);

    half3 shadowTint =
        lerp(
            half3(1, 1, 1),
            _LC_ShadowTint.rgb,
            _ShadowTintStrength);

    half materialOcclusion = 1.0h;
#if defined(_LC_MEADOW)
    materialOcclusion = 1-_MeadowRootOcclusion*(1-smoothstep(0,.65h,i.bladeData.x));
#endif
    half3 ambient =
        LCSpatialIndirect(i.worldPosition, normal, EvaluateHemisphereAmbient(normal)) *
        LCIndirectOcclusion(i.pos, materialOcclusion) *
        _AmbientStrength *
        lerp(
            shadowTint,
            half3(1, 1, 1),
            sunDiffuse);

    half3 direct =
        _LC_SunColor.rgb *
        sunDiffuse +
        _LC_MoonColor.rgb *
        moonDiffuse;
// Material root AO and screen AO were combined once above (minimum).

    half backlight =
        pow(
            saturate(
                dot(
                    -normal,
                    sunDirection)),
            1.7h);

    half3 transmissionColor =
        lerp(
            _TransmissionColor.rgb,
            half3(
                1.0h,
                0.50h,
                0.16h),
            _LC_Twilight *
            0.72h);

    half3 transmission =
        transmissionColor *
        _LC_SunColor.rgb *
        backlight *
        _TransmissionStrength *
        lerp(
            0.35h,
            1.0h,
            shadow);

    half3 finalColor =
        albedo *
        (ambient + direct) +
        albedo *
        transmission;

    half4 result =
        half4(
            finalColor,
            1.0);

    result.rgb = MixFog(result.rgb, InitializeInputDataFog(float4(i.worldPosition, 1), i.fogCoord.x));

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

#endif // LC_GRASS_URP_INCLUDED
