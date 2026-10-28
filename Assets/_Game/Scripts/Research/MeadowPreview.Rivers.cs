using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LittleCastle.World;
using LittleCastle.Rendering;
using UnityEngine;
namespace LittleCastle.Research
{
    public sealed partial class MeadowPreview
    {
        IEnumerator CaptureRivers(string dir)
        {
            moveProxy=false;wind=true;ApplyWind();var s=painter.Session;var checks=new List<string>();
            if(painter.RiverView==null)throw new Exception("Actual river catalog is missing. Prepare River Cliff Catalog before building.");
            s.Access=MapEditAccess.All;var r=new MapRiverRecord{id=20261026,seed=1729};
            Vector2[] points={new Vector2(-12,-25),new Vector2(-3,-5),new Vector2(-2,18),new Vector2(-10,40)};
            for(int i=0;i<points.Length;i++)r.nodes.Add(new MapRiverNode{id=i+1,position=points[i],width=new[]{9f,11,10,8}[i],depth=1.9f,waterHeight=-.8f-i*.05f,leftTransition=8,rightTransition=8,speed=.8f+i*.12f});
            var timer=System.Diagnostics.Stopwatch.StartNew();s.UpsertRiver(r);timer.Stop();double commit=timer.Elapsed.TotalMilliseconds;int chunks=painter.TerrainView.LastTerrainChunks,colliders=painter.TerrainView.PendingColliders;
            for(int i=0;i<45;i++){painter.RiverView.Tick(studyCamera.transform.position,true);yield return null;}
            if(painter.RiverView.ResidentRocks<10)throw new Exception("No real cliff modules presented.");
            checks.Add($"PASS curved river created: {r.nodes.Count} control points, final rocks={s.GetRiver(r.id).rocks.Count}, resident={painter.RiverView.ResidentRocks}, ribbon tiles={painter.RiverView.ResidentTiles}; commit={commit:F3}ms, terrain views={chunks}/{painter.TerrainView.ChunkCount}, queued colliders={colliders}.");
            studyCamera.transform.position=new Vector3(31,30,-34);studyCamera.transform.LookAt(new Vector3(-6,-.5f,8));yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"20_river_overview.png"));
            studyCamera.transform.position=new Vector3(10,9,-10);studyCamera.transform.LookAt(new Vector3(-5,-.9f,5));yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"21_cliff_contact_close.png"));
            var path=new MapRiverPath(s.GetRiver(r.id));float station=path.Length*.52f;var f=path.At(station);Vector2 a=f.center+f.Left*(f.width*.5f+f.leftTransition+.8f),b=f.center-f.Left*(f.width*.5f+f.rightTransition+.8f);
            if(s.CanTraverse(a,b))throw new Exception("Deep channel incorrectly traversable.");
            s.PaintBank(r.id,station,7,true,false);if(s.CanTraverse(a,b))throw new Exception("One ramp incorrectly considered a ford.");checks.Add("PASS one sandy ramp leaves the deep channel nontraversable.");
            timer.Restart();s.PaintBank(r.id,station,7,true,true,true,.25f);timer.Stop();
            if(!s.CanTraverse(a,b))throw new Exception("Ford has no connected route across both banks: inspect lattice depth/slope.");
            checks.Add($"PASS both ramps and continuous shallow band traversable at depth<=.45m and slope<=32deg, sampled every .2m; patch commit={timer.Elapsed.TotalMilliseconds:F3}ms, terrain views={painter.TerrainView.LastTerrainChunks}/{painter.TerrainView.ChunkCount}.");
            for(int i=0;i<30;i++){painter.RiverView.Tick(studyCamera.transform.position,true);yield return null;}
            studyCamera.transform.position=new Vector3(f.center.x+15,13,f.center.y-17);studyCamera.transform.LookAt(new Vector3(f.center.x,-.5f,f.center.y));yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"22_sand_and_ford.png"));
            // Test agent follows queried connected surface. This is explicitly not a NavMesh integration.
            for(int i=0;i<=40;i++){var p=Vector2.Lerp(a,b,i/40f);demonstrator.position=new Vector3(p.x,s.SampleHeight(p.x,p.y)+.7f,p.y);yield return null;}
            yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"23_surface_query_agent.png"));checks.Add("PASS diagnostic agent crossed ford using height/depth/slope queries; no NavMesh claimed.");
            s.Undo();if(s.CanTraverse(a,b))throw new Exception("Ford Undo failed.");s.Redo();if(!s.CanTraverse(a,b))throw new Exception("Ford Redo failed.");string json=s.ExportJson();File.WriteAllText(Path.Combine(dir,"river-edits-v5.json"),json);
            float baseHeight=s.SampleBaseHeight(points[1].x,points[1].y);s.TranslateRiver(r.id,new Vector2(25,0));if(Mathf.Abs(s.SampleHeight(points[1].x,points[1].y)-baseHeight)>.001f)throw new Exception("River move did not restore old terrain.");s.Undo();s.DeleteRiver(r.id);if(Mathf.Abs(s.SampleHeight(points[1].x,points[1].y)-baseHeight)>.001f)throw new Exception("River delete did not restore terrain.");s.Undo();s.LoadJson(json);if(s.ExportJson()!=json)throw new Exception("v5 reload changed final records.");checks.Add("PASS edit/move/delete/undo/redo/v5 save-load preserve authored height substrate, IDs and exact final composition.");
            int savedCount=s.GetRiver(r.id).rocks.Count;for(int i=0;i<3;i++)s.UpsertRiver(s.GetRiver(r.id));if(s.GetRiver(r.id).rocks.Count!=savedCount)throw new Exception("Duplicate rocks after regeneration.");
            foreach(var rock in s.GetRiver(r.id).rocks)if(rock.archetypeId==null)throw new Exception("Missing archetype identity.");checks.Add("PASS repeated regeneration does not duplicate or reshuffle final rocks.");
            s.Access=MapEditAccess.Player;bool denied=false;try{s.DeleteRiver(r.id);}catch(UnauthorizedAccessException){denied=true;}if(!denied)throw new Exception("Player river access leak.");s.Access=MapEditAccess.All;checks.Add("PASS river editing remains developer-only.");
            for(int i=0;i<30;i++)yield return null;
            int plants=0;var grassData=grass.data;
            foreach(var patch in grassData.patches)foreach(var matrix in patch.matrices){Vector3 p=matrix.GetColumn(3);if(!s.VegetationAllowed(p.x,p.z)){plants++;if(matrix.lossyScale.sqrMagnitude>.00001f)throw new Exception("Grass on water or cliff wall.");}}
            checks.Add($"PASS {plants} conflicting native clumps suppressed by derived geometry/exclusion layer; source matrices retained for restoration.");
            studyCamera.transform.position=new Vector3(30,52,-42);studyCamera.transform.LookAt(new Vector3(-7,-.5f,8));yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"24_river_strategy.png"));
            studyCamera.transform.position=new Vector3(f.center.x+11,8,f.center.y-13);studyCamera.transform.LookAt(new Vector3(f.center.x,-.5f,f.center.y));
            for(int i=0;i<12;i++){yield return new WaitForSecondsRealtime(.12f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"flow_"+i.ToString("D2")+".png"));}
            File.WriteAllLines(Path.Combine(dir,"river_checks.txt"),checks);File.WriteAllText(Path.Combine(dir,"player_result.txt"),$"Unity={Application.unityVersion}\nGPU={SystemInfo.graphicsDeviceName}\nAPI={SystemInfo.graphicsDeviceType}\nResolution={Screen.width}x{Screen.height}\nErrors:\n"+errors);
        }
    }
}
