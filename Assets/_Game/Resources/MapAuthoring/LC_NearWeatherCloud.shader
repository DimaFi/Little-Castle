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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
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
                float bank = Lobe(p, float3(0,.10,0), float3(.43,.18,.35));
                float backLeft = Lobe(p, float3(-.25,.05,-.15), float3(.21,.22,.23));
                float backRight = Lobe(p, float3(.21,.14,-.12), float3(.24,.27,.25));
                float crown = Lobe(p, float3(-.09,.22 + variation * .12,.02), float3(.23,.23,.26));
                float frontLeft = Lobe(p, float3(-.28,-.01,.16), float3(.19,.19,.22));
                float hanging = Lobe(p, float3(-.05,-.13 + variation * .09,.20), float3(.20,.26,.23));
                float frontRight = Lobe(p, float3(.25,-.06,.15), float3(.21,.22,.23));
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
                float nearestDistance = distance(_WorldSpaceCameraPos, closestWS);
                float distanceFade = smoothstep(45.0, 125.0, nearestDistance);
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
                float stormLook = smoothstep(.08, .86, storm);
                float3 dark = lerp(float3(.077,.105,.15), float3(.009,.019,.039), stormLook);
                float3 bright = lerp(float3(.49,.56,.65), float3(.13,.20,.31), stormLook);
                float3 viewDir = normalize(TransformObjectToWorld(i.positionOS) - _WorldSpaceCameraPos);
                float flash = LCStormFlashHalo(viewDir, _LC_StormFlashDirection, _LC_StormFlash);
                float flashFocus = saturate(_LC_StormFlash) *
                    pow(saturate(dot(viewDir, _LC_StormFlashDirection.xyz)), 105.0);
                float3 lightDir = float3(-.32,.77,.28);
                float3 noiseOffset = float3(_Seed + _Time.y * .013, 9.3, 17.0);

                [loop] for (int k = 0; k < steps; k++)
                {
                    float3 p = i.positionOS + dir * ((k + .5) * stepLength);
                    float body = Shape(p);
                    if (body < -.17) continue;
                    float3 noiseGradient;
                    float edgeNoise = LCStormValue3(p * float3(10.5, 5.8, 8.5) +
                        noiseOffset, noiseGradient);
                    float edge = 1 - smoothstep(.42,.85,body);
                    float density = smoothstep(.01,.54,body + (edgeNoise - .54) * .58 * edge);
                    density *= lerp(.60, 1.14, edgeNoise);
                    float boxEdge = max(abs(p.x), max(abs(p.y), abs(p.z)));
                    density *= 1 - smoothstep(.45, .50, boxEdge);
                    float alpha = 1 - exp2(-density * stepLength * opacity * 10.0);
                    float shadow = saturate(Shape(p + lightDir * .16));
                    float underside = smoothstep(-.25,.22,p.y);
                    // Light the noise gradient at the billow edges using the
                    // same eight hashes as the density sample.
                    float billowLight = -dot(noiseGradient, lightDir) * edge;
                    float topLight = saturate(.53 + underside * .38 - shadow * .57 +
                        billowLight * .26 + (edgeNoise - .5) * .17);
                    topLight = lerp(topLight, topLight * topLight, stormLook * .70);
                    float3 color = lerp(dark, bright, topLight) * day;
                    color += flash * (.20 + topLight * .60) * float3(.61,.70,1);
                    // Catch the thin folds nearest the channel in lavender.
                    float flashRim = edge * saturate(.54 + billowLight * .48);
                    color += flashFocus * (.16 + flashRim * .44) * float3(.80,.64,1);
                    scattering += remaining * alpha * color;
                    remaining *= 1 - alpha;
                    if (remaining < .035) break;
                }

                float alpha = 1 - remaining;
                // Fog the premultiplied color so thin edges fade with the bank.
                float3 distant = MixFogColor(scattering, unity_FogColor.rgb * alpha,
                    ComputeFogFactorZ0ToFar(nearestDistance));
                scattering = lerp(scattering, distant, .26);
                return half4(scattering, alpha) * distanceFade;
            }
            ENDHLSL
        }
    }
}
