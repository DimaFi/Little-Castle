using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using LittleCastle.Rendering;
using LittleCastle.Research;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    public static partial class ConnectedWaterM1Review
    {
        [MenuItem("Little Castle/Water/Prepare connected water visuals")]
        public static void Setup()
        {
            const string folder="Assets/_Game/Resources/MapAuthoring/ConnectedWater";
            void Folder(string path)
            {
                if(AssetDatabase.IsValidFolder(path))return;
                string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
            }
            Folder(folder);
            var profile=AssetDatabase.LoadAssetAtPath<MapWaterfallVisualProfile>("Assets/_Game/Resources/MapAuthoring/ConnectedWaterVisuals.asset");
            var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Studies/MountainBorder_20261029/WaterfallVolume.mat");
            if(!source)throw new InvalidOperationException("Existing owned waterfall material missing.");
            var reference=AssetDatabase.LoadMainAssetAtPath("Assets/_Game/Art/Studies/MountainBorder_20261029/MountainBorderProfile.asset");
            if(!reference)throw new InvalidOperationException("Existing owned water texture profile missing.");
            var serialized=new SerializedObject(reference);
            Texture Texture(string field)=>serialized.FindProperty(field)?.objectReferenceValue as Texture;
            var normal=Texture("normalTexture");var foam=Texture("foamTexture");var phase=Texture("phaseTexture");
            if(!normal||!foam||!phase)throw new InvalidOperationException("Normal/foam/phase water samples are required.");
            void Samples(Material m)
            {
                if(m.GetTexture("_NormalTex")==normal&&m.GetTexture("_FoamTex")==foam&&m.GetTexture("_PhaseTex")==phase)return;
                m.SetTexture("_NormalTex",normal);m.SetTexture("_FoamTex",foam);m.SetTexture("_PhaseTex",phase);
                EditorUtility.SetDirty(m);if(AssetDatabase.Contains(m))AssetDatabase.SaveAssetIfDirty(m);
            }
            void Finish(Material m,int mode,float opacity)
            {
                Samples(m);
                // These are owned connected-water materials, independent of the existing border study.
                m.SetColor("_WaterColor",new Color(.055f,.34f,.56f,1));
                m.SetColor("_FoamColor",new Color(.86f,.95f,1,1));
                m.SetFloat("_Opacity",opacity);m.SetFloat("_ContactSoftness",.045f);
                m.SetFloat("_FoamStrength",mode==1?1:.9f);m.SetFloat("_NormalStrength",.38f);
                m.renderQueue=mode==1?3010:3000;
                EditorUtility.SetDirty(m);if(AssetDatabase.Contains(m))AssetDatabase.SaveAssetIfDirty(m);
            }
            Material Material(string name,int mode,int cull,float opacity)
            {
                string path=folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(m){Finish(m,mode,opacity);return m;}
                m=new Material(source){name=name};m.SetFloat("_SurfaceMode",mode);m.SetFloat("_ConnectedRoute",1);
                m.SetFloat("_Cull",cull);m.SetFloat("_WaveScale",0);m.SetFloat("_FlowSpeed",4.5f);m.SetFloat("_Opacity",opacity);
                m.SetFloat("_FoamStrength",mode==1?.85f:.6f);m.SetFloat("_PreviewTime",-1);m.SetFloat("_DetailFade",1);
                Finish(m,mode,opacity);AssetDatabase.CreateAsset(m,path);return m;
            }
            if(!profile){profile=ScriptableObject.CreateInstance<MapWaterfallVisualProfile>();AssetDatabase.CreateAsset(profile,"Assets/_Game/Resources/MapAuthoring/ConnectedWaterVisuals.asset");}
            profile.body=Material("ConnectedWater_Body",0,2,.98f);profile.river=Material("ConnectedWater_River",2,2,.98f);profile.foam=Material("ConnectedWater_Foam",1,0,.86f);
            var mist=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Studies/MountainBorder_20261029/WaterMist.mat");
            if(mist)
            {
                var owned=AssetDatabase.LoadAssetAtPath<Material>(folder+"/ConnectedWater_Spray.mat");
                if(!owned){owned=new Material(mist){name="ConnectedWater_Spray"};owned.SetTexture("_NoiseTex",phase);AssetDatabase.CreateAsset(owned,folder+"/ConnectedWater_Spray.mat");}
                owned.SetColor("_Tint",new Color(.86f,.95f,1,1));owned.renderQueue=3020;
                EditorUtility.SetDirty(owned);AssetDatabase.SaveAssetIfDirty(owned);
                profile.spray=owned;
            }
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
        }

        static MapEditSession TrialSession(MapEditSession original,Vector2 upperPoint,Vector2 lowerPoint,float width=4)
        {
            var session=new MapEditSession(original.ExportMap());
            Vector2 direction=(lowerPoint-upperPoint).normalized,side=new Vector2(-direction.y,direction.x);
            float upper=session.SampleHeight(upperPoint.x,upperPoint.y),lower=session.SampleHeight(lowerPoint.x,lowerPoint.y)+.3f;
            int samples=Mathf.CeilToInt(width+1);
            for(int i=-samples;i<=samples;i++){var p=upperPoint+side*(i*.5f);upper=Mathf.Max(upper,session.SampleHeight(p.x,p.y));}
            upper=Mathf.Min(session.MaximumHeight,upper+.1f);
            MapRiverRecord River(long id,Vector2 a,Vector2 b,float y,float end) => new MapRiverRecord { id=id,layoutVersion=2,nodes=new List<MapRiverNode>{
                new MapRiverNode{id=1,position=a,width=width,depth=.6f,waterHeight=y,leftTransition=2,rightTransition=2,leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand},
                new MapRiverNode{id=2,position=b,width=width,depth=.6f,waterHeight=end,leftTransition=2,rightTransition=2,leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand}}};
            Vector2 start=upperPoint-direction*6,endPoint=lowerPoint+direction*10;
            float startLevel=Mathf.Min(session.MaximumHeight,Mathf.Min(upper+.899f,Mathf.Max(upper,session.SampleHeight(start.x,start.y)+.3f)));
            float endLevel=Mathf.Max(lower-1.499f,Mathf.Min(lower,session.SampleHeight(endPoint.x,endPoint.y)+.3f));
            session.UpsertRiver(River(810001,start,upperPoint,startLevel,upper));session.UpsertRiver(River(810002,lowerPoint,endPoint,lower,endLevel));
            var u=session.RiverNetwork.Endpoint(810001,false);var l=session.RiverNetwork.Endpoint(810002,true);
            float pad=Mathf.Max(u.width,l.width)*.6f+2;
            var area=Rect.MinMaxRect(Mathf.Min(upperPoint.x,lowerPoint.x)-pad,Mathf.Min(upperPoint.y,lowerPoint.y)-pad,Mathf.Max(upperPoint.x,lowerPoint.x)+pad,Mathf.Max(upperPoint.y,lowerPoint.y)+pad);
            var support=MapWaterfallSupportCapture.Capture(session,area,true,810001,810002);
            SnapshotSupport(support,u,l,width,upperPoint.y,upperPoint.x);
            var record=new MapWaterfallRecord{id=810003,upstreamRiverId=810001,upstreamNodeId=2,downstreamRiverId=810002,downstreamNodeId=1};
            record.route=MapWaterfallRouteBuilder.Build(u,l,support);session.CreateWaterfall(record);
            string json=session.ExportJson();var loaded=new MapEditSession(original.ExportMap());loaded.LoadJson(json);
            if(loaded.ExportJson()!=json)throw new InvalidOperationException("M1 round-trip changed the saved route.");
            return loaded;
        }

        static void Trial(bool secondary=false,bool motion=false)
        {
            string folder=Root+(secondary?"/trial-secondary":"/trial");
            Setup();Directory.CreateDirectory(folder);
            var painter=Object.FindAnyObjectByType<MeadowSurfacePainter>();
            var window=Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().FirstOrDefault(w=>w.VerificationSession!=null);
            if(!painter||!window||window.VerificationSession.HasPending)throw new InvalidOperationException("Existing WorldEditor with no active stroke is required; preserve its draft first.");
            var original=window.VerificationSession;MapEditSession trial=null;
            string originalMap=JsonUtility.ToJson(original.ExportMap());
            File.WriteAllText(folder+"/original-map.json",originalMap);
            var errors=new List<string>();
            var candidates=secondary?new[]{new Vector4(-25,44,-25,30),new Vector4(-27,44,-27,30),new Vector4(-33,54,-43,54)}:
                new[]{new Vector4(-14,48,-2,48),new Vector4(-13,47,-1,47),new Vector4(-12,46,0,46)};
            foreach(var p in candidates)
            {
                try{trial=TrialSession(original,new Vector2(p.x,p.y),new Vector2(p.z,p.w),secondary?6:4);break;}catch(Exception e){errors.Add(p+": "+e+" "+string.Join(", ",e.Data.Keys.Cast<object>().Select(k=>k+"="+e.Data[k])));}
            }
            File.WriteAllLines(folder+"/candidate-errors.txt",errors);
            if(trial==null)throw new InvalidOperationException("No valid existing ledge candidate:\n"+string.Join("\n",errors));
            File.WriteAllText(folder+"/saved-map-v9.json",trial.ExportJson());
            var ground=MeadowSurfacePainter.FindGround(painter.terrainMaterial);
            var clock=Object.FindAnyObjectByType<WorldTimeSystem>();var atmosphere=Object.FindAnyObjectByType<DayNightLightingController>();var globals=Object.FindAnyObjectByType<StylizedLightingGlobals>();
            var clockState=clock?clock.CaptureState():default;bool automatic=clock&&clock.RunAutomatically;
            MapTerrainPresentation terrain=null;MapRiverPresentation rivers=null;
            var captureRoot=new GameObject("Connected water review camera"){hideFlags=HideFlags.HideAndDontSave};var camera=captureRoot.AddComponent<Camera>();
            if(painter.studyCamera)camera.CopyFrom(painter.studyCamera);camera.enabled=false;camera.nearClipPlane=.15f;camera.farClipPlane=500;camera.fieldOfView=43;
            var cameraData=camera.GetUniversalAdditionalCameraData();cameraData.requiresDepthTexture=true;cameraData.requiresColorTexture=true;
            var link=trial.GetWaterfall(810003);var center=link.route.Bounds.center;
            Vector3 flow=link.route.sections.Last().FrontCenter-link.route.sections[0].FrontCenter;flow.y=0;flow.Normalize();
            Vector3 lateral=Vector3.Cross(flow,Vector3.up),oblique=flow*21-lateral*21+Vector3.up*14;
            try
            {
                if(clock){clock.RunAutomatically=false;clock.RestoreState(new WorldTimeState(0,13*60));}atmosphere?.ApplyLighting();globals?.ApplyGlobals();
                // Only presentation is suspended. The user's session/history/river draft is retained.
                window.RestorePreview();
                Shot("before",center+oblique,center);
                if(ground)terrain=new MapTerrainPresentation(trial,ground,Object.FindAnyObjectByType<MeadowGrassRenderer>(),Object.FindAnyObjectByType<InnerverseGrassStudy>());
                var catalog=painter.riverCatalogOverride?painter.riverCatalogOverride:MapRockCatalog.Default;
                rivers=new MapRiverPresentation(trial,catalog,painter.waterOnlyRivers){creationBudget=1024,maximumTiles=256,maximumDistance=150};
                rivers.Tick(center+oblique,true);
                if(!string.IsNullOrEmpty(rivers.Waterfalls.LastError))throw new InvalidOperationException(rivers.Waterfalls.LastError);
                Shot("day-oblique",center+oblique,center);
                Shot("day-side",center+flow-lateral*19+Vector3.up*4,center);
                Shot("day-front",center+flow*23+Vector3.up*5,center);
                if(clock)clock.RestoreState(new WorldTimeState(0,23*60));atmosphere?.ApplyLighting();globals?.ApplyGlobals();
                Shot("night-oblique",center+oblique,center);
                File.WriteAllText(folder+"/geometry.txt","routeSections="+link.route.sections.Count+"\ncolumns="+link.route.sections[0].points.Count+"\nwaterfalls="+rivers.Waterfalls.ResidentCount+"\nfreeFallSections="+link.route.sections.Count(s=>s.kind==MapWaterfallSectionKind.FreeFall)+"\nbounds="+link.route.Bounds);
                var shader=profileShader();
                var messages=ShaderUtil.GetShaderMessages(shader);
                File.WriteAllText(folder+"/shader-messages.txt",string.Join("\n",messages.Select(m=>m.severity+": "+m.message)));
                if(messages.Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))throw new InvalidOperationException("Water shader has compile errors.");
                if(motion)
                {
                    if(clock)clock.RestoreState(new WorldTimeState(0,13*60));atmosphere?.ApplyLighting();globals?.ApplyGlobals();
                    camera.transform.SetPositionAndRotation(center+oblique,Quaternion.LookRotation(-oblique));
                    Motion(camera,folder);
                }
            }
            finally
            {
                rivers?.Dispose();terrain?.Dispose();Object.DestroyImmediate(captureRoot);
                if(clock){clock.RestoreState(clockState);clock.RunAutomatically=automatic;}atmosphere?.ApplyLighting();globals?.ApplyGlobals();
                typeof(MapAuthoringWindow).GetMethod("BindScenePreview",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(window,null);
                bool preserved=JsonUtility.ToJson(original.ExportMap())==originalMap;
                File.WriteAllText(folder+"/cleanup.txt","Original draft exact match="+preserved+"\nEdit Mode="+!EditorApplication.isPlaying+"\nTemporary river/terrain/camera disposed; clock restored.\n");
                if(!preserved)throw new InvalidOperationException("Original draft changed during temporary water review.");
            }
            void Shot(string name,Vector3 eye,Vector3 target)
            {
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
                foreach(var ps in Object.FindObjectsByType<ParticleSystem>())if(ps.name=="Bounded impact droplets")ps.Simulate(2,true,true,true);
                Capture(camera,folder+"/"+name+".png");
            }
            Shader profileShader()=>Resources.Load<MapWaterfallVisualProfile>("MapAuthoring/ConnectedWaterVisuals").body.shader;
        }
    }
}
