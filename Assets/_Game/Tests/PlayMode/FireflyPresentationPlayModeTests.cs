using System.Collections;
using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.Rendering.Fireflies;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LittleCastle.Tests
{
    public sealed class FireflyPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator OptInRendererRunsAtPausedTimeAndReleasesOnDisable()
        {
            var root=new GameObject("Firefly PlayMode test");
            var cameraRoot=new GameObject("Firefly PlayMode camera");
            var profile=ScriptableObject.CreateInstance<FireflyPreviewProfile>();
            var target=new RenderTexture(128,128,24);
            float oldScale=Time.timeScale;
            try
            {
                profile.automaticWorld.enabled=true;profile.automaticWorld.meadowWeight=1;
                profile.automaticWorld.avoidReservedGround=false;profile.densityPer100SquareMetres=20;
                profile.nearEnabled=false;
                var map=new MapEditSession(new MapAuthoringDocument{mapId="firefly-play",bounds=new Vector4(-32,-32,64,64)});
                string before=map.ExportJson();
                var globals=root.AddComponent<StylizedLightingGlobals>();
                var renderer=root.AddComponent<FireflyPreviewRenderer>();
                renderer.Connect(profile);renderer.ConnectReadOnlyWorld(map);
                renderer.quality.quality=FireflyPreviewQuality.Low;renderer.quality.adaptiveEnabled=false;
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(FireflyPreviewRenderer).GetField("existingLighting",flags).SetValue(renderer,globals);
                var camera=cameraRoot.AddComponent<Camera>();camera.targetTexture=target;
                camera.orthographic=true;camera.orthographicSize=20;
                camera.transform.SetPositionAndRotation(new Vector3(0,20,0),Quaternion.Euler(90,0,0));
                // This isolated test controls only the presentation signal, never world data.
                globals.ApplyGlobals();
                yield return null;yield return null;yield return null;
                Assert.That(renderer.SubmittedWorldCount,Is.GreaterThan(0));
                renderer.quality.Resolve(out int cap,out _,out _);
                Assert.That(renderer.SubmittedWorldCount,Is.LessThanOrEqualTo(cap));
                var material=(Material)typeof(FireflyPreviewRenderer).GetField("generatedMaterial",flags).GetValue(renderer);
                float previous=material.GetFloat("_FireflyTime");
                Time.timeScale=0;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(material.GetFloat("_FireflyTime"),Is.GreaterThan(previous));
                Assert.That(map.ExportJson(),Is.EqualTo(before));
                renderer.enabled=false;yield return null;
                Assert.That(renderer.ResidentCellCount,Is.Zero);
                Assert.That(typeof(FireflyPreviewRenderer).GetField("generatedMaterial",flags).GetValue(renderer),Is.Null);
            }
            finally
            {
                Time.timeScale=oldScale;
                Object.Destroy(root);Object.Destroy(cameraRoot);Object.Destroy(profile);
                target.Release();Object.Destroy(target);
            }
        }
    }
}
