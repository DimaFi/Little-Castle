Shader "Little Castle/Presentation/Soft Scene"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_BlurTex);
        TEXTURE2D_X_FLOAT(_LCSoftSceneDepthTexture);
        float4 _LCSoftSceneSourceTexelSize;
        float4 _LCSoftSceneScreenTexelSize;
        float4 _LCSoftSceneBlurScaleBias;
        float4 _LCSoftSceneDepthScaleBias;
        float2 _BlurAxis;
        float _FocusDistance, _FocusBand, _BlurStrength, _ContactOcclusion;

        half4 BlurURP(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float2 d = _LCSoftSceneSourceTexelSize.xy * _BlurAxis * 3.5;
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord) * .4 +
                (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + d) +
                 SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord - d)) * .24 +
                (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord + d * 2) +
                 SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord - d * 2)) * .06;
        }

        float EyeDepthURP(float2 screenUV)
        {
            float2 uv = screenUV * _LCSoftSceneDepthScaleBias.xy + _LCSoftSceneDepthScaleBias.zw;
            float raw = SAMPLE_TEXTURE2D_X(_LCSoftSceneDepthTexture, sampler_PointClamp, uv).r;
            if (unity_OrthoParams.w > .5)
            {
                #if defined(UNITY_REVERSED_Z)
                raw = 1 - raw;
                #endif
                return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
            }
            return LinearEyeDepth(raw, _ZBufferParams);
        }

        half4 CompositeURP(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float2 screenUV = DYNAMIC_SCALING_REMOVE_SCALEBIAS(i.texcoord);
            float depth = EyeDepthURP(screenUV);
            float amount = smoothstep(_FocusBand, _FocusBand + 9, abs(depth - _FocusDistance)) * _BlurStrength;
            // Full-resolution contact must not reintroduce sharp detail over defocused color.
            // Keep it in focus; fade it out before blurred color becomes the majority.
            float contactVisibility = 1 - smoothstep(0, .5, amount);
            float2 blurUV = screenUV * _LCSoftSceneBlurScaleBias.xy + _LCSoftSceneBlurScaleBias.zw;
            half3 color = lerp(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord).rgb,
                SAMPLE_TEXTURE2D_X(_BlurTex, sampler_LinearClamp, blurUV).rgb, amount);
            // Keep the original plane-gradient rejection in destination screen UVs.
            float2 gradient = float2(ddx(depth) / ddx(screenUV.x), ddy(depth) / ddy(screenUV.y));
            float ao = 0;
            const float2 directions[8] = { float2(1,0), float2(-1,0), float2(0,1), float2(0,-1),
                float2(.707,.707), float2(-.707,.707), float2(.707,-.707), float2(-.707,-.707) };
            [branch] if (_ContactOcclusion > 0 && contactVisibility > 0)
            {
                [unroll] for (int k = 0; k < 8; k++)
                {
                    float2 offset = directions[k] * 7 * _LCSoftSceneScreenTexelSize.xy;
                    float neighbour = EyeDepthURP(screenUV + offset);
                    float delta = depth + dot(gradient, offset) - neighbour;
                    ao += smoothstep(.035, .25, delta) * (1 - smoothstep(.5, 1.3, delta));
                }
            }
            color *= 1 - ao * .125 * _ContactOcclusion * contactVisibility;
            half luminance = dot(color, half3(.2126, .7152, .0722));
            color = lerp(luminance.xxx, color, .96);
            color /= 1 + max(color - .8, 0) * .12;
            float2 p = (screenUV - .5) * 2;
            color *= 1 - .045 * saturate(dot(p,p) * .5);
            return half4(color, 1);
        }
        ENDHLSL
        Pass
        {
            Name "SoftSceneBlur"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment BlurURP
            ENDHLSL
        }
        Pass
        {
            Name "SoftSceneComposite"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment CompositeURP
            ENDHLSL
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BlurTex;
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        float4 _MainTex_TexelSize;
        float2 _BlurAxis;
        float _FocusDistance;
        float _FocusBand, _BlurStrength, _ContactOcclusion;
        fixed4 blur(v2f_img i) : SV_Target
        {
            float2 d = _MainTex_TexelSize.xy * _BlurAxis * 3.5;
            return tex2D(_MainTex,i.uv)*.4 +
                (tex2D(_MainTex,i.uv+d)+tex2D(_MainTex,i.uv-d))*.24 +
                (tex2D(_MainTex,i.uv+d*2)+tex2D(_MainTex,i.uv-d*2))*.06;
        }
        float eyeDepth(float2 uv)
        {
            float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv);
            float depth = LinearEyeDepth(raw);
            if(unity_OrthoParams.w > .5)
            {
                #if defined(UNITY_REVERSED_Z)
                raw = 1-raw;
                #endif
                depth = lerp(_ProjectionParams.y,_ProjectionParams.z,raw);
            }
            return depth;
        }
        fixed4 composite(v2f_img i) : SV_Target
        {
            float depth=eyeDepth(i.uv);
            float amount = smoothstep(_FocusBand,_FocusBand+9,abs(depth-_FocusDistance))*_BlurStrength;
            half3 color = lerp(tex2D(_MainTex,i.uv).rgb,tex2D(_BlurTex,i.uv).rgb,amount);
            // Reject the plane's depth gradient: only nearby protrusions occlude.
            // UV derivatives preserve orientation when the render target is flipped.
            float2 gradient=float2(ddx(depth)/ddx(i.uv.x),ddy(depth)/ddy(i.uv.y));
            float ao=0;
            const float2 directions[8]={float2(1,0),float2(-1,0),float2(0,1),float2(0,-1),
                float2(.707,.707),float2(-.707,.707),float2(.707,-.707),float2(-.707,-.707)};
            [branch] if(_ContactOcclusion>0) {
            [unroll] for(int k=0;k<8;k++) {
                float2 pixels=directions[k]*7;
                float neighbour=eyeDepth(i.uv+pixels*abs(_MainTex_TexelSize.xy));
                float delta=depth+dot(gradient,pixels*abs(_MainTex_TexelSize.xy))-neighbour;
                ao+=smoothstep(.035,.25,delta)*(1-smoothstep(.5,1.3,delta));
            }
            }
            color*=1-ao*.125*_ContactOcclusion;
            half luminance = dot(color,half3(.2126,.7152,.0722));
            color = lerp(luminance.xxx,color,.96);
            color /= 1+max(color-.8,0)*.12;
            float2 p=(i.uv-.5)*2;
            color *= 1-.045*saturate(dot(p,p)*.5);
            return half4(color,1);
        }
        ENDCG
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment composite
            ENDCG }
    }
}
