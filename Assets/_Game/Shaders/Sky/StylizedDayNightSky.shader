Shader "Little Castle/Sky/Stylized Day Night"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.22, 0.50, 0.82, 1)
        _HorizonColor ("Horizon", Color) = (0.78, 0.88, 0.96, 1)
        _LowerSkyColor ("Lower Sky", Color) = (0.50, 0.62, 0.72, 1)
        _TwilightColor ("Twilight", Color) = (1.0, 0.42, 0.18, 1)

        [HDR] _SunColor ("Sun", Color) = (1.55, 1.12, 0.68, 1)
        [HDR] _MoonColor ("Moon", Color) = (0.75, 0.86, 1.12, 1)
        _StarColor ("Stars", Color) = (0.82, 0.90, 1.0, 1)

        _SunDirection ("Sun Direction", Vector) = (0, 1, 0, 0)
        _MoonDirection ("Moon Direction", Vector) = (0, 1, 0, 0)

        _SunSize ("Sun Size", Range(0.000003, 0.00030)) = 0.000045
        _MoonSize ("Moon Size", Range(0.000003, 0.00030)) = 0.000060

        _SunVisibility ("Sun Visibility", Range(0, 1)) = 1
        _MoonVisibility ("Moon Visibility", Range(0, 1)) = 0
        _StarVisibility ("Star Visibility", Range(0, 1)) = 0
        _TwilightStrength ("Twilight Strength", Range(0, 1)) = 0
        _Daylight ("Daylight", Range(0, 1)) = 1

        _CloudStrength ("Cloud Strength", Range(0, 1)) = 0.24
        _CloudSpeed ("Cloud Speed", Range(0, 0.1)) = 0.010
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 worldDir : TEXCOORD0;
            };

            float4 _ZenithColor;
            float4 _HorizonColor;
            float4 _LowerSkyColor;
            float4 _TwilightColor;
            float4 _SunColor;
            float4 _MoonColor;
            float4 _StarColor;

            float4 _SunDirection;
            float4 _MoonDirection;

            float _SunSize;
            float _MoonSize;
            float _SunVisibility;
            float _MoonVisibility;
            float _StarVisibility;
            float _TwilightStrength;
            float _Daylight;
            float _CloudStrength;
            float _CloudSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.worldDir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float StarField(float3 dir)
            {
                const float PI = 3.14159265;

                float2 uv;
                uv.x = atan2(dir.z, dir.x) / (2.0 * PI) + 0.5;
                uv.y = asin(clamp(dir.y, -1.0, 1.0)) / PI + 0.5;

                float2 grid = uv * float2(360.0, 180.0);
                float2 cell = floor(grid);
                float2 local = frac(grid);

                float seed = Hash21(cell);
                float2 starPosition = float2(
                    Hash21(cell + 17.13),
                    Hash21(cell + 83.71));

                float radius = lerp(
                    0.018,
                    0.055,
                    Hash21(cell + 31.91));

                float star =
                    1.0 -
                    smoothstep(
                        radius * 0.25,
                        radius,
                        distance(local, starPosition));

                star *= step(0.988, seed);

                return star;
            }

            float CloudSignal(float3 dir)
            {
                float time = _Time.y * _CloudSpeed;

                float a =
                    sin(dot(dir, float3(17.3, 9.1, 13.7)) * 2.4 + time * 2.1);

                float b =
                    sin(dot(dir, float3(-11.2, 18.7, 7.4)) * 3.7 - time * 1.5);

                float c =
                    sin(dot(dir, float3(25.6, -5.8, 16.9)) * 1.8 + time * 0.9);

                return
                    a * 0.50 +
                    b * 0.32 +
                    c * 0.18;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.worldDir);

                // Most of this sky is seen near the horizon in a strategy
                // camera, so keep a visible vertical gradient there.
                float skyHeight =
                    pow(
                        saturate((dir.y + 0.025) / 0.975),
                        0.46);

                float3 color =
                    lerp(
                        _HorizonColor.rgb,
                        _ZenithColor.rgb,
                        skyHeight);

                float belowHorizon =
                    saturate(-dir.y * 5.0);

                color =
                    lerp(
                        color,
                        _LowerSkyColor.rgb,
                        belowHorizon);

                float horizonBand =
                    pow(
                        saturate(1.0 - abs(dir.y) * 2.2),
                        3.0);

                color +=
                    _HorizonColor.rgb *
                    horizonBand *
                    0.035;

                float3 sunDir =
                    normalize(_SunDirection.xyz);

                float sunDot =
                    saturate(dot(dir, sunDir));

                float sunAboveHorizon =
                    smoothstep(
                        -0.035,
                        0.015,
                        sunDir.y);

                float sunDisk =
                    smoothstep(
                        1.0 - _SunSize,
                        1.0 - _SunSize * 0.12,
                        sunDot);

                float sunGlow =
                    pow(sunDot, 420.0) * 0.08 +
                    pow(sunDot, 1450.0) * 0.12;

                float sunVisibility =
                    _SunVisibility *
                    sunAboveHorizon;

                color +=
                    _SunColor.rgb *
                    (sunDisk + sunGlow) *
                    sunVisibility;

                // Warm sunset/sunrise is local to the sun side of the
                // horizon instead of tinting the whole sky pink.
                float2 flatDir =
                    normalize(dir.xz + float2(0.00001, 0.00001));

                float2 flatSun =
                    normalize(sunDir.xz + float2(0.00001, 0.00001));

                float twilightAlignment =
                    pow(
                        saturate(dot(flatDir, flatSun)),
                        4.0);

                float twilightBand =
                    horizonBand *
                    twilightAlignment *
                    _TwilightStrength;

                color =
                    lerp(
                        color,
                        _TwilightColor.rgb,
                        saturate(twilightBand * 0.72));

                color +=
                    _TwilightColor.rgb *
                    twilightBand *
                    0.08;

                float3 moonDir =
                    normalize(_MoonDirection.xyz);

                float moonDot =
                    saturate(dot(dir, moonDir));

                float moonAboveHorizon =
                    smoothstep(
                        -0.025,
                        0.015,
                        moonDir.y);

                float moonDisk =
                    smoothstep(
                        1.0 - _MoonSize,
                        1.0 - _MoonSize * 0.14,
                        moonDot);

                float moonGlow =
                    pow(moonDot, 520.0) * 0.055;

                color +=
                    _MoonColor.rgb *
                    (moonDisk + moonGlow) *
                    _MoonVisibility *
                    moonAboveHorizon;

                float starHorizonFade =
                    smoothstep(
                        -0.01,
                        0.10,
                        dir.y);

                float stars =
                    StarField(dir) *
                    starHorizonFade *
                    _StarVisibility;

                color +=
                    _StarColor.rgb *
                    stars;

                // Very cheap, textureless high cloud pattern. It is kept
                // subtle because settlement readability is more important.
                float cloudSignal =
                    CloudSignal(dir);

                float cloudAltitude =
                    smoothstep(
                        -0.015,
                        0.30,
                        dir.y);

                float clouds =
                    smoothstep(
                        0.30,
                        0.72,
                        cloudSignal) *
                    cloudAltitude *
                    _CloudStrength;

                float3 dayCloud =
                    lerp(
                        _HorizonColor.rgb,
                        float3(1.0, 1.0, 1.0),
                        0.62);

                float3 nightCloud =
                    lerp(
                        _HorizonColor.rgb,
                        _ZenithColor.rgb,
                        0.55);

                float3 cloudColor =
                    lerp(
                        nightCloud,
                        dayCloud,
                        _Daylight);

                color =
                    lerp(
                        color,
                        cloudColor,
                        clouds);

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
