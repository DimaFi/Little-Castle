using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed partial class MapEditSession
    {
        /// <summary>Explicit migration, preserving identities and the exact linked endpoint widths. One Undo.</summary>
        public void UpgradeRiverProfiles(IEnumerable<long> riverIds,MapRiverSectionProfile limits=null)
        {
            RequireRiverAccess();var changes=new List<RiverChange>();var seen=new HashSet<long>();
            var proposed=ExportRivers();Rect area=default;
            foreach(long id in riverIds)
            {
                if(!seen.Add(id))continue;
                if(!rivers.TryGetValue(id,out var before))throw new InvalidDataException("Река для перехода не найдена.");
                if(before.record.layoutVersion==3)continue;
                var r=Clone(before.record);r.layoutVersion=3;r.sectionProfile=limits==null?new MapRiverSectionProfile():Clone(limits);
                for(int i=0;i<r.nodes.Count;i++)
                {
                    var n=r.nodes[i];
                    if(before.Nearest(n.position,out var f,out _))n.width=Mathf.Clamp(f.width,2,60);
                    n.leftTransition=Mathf.Clamp(n.leftTransition,.5f,6);n.rightTransition=Mathf.Clamp(n.rightTransition,.5f,6);
                }
                var path=ValidateRiver(r);proposed.RemoveAll(v=>v.id==id);proposed.Add(r);
                changes.Add(new RiverChange{before=before.record,after=r});area=Union(area,Union(before.bounds,path.bounds));
            }
            if(changes.Count==0)return;
            ValidateRiverState(proposed,ExportRockSections());
            var network=riverNetwork.WithRivers(proposed,false,MapObjectIds.New);
            foreach(var fall in network.Snapshot().waterfalls)if(fall.route!=null&&!fall.route.IsEmpty&&seen.Contains(fall.downstreamRiverId))
                network=network.WithWaterfallRoute(fall.id,MapWaterfallLanding.Blend(fall.route,network.Endpoint(fall.downstreamRiverId,true)));
            ValidateRiverNetworkPlacement(network);
            foreach(long id in seen)area=Union(area,ConnectedWaterfallBounds(id,network));
            var command=new Command{bounds=Pad(area,HeightSpacing*2),kind=RiverKinds,rivers=changes};AttachRiverNetwork(command,network);
            CommitStroke();Execute(command);
        }
    }
}
