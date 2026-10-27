Shader "Little Castle/Water/LC Water"
{
    Properties
    {
        _ShallowColor ("Shallow Water", Color) = (.12,.40,.45,1)
        _DeepColor ("Deep Water", Color) = (.035,.25,.38,1)
        _FoamColor ("Shore Foam", Color) = (.73,.90,.88,1)
        _DetailTex ("Painted Water Detail", 2D) = "gray" {}
        _WaveScale ("World Wave Scale", Float) = .16
        _Speed ("Water Motion", Float) = .3
        _FoamStrength ("Foam Strength", Range(0,1)) = .3
        _CurrentHighlights ("Current Streaks", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Name "WaterForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../Surface/LC_GroundPalette.cginc"
            TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor;
                float _WaveScale, _Speed, _FoamStrength, _CurrentHighlights;
            CBUFFER_END
            half4 _LC_SunDirection, _LC_SunColor, _LC_AmbientSkyColor;
            struct Attributes { float4 vertex:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 pos:SV_POSITION; float3 world:TEXCOORD0; half depth:TEXCOORD1; float fog:TEXCOORD2; float2 flowUV:TEXCOORD3; half shore:TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(v.vertex.xyz); o.pos=TransformWorldToHClip(o.world);
                o.depth=v.color.r; o.shore=v.color.g; o.flowUV=v.uv; o.fog=ComputeFogFactor(o.pos.z); return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.flowUV; float t=_Time.y*_Speed;
                float2 uv=p*_WaveScale-float2(t*.12,0);
                half detail=dot(SAMPLE_TEXTURE2D(_DetailTex,sampler_DetailTex,uv).rgb,half3(.2126,.7152,.0722));
                half ripple=sin(p.x*1.25+p.y*.8+t*2.8)*sin(p.y*1.65-p.x*.4-t*1.7);
                half3 n=normalize(half3(ripple*.035,1,cos(p.y*1.5+t)*.045));
                half3 view=normalize(_WorldSpaceCameraPos-i.world);
                half fresnel=pow(1-saturate(dot(n,view)),4);
                half3 col=lerp(_ShallowColor.rgb,_DeepColor.rgb,smoothstep(0,.75,i.depth));
                col*=.96h+(detail-.4h)*.22h+ripple*.018h;
                half foam=max(1-smoothstep(.025h,.16h,i.depth),i.shore)*(.35h+.65h*LCGroundNoise(p*.85+float2(t*.4,t*.1)));
                col=lerp(col,_FoamColor.rgb,saturate(foam*_FoamStrength));
                half streak=smoothstep(.49h,.70h,detail)*(.25h+.75h*LCGroundNoise(p*.23+float2(t*.1,0)));
                col=lerp(col,_FoamColor.rgb,streak*_CurrentHighlights*.45h);
                half spec=pow(saturate(dot(n,normalize(normalize(_LC_SunDirection.xyz)+view))),90)*.18h;
                col=col*(.65h+_LC_AmbientSkyColor.rgb*.35h+_LC_SunColor.rgb*.2h)+spec*_LC_SunColor.rgb;
                col=lerp(col,half3(.48,.72,.79),fresnel*.23h);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord=ComputeScreenPos(TransformWorldToHClip(i.world));
                #else
                float4 shadowCoord=TransformWorldToShadowCoord(i.world);
                #endif
                Light mainLight=GetMainLight(shadowCoord,i.world,half4(1,1,1,1));
                col*=lerp(.58h,1,mainLight.shadowAttenuation);
                return half4(MixFog(col,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Unlit/DepthOnly"
    }
    Fallback Off
}
