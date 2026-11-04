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
        _MoonAngularDiameter ("Moon Angular Diameter (0 = legacy size)", Range(0, 6)) = 0
        _MoonPhaseRotation ("Moon Phase Rotation", Range(-180, 180)) = 0

        _SunVisibility ("Sun Visibility", Range(0, 1)) = 1
        _MoonVisibility ("Moon Visibility", Range(0, 1)) = 0
        _StarVisibility ("Star Visibility", Range(0, 1)) = 0
        _TwilightStrength ("Twilight Strength", Range(0, 1)) = 0
        _Daylight ("Daylight", Range(0, 1)) = 1

        _CloudStrength ("Cloud Strength", Range(0, 1)) = 0.24
        _CloudSpeed ("Cloud Speed", Range(0, 0.1)) = 0.010
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = .43
        _HighCloudCoverage ("High Cloud Coverage", Range(0, 1)) = .22
        _CloudScale ("Cloud Scale", Range(.2, 3)) = 1
        _CloudAltitude ("Cloud Altitude", Range(.15, 2)) = .65
        [Enum(Puffy,0,Stretched,1,HighThin,2,Overcast,3)] _CloudShape ("Cloud Shape", Float) = 0
        _CloudQuality ("Cloud Quality (0-3)", Float) = 1
        _CloudDrift ("Cloud Drift (shared wind)", Vector) = (0,0,0,0)
        [HideInInspector] _CloudWindDriven ("External Wind", Float) = 0
        _CloudLightColor ("Cloud Lit Side", Color) = (.97,.96,.89,1)
        _CloudShadeColor ("Cloud Shaded Side", Color) = (.52,.64,.71,1)
        [HideInInspector] _StormCloudMass ("Distant Storm Mass", Range(0,1)) = 0
        _StormCloudBase ("Storm Bank Lower Sky Elevation", Range(0.01,0.4)) = .065
        _StormCloudReach ("Storm Bank Upper Sky Elevation", Range(0.3,1)) = .78
        _StormCloudDensity ("Storm Bank Density", Range(0,1)) = .84
        [HideInInspector] _StormPeakCloudMass ("Peak Cloud Mass", Range(0,1)) = 0
        _StormPeakCloudLower ("Peak Clouds Lower Elevation", Range(-.1,.4)) = .005
        _StormPeakCloudUpper ("Peak Clouds Upper Elevation", Range(.25,1)) = .68
        _StormPeakCloudDensity ("Peak Clouds Density", Range(0,1)) = .89
        _StormPeakCloudSoftness ("Peak Clouds Softness", Range(.01,.3)) = .09
        _MoonPhase ("Moon Phase (0 new, .5 full)", Range(0, 1)) = .5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "SKYBOX"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LC_CloudNoise.hlsl"
            #include "LC_StormCloudField.hlsl"

            // Owned by RainStormController; always reset on disable.
            float _LC_StormFlash;
            float4 _LC_StormFlashDirection;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 worldDir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
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
                float _MoonAngularDiameter, _MoonPhaseRotation;
                float _SunVisibility;
                float _MoonVisibility;
                float _StarVisibility;
                float _TwilightStrength;
                float _Daylight;
                float _CloudStrength;
                float _CloudSpeed;
                float _CloudCoverage, _HighCloudCoverage, _CloudScale, _CloudAltitude;
                float _CloudShape, _CloudQuality, _MoonPhase, _CloudWindDriven;
                float4 _CloudDrift, _CloudLightColor, _CloudShadeColor;
                float _StormCloudMass, _StormCloudBase, _StormCloudReach, _StormCloudDensity;
                float _StormPeakCloudMass, _StormPeakCloudLower, _StormPeakCloudUpper;
                float _StormPeakCloudDensity, _StormPeakCloudSoftness;
            CBUFFER_END

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = TransformObjectToHClip(v.vertex.xyz);
                // Preserve the skybox direction without object translation or
                // vertex normalization; normalize after interpolation as before.
                o.worldDir = TransformObjectToWorldDir(v.vertex.xyz, false);
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
                const float skyPI = 3.14159265;

                float2 uv;
                uv.x = atan2(dir.z, dir.x) / (2.0 * skyPI) + 0.5;
                uv.y = asin(clamp(dir.y, -1.0, 1.0)) / skyPI + 0.5;

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

            float4 CloudLayer(float3 dir, float3 sunDir)
            {
                // Curved distant layer: bounded at the horizon, independent of camera zoom.
                float2 p = dir.xz / max(.14, dir.y + .22) * _CloudScale * _CloudAltitude * 5.8;
                float2 stretch = _CloudShape > .5 && _CloudShape < 2.5 ? float2(.30, 1.7) : float2(1, 1);
                float2 drift = lerp(_Time.y * _CloudSpeed * float2(.7, .3), _CloudDrift.xy, _CloudWindDriven);
                p = p * stretch - drift;
                float coverage = _CloudShape > 2.5 ? max(.84, _CloudCoverage) : _CloudCoverage;
                float broad;
                float body = LCSkyCloudBody(p, _CloudQuality, broad);
                // Preserve both coverage endpoints, while giving ordinary fair
                // weather open sky between cloud clusters. Overcast still fills in.
                float threshold = lerp(1.03, -.18, pow(saturate(coverage), 1.4));
                float edgeWidth = max(.055, min(.16, fwidth(body) * 1.5));
                float alpha = smoothstep(threshold, threshold + edgeWidth, body);
                float lightSide = .56;
                if (_CloudQuality > .5)
                {
                    // Toward zenith corresponds to the upper side of the
                    // distant cloud layer. One broad-noise tap suggests round
                    // lit tops and shaded undersides without ray marching.
                    float2 radial = normalize(dir.xz * stretch + float2(.0001, .0001));
                    float above = LCCloudNoise(p - radial * .28);
                    lightSide = saturate(.53 + (broad - above) * 4.0);
                }
                float3 shade = lerp(_CloudShadeColor.rgb, _CloudLightColor.rgb, saturate(lightSide));
                shade = lerp(lerp(_ZenithColor.rgb, shade, _Daylight), shade, _CloudWindDriven);
                float silver = pow(saturate(dot(dir, sunDir)), 14) * (1 - alpha) * alpha * 3.5;
                shade += _SunColor.rgb * silver * _SunVisibility * .10;
                float sunsetRim = pow(saturate(dot(normalize(float3(dir.x, .15, dir.z)),
                    normalize(float3(sunDir.x, .15, sunDir.z)))), 5) * _TwilightStrength;
                shade = lerp(shade, _SunColor.rgb * .8, sunsetRim * .5);
                if (_CloudQuality > .5 || _CloudShape > 1.5 && _CloudShape < 2.5)
                {
                    // Drift has the same 256-cell period on both layers, including negative wind.
                    float high = LCCloudMask(LCCloudBody((p + drift) * float2(.45, 3.7) + 47 - drift, 0), _HighCloudCoverage);
                    alpha = 1 - (1 - alpha) * (1 - high * .16);
                }
                if (_CloudShape > 1.5 && _CloudShape < 2.5) alpha *= .42;
                alpha *= smoothstep(-.015, .14, dir.y) * _CloudStrength;
                return float4(shade, alpha);
            }

            // Art-directed storm bank: a distant sky layer, NOT fog/meshes around the
            // player camera. It reads as large billowing clouds over the far mountains
            // while remaining safely behind all gameplay geometry.
            // First dense 3D-look storm bank. Unlike legacy planar UV clouds,
            // this samples direction-space noise: scalloped outlines survive when
            // the strategy camera tilts toward distant mountains.
            float4 StormCloudBank(float3 dir, float3 sunDir)
            {
                float2 drift = lerp(_Time.y * _CloudSpeed * float2(.7,.3),
                    _CloudDrift.xy, _CloudWindDriven);
                float3 p = dir * (5.1 * _CloudScale) +
                    float3(drift.x * .28, .65, drift.y * .28);
                float broad, fine;
                float body = LCStormField(p, _CloudQuality, broad, fine);

                // Each column has a different top/bottom, rather than one
                // visible horizontal stripe across the horizon.
                float lower = _StormCloudBase +
                    (broad - .5) * .23 + (fine - .5) * .095;
                float upper = _StormCloudReach + (broad - .5) * .34;
                float lowerFade = smoothstep(lower - .095, lower + .070, dir.y);
                float upperFade = 1 - smoothstep(upper - .16, upper + .095, dir.y);
                float density = smoothstep(lerp(.67,.35,_StormCloudDensity),
                    lerp(.83,.51,_StormCloudDensity), body);
                float coverage = saturate(density * lowerFade * upperFade *
                    _StormCloudMass);

                // Three tonal values fake depth/self-shadow. The upper billows
                // receive cooler highlights, while their bellies stay dark.
                float belly = 1 - smoothstep(lower + .02, upper * .80, dir.y);
                float detailLight = saturate(.13 + fine * .55 +
                    (broad - fine) * .75 + (1 - belly) * .27);
                if (_CloudQuality > 1.5)
                {
                    // Cheap two-octave fake self-shadow from an offset sample:
                    // crests catch light while recessed billows stay dark.
                    float bs, fs;
                    float shadow = LCStormField(p + sunDir * .48,
                        0, bs, fs);
                    detailLight = saturate(detailLight +
                        (body - shadow) * 2.0);
                }
                float3 bottom = _CloudShadeColor.rgb * .68;
                float3 illuminated = _CloudLightColor.rgb * 1.18;
                float3 color = lerp(bottom, illuminated, detailLight);
                // Dim creases and give a little painterly ridge contrast.
                color *= lerp(.77, 1.12, broad);

                float flash = LCStormFlashHalo(dir,
                    _LC_StormFlashDirection, _LC_StormFlash);
                color = lerp(color, float3(.76,.85,1.0), flash * .91);
                return float4(color, coverage);
            }

            // Second bank: compact, rounded rolling cloud heads at peak height.
            // Built from differently scaled direction-space noise, so it has
            // independent silhouettes instead of repeating the main storm strip.
            float4 StormPeakCloudLayer(float3 dir)
            {
                float2 drift = lerp(_Time.y * _CloudSpeed * float2(.54,.30),
                    _CloudDrift.xy, _CloudWindDriven);
                float3 p = dir * (8.0 * _CloudScale) +
                    float3(drift.x * .38 + 11.7, 3.1, drift.y * .38 + 28.9);
                float broad, fine;
                float billow = LCStormField(p, _CloudQuality, broad, fine);

                float raggedLower = _StormPeakCloudLower +
                    (broad - .5) * .19 + (fine - .5) * .065;
                float raggedTop = _StormPeakCloudUpper +
                    (broad - .5) * .24;
                float low = smoothstep(
                    raggedLower - _StormPeakCloudSoftness,
                    raggedLower + _StormPeakCloudSoftness * .68, dir.y);
                float high = 1 - smoothstep(
                    raggedTop - _StormPeakCloudSoftness * 1.6,
                    raggedTop + _StormPeakCloudSoftness, dir.y);
                float threshold = lerp(.66,.35,_StormPeakCloudDensity);
                float density = smoothstep(threshold, threshold + .16, billow);
                float coverage = saturate(density * low * high *
                    _StormPeakCloudMass);
                float underside = 1 - smoothstep(raggedLower + .02,
                    raggedTop * .70, dir.y);
                float lit = saturate(.19 + fine * .52 +
                    (broad - fine) * .87 + (1 - underside) * .23);
                if (_CloudQuality > 1.5)
                {
                    float bs, fs;
                    float recessed = LCStormField(p + float3(.27,.49,.15),
                        0, bs, fs);
                    lit = saturate(lit + (billow - recessed) * 2.1);
                }
                float3 shadow = _CloudShadeColor.rgb * .61;
                float3 highlights = _CloudLightColor.rgb * .97;
                float3 color = lerp(shadow, highlights, lit);
                color *= lerp(.83, 1.14, broad);

                float flash = LCStormFlashHalo(dir,
                    _LC_StormFlashDirection, _LC_StormFlash);
                color = lerp(color, float3(.79,.88,1.0), flash * .95);
                return float4(color, coverage);
            }

            float LegacyCloudSignal(float3 dir)
            {
                float time = _Time.y * _CloudSpeed;
                return sin(dot(dir, float3(17.3, 9.1, 13.7)) * 2.4 + time * 2.1) * .50
                    + sin(dot(dir, float3(-11.2, 18.7, 7.4)) * 3.7 - time * 1.5) * .32
                    + sin(dot(dir, float3(25.6, -5.8, 16.9)) * 1.8 + time * .9) * .18;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
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

                // Preserve the legacy material's frame unless angular authoring is enabled.
                // The authored disk also has a finite frame when an orbit passes through zenith.
                float3 moonReference = float3(0, 1, .001);
                if (_MoonAngularDiameter > 0)
                    moonReference = abs(moonDir.y) > .9999 ? float3(0, 0, 1) : float3(0, 1, 0);
                float3 moonRight = normalize(cross(moonReference, moonDir));
                float3 moonUp = cross(moonDir, moonRight);
                float radius = max(.0001, sqrt(2 * _MoonSize));
                if (_MoonAngularDiameter > 0)
                    radius = sin(radians(clamp(_MoonAngularDiameter, .1, 6)) * .5);
                float2 moonUV = float2(dot(dir, moonRight), dot(dir, moonUp)) / radius;
                if (_MoonAngularDiameter > 0)
                {
                    float radialDistance = length(moonUV);
                    float edgeWidth = clamp(fwidth(radialDistance), .002, .35);
                    moonDisk = (1 - smoothstep(1 - edgeWidth, 1 + edgeWidth, radialDistance))
                        * step(0, dot(dir, moonDir));
                }
                float moonOcclusion = moonDisk;
                float phaseRotation = radians(_MoonPhaseRotation);
                moonUV = float2(moonUV.x * cos(phaseRotation) - moonUV.y * sin(phaseRotation),
                    moonUV.x * sin(phaseRotation) + moonUV.y * cos(phaseRotation));
                float3 moonNormal = float3(moonUV, sqrt(saturate(1 - dot(moonUV, moonUV))));
                float phaseAngle = _MoonPhase * 6.2831853;
                float lit = dot(moonNormal, float3(sin(phaseAngle), 0, -cos(phaseAngle)));
                float terminatorWidth = _MoonAngularDiameter > 0 ? clamp(fwidth(lit), .015, .25) : .035;
                moonDisk *= smoothstep(-terminatorWidth, terminatorWidth, lit);

                float moonGlow =
                    pow(moonDot, 520.0) * 0.055;
                if (_MoonAngularDiameter > 0)
                {
                    // A halo measured in moon radii cannot wash out a broad band of the night sky.
                    float distanceSquared = dot(dir - moonDir, dir - moonDir) / (radius * radius);
                    float phaseBrightness = .5 - .5 * cos(phaseAngle);
                    moonDisk *= saturate(phaseBrightness * 1000);
                    moonGlow = (.035 * exp(-4 * distanceSquared) + .012 * exp(-.55 * distanceSquared))
                        * lerp(.15, 1, phaseBrightness);
                    moonDisk *= smoothstep(-.002, .002, dir.y);
                }

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
                    (_StarVisibility > .001 ? StarField(dir) : 0) *
                    starHorizonFade *
                    _StarVisibility;

                color +=
                    _StarColor.rgb *
                    stars * (1 - moonOcclusion * _MoonVisibility * moonAboveHorizon);

                if (_CloudStrength > .001 && dir.y > -.015)
                {
                    if (_CloudWindDriven > .5)
                    {
                        float4 clouds = CloudLayer(dir, sunDir);
                        // The old overcast field makes thin horizontal streaks.
                        // During thunderstorms leave it only as a faint background
                        // under the new round, layered thunderhead geometry.
                        clouds.a *= 1 - saturate(_StormCloudMass) * .86;
                        color = lerp(color, clouds.rgb, clouds.a);
                    }
                    else
                    {
                        float clouds = smoothstep(.30, .72, LegacyCloudSignal(dir))
                            * smoothstep(-.015, .30, dir.y) * _CloudStrength *
                            (1 - saturate(_StormCloudMass) * .86);
                        float3 dayCloud = lerp(_HorizonColor.rgb, float3(1, 1, 1), .62);
                        float3 nightCloud = lerp(_HorizonColor.rgb, _ZenithColor.rgb, .55);
                        color = lerp(color, lerp(nightCloud, dayCloud, _Daylight), clouds);
                    }
                }

                if (_StormCloudMass > .001 && dir.y > 0)
                {
                    float4 bank = StormCloudBank(dir, sunDir);
                    color = lerp(color, bank.rgb, bank.a);
                }
                if (_StormPeakCloudMass > .001 && dir.y > -.02)
                {
                    float4 peaks = StormPeakCloudLayer(dir);
                    color = lerp(color, peaks.rgb, peaks.a);
                }

                // Directional cloud illumination from the existing distant
                // lightning; no full-screen white flash obscuring the camera.
                float3 flashDir = normalize(_LC_StormFlashDirection.xyz + float3(.00001,.00001,.00001));
                float localFlash = pow(saturate(dot(dir, flashDir)), 14);
                float cloudFlash = saturate(_LC_StormFlash) *
                    (.045 * _StormCloudMass + .76 * localFlash);
                color = lerp(color, float3(.69, .80, .98), saturate(cloudFlash));

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
