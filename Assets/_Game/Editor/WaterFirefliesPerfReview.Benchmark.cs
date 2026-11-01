using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public static partial class WaterFirefliesPerfReview
    {
        static void Benchmark(MapAuthoringWindow w)
        {
            BeginBUI(w);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var type=typeof(MapAuthoringWindow);
            var terrain=type.GetField("terrainPreview",flags).GetValue(w);
            var trees=type.GetField("treePreview",flags).GetValue(w);
            var water=type.GetField("riverPreview",flags).GetValue(w);
            var originals=Resources.FindObjectsOfTypeAll<Mesh>().Where(m=>m&&m.name.StartsWith("Terrain view ")).ToArray();
            var rows=new List<string>{"step,previewMs,terrainIdentity,treeIdentity,waterIdentity,originalMeshesAlive"};
            try
            {
                for(int i=0;i<3;i++)
                {
                    var watch=System.Diagnostics.Stopwatch.StartNew();
                    Action<MapRiverRecord> edit=river=>river.nodes[1].position.x+=.05f;
                    type.GetMethod("ChangeRiverHandle",flags).Invoke(w,new object[]{edit});
                    type.GetMethod("UpdateRiverGesturePreview",flags).Invoke(w,new object[]{true});
                    watch.Stop();
                    rows.Add(FormattableString.Invariant($"{i},{watch.Elapsed.TotalMilliseconds:F3},{ReferenceEquals(terrain,type.GetField("terrainPreview",flags).GetValue(w))},{ReferenceEquals(trees,type.GetField("treePreview",flags).GetValue(w))},{ReferenceEquals(water,type.GetField("riverPreview",flags).GetValue(w))},{originals.All(m=>m)}"));
                }
                type.GetMethod("FinishRiverGesture",flags).Invoke(w,null);
                EndBUI(w);
                File.WriteAllLines(Root+"/regional-preview.csv",rows);
            }
            finally
            {
                if(type.GetField("riverGesture",flags).GetValue(w)!=null)
                    type.GetMethod("CancelRiverGesture",flags).Invoke(w,new object[]{true});
            }
        }
    }
}
