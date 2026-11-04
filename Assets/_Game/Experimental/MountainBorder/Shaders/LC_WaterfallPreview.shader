Shader "Little Castle/Experimental/Waterfall Preview"
{
    Properties
    {
        _WaterColor ("Water", Color) = (0.12,0.42,0.49,1)
        _FoamColor ("Foam", Color) = (0.82,0.92,0.95,1)
        _FlowSpeed ("Downward Flow Speed", Float) = 0.5
        _Opacity ("Opacity", Range(0,1)) = 0.88
        [HideInInspector] _PreviewTime ("Editor Preview Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "WaterfallPreview"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _WaterColor;
                float4 _FoamColor;
                float _FlowSpeed;
                float _Opacity;
                float _PreviewTime;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float viewZ:TEXCOORD1; };
            V vert(A input)
            {
                V o;
                VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS=p.positionCS; o.viewZ=p.positionVS.z; o.uv=input.uv;
                return o;
            }
            half4 frag(V input):SV_Target
            {
                float2 uv=input.uv;
                // UV.y=1 at the lip, 0 at landing. Positive time moves features down.
                float flow=uv.y*7+(_Time.y+_PreviewTime)*_FlowSpeed;
                float stripe=pow(saturate(sin(uv.x*71+sin(flow*1.7)*0.65)*0.5+0.5),5);
                float pulse=0.6+0.4*sin(flow*6.283+sin(uv.x*17)*2);
                float lip=smoothstep(0.87,1,uv.y);
                float landing=1-smoothstep(0,0.18,uv.y);
                float foam=saturate(stripe*pulse*0.46+lip*0.55+landing*0.7);
                half3 color=lerp(_WaterColor.rgb,_FoamColor.rgb,foam);
                half edge=smoothstep(0,0.055,uv.x)*(1-smoothstep(0.945,1,uv.x));
                half fog=ComputeFogFactorZ0ToFar(max(-input.viewZ-_ProjectionParams.y,0));
                return half4(MixFog(color,fog),saturate(_Opacity)*edge);
            }
            ENDHLSL
        }
    }
    // Visual v0: no refraction, depth foam, collision, splash particles or hydraulics.
    Fallback Off
}
