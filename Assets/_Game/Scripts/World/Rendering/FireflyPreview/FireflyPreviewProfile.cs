using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Rendering.Fireflies
{
    public enum FireflyPreviewQuality { Off, Low, Medium, High, Custom }
    public enum FireflyPreviewZoneShape { Ellipse, Polygon }
    public enum FireflyPreviewDensityMode { Inherit, Multiply, Set, Exclude }
    public enum FireflyPreviewNearMode { NaturalZones, Everywhere }

    [Serializable]
    public sealed class FireflyPreviewZone
    {
        public string id;
        public string displayName = "Скопление светлячков";
        public bool enabled = true;
        public FireflyPreviewZoneShape shape;
        public Vector2 center;
        public Vector2 radii = new Vector2(12f, 8f);
        public float rotationDegrees;
        [Range(0f, 1f)] public float falloff = .35f;
        public int priority;
        public int seed;
        public FireflyPreviewDensityMode densityMode = FireflyPreviewDensityMode.Multiply;
        [Min(0f)] public float densityValue = 4f;
        public List<Vector2> polygon = new List<Vector2>();
        public bool overrideColor;
        public Color color = new Color(1f, .86f, .31f);
    }

    /// <summary>
    /// Standalone preview asset, intentionally NOT part of MapEditSession or map saves.
    /// Copy these authored settings into the eventual map integration only after visual sign-off.
    /// Units: density / 100 m², distances in metres, time in real seconds.
    /// </summary>
    [CreateAssetMenu(fileName = "FireflyPreview", menuName = "Little Castle/Firefly Preview Profile")]
    public sealed class FireflyPreviewProfile : ScriptableObject
    {
        public bool enabled = true;
        public int seed = 73421;
        public Vector4 mapBounds = new Vector4(-48f, -40f, 96f, 104f);
        [Range(0f, 80f)] public float densityPer100SquareMetres = 4f;
        [Range(0, 50000)] public int desiredGlobalCap = 10000;
        [Range(1, 1023)] public int desiredCellCap = 512;
        public float minimumHeight = .45f;
        public float maximumHeight = 2.8f;
        [Range(0f, 90f)] public float maximumSlopeDegrees = 55f;
        [Range(.01f, 1f)] public float worldDiameter = .13f;
        public Color coreColor = new Color(1f, .89f, .38f);
        [Range(0f, 1f)] public float colorVariance = .15f;
        [Range(0f, 5f)] public float emission = 1.4f;
        [Range(0f, 2f)] public float movementAmplitude = .6f;
        [Range(0f, 5f)] public float movementSpeed = .7f;
        [Range(0f, .5f)] public float flickerAmount = .22f;
        [Range(0f, .8f)] public float motionPauseFraction = .22f;
        [Range(2f, 25f)] public float motionCycleSeconds = 7f;
        [Range(.05f, .32f)] public float brightCoreRadius = .13f;
        [Range(0f, .5f)] public float haloStrength = .20f;
        [Range(3f, 20f)] public float maxDiameterPixels = 11f;
        [Range(.25f, .95f)] public float farLodStartFraction = .58f;
        [Range(.05f, 1f)] public float farLodSurvivalFraction = .28f;
        [Range(.1f, 1f)] public float farBrightnessFraction = .65f;
        [Range(0f, 1f)] public float minimumNightAmount = .10f;
        [Range(0f, 2f)] public float windResponse = .12f;
        [Header("Distant fireflies (stable, shuffled LOD)")]
        [Range(0f, .9f)] public float lodStartFraction = .42f;
        [Range(.1f, 1f)] public float lodEndFraction = .88f;
        [Range(.05f, 1f)] public float lodMinimumFraction = .23f;
        public bool nearEnabled = true;
        public FireflyPreviewNearMode nearMode = FireflyPreviewNearMode.NaturalZones;
        [Range(0, 48)] public int desiredNearCount = 8;
        [Range(1f, 20f)] public float nearRadius = 7f;
        [Range(0f, 100f)] public float maximumCameraHeightForNear = 22f;
        public List<FireflyPreviewZone> areas = new List<FireflyPreviewZone>();

        public bool Includes(float x, float z) =>
            x >= mapBounds.x && z >= mapBounds.y &&
            x <= mapBounds.x + mapBounds.z && z <= mapBounds.y + mapBounds.w;

        public void Validate()
        {
            mapBounds.z = Mathf.Clamp(mapBounds.z, .1f, 100000f);
            mapBounds.w = Mathf.Clamp(mapBounds.w, .1f, 100000f);
            densityPer100SquareMetres = Mathf.Clamp(densityPer100SquareMetres, 0f, 80f);
            desiredGlobalCap = Mathf.Clamp(desiredGlobalCap, 0, 50000);
            desiredCellCap = Mathf.Clamp(desiredCellCap, 1, 1023);
            lodStartFraction = Mathf.Clamp(lodStartFraction, 0f, .9f);
            lodEndFraction = Mathf.Clamp(lodEndFraction, lodStartFraction + .05f, 1f);
            lodMinimumFraction = Mathf.Clamp(lodMinimumFraction, .05f, 1f);
            minimumHeight = Mathf.Clamp(minimumHeight, 0f, 50f);
            maximumHeight = Mathf.Clamp(maximumHeight, minimumHeight, 100f);
            maximumSlopeDegrees = Mathf.Clamp(maximumSlopeDegrees, 0f, 90f);
            desiredNearCount = Mathf.Clamp(desiredNearCount, 0, 48);
            motionPauseFraction = Mathf.Clamp(motionPauseFraction, 0f, .8f);
            motionCycleSeconds = Mathf.Clamp(motionCycleSeconds, 2f, 25f);
            brightCoreRadius = Mathf.Clamp(brightCoreRadius, .05f, .32f);
            haloStrength = Mathf.Clamp(haloStrength, 0f, .5f);
            maxDiameterPixels = Mathf.Clamp(maxDiameterPixels, 3f, 20f);
            farLodStartFraction = Mathf.Clamp(farLodStartFraction, .25f, .95f);
            farLodSurvivalFraction = Mathf.Clamp(farLodSurvivalFraction, .05f, 1f);
            farBrightnessFraction = Mathf.Clamp(farBrightnessFraction, .1f, 1f);
            nearRadius = Mathf.Clamp(nearRadius, 1f, 20f);
            if(areas == null) areas = new List<FireflyPreviewZone>();
            for(int i = areas.Count - 1; i >= 0; i--)
            {
                var a = areas[i];
                if(a == null) { areas.RemoveAt(i); continue; }
                a.radii.x = Mathf.Clamp(a.radii.x, .5f, 2048f);
                a.radii.y = Mathf.Clamp(a.radii.y, .5f, 2048f);
                a.falloff = Mathf.Clamp01(a.falloff);
                a.densityValue = Mathf.Clamp(a.densityValue, 0f, 100f);
                if(a.polygon == null) a.polygon = new List<Vector2>();
            }
        }
        private void OnValidate() => Validate();
    }

    [Serializable]
    public sealed class FireflyPreviewBudget
    {
        public FireflyPreviewQuality quality = FireflyPreviewQuality.Medium;
        [Range(0f, 1f)] public float graphicsVsPerformance = .5f;
        [Range(0, 50000)] public int customWorldCap = 4500;
        [Range(0, 48)] public int customNearCap = 12;
        [Range(1f, 300f)] public float customRangeMetres = 100f;
        // Runtime-only FPS guard: adjusts this preview, never global URP/game quality.
        public bool adaptiveEnabled = true;
        [Range(20f, 144f)] public float targetFps = 60f;
        [Range(.2f, 1f)] public float minimumAdaptiveScale = .35f;
        [Header("Automatic FPS protection (Game camera / Play Mode only)")]
        public bool automaticFpsBudget;
        [Range(30, 144)] public int targetFps = 60;
        [Range(.2f, .9f)] public float minimumAutomaticScale = .4f;

        public void Resolve(out int world, out int near, out float range)
        {
            switch(quality)
            {
                case FireflyPreviewQuality.Off: world=0;near=0;range=0;return;
                case FireflyPreviewQuality.Low: world=400;near=0;range=55;break;
                case FireflyPreviewQuality.Medium: world=1800;near=8;range=95;break;
                case FireflyPreviewQuality.High: world=6000;near=24;range=155;break;
                default: world=Mathf.Clamp(customWorldCap,0,50000);
                    near=Mathf.Clamp(customNearCap,0,48);
                    range=Mathf.Clamp(customRangeMetres,1f,300f);break;
            }
            float t = Mathf.Clamp01(graphicsVsPerformance);
            world = Mathf.Clamp(Mathf.RoundToInt(world*Mathf.Lerp(.4f,1.6f,t)),0,50000);
            near = Mathf.Clamp(Mathf.RoundToInt(near*Mathf.Lerp(.4f,1.6f,t)),0,48);
            range *= Mathf.Lerp(.75f,1.2f,t);
        }
    }

    /// <summary>
    /// Small, allocation-free, independent feedback controller. Frame time is a signal,
    /// not a promise: it protects draw budgets but cannot guarantee target FPS.
    /// Used only in Play Mode and only once per rendered game frame.
    /// </summary>
    public sealed class FireflyPreviewAdaptiveBudget
    {
        float smoothedDelta;
        float lastAdjustment;
        int samples;
        public float Scale { get; private set; } = 1f;
        public float SmoothedFps => smoothedDelta > .00001f ? 1f / smoothedDelta : 0f;

        public void Reset()
        {
            smoothedDelta = 0f;
            lastAdjustment = 0f;
            samples = 0;
            Scale = 1f;
        }

        public float Step(float deltaSeconds, float nowSeconds, int targetFps, float minimumScale)
        {
            if(float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ||
               deltaSeconds <= 0f || deltaSeconds > .5f)return Scale;

            minimumScale = Mathf.Clamp(minimumScale, .2f, .9f);
            targetFps = Mathf.Clamp(targetFps, 30, 144);
            if(samples == 0)
            {
                smoothedDelta = deltaSeconds;
                lastAdjustment = nowSeconds;
            }
            else smoothedDelta = Mathf.Lerp(smoothedDelta, deltaSeconds, .08f);
            samples++;
            if(samples < 25 || nowSeconds - lastAdjustment < 1.25f)return Scale;

            float targetDelta = 1f / targetFps;
            // Hysteresis + slow recovery avoid flickering between quality levels.
            if(smoothedDelta > targetDelta * 1.10f)
                Scale = Mathf.Max(minimumScale, Scale - .12f);
            else if(smoothedDelta < targetDelta * .82f)
                Scale = Mathf.Min(1f, Scale + .05f);
            lastAdjustment = nowSeconds;
            return Scale;
        }
    }

    /// <summary>
    /// Conservative hysteresis-controlled FPS guard. Called at most once per 0.8s,
    /// with a smoothed game-frame FPS or an explicitly simulated editor value.
    /// This is a frame-time proxy, NOT a measurement of firefly GPU cost.
    /// </summary>
    public static class FireflyPreviewAdaptation
    {
        public static float NextScale(float previous, float measuredFps,
            float targetFps, float minimumScale)
        {
            float low = Mathf.Clamp(minimumScale, .2f, 1f);
            float scale = Mathf.Clamp(previous, low, 1f);
            float target = Mathf.Clamp(targetFps, 20f, 144f);
            if(float.IsNaN(measuredFps) || measuredFps <= 0f)
                return scale;
            if(measuredFps < target * .90f)
                return Mathf.Max(low, scale - Mathf.Clamp(
                    (target - measuredFps) / target * .40f, .05f, .15f));
            if(measuredFps > target * 1.12f)
                return Mathf.Min(1f, scale + .04f);
            return scale; // dead band prevents rapid quality oscillation
        }

        public static void ApplyScale(float scale, ref int world,
            ref int near, ref float range)
        {
            float factor = Mathf.Clamp01(scale);
            world = Mathf.RoundToInt(world * factor);
            near = Mathf.RoundToInt(near * factor);
            range *= Mathf.Lerp(.70f, 1f, factor);
        }
    }

    /// <summary>Side-effect-free, repeatable random placement and area blending.</summary>
    public static class FireflyPreviewDistribution
    {
        public static uint Hash(int x, int z, int seed, int stream = 0)
        {
            unchecked
            {
                uint v = (uint)seed ^ ((uint)x*0x9e3779b9u) ^
                    ((uint)z*0x85ebca6bu) ^ ((uint)stream*0xc2b2ae35u);
                v ^= v >> 16; v *= 0x7feb352du; v ^= v >> 15;
                v *= 0x846ca68bu; return v ^ (v >> 16);
            }
        }
        public static float Random01(int x, int z, int seed, int stream = 0) =>
            (Hash(x,z,seed,stream) >> 8) * (1f/16777216f);

        // GPU instances are pre-shuffled within each cell; rendering a shorter prefix
        // therefore covers the whole area rather than clipping one side of the cell.
        public static int LodCount(int cellCount, float distanceFraction,
            float startFraction, float endFraction, float minimumFraction)
        {
            if(cellCount <= 0)return 0;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
                startFraction, Mathf.Max(startFraction + .001f, endFraction), distanceFraction));
            return Mathf.Clamp(Mathf.RoundToInt(cellCount *
                Mathf.Lerp(1f, Mathf.Clamp01(minimumFraction), t)), 1, cellCount);
        }

        public static float Influence(FireflyPreviewZone a, Vector2 at)
        {
            if(a==null || !a.enabled) return 0f;
            if(a.shape==FireflyPreviewZoneShape.Ellipse)
            {
                float rad = -a.rotationDegrees*Mathf.Deg2Rad;
                float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
                Vector2 d = at-a.center;
                float x=(d.x*cs-d.y*sn)/Mathf.Max(.5f,a.radii.x);
                float y=(d.x*sn+d.y*cs)/Mathf.Max(.5f,a.radii.y);
                float dist=Mathf.Sqrt(x*x+y*y);
                if(dist>=1f)return 0f;
                if(a.falloff<=0f)return 1f;
                return Mathf.SmoothStep(0f,1f,(1f-dist)/a.falloff);
            }
            if(a.polygon == null || a.polygon.Count<3)return 0f;
            bool inside=false;
            float edgeDistance=float.MaxValue;
            int n=a.polygon.Count;
            for(int i=0,j=n-1;i<n;j=i++)
            {
                Vector2 v=a.polygon[i],w=a.polygon[j];
                if((v.y>at.y)!=(w.y>at.y)&&at.x<(w.x-v.x)*(at.y-v.y)/(w.y-v.y)+v.x)
                    inside=!inside;
                Vector2 edge=w-v;
                float t=Mathf.Clamp01(Vector2.Dot(at-v,edge)/Mathf.Max(edge.sqrMagnitude,.00001f));
                edgeDistance=Mathf.Min(edgeDistance,Vector2.Distance(at,v+edge*t));
            }
            if(!inside)return 0f;
            if(a.falloff<=0f)return 1f;
            float fadeMetres=Mathf.Max(.1f,Mathf.Min(a.radii.x,a.radii.y)*a.falloff);
            return Mathf.SmoothStep(0f,1f,edgeDistance/fadeMetres);
        }

        public static float Density(FireflyPreviewProfile profile,Vector2 at,out FireflyPreviewZone selected)
        {
            selected=null;
            if(profile==null || !profile.enabled || !profile.Includes(at.x,at.y))return 0f;
            float baseDensity=profile.densityPer100SquareMetres;
            float result=baseDensity;
            float exclusion=1f, selectedWeight=0f;
            var areas=profile.areas;
            if(areas==null)return baseDensity;
            for(int i=0;i<areas.Count;i++)
            {
                var a=areas[i];
                if(a==null || !a.enabled)continue;
                float w=Influence(a,at);
                if(w<=0f)continue;
                if(a.densityMode==FireflyPreviewDensityMode.Exclude)
                {
                    exclusion*=1f-w;
                    continue;
                }
                if(a.densityMode==FireflyPreviewDensityMode.Inherit)continue;
                if(selected!=null && (a.priority<selected.priority ||
                    a.priority==selected.priority && string.CompareOrdinal(a.id,selected.id)<0))
                    continue;
                selected=a;
                selectedWeight=w;
            }
            if(selected!=null)
            {
                float target=selected.densityMode==FireflyPreviewDensityMode.Set
                    ? selected.densityValue : baseDensity*selected.densityValue;
                result=Mathf.Lerp(baseDensity,target,selectedWeight);
            }
            return Mathf.Max(0f,result*exclusion);
        }
    }
}
