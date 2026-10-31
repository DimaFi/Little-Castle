using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Rendering
{
    public enum FireflyDensityMode { Inherit, Multiply, Set, Exclude }
    public enum FireflyAreaShape { Ellipse, Polygon }
    public enum FireflyQuality { Off, Low, Medium, High, Custom }
    public enum FireflyNearMode { NaturalAreas, Everywhere }

    /// <summary>Authored map state. Never stores transient meshes, lights or camera objects.</summary>
    [Serializable] public sealed class FireflyMapData
    {
        public int version = 1;
        public bool enabled = true;
        public int seed = 73421;
        [Tooltip("Expected lights per 100 square metres before area overrides and GPU budgets.")]
        [Range(0, 80)] public float densityPer100m2 = 5f;
        [Range(0, 50000)] public int globalDesiredCap = 50000;
        [Range(0, 20000)] public int perAreaDesiredCap = 20000;
        [Range(0, 2000)] public int cellDesiredCap = 512;
        public float minimumHeight = .5f;
        public float maximumHeight = 3.5f;
        public float maximumSlopeDegrees = 42f;
        public Color color = new Color(1f, .89f, .43f, 1f);
        [Range(0, 1)] public float colorVariation = .16f;
        [Range(0, 4)] public float glow = 1.3f;
        [Range(.01f, 1)] public float worldSize = .14f;
        [Range(0, 2)] public float motionAmplitude = .55f;
        [Range(0, 4)] public float motionSpeed = .65f;
        [Range(0, .5f)] public float flickerStrength = .23f;
        [Range(0, 2)] public float windInfluence = .15f;
        [Range(0, 1)] public float twilightStart = .25f;
        [Range(0, 1)] public float minimumNightAmount = .08f;
        public bool allowNear = true;
        public FireflyNearMode nearMode = FireflyNearMode.NaturalAreas;
        [Range(0, 48)] public int desiredNearCount = 8;
        public float nearRadius = 7f;
        public float nearCameraMinHeight = 0f;
        public float nearCameraMaxHeight = 22f;
        public FireflyQuality defaultQuality = FireflyQuality.Medium;
        public List<FireflyAreaData> areas = new List<FireflyAreaData>();
    }

    [Serializable] public sealed class FireflyAreaData
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "Скопление светлячков";
        public bool enabled = true;
        public int seed = 1;
        public int priority;
        public FireflyAreaShape shape;
        public Vector2 center;
        public Vector2 radius = new Vector2(12, 8);
        [Range(0, 360)] public float angleDegrees;
        [Range(0, 1)] public float falloff = .35f;
        public FireflyDensityMode densityMode = FireflyDensityMode.Multiply;
        [Range(0, 100)] public float densityValue = 3f;
        public bool overrideColor;
        public Color color = new Color(1f, .92f, .49f);
        public List<Vector2> polygon = new List<Vector2>();
    }

    /// <summary>Pure deterministic placement and area blending. UnityEngine.Random is never used.</summary>
    public static class FireflyDistribution
    {
        public static uint Hash(int x, int z, int seed, int stream = 0)
        {
            unchecked
            {
                uint n = (uint)seed ^ ((uint)x * 0x9E3779B9u) ^
                    ((uint)z * 0x85EBCA6Bu) ^ ((uint)stream * 0xC2B2AE35u);
                n ^= n >> 16; n *= 0x7FEB352Du; n ^= n >> 15;
                n *= 0x846CA68Bu; return n ^ (n >> 16);
            }
        }
        public static float Random01(int x, int z, int seed, int stream = 0) =>
            (Hash(x, z, seed, stream) >> 8) * (1f / 16777216f);

        public static float Influence(FireflyAreaData a, Vector2 point)
        {
            if (a == null || !a.enabled) return 0;
            float normalized;
            if (a.shape == FireflyAreaShape.Ellipse)
            {
                float angle = -a.angleDegrees * Mathf.Deg2Rad;
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                Vector2 d = point - a.center;
                float x = (d.x*c - d.y*s) / Mathf.Max(.01f, a.radius.x);
                float y = (d.x*s + d.y*c) / Mathf.Max(.01f, a.radius.y);
                normalized = Mathf.Sqrt(x*x + y*y);
            }
            else
            {
                if (a.polygon == null || a.polygon.Count < 3 || !Contains(a.polygon, point)) return 0;
                float nearest = float.MaxValue;
                for (int i = 0; i < a.polygon.Count; i++)
                {
                    Vector2 p = a.polygon[i], q = a.polygon[(i+1) % a.polygon.Count];
                    Vector2 d = q-p;
                    float t = Mathf.Clamp01(Vector2.Dot(point-p,d) / Mathf.Max(.0001f,d.sqrMagnitude));
                    nearest = Mathf.Min(nearest, Vector2.Distance(point,p+t*d));
                }
                // Polygon falloff thickness is relative to the area's nominal radius.
                float border = Mathf.Max(.1f, Mathf.Min(a.radius.x, a.radius.y) * Mathf.Max(.001f,a.falloff));
                return a.falloff <= 0 ? 1 : Mathf.SmoothStep(0,1,nearest/border);
            }
            if (normalized >= 1) return 0;
            float inner = 1f - Mathf.Clamp01(a.falloff);
            return normalized <= inner ? 1 : Mathf.SmoothStep(0, 1, (1-normalized)/Mathf.Max(.001f,a.falloff));
        }

        static bool Contains(List<Vector2> p, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = p.Count-1; i<p.Count; j=i++)
            {
                Vector2 a=p[i],b=p[j];
                if ((a.y>point.y)!=(b.y>point.y) &&
                    point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
            }
            return inside;
        }

        /// <summary>Exclusion wins, then lower priority to higher, then stable ID on ties.</summary>
        public static Color ColorAt(FireflyMapData data,Vector2 point)
        {
            Color result=data.color;
            FireflyAreaData best=null;
            float influence=0;
            if(data.areas==null)return result;
            foreach(var a in data.areas)
            {
                if(a==null||!a.overrideColor||!a.enabled)continue;
                float k=Influence(a,point);
                if(k<=0)continue;
                if(best==null||a.priority>best.priority||
                    a.priority==best.priority&&string.CompareOrdinal(a.id,best.id)>0)
                {best=a;influence=k;}
            }
            if(best!=null)result=Color.Lerp(result,best.color,influence);
            return result;
        }

        public static float Density(FireflyMapData data, Vector2 point)
        {
            if (data==null || !data.enabled) return 0;
            float density = Mathf.Max(0,data.densityPer100m2);
            var areas = data.areas;
            if (areas == null) return density;
            float exclude=1f;
            // Exclusion always wins, and must be applied exactly once.
            for (int i=0;i<areas.Count;i++)
                if(areas[i]!=null && areas[i].densityMode==FireflyDensityMode.Exclude)
                    exclude*=1f-Influence(areas[i],point);
            // A bounded authored list is deliberate; renderer caches this per resident cell.
            for (int i=0;i<areas.Count;i++)
            {
                var a=areas[i];
                if (a==null || a.densityMode==FireflyDensityMode.Exclude ||
                    a.densityMode==FireflyDensityMode.Inherit) continue;
                float influence=Influence(a,point);
                if (influence<=0) continue;
                // Determine whether any other overlapping override takes precedence.
                bool superseded=false;
                for (int j=0;j<areas.Count;j++)
                {
                    var b=areas[j]; if (b==null || b==a ||
                        b.densityMode==FireflyDensityMode.Exclude ||
                        b.densityMode==FireflyDensityMode.Inherit ||
                        Influence(b,point)<=0) continue;
                    if (b.priority>a.priority ||
                        b.priority==a.priority && string.CompareOrdinal(b.id,a.id)>0)
                    { superseded=true;break; }
                }
                if(superseded) continue;
                float target=a.densityMode==FireflyDensityMode.Set?
                    a.densityValue:density * a.densityValue;
                // Exclusions cannot be undone by a higher priority override.
                density=Mathf.Lerp(density,target,influence);
                break;
            }
            return Mathf.Max(0,density*exclude);
        }
    }

    /// <summary>Runtime budget, not saved inside the authored map: player can override safely.</summary>
    [Serializable] public sealed class FireflyRuntimeQuality
    {
        public FireflyQuality preset = FireflyQuality.Medium;
        [Range(0, 1)] public float graphicsVsFps = .5f;
        public int customWorldCap = 6000;
        public int customNearCap = 12;
        public float customDistance = 95f;
        public float refreshSeconds = .2f;

        public void Resolve(out int world, out int near, out float distance)
        {
            float t=Mathf.Clamp01(graphicsVsFps);
            switch (preset)
            {
                case FireflyQuality.Off: world=0;near=0;distance=0;return;
                case FireflyQuality.Low: world=450;near=0;distance=55;return;
                case FireflyQuality.Medium: world=1800;near=8;distance=95;return;
                case FireflyQuality.High: world=6500;near=24;distance=155;return;
                default:
                    world=Mathf.Clamp(customWorldCap,0,50000);
                    near=Mathf.Clamp(customNearCap,0,48);
                    distance=Mathf.Clamp(customDistance,5,300);return;
            }
        }
        public void ResolveAdaptive(out int world, out int near, out float distance)
        {
            Resolve(out int baseWorld,out int baseNear,out float baseDistance);
            // User-facing slider is meaningful on all active presets, without hidden multipliers.
            float factor=Mathf.Lerp(.4f,1.6f,Mathf.Clamp01(graphicsVsFps));
            world=Mathf.Clamp(Mathf.RoundToInt(baseWorld*factor),0,50000);
            near=Mathf.Clamp(Mathf.RoundToInt(baseNear*factor),0,48);
            distance=baseDistance*Mathf.Lerp(.75f,1.2f,Mathf.Clamp01(graphicsVsFps));
        }
    }
}
