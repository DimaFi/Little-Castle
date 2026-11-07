using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using LittleCastle.Rendering;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public static partial class SoftRiverAReview
    {
        static string guiMap,guiDraft,guiTool;
        static bool guiBrush;
        static readonly List<string> guiErrors=new List<string>();
        static void CollectGuiError(string message,string stack,LogType type)
        {if((type==LogType.Error||type==LogType.Exception)&&(message.Contains("GUILayout")||message.Contains("GUI")||stack.Contains("MapAuthoringWindow")))guiErrors.Add(message+"\n"+stack);}
        static void GuiBegin()
        {
            var w=Window();if(w.VerificationSession.HasPending)throw new InvalidOperationException("User stroke is active.");
            guiMap=JsonUtility.ToJson(w.VerificationSession.ExportMap());guiDraft=RiverState(w).ExportDraftJson();
            guiTool=(string)typeof(MapAuthoringWindow).GetField("activeToolId",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w);
            guiBrush=(bool)typeof(MapAuthoringWindow).GetField("brush",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w);
            guiErrors.Clear();Application.logMessageReceived+=CollectGuiError;
        }
        static void GuiEnd()
        {
            var w=Window();Application.logMessageReceived-=CollectGuiError;
            if(guiMap==null)throw new InvalidOperationException("No GUI review snapshot.");
            w.SetTool(guiTool);typeof(MapAuthoringWindow).GetField("brush",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(w,guiBrush);
            RiverState(w).RestoreDraftJson(guiDraft);RiverState(w).Refresh(w.VerificationSession);
            File.WriteAllLines(Root+"/gui-errors.txt",guiErrors);
            bool same=guiMap==JsonUtility.ToJson(w.VerificationSession.ExportMap())&&guiDraft==RiverState(w).ExportDraftJson();
            File.WriteAllText(Root+"/gui-restored.txt",same?"Exact map/draft/tool/brush restored":"MISMATCH");
            w.Repaint();if(!same||guiErrors.Count>0)throw new InvalidOperationException("GUI review failed; inspect gui-errors.txt.");
        }
        static void AnimationProbe()
        {
            var w=Window();var view=(MapRiverPresentation)typeof(MapAuthoringWindow).GetField("riverPreview",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w);
            var systems=view.Waterfalls.SpraySystems.ToArray();var block=new MaterialPropertyBlock();float time=-1;
            var renderer=view.WaterRenderers.FirstOrDefault();if(renderer){renderer.GetPropertyBlock(block);time=block.GetFloat("_PreviewTime");}
            int live=systems.Sum(p=>p.particleCount);
            File.WriteAllText(Root+"/editor-animation.json","{\"sources\":"+systems.Length+",\"liveParticles\":"+live+",\"previewTime\":"+time.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");
            if(systems.Length<2||live==0||time<=0)throw new InvalidOperationException("Stationary Editor water/spray are not advancing.");
        }
    }
}
