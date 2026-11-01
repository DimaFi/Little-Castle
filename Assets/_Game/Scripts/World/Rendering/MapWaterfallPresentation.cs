using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    /// <summary>Resident meshes from saved routes only. No scene-authored A/B curve is synthesized here.</summary>
    public sealed class MapWaterfallPresentation : IDisposable
    {
        sealed class View { public GameObject root; public Mesh body, impact; public readonly List<Mesh> ledgeFoam=new List<Mesh>();public ParticleSystem[] spray; public Renderer[] renderers; }
        MapEditSession session;
        readonly MapWaterfallVisualProfile profile;
        readonly Dictionary<long, View> views = new Dictionary<long, View>();
        readonly HashSet<long> wanted = new HashSet<long>();
        readonly List<long> remove = new List<long>();
        List<MapWaterfallRecord> records;
        bool dirty = true;
        Vector3 lastEye = Vector3.positiveInfinity;
        double lastAnimation=-1;
        float animationTime;
        readonly MaterialPropertyBlock animationProperties=new MaterialPropertyBlock();
        public float PreviewTime=>animationTime;
        public bool AnimationAdvanced {get;private set;}
        public string LastError { get; private set; }
        public int ResidentCount => views.Count;
        public Material RiverMaterial => profile ? profile.river : null;
        public RiverEffectsQuality SprayQuality=>profile?profile.sprayQuality:RiverEffectsQuality.Off;
        public IEnumerable<ParticleSystem> SpraySystems {get{foreach(var view in views.Values)if(view.spray!=null)foreach(var system in view.spray)if(system)yield return system;}}
        public IEnumerable<Renderer> Renderers {get{foreach(var view in views.Values)if(view.renderers!=null)foreach(var renderer in view.renderers)if(renderer)yield return renderer;}}
        internal void RebindSession(MapEditSession replacement,Rect area)
        {
            session.Changed-=Changed;session=replacement;session.Changed+=Changed;
            Changed(new MapRegionChange(area,false,MapChangeKind.Rivers));
        }
        public MapWaterfallPresentation(MapEditSession session, MapWaterfallVisualProfile profile = null)
        {
            this.session = session;
            this.profile = profile ? profile : Resources.Load<MapWaterfallVisualProfile>("MapAuthoring/ConnectedWaterVisuals");
            records = session.ExportRiverNetwork().waterfalls;
            session.Changed += Changed;
        }
        void Changed(MapRegionChange c)
        {
            if ((c.kind & (MapChangeKind.Height | MapChangeKind.Rivers)) == 0) return;
            dirty = true; records = session.ExportRiverNetwork().waterfalls; Clear();
        }
        public void Tick(Vector3 eye, bool force = false)
        {
            Animate();
            if (!dirty && !force && (eye - lastEye).sqrMagnitude < 4) return;
            lastEye = eye; dirty = false; wanted.Clear(); LastError = null;
            if (!profile || !profile.body || !profile.foam) return;
            foreach (var record in records)
            {
                if (record.route == null || record.route.IsEmpty) continue;
                if (record.route.Bounds.SqrDistance(eye) > profile.maximumDistance * profile.maximumDistance || wanted.Count >= profile.maximumResident) continue;
                wanted.Add(record.id); if (views.ContainsKey(record.id)) continue;
                try { views.Add(record.id, Build(record)); }
                catch (Exception e) { LastError = e.Message; }
            }
            remove.Clear(); foreach (long id in views.Keys) if (!wanted.Contains(id)) remove.Add(id);
            foreach (long id in remove) { DisposeView(views[id]); views.Remove(id); }
        }
        View Build(MapWaterfallRecord record)
        {
            var upper = new MapRiverPath(session.GetRiver(record.upstreamRiverId));
            var lower=new MapRiverPath(session.GetRiver(record.downstreamRiverId));
            float offset = session.RiverFlowTimeOffset(record.upstreamRiverId) + upper.TravelTime(upper.Length);
            var view = new View();
            try
            {
                view.root = new GameObject("Saved waterfall " + record.id) { hideFlags = HideFlags.HideAndDontSave };
                view.body = MapWaterfallMeshBuilder.Build(record.route, offset,lower,upper.record.layoutVersion==3);
                view.impact = MapWaterfallMeshBuilder.Impact(record.route,receivingRiver:lower,flowTimeOffset:session.RiverFlowTimeOffset(record.downstreamRiverId));
                Add(view.root, "Contact and free-fall volume", view.body, profile.body, record,landingDepth:Mathf.Lerp(lower.frames[0].depth,lower.frames[0].shallowDepth,lower.frames[0].ford),landingSpeed:lower.frames[0].speed);
                Add(view.root, "Lower river impact", view.impact, profile.foam, record,foamOpacity:lower.record.layoutVersion==3?.24f:-1);
                foreach(var site in MapWaterfallSpraySources.Resolve(record.route))if(site.row>0&&site.row<record.route.sections.Count-1)
                {
                    var foam=MapWaterfallMeshBuilder.Impact(record.route,site.row,.45f);view.ledgeFoam.Add(foam);
                    Add(view.root,"Ledge impact "+site.row,foam,profile.foam,record,record.route.sections[site.row]);
                }
                if (profile.spray) view.spray=AddSpray(view.root, record);
                view.renderers=view.root.GetComponentsInChildren<Renderer>();
                return view;
            }
            catch { DisposeView(view); throw; }
        }
        static void Add(GameObject root, string name, Mesh mesh, Material material, MapWaterfallRecord record,MapWaterfallSection impactSection=null,float landingDepth=0,float foamOpacity=-1,float landingSpeed=0)
        {
            var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = 4 };
            child.transform.SetParent(root.transform, false); child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.lightProbeUsage = LightProbeUsage.Off;
            var sections = record.route.sections;
            var lower = impactSection??sections[sections.Count - 1]; Vector3 landing = lower.FrontCenter;
            Vector3 direction = landing - sections[0].FrontCenter; direction.y = 0; direction.Normalize();
            float width = Vector3.Distance(lower.points[0].position, lower.points[lower.points.Count - 1].position);
            var properties = new MaterialPropertyBlock();
            properties.SetVector("_ImpactPosition", landing); properties.SetVector("_LipPosition", sections[0].FrontCenter);
            properties.SetVector("_FlowDirection", direction); properties.SetVector("_ChannelWidths", new Vector4(width, width, 0, 0));
            properties.SetFloat("_LandingDepth",landingDepth);
            properties.SetVector("_RiverFlowDirection",new Vector4(direction.x,direction.z,landingSpeed,0));
            properties.SetVector("_Effects",new Vector4(.6f,.7f,.35f,.5f));
            if(foamOpacity>=0)properties.SetFloat("_Opacity",foamOpacity);
            renderer.SetPropertyBlock(properties);
        }
        ParticleSystem[] AddSpray(GameObject root, MapWaterfallRecord record)
        {
            var sites=MapWaterfallSpraySources.Resolve(record.route);
            int budget=MapWaterfallSpraySources.Budget(profile.sprayQuality);if(budget==0)return Array.Empty<ParticleSystem>();
            var systems=new List<ParticleSystem>();
            for(int i=0;i<sites.Count;i++)
            {
            var site=sites[i];int capacity=budget/sites.Count+(i<budget%sites.Count?1:0);
            var child = new GameObject("Bounded spray at route row "+site.row) { hideFlags = HideFlags.HideAndDontSave };
            child.transform.SetParent(root.transform, false); child.transform.position = site.position+Vector3.up*.12f;
            child.transform.rotation = Quaternion.Euler(-90, 0, 0);
            var particles = child.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false; particles.randomSeed = (uint)((record.id+site.row*7919) & int.MaxValue) + 1;
            var main = particles.main; main.playOnAwake = false; main.loop = true; main.maxParticles = capacity;
            main.useUnscaledTime=true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.55f, 1.0f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(.10f, .29f); main.gravityModifier = .8f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var fade=particles.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.12f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var emission = particles.emission; emission.rateOverTime = capacity*.85f;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 28;
            shape.radius = site.width * .22f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = profile.spray;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (Application.isPlaying) particles.Play();else particles.Simulate(.01f,false,true,true);
            systems.Add(particles);
            }
            return systems.ToArray();
        }
        void Animate()
        {
            AnimationAdvanced=false;
            if(Application.isPlaying)return;
            double now=Time.realtimeSinceStartupAsDouble;
            if(lastAnimation<0){lastAnimation=now;return;}
            float delta=(float)(now-lastAnimation);if(delta<1f/30)return;
            lastAnimation=now;delta=Mathf.Min(delta,.1f);animationTime+=delta;AnimationAdvanced=true;
            foreach(var view in views.Values)
            {
                if(view.spray!=null)foreach(var particles in view.spray)if(particles)particles.Simulate(delta,false,false,true);
                if(view.renderers!=null)foreach(var renderer in view.renderers)if(renderer)
                {renderer.GetPropertyBlock(animationProperties);animationProperties.SetFloat("_PreviewTime",animationTime);renderer.SetPropertyBlock(animationProperties);}
            }
        }
        public bool Pick(Ray ray, out long waterfallId)
        {
            float best = float.PositiveInfinity; waterfallId = 0;
            foreach (var record in session.ExportRiverNetwork().waterfalls)
            {
                if (record.route == null || record.route.IsEmpty || !record.route.Bounds.IntersectRay(ray, out float entry)) continue;
                // AABB is broad phase only: sample the saved volume along this ray, no scene mesh ownership.
                float limit = record.route.Bounds.size.magnitude;
                for (float d = Mathf.Max(0, entry); d <= entry + limit; d += .08f)
                    if (record.route.Contains(ray.GetPoint(d), .1f))
                    { if (d < best) { best = d; waterfallId = record.id; } break; }
            }
            return waterfallId != 0;
        }
        void Clear() { foreach (var view in views.Values) DisposeView(view); views.Clear(); }
        static void DisposeView(View view) { Destroy(view.root); Destroy(view.body); Destroy(view.impact);foreach(var foam in view.ledgeFoam)Destroy(foam); }
        static void Destroy(UnityEngine.Object obj)
        { if (!obj) return; if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj); }
        public void Dispose() { session.Changed -= Changed; Clear(); }
    }
}
