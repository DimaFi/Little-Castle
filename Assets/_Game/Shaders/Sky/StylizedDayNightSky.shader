Shader "Little Castle/Sky/Stylized Day Night"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.28, 0.55, 0.84, 1)
        _HorizonColor ("Horizon", Color) = (0.78, 0.87, 0.94, 1)
        _LowerSkyColor ("Lower Sky", Color) = (0.54, 0.66, 0.72, 1)

        [HDR] _SunColor ("Sun", Color) = (1.8, 1.25, 0.72, 1)
        [HDR] _MoonColor ("Moon", Color) = (0.82, 0.9, 1.2, 1)
        _StarColor ("Stars", Color) = (0.8, 0.88, 1, 1)

        _SunDirection ("Sun Direction", Vector) = (0, 1, 0, 0)
        _MoonDirection ("Moon Direction", Vector) = (0, 1, 0, 0)

        _SunSize ("Sun Size", Range(0.0001, 0.008)) = 0.0012
        _MoonSize ("Moon Size", Range(0.0001, 0.008)) = 0.001
        _SunVisibility ("Sun Visibility", Range(0, 1)) = 1
        _MoonVisibility ("Moon Visibility", Range(0, 1)) = 0
        _StarVisibility ("Star Visibility", Range(0, 1)) = 0
        _CloudStrength ("Cloud Strength", Range(0, 1)) = 0.18
        _CloudSpeed ("Cloud Speed", Range(0, 0.1)) = 0.012
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

                float2 grid = uv * float2(300.0, 150.0);
                float2 cell = floor(grid);
                float2 local = frac(grid);

                float seed = Hash21(cell);
                float2 starPos = float2(
                    Hash21(cell + 17.13),
                    Hash21(cell + 83.71));

                float radius = lerp(0.025, 0.07, Hash21(cell + 31.91));
                float star = 1.0 - smoothstep(radius * 0.35, radius, distance(local, starPos));
                star *= step(0.986, seed);

                return star;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.worldDir);

                float skyT = saturate((dir.y + 0.04) / 0.96);
                skyT = pow(skyT, 0.62);

                float3 color = lerp(
                    _HorizonColor.rgb,
                    _ZenithColor.rgb,
                    skyT);

                float below = saturate(-dir.y * 4.0);
                color = lerp(color, _LowerSkyColor.rgb, below);

                float horizonGlow = pow(saturate(1.0 - abs(dir.y)), 5.0);
                color += _HorizonColor.rgb * horizonGlow * 0.08;

                float3 sunDir = normalize(_SunDirection.xyz);
                float sunDot = saturate(dot(dir, sunDir));
                float sunDisk = smoothstep(
                    1.0 - _SunSize,
                    1.0 - _SunSize * 0.18,
                    sunDot);

                float sunGlow = pow(sunDot, 96.0) * 0.22 +
                                pow(sunDot, 384.0) * 0.28;

                color += _SunColor.rgb *
                    (sunDisk + sunGlow) *
                    _SunVisibility;

                float3 moonDir = normalize(_MoonDirection.xyz);
                float moonDot = saturate(dot(dir, moonDir));
                float moonDisk = smoothstep(
                    1.0 - _MoonSize,
                    1.0 - _MoonSize * 0.22,
                    moonDot);

                float moonGlow = pow(moonDot, 220.0) * 0.12;

                color += _MoonColor.rgb *
                    (moonDisk + moonGlow) *
                    _MoonVisibility;

                float horizonStarFade = smoothstep(0.02, 0.22, dir.y);
                float stars = StarField(dir) *
                    horizonStarFade *
                    _StarVisibility;

                color += _StarColor.rgb * stars;

                float upperSky = smoothstep(0.03, 0.5, dir.y);
                float cloudWave =
                    sin(dir.x * 17.0 + dir.z * 11.0 + _Time.y * _CloudSpeed * 5.0) *
                    sin(dir.z * 23.0 - dir.x * 7.0 - _Time.y * _CloudSpeed * 3.0);

                cloudWave += sin(
                    (dir.x + dir.z) * 31.0 +
                    _Time.y * _CloudSpeed * 2.0) * 0.45;

                float clouds = smoothstep(0.62, 1.05, cloudWave) *
                    upperSky *
                    _CloudStrength;

                float3 cloudColor = lerp(
                    _HorizonColor.rgb,
                    float3(1.0, 1.0, 1.0),
                    0.55);

                color = lerp(color, cloudColor, clouds);

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
