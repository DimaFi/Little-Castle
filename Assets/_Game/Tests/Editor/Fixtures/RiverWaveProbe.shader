Shader "Hidden/Little Castle/Tests/River Wave Probe"
{
    Properties { _Point("World XZ and seconds",Vector)=(0,0,0,0) }
    SubShader { Pass { ZTest Always ZWrite Off Cull Off
        CGPROGRAM
        #pragma vertex vert_img
        #pragma fragment frag
        #include "UnityCG.cginc"
        #include "Assets/_Game/Experimental/MountainBorder/Shaders/LC_RiverWaves.hlsl"
        float4 _Point;
        float4 frag(v2f_img input):SV_Target{return float4(LCRiverWave(_Point.xy,_Point.z),0,0,1);}
        ENDCG
    }}
}
