using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Disposable, camera-resident stones derived from sandy rivers. Never enters map saves,
    /// object IDs, colliders or river cliff layout. Owns one instanced scatter adapter.
    /// </summary>
    public sealed class RiverShoreDecorationPresentation : IDisposable
    {
        readonly MapEditSession session;
        readonly MeadowScatterLibrary library;
        readonly Dictionary<ChunkCoordinate,List<WorldSpawnData>> regions = new Dictionary<ChunkCoordinate,List<WorldSpawnData>>();
        readonly HashSet<ChunkCoordinate> dirty = new HashSet<ChunkCoordinate>();
        readonly List<WorldSpawnData> combined = new List<WorldSpawnData>();
        readonly GameObject root;
        readonly MeadowScatterRenderer renderer;
        bool disposed,settled;
        Vector3 lastEye;
        float lastDistance;
        int lastMaximumRegions;

        public float maximumDistance = 60;
        public int maximumRegions = 36, maximumInstancesPerRegion = 96, rebuildBudget = 4;
        public float candidateSpacing = 1.25f, density = .24f;
        public float shoreInset = .15f, pathClearance = .35f, minimumSandCoverage = .25f;
        public float scaleMultiplier = .75f, maximumStoneSize = .8f;
        public Func<float,float,float,bool> placementAllowed;
        public int ResidentRegions => regions.Count;
        public int ResidentInstances => combined.Count;
        public int LastRebuiltRegions { get; private set; }
        public MeadowScatterLibrary SourceLibrary => library;
        /// <summary>Read-only diagnostic snapshot of derived presentation records, never authoritative spawns.</summary>
        public IReadOnlyList<WorldSpawnData> Placements => combined.AsReadOnly();

        public RiverShoreDecorationPresentation(MapEditSession session, MeadowScatterLibrary library)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.library = library;
            root = new GameObject("Derived river shore stones") { hideFlags = HideFlags.HideAndDontSave };
            renderer = root.AddComponent<MeadowScatterRenderer>();
            renderer.library = library;
            session.Changed += Changed;
        }

        void Changed(MapRegionChange change)
        {
            if ((change.kind & (MapChangeKind.Rivers | MapChangeKind.Height | MapChangeKind.Surface)) == 0) return;
            // A stone footprint can straddle a region boundary. Invalidate its owner too.
            float padding = Mathf.Max(0, maximumStoneSize) + Mathf.Max(0, pathClearance) + Mathf.Max(0, shoreInset);
            Rect area = change.bounds; area.xMin -= padding; area.yMin -= padding; area.xMax += padding; area.yMax += padding;
            foreach (var key in regions.Keys) if (MapEditSession.RegionBounds(key).Overlaps(area)) dirty.Add(key);
            // Stop drawing stale stones immediately, even if rebuilds are budgeted over later ticks.
            if (dirty.Count > 0)
            {
                foreach (var key in dirty) if (regions.TryGetValue(key, out var list)) list.Clear();
                Submit();
            }
        }

        /// <summary>Call from the owner's camera update. Force refreshes resident options/library edits.</summary>
        public void Tick(Vector3 eye, bool force = false)
        {
            if (disposed) return;
            LastRebuiltRegions = 0;
            if(!force && settled && dirty.Count==0 && (eye-lastEye).sqrMagnitude<1f && lastDistance==maximumDistance && lastMaximumRegions==maximumRegions)return;
            lastEye=eye;lastDistance=maximumDistance;lastMaximumRegions=maximumRegions;
            float distance = Mathf.Clamp(maximumDistance, 1, 256);
            var map = session.Bounds;
            var bounds = new Rect(map.x, map.y, map.z, map.w);
            var area = new Rect(eye.x - distance, eye.z - distance, distance * 2, distance * 2);
            var wanted = new List<ChunkCoordinate>();
            foreach (var key in MapEditSession.Regions(area))
            {
                var region = MapEditSession.RegionBounds(key);
                if (region.Overlaps(bounds)) wanted.Add(key);
            }
            wanted.Sort((a,b) =>
            {
                int order = (MapEditSession.RegionBounds(a).center - new Vector2(eye.x,eye.z)).sqrMagnitude.CompareTo(
                    (MapEditSession.RegionBounds(b).center - new Vector2(eye.x,eye.z)).sqrMagnitude);
                if (order != 0) return order;
                order = a.x.CompareTo(b.x); return order != 0 ? order : a.z.CompareTo(b.z);
            });
            int budget = Mathf.Clamp(maximumRegions, 0, 81);
            if (wanted.Count > budget) wanted.RemoveRange(budget, wanted.Count - budget);
            var keep = new HashSet<ChunkCoordinate>(wanted);
            bool changed = false;
            var release = new List<ChunkCoordinate>();
            foreach (var key in regions.Keys) if (!keep.Contains(key)) release.Add(key);
            foreach (var key in release) { regions.Remove(key); dirty.Remove(key); changed = true; }
            if (force) foreach (var key in regions.Keys) dirty.Add(key);
            foreach (var key in wanted)
            {
                if (regions.ContainsKey(key) && !dirty.Contains(key)) continue;
                if (LastRebuiltRegions >= Mathf.Clamp(rebuildBudget, 1, 81)) continue;
                regions[key] = BuildRegion(key);
                dirty.Remove(key); LastRebuiltRegions++; changed = true;
            }
            settled=dirty.Count==0;foreach(var key in wanted)if(!regions.ContainsKey(key)){settled=false;break;}
            renderer.maximumDistance = distance;
            if (changed) Submit();
        }

        void Submit()
        {
            combined.Clear();
            foreach (var pair in regions) combined.AddRange(pair.Value);
            combined.Sort((a,b) => a.stableId.CompareTo(b.stableId));
            renderer.InitializeSpawns(library, combined);
        }

        List<WorldSpawnData> BuildRegion(ChunkCoordinate key)
        {
            var result = new List<WorldSpawnData>();
            if (!library || library.prototypes == null || maximumInstancesPerRegion <= 0) return result;
            var prototypes = new List<MeadowScatterLibrary.Prototype>();
            float weight = 0;
            foreach (var p in library.prototypes)
            {
                if (!ValidStone(p)) continue;
                prototypes.Add(p); weight += p.weight;
            }
            if (weight <= 0) return result;
            // Library order does not affect the derived random stream or prototype choice.
            prototypes.Sort((a,b) => string.CompareOrdinal(a.stableId,b.stableId));
            var region = MapEditSession.RegionBounds(key);
            var paths = session.QueryRiverPaths(region);
            if (paths.Count == 0) return result;
            var surfaceArea = region;
            float padding = Mathf.Max(0, maximumStoneSize) + Mathf.Max(0, pathClearance);
            surfaceArea.xMin -= padding; surfaceArea.yMin -= padding; surfaceArea.xMax += padding; surfaceArea.yMax += padding;
            var strokes = session.QuerySurface(surfaceArea);
            float spacing = Mathf.Clamp(candidateSpacing, .5f, 8);
            // World lattice and half-open ownership prevent duplicates on seams and at negative coordinates.
            for (int z = Mathf.FloorToInt(region.yMin / spacing); z <= Mathf.FloorToInt(region.yMax / spacing); z++)
            for (int x = Mathf.FloorToInt(region.xMin / spacing); x <= Mathf.FloorToInt(region.xMax / spacing); x++)
            foreach (var path in paths)
            {
                if (path.record.layoutVersion < 2) continue;
                ulong id = StableId(path.record.id, path.record.seed, x, z);
                float wx = (x + .15f + Random01(id,1) * .7f) * spacing;
                float wz = (z + .15f + Random01(id,2) * .7f) * spacing;
                if (!region.Contains(new Vector2(wx,wz)) || Random01(id,3) >= Mathf.Clamp01(density)) continue;
                if (!path.Nearest(new Vector2(wx,wz), out var frame, out float signed)) continue;
                if ((signed >= 0 ? frame.leftSand : frame.rightSand) < .5f || frame.ford > .1f) continue;
                float pick = Random01(id,4) * weight;
                var chosen = prototypes[prototypes.Count - 1];
                foreach (var p in prototypes) { pick -= p.weight; if (pick <= 0) { chosen = p; break; } }
                float size = Mathf.Max(chosen.localBounds.size.x, Mathf.Max(chosen.localBounds.size.y,chosen.localBounds.size.z));
                float scale = Mathf.Min(Mathf.Lerp(chosen.scaleRange.x,chosen.scaleRange.y,Random01(id,5)) * Mathf.Max(.01f,scaleMultiplier),
                    Mathf.Clamp(maximumStoneSize,.05f,2) / Mathf.Max(.01f,size));
                float radius = Mathf.Max(chosen.clearanceRadius * scale, chosen.localBounds.extents.magnitude * scale);
                // VegetationAllowed excludes the whole sand slope by design; stones instead use this
                // dry footprint contract. Never bypass the water check just because the centre is dry.
                if (!ClearFootprint(wx,wz,radius,frame,strokes)) continue;
                if (placementAllowed != null && !placementAllowed(wx,wz,radius)) continue;
                if (Vector3.Angle(Vector3.up,session.SampleNormal(wx,wz)) > chosen.maximumSlope) continue;
                result.Add(new WorldSpawnData(unchecked((long)id),chosen.stableId,SpawnCategory.Decoration,
                    new Vector3(wx,session.SampleHeight(wx,wz),wz),Random01(id,6)*360,scale));
                break;
            }
            result.Sort((a,b) => a.stableId.CompareTo(b.stableId));
            int limit = Mathf.Clamp(maximumInstancesPerRegion,0,256);
            if (result.Count > limit) result.RemoveRange(limit,result.Count-limit);
            return result;
        }

        bool ClearFootprint(float x,float z,float radius,MapRiverFrame frame,List<SurfacePaintStroke> strokes)
        {
            var b = session.Bounds;
            for (int i=0;i<9;i++)
            {
                float angle = (i-1)*Mathf.PI/4;
                float px = x + (i==0?0:Mathf.Cos(angle)*(radius+Mathf.Max(0,shoreInset)));
                float pz = z + (i==0?0:Mathf.Sin(angle)*(radius+Mathf.Max(0,shoreInset)));
                if (px<b.x || pz<b.y || px>=b.x+b.z || pz>=b.y+b.w ||
                    session.SandCoverage(px,pz)<Mathf.Clamp01(minimumSandCoverage) ||
                    session.QueryWater(new Vector2(px,pz),out _,out _,out _) ||
                    session.SampleHeight(px,pz)<=frame.water+.03f) return false;
                // Expand road footprint independently of the dry-bank inset.
                float roadX=x+(i==0?0:Mathf.Cos(angle)*(radius+Mathf.Max(0,pathClearance)));
                float roadZ=z+(i==0?0:Mathf.Sin(angle)*(radius+Mathf.Max(0,pathClearance)));
                if (RoadCoverage(roadX,roadZ,strokes)>.08f) return false;
            }
            return true;
        }

        static float RoadCoverage(float x,float z,List<SurfacePaintStroke> strokes)
        {
            float road=0;
            foreach (var stroke in strokes)
            for (int i=0;i<stroke.points.Count;i++)
            {
                float distance=ChunkTerrainVertexMasks.DistanceToSegment(new Vector2(x,z),stroke.points[Mathf.Max(0,i-1)],stroke.points[i],out _);
                float coverage=MapBrushFalloff.SurfaceWeight(stroke,distance);
                if (!stroke.erase) road=stroke.road?Mathf.Max(road,coverage):Mathf.Min(road,1-coverage);
            }
            return road;
        }

        static bool ValidStone(MeadowScatterLibrary.Prototype p)
        {
            if (p==null || !p.enabled || p.category!=MeadowScatterCategory.Stone || p.weight<=0 ||
                string.IsNullOrEmpty(p.stableId) || p.scaleRange.x<=0 || p.scaleRange.y<p.scaleRange.x ||
                p.lodMeshes==null || p.lodMeshes.Length!=4 || p.materials==null) return false;
            foreach (var mesh in p.lodMeshes) if (!mesh || mesh.subMeshCount>p.materials.Length) return false;
            foreach (var material in p.materials) if (!material) return false;
            return true;
        }

        public static ulong StableId(long riverId,int seed,int worldCellX,int worldCellZ)
        {
            unchecked
            {
                ulong hash=14695981039346656037UL;
                hash=(hash^(ulong)riverId)*1099511628211UL;
                hash=(hash^(uint)seed)*1099511628211UL;
                hash=(hash^(uint)worldCellX)*1099511628211UL;
                return (hash^(uint)worldCellZ)*1099511628211UL;
            }
        }
        static float Random01(ulong id,uint channel)
        {
            unchecked
            {
                uint hash=(uint)id^(uint)(id>>32)^channel*0x9e3779b9u;
                hash^=hash>>16; hash*=0x7feb352du; hash^=hash>>15; hash*=0x846ca68bu; hash^=hash>>16;
                return (hash&0xffffffu)/16777216f;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed=true; session.Changed-=Changed;
            regions.Clear(); dirty.Clear(); combined.Clear();
            if (root) { root.SetActive(false); if (Application.isPlaying) UnityEngine.Object.Destroy(root); else UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
