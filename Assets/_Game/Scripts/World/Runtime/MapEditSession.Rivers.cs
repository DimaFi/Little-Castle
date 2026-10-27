using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed partial class MapEditSession
    {
        sealed class RiverChange {public MapRiverRecord before,after;}
        sealed class RockSectionChange {public MapRockSection before,after;}
        readonly SortedDictionary<long,MapRiverPath> rivers=new SortedDictionary<long,MapRiverPath>();
        readonly SortedDictionary<long,MapRockSection> rockSections=new SortedDictionary<long,MapRockSection>();
        readonly Dictionary<ChunkCoordinate,HashSet<long>> riverIndex=new Dictionary<ChunkCoordinate,HashSet<long>>();
        readonly List<MapRockDefinition> rockDefinitions=new List<MapRockDefinition>();
        readonly Dictionary<ChunkCoordinate,float> composedHeightCache=new Dictionary<ChunkCoordinate,float>();
        public long RiverRevision {get;private set;}
        public int RiverCount=>rivers.Count;
        const MapChangeKind RiverKinds=MapChangeKind.Rivers|MapChangeKind.Height|MapChangeKind.Surface|MapChangeKind.Decoration|MapChangeKind.Objects;
        void RequireRiverAccess(){if((Access&MapEditAccess.Rivers)==0)throw new UnauthorizedAccessException("Редактирование рек/скал выключено для этой роли.");}
        void InitializeRivers(){ReplaceRiverState(authored.rivers??new List<MapRiverRecord>(),authored.rockSections??new List<MapRockSection>());RiverRevision=0;}
        public void RegisterRockCatalog(IEnumerable<MapRockDefinition> entries)
        {
            var next=new List<MapRockDefinition>();var ids=new HashSet<string>();
            foreach(var d in entries)
            {
                if(d==null||string.IsNullOrWhiteSpace(d.id)||!ids.Add(d.id)||d.version<1||!Finite(d.length)||d.length<=1||!Finite(d.height)||d.height<=.5f||!Finite(d.thickness)||d.thickness<=.5f||!Finite(d.overlap)||d.overlap<0||d.overlap>d.length*.5f||!Finite(d.minimumScale)||!Finite(d.maximumScale)||d.minimumScale<.8f||d.maximumScale>1.2f||d.maximumScale<d.minimumScale||!Finite(d.weight)||d.weight<0||!Finite(d.maxTurn)||d.maxTurn<0||d.maxTurn>45)throw new InvalidDataException("Неверные метаданные каталога скал.");next.Add(Clone(d));
            }
            next.Sort((a,b)=>string.CompareOrdinal(a.id,b.id));rockDefinitions.Clear();rockDefinitions.AddRange(next);
        }
        public List<MapRiverRecord> ExportRivers(){var result=new List<MapRiverRecord>();foreach(var p in rivers.Values)result.Add(Clone(p.record));return result;}
        public List<MapRockSection> ExportRockSections(){var result=new List<MapRockSection>();foreach(var p in rockSections.Values)result.Add(Clone(p));return result;}
        public MapRiverRecord GetRiver(long id)=>rivers.TryGetValue(id,out var p)?Clone(p.record):throw new InvalidOperationException("Река не найдена.");
        public List<MapRiverPath> QueryRiverPaths(Rect area)
        {
            var ids=new SortedSet<long>();foreach(var key in Regions(area))if(riverIndex.TryGetValue(key,out var group))foreach(var id in group)ids.Add(id);
            var result=new List<MapRiverPath>();foreach(var id in ids)if(rivers[id].bounds.Overlaps(area))result.Add(new MapRiverPath(Clone(rivers[id].record)));return result;
        }
        IEnumerable<MapRiverPath> RiversAt(Vector2 p)
        {if(riverIndex.TryGetValue(TreeCell(p),out var group))foreach(long id in group)yield return rivers[id];}
        public void UpsertRiver(MapRiverRecord input)
        {
            CommitStroke();RequireRiverAccess();var record=Clone(input);var path=ValidateRiver(record);
            rivers.TryGetValue(record.id,out var before);
            if(before!=null&&JsonUtility.ToJson(new NodeList{nodes=before.record.nodes})!=JsonUtility.ToJson(new NodeList{nodes=record.nodes}))
                foreach(var rock in record.rocks)if(!rock.manual)
                {
                    var f=path.At(rock.station/before.Length*path.Length);
                    rock.position=f.Edge(rock.side)+f.Left*rock.side*rock.bankOffset;rock.yaw=Mathf.Atan2(f.tangent.x,f.tangent.y)*Mathf.Rad2Deg+(rock.side<0?180:0);rock.station=f.station;
                }
            foreach(var other in rivers.Values)if(other.record.id!=record.id&&other.bounds.Overlaps(path.bounds))
                foreach(var a in path.frames)foreach(var b in other.frames)
                    if(Vector2.Distance(a.center,b.center)<(a.width+b.width)*.5f+Mathf.Max(a.leftTransition,a.rightTransition)+Mathf.Max(b.leftTransition,b.rightTransition))throw new InvalidDataException("Перекрытие рек не поддержано: разделите берега. Соединения уровней зарезервированы в профиле.");
            // Existing final composition is retained. Moving a river should use TranslateRiver.
            if(record.rocks.Count==0&&(before==null||before.record.rocks.Count==0))record.rocks=LayoutRocks(path,record.id,record.seed);
            ValidateRockRecords(record.rocks);
            if(before!=null&&JsonUtility.ToJson(before.record)==JsonUtility.ToJson(record))return;
            Rect dirty=before==null?path.bounds:Union(before.bounds,path.bounds);
            if(before!=null&&JsonUtility.ToJson(new NodeList{nodes=before.record.nodes})==JsonUtility.ToJson(new NodeList{nodes=record.nodes}))
            {
                dirty=default;foreach(var patch in before.record.patches)dirty=Union(dirty,PatchBounds(before,patch));foreach(var patch in record.patches)dirty=Union(dirty,PatchBounds(path,patch));
                if(dirty.width==0)dirty=path.bounds;
            }
            var c=new Command{bounds=Pad(dirty,HeightSpacing*2),kind=RiverKinds};c.rivers.Add(new RiverChange{before=before?.record,after=record});Execute(c);
        }
        [Serializable] sealed class NodeList {public List<MapRiverNode> nodes;}
        static Rect Pad(Rect r,float amount)=>Rect.MinMaxRect(r.xMin-amount,r.yMin-amount,r.xMax+amount,r.yMax+amount);
        static Rect PatchBounds(MapRiverPath path,MapBankPatch patch)
        {Rect r=default;foreach(var f in path.frames)if(Mathf.Abs(f.station-patch.station)<=patch.radius+2){float e=f.width*.5f+Mathf.Max(f.leftTransition,f.rightTransition)+3;r=Union(r,new Rect(f.center.x-e,f.center.y-e,e*2,e*2));}return r;}
        public void TranslateRiver(long id,Vector2 delta)
        {
            var r=GetRiver(id);foreach(var n in r.nodes)n.position+=delta;foreach(var rock in r.rocks)if(rock.manual)rock.position+=delta;UpsertRiver(r);
        }
        public void DeleteRiver(long id)
        {CommitStroke();RequireRiverAccess();if(!rivers.TryGetValue(id,out var p))return;var c=new Command{bounds=Pad(p.bounds,HeightSpacing*2),kind=RiverKinds};c.rivers.Add(new RiverChange{before=p.record});Execute(c);}
        public long PaintBank(long riverId,float station,float radius,bool left,bool right,bool ford=false,float shallowDepth=.25f)
        {
            var r=GetRiver(riverId);long id=MapObjectIds.New();r.patches.Add(new MapBankPatch{id=id,station=station,radius=radius,left=left,right=right,crossChannel=ford,shallowDepth=shallowDepth});UpsertRiver(r);return id;
        }
        public void RemoveBankPatch(long riverId,long patchId){var r=GetRiver(riverId);r.patches.RemoveAll(p=>p.id==patchId);UpsertRiver(r);}
        public void EditRiverRock(long riverId,MapRockInstance rock)
        {
            var r=GetRiver(riverId);int i=r.rocks.FindIndex(p=>p.id==rock.id);if(i<0)throw new InvalidDataException("Скала не принадлежит реке.");var copy=Clone(rock);copy.manual=true;r.rocks[i]=copy;UpsertRiver(r);
        }
        public void UpsertRockSection(MapRockSection input)
        {
            CommitStroke();RequireRiverAccess();var s=Clone(input);ValidateSection(s);
            if(s.rocks.Count==0)
            {
                // Same rigid module layout and catalog; no second rock-placement algorithm.
                var r=new MapRiverRecord{id=s.id,seed=s.seed};for(int i=0;i<s.points.Count;i++)r.nodes.Add(new MapRiverNode{id=i+1,position=s.points[i],width=4,leftTransition=2,rightTransition=2});
                s.rocks=LayoutRocks(new MapRiverPath(r),s.id,s.seed,true);
            }
            ValidateRockRecords(s.rocks);rockSections.TryGetValue(s.id,out var old);var c=new Command{bounds=Union(SectionBounds(old),SectionBounds(s)),kind=MapChangeKind.Rivers|MapChangeKind.Objects|MapChangeKind.Decoration};c.sections.Add(new RockSectionChange{before=old,after=s});Execute(c);
        }
        public void DeleteRockSection(long id){CommitStroke();RequireRiverAccess();if(!rockSections.TryGetValue(id,out var s))return;var c=new Command{bounds=SectionBounds(s),kind=MapChangeKind.Rivers|MapChangeKind.Objects|MapChangeKind.Decoration};c.sections.Add(new RockSectionChange{before=s});Execute(c);}
        static Rect SectionBounds(MapRockSection s){Rect r=default;if(s!=null)foreach(var p in s.points)r=Union(r,new Rect(p.x-6,p.y-6,12,12));return r;}
        MapRiverPath ValidateRiver(MapRiverRecord r)
        {
            var path=new MapRiverPath(r);foreach(var n in r.nodes)
            {
                ValidatePoint(n.position);if(n.waterHeight-n.depth<MinimumHeight||n.waterHeight>MaximumHeight)throw new InvalidDataException("Дно/вода за пределами высот карты.");
            }
            foreach(var f in path.frames)
            {
                ValidatePoint(f.Edge(1)+f.Left*f.leftTransition);ValidatePoint(f.Edge(-1)-f.Left*f.rightTransition);
                foreach(var protectedArea in heightField.Definition.protectedAreas)if(Pad(protectedArea,HeightSpacing).Contains(f.center)||protectedArea.Contains(f.Edge(1))||protectedArea.Contains(f.Edge(-1)))throw new InvalidDataException("Река задевает защищённый рельеф.");
            }
            ValidateRockRecords(r.rocks);return path;
        }
        void ValidateSection(MapRockSection s)
        {if(s==null||s.id==0||s.points==null||s.points.Count<2||s.points.Count>1024||s.rocks==null)throw new InvalidDataException("Неверный скальный участок.");foreach(var p in s.points)ValidatePoint(p);ValidateRockRecords(s.rocks);}
        void ValidateRockRecords(List<MapRockInstance> values)
        {
            if(values==null||values.Count>100000)throw new InvalidDataException("Неверные записи скал.");var ids=new HashSet<long>();
            foreach(var r in values){if(r==null||r.id==0||!ids.Add(r.id)||string.IsNullOrWhiteSpace(r.archetypeId)||r.catalogVersion<1||!Finite(r.scale)||r.scale<.8f||r.scale>1.2f||!Finite(r.yaw)||!Finite(r.topOffset)||Mathf.Abs(r.topOffset)>4)throw new InvalidDataException("Неверная скала или масштаб вне 0.8–1.2.");ValidatePoint(r.position);}
        }
        List<MapRockInstance> LayoutRocks(MapRiverPath path,long owner,int seed,bool standalone=false)
        {
            var list=new List<MapRockInstance>();if(rockDefinitions.Count==0)return list;
            for(int side=standalone?0:-1;side<=1;side+=standalone?2:2)
            {
                float station=1;int index=0;
                while(station<path.Length-1)
                {
                    long id=MapObjectIds.FromKey(owner+"/rock/"+side+"/"+index);uint h=unchecked((uint)id^(uint)seed);float pick=(h%10007)/10007f,total=0;foreach(var d in rockDefinitions)total+=d.weight;pick*=total;
                    var def=rockDefinitions[0];foreach(var d in rockDefinitions){pick-=d.weight;if(pick<=0){def=d;break;}}
                    var f=path.At(station);float scale=Mathf.Lerp(def.minimumScale,def.maximumScale,((h>>12)%997)/997f);
                    var p=standalone?f.center:f.Edge(side)+f.Left*side*(def.thickness*.22f);
                    float yaw=Mathf.Atan2(f.tangent.x,f.tangent.y)*Mathf.Rad2Deg+(side<0?180:0);
                    list.Add(new MapRockInstance{id=id,archetypeId=def.id,catalogVersion=def.version,position=p,yaw=yaw,scale=scale,station=station,side=side,bankOffset=def.thickness*.22f,topOffset=standalone?def.height:0});
                    station+=(def.length-def.overlap)*scale;index++;
                }
            }
            return list;
        }
        void SetRiver(long id,MapRiverRecord record)
        {
            if(rivers.TryGetValue(id,out var before))foreach(var key in Regions(before.bounds))if(riverIndex.TryGetValue(key,out var group)){group.Remove(id);if(group.Count==0)riverIndex.Remove(key);}
            rivers.Remove(id);if(record!=null){var path=new MapRiverPath(record);rivers.Add(id,path);foreach(var key in Regions(path.bounds)){if(!riverIndex.TryGetValue(key,out var group))riverIndex.Add(key,group=new HashSet<long>());group.Add(id);}}
        }
        void ReplaceRiverState(List<MapRiverRecord> records,List<MapRockSection> sections)
        {composedHeightCache.Clear();rivers.Clear();riverIndex.Clear();rockSections.Clear();foreach(var r in records)SetRiver(r.id,Clone(r));foreach(var s in sections)rockSections.Add(s.id,Clone(s));RiverRevision++;HeightRevision++;}
        void ValidateRiverState(List<MapRiverRecord> records,List<MapRockSection> sections)
        {
            if(records==null||sections==null||records.Count>4096||sections.Count>10000)throw new InvalidDataException("Неверные списки рек/скал.");
            var ids=new HashSet<long>();var paths=new List<MapRiverPath>();foreach(var r in records){if(!ids.Add(r.id))throw new InvalidDataException("Повтор ID реки.");paths.Add(ValidateRiver(r));}
            foreach(var s in sections){ValidateSection(s);if(!ids.Add(s.id))throw new InvalidDataException("Повтор ID участка.");}
            for(int i=0;i<paths.Count;i++)for(int j=i+1;j<paths.Count;j++)if(paths[i].bounds.Overlaps(paths[j].bounds))foreach(var a in paths[i].frames)foreach(var b in paths[j].frames)if(Vector2.Distance(a.center,b.center)<(a.width+b.width)*.5f+Mathf.Max(a.leftTransition,a.rightTransition)+Mathf.Max(b.leftTransition,b.rightTransition))throw new InvalidDataException("Перекрывающиеся реки в сохранении.");
        }
        Rect AllRiverBounds(){Rect r=default;foreach(var p in rivers.Values)r=Union(r,p.bounds);foreach(var s in rockSections.Values)r=Union(r,SectionBounds(s));return r;}
        [Serializable] sealed class FeatureState {public List<MapRiverRecord> rivers;public List<MapRockSection> sections;}
        void AddRiverReset(Command c)
        {
            if(JsonUtility.ToJson(new FeatureState{rivers=ExportRivers(),sections=ExportRockSections()})==JsonUtility.ToJson(new FeatureState{rivers=authored.rivers,sections=authored.rockSections}))return;
            var baseR=new Dictionary<long,MapRiverRecord>();foreach(var r in authored.rivers)baseR.Add(r.id,r);
            foreach(var p in rivers.Values){baseR.TryGetValue(p.record.id,out var b);c.rivers.Add(new RiverChange{before=p.record,after=b});baseR.Remove(p.record.id);}
            foreach(var r in baseR.Values)c.rivers.Add(new RiverChange{after=r});
            var baseS=new Dictionary<long,MapRockSection>();foreach(var s in authored.rockSections)baseS.Add(s.id,s);
            foreach(var s in rockSections.Values){baseS.TryGetValue(s.id,out var b);c.sections.Add(new RockSectionChange{before=s,after=b});baseS.Remove(s.id);}
            foreach(var s in baseS.Values)c.sections.Add(new RockSectionChange{after=s});
            c.bounds=Union(c.bounds,AllRiverBounds());foreach(var r in authored.rivers)c.bounds=Union(c.bounds,new MapRiverPath(r).bounds);foreach(var s in authored.rockSections)c.bounds=Union(c.bounds,SectionBounds(s));c.kind|=RiverKinds;
        }
        float RiverGridSample(int x,int z)
        {
            var key=new ChunkCoordinate(x,z);if(composedHeightCache.TryGetValue(key,out var cached))return cached;
            var p=heightField.Position(x,z);float h=heightField.Sample(x,z);
            foreach(var path in RiversAt(p))if(path.Nearest(p,out var f,out float d))
            {
                float radial=Vector2.Distance(p,f.center);if(radial>Mathf.Abs(d)+.01f)d=Mathf.Sign(d==0?1:d)*radial;
                float transition=d>=0?f.leftTransition:f.rightTransition;if(Mathf.Abs(d)>f.width*.5f+transition)continue;
                h=MapRiverPath.Bed(f,d,h);
            }
            h=Mathf.Clamp(h,MinimumHeight,MaximumHeight);composedHeightCache[key]=h;return h;
        }
        void InvalidateComposedHeight(Rect area)
        {
            if(composedHeightCache.Count==0)return;
            var lo=heightField.Grid(area.xMin-HeightSpacing*2,area.yMin-HeightSpacing*2);var hi=heightField.Grid(area.xMax+HeightSpacing*2,area.yMax+HeightSpacing*2);
            for(int z=Mathf.FloorToInt(lo.y);z<=Mathf.CeilToInt(hi.y);z++)for(int x=Mathf.FloorToInt(lo.x);x<=Mathf.CeilToInt(hi.x);x++)composedHeightCache.Remove(new ChunkCoordinate(x,z));
        }
        public float SampleBaseHeight(float x,float z)=>heightField.Height(x,z);
        float ComposedHeight(float x,float z)
        {
            if(rivers.Count==0)return heightField.Height(x,z);var g=heightField.Grid(x,z);int ix=Mathf.FloorToInt(g.x),iz=Mathf.FloorToInt(g.y);float u=g.x-ix,v=g.y-iz;
            float a=RiverGridSample(ix,iz),b=RiverGridSample(ix+1,iz),c=RiverGridSample(ix,iz+1),d=RiverGridSample(ix+1,iz+1);
            return u+v<=1?a+(b-a)*u+(c-a)*v:d+(c-d)*(1-u)+(b-d)*(1-v);
        }
        public bool QueryWater(Vector2 p,out float level,out float depth,out Vector2 flow)
        {
            foreach(var path in RiversAt(p))if(path.Nearest(p,out var f,out float d)&&Vector2.Distance(p,f.center)<=f.width*.5f)
            {level=f.water;depth=Mathf.Max(0,level-SampleHeight(p.x,p.y));flow=f.tangent*f.speed;return depth>.01f;}
            level=depth=0;flow=Vector2.zero;return false;
        }
        public bool VegetationAllowed(float x,float z,float clearance=.3f)
        {
            var footprint=new Rect(x-clearance,z-clearance,2*clearance,2*clearance);
            foreach(var reserved in authored.placementExclusions)if(reserved.Overlaps(footprint)||reserved.Contains(new Vector2(x,z)))return false;
            var p=new Vector2(x,z);foreach(var path in RiversAt(p))if(path.Nearest(p,out var f,out float d))
            {
                float sand=d>=0?f.leftSand:f.rightSand;float edge=f.width*.5f+(sand>.05f?(d>=0?f.leftTransition:f.rightTransition):2);
                if(Vector2.Distance(p,f.center)<edge+clearance)return false;
            }
            foreach(var s in rockSections.Values)foreach(var rock in s.rocks)if(!rock.removed&&(rock.position-p).sqrMagnitude<Mathf.Pow(2+clearance,2))return false;
            return true;
        }
        public float SandCoverage(float x,float z)
        {
            var p=new Vector2(x,z);float result=0;foreach(var path in RiversAt(p))if(path.Nearest(p,out var f,out float d))
            {float half=f.width*.5f,transition=d>=0?f.leftTransition:f.rightTransition,sand=d>=0?f.leftSand:f.rightSand;result=Mathf.Max(result,Mathf.Abs(d)<=half?1:sand*(1-Mathf.SmoothStep(.65f,1,(Mathf.Abs(d)-half)/transition)));}return result;
        }
        public bool CanTraverse(Vector2 from,Vector2 to,float maxDepth=.45f,float maxSlope=32,float step=.2f)
        {
            int count=Mathf.CeilToInt(Vector2.Distance(from,to)/Mathf.Clamp(step,.1f,1));float previous=SampleHeight(from.x,from.y);
            for(int i=0;i<=count;i++){var p=Vector2.Lerp(from,to,i/(float)Mathf.Max(1,count));var b=Bounds;if(p.x<b.x||p.y<b.y||p.x>b.x+b.z||p.y>b.y+b.w)return false;float h=SampleHeight(p.x,p.y);if(QueryWater(p,out _,out var depth,out _)&&depth>maxDepth)return false;if(i>0&&Mathf.Atan2(Mathf.Abs(h-previous),Vector2.Distance(from,to)/Mathf.Max(1,count))*Mathf.Rad2Deg>maxSlope)return false;previous=h;}return true;
        }
        [Serializable] sealed class LegacyTreeBase
        {public int version;public string mapId;public int revision,decorationRulesVersion;public Vector4 bounds;public LittleCastle.Rendering.SurfacePaintDocument surface;public MapHeightFieldData heightField;public List<MapTreeInstance> trees;public List<Rect> placementExclusions;}
        string LegacyTreeFingerprint()=>Fingerprint(new LegacyTreeBase{version=authored.version,mapId=MapId,revision=authored.revision,decorationRulesVersion=authored.decorationRulesVersion,bounds=Bounds,surface=authored.surface,heightField=authored.heightField,trees=authored.trees,placementExclusions=authored.placementExclusions});
    }
}
