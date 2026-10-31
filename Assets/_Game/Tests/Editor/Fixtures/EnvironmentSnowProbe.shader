Shader "Hidden/LittleCastle/Tests/Environment Snow Probe"
{
    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../../Shaders/Surface/LC_EnvironmentAppearance.hlsl"
            struct Varyings {float4 positionCS: SV_POSITION; float2 uv: TEXCOORD0;};
            Varyings Vert(uint vertexID: SV_VertexID)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }
            half4 Frag(Varyings i): SV_Target
            {
                float2 p = lerp(float2(-24, -24), float2(24, 24), i.uv);
                half coverage = LCEnvironmentSnow(float3(p.x, 0, p.y), half3(0, 1, 0), 1);
                return half4(coverage, coverage, coverage, 1);
            }
            ENDHLSL
        }
    }
}
