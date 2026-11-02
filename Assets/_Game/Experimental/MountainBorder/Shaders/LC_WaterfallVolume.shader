// Owned lightweight URP waterfall family. Textures are passed from UnitySampleV1;
// optional URP depth/color buffers for the restored river surface; no fluid state.
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
        _SurfaceMode ("Volume 0 / surface foam 1 / river 2 / raised impact 3", Float) = 0
        _ImpactPosition ("Impact source position", Vector) = (-58,1,8,0)
        _LipPosition ("Lip source position", Vector) = (-58,24.15,24,0)
        _FlowDirection ("Local downstream direction", Vector) = (0,0,-1,0)
        _ChannelWidths ("Lip / landing width in metres", Vector) = (5.5,6.5,0,0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull mode", Float) = 0
        _ConnectedRoute ("Saved connected route", Float) = 0
        _WaveScale ("Section displacement scale", Range(0,1)) = 1
        _ContactSoftness ("Opaque contact feather in metres", Range(.01,1)) = .18
        [HideInInspector] _PreviewTime ("Presentation seconds", Float) = -1
        [HideInInspector] _DetailFade ("Distance detail", Float) = 1
        [HideInInspector] _LandingDepth ("Receiving channel depth", Float) = 0
        [NoScaleOffset] _RiverNormalMap ("Uber river ripple", 2D) = "bump" {}
        [NoScaleOffset] _RiverFoamMap ("Uber surface foam", 2D) = "gray" {}
        [NoScaleOffset] _RiverShoreMap ("Uber shoreline dissolve", 2D) = "gray" {}
        [NoScaleOffset] _RiverCausticMap ("Owned caustics", 2D) = "black" {}
        _RiverShallowColor ("River shallows", Color) = (.12,.42,.48,1)
        _RiverDeepColor ("River depth", Color) = (.035,.16,.24,1)
        _RiverWaveStrength ("Bounded river waves", Range(0,1)) = 1
        _RiverBackgroundContribution ("Refracted ground contribution", Range(0,1)) = 1
        [HideInInspector] _RiverReflection ("Per plane reflection", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "WaterfallVolumeV2"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull] ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "LC_RiverWaves.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);
            TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);
            TEXTURE2D(_PhaseTex); SAMPLER(sampler_PhaseTex);
            TEXTURE2D(_RiverNormalMap); SAMPLER(sampler_RiverNormalMap);
            TEXTURE2D(_RiverFoamMap); SAMPLER(sampler_RiverFoamMap);
            TEXTURE2D(_RiverShoreMap); SAMPLER(sampler_RiverShoreMap);
            TEXTURE2D(_RiverCausticMap); SAMPLER(sampler_RiverCausticMap);
            TEXTURE2D(_LC_RiverFlowMap); SAMPLER(sampler_LC_RiverFlowMap);
            TEXTURE2D(_RiverReflection); SAMPLER(sampler_RiverReflection);
            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor, _FoamColor;
                float4 _ImpactPosition, _LipPosition, _FlowDirection, _ChannelWidths;
                float _Opacity, _NormalStrength, _FlowSpeed, _FoamStrength;
                float _SurfaceMode, _PreviewTime, _DetailFade, _ContactSoftness, _ConnectedRoute, _WaveScale;
                float _LandingDepth;
                half4 _RiverShallowColor,_RiverDeepColor;
                float _RiverWaveStrength,_LC_UseFlowMap,_RiverReflectionValid,_RiverOpaqueAvailable,_RiverBackgroundContribution;
                float4 _LC_FlowBounds,_Effects,_RiverFlowDirection;
                float _ObstacleCount;
                float4 _Obstacles[8],_ObstacleFlows[8];
                float4x4 _RiverReflectionVP;
            CBUFFER_END
            half4 _LC_SunDirection, _LC_SunColor, _LC_MoonDirection, _LC_MoonColor;
            half4 _LC_AmbientColor, _LC_AmbientSkyColor, _LC_AmbientEquatorColor, _LC_AmbientGroundColor;
            float4 _LC_GameTime;
            float WaterPresentationTime(){return _PreviewTime>=0?_PreviewTime:(_LC_GameTime.z>0?_LC_GameTime.z:_Time.y);}
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 station : TEXCOORD1;
                float4 section : TEXCOORD2;
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
                float thickness : TEXCOORD6;
                float waveEnvelope : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float time = WaterPresentationTime();
                // Move only the exposed section, leaving bank width, back clearance
                // and both river endpoints fixed. No extra geometry or simulation.
                float wave = sin(input.uv.y * 1.35 - time * _FlowSpeed * 1.35 + input.color.g * 9)
                    * sin(input.uv.x * 13 + input.uv.y * .38 - time * 1.7);
                float3 position = input.positionOS.xyz;
                if (_SurfaceMode < .5)
                    position += input.section.xyz * (wave * input.section.w * .18 * _DetailFade * _WaveScale);
                else if (_SurfaceMode > 2.5)
                    position.y += sin(input.uv.x*17-time*4) * input.color.r * .12;
                else if (_SurfaceMode > 1.5 && _ConnectedRoute > .5)
                    position.y += LCRiverWave(TransformObjectToWorld(position).xz,time)*input.section.w*_RiverWaveStrength;
                VertexPositionInputs p = GetVertexPositionInputs(position);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.sourcePosition = position;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv; o.station = input.station; o.color = input.color;
                o.viewZ = p.positionVS.z;
                o.thickness = input.section.w;
                o.waveEnvelope=_ConnectedRoute>.5&&_SurfaceMode>1.5&&_SurfaceMode<2.5?input.section.w*_RiverWaveStrength:0;
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
                half highlight = (pow(saturate(dot(normal,halfVector)),36) * .5h
                    + pow(saturate(dot(normal,halfVector)),8) * .12h) * (1 - foam * .8h);
                half fresnel = .025h + .3h * pow(1-saturate(dot(normal,SafeNormalize(GetWorldSpaceViewDir(positionWS)))),4);
                return albedo * (max(ambient,half3(.025,.03,.035)) + sunColor * sun + _LC_MoonColor.rgb * moon)
                    + sunColor * highlight + max(ambient,half3(.025,.03,.035)) * fresnel * (1-foam);
            }
            #include "LC_RiverSurface.hlsl"
            half4 Frag(Varyings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float time = WaterPresentationTime();
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
                half2 fineRipple = SAMPLE_TEXTURE2D(_NormalTex,sampler_NormalTex,
                    baseUV*float2(5.5,.9)-float2(time*.035,time*_FlowSpeed*.9)).rg*2-1;
                ripple += fineRipple * _NormalStrength * _DetailFade * .18;
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
                // Irregular moving streaks, with wide clear water between them.
                // Slow lateral warp prevents the fixed, parallel barcode pattern.
                float warp = SAMPLE_TEXTURE2D(_PhaseTex,sampler_PhaseTex,
                    float2(baseUV.x * 1.8 + seed,baseUV.y * .19 - time * .13)).r - .5;
                float threads = SAMPLE_TEXTURE2D(_PhaseTex,sampler_PhaseTex,
                    float2(baseUV.x * 9.1 + seed * 2.3 + warp * .7,baseUV.y * .17 - time * _FlowSpeed * .17)).r;
                float edge = min(baseUV.x,1 - baseUV.x);
                half edgeFade = smoothstep(0,.055,edge + (coarse - .5) * .052);
                half aeration = smoothstep(.08,.62,input.station.y);
                half filaments = smoothstep(.48,.76,threads * .78 + foamNoise * .3);
                half impactAeration = smoothstep(.65,.94,input.station.y);
                half foam = saturate((filaments * .83 + smoothstep(.60,.88,foamNoise) * .22
                    + aeration * .06 + impactAeration * .2) * _FoamStrength * (.6 + .4 * input.color.b));
                half alpha = _Opacity * edgeFade;
                // Keep the foundation continuous; breakup is stronger only in the overlapping sheets.
                half breakup = smoothstep(.08,.34,coarse + foamNoise * .24);
                alpha *= lerp(lerp(.76h,.38h,input.color.a),1.0h,breakup);
                alpha *= lerp(.64h,1.0h,input.color.b);
                // Shed the closed volume into surface foam before its horizontal pool tail
                // can read as a blue rectangular card over the river.
                half endpoint = smoothstep(.01,.075,input.station.y)
                    * (1 - smoothstep(.855,.985,input.station.y + (coarse - .5) * .045));
                alpha *= _ConnectedRoute > .5 ? 1 : endpoint;
                if (_ConnectedRoute > .5 && _SurfaceMode < .5) alpha = max(alpha,_Opacity*.65h);
                half3 waterPigment=_WaterColor.rgb;
                // Approximate optical path through the actual mesh section. This
                // is absorption/scattering, not a simulated fluid or reflection.
                half facing = abs(dot(n,SafeNormalize(GetWorldSpaceViewDir(input.positionWS))));
                half optical = 1-exp2(-max(.03,input.thickness) * 2.8 / max(.3h,facing));
                if (_SurfaceMode < .5)
                {
                    waterPigment = lerp(_RiverShallowColor.rgb,_RiverDeepColor.rgb,optical*.7h);
                    alpha *= lerp(.67h,1.0h,optical);
                }
                if (_SurfaceMode > 2.5)
                {
                    half sprayBreakup = smoothstep(.24,.68,foamNoise*.65 + threads*.5);
                    alpha = input.color.r * edgeFade * sprayBreakup * _Opacity * _FoamStrength;
                    foam = .92h;
                }
                else if (_SurfaceMode > 1.5)
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
                    float2 flow = normalize(_FlowDirection.xz + float2(.000001,0));
                    float2 acrossFlow = float2(-flow.y,flow.x);
                    float width = max(1,_ChannelWidths.y);
                    float along = dot(delta.xz,flow);
                    float2 stretched = float2(dot(delta.xz,acrossFlow),along-width*.15) / (width*float2(.64,1.12));
                    float radius = length(stretched) + (coarse - .5) * .16;
                    float pool = (1 - smoothstep(.38,1.55,radius)) * (1 - smoothstep(.3,2,abs(delta.y)));
                    float wakes = smoothstep(.18,.68,foamNoise * .7 + coarse * .4);
                    float impactCore = 1 - smoothstep(.18,.63,radius);
                    float ring = pow(saturate(.5 + .5*sin(radius*22-time*3.5+coarse*3)),5)
                        * smoothstep(.4,.7,radius) * (1-smoothstep(.9,1.55,radius));
                    float3 lipDelta = input.sourcePosition - _LipPosition.xyz;
                    float lipMask = (1 - smoothstep(_ChannelWidths.x*.12,_ChannelWidths.x*.65,abs(dot(lipDelta.xz,flow)))) * (1 - smoothstep(.2,1.5,abs(lipDelta.y)));
                    float footprint = edgeFade * max(pool * max(wakes*.75+ring*.5,impactCore), lipMask * smoothstep(.35,.8,foamNoise) * .7);
                    alpha = saturate(footprint * _Opacity * _FoamStrength) * saturate(input.color.r);
                    foam = 1;
                }
                if (_ConnectedRoute > .5 && _SurfaceMode < .5)
                {
                    // The saved body has real thickness; retain a readable blue core.
                    // Clear pools, aerated slopes and detached sheets share one advected phase.
                    half slope = saturate(1-abs(input.normalWS.y));
                    // One authored palette for the falling section and receiving river.
                    // Thickness changes absorption; it must not introduce a separate blue hue.
                    waterPigment = lerp(_RiverShallowColor.rgb,_RiverDeepColor.rgb,optical*.7h);
                    foam = saturate((filaments*filaments*(.48h+.34h*coarse)
                        + smoothstep(.69,.9,foamNoise)*.22h + impactAeration*.10h)
                        * _FoamStrength * (.65h+.5h*slope));
                    alpha = _Opacity * lerp(.94h,1.0h,optical) * lerp(.7h,1.0h,edgeFade);
                    // The saved landing strip reaches the same depth profile, pigment,
                    // normal and advected phase as the receiving river across its full width.
                    half join=saturate(input.color.a);
                    float channelDepth=max(0,_LandingDepth)*(1-smoothstep(.3,1,abs(baseUV.x*2-1)));
                    half deep=1-exp2(-channelDepth*.85);
                    half3 riverPigment=lerp(_RiverShallowColor.rgb,_RiverDeepColor.rgb,deep);
                    half riverFoam=saturate((1-smoothstep(.08,.65,channelDepth))*smoothstep(.3,.72,foamNoise)*.68+smoothstep(.67,.88,foamNoise)*.075+filaments*filaments*.12h);
                    half riverAlpha=_Opacity*smoothstep(0,.035,edge);
                    waterPigment=lerp(waterPigment,riverPigment,join);foam=lerp(foam,riverFoam,join);alpha=lerp(alpha,riverAlpha,join);
                }
                else if (_ConnectedRoute > .5 && _SurfaceMode > 1.5 && _SurfaceMode < 2.5)
                {
                    // This family supplies its own foam; the old signed-UV overlay is not used.
                    foam = saturate(foam + filaments*filaments*.12h);
                    alpha = _Opacity * smoothstep(0,.035,edge) * input.color.r;
                }
                else if (_ConnectedRoute > .5 && _SurfaceMode > .5 && _SurfaceMode < 1.5)
                {
                    alpha *= (1-smoothstep(.48,1,baseUV.y/max(1,_ChannelWidths.y)))
                        * lerp(.18h,1.0h,smoothstep(.25,.75,foamNoise*.7+threads*.4));
                }
                // Soften intersections with opaque rocks/terrain. Transparent
                // river surfaces do not write depth and cannot erase the impact.
                float rawDepth = SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                float sceneEye = IsPerspectiveProjection() ? LinearEyeDepth(rawDepth,_ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);
                alpha *= saturate((sceneEye + input.viewZ) / max(.01,_ContactSoftness));
                half3 albedo = lerp(waterPigment,_FoamColor.rgb,foam);
                half3 color = Shade(albedo,n,input.positionWS,foam);
                half calm=_ConnectedRoute>.5&&_SurfaceMode>1.5&&_SurfaceMode<2.5?1:
                    (_ConnectedRoute>.5&&_SurfaceMode<.5?saturate(input.color.a):0);
                if(calm>0)
                {
                    float riverDepth=_SurfaceMode>1.5?max(0,input.station.x):
                        max(0,_LandingDepth)*(1-smoothstep(.3,1,abs(baseUV.x*2-1)));
                    float speed=_SurfaceMode>1.5?input.station.y:_RiverFlowDirection.z;
                    // Smooth analytic wave normal avoids exposing individual mesh triangles.
                    float2 slope=LCRiverWaveSlope(input.positionWS.xz,time)*input.waveEnvelope;
                    half3 geometric=SafeNormalize(input.normalWS+float3(-slope.x,0,-slope.y));
                    half4 river=LCRiverSurface(input.positionWS,geometric,GetNormalizedScreenSpaceUV(input.positionCS),
                        -input.viewZ,riverDepth,speed,time,IS_FRONT_VFACE(frontFace,1,-1),downstream.xz);
                    color=lerp(color,river.rgb,calm);
                }
                half fog = ComputeFogFactorZ0ToFar(max(-input.viewZ - _ProjectionParams.y,0));
                return half4(MixFog(color,fog),saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
