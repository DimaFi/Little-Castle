using System;
using System.Collections.Generic;
using LittleCastle.Research;
using LittleCastle.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace LittleCastle.World.Weather
{
    public interface ILightningOccluder
    {
        bool HidesLightningEndpoint(Camera camera, Vector3 endpoint);
    }

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
        private ParticleSystem nearRain, distantRain, splash, roofDrips;
        private Transform nearTransform, distantTransform;
        private Puddle[] puddles;
        private MapEditSession puddleSession;
        private const float PuddleCellSize = 7f;
        private readonly Dictionary<Vector2Int, PuddleSite> puddleSites = new Dictionary<Vector2Int, PuddleSite>(128);
        private readonly List<(Vector2Int key, float distance)> puddleCandidates = new List<(Vector2Int, float)>(128);
        private readonly List<Vector2Int> puddleRemoval = new List<Vector2Int>(128);
        private readonly Stack<int> freePuddles = new Stack<int>(64);
        private float puddleFadeEnd;
        private LineRenderer lightningLine, lightningBranch, lightningGlow, lightningBranchGlow;
        private Material lightningMaterial, lightningGlowMaterial;
        private Light flashLight;
        private AudioSource thunderSource, rainSource;
        private RainShelterMask shelterMask;
        private GameObject lensRoot;
        private Material lensMaterial;
        private float intensity, wetness, nextSurfaceUpdate, nextLightning, lightningAge = 100f;
        private float thunderDelay = -1f;
        private int lightningSequence;
        private Vector3 focus, strikePoint;
        private RainBudget budget;
        private bool initialized, rainPlaying, stormWasActive, hasCloudBolt, isDescendingBolt;
        private static readonly List<ILightningOccluder> lightningOccluders = new List<ILightningOccluder>(2);
        public static void RegisterLightningOccluder(ILightningOccluder source)
        {
            if (source != null && !lightningOccluders.Contains(source)) lightningOccluders.Add(source);
        }
        public static void UnregisterLightningOccluder(ILightningOccluder source) => lightningOccluders.Remove(source);
        private static readonly List<Renderer> lightningClouds = new List<Renderer>(4);

        // The optional landscape assembly supplies its live volumes without a reverse dependency.
        public static void RegisterLightningCloud(Renderer cloud)
        {
            if (cloud && !lightningClouds.Contains(cloud)) lightningClouds.Add(cloud);
        }

        public static void UnregisterLightningCloud(Renderer cloud) => lightningClouds.Remove(cloud);
        private static RainStormController active;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int WetnessId = Shader.PropertyToID("_Wetness");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int RippleRateId = Shader.PropertyToID("_RippleRate");
        private static readonly int GlobalWetId = Shader.PropertyToID("_LC_WeatherWetness");
        private static readonly int GlobalRainId = Shader.PropertyToID("_LC_WeatherRainIntensity");
        private static readonly int GlobalRiverImpactsId = Shader.PropertyToID("_LC_WeatherRiverImpacts");
        

        private RainStormProfile Settings => profile != null ? profile : ownedFallbackProfile;
        public RainMode CurrentMode => mode;
        public RainQuality CurrentQuality => quality;
        public float CurrentIntensity => intensity;
        public float CurrentWetness => wetness;

        public void SetMode(RainMode value) { mode = value; }
        public void SetQuality(RainQuality value) { quality = value; }
        public void ToggleDebugUI() { showDebugUI = !showDebugUI; }
        public static Rect DebugPanelBounds(int width,int height)=>new Rect(Mathf.Max(12,width-277),12,265,253);
        public static bool PointerOverDebugPanel(Vector2 screenPoint)=>active&&active.isActiveAndEnabled&&active.showDebugUI&&
            DebugPanelBounds(Screen.width,Screen.height).Contains(new Vector2(screenPoint.x,Screen.height-screenPoint.y));

        private void OnEnable()
        {
            if (Application.isPlaying) Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;
            if (active && active != this)
            {
                Debug.LogWarning("RainStorm: only one active weather presenter is allowed per scene. Duplicate disabled.", this);
                enabled = false;
                return;
            }
            active = this;
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
            roofDrips = CreateRoofDrips();
            puddleMesh = MakeQuad();
            CreatePuddles(64);
            CreateLightning();
            shelterMask = new RainShelterMask();
            CreateLensOverlay();
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

        private ParticleSystem CreateRoofDrips()
        {
            var obj = new GameObject("Shared roof-edge runoff pool");
            obj.transform.SetParent(ownedRoot.transform, false);
            var ps = obj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.startSpeed = 0f;
            main.startLifetime = 1.3f;
            main.maxParticles = 480;
            var emission = ps.emission; emission.rateOverTime = 0f;
            var shape = ps.shape; shape.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = streakMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = .24f;
            renderer.velocityScale = .07f;
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
        }

        private sealed class PuddleSite
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector2 radius;
            public float seed, chance, opacity, writtenWetness, snowFree = 1f;
            public bool selected, valid;
            public int rendererIndex = -1;
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
                var obj = new GameObject("Ground puddle " + i);
                obj.transform.SetParent(ownedRoot.transform, false);
                obj.AddComponent<MeshFilter>().sharedMesh = puddleMesh;
                var renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = rippleMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                puddles[i] = new Puddle { obj = obj, renderer = renderer, properties = new MaterialPropertyBlock() };
                freePuddles.Push(i);
                obj.SetActive(false);
            }
        }

        private void CreateLensOverlay()
        {
            Shader shader = Resources.Load<Shader>("RainStorm/LC_RainLens");
            if (!shader) return; // The rest of weather works without optional lens UI.
            lensMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lensRoot = new GameObject("Weather lens overlay (behind HUD)");
            lensRoot.hideFlags = HideFlags.HideAndDontSave;
            var canvas = lensRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = -200;
            var child = new GameObject("Sparse water droplets", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            child.transform.SetParent(lensRoot.transform, false);
            var rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = child.GetComponent<RawImage>();
            image.texture = Texture2D.whiteTexture;
            image.raycastTarget = false;
            image.material = lensMaterial;
            lensRoot.SetActive(false);
        }

        private void CreateLightning()
        {
            Shader shader = Resources.Load<Shader>("RainStorm/LC_StormLightning");
            // Missing optional shader must never prevent rain/snow rendering.
            if (shader)
            {
                lightningMaterial = new Material(shader)
                {
                    name = "Storm bolt HDR (runtime)", hideFlags = HideFlags.HideAndDontSave
                };
                lightningMaterial.SetColor("_EmissionColor", new Color(4.1f, 3.9f, 5.8f, 1f));
                lightningGlowMaterial = new Material(shader)
                {
                    name = "Storm wide glow (runtime)", hideFlags = HideFlags.HideAndDontSave
                };
                lightningGlowMaterial.SetColor("_EmissionColor", new Color(.55f, .43f, 1.2f, 1f));
                lightningGlowMaterial.SetFloat("_Opacity", .38f);
            }
            var obj = new GameObject("Procedural distant lightning");
            obj.transform.SetParent(ownedRoot.transform, false);
            lightningLine = obj.AddComponent<LineRenderer>();
            ConfigureLine(lightningLine, .40f, lightningMaterial);
            var branchObj = new GameObject("Branch");
            branchObj.transform.SetParent(obj.transform, false);
            lightningBranch = branchObj.AddComponent<LineRenderer>();
            ConfigureLine(lightningBranch, .22f, lightningMaterial);

            var glowObj = new GameObject("Wide soft glow");
            glowObj.transform.SetParent(obj.transform, false);
            lightningGlow = glowObj.AddComponent<LineRenderer>();
            ConfigureLine(lightningGlow, 1.5f, lightningGlowMaterial);
            var branchGlowObj = new GameObject("Branch glow");
            branchGlowObj.transform.SetParent(obj.transform, false);
            lightningBranchGlow = branchGlowObj.AddComponent<LineRenderer>();
            ConfigureLine(lightningBranchGlow, .8f, lightningGlowMaterial);

            var lightObj = new GameObject("Distant storm flash (shadowless)");
            lightObj.transform.SetParent(ownedRoot.transform, false);
            flashLight = lightObj.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.range = 165f;
            flashLight.shadows = LightShadows.None;
            flashLight.color = new Color(.65f, .78f, 1f);
            flashLight.enabled = false;

            var soundObj = new GameObject("Thunder audio");
            soundObj.transform.SetParent(ownedRoot.transform, false);
            thunderSource = soundObj.AddComponent<AudioSource>();
            thunderSource.playOnAwake = false;
            thunderSource.spatialBlend = .6f;
            var rainObj = new GameObject("Rain ambience (optional 2D loop)");
            rainObj.transform.SetParent(ownedRoot.transform, false);
            rainSource = rainObj.AddComponent<AudioSource>();
            rainSource.playOnAwake = false;
            rainSource.loop = true;
            rainSource.spatialBlend = 0f;
        }

        private void ConfigureLine(LineRenderer line, float width, Material overrideMaterial = null)
        {
            line.sharedMaterial = overrideMaterial ? overrideMaterial : streakMaterial;
            line.widthMultiplier = width;
            line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Stretch;
            line.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, .14f);
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
            Shader.SetGlobalFloat(GlobalWetId, wetness);
            Shader.SetGlobalFloat(GlobalRainId, intensity);
            budget = RainBudget.For(quality, Settings.customBudget);
            Shader.SetGlobalVector(GlobalRiverImpactsId, new Vector4(
                quality == RainQuality.Off ? 0f : Settings.rippleSpeed,
                Mathf.Clamp01((budget.splashCount - 4f) / 8f),
                budget.splashCount > 0 ? Settings.splashIntensity : 0f,
                RainStormMath.Unit(unchecked((uint)Settings.seed)) * 61f));
            if (lensRoot)
            {
                bool lensEnabled = Settings.allowLensRain && intensity > .05f &&
                    (quality == RainQuality.High || quality == RainQuality.Custom);
                if (lensRoot.activeSelf != lensEnabled) lensRoot.SetActive(lensEnabled);
                if (lensEnabled && lensMaterial) lensMaterial.SetFloat("_Opacity",
                    Mathf.Clamp01(intensity * Settings.lensStrength));
            }
            if (atmosphere && Settings.driveExistingAtmosphere)
            {
                atmosphere.SetWeatherPresentation(intensity * Settings.cloudiness, intensity * Settings.precipitationForAtmosphere);
                atmosphere.SetStormPresentation(mode == RainMode.Storm && quality != RainQuality.Off
                    ? intensity : 0f);
            }
            if (rainSource)
            {
                if (rainSource.clip != Settings.rainAmbience)
                {
                    rainSource.Stop();
                    rainSource.clip = Settings.rainAmbience;
                }
                rainSource.volume = intensity * Settings.rainVolume;
                if (rainSource.clip && !rainSource.isPlaying && intensity > .01f) rainSource.Play();
                if (rainSource.isPlaying && intensity <= .001f) rainSource.Stop();
            }
            if (!gameplayCamera) { StopRain(); HidePuddles(); return; }
            focus = GetGroundFocus(gameplayCamera);
            shelterMask?.Tick(focus);
            TickRain();
            if (Time.time >= nextSurfaceUpdate)
            {
                UpdateSurfaces();
                TickRoofDrips();
                nextSurfaceUpdate = Time.time + 0.35f;
            }
            TickPuddles(dt);
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

        private bool TryGroundSurface(float x, float z, out Vector3 p)
        {
            p = default;
            var session = mapPainter ? mapPainter.Session : null;
            float minimumUp = 1f - Mathf.Min(.045f, Settings.maximumGroundSlope);
            Vector3 origin = new Vector3(x, focus.y + 160f, z);
            int mask = Settings.groundLayers.value | Settings.waterLayers.value;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 320f, mask, QueryTriggerInteraction.Ignore))
            {
                if ((Settings.waterLayers.value & (1 << hit.collider.gameObject.layer)) != 0) return false;
                // Avoid puddles on high roofs / bridges if an authored ground height is available.
                if (session != null && hit.point.y > session.SampleHeight(x, z) + .16f) return false;
                if (RainShelterVolume.Active.Count == 0 && Settings.roofLayers.value != 0 &&
                    Physics.Raycast(hit.point + Vector3.up * .25f, Vector3.up, 64f,
                        Settings.roofLayers, QueryTriggerInteraction.Ignore)) return false;
                if (session != null && session.SampleEnvironmentSnow(x, z) > .55f) return false;
                if (hit.normal.y < minimumUp) return false;
                p = hit.point;
                if (mapPainter && mapPainter.TrySamplePathHeight(x, z, out float relief))
                    p.y = Mathf.Max(p.y, relief);
                return true;
            }
            // Authored terrain previews can run without colliders.
            if (Settings.useMapHeightFallback && session != null)
            {
                Vector4 bounds = session.Bounds;
                if (x < bounds.x || z < bounds.y || x > bounds.x + bounds.z || z > bounds.y + bounds.w ||
                    session.SampleEnvironmentSnow(x, z) > .55f || session.SampleNormal(x, z).y < minimumUp)
                    return false;
                p = new Vector3(x, session.SampleHeight(x, z), z);
                if (mapPainter.TrySamplePathHeight(x, z, out float relief)) p.y = Mathf.Max(p.y, relief);
                return true;
            }
            return false;
        }

        private bool TryFitPuddle(Vector3 center, Quaternion rotation, Vector2 radius, out Vector3 surface)
        {
            surface = center;
            var session = mapPainter ? mapPainter.Session : null;
            if (session != null && session.WeatherPuddleOverlapsRiver(new Vector2(center.x, center.z), radius.x))
                return false;

            // Sample both axes of the visible footprint. A flat sheet requires nearly level
            // support around its centre even when the local normal alone looks horizontal.
            Vector3 major = rotation * Vector3.right * (radius.x * .85f);
            Vector3 minor = rotation * Vector3.forward * (radius.y * .78f);
            float low = center.y, high = center.y;
            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = i < 2 ? major : minor;
                Vector3 point = center + (i % 2 == 0 ? offset : -offset);
                if (!TryGroundSurface(point.x, point.z, out Vector3 ground)) return false;
                low = Mathf.Min(low, ground.y);
                high = Mathf.Max(high, ground.y);
                if (high - low > .14f) return false;
            }
            surface.y = high + .025f;
            return true;
        }

        private void HidePuddles()
        {
            if (puddles == null) return;
            foreach (PuddleSite site in puddleSites.Values) ReleasePuddle(site);
        }

        private void ReleasePuddle(PuddleSite site)
        {
            if (site.rendererIndex < 0) return;
            puddles[site.rendererIndex].obj.SetActive(false);
            freePuddles.Push(site.rendererIndex);
            site.rendererIndex = -1;
            site.opacity = 0f;
        }

        private void OnPuddleSurfaceChanged(MapRegionChange change)
        {
            if (change.preview || (change.kind & (MapChangeKind.Height | MapChangeKind.Surface |
                MapChangeKind.Rivers | MapChangeKind.Snow | MapChangeKind.Objects)) == 0) return;
            Rect area = change.bounds;
            float margin = Settings.puddleRadius * 1.44f;
            area.min -= Vector2.one * margin;
            area.max += Vector2.one * margin;
            puddleRemoval.Clear();
            foreach (var pair in puddleSites)
            {
                var cell = new Rect((Vector2)pair.Key * PuddleCellSize, Vector2.one * PuddleCellSize);
                if (area.Overlaps(cell)) puddleRemoval.Add(pair.Key);
            }
            foreach (Vector2Int key in puddleRemoval)
            {
                ReleasePuddle(puddleSites[key]);
                puddleSites.Remove(key);
            }
        }

        private PuddleSite ProbePuddle(Vector2Int key)
        {
            uint seed = unchecked((uint)Settings.seed ^ (uint)(key.x * 73856093) ^ (uint)(key.y * 19349663));
            var site = new PuddleSite { seed = RainStormMath.Unit(seed + 5), chance = RainStormMath.Unit(seed) };
            Vector2 origin = (Vector2)key * PuddleCellSize;
            Vector2 preferred = origin + PuddleCellSize * new Vector2(
                .25f + RainStormMath.Unit(seed + 1) * .5f, .25f + RainStormMath.Unit(seed + 2) * .5f);
            Vector2 point = preferred;
            var inner = new Rect(origin + Vector2.one * .35f, Vector2.one * (PuddleCellSize - .7f));
            bool onPath = mapPainter && mapPainter.TryFindPathPoint(inner, preferred, out point);
            if (!onPath) point = preferred;
            if ((!onPath && site.chance < .25f) || !TryGroundSurface(point.x, point.y, out Vector3 ground))
                return site;
            float size = Settings.puddleRadius * (.68f + RainStormMath.Unit(seed + 3) * .76f);
            site.radius = new Vector2(size, size * (.61f + RainStormMath.Unit(seed + 6) * .36f));
            site.rotation = Quaternion.Euler(0, RainStormMath.Unit(seed + 4) * 360f, 0);
            if (!TryFitPuddle(ground, site.rotation, site.radius, out site.position))
            {
                site.radius *= .66f;
                if (!TryFitPuddle(ground, site.rotation, site.radius, out site.position)) return site;
            }
            if (puddleSession != null)
                site.snowFree -= MapWeatherBiomeMask.SnowWeight(puddleSession, point.x, point.y);
            site.valid = true;
            return site;
        }

        private void UpdateSurfaces()
        {
            if (puddles == null) return;
            var session = mapPainter ? mapPainter.Session : null;
            if (puddleSession != session)
            {
                if (puddleSession != null) puddleSession.Changed -= OnPuddleSurfaceChanged;
                HidePuddles();
                puddleSites.Clear();
                puddleSession = session;
                if (puddleSession != null) puddleSession.Changed += OnPuddleSurfaceChanged;
            }
            int wanted = wetness > .08f ? budget.puddleCount : 0;
            float reach = PuddleCellSize * (1f + 1.34f * Mathf.Sqrt(wanted / Mathf.PI));
            puddleFadeEnd = reach;
            Vector2 look = new Vector2(focus.x, focus.z);
            float retention = reach + PuddleCellSize * 1.5f;
            puddleRemoval.Clear();
            foreach (var pair in puddleSites)
            {
                pair.Value.selected = false;
                Vector2 center = ((Vector2)pair.Key + Vector2.one * .5f) * PuddleCellSize;
                if (pair.Value.rendererIndex < 0 && (center - look).sqrMagnitude > retention * retention)
                    puddleRemoval.Add(pair.Key);
            }
            foreach (Vector2Int key in puddleRemoval) puddleSites.Remove(key);
            if (wanted == 0) return;

            puddleCandidates.Clear();
            int minX = RainStormMath.Cell(look.x - reach, PuddleCellSize);
            int maxX = RainStormMath.Cell(look.x + reach, PuddleCellSize);
            int minZ = RainStormMath.Cell(look.y - reach, PuddleCellSize);
            int maxZ = RainStormMath.Cell(look.y + reach, PuddleCellSize);
            float search = reach + PuddleCellSize * .5f;
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                var key = new Vector2Int(x, z);
                Vector2 center = ((Vector2)key + Vector2.one * .5f) * PuddleCellSize;
                float distance = (center - look).sqrMagnitude;
                if (distance <= search * search) puddleCandidates.Add((key, distance));
            }
            puddleCandidates.Sort((a, b) => a.distance.CompareTo(b.distance));
            int probes = budget.probeCount, selected = 0;
            int bursts = Mathf.Min(budget.probeCount, budget.splashCount);
            foreach (var candidate in puddleCandidates)
            {
                if (!puddleSites.TryGetValue(candidate.key, out PuddleSite site))
                {
                    if (probes == 0) continue;
                    probes--;
                    site = ProbePuddle(candidate.key);
                    puddleSites.Add(candidate.key, site);
                }
                if (!site.valid || PuddleDistance(site) >= reach) continue;
                site.selected = true;
                if (bursts > 0 && intensity > .05f && site.opacity > .25f && EmitPuddleSplash(site)) bursts--;
                if (++selected >= wanted) break;
            }
        }

        private float PuddleDistance(PuddleSite site) =>
            Vector2.Distance(new Vector2(focus.x, focus.z), new Vector2(site.position.x, site.position.z));

        private void TickPuddles(float dt)
        {
            if (puddles == null) return;
            foreach (PuddleSite site in puddleSites.Values)
            {
                if (!site.selected && site.rendererIndex < 0) continue;
                float target = site.selected
                    ? Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(puddleFadeEnd * .68f, puddleFadeEnd, PuddleDistance(site)))
                    : 0f;
                if (site.rendererIndex < 0)
                {
                    if (target <= .001f || freePuddles.Count == 0) continue;
                    site.rendererIndex = freePuddles.Pop();
                    var entry = puddles[site.rendererIndex];
                    entry.obj.transform.SetPositionAndRotation(site.position, site.rotation);
                    entry.obj.transform.localScale = new Vector3(site.radius.x * 2f, 1f, site.radius.y * 2f);
                    entry.properties.SetFloat(SeedId, site.seed);
                    site.writtenWetness = -1f;
                }
                site.opacity = Mathf.MoveTowards(site.opacity, target, dt * 1.4f);
                if (site.opacity <= .001f && target <= .001f)
                {
                    ReleasePuddle(site);
                    continue;
                }
                float amount = wetness * Settings.wetReflection * site.snowFree * site.opacity;
                if (Mathf.Abs(site.writtenWetness - amount) < .001f) continue;
                site.writtenWetness = amount;
                var puddle = puddles[site.rendererIndex];
                puddle.properties.SetFloat(WetnessId, amount);
                puddle.renderer.SetPropertyBlock(puddle.properties);
                if (!puddle.obj.activeSelf) puddle.obj.SetActive(true);
            }
        }

        private bool EmitPuddleSplash(PuddleSite site)
        {
            float exposed = site.snowFree * (1f - RainShelterVolume.CoverageAt(site.position));
            if (exposed <= .15f) return false;
            var burst = new ParticleSystem.EmitParams
            {
                position = site.position,
                velocity = new Vector3(0f, .6f + site.chance, 0f),
                startLifetime = .22f,
                startSize = .04f + site.chance * .05f,
                startColor = new Color(.8f, .88f, .97f, intensity * Settings.splashIntensity * exposed * site.opacity)
            };
            splash.Emit(burst, Mathf.Clamp(Mathf.RoundToInt(2f + intensity * 3f), 2, 5));
            return true;
        }

        private void TickRoofDrips()
        {
            if (!roofDrips || quality == RainQuality.Off) return;
            float strength = Mathf.Max(intensity, wetness * .2f);
            if (strength < .035f) return;
            var sources = RainRoofDripSource.Active;
            int remaining = Mathf.Min(budget.splashCount * 2, 24);
            for (int i = 0; i < sources.Count && remaining > 0; i++)
            {
                RainRoofDripSource source = sources[i];
                if (!source || !source.affectsPreview || source.dripAmount <= 0f) continue;
                if (mapPainter && mapPainter.Session != null &&
                    MapWeatherBiomeMask.SnowWeight(mapPainter.Session,
                        source.transform.position.x, source.transform.position.z) > .8f) continue;
                if ((source.transform.position - focus).sqrMagnitude > 70f * 70f) continue;
                float actual = source.continueAfterRain ? strength : intensity;
                int drops = Mathf.Min(remaining, Mathf.FloorToInt(actual * source.dripAmount * 5f));
                for (int drop = 0; drop < drops; drop++)
                {
                    uint h = unchecked((uint)Settings.seed ^ (uint)(i * 7907) ^ (uint)(Time.frameCount * 251 + drop));
                    float along = RainStormMath.Unit(h) - .5f;
                    var emit = new ParticleSystem.EmitParams
                    {
                        position = source.transform.position + source.transform.right * (along * source.edgeLength),
                        velocity = new Vector3(0f, -4.5f - RainStormMath.Unit(h + 1) * 1.5f, 0f),
                        startLifetime = 1.05f,
                        startSize = .027f,
                        startColor = new Color(.75f, .84f, 1f, .52f)
                    };
                    roofDrips.Emit(emit, 1);
                }
                remaining -= drops;
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
            Shader.SetGlobalFloat("_LC_StormFlash", 0f);
            if (flashLight) flashLight.enabled = false;
            if (mode != RainMode.Storm || intensity < .65f ||
                quality == RainQuality.Off || !Settings.enableLightning || !gameplayCamera)
            {
                lightningLine.enabled = lightningBranch.enabled = false;
                lightningGlow.enabled = lightningBranchGlow.enabled = false;
                thunderDelay = -1f;
                stormWasActive = false;
                return;
            }
            if (!stormWasActive)
            {
                stormWasActive = true;
                // A first strike during the initial visual test, not 16–42 seconds later.
                nextLightning = Time.time + Mathf.Min(5f, NextLightningInterval());
            }
            if (Time.time >= nextLightning)
            {
                CreateBolt();
                nextLightning = Time.time + NextLightningInterval();
            }

            // Cloud lightning reads as a short double/triple flicker rather than a single white frame.
            float flicker = lightningAge < .085f ? 1f :
                lightningAge < .125f ? .08f :
                lightningAge < .22f ? .9f :
                lightningAge < .28f ? .04f :
                lightningAge < .41f ? .27f : 0f;
            if (Settings.reducedFlashes) flicker *= .35f;
            Shader.SetGlobalFloat("_LC_StormFlash", Settings.lightningFlash * flicker);
            bool visible = hasCloudBolt && flicker > .055f;
            lightningLine.enabled = lightningBranch.enabled = visible;
            bool expensiveGlow = quality == RainQuality.High || quality == RainQuality.Custom;
            lightningGlow.enabled = lightningBranchGlow.enabled = visible && expensiveGlow;
            if (visible)
            {
                Color color = new Color(1f, 1f, 1f, flicker);
                lightningLine.startColor = lightningLine.endColor = color;
                lightningBranch.startColor = lightningBranch.endColor = color;
                lightningGlow.startColor = lightningGlow.endColor = color;
                lightningBranchGlow.startColor = lightningBranchGlow.endColor = color;
                if (flashLight)
                {
                    flashLight.enabled = true;
                    flashLight.intensity = Settings.lightningFlash * 7f * flicker;
                }
            }

            if (thunderDelay >= 0f)
            {
                thunderDelay -= dt;
                if (thunderDelay < 0f && Settings.enableThunder && thunderSource)
                {
                    // The lightning is purposely far from the player.
                    AudioClip clip = Settings.distantThunder ? Settings.distantThunder : Settings.closeThunder;
                    if (clip) thunderSource.PlayOneShot(clip, Settings.thunderVolume);
                }
            }
        }

        private bool TryGetCloudBolt(uint seed, out Vector3 top, out Vector3 end)
        {
            top = end = Vector3.zero;
            float bestScore = float.NegativeInfinity;
            for (int i = lightningClouds.Count - 1; i >= 0; i--)
            {
                Renderer cloud = lightningClouds[i];
                if (!cloud) { lightningClouds.RemoveAt(i); continue; }
                if (!cloud.enabled || !cloud.gameObject.activeInHierarchy ||
                    cloud.gameObject.scene != gameObject.scene ||
                    (gameplayCamera.cullingMask & (1 << cloud.gameObject.layer)) == 0) continue;
                // Stay inside the broad central lobe, away from its noisy boundary.
                float flip = RainStormMath.Unit(seed + (uint)i * 17u) > .5f ? 1f : -1f;
                float z = Mathf.Lerp(-.07f, .07f, RainStormMath.Unit(seed + (uint)i + 37u));
                if (Vector3.Distance(gameplayCamera.transform.position,
                    cloud.bounds.ClosestPoint(gameplayCamera.transform.position)) < 125f) continue;
                // The RTS view frequently crops a bank along the top/left edge.
                // Try compact channels in its central and hanging lobes as well;
                // never move an endpoint into an arbitrary screen-space position.
                for (int anchor = 0; anchor < 3; anchor++)
                {
                    float halfSpan = anchor == 0 ? .21f : .085f;
                    float height = anchor == 0 ? .13f : anchor == 1 ? .035f : -.09f;
                    float depth = anchor == 2 ? .16f : z;
                    Vector3 a = cloud.transform.TransformPoint(new Vector3(-halfSpan * flip, height, depth));
                    Vector3 b = cloud.transform.TransformPoint(new Vector3(halfSpan * flip, height - .06f, depth));
                    Vector3 av = gameplayCamera.WorldToViewportPoint(a);
                    Vector3 bv = gameplayCamera.WorldToViewportPoint(b);
                    if (!InLightningView(av) || !InLightningView(bv)) continue;
                    // Prefer central, visible banks, with deterministic per-strike variation.
                    float score = 1f - Mathf.Abs((av.x + bv.x) * .5f - .5f) +
                        RainStormMath.Unit(seed + (uint)i * 71u) * .35f - anchor * .035f;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    top = a;
                    end = b;
                }
            }
            return bestScore > float.NegativeInfinity;
        }

        private bool InLightningView(Vector3 viewport) =>
            viewport.z > gameplayCamera.nearClipPlane && viewport.z < gameplayCamera.farClipPlane * .92f &&
            viewport.x > .025f && viewport.x < .975f && viewport.y > .08f && viewport.y < .975f;

        private bool TryDescendingBolt(uint seed, Vector3 top, out Vector3 end)
        {
            end = top;
            if (RainStormMath.Unit(seed + 903u) > .68f) return false;
            // A bounded rare-event query against the rendered mountain LOD. The
            // endpoint must be buried behind real rock, never just faded in air.
            Vector3 drift = gameplayCamera.transform.right * ((RainStormMath.Unit(seed + 904u) - .5f) * 12f);
            for (int step = 0; step < 6; step++)
            {
                Vector3 candidate = top + drift - Vector3.up * (45f + step * 24f);
                if (!InLightningView(gameplayCamera.WorldToViewportPoint(candidate))) continue;
                foreach (var occluder in lightningOccluders)
                {
                    if (occluder.HidesLightningEndpoint(gameplayCamera, top) ||
                        !occluder.HidesLightningEndpoint(gameplayCamera, candidate)) continue;
                    end = candidate;
                    return true;
                }
            }
            return false;
        }

        private void CreateBolt()
        {
            uint seed = unchecked((uint)Settings.seed + (uint)lightningSequence * 2654435761u);
            // A visible channel is an intracloud discharge: both ends live inside a
            // real rendered bank. Without one, use sheet lightning only, never a
            // camera-space zigzag hovering above the terrain.
            isDescendingBolt = false;
            hasCloudBolt = TryGetCloudBolt(seed, out Vector3 top, out Vector3 end);
            if (!hasCloudBolt)
            {
                Vector3 sky = Vector3.ProjectOnPlane(gameplayCamera.transform.forward, Vector3.up);
                if (sky.sqrMagnitude < .001f) sky = Vector3.forward;
                sky = (sky.normalized + Vector3.up * .35f).normalized;
                Shader.SetGlobalVector("_LC_StormFlashDirection", new Vector4(sky.x, sky.y, sky.z, 0f));
                lightningAge = 0f;
                thunderDelay = 1.4f;
                return;
            }
            bool descending = TryDescendingBolt(seed, top, out Vector3 hiddenEnd);
            isDescendingBolt = descending;
            if (descending) end = hiddenEnd;
            float distance = Vector3.Distance(gameplayCamera.transform.position, top);
            strikePoint = end;
            Vector3 side = gameplayCamera.transform.right;
            Vector3 depth = gameplayCamera.transform.forward;
            float length = Vector3.Distance(top, end);
            float deviation = Mathf.Clamp(length * .12f, 1.8f, 7f);
            const int segments = 20;
            var points = new Vector3[segments]; // Rare strike event, not per-frame GC.
            for (int i = 0; i < segments; i++)
            {
                float t = i / (segments - 1f);
                uint step = seed + (uint)(i * 41);
                float envelope = Mathf.Sin(t * Mathf.PI);
                points[i] = Vector3.Lerp(top, end, t) +
                    side * ((RainStormMath.Unit(step + 5) - .5f) * deviation * envelope) +
                    depth * ((RainStormMath.Unit(step + 6) - .5f) * deviation * envelope * .35f);
            }

            lightningLine.widthMultiplier = Mathf.Clamp(distance * .0019f, .2f, .65f);
            lightningGlow.widthMultiplier = lightningLine.widthMultiplier * 3.9f;
            lightningBranch.widthMultiplier = lightningLine.widthMultiplier * .55f;
            lightningBranchGlow.widthMultiplier = lightningGlow.widthMultiplier * .54f;
            lightningLine.positionCount = segments;
            lightningLine.SetPositions(points);
            lightningGlow.positionCount = segments;
            lightningGlow.SetPositions(points);
            const int branches = 7;
            lightningBranch.positionCount = branches;
            lightningBranchGlow.positionCount = branches;
            Vector3 fork = points[9];
            Vector3 branchEnd = descending
                ? points[16] + side * (deviation * 1.1f)
                : Vector3.Lerp(points[15], top, .2f) + Vector3.up * (deviation * .65f);
            for (int i = 0; i < branches; i++)
            {
                float t = i / (branches - 1f);
                uint step = seed + 97u + (uint)(i * 13);
                Vector3 p = Vector3.Lerp(fork, branchEnd, t) +
                    side * ((RainStormMath.Unit(step) - .5f) * deviation * .6f * Mathf.Sin(t * Mathf.PI));
                lightningBranch.SetPosition(i, p);
                lightningBranchGlow.SetPosition(i, p);
            }
            flashLight.transform.position = top;
            // Skybox is infinitely distant. This view ray makes the storm bank
            // glow around the actual bolt origin instead of flashing the entire sky.
            Vector3 skyDirection = (top - gameplayCamera.transform.position).normalized;
            Shader.SetGlobalVector("_LC_StormFlashDirection",
                new Vector4(skyDirection.x, skyDirection.y, skyDirection.z, 0f));
            lightningAge = 0f;
            thunderDelay = Mathf.Clamp(Vector3.Distance(gameplayCamera.transform.position, end) / 343f, .15f, 3f);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !showDebugUI) return;
            GUILayout.BeginArea(DebugPanelBounds(Screen.width,Screen.height), GUI.skin.box);
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
            if (mode == RainMode.Storm && intensity > .65f && Settings.enableLightning)
            {
                GUILayout.Label("Молния через " + Mathf.Max(0f, nextLightning-Time.time).ToString("F0") + " с");
                if (GUILayout.Button("Показать молнию сейчас"))
                {
                    CreateBolt();
                    nextLightning = Time.time + NextLightningInterval();
                }
            }
            if (mode == RainMode.Storm)
            {
                // Direct indication of the *actual* rendered skybox, not just
                // what the weather profile asked the atmosphere controller to set.
                Material actualSky = RenderSettings.skybox;
                bool supported = actualSky && actualSky.shader &&
                    actualSky.shader.name == "Little Castle/Sky/Stylized Day Night" &&
                    actualSky.HasProperty("_StormPeakCloudMass");
                if (!supported)
                    GUILayout.Label("Облака: проверь skybox / шейдер");
                else
                    GUILayout.Label("Тучи: " +
                        Mathf.RoundToInt(actualSky.GetFloat("_StormPeakCloudMass") * 100f) +
                        "% | горы: " +
                        Mathf.RoundToInt(Shader.GetGlobalFloat("_LC_MountainStormCloud") * 100f) +
                        "%");
            }
            if (GUILayout.Button("Скрыть панель (Inspector: showDebugUI)")) showDebugUI = false;
            GUILayout.EndArea();
        }

        private void OnDisable() { Cleanup(); }
        private void OnDestroy() { Cleanup(); }

        private void Cleanup()
        {
            if (puddleSession != null) puddleSession.Changed -= OnPuddleSurfaceChanged;
            puddleSession = null;
            puddleSites.Clear();
            puddleCandidates.Clear();
            puddleRemoval.Clear();
            freePuddles.Clear();
            if (active == this && atmosphere && Settings && Settings.driveExistingAtmosphere)
            {
                atmosphere.SetWeatherPresentation(0f, 0f);
                atmosphere.SetStormPresentation(0f);
            }
            if (shelterMask != null) { shelterMask.Dispose(); shelterMask = null; }
            if (ownedRoot) Destroy(ownedRoot);
            if (lensRoot) Destroy(lensRoot);
            if (lensMaterial) Destroy(lensMaterial);
            if (puddleMesh) Destroy(puddleMesh);
            if (lightningMaterial) Destroy(lightningMaterial);
            if (lightningGlowMaterial) Destroy(lightningGlowMaterial);
            if (streakMaterial) Destroy(streakMaterial);
            if (rippleMaterial) Destroy(rippleMaterial);
            if (ownedFallbackProfile) Destroy(ownedFallbackProfile);
            ownedRoot = null; puddleMesh = null; streakMaterial = null; rippleMaterial = null;
            lensRoot = null; lensMaterial = null;
            ownedFallbackProfile = null; initialized = false; rainPlaying = false; stormWasActive = false;
            if (active == this)
            {
                Shader.SetGlobalFloat(GlobalWetId, 0f);
                Shader.SetGlobalFloat(GlobalRainId, 0f);
                Shader.SetGlobalVector(GlobalRiverImpactsId, Vector4.zero);
                Shader.SetGlobalFloat("_LC_StormFlash", 0f);
                Shader.SetGlobalVector("_LC_StormFlashDirection", Vector4.zero);
                active = null;
            }
            puddles = null; nearRain = distantRain = splash = roofDrips = null;
            lightningLine = lightningBranch = lightningGlow = lightningBranchGlow = null;
            lightningMaterial = lightningGlowMaterial = null;
            thunderSource = null; rainSource = null; flashLight = null;
        }
    }
}
