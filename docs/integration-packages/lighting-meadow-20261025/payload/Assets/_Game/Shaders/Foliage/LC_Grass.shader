Shader "Little Castle/Foliage/LC Grass"
{
    Properties
    {
        _MainTex ("Grass Alpha", 2D) = "white" {}
        _OpacityMap ("Opacity Map", 2D) = "white" {}
        _OpacityMapStrength ("Separate Opacity Strength", Range(0,1)) = 0
        _Color ("Middle Color", Color) = (0.38,0.64,0.24,1)
        _RootColor ("Root Color", Color) = (0.18,0.32,0.12,1)
        _TipColor ("Tip Color", Color) = (0.62,0.82,0.34,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.42
        _UseRootAnchor ("UV1 Root Anchor Fade", Range(0,1)) = 0
        _UseObjectAnchorFade ("Whole Clump Origin Fade (No UV1)", Range(0,1)) = 0
        _FadeStart ("Geometry Fade Start (m)", Float) = 40
        _FadeEnd ("Geometry Fade End (m)", Float) = 70
        [Toggle(_LC_MEADOW)] _MeadowMode ("Meadow: Coherent Wind and Density", Float) = 0
        _DensityNear ("Meadow Full Density Distance", Float) = 16
        _DensityFar ("Meadow Minimum Density Distance", Float) = 90
        _DensityFloor ("Meadow Distant Density", Range(0,1)) = .12
        _MeadowSoftness ("Distant Meadow Contrast Reduction", Range(0,1)) = 0
        _MeadowSoftStart ("Meadow Softening Start (m)", Float) = 14
        _MeadowSoftEnd ("Meadow Softening End (m)", Float) = 65
        _MeadowRootOcclusion ("Meadow Root Ambient Occlusion", Range(0,.6)) = 0
        _InteractionStrength ("Local Interaction Strength", Range(0,1)) = 0
        _InteractionDistance ("Local Interaction Distance", Float) = 28
        [Toggle(_LC_SURFACE_PAINT)] _UseSurfacePaint ("Authored / Player Road Mask", Float) = 0
        _SurfacePaint ("Derived Road Coverage", 2D) = "black" {}
        _SurfacePaintBounds ("Road Mask Origin XZ / Inverse Size", Vector) = (0,0,.01,.01)

        _ColorVariation ("Per Clump Variation", Range(0,0.3)) = 0.08
        _MeadowPaletteStrength ("Shared Meadow Palette", Range(0,1)) = 0
        _DryColor ("Dry Meadow Middle Color", Color) = (.43,.46,.24,1)
        _UpNormalBias ("Upward Normal Bias", Range(0,1)) = 0.72

        _TransmissionColor ("Backlight Color", Color) = (0.70,0.92,0.30,1)
        _TransmissionStrength ("Backlight Strength", Range(0,2)) = 0.85

        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.94
        _ShadowTintStrength ("Shadow Tint", Range(0,1)) = 0.38

        _WindAmplitude ("Wind Amplitude", Range(0,1)) = 0.34
        _WindBend ("Tip Bend", Range(0,2)) = 1.0
        _GustStrength ("Gust Strength", Range(0,1)) = 0.38
        _GustScale ("Gust Spatial Scale", Range(0.001,0.08)) = 0.018
        _GustSpeed ("Gust Speed", Range(0.05,2)) = 0.28
        _MicroFlutter ("Tip Micro Flutter", Range(0,0.5)) = 0.10
        _MicroFlutterScale ("Micro Flutter Scale", Range(0.1,8)) = 2.2
        _MicroFlutterSpeed ("Micro Flutter Speed", Range(0.1,8)) = 3.2
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        LOD 180
        Cull Off

        HLSLINCLUDE
        #include "LC_GrassURP.hlsl"
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
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ _LC_MEADOW
            #pragma shader_feature_local _ _LC_SURFACE_PAINT
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
            #pragma shader_feature_local _ _LC_MEADOW
            #pragma shader_feature_local _ _LC_SURFACE_PAINT
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
            #pragma shader_feature_local _ _LC_MEADOW
            #pragma shader_feature_local _ _LC_SURFACE_PAINT
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
            #pragma shader_feature_local _ _LC_MEADOW
            #pragma shader_feature_local _ _LC_SURFACE_PAINT
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    Fallback Off
}
