Shader "Little Castle/Weather/Optional Lens Droplets"
{
    Properties { _Opacity ("Droplet opacity", Range(0,1)) = 0.1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "RainLens"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Opacity;
            CBUFFER_END
            struct Input { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Output Vert(Input i)
            {
                Output o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 Frag(Output i) : SV_Target
            {
                // Sparse macro drops that creep down the screen; deliberately no costly refraction.
                float2 grid = i.uv * float2(16,9);
                float2 cell = floor(grid);
                float2 uv = frac(grid);
                float selectDrop = Hash(cell + 3.15);
                float speed = lerp(.075, .19, Hash(cell + 9.21));
                float phase = frac(_Time.y * speed + Hash(cell + 5.7));
                float2 position = float2(.13 + .72*Hash(cell + 6.3), 1.16-phase*1.26);
                float2 displacement = (uv-position)*float2(1.0,1.55);
                float drop = 1.0 - smoothstep(.035,.085,length(displacement));
                float tail = exp(-abs(uv.x-position.x)*70.0) *
                    smoothstep(-.04,.12,uv.y-position.y) *
                    (1.0-smoothstep(.18,.53,uv.y-position.y)) * .2;
                half a = saturate(drop+tail) * step(.84,selectDrop) * _Opacity * (.65+.35*phase);
                return half4(.80h,.88h,.97h,a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
