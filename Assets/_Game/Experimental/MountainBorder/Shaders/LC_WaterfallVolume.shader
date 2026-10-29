// Owned lightweight URP waterfall family. Textures are passed from UnitySampleV1;
// no Shader Graph/VFX/scene-color dependency and no authoritative water state.
Shader "Little Castle/Experimental/Waterfall Volume V2"
{
    Properties
    {
        [NoScaleOffset] _NormalTex ("Linear packed ripple normal", 2D) = "bump" {}
        [NoScaleOffset] _FoamTex ("Linear packed foam", 2D) = "gray" {}
        [NoScaleOffset] _PhaseTex ("Linear phase noise", 2D) = "gray" {}
        _WaterColor ("Water", Color) = (.13,.35,.38,1)
        _FoamColor ("Foam", Color) = (.76,.85,.82,1)
        _Opacity ("Opacity", Range(0,1)) = .86
        _NormalStrength ("Flow normal strength", Range(0,2)) = .52
        _FlowSpeed ("Downstream metres per second", Range(1,12)) = 5.2
        _FoamStrength ("Foam", Range(0,1.5)) = .9
        _SurfaceMode ("Volume 0 / surface foam 1 / river body 2", Float) = 0
        _ImpactPosition ("Impact source position", Vector) = (-58,1,8,0)
        _LipPosition ("Lip source position", Vector) = (-58,24.15,24,0)
        [HideInInspector] _PreviewTime ("Presentation seconds", Float) = -1
        [HideInInspector] _DetailFade ("Distance detail", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "WaterfallVolumeV2"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);
            TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);
            TEXTURE2D(_PhaseTex); SAMPLER(sampler_PhaseTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor, _FoamColor;
                float4 _ImpactPosition, _LipPosition;
                float _Opacity, _NormalStrength, _FlowSpeed, _FoamStrength;
                float _SurfaceMode, _PreviewTime, _DetailFade;
            CBUFFER_END
            half4 _LC_SunDirection, _LC_SunColor, _LC_MoonDirection, _LC_MoonColor;
            half4 _LC_AmbientColor, _LC_AmbientSkyColor, _LC_AmbientEquatorColor, _LC_AmbientGroundColor;
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 station : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 sourcePosition : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float2 station : TEXCOORD4;
                half4 color : COLOR;
                float viewZ : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.sourcePosition = input.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv; o.station = input.station; o.color = input.color;
                o.viewZ = p.positionVS.z;
                return o;
            }
            half FoamSample(float2 uv)
            {
                half3 sample = SAMPLE_TEXTURE2D(_FoamTex,sampler_FoamTex,uv).rgb;
                return saturate(sample.g * 1.2h + sample.b * .3h);
            }
            half3 Shade(half3 albedo, half3 normal, float3 positionWS, half foam)
            {
                float signal = dot(abs(_LC_AmbientColor.rgb) + abs(_LC_AmbientSkyColor.rgb) + abs(_LC_SunColor.rgb),half3(1,1,1));
                Light mainLight = GetMainLight();
                half3 sunDirection = signal > .0001 ? SafeNormalize(_LC_SunDirection.xyz) : mainLight.direction;
                half3 sunColor = signal > .0001 ? _LC_SunColor.rgb : mainLight.color;
                half3 ambient = _LC_AmbientSkyColor.rgb * saturate(normal.y)
                    + _LC_AmbientEquatorColor.rgb * saturate(1 - abs(normal.y))
                    + _LC_AmbientGroundColor.rgb * saturate(-normal.y);
                if (dot(abs(ambient),half3(1,1,1)) < .0001) ambient = signal > .0001 ? _LC_AmbientColor.rgb : SampleSH(normal);
                half sun = .3h + .7h * saturate(dot(normal,sunDirection));
                half moon = .3h + .7h * saturate(dot(normal,SafeNormalize(_LC_MoonDirection.xyz)));
                half3 halfVector = SafeNormalize(sunDirection + SafeNormalize(GetWorldSpaceViewDir(positionWS)));
                half highlight = pow(saturate(dot(normal,halfVector)),28) * .14h * (1 - foam * .7h);
                return albedo * (max(ambient,half3(.025,.03,.035)) + sunColor * sun + _LC_MoonColor.rgb * moon)
                    + sunColor * highlight;
            }
            half4 Frag(Varyings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float time = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                float2 baseUV = input.uv;
                float seed = input.color.g;
                float phase = SAMPLE_TEXTURE2D(_PhaseTex,sampler_PhaseTex,float2(baseUV.x * 1.7,baseUV.y * .037)).r;
                float clock = time * _FlowSpeed / 4.5 + phase * .35 + seed;
                float a = frac(clock), b = frac(clock + .5);
                half blend = 1 - abs(a * 2 - 1);
                float2 uvA = baseUV * float2(.9,.16) - float2(0, a * 1.08);
                float2 uvB = baseUV * float2(.9,.16) - float2(0, b * 1.08);
                half2 ripple = (lerp(SAMPLE_TEXTURE2D(_NormalTex,sampler_NormalTex,uvB).rg,
                    SAMPLE_TEXTURE2D(_NormalTex,sampler_NormalTex,uvA).rg,blend) * 2 - 1) * _NormalStrength * _DetailFade * .6;
                float3 px = ddx(input.positionWS), py = ddy(input.positionWS);
                float2 ux = ddx(input.uv), uy = ddy(input.uv);
                float orient = ux.x * uy.y - ux.y * uy.x < 0 ? -1 : 1;
                half3 across = SafeNormalize((px * uy.y - py * ux.y) * orient);
                half3 downstream = SafeNormalize((py * ux.x - px * uy.x) * orient);
                half3 n = SafeNormalize(input.normalWS) * IS_FRONT_VFACE(frontFace,1,-1);
                n = SafeNormalize(n + across * ripple.x + downstream * ripple.y);
                // Stretch the foam along actual arc length rather than printing round cloud blobs
                // over each section. Two counterphased samples keep advection continuous.
                half foamNoise = lerp(FoamSample(uvB * float2(1.8,.85)),FoamSample(uvA * float2(1.8,.85)),blend);
                float coarse = SAMPLE_TEXTURE2D(_PhaseTex,sampler_PhaseTex,
                    float2(baseUV.x * 1.4 + seed,baseUV.y * .13 - time * _FlowSpeed * .13)).r;
                float threads = SAMPLE_TEXTURE2D(_PhaseTex,sampler_PhaseTex,
                    float2(baseUV.x * 4.3 + seed * 3.1,baseUV.y * .11 - time * _FlowSpeed * .11)).r;
                float edge = min(baseUV.x,1 - baseUV.x);
                half edgeFade = smoothstep(0,.055,edge + (coarse - .5) * .052);
                half aeration = smoothstep(.08,.62,input.station.y);
                half filaments = smoothstep(.35,.73,threads * .72 + foamNoise * .45);
                half foam = saturate((filaments * .85 + smoothstep(.35,.78,foamNoise) * .32
                    + aeration * .14) * _FoamStrength * (.75 + .25 * input.color.b));
                half alpha = _Opacity * edgeFade;
                // Keep the foundation continuous; breakup is stronger only in the overlapping sheets.
                half breakup = smoothstep(.08,.34,coarse + foamNoise * .24);
                alpha *= lerp(lerp(.76h,.38h,input.color.a),1.0h,breakup);
                alpha *= lerp(.64h,1.0h,input.color.b);
                // Shed the closed volume into surface foam before its horizontal pool tail
                // can read as a blue rectangular card over the river.
                half endpoint = smoothstep(.01,.075,input.station.y)
                    * (1 - smoothstep(.855,.985,input.station.y + (coarse - .5) * .045));
                alpha *= endpoint;
                half3 waterPigment=_WaterColor.rgb;
                if (_SurfaceMode > 1.5)
                {
                    // The river strip supplies physical depth/speed in UV1. This
                    // two-level preview does not need a single planar reflection
                    // or camera-color buffer to retain its own water pigment.
                    float depth=max(0,input.station.x);
                    half deep=1-exp2(-depth*.85);
                    waterPigment=lerp(_WaterColor.rgb,_WaterColor.rgb*half3(.32,.50,.72),deep);
                    half shore=(1-smoothstep(.08,.65,depth))*smoothstep(.3,.72,foamNoise);
                    foam=saturate(shore*.68+smoothstep(.67,.88,foamNoise)*.075);
                    alpha=smoothstep(0,.045,edge)*input.color.r*lerp(.64h,.96h,deep);
                }
                else if (_SurfaceMode > .5)
                {
                    float3 delta = input.sourcePosition - _ImpactPosition.xyz;
                    float2 flow = _ImpactPosition.xz - _LipPosition.xz;
                    flow *= rsqrt(max(dot(flow,flow),.0001));
                    float2 acrossFlow = float2(-flow.y,flow.x);
                    float2 stretched = float2(dot(delta.xz,acrossFlow),dot(delta.xz,flow)) / float2(3.8,5.8);
                    float radius = length(stretched) + (coarse - .5) * .2;
                    float pool = (1 - smoothstep(.28,1.42,radius)) * (1 - smoothstep(.3,2,abs(delta.y)));
                    float wakes = smoothstep(.18,.68,foamNoise * .7 + coarse * .4);
                    float impactCore = 1 - smoothstep(.12,.68,radius);
                    float3 lipDelta = input.sourcePosition - _LipPosition.xyz;
                    float lipMask = (1 - smoothstep(.6,3.6,abs(dot(lipDelta.xz,flow)))) * (1 - smoothstep(.2,1.5,abs(lipDelta.y)));
                    float footprint = edgeFade * max(pool * max(wakes,impactCore * .88), lipMask * smoothstep(.25,.75,foamNoise) * .7);
                    alpha = saturate(footprint * _Opacity * _FoamStrength);
                    foam = 1;
                }
                half3 albedo = lerp(waterPigment,_FoamColor.rgb,foam);
                half3 color = Shade(albedo,n,input.positionWS,foam);
                half fog = ComputeFogFactorZ0ToFar(max(-input.viewZ - _ProjectionParams.y,0));
                return half4(MixFog(color,fog),saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
