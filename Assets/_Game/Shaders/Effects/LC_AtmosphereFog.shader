Shader "Hidden/Little Castle/Atmosphere Fog"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _CameraDepthTexture;
            float4x4 _LC_AF_FrustumCorners;
            float3 _LC_AF_CameraPosition;
            float3 _LC_AF_CameraForward;
            float3 _LC_AF_FogColor;
            float _LC_AF_Start;
            float _LC_AF_DistanceDensity;
            float _LC_AF_MaxDistance;
            float _LC_AF_HeightBase;
            float _LC_AF_HeightDensity;
            float _LC_AF_HeightFalloff;
            float _LC_AF_NoiseScale;
            float _LC_AF_NoiseStrength;
            float _LC_AF_NoiseSpeed;
            float _LC_AF_SunScatter;
            float _LC_AF_ForwardExponent;
            float _LC_AF_Opacity;
            float _LC_AF_Saturation;
            float _LC_AF_Contrast;
            float _LC_AF_Steps;
            float _LC_AF_VolumeCount;
            float4 _LC_AF_Spheres[4];
            float4 _LC_AF_VolumeParams[4];
            float _LC_AF_PointLightCount;
            float4 _LC_AF_PointLights[4];
            float4 _LC_AF_PointColors[4];

            // Existing Little Castle lighting contract, NOT a new world clock.
            float4 _LC_SunDirection;
            float4 _LC_SunColor;
            float4 _LC_Wind;
            float4 _LC_GameTime;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float LocalDensity(float3 p)
            {
                float result = 0;
                [unroll] for (int v = 0; v < 4; ++v)
                {
                    if (v >= (int)_LC_AF_VolumeCount) break;
                    float4 sphere = _LC_AF_Spheres[v];
                    float4 prm = _LC_AF_VolumeParams[v];
                    float inside = sphere.w - distance(p, sphere.xyz);
                    float weight = saturate(inside / max(0.1, prm.y));
                    result += prm.x * (weight * weight * (3.0 - 2.0 * weight));
                }
                return result;
            }

            float3 NightFogLight(float3 p)
            {
                float3 result = 0;
                [unroll] for (int n = 0; n < 4; n++)
                {
                    if (n >= (int)_LC_AF_PointLightCount) break;
                    float4 light = _LC_AF_PointLights[n];
                    float w = saturate(1.0 - distance(p, light.xyz) / max(0.01, light.w));
                    result += _LC_AF_PointColors[n].rgb * w * w;
                }
                return result;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 scene = tex2D(_MainTex, i.uv);
                float2 depthUV = i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) depthUV.y = 1.0 - depthUV.y;
                #endif

                float depthRaw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, depthUV);
                // Sky has no geometry depth: leave the authored skybox unchanged.
                if (Linear01Depth(depthRaw) >= 0.9999) return scene;

                float3 lower = lerp(_LC_AF_FrustumCorners[0].xyz,
                                    _LC_AF_FrustumCorners[1].xyz, depthUV.x);
                float3 upper = lerp(_LC_AF_FrustumCorners[3].xyz,
                                    _LC_AF_FrustumCorners[2].xyz, depthUV.x);
                float3 ray = normalize(lerp(lower, upper, depthUV.y));
                float eyeDepth = LinearEyeDepth(depthRaw);
                float rayDistance = eyeDepth /
                    max(0.0001, dot(ray, normalize(_LC_AF_CameraForward)));
                rayDistance = min(rayDistance, _LC_AF_MaxDistance);
                float travel = max(0.0, rayDistance - _LC_AF_Start);
                if (travel <= 0.00001) return scene;

                int steps = (int)clamp(_LC_AF_Steps, 4.0, 24.0);
                float stepLength = travel / steps;
                float transmittance = 1.0;
                float3 inscatter = 0;
                float sunlight = pow(saturate(dot(ray, normalize(_LC_SunDirection.xyz))),
                                     max(1.0, _LC_AF_ForwardExponent));
                // This is directional in-scattering, NOT light-shaft occlusion.
                float3 scatterColor = max(0, _LC_AF_FogColor +
                    _LC_SunColor.rgb * sunlight * _LC_AF_SunScatter);
                float2 wind = _LC_Wind.xy * _LC_Wind.z *
                    _LC_GameTime.z * _LC_AF_NoiseSpeed;

                [loop] for (int k = 0; k < 24; k++)
                {
                    if (k >= steps) break;
                    float distanceAlongRay = _LC_AF_Start + (k + 0.5) * stepLength;
                    float3 p = _LC_AF_CameraPosition + ray * distanceAlongRay;
                    float altitude = max(0.0, p.y - _LC_AF_HeightBase);
                    float heightLayer = _LC_AF_HeightDensity *
                        exp(-altitude / max(0.1, _LC_AF_HeightFalloff));
                    float density = _LC_AF_DistanceDensity + heightLayer;
                    if (_LC_AF_NoiseStrength > 0.0001)
                    {
                        float breakup = ValueNoise(p.xz * _LC_AF_NoiseScale + wind);
                        density *= lerp(1.0, 0.5 + breakup, _LC_AF_NoiseStrength);
                    }
                    density += LocalDensity(p);
                    float segmentT = exp(-max(0.0, density) * stepLength);
                    float3 sampleLight = scatterColor + NightFogLight(p);
                    inscatter += transmittance * (1.0 - segmentT) * sampleLight;
                    transmittance *= segmentT;
                }

                float3 fogged = scene.rgb * transmittance + inscatter;
                float3 outColor = lerp(scene.rgb, fogged, saturate(_LC_AF_Opacity));
                float luma = dot(outColor, float3(0.2126, 0.7152, 0.0722));
                outColor = lerp(luma.xxx, outColor, _LC_AF_Saturation);
                outColor = (outColor - 0.5) * _LC_AF_Contrast + 0.5;
                return float4(max(0, outColor), scene.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
