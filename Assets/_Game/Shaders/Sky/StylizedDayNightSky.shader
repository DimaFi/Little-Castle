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
        [HideInInspector] _RainCloudMass ("Rain Cloud Mass", Range(0,1)) = 0
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

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
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
                float _RainCloudMass, _StormCloudMass, _StormCloudBase, _StormCloudReach, _StormCloudDensity;
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

            float CloudLobe(float3 p, float3 center, float3 inverseRadius)
            {
                float3 local = (p - center) * inverseRadius;
                return 1 - dot(local, local);
            }

            // Build broad silhouettes from overlapping lobes. Noise is applied
            // later, at their edges, so it cannot turn the core into speckle.
            float CloudCluster(float3 p)
            {
                float2 cell = floor(p.xz);
                float seed = LCStormHash3(float3(cell, 19.3));
                float signX = seed < .5 ? -1 : 1;
                float2 local = frac(p.xz) - .5;
                float3 q = float3(local.x * signX, p.y, local.y);
                q.x -= (seed - .5) * .09;
                q.y -= (seed - .5) * .20;
                float broad = CloudLobe(q, float3(0, .33, 0), float3(2.21, 2.4, 2.35));
                float crown = CloudLobe(q, float3(-.10, .60, -.10), float3(3.45, 2.22, 3.14));
                float shoulder = CloudLobe(q, float3(.16, .46, .10), float3(3.4, 2.8, 3.45));
                float turret = CloudLobe(q, float3(.06, .67, .13), float3(4.5, 2.8, 4.0));
                return max(max(broad, crown), max(shoulder, turret));
            }

            float4 DayCloudVolume(float3 dir, float3 sunDir)
            {
                float fade = smoothstep(.025, .23, dir.y);
                if (fade <= .001) return 0;
                float2 drift = lerp(_Time.y * _CloudSpeed * float2(.7, .3),
                    _CloudDrift.xy, _CloudWindDriven);
                float2 ray = dir.xz / max(.48, dir.y + .48);
                float scale = clamp(_CloudScale, .65, 1.55);
                float2 footprint = ray * 1.46 * scale + float2(.43, .20) + drift * .024;
                float3 lightDir = normalize(sunDir * .65 + float3(-.15, .70, .12));
                float cut = lerp(.42, .12, saturate(_CloudCoverage));
                int steps = _CloudQuality > 1.5 ? 12 : (_CloudQuality > .5 ? 10 : 8);
                float invSteps = rcp((float)steps);
                float remaining = 1;
                float3 scattering = 0;

                [loop] for (int i = 0; i < steps; i++)
                {
                    float h = (i + .5) * invSteps;
                    float2 uv = footprint + ray * (h * .42);
                    float3 p = float3(uv.x, h, uv.y);
                    float body = CloudCluster(p);
                    if (body < cut - .07) continue;
                    float3 slope;
                    float edge = LCStormValue3(p * 11.0 + 19.0, slope);
                    float density = smoothstep(cut, cut + .23, body + (edge - .55) * .24);
                    float alpha = 1 - exp2(-density * invSteps * 12.5);
                    float occlusion = saturate(CloudCluster(p + lightDir * .27));
                    float lit = saturate(.90 - occlusion * .76 + h * .22);
                    float3 light = lerp(_CloudShadeColor.rgb * .69, _CloudLightColor.rgb, lit);
                    scattering += remaining * alpha * light;
                    remaining *= 1 - alpha;
                    if (remaining < .035) break;
                }

                float coverage = 1 - remaining;
                return float4(scattering / max(.001, coverage),
                    coverage * fade * saturate(_CloudStrength * 1.42));
            }

            // Integrate density through two decks along the view ray. The
            // near-horizontal ray is bounded to keep sampling stable at distance.
            float3 RainCloudVolume(float3 sky, float3 dir, float3 sunDir,
                float wet, float storm)
            {
                float horizon = smoothstep(-.055, .16, dir.y);
                if (horizon <= .001) return sky;

                float2 drift = lerp(_Time.y * _CloudSpeed * float2(.7, .3),
                    _CloudDrift.xy, _CloudWindDriven);
                float2 ray = dir.xz / max(.43, dir.y + .43);
                float3 lightDir = normalize(sunDir * .36 + float3(-.22, .78, .18));
                float flash = LCStormFlashHalo(dir, _LC_StormFlashDirection, _LC_StormFlash);
                float stormLook = smoothstep(.08, .86, storm);
                float3 shade = lerp(float3(.066, .089, .13), float3(.006, .013, .029), stormLook);
                float3 crest = lerp(float3(.45, .53, .63), float3(.105, .165, .265), stormLook);
                float dayScale = lerp(.23, 1.0, _Daylight);
                float3 backdrop = lerp(_HorizonColor.rgb * .58, _ZenithColor.rgb * .55,
                    smoothstep(.04, .58, dir.y));
                sky = lerp(sky, backdrop * lerp(1.0, .46, stormLook),
                    horizon * wet * lerp(.58, .86, stormLook));

                int steps = _CloudQuality > 1.5 ? 20 : (_CloudQuality > .5 ? 16 : 10);
                float invSteps = rcp((float)steps);
                float transmittance = 1.0;
                float3 scattering = 0;
                float threshold = lerp(.35, .15, saturate(.58 * wet +
                    .27 * storm * _StormCloudDensity + .15 * _StormPeakCloudMass));
                float lowBase = clamp(_StormPeakCloudLower, -.04, .12);
                float lowTop = lerp(.45, .58, saturate(_StormPeakCloudUpper));
                float highBase = clamp(_StormCloudBase + .40, .39, .57);
                float highTop = max(highBase + .25, _StormCloudReach + .14);
                float scale = clamp(_CloudScale, .65, 1.55);
                // The storm has more compact heads across the sky than rain.
                float billowScale = scale * lerp(1.0, 1.42, stormLook);

                [loop] for (int i = 0; i < steps; i++)
                {
                    float h = (i + .5) * invSteps;
                    bool upper = h >= highBase;
                    float height = upper ? (h - highBase) / (highTop - highBase) :
                        (h - lowBase) / (lowTop - lowBase);
                    if (height < 0 || height > 1) continue;

                    float2 horizontal = ray * (.57 + h * .51) * billowScale + drift * .017;
                    horizontal += upper ? float2(.37, .64) : float2(.12, .29);
                    float3 p = float3(horizontal.x, height, horizontal.y);
                    float body = CloudCluster(p);
                    if (body < threshold - .13) continue;

                    float3 detailSlope;
                    float erosion = LCStormValue3(p * float3(10.4, 6.8, 10.4) +
                        float3(11.2, 3.6, 7.4), detailSlope);
                    float edge = 1 - smoothstep(.42, .90, body);
                    float density = smoothstep(threshold, threshold +
                        clamp(_StormPeakCloudSoftness * 1.65, .10, .26),
                        body + (erosion - .56) * edge * .54);
                    density *= lerp(.72, 1.10, erosion);
                    density *= upper ? .82 : lerp(.64, 1.0, _StormPeakCloudDensity);
                    float alpha = 1 - exp2(-density * invSteps * wet * lerp(10.0, 17.0, storm));

                    float obstruction = saturate(CloudCluster(p + lightDir * .22));
                    float edgeLight = saturate(.45 - dot(detailSlope, lightDir) * .42);
                    float lit = saturate(.65 + height * .19 - obstruction * .65 +
                        (edgeLight - .5) * .36 - density * .17);
                    lit = lerp(lit, lit * lit, stormLook * .78);
                    float3 deckShade = shade * (upper ? 1.20 : 1.0);
                    float3 light = lerp(deckShade, crest, lit) * dayScale;
                    light += flash * (.15 + edgeLight * .78) * float3(.60, .69, 1.0);
                    scattering += transmittance * alpha * light;
                    transmittance *= 1 - alpha;
                    if (transmittance < .025) break;
                }

                return lerp(sky, sky * transmittance + scattering, horizon);
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

                float wet = saturate(max(_RainCloudMass, _StormCloudMass));
                float skyHeight = pow(saturate(dir.y), .53);
                float3 lowerSky = lerp(_LowerSkyColor.rgb, _HorizonColor.rgb * .82, wet);
                float3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, skyHeight);
                color = lerp(color, lowerSky, smoothstep(.0, .24, -dir.y));

                // Match the atmosphere between the silhouette and sky. This is an
                // angular veil over the sky only; terrain keeps its own fog falloff.
                float horizonVeil = 1 - smoothstep(.015, lerp(.20, .30, wet), abs(dir.y));
                float3 airlight = MixFog(lerp(_HorizonColor.rgb, lowerSky, .30),
                    ComputeFogFactorZ0ToFar(_ProjectionParams.z));

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
                    pow(saturate(1.0 - abs(dir.y) * 2.2), 3.0) *
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

                float dayVolume = smoothstep(.16, .68, _Daylight) *
                    (1 - smoothstep(.04, .48, wet)) *
                    (1 - step(.5, _CloudShape));
                if (_CloudStrength > .001 && dir.y > -.015 && wet < .995)
                {
                    if (_CloudWindDriven > .5)
                    {
                        float4 clouds = CloudLayer(dir, sunDir);
                        // The old overcast field makes thin horizontal streaks.
                        // During thunderstorms leave it only as a faint background
                        // under the new round, layered thunderhead geometry.
                        clouds.a *= (1 - wet) * (1 - dayVolume * .84);
                        color = lerp(color, clouds.rgb, clouds.a);
                    }
                    else
                    {
                        float clouds = smoothstep(.30, .72, LegacyCloudSignal(dir))
                            * smoothstep(-.015, .30, dir.y) * _CloudStrength *
                            (1 - wet) * (1 - dayVolume * .84);
                        float3 dayCloud = lerp(_HorizonColor.rgb, float3(1, 1, 1), .62);
                        float3 nightCloud = lerp(_HorizonColor.rgb, _ZenithColor.rgb, .55);
                        color = lerp(color, lerp(nightCloud, dayCloud, _Daylight), clouds);
                    }
                }

                if (dayVolume * _CloudStrength > .001)
                {
                    float4 dayClouds = DayCloudVolume(dir, sunDir);
                    color = lerp(color, dayClouds.rgb, dayClouds.a * dayVolume);
                }

                if (wet > .001)
                    color = RainCloudVolume(color, dir, sunDir, wet, saturate(_StormCloudMass));

                color = lerp(color, airlight, horizonVeil * lerp(.20, .43, wet));

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
