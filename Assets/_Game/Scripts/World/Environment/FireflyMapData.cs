using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World.Environment
{
    public enum FireflyQuality { Off, Low, Medium, High, Custom }
    public enum FireflyAreaShape { Ellipse, Polygon }
    public enum FireflyDensityMode { Inherit, Multiply, Set, Exclude }

    /// <summary>Map-owned appearance and density. All distances are metres and densities are points/m².</summary>
    [Serializable] public sealed class FireflySettings
    {
        public bool enabled = true;
        public int seed = 72411;
        [Range(0f, 1f)] public float performanceToQuality = .5f;
        public FireflyQuality quality = FireflyQuality.Medium;
        [Range(0f, .5f)] public float densityPerSquareMetre = .035f;
        [Min(0)] public int maxWorldCount = 10000;
        [Min(1)] public int maxPerCell = 256;
        public float cellSize = 32f;
        public float minHeight = .35f;
        public float maxHeight = 2.7f;
        public float worldSize = .12f;
        public float minPixelSize = .8f;
        public float maxPixelSize = 9f;
        public float nearFadeMetres = .75f;
        public float depthFadeMetres = .65f;
        public Color color = new Color(1f, .91f, .40f, 1f);
        public float colorVariance = .16f;
        public float emission = 1.5f;
        public float haloStrength = .36f;
        public float flightSpeed = 1f;
        public float movementRadius = .65f;
        public float windInfluence = .15f;
        public float flickerSpeed = 1.7f;
        public float flickerDepth = .28f;
        public float nearDistance = 32f;
        public float farDistance = 110f;
        public float minimumNightAmount = .16f;
        public float twilightBoost = .28f;
        public float roadAvoidance = .70f;
        public float winterMultiplier = .2f;
        public float wetWeatherMultiplier = .65f;
        public bool closeLayer = true;
        public bool closeAnywhere;
        public float closeEnableCameraHeight = 16f;
        public int maxCloseCount = 8;
        public float closeRadius = 5f;
        public float closeSizeMultiplier = 1.15f;
        public bool cheapLight = false;
        public bool realtimeLight = false;
        public int maxRealtimeLights = 1;
    }

    [Serializable] public sealed class FireflyArea
    {
        public string id;
        public string name = "Скопление светлячков";
        public bool enabled = true;
        public FireflyAreaShape shape;
        // World X/Z center; ellipse radius stored in size X/Y.
        public Vector2 center;
        public Vector2 radius = new Vector2(10, 7);
        public float rotationDegrees;
        public List<Vector2> polygon = new List<Vector2>();
        public int priority;
        public int seed = 1;
        public float falloff = .2f;
        public FireflyDensityMode densityMode = FireflyDensityMode.Multiply;
        public float densityValue = 3f;
        public bool overrideColor;
        public Color color = new Color(1f, .89f, .36f);
        public bool overrideSpeed;
        public float speedMultiplier = 1f;
        public bool overrideEmission;
        public float emissionMultiplier = 1f;
        public bool overrideMaxDistance;
        public float maxDistance = 110f;
    }

    [Serializable] public sealed class FireflyMapData
    {
        public int version = 1;
        public FireflySettings settings = new FireflySettings();
        public List<FireflyArea> areas = new List<FireflyArea>();
    }

    /// <summary>Pure deterministic density evaluation shared by editor and renderer.</summary>
    public static class FireflyDensity
    {
        public static void Validate(FireflyMapData map, Vector4 bounds)
        {
            if(map == null || map.version != 1 || map.settings == null || map.areas == null || map.areas.Count > 256)
                throw new InvalidDataException("Unsupported firefly map.");
            var s = map.settings;
            if(!Finite(s.densityPerSquareMetre) || s.densityPerSquareMetre < 0 || s.densityPerSquareMetre > .5f ||
               !Finite(s.performanceToQuality) || s.performanceToQuality < 0 || s.performanceToQuality > 1 ||
               s.maxWorldCount < 0 || s.maxWorldCount > 500000 || s.maxPerCell < 1 || s.maxPerCell > 4096 ||
               !Finite(s.cellSize) || s.cellSize < 8 || s.cellSize > 128 ||
               !Finite(s.minHeight) || !Finite(s.maxHeight) || s.minHeight < 0 || s.maxHeight < s.minHeight || s.maxHeight > 100 ||
               !Finite(s.worldSize) || s.worldSize <= 0 || s.worldSize > 3 ||
               !Finite(s.minPixelSize) || s.minPixelSize < 0 || s.minPixelSize > 20 ||
               !Finite(s.maxPixelSize) || s.maxPixelSize < s.minPixelSize || s.maxPixelSize > 50 ||
               !Finite(s.nearFadeMetres) || s.nearFadeMetres < 0 || s.nearFadeMetres > 20 ||
               !Finite(s.depthFadeMetres) || s.depthFadeMetres < 0 || s.depthFadeMetres > 20 ||
               !Finite(s.emission) || s.emission < 0 || s.emission > 12 ||
               !Finite(s.haloStrength) || s.haloStrength < 0 || s.haloStrength > 3 ||
               !Finite(s.flightSpeed) || s.flightSpeed < 0 || s.flightSpeed > 10 ||
               !Finite(s.movementRadius) || s.movementRadius < 0 || s.movementRadius > 8 ||
               !Finite(s.windInfluence) || s.windInfluence < 0 || s.windInfluence > 3 ||
               !Finite(s.flickerSpeed) || s.flickerSpeed < 0 || s.flickerSpeed > 12 ||
               !Finite(s.flickerDepth) || s.flickerDepth < 0 || s.flickerDepth > 1 ||
               !Finite(s.nearDistance) || s.nearDistance < 0 || s.nearDistance > 1000 ||
               !Finite(s.farDistance) || s.farDistance < s.nearDistance || s.farDistance > 2000 ||
               !Finite(s.minimumNightAmount) || s.minimumNightAmount < 0 || s.minimumNightAmount > 1 ||
               !Finite(s.twilightBoost) || s.twilightBoost < 0 || s.twilightBoost > 1 ||
               !Finite(s.roadAvoidance) || s.roadAvoidance < 0 || s.roadAvoidance > 1 ||
               !Finite(s.winterMultiplier) || s.winterMultiplier < 0 || s.winterMultiplier > 2 ||
               !Finite(s.wetWeatherMultiplier) || s.wetWeatherMultiplier < 0 || s.wetWeatherMultiplier > 2 ||
               !Finite(s.closeEnableCameraHeight) || s.closeEnableCameraHeight < 0 || s.closeEnableCameraHeight > 500 ||
               s.maxCloseCount < 0 || s.maxCloseCount > 128 ||
               !Finite(s.closeRadius) || s.closeRadius < 1 || s.closeRadius > 30 ||
               !Finite(s.closeSizeMultiplier) || s.closeSizeMultiplier <= 0 || s.closeSizeMultiplier > 5 ||
               s.maxRealtimeLights < 0 || s.maxRealtimeLights > 4 ||
               !ColorValid(s.color) || !Finite(s.colorVariance) || s.colorVariance < 0 || s.colorVariance > 1)
                throw new InvalidDataException("Invalid firefly settings.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach(var area in map.areas)
            {
                if(area == null || string.IsNullOrWhiteSpace(area.id) || area.id.Length > 128 || !ids.Add(area.id) ||
                   area.name == null || area.name.Length > 128 ||
                   !Finite(area.center.x) || !Finite(area.center.y) ||
                   area.center.x < bounds.x || area.center.x > bounds.x+bounds.z ||
                   area.center.y < bounds.y || area.center.y > bounds.y+bounds.w ||
                   !Finite(area.radius.x) || !Finite(area.radius.y) || area.radius.x < .5f || area.radius.y < .5f ||
                   area.radius.x > 2048 || area.radius.y > 2048 ||
                   !Finite(area.rotationDegrees) || !Finite(area.falloff) || area.falloff < 0 || area.falloff > 1 ||
                   !Finite(area.densityValue) || area.densityValue < 0 || area.densityValue > 100 ||
                   !Finite(area.speedMultiplier) || area.speedMultiplier < 0 || area.speedMultiplier > 10 ||
                   !Finite(area.emissionMultiplier) || area.emissionMultiplier < 0 || area.emissionMultiplier > 10 ||
                   !Finite(area.maxDistance) || area.maxDistance < 0 || area.maxDistance > 2000 ||
                   !ColorValid(area.color) || area.polygon == null || area.polygon.Count > 128 ||
                   (int)area.shape < 0 || (int)area.shape > 1 || (int)area.densityMode < 0 || (int)area.densityMode > 3)
                    throw new InvalidDataException("Invalid firefly area.");
                if(area.shape == FireflyAreaShape.Polygon && area.polygon.Count < 3)
                    throw new InvalidDataException("Polygon area needs at least three vertices.");
                foreach(var p in area.polygon)
                    if(!Finite(p.x) || !Finite(p.y) || p.x < bounds.x || p.x > bounds.x+bounds.z || p.y < bounds.y || p.y > bounds.y+bounds.w)
                        throw new InvalidDataException("Firefly polygon vertex outside map.");
            }
        }
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        static bool ColorValid(Color c) => Finite(c.r) && Finite(c.g) && Finite(c.b) && Finite(c.a) && c.r>=0 && c.g>=0 && c.b>=0 && c.r<=4 && c.g<=4 && c.b<=4;

        // Exclusion wins. Otherwise highest priority first, stable ID breaks ties.
        // Smooth edge fades apply even to Set and Multiply modes.
        public static float DensityAt(FireflyMapData map, Vector2 p, out FireflyArea winner)
        {
            float density = map.settings.densityPerSquareMetre;
            winner = null;
            float bestWeight = 0f;
            foreach(var area in map.areas)
            {
                if(!area.enabled) continue;
                float weight = Weight(area,p);
                if(weight <= 0) continue;
                if(area.densityMode == FireflyDensityMode.Exclude) return 0f;
                if(area.densityMode == FireflyDensityMode.Inherit) continue;
                if(winner != null && (area.priority < winner.priority ||
                    area.priority == winner.priority && string.CompareOrdinal(area.id,winner.id)>0)) continue;
                winner = area; bestWeight = weight;
            }
            if(winner == null) return density;
            float target = winner.densityMode == FireflyDensityMode.Set
                ? winner.densityValue : density*winner.densityValue;
            return Mathf.Lerp(density,target,bestWeight);
        }
        public static float Weight(FireflyArea a, Vector2 point)
        {
            float normalized;
            if(a.shape == FireflyAreaShape.Ellipse)
            {
                float angle = -a.rotationDegrees*Mathf.Deg2Rad;
                float sn=Mathf.Sin(angle), cs=Mathf.Cos(angle);
                float x=point.x-a.center.x, z=point.y-a.center.y;
                float u=(x*cs-z*sn)/a.radius.x, v=(x*sn+z*cs)/a.radius.y;
                normalized = Mathf.Sqrt(u*u+v*v);
            }
            else
            {
                bool inside = false; float nearest = float.MaxValue;
                var pts=a.polygon;
                for(int i=0,j=pts.Count-1;i<pts.Count;j=i++)
                {
                    Vector2 v=pts[i],w=pts[j];
                    if((v.y>point.y)!=(w.y>point.y) && point.x<(w.x-v.x)*(point.y-v.y)/(w.y-v.y)+v.x)inside=!inside;
                    Vector2 edge=w-v;
                    float t=Mathf.Clamp01(Vector2.Dot(point-v,edge)/Mathf.Max(edge.sqrMagnitude,.00001f));
                    nearest=Mathf.Min(nearest,Vector2.Distance(point,v+edge*t));
                }
                if(!inside)return 0;
                if(a.falloff<=0)return 1;
                float maxEdge=Mathf.Max(.5f,Mathf.Min(a.radius.x,a.radius.y))*a.falloff;
                return Mathf.SmoothStep(0,1,nearest/maxEdge);
            }
            if(normalized>=1)return 0;
            if(a.falloff<=0)return 1;
            return Mathf.SmoothStep(0,1,(1-normalized)/a.falloff);
        }

        public static uint Hash(uint x)
        {
            x^=x>>16; x*=0x7feb352du; x^=x>>15; x*=0x846ca68bu; x^=x>>16;
            return x;
        }
        public static float Random01(uint v) => (Hash(v)&0xffffffu)/16777216f;
    }
}
