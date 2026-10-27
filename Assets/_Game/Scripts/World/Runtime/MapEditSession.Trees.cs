using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed partial class MapEditSession
    {
        sealed class TreeChange {public MapTreeInstance before,after;}
        readonly Dictionary<long,MapTreeInstance> trees=new Dictionary<long,MapTreeInstance>();
        readonly Dictionary<long,MapTreeInstance> baseTrees=new Dictionary<long,MapTreeInstance>();
        readonly Dictionary<ChunkCoordinate,HashSet<long>> treeIndex=new Dictionary<ChunkCoordinate,HashSet<long>>();
        readonly Dictionary<string,MapTreeDefinition> treeDefinitions=new Dictionary<string,MapTreeDefinition>(StringComparer.Ordinal);
        readonly Dictionary<string,MapTreePack> treePacks=new Dictionary<string,MapTreePack>(StringComparer.Ordinal);
        float maximumTreeRadius=1;
        Command treeBrush;
        readonly HashSet<ChunkCoordinate> brushCells=new HashSet<ChunkCoordinate>();
        MapTreePack activePack;
        float treeBrushRadius,treeDensity,treeSpacing;
        int treeSeed;
        string brushId;
        public long TreeRevision {get;private set;}
        public int TreeCount=>trees.Count;
        bool AuthorTools=>(Access&MapEditAccess.Author)!=0;
        void InitializeTrees()
        {
            ValidateTreeRecords(authored.trees);ValidateExclusions();
            foreach(var record in authored.trees){baseTrees.Add(record.stableId,record);SetTree(record.stableId,record);}
            TreeRevision=0;
        }
        public void RegisterTreeCatalog(IEnumerable<MapTreeDefinition> definitions,IEnumerable<MapTreePack> packs)
        {
            var defs=new Dictionary<string,MapTreeDefinition>(StringComparer.Ordinal);var nextPacks=new Dictionary<string,MapTreePack>(StringComparer.Ordinal);
            foreach(var d in definitions)
            {
                if(d==null||string.IsNullOrWhiteSpace(d.archetypeId)||d.version<1||!Finite(d.minimumScale)||!Finite(d.maximumScale)||d.minimumScale<.2f||d.maximumScale>4||d.maximumScale<d.minimumScale||
                    !Finite(d.minimumSpacing)||d.minimumSpacing<1||d.minimumSpacing>20||!Finite(d.maxSlopeDegrees)||d.maxSlopeDegrees<0||d.maxSlopeDegrees>70||!Finite(d.groundOffset)||!defs.TryAdd(d.archetypeId,Clone(d)))throw new InvalidDataException("Invalid/duplicate tree catalog entry.");
            }
            foreach(var pack in packs)
            {
                if(pack==null||string.IsNullOrWhiteSpace(pack.id)||pack.version<1||pack.entries==null||pack.entries.Count==0||!nextPacks.TryAdd(pack.id,Clone(pack)))throw new InvalidDataException("Invalid/duplicate tree pack.");
                float sum=0;foreach(var e in pack.entries){if(e==null||!defs.ContainsKey(e.archetypeId)||!Finite(e.weight)||e.weight<0)throw new InvalidDataException("Invalid tree pack member.");sum+=e.weight;}if(sum<=0)throw new InvalidDataException("Tree pack has no weighted entries.");
            }
            treeDefinitions.Clear();treePacks.Clear();foreach(var d in defs)treeDefinitions.Add(d.Key,d.Value);foreach(var p in nextPacks)treePacks.Add(p.Key,p.Value);
        }
        public bool CanPlaceTree(string archetypeId)=>treeDefinitions.TryGetValue(archetypeId,out var d)&&(Access&MapEditAccess.TreePlace)!=0&&(AuthorTools||d.playerPlaceable&&!d.authorOnly);
        void RequireTreeDefinition(string id,out MapTreeDefinition definition)
        {
            if(!treeDefinitions.TryGetValue(id,out definition))throw new InvalidDataException("Tree source is unavailable in this catalog.");
            if(!AuthorTools&&(definition.authorOnly||!definition.playerPlaceable))throw new UnauthorizedAccessException("This tree is reserved for the author.");
        }
        void RequireTreeAccess(MapEditAccess required)
        {if((Access&required)!=required)throw new UnauthorizedAccessException("This tree tool is disabled for the current role.");}
        public long PlaceTree(string archetypeId,Vector2 position,float yaw=0,float scale=1,long stableId=0)
        {
            CommitStroke();RequireTreeAccess(MapEditAccess.TreePlace);RequireTreeDefinition(archetypeId,out var definition);
            var record=CreateTree(definition,stableId==0?MapObjectIds.New():stableId,position,yaw,scale);
            if(trees.ContainsKey(record.stableId)||baseTrees.ContainsKey(record.stableId))throw new InvalidDataException("Duplicate tree ID.");
            ValidateTreeRecord(record);ValidatePlacement(record,definition,0);var command=TreeCommand(MapEditAccess.TreePlace);command.treeChanges.Add(new TreeChange{after=record});command.bounds=TreeBounds(record);Execute(command);return record.stableId;
        }
        public void TransformTree(long id,Vector2 position,float yaw,float scale)
        {
            CommitStroke();RequireTreeAccess(MapEditAccess.TreeEdit);var before=GetTreeOwned(id);CheckTreeEditable(before);RequireTreeDefinition(before.archetypeId,out var definition);
            var after=Clone(before);after.position=position;after.yawDegrees=NormalizeYaw(yaw);after.uniformScale=scale;after.clearanceRadius=before.clearanceRadius*scale/before.uniformScale;
            ValidateTreeRecord(after);if(scale!=before.uniformScale&&(scale<definition.minimumScale||scale>definition.maximumScale))throw new InvalidDataException("Tree scale exceeds the source range.");
            if(position!=before.position||scale!=before.uniformScale)ValidatePlacement(after,definition,id);
            if(SameTree(before,after))return;var command=TreeCommand(MapEditAccess.TreeEdit);command.treeChanges.Add(new TreeChange{before=before,after=after});command.bounds=Union(TreeBounds(before),TreeBounds(after));Execute(command);
        }
        public void DeleteTree(long id)
        {
            CommitStroke();RequireTreeAccess(MapEditAccess.TreeEdit);var before=GetTreeOwned(id);CheckTreeEditable(before);
            var command=TreeCommand(MapEditAccess.TreeEdit);command.treeChanges.Add(new TreeChange{before=before});command.bounds=TreeBounds(before);Execute(command);
        }
        public void BeginTreeBrush(string packId,float radius,float density,float spacing,int seed,string transactionId=null,float softness=0)
        {
            CommitStroke();RequireTreeAccess(MapEditAccess.TreeBrush);
            if(!treePacks.TryGetValue(packId,out var pack))throw new InvalidDataException("Unknown tree pack.");
            if(pack.authorOnly&&!AuthorTools)throw new UnauthorizedAccessException("This tree pack is reserved for the author.");
            foreach(var member in pack.entries)if(member.weight>0)RequireTreeDefinition(member.archetypeId,out _);
            if(!Finite(radius)||radius<1||radius>128||!Finite(softness)||softness<0||softness>1||!Finite(density)||density<0||density>1||!Finite(spacing)||spacing<2||spacing>16)throw new InvalidDataException("Invalid tree brush settings.");
            activePack=Clone(pack);treeBrushRadius=radius;treeSoftness=softness;treeDensity=density;treeSpacing=spacing;treeSeed=seed;brushId=transactionId??Guid.NewGuid().ToString("N");brushCells.Clear();
            treeBrush=TreeCommand(MapEditAccess.TreeBrush|(pack.authorOnly?MapEditAccess.Author:MapEditAccess.None));
        }
        float treeSoftness;
        public int AppendTreeBrush(Vector2 center)
        {
            if(treeBrush==null)throw new InvalidOperationException("Begin a tree brush first.");CheckTreeCommandAccess(treeBrush);ValidatePoint(center);int count=0;Rect dirty=default;
            for(int z=Mathf.FloorToInt((center.y-treeBrushRadius)/treeSpacing);z<=Mathf.FloorToInt((center.y+treeBrushRadius)/treeSpacing);z++)
            for(int x=Mathf.FloorToInt((center.x-treeBrushRadius)/treeSpacing);x<=Mathf.FloorToInt((center.x+treeBrushRadius)/treeSpacing);x++)
            {
                var cell=new ChunkCoordinate(x,z);if(brushCells.Contains(cell))continue;
                var p=new Vector2((x+.2f+TreeHash01(x,z,0)*.6f)*treeSpacing,(z+.2f+TreeHash01(x,z,1)*.6f)*treeSpacing);
                if((p-center).sqrMagnitude>treeBrushRadius*treeBrushRadius)continue;
                if(TreeHash01(x,z,2)>=treeDensity*MapBrushFalloff.Weight(Vector2.Distance(p,center),treeBrushRadius,treeSoftness))continue;
                brushCells.Add(cell);
                float total=0;foreach(var e in activePack.entries)total+=e.weight;float pick=TreeHash01(x,z,3)*total;MapTreeDefinition definition=null;
                foreach(var entry in activePack.entries){pick-=entry.weight;if(entry.weight>0&&pick<=0){RequireTreeDefinition(entry.archetypeId,out definition);break;}}if(definition==null)continue;
                long id=MapObjectIds.FromKey(MapId+"/"+brushId+"/"+x+"/"+z);if(trees.ContainsKey(id)||baseTrees.ContainsKey(id))continue;
                var record=CreateTree(definition,id,p,TreeHash01(x,z,4)*360,Mathf.Lerp(definition.minimumScale,definition.maximumScale,TreeHash01(x,z,5)));
                if(!PlacementAllowed(record,definition,0,out _))continue;
                SetTree(id,record);treeBrush.treeChanges.Add(new TreeChange{after=record});treeBrush.bounds=Union(treeBrush.bounds,TreeBounds(record));dirty=Union(dirty,TreeBounds(record));count++;
            }
            if(count>0)Notify(dirty,true,MapChangeKind.Objects);return count;
        }
        void CommitTreeBrush()
        {
            if(treeBrush==null)return;try{CheckTreeCommandAccess(treeBrush);}catch{CancelTreeBrush();throw;}
            var command=treeBrush;treeBrush=null;activePack=null;brushCells.Clear();if(command.treeChanges.Count>0)Execute(command);
        }
        void CancelTreeBrush()
        {
            if(treeBrush==null)return;var command=treeBrush;treeBrush=null;activePack=null;brushCells.Clear();
            foreach(var change in command.treeChanges)SetTree(change.after.stableId,change.before);Notify(command.bounds,false,MapChangeKind.Objects);
        }
        static Command TreeCommand(MapEditAccess required)=>new Command{kind=MapChangeKind.Objects,treeAccess=required};
        void CheckTreeCommandAccess(Command c)
        {if(c.treeChanges.Count==0&&c.treeAccess==0)return;RequireTreeAccess(c.treeAccess);foreach(var change in c.treeChanges){if(change.before!=null)CheckTreeEditable(change.before);if(change.after!=null)CheckTreeEditable(change.after);}}
        void CheckTreeEditable(MapTreeInstance record){if(!AuthorTools&&!record.playerEditable)throw new UnauthorizedAccessException("This tree is reserved for the author.");}
        MapTreeInstance CreateTree(MapTreeDefinition d,long id,Vector2 p,float yaw,float scale)
        {
            if(!Finite(scale)||scale<d.minimumScale||scale>d.maximumScale)throw new InvalidDataException("Tree scale exceeds the source range.");
            return new MapTreeInstance{stableId=id,archetypeId=d.archetypeId,catalogVersion=d.version,position=p,yawDegrees=NormalizeYaw(yaw),uniformScale=scale,
                groundOffset=d.groundOffset,clearanceRadius=d.minimumSpacing*.5f*scale,playerEditable=d.playerPlaceable&&!d.authorOnly};
        }
        static float NormalizeYaw(float yaw){if(!Finite(yaw))throw new InvalidDataException("Invalid tree rotation.");return Mathf.Repeat(yaw,360);}
        void ValidatePlacement(MapTreeInstance r,MapTreeDefinition d,long ignore){if(!PlacementAllowed(r,d,ignore,out var reason))throw new InvalidOperationException(reason);}
        bool PlacementAllowed(MapTreeInstance r,MapTreeDefinition d,long ignore,out string reason)
        {
            reason="Tree placement is blocked.";var b=Bounds;Vector2 p=r.position;
            if(p.x<b.x+r.clearanceRadius||p.y<b.y+r.clearanceRadius||p.x>b.x+b.z-r.clearanceRadius||p.y>b.y+b.w-r.clearanceRadius)return false;
            if(Vector3.Angle(Vector3.up,SampleNormal(p.x,p.y))>d.maxSlopeDegrees){reason="The slope is too steep.";return false;}
            if(!VegetationAllowed(p.x,p.y,r.clearanceRadius)){reason="Река или скала блокирует посадку.";return false;}
            var footprint=TreeBounds(r);foreach(var area in authored.placementExclusions)if(area.Overlaps(footprint)){reason="Water or a reserved area blocks this position.";return false;}
            var nearby=Rect.MinMaxRect(p.x-r.clearanceRadius-maximumTreeRadius,p.y-r.clearanceRadius-maximumTreeRadius,p.x+r.clearanceRadius+maximumTreeRadius,p.y+r.clearanceRadius+maximumTreeRadius);
            foreach(var existing in QueryTreesInternal(nearby))if(existing.stableId!=ignore&&(existing.position-p).sqrMagnitude<Mathf.Pow(existing.clearanceRadius+r.clearanceRadius,2)){reason="Another object is too close.";return false;}
            var strokes=QuerySurface(footprint);
            for(int sample=0;sample<9;sample++)
            {
                var point=p;if(sample>0){float angle=(sample-1)*Mathf.PI*.25f;point+=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r.clearanceRadius*.7f;}
                float road=0;
                foreach(var stroke in strokes)
                {
                    float distance=float.PositiveInfinity;for(int i=0;i<stroke.points.Count;i++)distance=Mathf.Min(distance,ChunkTerrainVertexMasks.DistanceToSegment(point,stroke.points[Mathf.Max(0,i-1)],stroke.points[i],out _));
                    float cover=MapBrushFalloff.SurfaceWeight(stroke,distance);
                    if(!stroke.erase)road=stroke.road?Mathf.Max(road,cover):Mathf.Min(road,1-cover);
                }
                if(road>.15f){reason="Road clearance blocks this position.";return false;}
            }
            return true;
        }
        float TreeHash01(int x,int z,int channel)
        {unchecked{uint v=(uint)x*0x8da6b343u^(uint)z*0xd8163841u^(uint)treeSeed*0xcb1ab31fu^(uint)(channel+41)*0x9e3779b9u;v^=v>>16;v*=0x7feb352du;v^=v>>15;v*=0x846ca68bu;return (v&0xffffffu)/16777216f;}}
        void SetTree(long id,MapTreeInstance value)
        {
            if(trees.TryGetValue(id,out var before))
            {
                if(value!=null&&SameTree(before,value))return;
                var key=TreeCell(before.position);if(treeIndex.TryGetValue(key,out var group)){group.Remove(id);if(group.Count==0)treeIndex.Remove(key);}trees.Remove(id);
            }
            if(value!=null){trees.Add(id,value);var key=TreeCell(value.position);if(!treeIndex.TryGetValue(key,out var group))treeIndex.Add(key,group=new HashSet<long>());group.Add(id);maximumTreeRadius=Mathf.Max(maximumTreeRadius,value.clearanceRadius);}
            TreeRevision++;
        }
        static ChunkCoordinate TreeCell(Vector2 p)=>new ChunkCoordinate(Mathf.FloorToInt(p.x/RegionSize),Mathf.FloorToInt(p.y/RegionSize));
        static Rect TreeBounds(MapTreeInstance r)=>new Rect(r.position.x-r.clearanceRadius,r.position.y-r.clearanceRadius,r.clearanceRadius*2,r.clearanceRadius*2);
        IEnumerable<MapTreeInstance> QueryTreesInternal(Rect area)
        {foreach(var key in Regions(area))if(treeIndex.TryGetValue(key,out var group))foreach(long id in group){var record=trees[id];if(area.Contains(record.position))yield return record;}}
        public List<MapTreeInstance> QueryTrees(Rect area){var list=new List<MapTreeInstance>();foreach(var record in QueryTreesInternal(area))list.Add(Clone(record));list.Sort((a,b)=>a.stableId.CompareTo(b.stableId));return list;}
        public List<MapTreeInstance> ExportTrees(){var list=new List<MapTreeInstance>();foreach(var r in trees.Values)list.Add(Clone(r));list.Sort((a,b)=>a.stableId.CompareTo(b.stableId));return list;}
        MapTreeInstance GetTreeOwned(long id)=>trees.TryGetValue(id,out var r)?r:throw new InvalidOperationException("Tree no longer exists.");
        public MapTreeInstance GetTree(long id)=>Clone(GetTreeOwned(id));
        public bool HasTree(long id)=>trees.ContainsKey(id);
        static bool SameTree(MapTreeInstance a,MapTreeInstance b)=>a.stableId==b.stableId&&a.archetypeId==b.archetypeId&&a.catalogVersion==b.catalogVersion&&a.position==b.position&&
            a.yawDegrees==b.yawDegrees&&a.uniformScale==b.uniformScale&&a.groundOffset==b.groundOffset&&a.clearanceRadius==b.clearanceRadius&&a.playerEditable==b.playerEditable;
        void ValidateTreeRecord(MapTreeInstance r)
        {
            if(r==null||r.stableId==0||string.IsNullOrWhiteSpace(r.archetypeId)||r.archetypeId.Length>128||r.catalogVersion<1||!Finite(r.uniformScale)||r.uniformScale<.2f||r.uniformScale>4||
                !Finite(r.yawDegrees)||!Finite(r.groundOffset)||Mathf.Abs(r.groundOffset)>10||!Finite(r.clearanceRadius)||r.clearanceRadius<=0||r.clearanceRadius>64)throw new InvalidDataException("Invalid tree instance.");ValidatePoint(r.position);
        }
        void ValidateTreeRecords(List<MapTreeInstance> values)
        {if(values==null||values.Count>200000)throw new InvalidDataException("Invalid tree records.");var seen=new HashSet<long>();foreach(var r in values){ValidateTreeRecord(r);if(!seen.Add(r.stableId))throw new InvalidDataException("Duplicate tree ID.");}}
        void ValidateExclusions(){if(authored.placementExclusions==null)throw new InvalidDataException("Missing placement exclusions.");foreach(var r in authored.placementExclusions)if(!Finite(r.x)||!Finite(r.y)||!Finite(r.width)||!Finite(r.height)||r.width<0||r.height<0)throw new InvalidDataException("Invalid placement exclusion.");}
        static List<MapTreeInstance> CloneTrees(List<MapTreeInstance> input)
        {var list=new List<MapTreeInstance>();if(input==null)throw new InvalidDataException("Missing tree records.");foreach(var record in input)list.Add(Clone(record));return list;}
        bool HasTreeEdits=>ExportTreeOverrides().Count>0||ExportRemovedTrees().Count>0;
        List<MapTreeInstance> ExportTreeOverrides(){var list=new List<MapTreeInstance>();foreach(var r in trees.Values)if(!baseTrees.TryGetValue(r.stableId,out var b)||!SameTree(r,b))list.Add(Clone(r));list.Sort((a,b)=>a.stableId.CompareTo(b.stableId));return list;}
        List<long> ExportRemovedTrees(){var list=new List<long>();foreach(long id in baseTrees.Keys)if(!trees.ContainsKey(id))list.Add(id);list.Sort();return list;}
        List<MapTreeInstance> ValidateTreeSave(MapEditSave save)
        {
            ValidateTreeRecords(save.treeOverrides);if(save.removedTreeIds==null)throw new InvalidDataException("Missing removed-tree list.");
            var combined=new Dictionary<long,MapTreeInstance>(baseTrees);var removed=new HashSet<long>();
            foreach(long id in save.removedTreeIds){if(!baseTrees.ContainsKey(id)||!removed.Add(id))throw new InvalidDataException("Invalid removed tree ID.");combined.Remove(id);}
            foreach(var r in save.treeOverrides){if(removed.Contains(r.stableId))throw new InvalidDataException("Tree is both removed and overridden.");combined[r.stableId]=Clone(r);}
            return new List<MapTreeInstance>(combined.Values);
        }
        void ReplaceTrees(List<MapTreeInstance> values)
        {trees.Clear();treeIndex.Clear();treeBrush=null;activePack=null;brushCells.Clear();foreach(var r in values)SetTree(r.stableId,Clone(r));TreeRevision++;}
        Rect AllTreeBounds(){Rect area=default;foreach(var r in trees.Values)area=Union(area,TreeBounds(r));return area;}
        Rect TreeListBounds(List<MapTreeInstance> list){Rect area=default;foreach(var r in list)area=Union(area,TreeBounds(r));return area;}
        [Serializable] sealed class LegacyHeightBase
        {public int version;public string mapId;public int revision,decorationRulesVersion;public Vector4 bounds;public LittleCastle.Rendering.SurfacePaintDocument surface;public MapHeightFieldData heightField;}
        string LegacyHeightFingerprint()=>Fingerprint(new LegacyHeightBase{version=authored.version,mapId=MapId,revision=authored.revision,decorationRulesVersion=authored.decorationRulesVersion,bounds=Bounds,surface=authored.surface,heightField=authored.heightField});
    }
}
