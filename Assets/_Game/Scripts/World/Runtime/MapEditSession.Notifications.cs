using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed partial class MapEditSession
    {
        int notificationDepth;
        readonly Dictionary<ChunkCoordinate,MapRegionChange> queuedChanges=new Dictionary<ChunkCoordinate,MapRegionChange>();
        readonly List<MapRegionChange> dispatchChanges=new List<MapRegionChange>();
        void NotifyCommand(Command command)
        {
            if(notificationDepth==0||command.rivers.Count>0||command.sections.Count>0){Notify(command.bounds,false,command.kind);return;}
            foreach(var stroke in command.added)NotifyStrokeRegions(stroke,false);
            foreach(var stroke in command.removed)NotifyStrokeRegions(stroke,false);
            NotifyHeightRegions(command.heights,false);
            foreach(var tree in command.treeChanges)
            {
                if(tree.before!=null)Notify(TreeBounds(tree.before),false,MapChangeKind.Objects);
                if(tree.after!=null)Notify(TreeBounds(tree.after),false,MapChangeKind.Objects);
            }
        }
        void NotifyStrokeRegions(LittleCastle.Rendering.SurfacePaintStroke stroke,bool preview)
        {
            if(stroke==null)return;
            for(int i=0;i<stroke.points.Count;i++)Notify(SegmentBounds(stroke.points[Mathf.Max(0,i-1)],stroke.points[i],stroke.radius),preview);
        }
        void NotifyHeightRegions(IEnumerable<HeightChange> heights,bool preview)
        {
            foreach(var change in heights)
            {
                var p=heightField.Position(change.x,change.z);
                Notify(new Rect(p.x-HeightSpacing*2,p.y-HeightSpacing*2,HeightSpacing*4,HeightSpacing*4),preview,MapChangeKind.Height);
            }
        }
        public NotificationBatch BatchNotifications(){notificationDepth++;return new NotificationBatch(this);}
        public readonly struct NotificationBatch : IDisposable
        {
            readonly MapEditSession session;
            internal NotificationBatch(MapEditSession session){this.session=session;}
            public void Dispose()=>session.FlushNotifications();
        }
        void QueueNotification(MapRegionChange change)
        {
            if(notificationDepth==0){Changed?.Invoke(change);return;}
            foreach(var key in Regions(change.bounds))
            {
                var region=RegionBounds(key);
                var clipped=Rect.MinMaxRect(Mathf.Max(region.xMin,change.bounds.xMin),Mathf.Max(region.yMin,change.bounds.yMin),
                    Mathf.Min(region.xMax,change.bounds.xMax),Mathf.Min(region.yMax,change.bounds.yMax));
                if(clipped.width<=0||clipped.height<=0)continue;
                if(queuedChanges.TryGetValue(key,out var previous))
                    queuedChanges[key]=new MapRegionChange(Union(previous.bounds,clipped),previous.preview&&change.preview,previous.kind|change.kind);
                else queuedChanges.Add(key,new MapRegionChange(clipped,change.preview,change.kind));
            }
        }
        void FlushNotifications()
        {
            if(--notificationDepth!=0)return;
            dispatchChanges.Clear();dispatchChanges.AddRange(queuedChanges.Values);queuedChanges.Clear();
            foreach(var change in dispatchChanges)Changed?.Invoke(change);
            dispatchChanges.Clear();
        }
    }
}
