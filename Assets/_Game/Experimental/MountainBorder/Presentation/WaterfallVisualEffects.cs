using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LittleCastle.Experimental.MountainBorder.Presentation
{
    /// <summary>
    /// Owns only transient presentation. Authored source meshes remain untouched.
    /// Tick once from the owning presentation; no scene/global shader state is written.
    /// Source geometry coordinates are local to the owner's section transform.
    /// </summary>
    public sealed class WaterfallVisualEffects : IDisposable
    {
        public GameObject Root { get; private set; }
        public Mesh VolumeMesh { get; private set; }
        public Mesh FoamMesh { get; private set; }
        public Mesh SplashMesh { get; private set; }
        public bool IsDisposed { get; private set; }
        public Vector4 WetnessLip => WetPoint(lip, wetRadius);
        public Vector4 WetnessLanding => WetPoint(landing, wetRadius * 1.5f);
        public Vector4 WetnessRadius => new Vector4(wetRadius, wetRadius * 1.5f, 1.5f, .65f);

        private readonly Vector3 lip, landing;
        private readonly float wetRadius, nearDistance, farDistance;
        private readonly WaterfallVisualSettings settings;
        private readonly Vector3 flowDirection;
        private readonly float lipWidth, landingWidth;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<ParticleSystem> particles = new List<ParticleSystem>();
        private readonly List<float> rates = new List<float>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private Mesh farMesh;
        private MeshFilter volumeFilter;
        private Renderer foamRenderer;
        private Renderer splashRenderer;
        private int distanceTier = -1;

        public WaterfallVisualEffects(Transform owner, WaterfallGeometryV1 geometry,
            WaterfallVisualSettings settings, Material volumeMaterial, Material mistMaterial,
            Texture normalTexture = null, Texture foamTexture = null, Texture phaseTexture = null, int seed = 947)
        {
            if (geometry == null || !geometry.fall || !geometry.lower || !geometry.upper) throw new ArgumentException("Complete waterfall geometry is required.", nameof(geometry));
            this.settings = settings ?? new WaterfallVisualSettings();
            lip = geometry.lip; landing = geometry.landing;
            var sourceRows = ReadRows(geometry.fall);
            lipWidth = Vector3.Distance(Sample(sourceRows, .1f).left, Sample(sourceRows, .1f).right);
            landingWidth = Vector3.Distance(Sample(sourceRows, .9f).left, Sample(sourceRows, .9f).right);
            flowDirection = Vector3.ProjectOnPlane(landing - lip, Vector3.up).normalized;
            if (flowDirection.sqrMagnitude < .001f) flowDirection = Vector3.back;
            wetRadius = Mathf.Clamp(this.settings.wetRockRadius, 1f, 8f);
            nearDistance = Mathf.Max(20f, this.settings.nearDistance);
            farDistance = Mathf.Max(nearDistance + 10f, this.settings.farDistance);
            Root = new GameObject("Waterfall V2 · local presentation") { hideFlags = HideFlags.DontSave };
            Root.transform.SetParent(owner, false);
            Root.transform.localPosition = Vector3.zero;
            Root.transform.localRotation = Quaternion.identity;
            Root.transform.localScale = Vector3.one;
            try
            {
                var budget = this.settings.Budget;
                if (budget.strands == 0) return;
                if (!volumeMaterial) throw new ArgumentNullException(nameof(volumeMaterial));
                Material water = CloneMaterial(volumeMaterial, "Waterfall V2 volume (owned)", normalTexture, foamTexture, phaseTexture);
                VolumeMesh = BuildVolumeMesh(geometry.fall, this.settings, seed);
                volumeFilter = AddMesh("Connected curtain · overlapping water sheets", VolumeMesh, water);
                if (this.settings.quality != WaterfallVisualQuality.Low)
                    farMesh = BuildVolumeMesh(geometry.fall, new WaterfallVisualSettings
                        { quality = WaterfallVisualQuality.Low, thickness = this.settings.thickness }, seed);
                Material foam = CloneMaterial(volumeMaterial, "Waterfall V2 impact and lip foam (owned)", normalTexture, foamTexture, phaseTexture);
                foam.renderQueue=3003;
                foam.SetFloat("_SurfaceMode", 1f);
                foam.SetFloat("_Opacity", .72f);
                FoamMesh = BuildFoamMesh(geometry.upper, geometry.lower, lip, landing, lipWidth, landingWidth);
                foamRenderer = AddMesh("Lip foam · impact wakes", FoamMesh, foam).GetComponent<Renderer>();
                Material splash = CloneMaterial(volumeMaterial, "Waterfall raised impact (owned)", normalTexture, foamTexture, phaseTexture);
                splash.renderQueue = 3006; splash.SetFloat("_SurfaceMode", 3f);
                SplashMesh = BuildImpactMesh(landing, flowDirection, landingWidth, budget, seed);
                splashRenderer = AddMesh("Raised impact · turbulent water crown", SplashMesh, splash).GetComponent<Renderer>();
                if (mistMaterial)
                {
                    Material spray = new Material(mistMaterial) { name = "Waterfall V2 spray (owned)", hideFlags = HideFlags.DontSave };
                    materials.Add(spray);
                    spray.SetColor("_Tint", this.settings.foamColor);
                    if (phaseTexture) spray.SetTexture("_NoiseTex", phaseTexture);
                    AddParticles("Impact spray", spray, budget.sprayParticles, 45f, 1.1f, .28f, .85f, 6.2f, .42f, seed, 0);
                    AddParticles("Falling droplets", spray, budget.droplets, 35f, .9f, .075f, .18f, 4.6f, .65f, seed, 1);
                    if (budget.mistParticles > 0)
                        AddParticles("Light landing mist", spray, budget.mistParticles, 4.5f, 3.2f, 2.2f, 4.2f, 1.1f, 0f, seed, 2);
                }
                UpdatePresentation(null, 0f);
            }
            catch { Dispose(); throw; }
        }

        private Material CloneMaterial(Material template, string name, Texture normal, Texture foam, Texture phase)
        {
            var material = new Material(template) { name = name, hideFlags = HideFlags.DontSave };
            materials.Add(material);
            material.SetFloat("_SurfaceMode", 0f);
            material.SetFloat("_NormalStrength", Mathf.Clamp(settings.normalStrength, .2f, 2f));
            material.SetFloat("_FlowSpeed", Mathf.Clamp(settings.flowSpeed, 1f, 12f));
            material.SetFloat("_FoamStrength", Mathf.Clamp(settings.foamStrength, 0f, 1.5f));
            material.SetColor("_WaterColor", settings.waterColor);
            material.SetColor("_FoamColor", settings.foamColor);
            material.SetVector("_ImpactPosition", landing);
            material.SetVector("_LipPosition", lip);
            material.SetVector("_FlowDirection", flowDirection);
            material.SetVector("_ChannelWidths", new Vector4(lipWidth, landingWidth, 0f, 0f));
            if (normal) material.SetTexture("_NormalTex", normal);
            if (foam) material.SetTexture("_FoamTex", foam);
            if (phase) material.SetTexture("_PhaseTex", phase);
            return material;
        }

        private MeshFilter AddMesh(string name, Mesh mesh, Material material)
        {
            var child = new GameObject(name) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(Root.transform, false);
            var filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureRenderer(renderer);
            return filter;
        }

        private void ConfigureRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderers.Add(renderer);
        }

        private void AddParticles(string name, Material material, int cap, float rate, float lifetime,
            float sizeMin, float sizeMax, float speed, float gravity, int seed, int kind)
        {
            var child = new GameObject(name) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(Root.transform, false);
            child.transform.localPosition = landing + Vector3.up * (kind == 2 ? .55f : .12f);
            Vector3 flow = flowDirection;
            child.transform.localRotation = Quaternion.LookRotation(Vector3.up, -flow);
            var system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false;
            system.randomSeed = unchecked((uint)(seed + 101 + kind * 719)) | 1u;
            var main = system.main;
            main.playOnAwake = false; main.loop = true; main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = cap; main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .55f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.gravityModifier = gravity;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // The shared spray material already owns foamColor; tinting both would square/darken it.
            Color color = Color.white;
            color.a = kind == 2 ? Mathf.Clamp(settings.mistOpacity * 2.2f, 0f, .5f) : kind == 1 ? .7f : .62f;
            main.startColor = color;
            var emission = system.emission;
            float widthScale = Mathf.Clamp(landingWidth / 6.5f, .3f, 4f);
            float amount = kind == 2 ? Mathf.Clamp01(settings.mistOpacity / .13f) : Mathf.Clamp01(settings.sprayAmount);
            float boundedRate = Mathf.Min(rate * widthScale * amount, cap / lifetime * .85f);
            emission.rateOverTime = boundedRate;
            var shape = system.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = kind == 2 ? 40f : 32f;
            // Local X is channel-across, -Y is downstream, Z emits upwards.
            // Spread across the entire impact line without growing the particle cap.
            shape.radius = 1f;
            shape.radiusThickness = 1f;
            shape.scale = new Vector3(landingWidth * (kind == 2 ? .48f : .46f),
                Mathf.Clamp(landingWidth * .11f, .2f, 1.8f), 1f);
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = 0f; velocity.z = 0f; velocity.speedModifier = 1f;
            velocity.y = kind == 2 ? -.32f : -.75f;
            var collision = system.collision; collision.enabled = false;
            var trails = system.trails; trails.enabled = false;
            var colorOver = system.colorOverLifetime; colorOver.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .16f), new GradientAlphaKey(.65f, .55f), new GradientAlphaKey(0f, 1f) });
            colorOver.color = gradient;
            var sizeOver = system.sizeOverLifetime; sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .6f, 1f, kind == 2 ? 1.8f : .25f));
            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = kind == 1 ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = .8f; renderer.velocityScale = .06f;
            renderer.maxParticleSize = kind == 2 ? .2f : .035f;
            var properties = new MaterialPropertyBlock(); properties.SetFloat("_MistKind", kind);
            renderer.SetPropertyBlock(properties);
            ConfigureRenderer(renderer);
            particles.Add(system); rates.Add(boundedRate);
        }

        /// <summary>Near: all budgeted layers; middle: reduced emission; far: mesh only.</summary>
        public void UpdatePresentation(Camera camera, float presentationTime)
        {
            if (IsDisposed) return;
            SetPreviewTime(presentationTime);
            float distance = camera ? Vector3.Distance(camera.transform.position,
                Root.transform.TransformPoint(Vector3.Lerp(lip, landing, .5f))) : 0f;
            int tier = distance > farDistance ? 2 : distance > nearDistance ? 1 : 0;
            bool changed = tier != distanceTier;
            distanceTier = tier;
            if (changed)
            {
                if (volumeFilter) volumeFilter.sharedMesh = tier == 2 && farMesh ? farMesh : VolumeMesh;
                if (foamRenderer) foamRenderer.enabled = tier != 2;
                if (splashRenderer) splashRenderer.enabled = tier != 2;
                foreach (var material in materials)
                    if (material.HasProperty("_DetailFade")) material.SetFloat("_DetailFade", tier == 2 ? .35f : 1f);
            }
            for (int i = 0; i < particles.Count; i++)
            {
                var system = particles[i];
                bool enabled = tier < 2;
                var emission = system.emission;
                if (changed) emission.rateOverTime = enabled ? rates[i] * (tier == 1 ? .55f : 1f) : 0f;
                // Simulate() pauses an Editor preview. Resume even without a distance-tier change.
                if (enabled && !system.isPlaying) system.Play(false);
                else if (!enabled && changed) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void SetPreviewTime(float presentationTime)
        {
            foreach (var material in materials)
                if (material && material.HasProperty("_PreviewTime")) material.SetFloat("_PreviewTime", presentationTime);
        }

        /// <summary>Bounded wet-rock influence. Does not modify terrain, paint channels or authored exclusions.</summary>
        public float QueryWetness(Vector3 worldPosition)
        {
            if (IsDisposed || settings.quality == WaterfallVisualQuality.Off) return 0f;
            worldPosition = Root.transform.InverseTransformPoint(worldPosition);
            Vector3 a = lip, b = landing;
            float t = Mathf.Clamp01(Vector3.Dot(worldPosition - a, b - a) / (b - a).sqrMagnitude);
            float distance = Vector3.Distance(worldPosition, Vector3.Lerp(a, b, t));
            float cascade = 1f - SmoothRange(0f, wetRadius, distance);
            float impact = (1f - SmoothRange(wetRadius * .35f, wetRadius * 1.5f,
                Vector2.Distance(new Vector2(worldPosition.x, worldPosition.z), new Vector2(b.x, b.z))))
                * (1f - SmoothRange(.5f, 3f, Mathf.Abs(worldPosition.y - b.y)));
            return Mathf.Clamp01(Mathf.Max(cascade * .65f, impact));
        }
        static float SmoothRange(float minimum,float maximum,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(minimum,maximum,value));

        private Vector4 WetPoint(Vector3 point, float radius)
        {
            if (Root) point = Root.transform.TransformPoint(point);
            return new Vector4(point.x, point.y, point.z, radius);
        }

        private struct Row { public float station, distance; public Vector3 left, right, normal; }

        private static List<Row> ReadRows(Mesh source)
        {
            var vertices = source.vertices; var uv = source.uv; var stations = source.uv2;
            if (vertices.Length == 0 || uv.Length != vertices.Length || stations.Length != vertices.Length)
                throw new ArgumentException("Source mesh requires arc-length UV0 and station UV2.", nameof(source));
            var grouped = new SortedDictionary<float, List<int>>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!grouped.TryGetValue(stations[i].y, out var indices)) grouped.Add(stations[i].y, indices = new List<int>());
                indices.Add(i);
            }
            var result = new List<Row>();
            foreach (var pair in grouped)
            {
                int left = pair.Value[0], right = left;
                foreach (int index in pair.Value) { if (uv[index].x < uv[left].x) left = index; if (uv[index].x > uv[right].x) right = index; }
                result.Add(new Row { station = pair.Key, distance = uv[left].y, left = vertices[left], right = vertices[right] });
            }
            if (result.Count < 2) throw new ArgumentException("Source mesh requires multiple stations.", nameof(source));
            for (int i = 0; i < result.Count; i++)
            {
                Row row = result[i];
                Vector3 before = (result[Mathf.Max(0, i - 1)].left + result[Mathf.Max(0, i - 1)].right) * .5f;
                Vector3 after = (result[Mathf.Min(result.Count - 1, i + 1)].left + result[Mathf.Min(result.Count - 1, i + 1)].right) * .5f;
                row.normal = Vector3.Cross(row.right - row.left, after - before).normalized;
                result[i] = row;
            }
            return result;
        }

        private static Row Sample(List<Row> rows, float station)
        {
            int i = 0;
            while (i < rows.Count - 2 && rows[i + 1].station < station) i++;
            Row a = rows[i], b = rows[i + 1];
            float t = Mathf.InverseLerp(a.station, b.station, station);
            return new Row { station = station, distance = Mathf.Lerp(a.distance, b.distance, t),
                left = Vector3.Lerp(a.left, b.left, t), right = Vector3.Lerp(a.right, b.right, t), normal = Vector3.Slerp(a.normal, b.normal, t).normalized };
        }

        /// <summary>
        /// A full-width shallow curtain and overlapping irregular sheets, all closed in cross-section.
        /// Keeps the original carved width and exposed-side clearance; never changes the source mesh.
        /// </summary>
        public static Mesh BuildVolumeMesh(Mesh source, WaterfallVisualSettings settings, int seed)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            settings = settings ?? new WaterfallVisualSettings();
            var budget = settings.Budget;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>();
            var colors = new List<Color>(); var triangles = new List<int>();
            var sections = new List<Vector4>();
            if (budget.strands > 0)
            {
                var rows = ReadRows(source);
                var random = new System.Random(seed);
                for (int strand = 0; strand < budget.strands; strand++)
                {
                    float phase = (float)random.NextDouble() * 6.283185f;
                    bool curtain = strand == 0;
                    float widthVariation = .82f + (float)random.NextDouble() * .4f;
                    float centerVariation = ((float)random.NextDouble() - .5f) * .48f;
                    int first = vertices.Count;
                    for (int r = 0; r <= budget.rows; r++)
                    {
                        float t = (float)r / budget.rows;
                        Row row = Sample(rows, t);
                        float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Min(t / .14f, (1f - t) / .14f));
                        float width = Vector3.Distance(row.left, row.right);
                        Vector3 lateral = (row.right - row.left).normalized;
                        int sheets = budget.strands - 1;
                        float centerU = curtain ? .5f : (strand + centerVariation) / (sheets + 1);
                        centerU += curtain ? 0f : Mathf.Sin(t * 8.3f + phase) * .16f / (sheets + 1) * envelope;
                        float halfU = curtain ? .5f : widthVariation / (sheets + 1)
                            * (1f + .22f * Mathf.Sin(t * 10.1f + phase));
                        halfU = Mathf.Min(halfU, Mathf.Min(centerU, 1f - centerU));
                        float halfWidth = width * halfU;
                        float thick = Mathf.Clamp(settings.thickness, .08f, .9f) * envelope
                            * (curtain ? .8f : .78f + .18f * Mathf.Sin(t * 9f + phase));
                        for (int c = 0; c <= budget.radialSegments; c++)
                        {
                            float angle = (float)c / budget.radialSegments * Mathf.PI * 2f;
                            float across = Mathf.Cos(angle);
                            Vector3 center = Vector3.Lerp(row.left, row.right, centerU);
                            float front = .5f + .5f * Mathf.Sin(angle);
                            // Shallow, broad section faces avoid the repeated cylindrical hose silhouette.
                            float bulge = front * (1f + .24f * Mathf.Sin(across * 6.3f + t * 10f + phase));
                            vertices.Add(center + lateral * (across * halfWidth) + row.normal * (.065f + thick * bulge));
                            uv.Add(new Vector2(centerU + across * halfWidth / width, row.distance));
                            uv2.Add(new Vector2((float)c / budget.radialSegments, t));
                            colors.Add(new Color(envelope, phase / 6.283185f, front, curtain ? 0f : 1f));
                            sections.Add(new Vector4(row.normal.x, row.normal.y, row.normal.z, thick * bulge));
                        }
                    }
                    int columns = budget.radialSegments + 1;
                    for (int r = 0; r < budget.rows; r++)
                    for (int c = 0; c < budget.radialSegments; c++)
                    {
                        int a = first + r * columns + c, b = a + 1, d = a + columns;
                        triangles.Add(a); triangles.Add(b); triangles.Add(d);
                        triangles.Add(b); triangles.Add(d + 1); triangles.Add(d);
                    }
                }
            }
            var result = CreateMesh("Waterfall V2 · connected curtain with overlapping sheets", vertices, uv, uv2, colors, triangles);
            result.SetUVs(2, sections);
            // Vertex waves are bounded to 18% of the section thickness.
            var bounds = result.bounds;
            for (int i = 0; i < vertices.Count; i++)
            {
                var section = sections[i];
                var offset = new Vector3(section.x, section.y, section.z) * (section.w * .18f);
                bounds.Encapsulate(vertices[i] + offset); bounds.Encapsulate(vertices[i] - offset);
            }
            // Bounds stores center/extents; leave room for their float rounding
            // after rotating a link, rather than culling at the exact wave extremum.
            bounds.Expand(.001f);
            result.bounds = bounds;
            return result;
        }

        private static Mesh BuildFoamMesh(Mesh upper, Mesh lower, Vector3 lip, Vector3 landing, float lipWidth, float landingWidth)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>();
            var colors = new List<Color>(); var triangles = new List<int>();
            Vector3 downstream = landing - lip; downstream.y = 0f;
            downstream = downstream.sqrMagnitude > .0001f ? downstream.normalized : Vector3.back;
            foreach (Mesh source in new[] { upper, lower })
            {
                var points = source.vertices; var sourceUV = source.uv;
                var indices = source.triangles;
                int start = vertices.Count;
                for (int i = 0; i < points.Length; i++)
                {
                    vertices.Add(points[i] + Vector3.up * .035f); uv.Add(sourceUV[i]); uv2.Add(Vector2.zero);
                    colors.Add(Color.white);
                }
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 p = (points[indices[i]] + points[indices[i + 1]] + points[indices[i + 2]]) / 3f;
                    float along = Vector3.Dot(p - (source == upper ? lip : landing), downstream);
                    // Selection ends outside the shader's feather, never through visible foam.
                    // Include complete triangles around the footprint. Testing only a
                    // centroid clipped visible foam on sparse or wide river meshes.
                    float pad = Mathf.Max(Vector3.Distance(points[indices[i]], p),
                        Mathf.Max(Vector3.Distance(points[indices[i + 1]], p), Vector3.Distance(points[indices[i + 2]], p)));
                    bool near = source == upper ? Mathf.Abs(along) < lipWidth * 1.2f + pad
                        : along > -landingWidth * .8f - pad && along < landingWidth * 3.2f + pad;
                    if (!near) continue;
                    triangles.Add(start + indices[i]); triangles.Add(start + indices[i + 1]); triangles.Add(start + indices[i + 2]);
                }
            }
            return CreateMesh("Waterfall V2 · foam follows existing water triangles", vertices, uv, uv2, colors, triangles);
        }

        private static Mesh BuildImpactMesh(Vector3 landing, Vector3 flow, float width, WaterfallVisualBudget budget, int seed)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var station = new List<Vector2>();
            var colors = new List<Color>(); var triangles = new List<int>();
            Vector3 lateral = Vector3.Cross(flow, Vector3.up);
            float reach = Mathf.Clamp(width * .42f, .6f, 3.8f);
            float height = Mathf.Clamp(width * .22f, .35f, 1.8f);
            float phase = (seed % 997) * .017f;
            for (int row = 0; row <= budget.ImpactRows; row++)
            for (int col = 0; col <= budget.ImpactColumns; col++)
            {
                float u = (float)col / budget.ImpactColumns, t = (float)row / budget.ImpactRows;
                float crest = .72f + .2f * Mathf.Sin(u * 17f + phase) + .12f * Mathf.Sin(u * 31f - phase);
                float envelope = Mathf.Sin(Mathf.PI * t) * Mathf.SmoothStep(0, 1, Mathf.Min(u, 1-u) * 8f);
                vertices.Add(landing + lateral * ((u-.5f) * width * (.94f + t * .12f))
                    + flow * (t * reach) + Vector3.up * (.06f + envelope * height * crest));
                uv.Add(new Vector2(u, t * reach)); station.Add(new Vector2(u,t));
                colors.Add(new Color(envelope, (phase % 1f), 1f, 1f));
                if (row == budget.ImpactRows || col == budget.ImpactColumns) continue;
                int a = row * (budget.ImpactColumns+1) + col, d = a + budget.ImpactColumns+1;
                triangles.Add(a); triangles.Add(a+1); triangles.Add(d);
                triangles.Add(a+1); triangles.Add(d+1); triangles.Add(d);
            }
            var mesh = CreateMesh("Waterfall V2 · bounded raised impact", vertices, uv, station, colors, triangles);
            var bounds = mesh.bounds; bounds.Expand(.3f); mesh.bounds = bounds;
            return mesh;
        }

        private static Mesh CreateMesh(string name, List<Vector3> vertices, List<Vector2> uv, List<Vector2> uv2, List<Color> colors, List<int> triangles)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            try
            {
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv2); mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
            catch { Destroy(mesh); throw; }
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            foreach (var particle in particles) if (particle) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(Root); Root = null;
            Destroy(VolumeMesh); VolumeMesh = null; Destroy(FoamMesh); FoamMesh = null;
            Destroy(SplashMesh); SplashMesh = null;
            Destroy(farMesh); farMesh = null;
            foreach (var material in materials) Destroy(material);
            materials.Clear(); particles.Clear(); rates.Clear(); renderers.Clear();
        }

        private static void Destroy(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
