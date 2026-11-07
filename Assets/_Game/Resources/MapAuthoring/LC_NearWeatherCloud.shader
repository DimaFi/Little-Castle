Shader "Little Castle/Weather/Near Cloud Volume"
{
    Properties
    {
        _Weather ("Rain, storm, daylight, quality", Vector) = (0,0,1,1)
        _Seed ("Cloud variation", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Name "Local weather cloud"
            Blend One OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../Shaders/Sky/LC_StormCloudField.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Weather;
                float _Seed;
            CBUFFER_END
            float _LC_StormFlash;
            float4 _LC_StormFlashDirection;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(positionOS);
                o.positionOS = positionOS;
                return o;
            }

            float Lobe(float3 p, float3 center, float3 radius)
            {
                float3 q = (p - center) / radius;
                return 1 - dot(q, q);
            }

            float Shape(float3 p)
            {
                float variation = frac(_Seed * .618) - .5;
                p.x *= variation > 0 ? -1 : 1;
                // A broad upper bank connects staggered hanging lobes. Different
                // front/back heights keep the underside readable from an RTS camera.
                p.z += variation * p.x * .24;
                float bank = Lobe(p, float3(0,.10,0), float3(.46,.19,.38));
                float backLeft = Lobe(p, float3(-.23,.07,-.15), float3(.24,.25,.25));
                float backRight = Lobe(p, float3(.20,.11,-.12), float3(.25,.27,.26));
                float crown = Lobe(p, float3(-.06,.20 + variation * .10,.02), float3(.27,.23,.28));
                float frontLeft = Lobe(p, float3(-.27,-.04,.16), float3(.19,.22,.22));
                float hanging = Lobe(p, float3(-.04,-.10 + variation * .06,.19), float3(.22,.25,.25));
                float frontRight = Lobe(p, float3(.24,-.02,.15), float3(.22,.23,.24));
                return max(max(bank, crown), max(max(backLeft, backRight),
                    max(frontLeft, max(hanging, frontRight))));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float wet = saturate(_Weather.x);
                float storm = saturate(_Weather.y);
                float opacity = max(wet * .77, storm);
                if (opacity <= .002) return 0;

                float3 cameraOS = TransformWorldToObject(_WorldSpaceCameraPos);
                // Measure from the nearest point on the box in world metres.
                // A wide bank should fade at the same distance as a narrow one.
                float3 closestWS = TransformObjectToWorld(clamp(cameraOS, -.5, .5));
                float distanceFade = smoothstep(45.0, 125.0, distance(_WorldSpaceCameraPos, closestWS));
                if (distanceFade <= .001) return 0;

                float3 dir = normalize(i.positionOS - cameraOS);
                float3 raySign = step(0, dir) * 2 - 1;
                float3 safeDir = raySign * max(abs(dir), .0001);
                float3 distanceToExit = (raySign * .5 - i.positionOS) / safeDir;
                float lengthInside = max(0, min(distanceToExit.x, min(distanceToExit.y, distanceToExit.z)));
                int steps = _Weather.w > 1.5 ? 12 : (_Weather.w > .5 ? 9 : 6);
                float stepLength = lengthInside / steps;
                float remaining = 1;
                float3 scattering = 0;
                float day = lerp(.28, 1, saturate(_Weather.z));
                float3 dark = lerp(float3(.080,.105,.14), float3(.032,.048,.078), storm);
                float3 bright = lerp(float3(.46,.53,.62), float3(.31,.38,.51), storm);
                float3 viewDir = normalize(TransformObjectToWorld(i.positionOS) - _WorldSpaceCameraPos);
                float flash = LCStormFlashHalo(viewDir, _LC_StormFlashDirection, _LC_StormFlash);
                float3 lightDir = float3(-.32,.77,.28);

                [loop] for (int k = 0; k < steps; k++)
                {
                    float3 p = i.positionOS + dir * ((k + .5) * stepLength);
                    float body = Shape(p);
                    if (body < -.09) continue;
                    float3 noiseGradient;
                    float edgeNoise = LCStormValue3(p * float3(12, 7, 11) +
                        float3(_Seed, 9.3, 17.0) + float3(_Time.y * .013, 0, 0), noiseGradient);
                    float edge = 1 - smoothstep(.42,.85,body);
                    float density = smoothstep(.015,.53,body + (edgeNoise - .55) * .48 * edge);
                    // Preserve soft internal layers instead of filling every lobe
                    // with the same opaque density; reuse the existing noise sample.
                    density *= lerp(.68, 1.12, edgeNoise);
                    float alpha = 1 - exp2(-density * stepLength * opacity * 10.0);
                    float shadow = saturate(Shape(p + lightDir * .16));
                    float underside = smoothstep(-.25,.22,p.y);
                    float topLight = saturate(.66 + underside * .28 - shadow * .64 +
                        (edgeNoise - .5) * .16);
                    float3 color = lerp(dark, bright, topLight) * day;
                    color += flash * (.20 + topLight * .60) * float3(.61,.70,1);
                    scattering += remaining * alpha * color;
                    remaining *= 1 - alpha;
                    if (remaining < .035) break;
                }

                return half4(scattering, 1 - remaining) * distanceFade;
            }
            ENDHLSL
        }
    }
}
