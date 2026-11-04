using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    // Explicit, local review requests only. No map mutation or polling work without our lease.
    [InitializeOnLoad] public static partial class SoftRiverAReview
    {
        public const string Root = "E:/Games_Develop/Research/SoftRiverA_20261031";
        public const string Owner = "01a191ef-b3bd-7be0-8e9d-bd5a9894b22c";
        public const string BuildTag="A09";
        [Serializable] sealed class Lease { public string ownerThreadId, status; }
        [Serializable] sealed class Request { public string id, action,revision; }
        [Serializable] sealed class Result { public string id, status, error; }
        static double next;
        static SoftRiverAReview() { EditorApplication.update += Poll;EditorApplication.update+=PollCurrentPlay;if(PlayStage!="")Application.logMessageReceived+=CollectPlayError;EditorApplication.delayCall+=()=>File.WriteAllText(Root+"/ready.txt",BuildTag); }
        public static bool Owns()
        {
            try { var l = JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json")); return l.status == "active" && l.ownerThreadId == Owner; }
            catch { return false; }
        }
        public static void Release()
        {
            if (Owns()) File.WriteAllText("Library/SharedPcControl.json", "{\"ownerThreadId\":\"" + Owner + "\",\"status\":\"released\",\"purpose\":\"Soft river review finished\",\"updatedAtUtc\":\"" + DateTime.UtcNow.ToString("o") + "\"}");
        }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + .5;
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || !Owns())return;
            string file = Root + "/request.json";
            if (!File.Exists(file) || DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > TimeSpan.FromMinutes(10)) return;
            var r = JsonUtility.FromJson<Request>(File.ReadAllText(file));
            if(r!=null&&r.revision!=BuildTag)return;
            if (r == null || string.IsNullOrEmpty(r.id) || r.id == SessionState.GetString("LC.SoftRiverA.Request", "")) return;
            if(!Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().Any(w=>w.VerificationSession!=null))return;
            SessionState.SetString("LC.SoftRiverA.Request", r.id);
            var result = new Result { id = r.id, status = "PASS" };
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Review needs Edit Mode.");
                if (r.action == "baseline") Baseline();
                else if(r.action=="trial")Trial();
                else if(r.action=="publish")Publish();
                else if(r.action=="window-check")WindowCheck();
                else if(r.action=="gui-begin")GuiBegin();
                else if(r.action=="gui-end")GuiEnd();
                else if(r.action=="animation-probe")AnimationProbe();
                else if(r.action=="cleanup")Disarm();
                else if(r.action=="play-current")StartCurrentPlay();
                else throw new InvalidOperationException("Unknown review request.");
            }
            catch (Exception e) { result.status = "FAIL"; result.error = e.ToString(); }
            finally { File.WriteAllText(Root + "/result.json", JsonUtility.ToJson(result, true));if(PlayStage=="")Release(); }
        }
        static MapAuthoringWindow Window() => Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().Single(w => w.VerificationSession != null);
        static void Disarm()
        {
            EditorApplication.update-=Poll;
            EditorApplication.update-=PollCurrentPlay;Application.logMessageReceived-=CollectPlayError;
            var testType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LittleCastle.Tests.SoftRiverATestRunner")).FirstOrDefault(t=>t!=null);
            testType?.GetMethod("Disarm",BindingFlags.Static|BindingFlags.Public)?.Invoke(null,null);
        }
        static void Baseline()
        {
            var w = Window(); var s = w.VerificationSession;
            if (s.HasPending) throw new InvalidOperationException("Finish the user's active stroke before review.");
            string folder = Root + "/live-before"; Directory.CreateDirectory(folder);
            File.WriteAllText(folder + "/map.json", JsonUtility.ToJson(s.ExportMap(), true));
            File.WriteAllText(folder + "/save.json", s.ExportJson());
            var field = typeof(MapAuthoringWindow).GetField("riverTools", BindingFlags.NonPublic | BindingFlags.Instance);
            File.WriteAllText(folder + "/draft.json", ((MapRiverToolState)field.GetValue(w)).ExportDraftJson());
            File.WriteAllText(folder + "/window.json", EditorJsonUtility.ToJson(w, true));
            // Freeze native v9 before expanding any authoring DTOs.
            string fixture = "Assets/_Game/Tests/Editor/Fixtures/SoftRiverV9";
            if(!File.Exists(fixture+"/authored-save.json")&&s.ExportSave().version==9)
            {
                File.WriteAllText(fixture + "/authored-map.json", JsonUtility.ToJson(s.ExportMap(), true));
                File.WriteAllText(fixture + "/authored-save.json", new MapEditSession(s.ExportMap()).ExportJson());
            }
        }
    }
}
