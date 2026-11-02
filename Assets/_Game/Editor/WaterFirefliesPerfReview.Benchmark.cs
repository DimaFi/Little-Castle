using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using LittleCastle.World;
using LittleCastle.Rendering;
using LittleCastle.Research;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public static partial class WaterFirefliesPerfReview
    {
        static void WaterColourComparison(MapAuthoringWindow w)
        {
            var painter=UnityEngine.Object.FindAnyObjectByType<MeadowSurfacePainter>();
            if(!painter||!painter.studyCamera)throw new InvalidOperationException("No study camera");
            var view=(MapRiverPresentation)typeof(MapAuthoringWindow).GetField("riverPreview",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w);
            if(view==null)throw new InvalidOperationException("No river preview");
            string map=JsonUtility.ToJson(w.VerificationSession.ExportMap());
            var profile=view.EffectsProfile;float previous=profile.refractionBackground;
            var renderers=view.WaterRenderers.Cast<Renderer>().Concat(view.Waterfalls.Renderers).ToArray();
            var blocks=new MaterialPropertyBlock[renderers.Length];
            string root="E:/Games_Develop/Research/WaterFirefliesFinish_20261102/water-ab";Directory.CreateDirectory(root);
            try
            {
                for(int i=0;i<renderers.Length;i++)
                {
                    blocks[i]=new MaterialPropertyBlock();renderers[i].GetPropertyBlock(blocks[i]);
                    var block=new MaterialPropertyBlock();renderers[i].GetPropertyBlock(block);block.SetFloat("_PreviewTime",5);renderers[i].SetPropertyBlock(block);
                }
                // In-memory, scoped A/B only; never dirties or saves this profile asset.
                profile.refractionBackground=1;
                Capture(painter.studyCamera,root+"/legacy-background.png",1200,750);
                profile.refractionBackground=.55f;
                Capture(painter.studyCamera,root+"/palette-retained.png",1200,750);
                File.WriteAllText(root+"/status.txt",map==JsonUtility.ToJson(w.VerificationSession.ExportMap())?"PASS same map, camera, lighting and water phase":"FAIL map changed");
            }
            finally
            {
                profile.refractionBackground=previous;
                for(int i=0;i<renderers.Length;i++)if(renderers[i]&&blocks[i]!=null)renderers[i].SetPropertyBlock(blocks[i]);
            }
        }
        static void Benchmark(MapAuthoringWindow w)
        {
            typeof(MapAuthoringWindow).GetMethod("EnsureScenePreview",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w,null);
            BeginBUI(w);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var type=typeof(MapAuthoringWindow);
            var terrain=type.GetField("terrainPreview",flags).GetValue(w);
            var trees=type.GetField("treePreview",flags).GetValue(w);
            var water=type.GetField("riverPreview",flags).GetValue(w);
            var originals=Resources.FindObjectsOfTypeAll<Mesh>().Where(m=>m&&m.name.StartsWith("Terrain view ")).ToArray();
            var rows=new List<string>{"step,previewMs,terrainIdentity,treeIdentity,waterIdentity,originalMeshesAlive,solveMs,applyMs,exportMs,sessionMs,solverApplyMs,rebindMs,maskMs"};
            MapRiverEditSolver.CaptureTimings=true;
            try
            {
                for(int i=0;i<3;i++)
                {
                    var watch=System.Diagnostics.Stopwatch.StartNew();
                    Action<MapRiverRecord> edit=river=>river.nodes[1].position.x+=.05f;
                    type.GetMethod("ChangeRiverHandle",flags).Invoke(w,new object[]{edit});
                    type.GetMethod("UpdateRiverGesturePreview",flags).Invoke(w,new object[]{true});
                    watch.Stop();
                    var gesture=(MapRiverEditGesture)type.GetField("riverGesture",flags).GetValue(w);
                    var valid=gesture.LastValid;
                    rows.Add(FormattableString.Invariant($"{i},{watch.Elapsed.TotalMilliseconds:F3},{ReferenceEquals(terrain,type.GetField("terrainPreview",flags).GetValue(w))},{ReferenceEquals(trees,type.GetField("treePreview",flags).GetValue(w))},{ReferenceEquals(water,type.GetField("riverPreview",flags).GetValue(w))},{originals.All(m=>m)},{gesture.LastSolveMilliseconds:F3},{gesture.LastPreviewApplyMilliseconds:F3},{valid.exportMilliseconds:F3},{valid.sessionMilliseconds:F3},{valid.applyMilliseconds:F3},{type.GetField("lastRiverRebindMilliseconds",flags).GetValue(w)},{type.GetField("lastRiverMaskMilliseconds",flags).GetValue(w)}"));
                }
                var drainWatch=System.Diagnostics.Stopwatch.StartNew();double maximumSlice=0,maximumWater=0;int slices=0;
                var pending=type.GetProperty("RiverPreviewWorkPending",flags);var flush=type.GetMethod("Tick",flags);
                while((bool)pending.GetValue(w))
                {
                    var slice=System.Diagnostics.Stopwatch.StartNew();flush.Invoke(w,null);slice.Stop();
                    maximumSlice=Math.Max(maximumSlice,slice.Elapsed.TotalMilliseconds);
                    maximumWater=Math.Max(maximumWater,((MapRiverPresentation)water).LastMeshMilliseconds);
                    if(++slices>100000)throw new InvalidOperationException("Preview queue did not drain");
                }
                drainWatch.Stop();
                var currentGesture=(MapRiverEditGesture)type.GetField("riverGesture",flags).GetValue(w);
                var expected=(byte[])((byte[])type.GetField("pixels",flags).GetValue(w)).Clone();
                var area=(Rect)type.GetField("riverGestureMaskArea",flags).GetValue(w);var bounds=w.VerificationSession.Bounds;
                var dirty=SurfacePaintRasterizer.PixelRect(bounds,512,512,area);
                SurfacePaintRasterizer.Rebuild(expected,bounds,512,512,currentGesture.PreviewSession.QuerySurface(area),dirty);
                SurfacePaintRasterizer.ApplyRiverExclusions(expected,bounds,512,512,currentGesture.PreviewSession,dirty);
                if(!expected.SequenceEqual((byte[])type.GetField("riverGesturePixels",flags).GetValue(w)))throw new InvalidOperationException("Budgeted mask differs from synchronous oracle");
                File.WriteAllText(Root+"/regional-preview-budget.txt",FormattableString.Invariant($"Coalesced updates -> complete mask oracle PASS; full editor ticks={slices}; totalMs={drainWatch.Elapsed.TotalMilliseconds:F3}; maxOwnerTickMs={maximumSlice:F3}; maxWaterTickMs={maximumWater:F3}"));
                var finishWatch=System.Diagnostics.Stopwatch.StartNew();
                type.GetMethod("FinishRiverGesture",flags).Invoke(w,null);
                finishWatch.Stop();File.WriteAllText(Root+"/regional-preview-finish-ms.txt",finishWatch.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
                EndBUI(w);
                File.WriteAllLines(Root+"/regional-preview.csv",rows);
            }
            finally
            {
                MapRiverEditSolver.CaptureTimings=false;
                if(type.GetField("riverGesture",flags).GetValue(w)!=null)
                    type.GetMethod("CancelRiverGesture",flags).Invoke(w,new object[]{true});
            }
        }
    }
}
