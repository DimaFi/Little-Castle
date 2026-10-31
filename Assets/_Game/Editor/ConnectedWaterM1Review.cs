using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using LittleCastle.World;
using LittleCastle.Rendering;
using LittleCastle.Research;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Explicit lease-bound review only. An idle Editor never changes a map or starts a test.</summary>
    [InitializeOnLoad] public static partial class ConnectedWaterM1Review
    {
        public const string Root = "E:/Games_Develop/Research/ConnectedWaterM1_20261030";
        const string Owner = "01a1884e-2c88-78b1-872c-874f75849e21";
        [Serializable] sealed class Lease { public string ownerThreadId, status; }
        [Serializable] sealed class Request { public string id, action; }
        static double nextPoll;
        static ConnectedWaterM1Review() { EditorApplication.update += Poll; }
        static bool Owns()
        {
            try { var l = JsonUtility.FromJson<Lease>(File.ReadAllText(Application.dataPath + "/../Library/SharedPcControl.json")); return l.status == "active" && l.ownerThreadId == Owner; }
            catch { return false; }
        }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating || !Owns()) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            string path = Root + "/request.json";
            if (!File.Exists(path) || DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > TimeSpan.FromMinutes(10)) return;
            var r = JsonUtility.FromJson<Request>(File.ReadAllText(path));
            if (r == null || string.IsNullOrEmpty(r.id) || r.id == SessionState.GetString("LC.ConnectedWaterM1.Request", null)) return;
            SessionState.SetString("LC.ConnectedWaterM1.Request", r.id);
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Review requires Edit Mode.");
                if (r.action == "baseline") Baseline(); else if(r.action=="setup")Setup();else if(r.action=="trial")Trial();else if(r.action=="trial-secondary")Trial(true);else if(r.action=="motion")Trial(false,true);else if(r.action=="publish-demo")PublishDemo();else throw new InvalidOperationException("Unknown water review action.");
                File.WriteAllText(Root + "/result.json", "{\"id\":\"" + r.id + "\",\"status\":\"PASS\"}");
            }
            catch (Exception e) { File.WriteAllText(Root + "/result.json", JsonUtility.ToJson(new Error { id = r.id, error = e.ToString() })); Debug.LogException(e); }
            finally { Release(); }
        }
        [Serializable] sealed class Error { public string id, status = "FAIL", error; }
        static void Release()
        {
            if (!Owns()) return;
            File.WriteAllText(Application.dataPath + "/../Library/SharedPcControl.json", "{\"ownerThreadId\":\"" + Owner + "\",\"status\":\"released\",\"purpose\":\"Connected water review finished\",\"updatedAtUtc\":\"" + DateTime.UtcNow.ToString("o") + "\"}");
        }
        static void Baseline()
        {
            Directory.CreateDirectory(Root + "/baseline");
            var painter = Object.FindAnyObjectByType<MeadowSurfacePainter>();
            if (!painter || !painter.source) throw new InvalidOperationException("Open the existing WorldEditor map first.");
            var window = Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().FirstOrDefault(w => w.VerificationSession != null);
            var session = window ? window.VerificationSession : new MapEditSession(MapEditSession.FromSurface(painter.source));
            var text = new StringBuilder(); var scene = SceneManager.GetActiveScene();
            text.AppendLine("scene=" + scene.path + " dirty=" + scene.isDirty + " play=" + EditorApplication.isPlaying);
            text.AppendLine("map=" + session.MapId + " revision=" + painter.source.revision + " rivers=" + session.RiverCount + " falls=" + session.WaterfallCount + " pending=" + session.HasPending);
            var b = session.Bounds; float step = 1;
            var csv = new StringBuilder("x,z,y\n");
            for (float z = b.y + 3; z < b.y + b.w - 3; z += step)
                for (float x = b.x + 3; x < b.x + b.z - 3; x += step)
                    csv.AppendLine(x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + z.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + session.SampleHeight(x, z).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            File.WriteAllText(Root + "/baseline/terrain.csv", csv.ToString()); File.WriteAllText(Root + "/baseline/scene.txt", text.ToString());
            Capture(SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.camera : Camera.main, Root + "/baseline/current-map.png");
            // Golden documents are synthetic and never attached to the scene or user session.
            var map = new MapAuthoringDocument { mapId = "m1-v8-golden", bounds = new Vector4(-128, -128, 256, 256),
                heightField = new MapHeightFieldData { minimumHeight = -8, maximumHeight = 32 } };
            var golden = new MapEditSession(map);
            if(golden.ExportSave().version!=8||File.Exists(Root+"/baseline/v8-authored-save.json"))return;
            MapRiverRecord River(long id, float start, float end, float y) => new MapRiverRecord { id = id, nodes = new List<MapRiverNode> {
                new MapRiverNode { id = 1, position = new Vector2(0, start), waterHeight = y, width = 4 },
                new MapRiverNode { id = 2, position = new Vector2(0, end), waterHeight = y, width = 4 } } };
            golden.UpsertRiver(River(10, -60, -30, 20)); golden.UpsertRiver(River(20, 0, 30, 5));
            golden.CreateWaterfall(new MapWaterfallRecord { id = 101, upstreamRiverId = 10, upstreamNodeId = 2, downstreamRiverId = 20, downstreamNodeId = 1 });
            File.WriteAllText(Root + "/baseline/v8-linked.json", golden.ExportJson());
            var authored = golden.ExportMap(); File.WriteAllText(Root + "/baseline/v8-authored-map.json", JsonUtility.ToJson(authored, true));
            File.WriteAllText(Root + "/baseline/v8-authored-save.json", new MapEditSession(authored).ExportJson());
        }
        static void Capture(Camera source, string path, int width=1600, int height=1000)
        {
            if (!source) throw new InvalidOperationException("No scene camera for baseline.");
            var go = new GameObject("Water M0 capture") { hideFlags = HideFlags.HideAndDontSave };
            var camera = go.AddComponent<Camera>(); camera.CopyFrom(source); camera.enabled = false;
            camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32); var old = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                CameraCaptureCompatibility.Render(camera, rt); RenderTexture.active = rt;
                pixels = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); if (pixels) Object.DestroyImmediate(pixels); Object.DestroyImmediate(go); }
        }
    }
}
