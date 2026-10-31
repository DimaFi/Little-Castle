using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed partial class MapEditSession
    {
        sealed class FenceChange {public MapFenceRecord before,after;}
        readonly SortedDictionary<long,MapFencePath> fences=new SortedDictionary<long,MapFencePath>();
        MapFenceRecord fenceStroke;
        MapFenceRecord fenceStrokeBase;
        bool fenceStrokePrepend;
        public long FenceRevision {get;private set;}
        public int FenceCount=>fences.Count;
        public MapFenceRecord FenceDraft=>fenceStroke==null?null:CombineFenceStroke(fenceStroke);
        void RequireFenceAccess(){if((Access&MapEditAccess.Fences)==0)throw new UnauthorizedAccessException("Рисование заборов доступно инструментам разработчика.");}
        void InitializeFences(){ReplaceFenceState(ValidateFences(authored.fences));FenceRevision=0;}
        public List<MapFenceRecord> ExportFences(){var result=new List<MapFenceRecord>();foreach(var path in fences.Values)result.Add(Clone(path.record));return result;}
        public MapFenceRecord GetFence(long id)=>fences.TryGetValue(id,out var p)?Clone(p.record):throw new InvalidOperationException("Забор не найден.");
        public bool HasFence(long id)=>fences.ContainsKey(id);
        public List<MapFencePath> QueryFencePaths(Rect area)
        {var result=new List<MapFencePath>();foreach(var p in fences.Values)if(p.bounds.Overlaps(area))result.Add(new MapFencePath(p.record));return result;}
        List<MapFenceRecord> ValidateFences(List<MapFenceRecord> records)
        {
            var result=new List<MapFenceRecord>();var seen=new HashSet<long>();
            foreach(var r in records??new List<MapFenceRecord>())
            {
                if(result.Count>=4096)throw new InvalidDataException("Превышен лимит линий забора.");
                var path=ValidateFence(r);if(!seen.Add(r.id))throw new InvalidDataException("Повторяющийся ID забора.");result.Add(path.record);
            }
            return result;
        }
        MapFencePath ValidateFence(MapFenceRecord r)
        {
            var path=new MapFencePath(r);
            foreach(var f in path.frames)ValidatePoint(f.position);
            return path;
        }
        void ReplaceFenceState(List<MapFenceRecord> records)
        {fenceStroke=null;fenceStrokeBase=null;fences.Clear();foreach(var r in records)fences.Add(r.id,new MapFencePath(r));FenceRevision++;}
        public void UpsertFence(MapFenceRecord input)
        {
            RequireFenceAccess();var path=ValidateFence(input);CommitStroke();fences.TryGetValue(input.id,out var old);
            if(old!=null&&JsonUtility.ToJson(old.record)==JsonUtility.ToJson(path.record))return;
            if(old==null&&fences.Count>=4096)throw new InvalidDataException("Превышен лимит линий забора.");
            var command=new Command{kind=MapChangeKind.Fences,bounds=old==null?path.bounds:Union(old.bounds,path.bounds)};
            command.fences.Add(new FenceChange{before=old?.record,after=path.record});Execute(command);
        }
        public void DeleteFence(long id)
        {
            RequireFenceAccess();CommitStroke();if(!fences.TryGetValue(id,out var old))return;
            var command=new Command{kind=MapChangeKind.Fences,bounds=old.bounds};command.fences.Add(new FenceChange{before=old.record});Execute(command);
        }
        public void BeginFenceStroke(string variant,float spacing=1.6f,float smoothing=.85f,float scale=1)
        {
            RequireFenceAccess();CommitStroke();
            // Validate settings before entering a pending transaction.
            var value=new MapFenceRecord{id=MapObjectIds.FromKey(Guid.NewGuid().ToString("N")),variantId=variant,spacing=spacing,smoothing=smoothing,scale=scale,
                points=new List<Vector2>{Vector2.zero,Vector2.right}};
            _=new MapFencePath(value);value.points.Clear();fenceStroke=value;fenceStrokeBase=null;
        }
        public void AppendFencePoint(Vector2 point)
        {
            RequireFenceAccess();ValidatePoint(point);if(fenceStroke==null)throw new InvalidOperationException("Сначала начните линию забора.");
            var points=fenceStroke.points;
            if(points.Count==0&&FindFenceEndpoint(point,out var endpoint,out bool first))
            {
                fenceStrokeBase=Clone(endpoint);fenceStrokePrepend=first;
                // Continuing inherits the existing recipe, independent of the brush dropdowns.
                fenceStroke.id=endpoint.id;fenceStroke.variantId=endpoint.variantId;fenceStroke.spacing=endpoint.spacing;
                fenceStroke.smoothing=endpoint.smoothing;fenceStroke.scale=endpoint.scale;
                point=endpoint.points[first?0:endpoint.points.Count-1];
            }
            if(points.Count>0&&(points[points.Count-1]-point).sqrMagnitude<.015f*.015f)return;
            if(points.Count>=8192)throw new InvalidDataException("Разделите длинный штрих забора на несколько линий.");
            var previous=points.Count==0?point:points[points.Count-1];points.Add(point);
            Notify(SegmentBounds(previous,point,.3f),true,MapChangeKind.Fences);
        }
        Vector2 SnapFenceEndpoint(Vector2 point)
        {
            return FindFenceEndpoint(point,out var record,out bool first)?record.points[first?0:record.points.Count-1]:point;
        }
        bool FindFenceEndpoint(Vector2 point,out MapFenceRecord record,out bool first)
        {
            float nearest=.35f*.35f;record=null;first=false;
            foreach(var p in fences.Values)if(!p.record.closed)for(int end=0;end<2;end++)
            {
                int i=end==0?0:p.record.points.Count-1;float d=(p.record.points[i]-point).sqrMagnitude;
                if(d>=nearest)continue;nearest=d;record=p.record;first=end==0;
            }
            return record!=null;
        }
        MapFenceRecord CombineFenceStroke(MapFenceRecord extension)
        {
            var result=Clone(extension);if(fenceStrokeBase==null)return result;
            var points=new List<Vector2>();
            if(fenceStrokePrepend)
            {
                for(int i=extension.points.Count-1;i>0;i--)points.Add(extension.points[i]);
                points.AddRange(fenceStrokeBase.points);
            }
            else
            {
                points.AddRange(fenceStrokeBase.points);
                for(int i=1;i<extension.points.Count;i++)points.Add(extension.points[i]);
            }
            result.points=points;return result;
        }
        void CommitFenceStroke()
        {
            if(fenceStroke==null)return;var extension=fenceStroke;
            if(extension.points.Count<2){fenceStroke=null;fenceStrokeBase=null;return;}
            RequireFenceAccess();extension.points=MapFencePath.Simplify(extension.points);
            float strokeLength=0;for(int i=1;i<extension.points.Count;i++)strokeLength+=Vector2.Distance(extension.points[i-1],extension.points[i]);
            if(strokeLength<.25f){fenceStroke=null;fenceStrokeBase=null;return;}
            extension.points[extension.points.Count-1]=SnapFenceEndpoint(extension.points[extension.points.Count-1]);
            var record=CombineFenceStroke(extension);fenceStroke=null;fenceStrokeBase=null;
            if((record.points[0]-record.points[record.points.Count-1]).magnitude<.35f&&record.points.Count>3)
            {record.closed=true;record.points.RemoveAt(record.points.Count-1);}
            if(record.points.Count==2&&Vector2.Distance(record.points[0],record.points[1])<.25f)return;
            UpsertFence(record);
        }
        void CancelFenceStroke()
        {
            if(fenceStroke==null)return;var record=fenceStroke;fenceStroke=null;fenceStrokeBase=null;
            foreach(var point in record.points)Notify(new Rect(point.x-.3f,point.y-.3f,.6f,.6f),false,MapChangeKind.Fences);
        }
        void ApplyFenceCommand(Command command,bool reverse)
        {
            if(command.fences.Count==0)return;
            foreach(var change in command.fences)
            {
                var r=reverse?change.before:change.after;long id=(change.before??change.after).id;
                fences.Remove(id);if(r!=null)fences.Add(id,new MapFencePath(r));
            }
            FenceRevision++;
        }
        bool HasFenceEdits=>JsonUtility.ToJson(new FenceState{fences=ExportFences()})!=JsonUtility.ToJson(new FenceState{fences=authored.fences??new List<MapFenceRecord>()});
        void AddFenceReset(Command command)
        {
            if(!HasFenceEdits)return;
            var targets=new Dictionary<long,MapFenceRecord>();foreach(var r in authored.fences??new List<MapFenceRecord>())targets.Add(r.id,r);
            foreach(var r in fences.Values)
            {targets.TryGetValue(r.record.id,out var after);command.fences.Add(new FenceChange{before=r.record,after=after});targets.Remove(r.record.id);command.bounds=Union(command.bounds,r.bounds);}
            foreach(var r in targets.Values){command.fences.Add(new FenceChange{after=r});command.bounds=Union(command.bounds,new MapFencePath(r).bounds);}
            command.kind|=MapChangeKind.Fences;
        }
        [Serializable] sealed class FenceState {public List<MapFenceRecord> fences;}
        [Serializable] sealed class FenceBase {public string environmentFingerprint;public List<MapFenceRecord> fences;}
        string FenceBaseFingerprint()=>authored.fences==null||authored.fences.Count==0?EnvironmentBaseFingerprint():
            Fingerprint(new FenceBase{environmentFingerprint=EnvironmentBaseFingerprint(),fences=authored.fences});
    }
}
