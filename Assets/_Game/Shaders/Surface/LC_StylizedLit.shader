Shader "Little Castle/Surface/LC Stylized Lit"
{
    Properties
    {
        _MainTex ("Base Color", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1

        _OcclusionMap ("Ambient Occlusion", 2D) = "white" {}
        _OcclusionStrength ("AO Strength", Range(0,1)) = 0.75

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
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        LOD 250

        Pass
        {
            Name "FORWARD"
            Tags
            {
                "LightMode" = "ForwardBase"
            }

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
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "UnityStandardUtils.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            sampler2D _BumpMap;
            half _BumpScale;

            sampler2D _OcclusionMap;
            half _OcclusionStrength;

            sampler2D _EmissionMap;
            half4 _EmissionColor;
            half _EmissionDayStrength;
            half _EmissionNightStrength;
            half _EmissionFlickerStrength;
            half _EmissionFlickerSpeed;

            half _LightWrap;
            half _ShadowSoftness;
            half _AmbientStrength;
            half _ShadowTintStrength;
            half _SpecularStrength;
            half _SpecularPower;
            half _RimStrength;
            half _RimPower;

            half4 _LC_SunDirection;
            half4 _LC_SunColor;
            half4 _LC_MoonDirection;
            half4 _LC_MoonColor;
            half4 _LC_AmbientColor;
            half4 _LC_ShadowTint;
            half _LC_Daylight;
            half _LC_Twilight;
            half _LC_NightAmount;
            float4 _LC_GameTime;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;

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

                SHADOW_COORDS(5)
                UNITY_FOG_COORDS(6)

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.position =
                    UnityObjectToClipPos(
                        v.vertex);

                o.uv =
                    TRANSFORM_TEX(
                        v.uv,
                        _MainTex);

                o.worldPosition =
                    mul(
                        unity_ObjectToWorld,
                        v.vertex).xyz;

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

                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(
                    o,
                    o.position);

                return o;
            }

            half3 EvaluateWorldNormal(v2f i)
            {
                half3 tangentNormal =
                    UnpackScaleNormal(
                        tex2D(
                            _BumpMap,
                            i.uv),
                        _BumpScale);

                return normalize(
                    tangentNormal.x *
                        normalize(i.worldTangent) +
                    tangentNormal.y *
                        normalize(i.worldBitangent) +
                    tangentNormal.z *
                        normalize(i.worldNormal));
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

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                fixed4 albedoSample =
                    tex2D(
                        _MainTex,
                        i.uv) *
                    _Color;

                half3 normal =
                    EvaluateWorldNormal(i);

                half3 viewDirection =
                    normalize(
                        _WorldSpaceCameraPos -
                        i.worldPosition);

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

                half shadowAttenuation =
                    SHADOW_ATTENUATION(i);

                sunDiffuse *=
                    shadowAttenuation;

                half occlusionSample =
                    tex2D(
                        _OcclusionMap,
                        i.uv).r;

                half occlusion =
                    lerp(
                        1.0h,
                        occlusionSample,
                        _OcclusionStrength);

                half directPresence =
                    saturate(
                        sunDiffuse +
                        moonDiffuse * 0.45h);

                half3 shadowTint =
                    lerp(
                        half3(1.0h, 1.0h, 1.0h),
                        _LC_ShadowTint.rgb,
                        _ShadowTintStrength);

                half3 ambientTint =
                    lerp(
                        shadowTint,
                        half3(1.0h, 1.0h, 1.0h),
                        directPresence);

                half3 ambient =
                    _LC_AmbientColor.rgb *
                    _AmbientStrength *
                    ambientTint *
                    occlusion;

                half directOcclusion =
                    lerp(
                        occlusion,
                        1.0h,
                        0.72h);

                half3 sunLight =
                    _LC_SunColor.rgb *
                    sunDiffuse *
                    directOcclusion;

                half3 moonLight =
                    _LC_MoonColor.rgb *
                    moonDiffuse *
                    directOcclusion;

                half3 halfDirection =
                    normalize(
                        sunDirection +
                        viewDirection);

                half sunSpecular =
                    pow(
                        saturate(
                            dot(
                                normal,
                                halfDirection)),
                        _SpecularPower) *
                    _SpecularStrength *
                    sunDiffuse;

                half3 moonHalfDirection =
                    normalize(
                        moonDirection +
                        viewDirection);

                half moonSpecular =
                    pow(
                        saturate(
                            dot(
                                normal,
                                moonHalfDirection)),
                        _SpecularPower) *
                    (_SpecularStrength * 0.35h) *
                    moonDiffuse;

                half fresnel =
                    pow(
                        1.0h -
                        saturate(
                            dot(
                                normal,
                                viewDirection)),
                        _RimPower);

                half3 rimColor =
                    lerp(
                        _LC_AmbientColor.rgb,
                        _LC_SunColor.rgb +
                        _LC_MoonColor.rgb,
                        directPresence);

                half3 rim =
                    rimColor *
                    fresnel *
                    _RimStrength;

                half emissionStrength =
                    lerp(
                        _EmissionDayStrength,
                        _EmissionNightStrength,
                        _LC_NightAmount);

                float3 flickerCell =
                    floor(
                        i.worldPosition *
                        0.35);

                half flicker =
                    1.0h +
                    sin(
                        _LC_GameTime.z *
                        _EmissionFlickerSpeed +
                        dot(
                            flickerCell,
                            float3(
                                0.73,
                                1.37,
                                2.11))) *
                    _EmissionFlickerStrength;

                half3 emission =
                    tex2D(
                        _EmissionMap,
                        i.uv).rgb *
                    _EmissionColor.rgb *
                    emissionStrength *
                    max(
                        0.0h,
                        flicker);

                half3 lighting =
                    ambient +
                    sunLight +
                    moonLight;

                half3 finalColor =
                    albedoSample.rgb *
                    lighting;

                finalColor +=
                    _LC_SunColor.rgb *
                    sunSpecular;

                finalColor +=
                    _LC_MoonColor.rgb *
                    moonSpecular;

                finalColor +=
                    albedoSample.rgb *
                    rim;

                finalColor +=
                    emission;

                fixed4 result =
                    fixed4(
                        finalColor,
                        albedoSample.a);

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
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

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

                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vertShadow(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

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
