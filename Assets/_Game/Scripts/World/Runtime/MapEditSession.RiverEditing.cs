using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed partial class MapEditSession
    {
        // Only the gesture's detached session calls this; live history is never cleared for preview.
        internal void ForgetDetachedPreviewHistory(){undo.Clear();redo.Clear();}
        /// <summary>Validate the complete candidate before changing live data/history.</summary>
        public void ApplyRiverEdit(MapRiverRecord input)
        {
            RequireRiverAccess();if(HasPending)throw new InvalidOperationException("Завершите текущий штрих.");
            if(!rivers.ContainsKey(input.id)){UpsertRiver(input);return;}
            var command=PrepareRiverEdit(input);if(command!=null)Execute(command);
        }
        /// <summary>Run the complete baseline validation/rock/route preparation without changing data or history.</summary>
        public MapRiverRecord ValidateRiverEditCandidate(MapRiverRecord input)
        {
            RequireRiverAccess();if(HasPending)throw new InvalidOperationException("Завершите текущий штрих.");
            if(!rivers.ContainsKey(input.id))
            {
                // New-river placement retains the existing Upsert behavior on a detached map.
                var preview=new MapEditSession(ExportMap(),Access);preview.ApplyRiverEdit(input);return preview.GetRiver(input.id);
            }
            var command=PrepareRiverEdit(input);
            return Clone(command==null?rivers[input.id].record:command.rivers[0].after);
        }
        Command PrepareRiverEdit(MapRiverRecord input)
        {
            var previous=rivers[input.id];
            var record=Clone(input);var path=ValidateRiver(record);
            if(JsonUtility.ToJson(previous.record)==JsonUtility.ToJson(record))return null;
            foreach(var rock in record.rocks)if(!rock.manual)
            {
                var originalRock=previous.record.rocks.Find(v=>v.id==rock.id);
                var f=path.At((originalRock?.station??rock.station)/previous.Length*path.Length);
                rock.position=f.Edge(rock.side)+f.Left*rock.side*rock.bankOffset;
                rock.yaw=Mathf.Atan2(f.tangent.x,f.tangent.y)*Mathf.Rad2Deg+(rock.side<0?180:0);rock.station=f.station;
            }
            var proposed=ExportRivers();proposed.RemoveAll(r=>r.id==record.id);proposed.Add(record);
            ValidateRiverState(proposed,ExportRockSections());
            var graph=ExportRiverNetwork();bool rebuild=false;
            foreach(var fall in graph.waterfalls)
                if(fall.route!=null&&!fall.route.IsEmpty&&(fall.upstreamRiverId==record.id||fall.downstreamRiverId==record.id))
                {
                    bool upstream=fall.upstreamRiverId==record.id;
                    var oldPoint=new MapRiverEndpoint(previous,!upstream);var newPoint=new MapRiverEndpoint(path,!upstream);
                    if(oldPoint.nodeId!=newPoint.nodeId)throw new InvalidDataException("Связанный конец нельзя удалять или продолжать через водопад.");
                    if(oldPoint.position!=newPoint.position||oldPoint.width!=newPoint.width||oldPoint.waterHeight!=newPoint.waterHeight||oldPoint.tangent!=newPoint.tangent||oldPoint.depth!=newPoint.depth||oldPoint.speed!=newPoint.speed||oldPoint.surfaceSlope!=newPoint.surfaceSlope)
                    {fall.route=null;rebuild=true;}
                }
            var next=new MapRiverNetwork(proposed,graph);
            if(rebuild)
            {
                var document=ExportMap();document.rivers=proposed;document.riverNetwork=graph;
                var candidate=new MapEditSession(document,Access);
                foreach(var fall in graph.waterfalls)if(fall.route==null&&HasWaterfall(fall.id)&&GetWaterfall(fall.id).route!=null&&!GetWaterfall(fall.id).route.IsEmpty)
                {
                    var upper=next.Endpoint(fall.upstreamRiverId,false);var lower=next.Endpoint(fall.downstreamRiverId,true);
                    Rect area=Union(WaterfallBounds(GetWaterfall(fall.id)),WaterfallBounds(fall,next));
                    var support=MapWaterfallSupportCapture.Capture(candidate,Pad(area,2),true,fall.upstreamRiverId,fall.downstreamRiverId);
                    var route=MapWaterfallRouteBuilder.Build(upper,lower,support);next=next.WithWaterfallRoute(fall.id,route);
                }
            }
            ValidateRiverNetworkPlacement(next);
            Rect dirty=Union(previous.bounds,path.bounds);dirty=Union(dirty,Union(ConnectedWaterfallBounds(record.id,riverNetwork),ConnectedWaterfallBounds(record.id,next)));
            var command=new Command{bounds=Pad(dirty,HeightSpacing*2),kind=RiverKinds};
            command.rivers.Add(new RiverChange{before=previous.record,after=record});AttachRiverNetwork(command,next);
            return command;
        }
    }
}
