using System;
using System.Collections;
using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.Rendering.Fireflies;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class FireflyIntegrationTests
    {
        [Test]
        public void FractionalLodIsContinuousAndNeverIncreasesWithDistance()
        {
            float previous=800;
            for(int i=0;i<=1000;i++)
            {
                float actual=FireflyPreviewDistribution.LodPopulation(800,i/1000f,.42f,.88f,.23f);
                Assert.That(actual,Is.LessThanOrEqualTo(previous+.0001f));
                Assert.That(previous-actual,Is.LessThan(3));previous=actual;
            }
            Assert.That(previous,Is.EqualTo(184).Within(.0001));
            Assert.That(FireflyPreviewDistribution.LodPopulation(0,1,0,1,.1f),Is.Zero);
        }

        static object Field(object value,string name)=>value.GetType().GetField(name,
            BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);

        [UnityTest]
        public IEnumerator RealGpuInstancingHonoursOffOcclusionLiveMapAndCaps()
        {
            if(!SystemInfo.supportsInstancing)Assert.Ignore("GPU instancing unsupported");
            var shader=Shader.Find("Little Castle/Effects/Firefly Preview URP");
            Assert.That(shader,Is.Not.Null);Assert.That(ShaderUtil.ShaderHasError(shader),Is.False);
            var profile=ScriptableObject.CreateInstance<FireflyPreviewProfile>();
            var root=new GameObject("Firefly GPU integration test"){hideFlags=HideFlags.HideAndDontSave,layer=31};
            var cameraRoot=new GameObject("Firefly test camera"){hideFlags=HideFlags.HideAndDontSave};
            var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            camera.orthographic=true;camera.orthographicSize=18;camera.nearClipPlane=.1f;camera.farClipPlane=100;
            camera.transform.SetPositionAndRotation(new Vector3(0,20,0),Quaternion.Euler(90,0,0));
            var target=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(256,256,TextureFormat.RGB24,false);
            var session=new MapEditSession(new MapAuthoringDocument{mapId="firefly-integration",bounds=new Vector4(-32,-32,64,64)});
            string mapBefore=session.ExportJson();
            profile.automaticWorld.enabled=true;profile.automaticWorld.meadowWeight=1;
            profile.automaticWorld.avoidReservedGround=false;profile.densityPer100SquareMetres=60;
            profile.worldDiameter=.6f;profile.cellFadeSeconds=.05f;profile.nearEnabled=false;
            profile.movementAmplitude=0;profile.farLodSurvivalFraction=1;profile.emission=4;
            var renderer=root.AddComponent<FireflyPreviewRenderer>();renderer.Connect(profile);renderer.ConnectReadOnlyWorld(session);
            renderer.quality.quality=FireflyPreviewQuality.Custom;renderer.quality.customWorldCap=400;
            renderer.quality.customRangeMetres=80;renderer.quality.graphicsVsPerformance=.5f;
            renderer.quality.adaptiveEnabled=false;
            GameObject blocker=null;
            Material blockerMaterial=null;
            try
            {
                renderer.quality.quality=FireflyPreviewQuality.Off;
                Capture(camera,target,pixels);int dark=LitPixels(pixels);
                renderer.quality.quality=FireflyPreviewQuality.Custom;
                renderer.quality.buildSamplesPerFrame=1;
                Capture(camera,target,pixels);Capture(camera,target,pixels);
                Assert.That(renderer.BuildStepsThisFrame,Is.LessThanOrEqualTo(1),"Render requests share the frame budget");
                Assert.That(renderer.PendingCellCount,Is.GreaterThan(0));
                Assert.That(renderer.SubmittedWorldCount,Is.Zero,"Incomplete cells are not drawn");
                renderer.quality.buildSamplesPerFrame=24;
                Capture(camera,target,pixels);yield return new WaitForSecondsRealtime(.15f);
                for(int i=0;i<180&&renderer.SubmittedWorldCount==0;i++){yield return null;Capture(camera,target,pixels);}
                yield return new WaitForSecondsRealtime(.15f);Capture(camera,target,pixels);
                int lit=LitPixels(pixels);
                Assert.That(lit,Is.GreaterThan(dark+20),"Native render must contain visible glow");
                Assert.That(renderer.AutomaticWorldReady,Is.True);
                renderer.quality.Resolve(out int cap,out _,out _);
                Assert.That(renderer.SubmittedWorldCount,Is.InRange(1,cap));
                Assert.That(renderer.ResidentCellCount,Is.LessThanOrEqualTo(16),"Do not build empty cells outside the live map");
                Assert.That(session.ExportJson(),Is.EqualTo(mapBefore),"Rendering must not edit the map");
                int resident=renderer.ResidentCellCount;
                typeof(FireflyPreviewRenderer).GetField("allowReflectionCamera",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(renderer,true);
                typeof(FireflyPreviewRenderer).GetField("reflectionPointLimit",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(renderer,32);
                camera.cameraType=CameraType.Reflection;
                camera.transform.position=new Vector3(1000,20,1000);
                Capture(camera,target,pixels);
                Assert.That(renderer.ResidentCellCount,Is.EqualTo(resident),"Reflection view must not evict the game camera's resident cells");
                camera.cameraType=CameraType.Game;camera.transform.position=new Vector3(0,20,0);
                blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.hideFlags=HideFlags.HideAndDontSave;
                blocker.layer=31;blocker.transform.position=new Vector3(0,9,0);blocker.transform.localScale=new Vector3(80,1,80);
                blockerMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){hideFlags=HideFlags.HideAndDontSave};
                blockerMaterial.SetColor("_BaseColor",Color.black);blocker.GetComponent<Renderer>().sharedMaterial=blockerMaterial;
                yield return null; // Register the newly created opaque renderer before culling.
                Capture(camera,target,pixels);int blocked=LitPixels(pixels);
                renderer.quality.quality=FireflyPreviewQuality.Off;yield return null;Capture(camera,target,pixels);
                Assert.That(Math.Abs(blocked-LitPixels(pixels)),Is.LessThanOrEqualTo(4),"Opaque geometry must occlude glow");
                Assert.That(renderer.ResidentCellCount,Is.Zero);
                Assert.That(Field(renderer,"generatedMaterial"),Is.Null);
                Assert.That(Field(renderer,"quad"),Is.Null);
                renderer.quality.quality=FireflyPreviewQuality.Custom;
                Capture(camera,target,pixels);
                Assert.That(renderer.AutomaticWorldReady,Is.True,"Off/on must reconnect the same live session");
                Assert.That(ShaderUtil.ShaderHasError(shader),Is.False);
            }
            finally
            {
                if(blocker)Object.DestroyImmediate(blocker);
                if(blockerMaterial)Object.DestroyImmediate(blockerMaterial);
                Object.DestroyImmediate(root);Object.DestroyImmediate(cameraRoot);Object.DestroyImmediate(profile);
                Object.DestroyImmediate(pixels);target.Release();Object.DestroyImmediate(target);
            }
        }
        static void Capture(Camera camera,RenderTexture rt,Texture2D pixels)
        {
            var old=RenderTexture.active;
            try{CameraCaptureCompatibility.Render(camera,rt);RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();}
            finally{RenderTexture.active=old;}
        }
        static int LitPixels(Texture2D pixels)
        {
            int n=0;foreach(var c in pixels.GetPixels32())if(c.r>4||c.g>4||c.b>4)n++;return n;
        }
    }
}
