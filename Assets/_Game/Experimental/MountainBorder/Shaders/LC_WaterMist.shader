Shader "Little Castle/Experimental/Water Mist V2"
{
    Properties
    {
        _Tint ("Spray tint", Color) = (.76,.85,.82,1)
        [NoScaleOffset] _NoiseTex ("Linear soft breakup", 2D) = "gray" {}
        [HideInInspector] _PreviewTime ("Presentation seconds", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _PreviewTime;
            CBUFFER_END
            half4 _LC_AmbientColor, _LC_AmbientSkyColor, _LC_SunColor, _LC_MoonColor;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float viewZ : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS; o.viewZ = p.positionVS.z; o.uv = input.uv; o.color = input.color;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float time = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                float2 delta = input.uv * 2 - 1;
                half soft = 1 - smoothstep(.12,1,dot(delta,delta));
                half noise = SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,input.uv * .8 + float2(time * .03,-time * .025)).r;
                half3 ambient = max(_LC_AmbientColor.rgb,_LC_AmbientSkyColor.rgb);
                half signal = dot(ambient + _LC_SunColor.rgb + _LC_MoonColor.rgb,half3(1,1,1));
                half3 lighting = signal > .0001 ? max(ambient,half3(.025,.03,.035)) + (_LC_SunColor.rgb + _LC_MoonColor.rgb) * .65
                    : max(SampleSH(half3(0,1,0)),half3(.18,.2,.22)) + GetMainLight().color * .65;
                half3 color = _Tint.rgb * input.color.rgb * lighting;
                // Keep a soft core when phase noise is dark; a noise cutout erased small spray
                // almost entirely at the strategy camera's normal inspection distance.
                half alpha = input.color.a * _Tint.a * soft * lerp(.4h,1.0h,smoothstep(.08,.72,noise));
                half fog = ComputeFogFactorZ0ToFar(max(-input.viewZ - _ProjectionParams.y,0));
                return half4(MixFog(color,fog),alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
