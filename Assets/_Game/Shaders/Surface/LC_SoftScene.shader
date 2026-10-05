Shader "Little Castle/Presentation/Soft Scene"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
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
