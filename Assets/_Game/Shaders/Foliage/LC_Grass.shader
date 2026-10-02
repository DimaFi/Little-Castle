Shader "Little Castle/Foliage/LC Grass"
{
    Properties
    {
        _MainTex ("Grass Alpha", 2D) = "white" {}
        _Color ("Middle Color", Color) = (0.38,0.64,0.24,1)
        _RootColor ("Root Color", Color) = (0.18,0.32,0.12,1)
        _TipColor ("Tip Color", Color) = (0.62,0.82,0.34,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.42

        _ColorVariation ("Per Clump Variation", Range(0,0.3)) = 0.08
        _UpNormalBias ("Upward Normal Bias", Range(0,1)) = 0.72

        _TransmissionColor ("Backlight Color", Color) = (0.70,0.92,0.30,1)
        _TransmissionStrength ("Backlight Strength", Range(0,2)) = 0.85

        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.94
        _ShadowTintStrength ("Shadow Tint", Range(0,1)) = 0.38

        _WindAmplitude ("Wind Amplitude", Range(0,1)) = 0.34
        _WindBend ("Tip Bend", Range(0,2)) = 1.0
        _GustStrength ("Gust Strength", Range(0,1)) = 0.38
        _GustScale ("Gust Spatial Scale", Range(0.001,0.08)) = 0.018
        _GustSpeed ("Gust Speed", Range(0.05,2)) = 0.28
        _MicroFlutter ("Tip Micro Flutter", Range(0,0.5)) = 0.10
        _MicroFlutterScale ("Micro Flutter Scale", Range(0.1,8)) = 2.2
        _MicroFlutterSpeed ("Micro Flutter Speed", Range(0.1,8)) = 3.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        LOD 180
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

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed4 _Color;
            fixed4 _RootColor;
            fixed4 _TipColor;
            half _Cutoff;
            half _ColorVariation;
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

            half4 _LC_SunDirection;
            half4 _LC_SunColor;
            half4 _LC_MoonDirection;
            half4 _LC_MoonColor;
            half4 _LC_AmbientColor;
            half4 _LC_ShadowTint;
            half _LC_Twilight;
            float4 _LC_Wind;
            float4 _LC_GameTime;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 worldNormal : TEXCOORD1;
                half bladeHeight : TEXCOORD2;
                half variation : TEXCOORD3;

                SHADOW_COORDS(4)
                UNITY_FOG_COORDS(5)

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

            float3 ApplyGrassWind(
                float3 objectVertex,
                float bladeHeight)
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

                float seed =
                    HashObject(
                        objectOrigin);

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

                half bladeHeight =
                    saturate(
                        v.uv.y);

                float3 deformed =
                    ApplyGrassWind(
                        v.vertex.xyz,
                        bladeHeight);

                float4 vertex =
                    float4(
                        deformed,
                        1.0);

                o.position =
                    UnityObjectToClipPos(
                        vertex);

                o.uv =
                    TRANSFORM_TEX(
                        v.uv,
                        _MainTex);

                o.worldNormal =
                    UnityObjectToWorldNormal(
                        v.normal);

                o.bladeHeight =
                    bladeHeight;

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

            fixed4 frag(
                v2f i,
                fixed facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                fixed4 alphaSample =
                    tex2D(
                        _MainTex,
                        i.uv);

                clip(
                    alphaSample.a -
                    _Cutoff);

                half rootToTip =
                    smoothstep(
                        0.0h,
                        1.0h,
                        i.bladeHeight);

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

                half variation =
                    lerp(
                        1.0h - _ColorVariation,
                        1.0h + _ColorVariation,
                        i.variation);

                half3 albedo =
                    gradient *
                    alphaSample.rgb *
                    variation;

                half faceSign =
                    facing >= 0
                        ? 1.0h
                        : -1.0h;

                half3 normal =
                    normalize(
                        i.worldNormal *
                        faceSign);

                normal =
                    normalize(
                        lerp(
                            normal,
                            half3(
                                0.0h,
                                1.0h,
                                0.0h),
                            _UpNormalBias));

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
                    SHADOW_ATTENUATION(i);

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

                half3 ambient =
                    _LC_AmbientColor.rgb *
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
            half _Cutoff;

            half _WindAmplitude;
            half _WindBend;
            half _GustStrength;
            half _GustScale;
            half _GustSpeed;
            half _MicroFlutter;
            half _MicroFlutterScale;
            half _MicroFlutterSpeed;

            float4 _LC_Wind;
            float4 _LC_GameTime;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;

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

            float3 ApplyGrassWind(
                float3 objectVertex,
                float bladeHeight)
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

                float seed =
                    HashObject(
                        objectOrigin);

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

                half bladeHeight =
                    saturate(
                        v.uv.y);

                v.vertex.xyz =
                    ApplyGrassWind(
                        v.vertex.xyz,
                        bladeHeight);

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
                clip(
                    tex2D(
                        _MainTex,
                        i.uv).a -
                    _Cutoff);

                SHADOW_CASTER_FRAGMENT(i)
            }

            ENDCG
        }
    }

    Fallback "Transparent/Cutout/Diffuse"
}
