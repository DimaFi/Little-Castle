using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LittleCastle.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Research
{
    /// <summary>Separate review Player only; never loads or overwrites a user's saved map.</summary>
    public sealed class FlowerVerificationPreview:MonoBehaviour
    {
        public MeadowSurfacePainter painter;
        public Camera reviewCamera;
        public string output;
        readonly List<string> errors=new List<string>();
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
        IEnumerator Start()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--flower-capture")<0)
            {
                var controls=reviewCamera.GetComponent<MeadowPreview>();if(controls)controls.enabled=true;
                enabled=false;yield break;
            }
            yield return new WaitForSecondsRealtime(2);
            Directory.CreateDirectory(output);
            painter.AddStroke(new SurfacePaintStroke{id="review-flowers",road=false,scatter=true,scatterProfile=MeadowScatterProfile.Flowers,radius=8,points=new List<Vector2>{new Vector2(-8,10),new Vector2(7,19)}});
            painter.AddStroke(new SurfacePaintStroke{id="review-road",road=true,radius=1.1f,points=new List<Vector2>{new Vector2(-19,7),new Vector2(18,14)}});
            painter.AddStroke(new SurfacePaintStroke{id="review-hill",road=false,scatter=true,scatterProfile=MeadowScatterProfile.Flowers,radius=6,points=new List<Vector2>{new Vector2(-24,29)}});
            if(painter.Session.CanPlaceTree("quaternius.CommonTree_1.autumn"))
            {
                TryTree("quaternius.CommonTree_1.autumn",new Vector2(16,24));
                TryTree("quaternius.CommonTree_3",new Vector2(24,22));
            }
            painter.scatterRenderer.maximumDistance=90;
            yield return new WaitForSecondsRealtime(1);
            var targets=new[]{new Vector3(-8,0,10),new Vector3(4,0,17),new Vector3(16,0,24),new Vector3(-24,0,29)};
            var offsets=new[]{new Vector3(3,4,-6),new Vector3(8,14,-18),new Vector3(12,12,-17),new Vector3(3,6,8)};
            string[] names={"01_flat_close","02_meadow_road","03_forest_edge","04_hill"};
            for(int i=0;i<targets.Length;i++)
            {
                Vector3 target=targets[i];target.y=painter.Session.SampleHeight(target.x,target.z)+.4f;
                reviewCamera.transform.position=target+offsets[i];reviewCamera.transform.LookAt(target);
                if(reviewCamera.orthographic)reviewCamera.orthographicSize=i==0?3.8f:i==1?13:10;
                yield return new WaitForSecondsRealtime(.5f);
                Capture(Path.Combine(output,names[i]+".png"));
            }
            var scatter=painter.scatterRenderer;
            File.WriteAllText(Path.Combine(output,"player-result.txt"),$"Instances={scatter.authoredInstances}\nSubmitted={scatter.submittedInstances}\nTriangles={scatter.submittedTriangles}\nDrawCalls={scatter.drawCalls}\nErrors={errors.Count}\n"+string.Join("\n",errors));
            if(painter.treeCatalog)MapTreeThumbnailCapture.Capture(painter.treeCatalog,reviewCamera,Path.GetFullPath(Path.Combine(output,"../TreePreviews")));
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--flower-capture")>=0)Application.Quit(errors.Count==0?0:2);
        }
        void TryTree(string id,Vector2 position)
        {try{painter.Session.PlaceTree(id,position);}catch(Exception e){Debug.Log("Review tree position unavailable: "+e.Message);}}
        void Capture(string path)
        {
            var target=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(reviewCamera,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Destroy(texture);
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
    }
}
