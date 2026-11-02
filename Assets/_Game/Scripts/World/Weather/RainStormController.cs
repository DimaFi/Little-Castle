using System;
using LittleCastle.Research;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.World.Weather
{
    /// <summary>
    /// Explicit opt-in weather presentation. No scene, shared material, map or save data is mutated.
    /// Driven from the existing camera, atmosphere, and optional world-editor height source.
    /// </summary>
    [DefaultExecutionOrder(180)]
    [DisallowMultipleComponent]
    public sealed class RainStormController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RainStormProfile profile;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private DayNightLightingController atmosphere;
        [SerializeField] private MeadowSurfacePainter mapPainter;

        [Header("Game preview (opt-in component; no automatic spawning)")]
        [SerializeField] private RainMode mode = RainMode.Rain;
        [SerializeField] private RainQuality quality = RainQuality.High;
        [SerializeField] private bool showDebugUI = true;

        private RainStormProfile ownedFallbackProfile;
        private Material streakMaterial, rippleMaterial;
        private GameObject ownedRoot;
        private Mesh puddleMesh;
        private ParticleSystem nearRain, distantRain, splash;
        private Transform nearTransform, distantTransform;
        private Puddle[] puddles;
        private LineRenderer lightningLine, lightningBranch;
        private Light flashLight;
        private AudioSource thunderSource;
        private float intensity, wetness, nextSurfaceUpdate, nextLightning, lightningAge = 100f;
        private float thunderDelay = -1f;
        private int lightningSequence;
        private Vector3 focus, strikePoint;
        private RainBudget budget;
        private bool initialized, rainPlaying;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int WetnessId = Shader.PropertyToID("_Wetness");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int RippleRateId = Shader.PropertyToID("_RippleRate");

        private RainStormProfile Settings => profile != null ? profile : ownedFallbackProfile;
        public RainMode CurrentMode => mode;
        public RainQuality CurrentQuality => quality;
        public float CurrentIntensity => intensity;
        public float CurrentWetness => wetness;

        public void SetMode(RainMode value) { mode = value; }
        public void SetQuality(RainQuality value) { quality = value; }
        public void ToggleDebugUI() { showDebugUI = !showDebugUI; }

        private void OnEnable()
        {
            if (Application.isPlaying) Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;
            if (!profile)
            {
                ownedFallbackProfile = ScriptableObject.CreateInstance<RainStormProfile>();
                ownedFallbackProfile.hideFlags = HideFlags.HideAndDontSave;
            }
            if (!gameplayCamera) gameplayCamera = Camera.main;
            if (!atmosphere) atmosphere = FindAnyObjectByType<DayNightLightingController>();
            if (!mapPainter) mapPainter = FindAnyObjectByType<MeadowSurfacePainter>();
            var streak = Resources.Load<Shader>("RainStorm/LC_RainStreak");
            var ripple = Resources.Load<Shader>("RainStorm/LC_RainRipple");
            if (!streak || !ripple)
            {
                Debug.LogError("RainStorm: Resources/RainStorm shaders missing. Weather safely disabled.", this);
                Cleanup();
                enabled = false;
                return;
            }
            streakMaterial = new Material(streak) { name = "RainStorm Streak (Runtime)", hideFlags = HideFlags.HideAndDontSave };
            rippleMaterial = new Material(ripple) { name = "RainStorm Ripple (Runtime)", hideFlags = HideFlags.HideAndDontSave };
            rippleMaterial.SetFloat(RippleRateId, Settings.rippleSpeed);
            ownedRoot = new GameObject("RainStorm preview (owned runtime resources)");
            ownedRoot.hideFlags = HideFlags.HideAndDontSave;
            ownedRoot.transform.SetParent(transform, false);
            nearRain = CreateRain("Near streaks", out nearTransform, 0.45f);
            distantRain = CreateRain("Distant streaks", out distantTransform, 0.23f);
            splash = CreateSplash();
            puddleMesh = MakeQuad();
            CreatePuddles(64);
            CreateLightning();
            nextLightning = Time.time + NextLightningInterval();
            initialized = true;
        }

        private ParticleSystem CreateRain(string label, out Transform emitter, float brightness)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(ownedRoot.transform, false);
            emitter = obj.transform;
            var ps = obj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 2.9f;
            main.startSpeed = 0f;
            main.maxParticles = 6000;
            main.startSize = Settings.streakWidth;
            main.startColor = Settings.rainTint * new Color(1f, 1f, 1f, brightness);
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = -Settings.fallSpeed;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = streakMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.08f;
            renderer.lengthScale = Settings.streakLength;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private ParticleSystem CreateSplash()
        {
            var obj = new GameObject("Ground splashes (pooled particles)");
            obj.transform.SetParent(ownedRoot.transform, false);
            var ps = obj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = 0.3f;
            main.maxParticles = 320;
            var emission = ps.emission; emission.rateOverTime = 0f;
            var shape = ps.shape; shape.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = streakMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private struct Puddle
        {
            public GameObject obj;
            public MeshRenderer renderer;
            public MaterialPropertyBlock properties;
            public int x, z;
        }

        private static Mesh MakeQuad()
        {
            var mesh = new Mesh { name = "RainStorm quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f),
                new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, .1f, 1f));
            return mesh;
        }

        private void CreatePuddles(int maximum)
        {
            puddles = new Puddle[maximum];
            for (int i = 0; i < maximum; i++)
            {
                var obj = new GameObject("Puddle / water ripple " + i);
                obj.transform.SetParent(ownedRoot.transform, false);
                obj.AddComponent<MeshFilter>().sharedMesh = puddleMesh;
                var renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = rippleMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                puddles[i] = new Puddle { obj = obj, renderer = renderer,
                    properties = new MaterialPropertyBlock(), x = int.MinValue, z = int.MinValue };
                obj.SetActive(false);
            }
        }

        private void CreateLightning()
        {
            var obj = new GameObject("Procedural lightning");
            obj.transform.SetParent(ownedRoot.transform, false);
            lightningLine = obj.AddComponent<LineRenderer>();
            ConfigureLine(lightningLine, 0.13f);
            var branchObj = new GameObject("Branch");
            branchObj.transform.SetParent(obj.transform, false);
            lightningBranch = branchObj.AddComponent<LineRenderer>();
            ConfigureLine(lightningBranch, .055f);
            var lightObj = new GameObject("Storm flash (no shadows)");
            lightObj.transform.SetParent(ownedRoot.transform, false);
            flashLight = lightObj.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.range = 125f;
            flashLight.shadows = LightShadows.None;
            flashLight.color = new Color(.65f, .78f, 1f);
            flashLight.enabled = false;
            var soundObj = new GameObject("Thunder audio");
            soundObj.transform.SetParent(ownedRoot.transform, false);
            thunderSource = soundObj.AddComponent<AudioSource>();
            thunderSource.playOnAwake = false;
            thunderSource.spatialBlend = 0.6f;
        }

        private void ConfigureLine(LineRenderer line, float width)
        {
            line.sharedMaterial = streakMaterial;
            line.widthMultiplier = width;
            line.useWorldSpace = true;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
        }

        private void Update()
        {
            if (!initialized)
            {
                Initialize();
                if (!initialized) return;
            }
            if (!gameplayCamera) gameplayCamera = Camera.main;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            float desired = quality == RainQuality.Off ? 0f : RainStormMath.Intensity(mode);
            intensity = RainStormMath.Approach(intensity, desired, Settings.rainBlendSeconds, dt);
            wetness = RainStormMath.Wetness(wetness, intensity, dt, Settings.wettingSeconds, Settings.dryingSeconds);
            budget = RainBudget.For(quality, Settings.customBudget);
            if (atmosphere && Settings.driveExistingAtmosphere)
            {
                atmosphere.SetWeatherPresentation(intensity * Settings.cloudiness, intensity * Settings.precipitationForAtmosphere);
            }
            if (!gameplayCamera) { StopRain(); HidePuddles(); return; }
            focus = GetGroundFocus(gameplayCamera);
            TickRain();
            if (Time.time >= nextSurfaceUpdate)
            {
                UpdateSurfaces();
                nextSurfaceUpdate = Time.time + 0.35f;
            }
            TickLightning(dt);
        }

        private Vector3 GetGroundFocus(Camera camera)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            var plane = new Plane(Vector3.up, Vector3.zero);
            Vector3 p = plane.Raycast(ray, out float distance) && distance > 0 ? ray.GetPoint(distance) :
                camera.transform.position + camera.transform.forward * 30f;
            if (mapPainter && mapPainter.Session != null)
                p.y = mapPainter.Session.SampleHeight(p.x, p.z);
            else if (Physics.Raycast(ray, out RaycastHit hit, 600f, Settings.groundLayers, QueryTriggerInteraction.Ignore))
                p = hit.point;
            return p;
        }

        private void TickRain()
        {
            if (intensity < 0.004f || budget.nearParticles + budget.distantParticles == 0) { StopRain(); return; }
            if (!rainPlaying)
            {
                nearRain.Play(); distantRain.Play();
                rainPlaying = true;
            }
            ConfigureRainLayer(nearRain, nearTransform, budget.nearParticles, Settings.nearRadius, 1f);
            ConfigureRainLayer(distantRain, distantTransform, budget.distantParticles, Settings.distantRadius, .65f);
        }

        private void ConfigureRainLayer(ParticleSystem ps, Transform t, int count, float radius, float density)
        {
            float height = Settings.spawnHeight;
            Vector3 p = focus + Vector3.up * height;
            if ((t.position - p).sqrMagnitude > radius * radius * 2f) ps.Clear();
            t.position = p;
            var main = ps.main;
            main.maxParticles = Mathf.Max(1, count);
            main.startLifetime = Mathf.Max(.4f, (height + 8f) / Settings.fallSpeed);
            main.startSize = Settings.streakWidth * (density > .9f ? 1f : .72f);
            main.startColor = Settings.rainTint * new Color(1f, 1f, 1f, Mathf.Clamp01(intensity * density));
            var shape = ps.shape;
            shape.scale = new Vector3(radius * 2f, .25f, radius * 2f);
            var emission = ps.emission;
            emission.rateOverTime = count > 0 ? count * intensity / Mathf.Max(.4f, main.startLifetime.constant) : 0f;
            Vector4 wind = Shader.GetGlobalVector("_LC_Wind");
            var velocity = ps.velocityOverLifetime;
            float gust = 1f + Mathf.Sin(Time.time * .61f + Settings.seed) * .14f;
            velocity.x = wind.x * wind.z * Settings.windInfluence * gust * Settings.fallSpeed * 0.24f;
            velocity.y = -Settings.fallSpeed;
            velocity.z = wind.y * wind.z * Settings.windInfluence * gust * Settings.fallSpeed * 0.24f;
        }

        private void StopRain()
        {
            if (!rainPlaying) return;
            nearRain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            distantRain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            rainPlaying = false;
        }

        private bool TrySurface(float x, float z, out Vector3 p, out bool water)
        {
            water = false;
            p = default;
            Vector3 origin = new Vector3(x, focus.y + 160f, z);
            int mask = Settings.groundLayers.value | Settings.waterLayers.value;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 320f, mask, QueryTriggerInteraction.Ignore))
            {
                water = (Settings.waterLayers.value & (1 << hit.collider.gameObject.layer)) != 0;
                if (!water && Vector3.Dot(hit.normal, Vector3.up) < 1f - Settings.maximumGroundSlope) return false;
                p = hit.point + Vector3.up * (water ? 0.045f : 0.035f);
                return true;
            }
            // The world-editor mesh may have no collider; fallback samples only terrain.
            // Disable this in the Inspector if the authored map has an uncovered river.
            if (Settings.useMapHeightFallback && mapPainter && mapPainter.Session != null)
            {
                p = new Vector3(x, mapPainter.Session.SampleHeight(x, z) + .035f, z);
                return true;
            }
            return false;
        }

        private void HidePuddles()
        {
            if (puddles == null) return;
            for (int i = 0; i < puddles.Length; i++) puddles[i].obj.SetActive(false);
        }

        private void UpdateSurfaces()
        {
            if (puddles == null) return;
            int used = wetness > .08f ? budget.puddleCount : 0;
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, used))));
            const float cellSize = 7f;
            int cx = RainStormMath.Cell(focus.x, cellSize), cz = RainStormMath.Cell(focus.z, cellSize);
            for (int i = 0; i < puddles.Length; i++)
            {
                var pd = puddles[i];
                if (i >= used) { pd.obj.SetActive(false); continue; }
                int x = cx + i % columns - columns / 2;
                int z = cz + i / columns - columns / 2;
                uint seed = unchecked((uint)Settings.seed ^ (uint)(x * 73856093) ^ (uint)(z * 19349663));
                float chance = RainStormMath.Unit(seed);
                if (chance < .25f) { pd.obj.SetActive(false); continue; }
                float px = (x + .25f + RainStormMath.Unit(seed + 1) * .5f) * cellSize;
                float pz = (z + .25f + RainStormMath.Unit(seed + 2) * .5f) * cellSize;
                if (!TrySurface(px, pz, out Vector3 surface, out bool isWater))
                { pd.obj.SetActive(false); continue; }
                float size = Settings.puddleRadius * (0.6f + RainStormMath.Unit(seed + 3) * .9f);
                pd.obj.transform.position = surface;
                pd.obj.transform.rotation = Quaternion.Euler(0, RainStormMath.Unit(seed + 4) * 360f, 0);
                pd.obj.transform.localScale = new Vector3(size * 2, 1, size * 2);
                pd.properties.Clear();
                pd.properties.SetFloat(WetnessId, wetness * (isWater ? .55f : Settings.wetReflection));
                pd.properties.SetFloat(SeedId, RainStormMath.Unit(seed + 5));
                pd.renderer.SetPropertyBlock(pd.properties);
                pd.obj.SetActive(true);
                // A bounded handful of splash bursts per update, never physics for every drop.
                if (intensity > .05f && i < Mathf.Min(budget.probeCount, budget.splashCount))
                {
                    var burst = new ParticleSystem.EmitParams
                    {
                        position = surface,
                        velocity = new Vector3(0, .6f + chance, 0),
                        startLifetime = .22f,
                        startSize = .04f + chance * .05f,
                        startColor = new Color(.8f, .88f, .97f, intensity * Settings.splashIntensity)
                    };
                    splash.Emit(burst, 2);
                }
            }
        }

        private float NextLightningInterval()
        {
            uint seed = unchecked((uint)Settings.seed + (uint)lightningSequence++ * 747796405u);
            return Mathf.Lerp(Settings.minLightningInterval, Settings.maxLightningInterval, RainStormMath.Unit(seed));
        }

        private void TickLightning(float dt)
        {
            lightningAge += dt;
            if (flashLight) flashLight.enabled = false;
            if (mode != RainMode.Storm || intensity < .65f || quality == RainQuality.Off || !Settings.enableLightning)
            {
                lightningLine.enabled = lightningBranch.enabled = false;
                thunderDelay = -1f;
                nextLightning = Time.time + NextLightningInterval();
                return;
            }
            if (Time.time >= nextLightning)
            {
                CreateBolt();
                nextLightning = Time.time + NextLightningInterval();
            }
            bool visible = lightningAge < .23f && (lightningAge < .095f || lightningAge > .145f);
            lightningLine.enabled = lightningBranch.enabled = visible;
            if (visible && flashLight)
            {
                flashLight.enabled = true;
                float factor = lightningAge < .095f ? 1f : .3f;
                flashLight.intensity = (Settings.reducedFlashes ? .25f : 1f) * Settings.lightningFlash * 5f * factor;
            }
            if (thunderDelay >= 0f)
            {
                thunderDelay -= dt;
                if (thunderDelay < 0f && Settings.enableThunder && thunderSource)
                {
                    AudioClip clip = Vector3.Distance(gameplayCamera.transform.position, strikePoint) < 55f ?
                        Settings.closeThunder : Settings.distantThunder;
                    if (clip) thunderSource.PlayOneShot(clip, Settings.thunderVolume);
                }
            }
        }

        private void CreateBolt()
        {
            uint seed = unchecked((uint)Settings.seed + (uint)lightningSequence * 2654435761u);
            float dx = (RainStormMath.Unit(seed + 1) - .5f) * 115f;
            float dz = (RainStormMath.Unit(seed + 2) - .5f) * 115f;
            strikePoint = focus + new Vector3(dx, 0, dz);
            float height = 42f + RainStormMath.Unit(seed + 3) * 25f;
            const int segments = 14;
            var points = new Vector3[segments]; // Rare storm event only, not steady-state allocation.
            for (int i = 0; i < segments; i++)
            {
                float t = i / (segments - 1f);
                uint s = seed + (uint)(i * 31);
                points[i] = strikePoint + new Vector3((RainStormMath.Unit(s + 5) - .5f) * 5f * Mathf.Sin(t * Mathf.PI),
                    height * (1f - t), (RainStormMath.Unit(s + 6) - .5f) * 5f * Mathf.Sin(t * Mathf.PI));
            }
            lightningLine.positionCount = segments;
            lightningLine.SetPositions(points);
            lightningBranch.positionCount = 5;
            Vector3 b = points[6];
            for (int i = 0; i < 5; i++)
                lightningBranch.SetPosition(i, b + new Vector3(i * 2.1f, -i * 3.1f,
                    (RainStormMath.Unit(seed + 50 + (uint)i) - .5f) * i * 3f));
            flashLight.transform.position = strikePoint + Vector3.up * 18f;
            lightningAge = 0f;
            float distance = Vector3.Distance(gameplayCamera.transform.position, strikePoint);
            thunderDelay = Mathf.Clamp(distance / 343f, .09f, 4f);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !showDebugUI) return;
            GUILayout.BeginArea(new Rect(12, 12, 265, 174), GUI.skin.box);
            GUILayout.Label("Little Castle — Дождь / Гроза");
            GUILayout.Label("Осадки " + Mathf.RoundToInt(intensity * 100) + "% | Влажность " + Mathf.RoundToInt(wetness * 100) + "%");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Выкл.")) mode = RainMode.Off;
            if (GUILayout.Button("Морось")) mode = RainMode.Drizzle;
            if (GUILayout.Button("Дождь")) mode = RainMode.Rain;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ливень")) mode = RainMode.Downpour;
            if (GUILayout.Button("Гроза")) mode = RainMode.Storm;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Low")) quality = RainQuality.Low;
            if (GUILayout.Button("Medium")) quality = RainQuality.Medium;
            if (GUILayout.Button("High")) quality = RainQuality.High;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Скрыть панель (Inspector: showDebugUI)")) showDebugUI = false;
            GUILayout.EndArea();
        }

        private void OnDisable() { Cleanup(); }
        private void OnDestroy() { Cleanup(); }

        private void Cleanup()
        {
            if (atmosphere && Settings && Settings.driveExistingAtmosphere)
                atmosphere.SetWeatherPresentation(0f, 0f);
            if (ownedRoot) Destroy(ownedRoot);
            if (puddleMesh) Destroy(puddleMesh);
            if (streakMaterial) Destroy(streakMaterial);
            if (rippleMaterial) Destroy(rippleMaterial);
            if (ownedFallbackProfile) Destroy(ownedFallbackProfile);
            ownedRoot = null; puddleMesh = null; streakMaterial = null; rippleMaterial = null;
            ownedFallbackProfile = null; initialized = false; rainPlaying = false;
            puddles = null; nearRain = distantRain = splash = null;
            lightningLine = lightningBranch = null; thunderSource = null; flashLight = null;
        }
    }
}
