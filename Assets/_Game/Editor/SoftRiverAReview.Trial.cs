using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    public static partial class SoftRiverAReview
    {
        static MapRiverRecord Example(MapEditSession original)
        {
            if(original.ExportRivers().Any(r=>r.id==810101))throw new InvalidOperationException("Example ID is already occupied; preserve that river.");
            var map=original.ExportMap();var errors=new List<string>();
            var starts=new[]{new Vector2(-31,-28),new Vector2(-24,-28),new Vector2(28,-28),new Vector2(31,-20),new Vector2(-30,3),new Vector2(25,2)};
            foreach(var start in starts)
            {
                try
                {
                    var r=new MapRiverRecord{id=810101,layoutVersion=3,sectionProfile=new MapRiverSectionProfile()};
                    var positions=new[]{start,start+new Vector2(3,14),start+new Vector2(-2,28),start+new Vector2(0,42)};
                    float water=positions.Min(p=>original.SampleBaseHeight(p.x,p.y))+.08f;
                    water=Mathf.Max(water,original.MinimumHeight+1.3f);
                    for(int i=0;i<positions.Length;i++)r.nodes.Add(new MapRiverNode{id=i+1,position=positions[i],width=new[]{4f,6,10,4}[i],depth=new[]{.35f,.65f,1.2f,.5f}[i],speed=new[]{.15f,.3f,1.2f,.2f}[i],waterHeight=water,leftTransition=1.2f,rightTransition=1.2f,leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand});
                    var path=new MapRiverPath(r);
                    foreach(var f in path.frames)
                    {
                        float clearance=f.width*.5f+Mathf.Max(f.leftTransition,f.rightTransition);
                        foreach(var stroke in map.surface.strokes)if(stroke.road&&!stroke.erase)
                            for(int i=0;i<stroke.points.Count;i++)
                                if(MapRiverPath.SegmentDistance(f.center,f.center,stroke.points[Mathf.Max(0,i-1)],stroke.points[i])<clearance+stroke.radius+.5f)
                                    throw new InvalidOperationException("Candidate touches an authored road.");
                        foreach(var fence in map.fences)for(int i=1;i<fence.points.Count;i++)
                            if(MapRiverPath.SegmentDistance(f.center,f.center,fence.points[i-1],fence.points[i])<clearance+1)
                                throw new InvalidOperationException("Candidate touches an authored fence.");
                    }
                    foreach(var renderer in Object.FindObjectsByType<MeshRenderer>())
                    {
                        string name=renderer.name.ToLowerInvariant();
                        if(!name.Contains("house")&&!name.Contains("cottage")&&!name.Contains("townhall")&&!name.Contains("church"))continue;
                        var b=renderer.bounds;if(path.bounds.Overlaps(new Rect(b.min.x-1,b.min.z-1,b.size.x+2,b.size.z+2)))throw new InvalidOperationException("Candidate touches an existing building.");
                    }
                    var check=new MapEditSession(original.ExportMap());check.UpsertRiver(r);
                    File.WriteAllLines(Root+"/trial/example-candidates.txt",errors.Concat(new[]{"Selected "+start}));return r;
                }
                catch(Exception e){errors.Add(start+": "+e.Message);}
            }
            File.WriteAllLines(Root+"/trial/example-candidates.txt",errors);throw new InvalidOperationException("No free valid example location: "+string.Join("; ",errors));
        }
        static MapRiverToolState RiverState(MapAuthoringWindow w)=>(MapRiverToolState)typeof(MapAuthoringWindow).GetField("riverTools",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w);
        static void Trial()
        {
            var w=Window();var original=w.VerificationSession;if(original.HasPending)throw new InvalidOperationException("Finish the current stroke first.");
            string folder=Root+"/trial";Directory.CreateDirectory(folder);
            string map=JsonUtility.ToJson(original.ExportMap());string draft=RiverState(w).ExportDraftJson();
            File.WriteAllText(folder+"/original-map.json",map);File.WriteAllText(folder+"/original-draft.json",draft);
            var candidate=new MapEditSession(original.ExportMap());
            // This specific legacy ledge needs up to 3.56 m of narrow retaining earthwork.
            // The explicit review profile raises the fill limit to 4 m; the default stays 2 m.
            candidate.UpgradeRiverProfiles(new long[]{810001,810002},new MapRiverSectionProfile{maximumFill=4});
            var example=Example(candidate);candidate.UpsertRiver(example);
            File.WriteAllText(folder+"/example.json",JsonUtility.ToJson(example,true));
            File.WriteAllText(folder+"/candidate-map.json",JsonUtility.ToJson(candidate.ExportMap(),true));
            File.WriteAllText(folder+"/candidate-save.json",candidate.ExportJson());
            var painter=Object.FindAnyObjectByType<MeadowSurfacePainter>();if(!painter)throw new InvalidOperationException("Existing painter missing.");
            var ground=MeadowSurfacePainter.FindGround(painter.terrainMaterial);
            var catalog=painter.riverCatalogOverride?painter.riverCatalogOverride:MapRockCatalog.Default;
            var cameraRoot=new GameObject("Soft river A review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=cameraRoot.AddComponent<Camera>();
            if(painter.studyCamera)camera.CopyFrom(painter.studyCamera);camera.enabled=false;camera.nearClipPlane=.05f;camera.farClipPlane=300;camera.fieldOfView=48;camera.orthographic=false;
            var extra=camera.GetUniversalAdditionalCameraData();extra.requiresDepthTexture=true;extra.requiresColorTexture=true;
            MapTerrainPresentation terrain=null;MapRiverPresentation rivers=null;
            var center=candidate.GetWaterfall(810003).route.Bounds.center;
            try
            {
                w.RestorePreview();
                void View(MapEditSession s)
                {
                    rivers?.Dispose();rivers=null;terrain?.Dispose();terrain=null;
                    terrain=new MapTerrainPresentation(s,ground,Object.FindAnyObjectByType<MeadowGrassRenderer>(),Object.FindAnyObjectByType<InnerverseGrassStudy>());
                    terrain.FlushColliderBudget(int.MaxValue);
                    rivers=new MapRiverPresentation(s,catalog,painter.waterOnlyRivers){creationBudget=1024,maximumTiles=128,maximumDistance=140};
                    rivers.Tick(center,true);
                    if(!string.IsNullOrEmpty(rivers.Waterfalls.LastError))throw new InvalidOperationException(rivers.Waterfalls.LastError);
                    foreach(var particles in rivers.Waterfalls.SpraySystems)particles.Simulate(1.2f,false,true,true);
                }
                void Shot(string name,Vector3 eye,Vector3 target)
                {camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));Capture(camera,folder+"/"+name+".png");}
                View(new MapEditSession(original.ExportMap()));
                Shot("before-oblique",center+new Vector3(21,14,-21),center);
                Shot("before-side",center+new Vector3(0,4,-19),center);
                var lip=candidate.RiverNetwork.Endpoint(810002,true);var join=new Vector3(lip.position.x,lip.waterHeight,lip.position.y);
                Shot("before-landing-close",join+new Vector3(4,1.7f,-4),join+new Vector3(-.8f,.15f,0));
                View(candidate);
                Shot("after-oblique",center+new Vector3(21,14,-21),center);
                Shot("after-side",center+new Vector3(0,4,-19),center);
                Shot("after-front",center+new Vector3(23,5,0),center);
                Shot("after-landing-close",join+new Vector3(4,1.7f,-4),join+new Vector3(-.8f,.15f,0));
                var lower=candidate.RiverNetwork.Endpoint(810002,true);var water=new Vector3(lower.position.x+3,lower.waterHeight+.25f,lower.position.y);
                Shot("water-level",water+new Vector3(8,.25f,-7),water);
                Shot("underwater-diagnostic",water+new Vector3(3,-1.4f,-4),water+new Vector3(-1,-.35f,0));
                var meander=new MapRiverPath(candidate.GetRiver(example.id));var mf=meander.At(meander.Length*.5f);var mc=new Vector3(mf.center.x,mf.water,mf.center.y);
                Shot("meander-overview",mc+new Vector3(20,27,-21),mc);
                Shot("meander-water-level",mc+new Vector3(7,.7f,-9),mc);
                Shot("meander-underwater",mc+new Vector3(0,-.6f,-2),mc+Vector3.forward*5);
                var systems=rivers.Waterfalls.SpraySystems.ToArray();
                File.WriteAllText(folder+"/spray.json","{\"quality\":\""+rivers.Waterfalls.SprayQuality+"\",\"sources\":"+systems.Length+",\"liveParticles\":"+systems.Sum(p=>p.particleCount)+",\"particleCap\":"+systems.Sum(p=>p.main.maxParticles)+"}");
                if(systems.Length<2)throw new InvalidOperationException("Missing lip/impact spray systems.");
                Directory.CreateDirectory(folder+"/motion");camera.transform.SetPositionAndRotation(center+new Vector3(21,11,-19),Quaternion.LookRotation(center-(center+new Vector3(21,11,-19))));
                var block=new MaterialPropertyBlock();var renderers=rivers.Waterfalls.Renderers.Concat(rivers.WaterRenderers).Where(r=>r.sharedMaterial&&r.sharedMaterial.HasProperty("_PreviewTime")).ToArray();
                for(int frame=0;frame<=120;frame++)
                {
                    float time=frame/12f;
                    foreach(var renderer in renderers){renderer.GetPropertyBlock(block);block.SetFloat("_PreviewTime",time);renderer.SetPropertyBlock(block);}
                    foreach(var particles in systems)particles.Simulate(time,false,true,true);
                    Capture(camera,folder+"/motion/"+frame.ToString("D4")+".png",960,600);
                }
                if(File.ReadAllBytes(folder+"/motion/0000.png").SequenceEqual(File.ReadAllBytes(folder+"/motion/0120.png")))throw new InvalidOperationException("Motion capture stayed static.");
                var errors=new List<string>();
                foreach(var shader in renderers.Select(r=>r.sharedMaterial.shader).Distinct())foreach(var message in ShaderUtil.GetShaderMessages(shader))
                    if(message.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)errors.Add(shader.name+": "+message.message);
                File.WriteAllLines(folder+"/shader-errors.txt",errors);if(errors.Count>0)throw new InvalidOperationException("Water shader errors.");
                string saved=candidate.ExportJson();var loaded=new MapEditSession(original.ExportMap());loaded.LoadJson(saved);
                if(saved!=loaded.ExportJson())throw new InvalidOperationException("v10 round trip changed candidate geometry.");
                File.WriteAllText(folder+"/checks.txt","v10 round trip PASS\nreal Unity renders, 121 frames / 10 seconds\nshaderErrors=0\n");
            }
            finally
            {
                rivers?.Dispose();terrain?.Dispose();Object.DestroyImmediate(cameraRoot);
                typeof(MapAuthoringWindow).GetMethod("BindScenePreview",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(w,null);
                bool same=map==JsonUtility.ToJson(original.ExportMap())&&draft==RiverState(w).ExportDraftJson();
                File.WriteAllText(folder+"/restored.txt",same?"Exact live map and river draft match":"RESTORE MISMATCH");
                if(!same)throw new InvalidOperationException("User map/draft changed during review.");
            }
        }
        static void Capture(Camera camera,string path,int width=1440,int height=900)
        {
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;Texture2D pixels=null;
            try
            {
                CameraCaptureCompatibility.Render(camera,rt);RenderTexture.active=rt;pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);if(pixels)Object.DestroyImmediate(pixels);}
        }
        static void Publish()
        {
            var w=Window();var s=w.VerificationSession;
            if(s.HasPending)throw new InvalidOperationException("Finish the current stroke before publication.");
            string before=JsonUtility.ToJson(s.ExportMap());
            if(before!=File.ReadAllText(Root+"/trial/original-map.json")||RiverState(w).ExportDraftJson()!=File.ReadAllText(Root+"/trial/original-draft.json"))throw new InvalidOperationException("Current map/draft changed after trial. Review it again before publication.");
            bool pendingDraft=RiverState(w).HasUnappliedDraft(s);
            var example=JsonUtility.FromJson<MapRiverRecord>(File.ReadAllText(Root+"/trial/example.json"));
            var check=new MapEditSession(s.ExportMap());check.UpgradeRiverProfiles(new long[]{810001,810002},new MapRiverSectionProfile{maximumFill=4});check.UpsertRiver(example);
            string expected=JsonUtility.ToJson(check.ExportMap());
            s.UpgradeRiverProfiles(new long[]{810001,810002},new MapRiverSectionProfile{maximumFill=4});
            string migrated=JsonUtility.ToJson(s.ExportMap());s.Undo();if(before!=JsonUtility.ToJson(s.ExportMap()))throw new InvalidOperationException("Upgrade Undo mismatch.");s.Redo();if(migrated!=JsonUtility.ToJson(s.ExportMap()))throw new InvalidOperationException("Upgrade Redo mismatch.");
            s.UpsertRiver(example);string final=JsonUtility.ToJson(s.ExportMap());s.Undo();if(migrated!=JsonUtility.ToJson(s.ExportMap()))throw new InvalidOperationException("Example Undo mismatch.");s.Redo();
            if(expected!=JsonUtility.ToJson(s.ExportMap())||final!=expected)throw new InvalidOperationException("Final candidate mismatch.");
            if(!pendingDraft)RiverState(w).Refresh(s);
            Directory.CreateDirectory(Root+"/published");File.WriteAllText(Root+"/published/map.json",JsonUtility.ToJson(s.ExportMap(),true));File.WriteAllText(Root+"/published/save.json",s.ExportJson());
            MapAuthoringRecovery.Write(Root+"/published/map.json.recovery.json",s.ExportMap(),RiverState(w).ExportDraftJson());
            typeof(MapAuthoringWindow).GetMethod("CheckpointSession",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(w,null);
            File.WriteAllText(Root+"/published/checks.txt","Main map: explicit migration + example, each one Undo/Redo PASS. IDs preserved. Source assets not applied.");
            w.Repaint();SceneView.RepaintAll();
        }
    }
}
