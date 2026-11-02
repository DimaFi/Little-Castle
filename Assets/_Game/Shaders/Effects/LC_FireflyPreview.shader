Shader "Little Castle/Effects/Firefly Preview URP"
{
    Properties
    {
        [HDR] _FireflyColor ("Core tint", Color) = (1,0.89,0.38,1)
        [PerRendererData] _FireflyInstanceColor ("Instance color", Color) = (0,0,0,0)
        _FireflyDiameter ("Diameter in metres", Range(0.01,1)) = 0.13
        _FireflyGlow ("HDR emission", Range(0,5)) = 1.4
        _FireflyNight ("Night strength", Range(0,1)) = 1
        _FireflyRange ("View range", Float) = 95
        _FireflyTime ("Presentation time", Float) = 0
        _FireflyMotion ("Amplitude, speed, wind, flicker", Vector) = (.6,.7,.12,.22)
        _FireflyFades ("Near, depth, color variance, unused", Vector) = (.75,0,.15,0)
        _FireflyFog ("Fog influence", Range(0,1)) = .8
        _FireflyBehavior ("Pause fraction and cycle seconds", Vector) = (.22,7,0,0)
        _FireflyLook ("Core, halo, maximum pixel diameter", Vector) = (.13,.2,11,0)
        [PerRendererData] _FireflyCellFade ("Cell spawn fade", Range(0,1)) = 1
        [PerRendererData] _FireflyLodCount ("Fractional visible instances", Float) = 1023
        _FireflyLod ("Start fraction, survival, far brightness", Vector) = (.58,.28,.65,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15"
               "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Firefly Preview Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _LC_FIREFLY_SOFT_DEPTH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #if defined(_LC_FIREFLY_SOFT_DEPTH)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                half4 _FireflyColor;
                float _FireflyDiameter;
                float _FireflyGlow;
                float _FireflyNight;
                float _FireflyRange;
                float _FireflyTime;
                float4 _FireflyMotion;
                float4 _FireflyFades;
                float _FireflyFog;
                float4 _FireflyBehavior;
                float4 _FireflyLook;
                float4 _FireflyLod;
                float _FireflyCellFade;
                float _FireflyLodCount;
            CBUFFER_END

            float4 _LC_Wind;
            UNITY_INSTANCING_BUFFER_START(PerFirefly)
                UNITY_DEFINE_INSTANCED_PROP(float4, _FireflyInstanceColor)
            UNITY_INSTANCING_BUFFER_END(PerFirefly)

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float alpha : TEXCOORD1;
                float3 color : TEXCOORD2;
                float fog : TEXCOORD3;
                float eyeDepth : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash01(float3 p)
            {
                return frac(sin(dot(p,float3(12.9898,78.233,43.719))) * 43758.5453);
            }
            Varyings Vert(Attributes input)
            {
                Varyings o=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 center=TransformObjectToWorld(float3(0,0,0));
                float3 staticCenter=center;
                float seed=Hash01(floor(center*17.37));
                // Each insect has its own pause cycle. Smoothstep travel time
                // maintains C1 motion at the transition instead of teleporting.
                float cycle=max(2.0,_FireflyBehavior.y);
                float shifted=(_FireflyTime+seed*19.71)/cycle;
                float phase=frac(shifted);
                float movePhase=saturate((phase-_FireflyBehavior.x)/
                    max(.05,1.0-_FireflyBehavior.x));
                movePhase=movePhase*movePhase*(3.0-2.0*movePhase);
                float travelSeconds=(floor(shifted)+movePhase)*cycle*
                    (1.0-_FireflyBehavior.x);
                float t=travelSeconds*_FireflyMotion.y*(.68+seed*.65)+seed*6.2831853;
                // Light continues a little while the insect is resting.
                float twinkleTime=_FireflyTime*_FireflyMotion.y*(.74+seed*.7)+seed*13.1;
                float amplitude=_FireflyMotion.x;
                center.x += amplitude*(.55*sin(t)+.28*sin(1.73*t+seed*13));
                center.z += amplitude*(.4*cos(1.11*t+seed*7)+.22*sin(.53*t));
                center.y += amplitude*(.23*sin(.71*t+seed*11));
                center.xz += _LC_Wind.xy * _LC_Wind.z * _FireflyMotion.z*.1*sin(.43*t);

                float3 fromEye=center-_WorldSpaceCameraPos;
                float dist=length(fromEye);
                float4 centerClip=TransformWorldToHClip(center);
                // Works with strategic orthographic and low-altitude perspective cameras.
                float pixelsPerMetre=max(.001,abs(UNITY_MATRIX_P._m11)*_ScreenParams.y*.5 /
                    max(.00001,abs(centerClip.w)));
                float lodDistance=saturate(dist/max(1.0,_FireflyRange));
                float farBlend=smoothstep(_FireflyLod.x,1.0,lodDistance);
                float diameter=clamp(_FireflyDiameter*(.78+.44*seed),
                    .7/pixelsPerMetre,max(3.0,_FireflyLook.z)/pixelsPerMetre);
                diameter*=lerp(1.0,.83,farBlend);

                float3 right=normalize(mul((float3x3)UNITY_MATRIX_I_V,float3(1,0,0)));
                float3 up=normalize(mul((float3x3)UNITY_MATRIX_I_V,float3(0,1,0)));
                float3 worldPosition=center+(right*input.positionOS.x+
                    up*input.positionOS.y)*diameter;
                o.positionCS=TransformWorldToHClip(worldPosition);
                o.screenPos=ComputeScreenPos(o.positionCS);
                o.eyeDepth=-TransformWorldToView(worldPosition).z;
                o.uv=input.uv;
                o.fog=ComputeFogFactor(o.positionCS.z);

                float front=smoothstep(max(.001,_FireflyFades.x),
                    max(.002,_FireflyFades.x)+.7,dist);
                float back=1-smoothstep(_FireflyRange*.82,_FireflyRange,dist);
                float pulse=.78+.22*sin(twinkleTime*.76+sin(twinkleTime*.41)*1.3);
                pulse=lerp(1,pulse,saturate(_FireflyMotion.w*2));
                // Stable per-point pseudo-random LOD prevents synchronised fading.
                // Fragment clipping reduces distant glow overdraw (not vertex count).
                float lodSeed=Hash01(floor(staticCenter*29.317)+float3(31,17,7));
                float survivingFraction=lerp(1.0,_FireflyLod.y,farBlend);
                float stochasticFade=smoothstep(lodSeed-.075,lodSeed+.075,survivingFraction);
                float4 instanceColor=UNITY_ACCESS_INSTANCED_PROP(PerFirefly, _FireflyInstanceColor);
                float rank=frac(instanceColor.a)*2048-1;
                float lodAlpha=saturate(_FireflyLodCount-rank);
                o.alpha=saturate(_FireflyNight*front*back*pulse*
                    stochasticFade*lerp(1.0,_FireflyLod.z,farBlend)*_FireflyCellFade*lodAlpha);
                float3 baseColor=lerp(_FireflyColor.rgb,instanceColor.rgb,step(1,instanceColor.a));
                o.color=lerp(baseColor,baseColor*float3(.72,1.06,.57),
                    seed*saturate(_FireflyFades.z));
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float radius=length(i.uv*2-1);
                float core=1-smoothstep(_FireflyLook.x,
                    _FireflyLook.x+.25,radius);
                float halo=(1-smoothstep(.20,1.0,radius))*_FireflyLook.y;
                float opacity=(core*.85+halo)*i.alpha;
                clip(opacity-.001);
                #if defined(_LC_FIREFLY_SOFT_DEPTH)
                    float2 uv=i.screenPos.xy/max(.00001,i.screenPos.w);
                    float rawDepth=SampleSceneDepth(uv);
                    float sceneEye=LinearEyeDepth(rawDepth,_ZBufferParams);
                    if(_FireflyFades.y>.0001)
                        opacity*=saturate((sceneEye-i.eyeDepth-.04)/_FireflyFades.y);
                #endif
                half3 emission=i.color*_FireflyGlow*(core+halo);
                half3 fogged=MixFogColor(emission,half3(0,0,0),i.fog);
                emission=lerp(emission,fogged,saturate(_FireflyFog));
                return half4(emission,opacity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
