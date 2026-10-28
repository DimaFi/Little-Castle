using System;
using System.Collections.Generic;
using LittleCastle.Research;
using LittleCastle.World;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Derived meadow decoration, rebuilt per touched 32m region after painting/undo/load. No prop GameObjects,
    /// colliders or per-prop Update. Authored strokes and runtime strokes use the same mask.
    /// </summary>
    [ExecuteAlways]
    public sealed class MeadowScatterRenderer : MonoBehaviour
    {
        public MeadowScatterLibrary library;
        public MeadowSurfacePainter painter;
        [NonSerialized] public Func<float, float, float> heightSampler;
        [Min(1)] public float maximumDistance = 45;
        public Vector3 lodDistances = new Vector3(12, 23, 34);
        // Per 32m resident region; deterministic budget, independent of edits elsewhere.
        [Range(0, 1500)] public int maximumInstances = 1500;
        [Min(.5f)] public float candidateSpacing = 1.6f;
        [Min(.5f)] public float plantGroupSize = 2.4f;
        public int seed = 417;
        [Range(0, 2)] public float densityMultiplier = 1;
        [Range(0, 1)] public float isolatedChance = .04f;
        [Range(0, 1)] public float clearingChance = .72f;
        [Min(1)] public float clearingRadius = 5.26f;
        [Range(0,3)] public float minimumSeparation = .35f;
        // Optional world/chunk adapter. Authoring uses the existing map session below.
        [NonSerialized] public Func<float,float,BiomeKind> biomeSampler;
        [NonSerialized] public Func<float,float,SurfaceKind> surfaceSampler;
        [NonSerialized] public Func<float,float,float,bool> placementAllowed;
        public int authoredInstances, visiblePatches, submittedInstances, submittedTriangles, drawCalls;
        public bool candidateBudgetReached;
        bool digestDirty=true;
        ulong cachedDigest;
        public ulong placementDigest
        {
            get
            {
                if(!digestDirty)return cachedDigest;
                var all=new List<Placement>();foreach(var pair in regionPlacements)all.AddRange(pair.Value);
                all.Sort(ComparePlacements);cachedDigest=14695981039346656037UL;
                foreach(var p in all){Digest(p.prototype);Digest(Mathf.RoundToInt(p.position.x*10000));Digest(Mathf.RoundToInt(p.position.y*10000));Digest(Mathf.RoundToInt(p.position.z*10000));}
                digestDirty=false;return cachedDigest;
            }
        }
        static int ComparePlacements(Placement a,Placement b)
        {int order=a.priority.CompareTo(b.priority);if(order!=0)return order;order=a.position.x.CompareTo(b.position.x);return order!=0?order:a.position.z.CompareTo(b.position.z);}
        public ulong RegionDigest(ChunkCoordinate key)
        {
            ulong hash=14695981039346656037UL;
            if(regionPlacements.TryGetValue(key,out var placements))foreach(var p in placements)
            {hash^=p.priority;hash=unchecked(hash*1099511628211UL);hash^=unchecked((uint)p.prototype);hash=unchecked(hash*1099511628211UL);}
            return hash;
        }
        internal int CountCategoryInCircle(MeadowScatterCategory category,Vector2 center,float radius)
        {
            int count=0;float squared=radius*radius;
            foreach(var patch in patches)for(int type=0;type<library.prototypes.Length;type++)
            {
                if(library.prototypes[type].category!=category||patch.matrices[type]==null)continue;
                foreach(var matrix in patch.matrices[type]){Vector3 position=matrix.GetColumn(3);float x=position.x-center.x,z=position.z-center.y;if(x*x+z*z<=squared)count++;}
            }
            return count;
        }

        const float PatchSize = 8;
        const int MaximumCandidateCells = 120000;
        readonly Plane[] planes = new Plane[6];
        static readonly ProfilerMarker DrawMarker = new ProfilerMarker("LC.Meadow.ScatterCullAndSubmit");
        static readonly ProfilerMarker RebuildMarker = new ProfilerMarker("LC.Meadow.ScatterRebuild");

        sealed class Patch
        {
            public Bounds bounds;
            public Matrix4x4[][] matrices;
        }

        sealed class Batch
        {
            public Matrix4x4[] matrices;
            public int count;
            public Bounds bounds;
        }

        sealed class StrokeRegion
        {
            public SurfacePaintStroke stroke;
            public Rect bounds;
            public float radius;
        }

        struct Placement
        {
            public int prototype;
            public Vector3 position;
            public Matrix4x4 matrix;
            public Bounds bounds;
            public uint priority;
        }

        readonly Dictionary<ChunkCoordinate,List<Placement>> regionPlacements=new Dictionary<ChunkCoordinate,List<Placement>>();
        readonly Dictionary<long,Patch> patchCache=new Dictionary<long,Patch>();
        public int lastRebuiltRegions {get;private set;}
        Patch[] patches = Array.Empty<Patch>();
        Batch[,] batches;
        Vector2 samplingOrigin;
        MapEditSession placementSession;

        void OnEnable() { RenderPipelineManager.beginCameraRendering += Draw; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= Draw; }
        void Start() { if (patches.Length == 0 && painter && painter.mask) Rebuild(painter); }

        [ContextMenu("Rebuild meadow flower placements")]
        public void RebuildCurrent(){if(painter&&painter.mask)Rebuild(painter);}

        public void InitializeSpawns(MeadowScatterLibrary source,IReadOnlyList<WorldSpawnData> spawns,WorldChunkData chunk=null,float chunkWorldSize=0)
        {
            library=source;painter=null;regionPlacements.Clear();patchCache.Clear();digestDirty=true;
            var placements=new List<Placement>();
            if(source&&source.prototypes!=null)foreach(var spawn in spawns)
            {
                for(int i=0;i<source.prototypes.Length;i++)
                {
                    var p=source.prototypes[i];if(p==null||p.stableId!=spawn.archetypeId)continue;
                    if(!Valid(p))break;
                    Vector3 normal=Vector3.up;
                    if(chunk!=null&&chunkWorldSize>0)
                    {
                        float cellSize=chunkWorldSize/chunk.CellsPerSide;
                        int cx=Mathf.Clamp(Mathf.FloorToInt((spawn.worldPosition.x-chunk.Coordinate.x*chunkWorldSize)/cellSize),0,chunk.CellsPerSide-1);
                        int cz=Mathf.Clamp(Mathf.FloorToInt((spawn.worldPosition.z-chunk.Coordinate.z*chunkWorldSize)/cellSize),0,chunk.CellsPerSide-1);
                        if(!p.allowedBiomes.Contains(chunk.GetBiome(cx,cz)))break;
                        var surface=chunk.GetSurface(cx,cz);
                        if(!(surface==SurfaceKind.Grass&&p.allowGrass||surface==SurfaceKind.WoodlandFloor&&p.allowWoodlandFloor||surface==SurfaceKind.Mud&&p.allowMud))break;
                        normal=WorldChunkSampling.SampleTriangleNormal(chunk,chunkWorldSize,spawn.worldPosition.x,spawn.worldPosition.z);
                        if(Vector3.Angle(Vector3.up,normal)>p.maximumSlope)break;
                    }
                    var rotation=Quaternion.FromToRotation(Vector3.up,Vector3.RotateTowards(Vector3.up,normal,Mathf.Max(0,p.maximumTilt)*Mathf.Deg2Rad,0))*
                        Quaternion.Euler(0,Mathf.Lerp(p.yawRange.x,p.yawRange.y,spawn.yawDegrees/360f),0);
                    var position=spawn.worldPosition-Vector3.up*(p.groundInset*spawn.uniformScale)-rotation*new Vector3(0,p.localBounds.min.y*spawn.uniformScale,0);
                    var matrix=Matrix4x4.TRS(position,rotation,Vector3.one*spawn.uniformScale);
                    placements.Add(new Placement{prototype=i,position=position,matrix=matrix,bounds=TransformBounds(p.localBounds,matrix),priority=unchecked((uint)spawn.stableId)});break;
                }
            }
            if(!source){patches=Array.Empty<Patch>();batches=null;return;}
            regionPlacements[new ChunkCoordinate(0,0)]=placements;
            BuildCaches(placements,new HashSet<long>());
        }

        public void Rebuild(MeadowSurfacePainter surface,Rect? dirty=null)
        {
            painter=surface;
            Rebuild(surface?surface.Session:null,surface&&surface.source?surface.source.bounds:default,
                surface?surface.SampleRoad01:null,surface?surface.SampleGrass01:null,dirty);
        }

        /// <summary>Shared placement algorithm for runtime and disposable SceneView previews.</summary>
        public void Rebuild(MapEditSession session,Vector4 map,Func<float,float,float> sampleRoad,
            Func<float,float,float> sampleGrass,Rect? dirty=null)
        {
            placementSession=session;
            authoredInstances = visiblePatches = submittedInstances = submittedTriangles = drawCalls = 0;
            if(!dirty.HasValue){regionPlacements.Clear();patchCache.Clear();}
            digestDirty=true;lastRebuiltRegions=0;
            candidateBudgetReached = false;
            if (!library || library.prototypes == null || library.prototypes.Length == 0 ||
                session==null || sampleRoad==null || sampleGrass==null || maximumInstances <= 0) {patches=Array.Empty<Patch>();batches=null;regionPlacements.Clear();patchCache.Clear();return;}

            using (RebuildMarker.Auto())
            {
                samplingOrigin=session.HeightSettings.origin;
                Rect area=dirty??new Rect(map.x,map.y,map.z,map.w);
                var changedPatches=new HashSet<long>();
                var changedPlacements=new List<Placement>();
                foreach(var key in MapEditSession.Regions(area))
                {
                Rect regionBounds=MapEditSession.RegionBounds(key);
                float minX=Mathf.Max(map.x,regionBounds.xMin),minZ=Mathf.Max(map.y,regionBounds.yMin);
                Vector4 world=new Vector4(minX,minZ,Mathf.Min(map.x+map.z,regionBounds.xMax)-minX,Mathf.Min(map.y+map.w,regionBounds.yMax)-minZ);
                if(world.z<=0||world.w<=0)continue;
                lastRebuiltRegions++;
                for(int pz=key.z*4;pz<key.z*4+4;pz++)for(int px=key.x*4;px<key.x*4+4;px++)changedPatches.Add(CellKey(px,pz));
                var regions = new List<StrokeRegion>();
                AddRegions(session.QuerySurface(regionBounds), regions);
                var cells = new HashSet<long>();
                float spacing = Mathf.Max(.5f, candidateSpacing);
                foreach (var region in regions)
                {
                    if (!region.stroke.scatter || region.stroke.road || region.stroke.erase) continue;
                    var points = region.stroke.points;
                    for (int point = 0; point < points.Count && cells.Count < MaximumCandidateCells; point++)
                    {
                        Vector2 a = points[Mathf.Max(0, point - 1)], b = points[point];
                        int x0 = Mathf.FloorToInt(Mathf.Max(world.x, Mathf.Min(a.x, b.x) - region.radius) / spacing);
                        int x1 = Mathf.FloorToInt(Mathf.Min(world.x + world.z, Mathf.Max(a.x, b.x) + region.radius) / spacing);
                        int z0 = Mathf.FloorToInt(Mathf.Max(world.y, Mathf.Min(a.y, b.y) - region.radius) / spacing);
                        int z1 = Mathf.FloorToInt(Mathf.Min(world.y + world.w, Mathf.Max(a.y, b.y) + region.radius) / spacing);
                        for (int z = z0; z <= z1 && cells.Count < MaximumCandidateCells; z++)
                            for (int x = x0; x <= x1 && cells.Count < MaximumCandidateCells; x++)
                                cells.Add(CellKey(x, z));
                    }
                }
                candidateBudgetReached |= cells.Count >= MaximumCandidateCells;

                // Weight tables are rebuilt with edits, never per frame or from a global brush mode.
                var profileTotals = new float[MeadowScatterProfiles.Count];
                foreach (var prototype in library.prototypes) if (Valid(prototype))
                    for (int profile = 0; profile < profileTotals.Length; profile++)
                        profileTotals[profile] += Mathf.Max(0, prototype.weight) *
                            ProfileWeight((MeadowScatterProfile)profile, prototype.category);
                if (profileTotals[0] <= 0) {regionPlacements.Remove(key);continue;}
                var placements = new List<Placement>();
                foreach (long cell in cells)
                {
                    int cx = (int)(cell >> 32), cz = unchecked((int)(uint)cell);
                    float x = (cx + .12f + Hash01(cx, cz, 0) * .76f) * spacing;
                    float z = (cz + .12f + Hash01(cx, cz, 1) * .76f) * spacing;
                    if (x < world.x || z < world.y || x >= world.x + world.z || z >= world.y + world.w) continue;
                    float coverage = ScatterCoverage(new Vector2(x, z), regions, out var profile);
                    if (coverage <= 0 || sampleGrass(x, z) < .7f || sampleRoad(x, z) > .15f) continue;
                    float totalWeight = profileTotals[(int)profile];
                    if (totalWeight <= 0) continue;
                    // Low frequency grouping keeps little props in clusters instead of regular wallpaper.
                    float patch=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.7f,Noise(x/Mathf.Max(1,clearingRadius),z/Mathf.Max(1,clearingRadius))));
                    float probability = Mathf.Lerp(isolatedChance, clearingChance, patch) * coverage * densityMultiplier;
                    if (Hash01(cx, cz, 2) >= probability) continue;
                    if (!Separated(cx,cz,x,z,spacing)) continue;
                    // Nearby plants share a species; each group still follows the library weights.
                    float groupSize=Mathf.Max(.5f,plantGroupSize);
                    float pick = Hash01(Mathf.FloorToInt(x/groupSize),Mathf.FloorToInt(z/groupSize),3) * totalWeight;
                    int selected = -1;
                    for (int type = 0; type < library.prototypes.Length; type++)
                    {
                        var prototype = library.prototypes[type];
                        if (!Valid(prototype)) continue;
                        float typeWeight = Mathf.Max(0, prototype.weight) * ProfileWeight(profile, prototype.category);
                        if (typeWeight <= 0) continue;
                        pick -= typeWeight;
                        if (pick <= 0) { selected = type; break; }
                    }
                    if (selected < 0) continue;
                    var chosen = library.prototypes[selected];
                    if(chosen.clearanceRadius>.25f&&!Separated(cx,cz,x,z,spacing,Mathf.Max(minimumSeparation,chosen.clearanceRadius*2)))continue;
                    bool footprintClear=true;
                    for(int corner=0;corner<4;corner++)
                    {
                        float angle=(corner*90+45)*Mathf.Deg2Rad;
                        float px=x+Mathf.Cos(angle)*chosen.clearanceRadius,pz=z+Mathf.Sin(angle)*chosen.clearanceRadius;
                        if(sampleRoad(px,pz)>.15f||sampleGrass(px,pz)<.7f){footprintClear=false;break;}
                    }
                    if(!footprintClear)continue;
                    if (!session.VegetationAllowed(x,z,chosen.clearanceRadius) ||
                        (placementAllowed != null && !placementAllowed(x,z,chosen.clearanceRadius))) continue;
                    if (biomeSampler != null && !chosen.allowedBiomes.Contains(biomeSampler(x,z))) continue;
                    SurfaceKind ground=surfaceSampler!=null?surfaceSampler(x,z):SurfaceKind.Grass;
                    if (!(ground==SurfaceKind.Grass&&chosen.allowGrass || ground==SurfaceKind.WoodlandFloor&&chosen.allowWoodlandFloor || ground==SurfaceKind.Mud&&chosen.allowMud)) continue;
                    // Derive the actual local face normal, not a smoothed shading normal.
                    var normal=GroundNormal(x,z,session.HeightSpacing);
                    float slope=Vector3.Angle(Vector3.up,normal);
                    if(slope>chosen.maximumSlope)continue;
                    float scale = Mathf.Lerp(chosen.scaleRange.x, chosen.scaleRange.y, Hash01(cx, cz, 4));
                    float height = heightSampler != null ? heightSampler(x, z) : session.SampleHeight(x, z);
                    var rotation=Quaternion.FromToRotation(Vector3.up,Vector3.RotateTowards(Vector3.up,normal,Mathf.Min(slope,chosen.maximumTilt)*Mathf.Deg2Rad,0)) *
                        Quaternion.Euler(0,Mathf.Lerp(chosen.yawRange.x,chosen.yawRange.y,Hash01(cx,cz,5)),0);
                    // localBounds.min.y is the root plane for both imported and old adapters.
                    var position = new Vector3(x, height - chosen.groundInset * scale, z) - rotation * new Vector3(0,chosen.localBounds.min.y*scale,0);
                    var matrix = Matrix4x4.TRS(position, rotation, Vector3.one * scale);
                    placements.Add(new Placement { prototype = selected, position = position, matrix = matrix,
                        bounds = TransformBounds(chosen.localBounds, matrix), priority = Hash(cx, cz, 7) });
                }
                // Stable priority avoids filling only one geographical corner of a region at the budget.
                placements.Sort((a, b) => { int order = a.priority.CompareTo(b.priority); if (order != 0) return order;
                    order = a.position.x.CompareTo(b.position.x); return order != 0 ? order : a.position.z.CompareTo(b.position.z); });
                int budget = Mathf.Clamp(maximumInstances, 0, 1500);
                if (placements.Count > budget) placements.RemoveRange(budget, placements.Count - budget);
                // A fixed regional budget cannot displace distant decoration after a local edit.
                regionPlacements[key]=placements;changedPlacements.AddRange(placements);
                }
                BuildCaches(changedPlacements,changedPatches);
            }
        }

        static void AddRegions(List<SurfacePaintStroke> strokes, List<StrokeRegion> regions)
        {
            if (strokes == null) return;
            foreach (var stroke in strokes)
            {
                if (stroke == null || stroke.points == null || stroke.points.Count == 0) continue;
                float radius = Mathf.Clamp(stroke.radius, .1f, 128);
                Vector2 min = stroke.points[0], max = min;
                bool finite = true;
                foreach (Vector2 p in stroke.points)
                {
                    if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y)) { finite = false; break; }
                    min = Vector2.Min(min, p); max = Vector2.Max(max, p);
                }
                if (finite) regions.Add(new StrokeRegion { stroke = stroke, radius = radius,
                    bounds = Rect.MinMaxRect(min.x - radius, min.y - radius, max.x + radius, max.y + radius) });
            }
        }

        static float ScatterCoverage(Vector2 point, List<StrokeRegion> regions, out MeadowScatterProfile profile)
        {
            profile = MeadowScatterProfile.Mixed;
            for (int r = regions.Count - 1; r >= 0; r--)
            {
                var region = regions[r];
                if (!region.bounds.Contains(point)) continue;
                float distance = float.PositiveInfinity;
                var points = region.stroke.points;
                for (int i = 0; i < points.Count; i++)
                    distance = Mathf.Min(distance, LittleCastle.World.ChunkTerrainVertexMasks.DistanceToSegment(point,
                        points[Mathf.Max(0, i - 1)], points[i], out _));
                if (distance >= region.radius) continue;
                if (region.stroke.road || region.stroke.erase || !region.stroke.scatter) return 0;
                profile = NormalizeProfile(region.stroke.scatterProfile);
                return MapBrushFalloff.Weight(distance,region.radius,region.stroke.brushSoftness==0?.38f:region.stroke.brushSoftness-.001f)*region.stroke.density;
            }
            return 0;
        }

        /// <summary>Category multiplier; base prototype weights and deterministic cell hashes stay intact.</summary>
        public static float ProfileWeight(MeadowScatterProfile profile, MeadowScatterCategory category)
        {
            switch (profile)
            {
                case MeadowScatterProfile.Flowers:
                    return category == MeadowScatterCategory.Flower ? 1 : 0;
                case MeadowScatterProfile.ForestFloor:
                    return category == MeadowScatterCategory.Flower ? .12f : category == MeadowScatterCategory.Stone ? .5f :
                        category == MeadowScatterCategory.ForestDebris ? 1 : 0;
                case MeadowScatterProfile.MeadowGrass:
                    return category==MeadowScatterCategory.Grass?1:0;
                case MeadowScatterProfile.Stones:
                    return category==MeadowScatterCategory.Stone?1:0;
                case MeadowScatterProfile.Understory:
                    return category==MeadowScatterCategory.Shrub?1:category==MeadowScatterCategory.ForestDebris?.4f:0;
                default:
                    return 1;
            }
        }

        static MeadowScatterProfile NormalizeProfile(MeadowScatterProfile profile) =>
            (int)profile>=0&&(int)profile<MeadowScatterProfiles.Count?profile:MeadowScatterProfile.Mixed;

        void BuildCaches(List<Placement> placements,HashSet<long> changedPatches)
        {
            int types = library.prototypes.Length;
            var counts = new int[types];
            var spatial = new Dictionary<long, List<Placement>>();
            foreach (var placement in placements)
            {
                long key = CellKey(Mathf.FloorToInt(placement.position.x / PatchSize), Mathf.FloorToInt(placement.position.z / PatchSize));
                if (!spatial.TryGetValue(key, out var group)) spatial.Add(key, group = new List<Placement>());
                group.Add(placement);
            }
            foreach(long key in changedPatches)patchCache.Remove(key);
            foreach (var pair in spatial)
            {
                if(patchCache.ContainsKey(pair.Key))continue;
                var lists = new List<Matrix4x4>[types];
                var patch = new Patch { matrices = new Matrix4x4[types][] };
                bool first = true;
                foreach (var placement in pair.Value)
                {
                    if (first) { patch.bounds = placement.bounds; first = false; } else patch.bounds.Encapsulate(placement.bounds);
                    if (lists[placement.prototype] == null) lists[placement.prototype] = new List<Matrix4x4>();
                    lists[placement.prototype].Add(placement.matrix);
                }
                patch.bounds.Expand(.1f);
                for (int type = 0; type < types; type++) if (lists[type] != null) patch.matrices[type] = lists[type].ToArray();
                patchCache[pair.Key]=patch;
            }
            patches=new List<Patch>(patchCache.Values).ToArray();authoredInstances=0;
            foreach(var patch in patches)for(int type=0;type<types;type++)
                if(patch.matrices[type]!=null){counts[type]+=patch.matrices[type].Length;authoredInstances+=patch.matrices[type].Length;}
            if(batches==null||batches.GetLength(0)!=types)batches=new Batch[types,4];
            for(int type=0;type<types;type++)for(int lod=0;lod<4;lod++)
                if(batches[type,lod]==null||batches[type,lod].matrices.Length<counts[type])
                    batches[type,lod]=new Batch{matrices=new Matrix4x4[Mathf.NextPowerOfTwo(Mathf.Max(1,counts[type]))]};
        }
        void Digest(int value){cachedDigest^=unchecked((uint)value);cachedDigest=unchecked(cachedDigest*1099511628211UL);}

        void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (!isActiveAndEnabled || !library || batches == null || !camera || camera.cameraType == CameraType.Preview || !SystemInfo.supportsInstancing) return;
            using (DrawMarker.Auto())
            {
                visiblePatches = submittedInstances = submittedTriangles = drawCalls = 0;
                foreach (var batch in batches) batch.count = 0;
                GeometryUtility.CalculateFrustumPlanes(camera, planes);
                Vector3 eye = camera.transform.position;
                foreach (var patch in patches)
                {
                    if (!GeometryUtility.TestPlanesAABB(planes, patch.bounds)) continue;
                    float distance = patch.bounds.SqrDistance(eye);
                    if (distance > maximumDistance * maximumDistance) continue;
                    int lod = distance < lodDistances.x * lodDistances.x ? 0 : distance < lodDistances.y * lodDistances.y ? 1 : distance < lodDistances.z * lodDistances.z ? 2 : 3;
                    visiblePatches++;
                    for (int type = 0; type < library.prototypes.Length; type++)
                    {
                        var matrices = patch.matrices[type];
                        if (matrices == null || matrices.Length == 0) continue;
                        var batch = batches[type, lod];
                        if (batch.count == 0) batch.bounds = patch.bounds; else batch.bounds.Encapsulate(patch.bounds);
                        Array.Copy(matrices, 0, batch.matrices, batch.count, matrices.Length);
                        batch.count += matrices.Length;
                    }
                }
                for (int type = 0; type < library.prototypes.Length; type++) for (int lod = 0; lod < 4; lod++)
                {
                    var batch = batches[type, lod];
                    if (batch.count == 0) continue;
                    var prototype = library.prototypes[type];
                    if(!Valid(prototype))continue;
                    var mesh = prototype.lodMeshes[lod];
                    submittedInstances += batch.count;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        var material = prototype.materials[sub];
                        if (!material) continue;
                        var parameters = new RenderParams(material) { camera = camera, worldBounds = batch.bounds,
                            layer = gameObject.layer, shadowCastingMode = ShadowCastingMode.Off,
                            receiveShadows = true, lightProbeUsage = LightProbeUsage.Off };
                        submittedTriangles += batch.count * (int)(mesh.GetIndexCount(sub) / 3);
                        for (int start = 0; start < batch.count; start += 500)
                        {
                            Graphics.RenderMeshInstanced(parameters, mesh, sub, batch.matrices, Mathf.Min(500, batch.count - start), start);
                            drawCalls++;
                        }
                    }
                }
            }
        }

        static bool Valid(MeadowScatterLibrary.Prototype prototype)
        {
            if (prototype == null || !prototype.enabled || prototype.weight<=0 || prototype.lodMeshes == null || prototype.lodMeshes.Length != 4 || prototype.materials == null) return false;
            foreach (var mesh in prototype.lodMeshes) if (!mesh || mesh.subMeshCount > prototype.materials.Length) return false;
            return prototype.scaleRange.x > 0 && prototype.scaleRange.y >= prototype.scaleRange.x;
        }

        static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center), extents = bounds.extents;
            Vector3 x = matrix.MultiplyVector(new Vector3(extents.x, 0, 0));
            Vector3 y = matrix.MultiplyVector(new Vector3(0, extents.y, 0));
            Vector3 z = matrix.MultiplyVector(new Vector3(0, 0, extents.z));
            return new Bounds(center, 2 * new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)));
        }

        static long CellKey(int x, int z) => ((long)x << 32) | (uint)z;
        uint Hash(int x, int z, int channel)
        {
            unchecked { uint value = (uint)x * 0x8da6b343u ^ (uint)z * 0xd8163841u ^ (uint)(channel + seed) * 0xcb1ab31fu;
                value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ (value >> 16); }
        }
        float Hash01(int x, int z, int channel) => (Hash(x, z, channel) & 0xffffffu) / 16777216f;
        float Noise(float x, float z)
        {
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
            float fx = x - ix, fz = z - iz; fx = fx * fx * (3 - 2 * fx); fz = fz * fz * (3 - 2 * fz);
            return Mathf.Lerp(Mathf.Lerp(Hash01(ix, iz, 11), Hash01(ix + 1, iz, 11), fx),
                Mathf.Lerp(Hash01(ix, iz + 1, 11), Hash01(ix + 1, iz + 1, 11), fx), fz);
        }

        Vector3 GroundNormal(float x,float z,float step)
        {
            step=Mathf.Max(.01f,step);
            float x0=samplingOrigin.x+Mathf.Floor((x-samplingOrigin.x)/step)*step,z0=samplingOrigin.y+Mathf.Floor((z-samplingOrigin.y)/step)*step;
            float Height(float px,float pz)=>heightSampler!=null?heightSampler(px,pz):placementSession.SampleHeight(px,pz);
            float a=Height(x0,z0),b=Height(x0+step,z0),c=Height(x0,z0+step),d=Height(x0+step,z0+step);
            return (x-x0+z-z0<=step?new Vector3(a-b,step,a-c):new Vector3(c-d,step,b-d)).normalized;
        }

        bool Separated(int cx,int cz,float x,float z,float spacing,float requestedRadius=-1)
        {
            // Symmetric priority thinning on the infinite candidate lattice: no seam/order dependence.
            float radius=Mathf.Clamp(requestedRadius<0?minimumSeparation:requestedRadius,0,3);
            int reach=Mathf.CeilToInt(radius/spacing)+1;uint priority=Hash(cx,cz,19);
            for(int dz=-reach;dz<=reach;dz++)for(int dx=-reach;dx<=reach;dx++)
            {
                if(dx==0&&dz==0)continue;int nx=cx+dx,nz=cz+dz;
                uint other=Hash(nx,nz,19);
                if(other>priority || other==priority&&(nx>cx||nx==cx&&nz>cz))continue;
                float px=(nx+.12f+Hash01(nx,nz,0)*.76f)*spacing;
                float pz=(nz+.12f+Hash01(nx,nz,1)*.76f)*spacing;
                if((px-x)*(px-x)+(pz-z)*(pz-z)<radius*radius)return false;
            }
            return true;
        }
    }
}
