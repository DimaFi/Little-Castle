using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LittleCastle.World;
using LittleCastle.Rendering;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    public static partial class ConnectedWaterM1Review
    {
        static void SnapshotSupport(MapWaterfallSupport support,MapRiverEndpoint upper,MapRiverEndpoint lower,float width,float z,float x)
        {
            string folder=Root+"/support-snapshots";Directory.CreateDirectory(folder);
            string file=folder+"/w"+width.ToString(System.Globalization.CultureInfo.InvariantCulture)+"_z"+z.ToString(System.Globalization.CultureInfo.InvariantCulture)+"_x"+x.ToString(System.Globalization.CultureInfo.InvariantCulture)+".bin";
            var triangles=(System.Collections.IEnumerable)typeof(MapWaterfallSupport).GetField("triangles",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(support);
            using(var stream=File.Create(file))using(var writer=new BinaryWriter(stream))
            {
                writer.Write(upper.position.x);writer.Write(upper.position.y);writer.Write(upper.waterHeight);writer.Write(upper.width);
                writer.Write(lower.position.x);writer.Write(lower.position.y);writer.Write(lower.waterHeight);writer.Write(lower.width);
                writer.Write(support.TriangleCount);
                foreach(var triangle in triangles)
                {
                    var type=triangle.GetType();writer.Write((string)type.GetField("key").GetValue(triangle));writer.Write((int)type.GetField("index").GetValue(triangle));
                    foreach(string field in new[]{"a","b","c"}){var p=(Vector3)type.GetField(field).GetValue(triangle);writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
                }
            }
        }
        // Real Unity frames, deterministic presentation time. This does not measure runtime FPS.
        static void Motion(Camera camera,string folder)
        {
            string frames=folder+"/motion-frames";Directory.CreateDirectory(frames);
            var profile=Resources.Load<MapWaterfallVisualProfile>("MapAuthoring/ConnectedWaterVisuals");
            var renderers=Object.FindObjectsByType<Renderer>().Where(r=>r.sharedMaterial==profile.body||r.sharedMaterial==profile.river||r.sharedMaterial==profile.foam||r.sharedMaterial==profile.spray).ToArray();
            var old=renderers.Select(r=>{var p=new MaterialPropertyBlock();r.GetPropertyBlock(p);return p;}).ToArray();
            var drops=Object.FindObjectsByType<ParticleSystem>().Where(p=>p.name=="Bounded impact droplets").ToArray();
            try
            {
                for(int frame=0;frame<=120;frame++)
                {
                    float time=frame/12f;
                    foreach(var r in renderers){var p=new MaterialPropertyBlock();r.GetPropertyBlock(p);p.SetFloat("_PreviewTime",time);r.SetPropertyBlock(p);}
                    foreach(var p in drops)p.Simulate(time+2,true,true,true);
                    Capture(camera,frames+"/frame_"+frame.ToString("D4")+".png",960,600);
                }
                File.WriteAllText(folder+"/motion.txt","Real Unity render: 121 frames, 0..10 presentation seconds, 12 fps, 960x600. Editor deterministic preview; no runtime FPS claim.\n");
            }
            finally{for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].SetPropertyBlock(old[i]);}
        }

        static void PublishDemo()
        {
            var window=Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().Single(w=>w.VerificationSession!=null);
            var session=window.VerificationSession;
            if(session.HasPending)throw new InvalidOperationException("Finish the current user stroke before adding the water chain.");
            string expected=File.ReadAllText(Root+"/trial/original-map.json");
            if(JsonUtility.ToJson(session.ExportMap())!=expected)throw new InvalidOperationException("The map changed after review; rebuild the trial before appending water.");
            var saved=JsonUtility.FromJson<MapEditSave>(File.ReadAllText(Root+"/trial/saved-map-v9.json"));
            var upper=saved.rivers.Single(r=>r.id==810001);var lower=saved.rivers.Single(r=>r.id==810002);var link=saved.riverNetwork.waterfalls.Single(r=>r.id==810003);
            Directory.CreateDirectory(Root+"/demo");
            File.WriteAllText(Root+"/demo/user-session-before.json",session.ExportJson());
            File.WriteAllText(Root+"/demo/user-map-before.json",JsonUtility.ToJson(session.ExportMap(),true));
            // Validate the entire append and transport before touching the existing user's history.
            var check=new MapEditSession(session.ExportMap());check.CreateConnectedWaterChain(upper,lower,link);
            string valid=check.ExportJson();var reload=new MapEditSession(session.ExportMap());reload.LoadJson(valid);
            if(reload.ExportJson()!=valid)throw new InvalidOperationException("Reviewed chain did not round-trip.");
            session.CreateConnectedWaterChain(upper,lower,link);
            File.WriteAllText(Root+"/demo/world-editor-water-v9.json",session.ExportJson());
            File.WriteAllText(Root+"/demo/world-editor-map-v9.json",JsonUtility.ToJson(session.ExportMap(),true));
            typeof(MapAuthoringWindow).GetMethod("CheckpointSession",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(link.route.Bounds.center,Quaternion.Euler(28,-45,0),24,false,true);
            window.Repaint();SceneView.RepaintAll();
            File.WriteAllText(Root+"/demo/result.txt","Added 2 rivers and 1 saved waterfall to existing World Editor draft as ONE Undo command. Existing map content retained; source asset and scene not applied. Private standalone map and v9 save exported.\n");
        }
    }
}
