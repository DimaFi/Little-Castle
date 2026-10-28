Shader "Little Castle/Foliage/LC Foliage"
{
    Properties
    {
        _LightResponse ("Continuous Diffuse", Range(0,1)) = 0
        _MainTex ("Base Color + Alpha", 2D) = "white" {}
        _OpacityMap ("Opacity Map", 2D) = "white" {}
        _OpacityMapStrength ("Separate Opacity Strength", Range(0,1)) = 0
        _Color ("Tint", Color) = (1,1,1,1)
        _SnowAcceptance ("Environment Snow Acceptance", Range(0,1)) = 1
        _TextureDetail ("Texture Detail Strength", Range(0,1)) = 1
        _CanopyNormalBlend ("Canopy Volume Normal Blend", Range(0,1)) = 0
        _CanopyCenter ("Local Canopy Center", Vector) = (0,4.6,0,0)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.45

        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 0.65

        _OcclusionMap ("Ambient Occlusion", 2D) = "white" {}
        _OcclusionStrength ("AO Strength", Range(0,1)) = 0.65

        _TransmissionColor ("Sun Transmission", Color) = (0.68,0.92,0.30,1)
        _TransmissionStrength ("Transmission Strength", Range(0,2)) = 0.72
        _TransmissionPower ("Transmission Size", Range(0.5,8)) = 2.3

        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.92
        _ShadowTintStrength ("Shadow Tint", Range(0,1)) = 0.42
        _LightWrap ("Light Wrap", Range(0,0.8)) = 0.34
        _ShadowSoftness ("Light Softness", Range(0.03,0.8)) = 0.42

        _ColorVariation ("Per Object Variation", Range(0,0.25)) = 0.055

        _WindAmplitude ("Wind Amplitude", Range(0,1)) = 0.22
        _WindSecondary ("Secondary Motion", Range(0,1)) = 0.28
        _CrownSway ("Crown Sway", Range(0,1)) = 0.42
        _VertexWave ("Vertex Wave", Range(0,1)) = 0.46
        _LeafFlutter ("Leaf Flutter", Range(0,1)) = 0.16
        _FlutterScale ("Flutter Spatial Scale", Range(0.1,8)) = 2.4
        _FlutterSpeed ("Flutter Speed", Range(0.1,8)) = 2.7
        _UseHeightWindMask ("Use Automatic Height Wind Mask", Range(0,1)) = 0
        _WindAnchorHeight ("Wind Anchor Height", Float) = 0
        _WindHeightRange ("Wind Height Range", Float) = 5
        _UseVertexWindMask ("Use Vertex Color R Wind Mask", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        LOD 240
        Cull Off

        HLSLINCLUDE
        #include "LC_FoliageURP.hlsl"
        ENDHLSL

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LC_SPATIAL_INDIRECT
            #pragma target 4.5 _LC_SPATIAL_INDIRECT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertShadow
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            ENDHLSL
        }

        // Match animated cutouts when the renderer requests a normals prepass (e.g. SSAO).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment fragDepthNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    Fallback Off
}
