Shader "Little Castle/Effects/LC Fireflies"
{
    Properties
    {
        [HDR] _LC_FireflyColor ("Glow tint", Color) = (1,0.88,0.43,1)
        _LC_FireflySize ("World diameter (m)", Range(0.01,1)) = 0.14
        _LC_FireflyGlow ("Emission", Range(0,4)) = 1.3
        _LC_FireflyFlicker ("Flicker", Range(0,0.5)) = 0.23
        _LC_FireflyNightMin ("Night threshold", Range(0,1)) = 0.08
        _LC_FireflyViewRange ("Culling distance (m)", Float) = 95
        _LC_FireflyTime ("Presentation time (s)", Float) = 0
        _LC_FireflyMotion ("Amplitude / speed / wind / color spread", Vector) = (0.55,0.65,0.15,0.16)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Firefly Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local _LC_FIREFLY_SOFT_DEPTH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #if defined(_LC_FIREFLY_SOFT_DEPTH)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
            half4 _LC_FireflyColor;
            float _LC_FireflySize;
            float _LC_FireflyGlow;
            float _LC_FireflyFlicker;
            float _LC_FireflyNightMin;
            float _LC_FireflyViewRange;
            float _LC_FireflyTime;
            float4 _LC_FireflyMotion;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(FireflyProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _LC_FireflyInstanceColor)
            UNITY_INSTANCING_BUFFER_END(FireflyProps)
            half _LC_NightAmount;
            float4 _LC_Wind;

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
                float fogFactor : TEXCOORD1;
                float alpha : TEXCOORD2;
                float3 tint : TEXCOORD3;
                float eyeDepth : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Rand(float3 p) { return frac(sin(dot(p,float3(12.9898,78.233,43.719))) * 43758.5453); }

            Varyings Vertex(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 localColor=UNITY_ACCESS_INSTANCED_PROP(FireflyProps,_LC_FireflyInstanceColor);

                float3 center=TransformObjectToWorld(float3(0,0,0));
                float seed=Rand(floor(center*17.0));
                float speed=_LC_FireflyMotion.y*(.65+seed*.8);
                float t=_LC_FireflyTime*speed+seed*6.2831853;
                // Two incommensurate frequencies avoid synchronous circular-orbit motion.
                center.x+=_LC_FireflyMotion.x*(.55*sin(t)+.28*sin(1.73*t+seed*13));
                center.z+=_LC_FireflyMotion.x*(.40*cos(1.11*t+seed*7)+.22*sin(.53*t));
                center.y+=_LC_FireflyMotion.x*(.23*sin(.71*t+seed*11));
                center.xz+=_LC_Wind.xz*_LC_FireflyMotion.z*.1*sin(.43*t);

                float3 delta=center-_WorldSpaceCameraPos;
                float distance=length(delta);
                // Pixel bounds avoid a giant disc beside the near plane or at strategic zoom.
                float projectionScale=max(.0001,abs(UNITY_MATRIX_P._m11)*_ScreenParams.y*.5);
                float metresPerPixel=max(.00001,abs(TransformWorldToHClip(center).w)/projectionScale);
                float diameter=clamp(_LC_FireflySize*(.8+.4*seed),metresPerPixel*.7,metresPerPixel*11);
                float3 right=normalize(mul((float3x3)UNITY_MATRIX_I_V,float3(1,0,0)));
                float3 up=normalize(mul((float3x3)UNITY_MATRIX_I_V,float3(0,1,0)));
                float3 position=center+(right*input.positionOS.x+up*input.positionOS.y)*diameter;
                output.positionCS=TransformWorldToHClip(position);
                output.screenPos=ComputeScreenPos(output.positionCS);
                output.eyeDepth=-TransformWorldToView(position).z;
                output.uv=input.uv;
                output.fogFactor=ComputeFogFactor(output.positionCS.z);

                float appear=smoothstep(.45,1.6,distance);
                float farFade=1-smoothstep(_LC_FireflyViewRange*.83,_LC_FireflyViewRange,distance);
                float night=smoothstep(_LC_FireflyNightMin, max(_LC_FireflyNightMin+.001,1),_LC_NightAmount);
                float twinkle=.75+.25*sin(t*.76+sin(t*.41)*1.3);
                twinkle=lerp(1,twinkle,saturate(_LC_FireflyFlicker*2));
                output.alpha=saturate(appear*farFade*night*twinkle);
                output.tint=lerp(localColor.rgb,localColor.rgb*float3(.68,1.05,.48),
                    seed*saturate(_LC_FireflyMotion.w));
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float radius=length(input.uv*2-1);
                float core=1-smoothstep(.05,.35,radius);
                float halo=(1-smoothstep(.12,1,radius))*.22;
                half opacity=(core*.8+halo)*input.alpha;
                #if defined(_LC_FIREFLY_SOFT_DEPTH)
                    float raw=SampleSceneDepth(input.screenPos.xy/max(input.screenPos.w,.00001));
                    float sceneEye=LinearEyeDepth(raw,_ZBufferParams);
                    opacity*=saturate((sceneEye-input.eyeDepth-.05)/.65);
                #endif
                half fog=InitializeInputDataFog(float4(0,0,0,1),input.fogFactor);
                float3 emission=input.tint*_LC_FireflyGlow*(core+halo);
                emission=MixFogColor(emission,half3(0,0,0),fog);
                return half4(emission,opacity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
