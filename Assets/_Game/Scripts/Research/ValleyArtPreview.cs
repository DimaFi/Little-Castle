using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LittleCastle.Rendering;
using LittleCastle.CameraSystem;

namespace LittleCastle.Research
{
    public sealed class ValleyArtPreview:MonoBehaviour
    {
        public UniversalRenderPipelineAsset profile;
        public Camera studyCamera;
        public Light sun;
        public StylizedLightingGlobals globals;
        public GameObject snowfall;
        public Material water;
        RenderPipelineAsset previous;
        bool showUI=true,wind=true,snow=true,autoCapture,free;
        int view;
        string errors="";
        void Awake(){previous=QualitySettings.renderPipeline;QualitySettings.renderPipeline=profile;Application.runInBackground=true;Application.targetFrameRate=60;Application.logMessageReceived+=Log;}
        void OnDestroy(){if(QualitySettings.renderPipeline==profile)QualitySettings.renderPipeline=previous;Application.logMessageReceived-=Log;}
        void Log(string m,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors+=m+"\n"+stack+"\n";}
        public void ApplyWeather(){globals.SetWindPresentation(new Vector2(.9f,.32f),wind?.65f:0,1);snowfall.SetActive(snow);}
        public void SetView(int i)
        {
            view=i;free=false;studyCamera.GetComponent<StrategyCameraController>().enabled=false;
            Vector3[] eyes={new Vector3(57,53,-73),new Vector3(26,21,-35),new Vector3(24,22,31),new Vector3(66,20,20),new Vector3(-41,15,-24)};
            Vector3[] targets={new Vector3(-6,5,10),new Vector3(-3,4,0),new Vector3(3,2,45),new Vector3(50,8,58),new Vector3(-32,8,-1)};
            studyCamera.transform.position=eyes[i];studyCamera.transform.LookAt(targets[i]);
        }
        public void SetLight(bool evening)
        {
            sun.transform.rotation=Quaternion.Euler(evening?24:43,-38,0);sun.color=evening?new Color(1,.83f,.67f):new Color(1,.94f,.82f);sun.intensity=evening?1.1f:1.2f;globals.ApplyGlobals();
        }
        IEnumerator Start()
        {
            SetView(0);ApplyWeather();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-valleyCapture");if(at<0||at+1>=args.Length)yield break;
            autoCapture=true;showUI=false;string dir=args[at+1];Directory.CreateDirectory(dir);yield return new WaitForSecondsRealtime(4);
            string[] names={"01_overview","02_bridge","03_ford","04_snowfall","05_forest"};
            for(int i=0;i<5;i++){SetView(i);yield return new WaitForSecondsRealtime(.7f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,names[i]+".png"));}
            snow=false;wind=true;ApplyWeather();SetView(4);
            var times=new List<string>();
            for(int i=0;i<16;i++){yield return new WaitForSecondsRealtime(.12f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"wind_"+i.ToString("D3")+".png"));times.Add("wind,"+i+","+Time.unscaledTime.ToString(System.Globalization.CultureInfo.InvariantCulture));}
            wind=false;ApplyWeather();yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"wind_off.png"));
            SetView(3);snow=true;ApplyWeather();
            for(int i=0;i<16;i++){yield return new WaitForSecondsRealtime(.12f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"snow_"+i.ToString("D3")+".png"));times.Add("snow,"+i+","+Time.unscaledTime.ToString(System.Globalization.CultureInfo.InvariantCulture));}
            snow=false;ApplyWeather();yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"snow_off.png"));
            SetView(0);SetLight(true);yield return new WaitForSecondsRealtime(.7f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"06_evening.png"));
            File.WriteAllLines(Path.Combine(dir,"frame_times.csv"),times);
            File.WriteAllText(Path.Combine(dir,"player_result.txt"),"Unity="+Application.unityVersion+"\nPipeline="+GraphicsSettings.currentRenderPipeline+"\nGPU="+SystemInfo.graphicsDeviceName+"\nFive views, animated wind and snowfall, evening captured.\nErrors:\n"+errors);yield return null;Application.Quit(string.IsNullOrEmpty(errors)?0:2);
        }
        void Capture(string path){var t=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());Destroy(t);}
        void Update(){if(autoCapture)return;for(int i=0;i<5;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))SetView(i);if(Input.GetKeyDown(KeyCode.H))showUI=!showUI;if(Input.GetKeyDown(KeyCode.Escape))Application.Quit();}
        void OnGUI()
        {
            if(!showUI)return;float scale=Mathf.Clamp(Screen.width/1600f,.8f,1.4f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            GUILayout.BeginArea(new Rect(18,18,280,346),GUI.skin.box);GUILayout.Label("ДОЛИНА · ХУДОЖЕСТВЕННЫЙ СТЕНД");
            string[] names={"1  Общий вид","2  Мост и высокие берега","3  Брод","4  Зимняя область","5  Дубы и ветер"};for(int i=0;i<5;i++)if(GUILayout.Button(names[i],GUILayout.Height(26)))SetView(i);
            bool w=GUILayout.Toggle(wind,"Ветер в листве и траве"),s=GUILayout.Toggle(snow,"Снегопад в зимней области");if(w!=wind||s!=snow){wind=w;snow=s;ApplyWeather();}
            GUILayout.BeginHorizontal();if(GUILayout.Button("Дневной свет"))SetLight(false);if(GUILayout.Button("Вечерний свет"))SetLight(true);GUILayout.EndHorizontal();
            if(GUILayout.Button(free?"Вернуться к ракурсу":"Свободно осмотреть")){if(free)SetView(view);else{free=true;studyCamera.GetComponent<StrategyCameraController>().enabled=true;}}
            GUILayout.Label("H: скрыть панель · Esc: выход");GUILayout.EndArea();
        }
    }
}
