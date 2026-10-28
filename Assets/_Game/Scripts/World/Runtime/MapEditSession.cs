using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.Rendering;
using UnityEngine;

namespace LittleCastle.World
{
    [Flags] public enum MapEditAccess { None=0, Road=1, Meadow=2, Erase=4, AllSurface=7, Terrain=8, TreePlace=16, TreeEdit=32, TreeBrush=64, Author=128, Player=63, Rivers=256, Biomes=512, Snow=1024, All=2047 }
    [Flags] public enum MapChangeKind { Surface=1, Decoration=2, Height=4, Objects=8, Rivers=16, Biomes=32, Snow=64 }

    /// <summary>Plain authored map root. Extend with height/object records in subsequent stages.</summary>
    [Serializable] public sealed class MapAuthoringDocument
    {
        public int version=1;
        public string mapId;
        public int revision=1;
        public int decorationRulesVersion=1;
        public Vector4 bounds;
        public SurfacePaintDocument surface=new SurfacePaintDocument();
        public MapHeightFieldData heightField;
        public List<MapTreeInstance> trees=new List<MapTreeInstance>();
        public List<Rect> placementExclusions=new List<Rect>();
        public List<MapRiverRecord> rivers=new List<MapRiverRecord>();
        public List<MapRockSection> rockSections=new List<MapRockSection>();
        public Environment.MapEnvironmentData environment;
    }

    [Serializable] public sealed class MapEditSave
    {
        public int version=6;
        public string mapId;
        public int baseRevision;
        public string baseFingerprint;
        public SurfacePaintDocument surface=new SurfacePaintDocument();
        public List<MapHeightSample> heightEdits=new List<MapHeightSample>();
        public List<MapTreeInstance> treeOverrides=new List<MapTreeInstance>();
        public List<long> removedTreeIds=new List<long>();
        public List<MapRiverRecord> rivers=new List<MapRiverRecord>();
        public List<MapRockSection> rockSections=new List<MapRockSection>();
        public Environment.MapEnvironmentData environment;
    }

    public readonly struct MapRegionChange
    {
        public readonly Rect bounds;
        public readonly MapChangeKind kind;
        public readonly bool preview;
        public MapRegionChange(Rect bounds,bool preview=false,MapChangeKind kind=MapChangeKind.Surface|MapChangeKind.Decoration)
        { this.bounds=bounds;this.preview=preview;this.kind=kind; }
    }

    /// <summary>
    /// Shared command boundary for SceneView and gameplay. Owns copies of all input data.
    /// A live stroke is one transaction; previews never enter the saved delta/history.
    /// Coordinates and spatial keys are world X/Z, including negative chunks.
    /// </summary>
    public sealed partial class MapEditSession
    {
        public const float RegionSize=32;
        readonly MapAuthoringDocument authored;
        readonly List<SurfacePaintStroke> edits=new List<SurfacePaintStroke>();
        sealed class Command
        {
            public List<SurfacePaintStroke> added=new List<SurfacePaintStroke>();
            public List<SurfacePaintStroke> removed=new List<SurfacePaintStroke>();
            public Rect bounds;
            public List<HeightChange> heights=new List<HeightChange>();
            public MapChangeKind kind=MapChangeKind.Surface|MapChangeKind.Decoration;
            public List<TreeChange> treeChanges=new List<TreeChange>();
            public MapEditAccess treeAccess;
            public List<RiverChange> rivers=new List<RiverChange>();
            public List<RockSectionChange> sections=new List<RockSectionChange>();
            public List<EnvironmentTileChange> environment=new List<EnvironmentTileChange>();
            public Environment.MapEnvironmentSettings environmentSettingsBefore,environmentSettingsAfter;
        }
        readonly Stack<Command> undo=new Stack<Command>();
        readonly Stack<Command> redo=new Stack<Command>();
        readonly Dictionary<ChunkCoordinate,List<SurfacePaintStroke>> index=new Dictionary<ChunkCoordinate,List<SurfacePaintStroke>>();
        readonly HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
        SurfacePaintStroke pending;
        public MapEditAccess Access { get; set; }
        public string MapId=>authored.mapId;
        public Vector4 Bounds=>authored.bounds;
        public bool CanUndo=>undo.Count>0||HasPending;
        public bool CanRedo=>redo.Count>0&&!HasPending;
        public bool HasPending=>pending!=null||pendingHeights!=null||treeBrush!=null||environmentStroke!=null;
        public event Action<MapRegionChange> Changed;

