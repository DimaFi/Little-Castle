using System;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Bounded, deterministic snow fields: a readable near layer and a larger
    /// high-altitude layer. Both use the SAME winter mask as liquid rain.
    /// No individual GameObjects or particle collisions per snowflake.
    /// </summary>
    public sealed class MapSnowfallPresentation : IDisposable
    {
        const int NearCount = 640;
        const int FarCount = 1536;
        const float NearRadius = 34f;
        const float FarRadius = 106f;

        readonly MapEditSession session;
        readonly MapWeatherBiomeMask biomeMask;
        readonly Layer near;
        readonly Layer far;

        sealed class Layer : IDisposable
        {
            internal readonly GameObject root;
            internal readonly Mesh mesh;
            internal readonly Material material;
            internal readonly MeshRenderer renderer;

            internal Layer(Material template, string name, int count, float radius,
                float baseHeight, float height, float minDistance, float size, float alpha)
            {
                root = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                mesh = new Mesh { name = name + " shared flake quads",
                    hideFlags = HideFlags.HideAndDontSave };
                var vertices = new Vector3[count * 4];
                var uvs = new Vector2[count * 4];
                var seeds = new Vector2[count * 4];
                var indices = new int[count * 6];
                uint random = 741103u + (uint)count * 397u;
                for (int i = 0; i < count; i++)
                {
                    random = unchecked(random * 1664525u + 1013904223u);
                    float angle = (random & 65535) / 65535f * Mathf.PI * 2f;
                    random = unchecked(random * 1664525u + 1013904223u);
                    float r = Mathf.Sqrt((random & 65535) / 65535f) * radius;
                    Vector3 center = new Vector3(Mathf.Cos(angle) * r, 0,
                        Mathf.Sin(angle) * r);
                    // Fixed seed makes locations stable as the layer follows the focus.
                    Vector2 seed = new Vector2((i * .618033989f) % 1f,
                        (i * .414213562f) % 1f);
                    for (int j = 0; j < 4; j++)
                    {
                        vertices[i * 4 + j] = center;
                        seeds[i * 4 + j] = seed;
                    }
                    uvs[i * 4] = Vector2.zero;
                    uvs[i * 4 + 1] = Vector2.right;
                    uvs[i * 4 + 2] = Vector2.one;
                    uvs[i * 4 + 3] = Vector2.up;
                    int at = i * 6, v = i * 4;
                    indices[at] = v; indices[at + 1] = v + 2; indices[at + 2] = v + 1;
                    indices[at + 3] = v; indices[at + 4] = v + 3; indices[at + 5] = v + 2;
                }

                mesh.vertices = vertices;
                mesh.uv = uvs;
                mesh.uv2 = seeds;
                mesh.triangles = indices;
                // CPU bounds encompass the ENTIRE GPU animation, not only mesh vertices
                // at zero height. Otherwise Unity culls flakes at oblique camera angles.
                mesh.bounds = new Bounds(new Vector3(0, baseHeight + height * .5f, 0),
                    new Vector3(radius * 2f + 12f, height + 20f, radius * 2f + 12f));
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = root.AddComponent<MeshRenderer>();
                material = new Material(template) { hideFlags = HideFlags.HideAndDontSave };
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.enabled = false;
                material.SetFloat("_FallHeight", height);
                material.SetFloat("_BaseHeight", baseHeight);
                material.SetFloat("_FieldRadius", radius);
                material.SetFloat("_MinDistance", minDistance);
                material.SetFloat("_FlakeScale", size);
                material.SetFloat("_LayerOpacity", alpha);
            }

            internal void Tick(Vector3 groundFocus, float strength)
            {
                root.transform.position = groundFocus;
                renderer.enabled = strength > .001f;
                if (renderer.enabled)
                    material.SetFloat("_SnowfallDensity", Mathf.Clamp01(strength));
            }

            public void Dispose()
            {
                Destroy(root); Destroy(mesh); Destroy(material);
            }
        }

        public MapSnowfallPresentation(MapEditSession session)
        {
            this.session = session;
            biomeMask = new MapWeatherBiomeMask(session);
            Material template = Resources.Load<Material>("MapAuthoring/LocalSnowfall");
            // The winter biome mask still works even when the optional flake
            // presentation material is absent in a partial checkout.
            if (!template) return;
            near = new Layer(template, "Near falling snow", NearCount, NearRadius,
                -5f, 48f, 0f, 1.0f, .85f);
            far = new Layer(template, "Distant high falling snow", FarCount, FarRadius,
                -10f, 116f, 24f, .62f, .46f);
        }

        public void Tick(Vector3 groundFocus, float ambientWinterSnow = .17f,
            bool renderDistantSnow = true)
        {
            biomeMask.Tick(groundFocus);
            if (near == null) return;
            var settings = session.EnvironmentSettings;
            float precipitation = Shader.GetGlobalFloat("_LC_WeatherRainIntensity");
            float strength = EvaluateIntensity(1f, 1f, settings.simulateWeather,
                settings.snowfallIntensity, precipitation, ambientWinterSnow);
            // Do not use only the central point: a dry summer foreground can
            // still see winter snowfall across the river or against distant hills.
            if (biomeMask.VisibleWinterCoverage < .01f) strength = 0f;
            near.Tick(groundFocus, strength);
            far.Tick(groundFocus, renderDistantSnow ? strength * .88f : 0f);
        }

        public static float EvaluateIntensity(float winterTheme, float visibleSnow,
            bool simulateWeather, float authoredSnow, float precipitationIntensity,
            float ambientSnow = .17f)
        {
            float winter = Mathf.Max(Mathf.Clamp01(winterTheme), Mathf.Clamp01(visibleSnow));
            float gentle = Mathf.Clamp01(ambientSnow);
            float authored = simulateWeather ? Mathf.Clamp01(authoredSnow) : 0f;
            float weather = gentle + (1f - gentle) * Mathf.Clamp01(precipitationIntensity);
            return Mathf.Clamp01(Mathf.Max(gentle, Mathf.Max(authored, weather)) * winter);
        }

        public void Dispose()
        {
            near?.Dispose();
            far?.Dispose();
            biomeMask.Dispose();
        }

        static void Destroy(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
