using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.Research
{
    /// <summary>Standalone-only temporal validation; never installed into map tools.</summary>
    public sealed class SpatialIndirectTemporalStudy
    {
        readonly SpatialIndirectStudyReview review;
        readonly SpatialIndirectVolume field;
        readonly string output;
        readonly List<Step> steps=new List<Step>();
        readonly List<Frame> frames=new List<Frame>(32000);
        readonly string[] args=Environment.GetCommandLineArgs();
        readonly Vector3 point=new Vector3(-6,2.2f,2.93f);
        int sequence;
        SpatialIndirectTemporalStudy(SpatialIndirectStudyReview review,string output){this.review=review;field=review.field;this.output=output;}
        public static IEnumerator Run(SpatialIndirectStudyReview review,string output)=>new SpatialIndirectTemporalStudy(review,output).Execute();
        bool Has(string flag)=>Array.IndexOf(args,flag)>=0;
        float Arg(string name,float fallback){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?float.Parse(args[i+1],System.Globalization.CultureInfo.InvariantCulture):fallback;}
        void Record(string phase)
        {
            var color=field.SampleDisplayed(point,Vector3.back,out bool fallback);
            frames.Add(new Frame{phase=phase,time=Time.realtimeSinceStartupAsDouble,hour=review.clock.CurrentHour,gameMinutes=review.clock.Clock.AbsoluteGameMinutes,requestedMinutes=field.RequestedClockMinutes,publishedMinutes=field.PublishedClockMinutes,blend=field.DisplayBlend,version=field.Publication?.PublishedRevision??0,request=field.Publication?.RequestRevision??0,pending=field.Cache.PendingCount,color=color,fallback=fallback,solveMs=field.LastSolveMs,publishMs=field.LastPublishMs,uploadBytes=field.LastUploadedBytes});
        }
        IEnumerator Wait(string reason)
        {
            double start=Time.realtimeSinceStartupAsDouble,solve=field.TotalSolveMs,publish=field.TotalPublishMs,first=-1;long bytes=field.TotalUploadBytes,changes=field.DisplayChangeSerial;int count=field.PublicationCount;
            double max=0,maxTotal=0;
            while(!field.Ready)
            {
                if(Time.realtimeSinceStartupAsDouble-start>180)throw new Exception("Temporal convergence timeout: "+reason);
                Record(reason);max=Math.Max(max,field.LastSolveMs);maxTotal=Math.Max(maxTotal,field.LastSolveMs+field.LastUploadMs);
                if(first<0&&(field.UseTemporal?field.PublicationCount>count:field.DisplayChangeSerial>changes))first=(Time.realtimeSinceStartupAsDouble-start)*1000;
                if(Has("-giProgressCapture")&&reason=="sunset"&&Time.frameCount%3==0)yield return review.Capture("transition-"+(sequence++).ToString("D3"));
                yield return null;
            }
            max=Math.Max(max,field.LastSolveMs);maxTotal=Math.Max(maxTotal,field.LastSolveMs+field.LastUploadMs);Record(reason);
            var step=new Step{reason=reason,wallMs=(Time.realtimeSinceStartupAsDouble-start)*1000,firstPublicationMs=first,solveMs=field.TotalSolveMs-solve,publishMs=field.TotalPublishMs-publish,uploadedBytes=field.TotalUploadBytes-bytes,maxSolveFrameMs=max,maxSolvePublishFrameMs=maxTotal,request=field.Publication?.RequestRevision??0,published=field.Publication?.PublishedRevision??0,geometryRevision=field.Publication?.PublishedGeometryRevision??0,gpuBytes=field.BufferBytes,commitMs=field.LastCommitMs,commitUploadMs=field.LastCommitUploadMs,commitUploadBytes=field.LastCommitUploadBytes};steps.Add(step);
            Debug.Log("LC_GI_TEMPORAL "+reason+" wallMs="+step.wallMs.ToString("F1")+" solveMs="+step.solveMs.ToString("F1"));
        }
        void Dump(string name)
        {
            if(!Has("-giDump"))return;
            using(var w=new BinaryWriter(File.Create(output+"/"+name+"-probes.bin")))foreach(var p in field.Cache.Data){Write(w,p.c0);Write(w,p.cx);Write(w,p.cy);Write(w,p.cz);}
        }
        static void Write(BinaryWriter w,Vector4 v){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}
        IEnumerator Execute()
        {
            if(Has("-giProgressCapture"))field.blendSeconds=1;
            field.continuousLighting=false;review.SetView(0);yield return Wait("initial");Dump("day");yield return review.Capture("temporal-overview-day");
            var baseline=(SpatialIndirectTrace.Probe[])field.Cache.Data.Clone();review.SetView(1);yield return review.Capture("temporal-under-roof-day");
            review.SetHour(18.5f);yield return Wait("sunset");Dump("sunset");yield return review.Capture("temporal-under-roof-sunset");
            review.SetHour(12);yield return Wait("return-day");
            var roof=field.proxies[1];var oldBounds=roof.bounds;int oldRequest=field.Publication?.RequestRevision??0;
            field.CommitProxy(1,false,oldBounds,roof.albedo,true);
            if(!roof.active||(field.Publication?.RequestRevision??0)!=oldRequest)throw new Exception("Preview changed committed publication.");
            field.CommitProxy(1,false,oldBounds,roof.albedo);bool barrier=true;
            if(field.UseTemporal)
            {
                barrier=field.SampleDisplayed(new Vector3(-6,.5f,4),Vector3.up,out _).sqrMagnitude<1e-12f;
                if(!barrier)throw new Exception("Old indirect survived the geometry safety barrier.");
            }
            if(Has("-giProgressCapture"))yield return review.Capture("geometry-barrier");
            yield return null;field.CommitProxy(1,true,oldBounds,roof.albedo);
            var moved=oldBounds;moved.center+=Vector3.right*12;roof.visual.transform.position=moved.center;
            yield return null;field.CommitProxy(1,true,moved,roof.albedo);
            yield return null;roof.visual.transform.position=oldBounds.center;field.CommitProxy(1,true,oldBounds,roof.albedo);
            yield return Wait("rapid-undo-remote-move");Dump("restored");
            for(int i=0;i<baseline.Length;i++){var a=baseline[i];var b=field.Cache.Data[i];if(!a.c0.Equals(b.c0)||!a.cx.Equals(b.cx)||!a.cy.Equals(b.cy)||!a.cz.Equals(b.cz))throw new Exception("Undo failed exact transport restoration at "+i);}
            review.SetHour(1);yield return Wait("night");Dump("night");review.SetView(0);yield return review.Capture("temporal-overview-night");
            Application.targetFrameRate=-1;yield return review.Measure(false);yield return review.Measure(true);
            // Isolate the extra history-read shader path from CPU solve. Five-second
            // fade deliberately exceeds this uncapped 600-frame measurement window.
            field.blendSeconds=5;review.SetHour(18.5f);
            while(field.Cache.PendingCount>0||(field.UseTemporal&&field.Publication.Pending))yield return null;
            yield return review.Measure(true);review.WriteBenchmarks();
            while(!field.Ready)yield return null;field.blendSeconds=.2f;Application.targetFrameRate=60;
            float speed=Arg("-giCycleSpeed",0);Cycle cycle=null;
            if(speed>0)
            {
                review.SetHour(6);yield return Wait("cycle-start");review.SetView(0);
                double startGame=review.clock.Clock.AbsoluteGameMinutes,startTime=Time.realtimeSinceStartupAsDouble;int startPublications=field.PublicationCount;long startGeometry=field.Transport.GeometryBuilds;
                field.continuousLighting=true;review.clock.SimulationSpeedMultiplier=speed;review.clock.RunAutomatically=true;
                int milestone=0;double[] moments={360,750,870,1110,1320,1440};
                while(review.clock.Clock.AbsoluteGameMinutes-startGame<1440)
                {
                    if(Time.realtimeSinceStartupAsDouble-startTime>700/speed+30)throw new Exception("Continuous cycle timed out.");
                    Record("continuous");double progress=review.clock.Clock.AbsoluteGameMinutes-startGame;
                    if(milestone<moments.Length&&progress>=moments[milestone]){yield return review.Capture("cycle-"+(milestone++).ToString("D2"));}
                    yield return null;
                }
                review.clock.RunAutomatically=false;field.continuousLighting=false;field.RefreshLighting();yield return Wait("cycle-settle");
                cycle=new Cycle{speed=speed,realSeconds=Time.realtimeSinceStartupAsDouble-startTime,gameMinutes=review.clock.Clock.AbsoluteGameMinutes-startGame,dayRealSeconds=review.clock.Settings.DayRealSeconds,nightRealSeconds=review.clock.Settings.NightRealSeconds,publications=field.PublicationCount-startPublications,geometryRebuilds=field.Transport.GeometryBuilds-startGeometry};
                if(cycle.geometryRebuilds!=0)throw new Exception("Clock changes rebuilt primary geometry transport.");
            }
            File.WriteAllText(output+"/temporal-steps.json",JsonUtility.ToJson(new Steps{temporal=field.UseTemporal,blendSeconds=.2f,geometryBarrierPassed=barrier,rapidUndoExact=true,steps=steps.ToArray(),cycle=cycle},true));
            File.WriteAllText(output+"/temporal-frames.json",JsonUtility.ToJson(new Frames{frames=frames.ToArray()},false));
            File.WriteAllText(output+"/result.txt","PASS coherent snapshots, preview isolation, geometry barrier (temporal mode), rapid undo/move, exact final transport.\n");
        }
        [Serializable] public sealed class Step{public string reason;public double wallMs,firstPublicationMs,solveMs,publishMs,maxSolveFrameMs,maxSolvePublishFrameMs,commitMs,commitUploadMs;public long uploadedBytes,commitUploadBytes;public int request,published,geometryRevision,gpuBytes;}
        [Serializable] public sealed class Cycle{public float speed,dayRealSeconds,nightRealSeconds;public double realSeconds,gameMinutes;public long geometryRebuilds;public int publications;}
        [Serializable] public sealed class Steps{public bool temporal,geometryBarrierPassed,rapidUndoExact;public float blendSeconds;public Step[] steps;public Cycle cycle;}
        [Serializable] public sealed class Frame{public string phase;public double time,gameMinutes,requestedMinutes,publishedMinutes,solveMs,publishMs;public float hour,blend;public int version,request,pending,uploadBytes;public Vector3 color;public bool fallback;}
        [Serializable] public sealed class Frames{public Frame[] frames;}
    }
}