        public MapEditSession(MapAuthoringDocument source,MapEditAccess access=MapEditAccess.All)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            authored=Clone(source);Access=access;
            if(authored.version!=1||authored.decorationRulesVersion!=1||authored.revision<1||string.IsNullOrWhiteSpace(authored.mapId))
                throw new InvalidDataException("Unknown authored map version or identity.");
            var b=authored.bounds;
            if(!Finite(b.x)||!Finite(b.y)||!Finite(b.z)||!Finite(b.w)||b.z<=0||b.w<=0||b.z>100000||b.w>100000)
                throw new InvalidDataException("Invalid finite map bounds.");
            ValidateDocument(authored.surface);
            heightField=new MapHeightField(authored.heightField);
            InitializeTrees();ValidateRiverState(authored.rivers,authored.rockSections);InitializeRivers();InitializeEnvironment();Reindex();
        }

        public static MapAuthoringDocument FromSurface(SurfacePaintData data)=>new MapAuthoringDocument
        { environment=data.hasEnvironmentBase?data.environment:null,mapId=data.mapId,revision=data.revision,bounds=data.bounds,surface=Clone(data.authored),heightField=data.hasHeightBase&&data.heightField!=null?Clone(data.heightField):null,trees=data.hasTreeBase?CloneTrees(data.treeInstances):new List<MapTreeInstance>(),rivers=data.rivers??new List<MapRiverRecord>(),rockSections=data.rockSections??new List<MapRockSection>(),placementExclusions=data.placementExclusions==null?new List<Rect>():new List<Rect>(data.placementExclusions) };
        public MapAuthoringDocument ExportAuthored()=>Clone(authored);
        public SurfacePaintDocument ExportEdits()=>new SurfacePaintDocument{strokes=Copy(edits)};
        public MapEditSave ExportSave(){CommitStroke();return new MapEditSave{mapId=MapId,baseRevision=authored.revision,
            baseFingerprint=EnvironmentBaseFingerprint(),environment=ExportEnvironment(),surface=ExportEdits(),heightEdits=heightField.ExportEdits(),treeOverrides=ExportTreeOverrides(),removedTreeIds=ExportRemovedTrees(),rivers=ExportRivers(),rockSections=ExportRockSections()};}
        public string ExportJson()=>JsonUtility.ToJson(ExportSave(),true);
        public void StoreIn(WorldSaveState save){CommitStroke();save.mapEdits=ExportSave();}

        public void LoadJson(string json)
        {
            // The old v1 study file contains only strokes. Validate fully before changing this session.
            var header=JsonUtility.FromJson<MapEditSave>(json);
            if(header==null)throw new InvalidDataException("Empty map edit save.");
            if(header.version==1)ReplaceEdits(JsonUtility.FromJson<SurfacePaintDocument>(json));
            else LoadSave(header);
        }
        public void LoadSave(MapEditSave save)
        {
            if(save==null||(save.version!=2&&save.version!=3&&save.version!=4&&save.version!=5&&save.version!=6)||save.mapId!=MapId||save.baseRevision!=authored.revision||
                save.baseFingerprint!=(save.version==2?LegacyFingerprint():save.version==3?LegacyHeightFingerprint():save.version==4?LegacyTreeFingerprint():save.version==5?LegacyFeatureFingerprint():EnvironmentBaseFingerprint()))throw new InvalidDataException("Map edits belong to another map/base revision.");
            ReplaceEdits(save.surface,save.version==2?new List<MapHeightSample>():save.heightEdits,save.version>=4?ValidateTreeSave(save):CloneTrees(authored.trees),save.version>=5?save.rivers:Clone(authored).rivers,save.version>=5?save.rockSections:Clone(authored).rockSections,save.version>=6?save.environment:authored.environment);
        }
        void ReplaceEdits(SurfacePaintDocument value,List<MapHeightSample> heights=null,List<MapTreeInstance> loadedTrees=null,List<MapRiverRecord> loadedRivers=null,List<MapRockSection> loadedSections=null,Environment.MapEnvironmentData loadedEnvironment=null)
        {
            var validEnvironment=ValidateEnvironment(loadedEnvironment??authored.environment);
            heights=heights??new List<MapHeightSample>();ValidateHeightEdits(heights);loadedTrees=loadedTrees??CloneTrees(authored.trees);ValidateTreeRecords(loadedTrees);
            loadedRivers=loadedRivers??Clone(authored).rivers;loadedSections=loadedSections??Clone(authored).rockSections;ValidateRiverState(loadedRivers,loadedSections);
            ValidateDocument(value);
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var s in authored.surface.strokes)seen.Add(s.id);
            foreach(var s in value.strokes)if(!seen.Add(s.id))throw new InvalidDataException("Duplicate stroke ID.");
            var dirty=Union(Union(Union(AllEditBounds(),DocumentBounds(value.strokes)),Union(HeightEditsBounds(heightField.ExportEdits()),HeightEditsBounds(heights))),Union(AllTreeBounds(),TreeListBounds(loadedTrees)));
            dirty=Union(dirty,AllRiverBounds());foreach(var r in loadedRivers)dirty=Union(dirty,new MapRiverPath(r).bounds);foreach(var r in loadedSections)dirty=Union(dirty,SectionBounds(r));
            ReplaceRiverState(loadedRivers,loadedSections);RestoreEnvironment(validEnvironment);BiomeRevision++;environmentStroke=null;dirty=Union(dirty,EnvironmentBounds);
            pending=null;pendingHeights=null;pendingHeightBounds=default;if(heightField.EditCount>0||heights.Count>0)HeightRevision++;heightField.ReplaceEdits(heights);ReplaceTrees(loadedTrees);edits.Clear();edits.AddRange(Copy(value.strokes));undo.Clear();redo.Clear();Reindex();Notify(dirty,false,MapChangeKind.Surface|MapChangeKind.Decoration|MapChangeKind.Height|MapChangeKind.Objects|MapChangeKind.Rivers|MapChangeKind.Biomes|MapChangeKind.Snow);
        }

        public void BeginStroke(SurfacePaintStroke settings)
        {
            CommitStroke();var s=Clone(settings);CheckAccess(s);
            if(string.IsNullOrEmpty(s.id))s.id=Guid.NewGuid().ToString("N");
            if(ids.Contains(s.id))throw new InvalidDataException("Duplicate stroke ID.");
            s.points=new List<Vector2>();ValidateSettings(s);pending=s;
        }
        public void AppendPoint(Vector2 point)
        {
            if(pending==null)throw new InvalidOperationException("Begin a stroke first.");
            ValidatePoint(point);
            if(pending.points.Count>=65536)throw new InvalidDataException("Stroke point limit reached.");
            Vector2 last=pending.points.Count==0?point:pending.points[pending.points.Count-1];pending.points.Add(point);
            Notify(SegmentBounds(last,point,pending.radius),true);
        }
        public void CommitStroke()
        {
            CommitEnvironmentStroke();CommitTreeBrush();CommitHeightStroke();if(pending==null)return;
            var stroke=pending;pending=null;
            if(stroke.points.Count==0)return;
            try{CheckAccess(stroke);}catch{Notify(StrokeBounds(stroke));throw;}
            Execute(new Command{added=new List<SurfacePaintStroke>{stroke},bounds=StrokeBounds(stroke)});
        }
        public void CancelStroke(){CancelEnvironmentStroke();CancelTreeBrush();CancelHeightStroke();if(pending==null)return;var stroke=pending;pending=null;if(notificationDepth>0)NotifyStrokeRegions(stroke,false);else Notify(StrokeBounds(stroke));}
        public void AddStroke(SurfacePaintStroke value)
        {
            var s=Clone(value);CheckAccess(s);ValidateStroke(s);
            if(ids.Contains(s.id)||pending?.id==s.id)throw new InvalidDataException("Duplicate stroke ID.");
            CommitStroke();Execute(new Command{added=new List<SurfacePaintStroke>{s},bounds=StrokeBounds(s)});
        }
        public void Undo()
        {
            CommitStroke();if(undo.Count==0)return;var command=undo.Peek();CheckCommandAccess(command);
            undo.Pop();ApplyCommand(command,true);redo.Push(command);
        }
        public void Redo()
        {
            CommitStroke();if(redo.Count==0)return;var command=redo.Peek();CheckCommandAccess(command);
            redo.Pop();ApplyCommand(command,false);undo.Push(command);
        }
        public void ClearEdits()
        {
            CommitStroke();if(edits.Count==0&&heightField.EditCount==0&&!HasTreeEdits&&!HasEnvironmentEdits&&JsonUtility.ToJson(new FeatureState{rivers=ExportRivers(),sections=ExportRockSections()})==JsonUtility.ToJson(new FeatureState{rivers=authored.rivers,sections=authored.rockSections}))return;
            var command=new Command{removed=new List<SurfacePaintStroke>(edits),bounds=Union(AllEditBounds(),HeightEditsBounds(heightField.ExportEdits()))};
            foreach(var sample in heightField.ExportEdits())command.heights.Add(new HeightChange{x=sample.x,z=sample.z,before=sample.height,after=heightField.BaseSample(sample.x,sample.z)});
            if(command.heights.Count>0)command.kind|=MapChangeKind.Height;
            foreach(var r in ExportTreeOverrides())command.treeChanges.Add(new TreeChange{before=trees[r.stableId],after=baseTrees.TryGetValue(r.stableId,out var b)?b:null});
            foreach(long id in ExportRemovedTrees())command.treeChanges.Add(new TreeChange{after=baseTrees[id]});
            if(command.treeChanges.Count>0){command.treeAccess=MapEditAccess.TreeEdit;command.kind|=MapChangeKind.Objects;command.bounds=Union(command.bounds,Union(AllTreeBounds(),TreeListBounds(authored.trees)));}
            AddRiverReset(command);AddEnvironmentReset(command);CheckCommandAccess(command);Execute(command);
        }
        void CheckCommandAccess(Command command)
        {foreach(var s in command.added)CheckAccess(s);foreach(var s in command.removed)CheckAccess(s);if(command.heights.Count>0)RequireTerrainAccess();CheckTreeCommandAccess(command);if(command.rivers.Count>0||command.sections.Count>0)RequireRiverAccess();if(command.environment.Count>0){if((command.kind&MapChangeKind.Biomes)!=0)RequireEnvironmentAccess(false);if((command.kind&MapChangeKind.Snow)!=0)RequireEnvironmentAccess(true);}if(command.environmentSettingsBefore!=null)RequireEnvironmentAccess(true);}
        void Execute(Command command){undo.Push(command);redo.Clear();ApplyCommand(command,false);}
        void ApplyCommand(Command command,bool reverse)
        {
            foreach(var stroke in reverse?command.added:command.removed){edits.Remove(stroke);RemoveFromIndex(stroke);}
            foreach(var stroke in reverse?command.removed:command.added){edits.Add(stroke);AddToIndex(stroke);}
            bool heightChanged=false;
            foreach(var change in command.heights){float value=reverse?change.before:change.after;if(heightField.Sample(change.x,change.z)!=value)heightChanged=true;heightField.Set(change.x,change.z,value);}
            if(heightChanged)HeightRevision++;
            foreach(var change in command.treeChanges){var r=reverse?change.before:change.after;long id=(change.before??change.after).stableId;SetTree(id,r);}
            foreach(var change in command.rivers){var r=reverse?change.before:change.after;SetRiver((change.before??change.after).id,r);}foreach(var change in command.sections){var r=reverse?change.before:change.after;long id=(change.before??change.after).id;rockSections.Remove(id);if(r!=null)rockSections.Add(id,r);}
            if(command.rivers.Count>0||command.sections.Count>0){RiverRevision++;HeightRevision++;}
            if((command.kind&MapChangeKind.Height)!=0)InvalidateComposedHeight(command.bounds);
            ApplyEnvironmentCommand(command,reverse);NotifyCommand(command);
        }
        void RemoveFromIndex(SurfacePaintStroke stroke)
        {
            ids.Remove(stroke.id);sequence.Remove(stroke);
            var visited=new HashSet<ChunkCoordinate>();
            for(int i=0;i<stroke.points.Count;i++)foreach(var key in Regions(SegmentBounds(stroke.points[Mathf.Max(0,i-1)],stroke.points[i],stroke.radius)))
                if(visited.Add(key)&&index.TryGetValue(key,out var list)){list.Remove(stroke);if(list.Count==0)index.Remove(key);}
        }
        void CheckAccess(SurfacePaintStroke s)
        {
            if(s==null)throw new InvalidDataException("Missing stroke.");
            var required=s.erase?MapEditAccess.Erase:s.road?MapEditAccess.Road:MapEditAccess.Meadow;
            if((Access&required)!=required)throw new UnauthorizedAccessException("This map tool is disabled for the current role.");
        }
        void Reindex(){index.Clear();ids.Clear();sequence.Clear();foreach(var s in authored.surface.strokes)AddToIndex(s);foreach(var s in edits)AddToIndex(s);}
        void AddToIndex(SurfacePaintStroke s)
        {
            ids.Add(s.id);sequence.Add(s,sequence.Count);var visited=new HashSet<ChunkCoordinate>();
            for(int i=0;i<s.points.Count;i++)foreach(var key in Regions(SegmentBounds(s.points[Mathf.Max(0,i-1)],s.points[i],s.radius)))
            {
                if(!visited.Add(key))continue;
                if(!index.TryGetValue(key,out var list))index.Add(key,list=new List<SurfacePaintStroke>());list.Add(s);
            }
        }
        /// <summary>Ordered snapshots only for regions requested by a resident view/cache.</summary>
        public List<SurfacePaintStroke> QuerySurface(Rect area)
        {
            var found=new HashSet<SurfacePaintStroke>();
            foreach(var key in Regions(area))if(index.TryGetValue(key,out var list))foreach(var s in list)found.Add(s);
            var result=new List<SurfacePaintStroke>();
            foreach(var s in found)if(StrokeBounds(s).Overlaps(area))result.Add(s);
            result.Sort((a,b)=>sequence[a].CompareTo(sequence[b]));
            result=Copy(result);
            if(pending!=null&&pending.points.Count>0&&StrokeBounds(pending).Overlaps(area))result.Add(Clone(pending));
            return result;
        }
        readonly Dictionary<SurfacePaintStroke,int> sequence=new Dictionary<SurfacePaintStroke,int>();
        public static IEnumerable<ChunkCoordinate> Regions(Rect area)
        {
            if(area.width<=0||area.height<=0)yield break;
            for(int z=Mathf.FloorToInt(area.yMin/RegionSize);z<=Mathf.FloorToInt(area.yMax/RegionSize);z++)
                for(int x=Mathf.FloorToInt(area.xMin/RegionSize);x<=Mathf.FloorToInt(area.xMax/RegionSize);x++)yield return new ChunkCoordinate(x,z);
        }
        public static Rect RegionBounds(ChunkCoordinate key)=>new Rect(key.x*RegionSize,key.z*RegionSize,RegionSize,RegionSize);
        public static Rect StrokeBounds(SurfacePaintStroke s)
        {
            if(s==null||s.points==null||s.points.Count==0)return default;
            var min=s.points[0];var max=min;foreach(var p in s.points){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            return Rect.MinMaxRect(min.x-s.radius,min.y-s.radius,max.x+s.radius,max.y+s.radius);
        }
        static Rect SegmentBounds(Vector2 a,Vector2 b,float r)=>Rect.MinMaxRect(Mathf.Min(a.x,b.x)-r,Mathf.Min(a.y,b.y)-r,Mathf.Max(a.x,b.x)+r,Mathf.Max(a.y,b.y)+r);
        static Rect Union(Rect a,Rect b)=>a.width<=0?b:b.width<=0?a:Rect.MinMaxRect(Mathf.Min(a.xMin,b.xMin),Mathf.Min(a.yMin,b.yMin),Mathf.Max(a.xMax,b.xMax),Mathf.Max(a.yMax,b.yMax));
        static Rect DocumentBounds(List<SurfacePaintStroke> list){Rect r=default;foreach(var s in list)r=Union(r,StrokeBounds(s));return r;}
        Rect AllEditBounds()=>Union(DocumentBounds(edits),StrokeBounds(pending));
        void Notify(Rect dirty,bool preview=false,MapChangeKind kind=MapChangeKind.Surface|MapChangeKind.Decoration){if(dirty.width>0)QueueNotification(new MapRegionChange(dirty,preview,kind));}
        void ValidateDocument(SurfacePaintDocument d)
        {
            if(d==null||d.version!=1||d.strokes==null||d.strokes.Count>100000)throw new InvalidDataException("Unknown surface document.");
            var seen=new HashSet<string>();foreach(var s in d.strokes){ValidateStroke(s);if(!seen.Add(s.id))throw new InvalidDataException("Duplicate stroke ID.");}
        }
        void ValidateStroke(SurfacePaintStroke s)
        {
            ValidateSettings(s);if(s.points==null||s.points.Count==0||s.points.Count>65536)throw new InvalidDataException("Invalid stroke points.");foreach(var p in s.points)ValidatePoint(p);
        }
        static void ValidateSettings(SurfacePaintStroke s)
        {
            if(s==null||string.IsNullOrWhiteSpace(s.id)||s.id.Length>128||!Finite(s.radius)||s.radius<.1f||s.radius>128||
                !Finite(s.strength)||s.strength<0||s.strength>1||!Finite(s.density)||s.density<0||s.density>1||
                !Finite(s.brushSoftness)||s.brushSoftness<0||s.brushSoftness>1.0011f||
                (int)s.scatterProfile<0||(int)s.scatterProfile>=MeadowScatterProfiles.Count)throw new InvalidDataException("Invalid surface stroke.");
        }
        void ValidatePoint(Vector2 p)
        { var b=Bounds;if(!Finite(p.x)||!Finite(p.y)||p.x<b.x||p.y<b.y||p.x>b.x+b.z||p.y>b.y+b.w)throw new InvalidDataException("Point outside finite map bounds."); }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        static T Clone<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static List<SurfacePaintStroke> Copy(List<SurfacePaintStroke> values){var r=new List<SurfacePaintStroke>(values.Count);foreach(var s in values)r.Add(Clone(s));return r;}
        static string Fingerprint<T>(T d)
        { ulong h=14695981039346656037UL;foreach(char c in System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(d), @",""brushSoftness"":0(?:\.0)?(?=[,}])", "")){h^=c;h=unchecked(h*1099511628211UL);}return h.ToString("x16"); }
    }
}
