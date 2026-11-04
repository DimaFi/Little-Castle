using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class MapRiverRecoveryTests
    {
        [Test] public void WaveEvaluatorMatchesGPUAndStaysInsideDeclaredEnvelope()
        {
            var shader=Shader.Find("Hidden/Little Castle/Tests/River Wave Probe");Assert.That(shader,Is.Not.Null);
            var material=new Material(shader);var rt=RenderTexture.GetTemporary(1,1,0,RenderTextureFormat.ARGBFloat);var pixels=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;
            try
            {
                for(int i=0;i<18;i++)
                {
                    var p=new Vector2(i*1.137f-7,i*.719f-3);float time=i*.37f;
                    material.SetVector("_Point",new Vector4(p.x,p.y,time,0));Graphics.Blit(Texture2D.blackTexture,rt,material);RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1,1),0,0);pixels.Apply();
                    float cpu=MapRiverWaves.Displacement(p,time);Assert.That(pixels.GetPixel(0,0).r,Is.EqualTo(cpu).Within(.0001f));Assert.That(Mathf.Abs(cpu),Is.LessThanOrEqualTo(MapRiverWaves.MaximumDisplacement));
                }
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(pixels);Object.DestroyImmediate(material);}
        }
        [TestCase(2)][TestCase(4)][TestCase(10)][TestCase(40)][TestCase(60)]
        public void RestoredWavesKeepCapsBedAndTileSeamsFixed(float width)
        {
            var map=new MapAuthoringDocument{mapId="wave-envelope",bounds=new Vector4(-80,-80,160,160),heightField=new MapHeightFieldData{minimumHeight=-8,maximumHeight=12}};
            var session=new MapEditSession(map);session.UpsertRiver(new MapRiverRecord{id=10,layoutVersion=3,sectionProfile=new MapRiverSectionProfile(),nodes=new List<MapRiverNode>{new MapRiverNode{id=1,position=new Vector2(0,-20),waterHeight=0,width=width,depth=1,leftTransition=2,rightTransition=2},new MapRiverNode{id=2,position=new Vector2(0,20),waterHeight=0,width=width,depth=1,leftTransition=2,rightTransition=2}}});
            string save=session.ExportJson();var catalog=Object.Instantiate(MapRockCatalog.Default);
            try
            {
                using(var view=new MapRiverPresentation(session,catalog,true){creationBudget=128,maximumTiles=128})
                {
                    view.Tick(Vector3.zero,true);Assert.That(view.ResidentTiles,Is.GreaterThan(1));
                    var seen=new Dictionary<Vector3,float>();int moving=0,duplicates=0;
                    foreach(var renderer in view.WaterRenderers)
                    {
                        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var data=new List<Vector4>();mesh.GetUVs(2,data);Assert.That(data.Count,Is.EqualTo(vertices.Length));
                        for(int i=0;i<vertices.Length;i++)
                        {
                            float weight=data[i].w;Assert.That(weight,Is.InRange(0,1));if(normals[i].y<0)Assert.That(weight,Is.Zero);
                            if(normals[i].y>.5&&Mathf.Abs(vertices[i].z)>=19.999f)Assert.That(weight,Is.Zero);
                            if(weight>0)moving++;
                            if(normals[i].y>.5&&seen.TryGetValue(vertices[i],out float other)){duplicates++;Assert.That(weight,Is.EqualTo(other).Within(.000001));}
                            if(normals[i].y>.5)seen[vertices[i]]=weight;
                        }
                    }
                    Assert.That(moving,Is.GreaterThan(100));Assert.That(duplicates,Is.GreaterThan(10));
                }
                Assert.That(session.ExportJson(),Is.EqualTo(save));session.Undo();session.Redo();Assert.That(session.ExportJson(),Is.EqualTo(save));
            }
            finally{if(catalog)Object.DestroyImmediate(catalog);}
        }
        [Test] public void ReflectionBudgetAndLegacyShaderRemainAvailable()
        {
            Assert.That(MapRiverReflections.Budget(RiverEffectsQuality.Low),Is.Zero);Assert.That(MapRiverReflections.Budget(RiverEffectsQuality.Medium),Is.EqualTo(1));Assert.That(MapRiverReflections.Budget(RiverEffectsQuality.High),Is.EqualTo(2));
            Assert.That(Shader.Find("Little Castle/Rivers/Uber Water Adapted"),Is.Not.Null);
            var profile=Resources.Load<MapWaterfallVisualProfile>("MapAuthoring/ConnectedWaterVisuals");
            foreach(var material in new[]{profile.river,profile.body})foreach(string property in new[]{"_RiverNormalMap","_RiverShoreMap","_RiverFoamMap","_RiverCausticMap"})Assert.That(material.GetTexture(property),Is.Not.Null,material.name+" "+property);
        }
        [Test] public void UpperLipConsumesItsOwnCurrentFrameSlotWithoutBorrowingLowerPlane()
        {
            var session=new MapEditSession(new MapAuthoringDocument{mapId="lip-reflection",bounds=new Vector4(-8,-8,16,16)});
            var root=new GameObject("Lip reflection binding test");var material=new Material(Shader.Find("Little Castle/Experimental/Waterfall Volume V2"));
            try
            {
                var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                using(var view=new MapRiverPresentation(session,MapRockCatalog.Default,true))
                {
                    var reflections=view.Reflections;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var slots=(System.Array)typeof(MapRiverReflections).GetField("slots",flags).GetValue(reflections);
                    var upper=slots.GetValue(0);var lower=slots.GetValue(1);var type=upper.GetType();
                    var upperTexture=new RenderTexture(16,16,0);var lowerTexture=new RenderTexture(16,16,0);
                    upperTexture.Create();lowerTexture.Create();
                    var upperVP=Matrix4x4.Translate(new Vector3(0,4,0));var lowerVP=Matrix4x4.Translate(new Vector3(0,1,0));
                    void Set(object slot,float height,RenderTexture texture,Matrix4x4 vp,int frame)
                    {type.GetField("height").SetValue(slot,height);type.GetField("texture").SetValue(slot,texture);type.GetField("vp").SetValue(slot,vp);type.GetField("frame").SetValue(slot,frame);}
                    Set(upper,4,upperTexture,upperVP,Time.frameCount);Set(lower,1,lowerTexture,lowerVP,Time.frameCount);
                    var count=typeof(MapRiverReflections).GetProperty("RenderedSlots").GetSetMethod(true);
                    var apply=typeof(MapRiverReflections).GetMethod("Apply",flags);var block=new MaterialPropertyBlock();
                    count.Invoke(reflections,new object[]{1});apply.Invoke(reflections,new object[]{renderer,1f,true,4f});renderer.GetPropertyBlock(block);
                    Assert.That(block.GetFloat("_RiverLipReflectionValid"),Is.EqualTo(1));Assert.That(block.GetTexture("_RiverLipReflection"),Is.SameAs(upperTexture));
                    Assert.That(block.GetMatrix("_RiverLipReflectionVP"),Is.EqualTo(upperVP));Assert.That(block.GetFloat("_RiverReflectionValid"),Is.Zero,"An upper-only Medium slot cannot act as the receiving plane.");
                    count.Invoke(reflections,new object[]{2});apply.Invoke(reflections,new object[]{renderer,1f,true,4f});renderer.GetPropertyBlock(block);
                    Assert.That(block.GetFloat("_RiverReflectionValid"),Is.EqualTo(1));Assert.That(block.GetTexture("_RiverReflection"),Is.SameAs(lowerTexture));Assert.That(block.GetMatrix("_RiverReflectionVP"),Is.EqualTo(lowerVP));
                    Set(upper,4,upperTexture,upperVP,Time.frameCount-1);apply.Invoke(reflections,new object[]{renderer,1f,true,4f});renderer.GetPropertyBlock(block);
                    Assert.That(block.GetFloat("_RiverLipReflectionValid"),Is.Zero,"Stale upper captures fall back to the same environment as an unserved upper river.");
                }
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(material);}
        }
        [Test] public void FallOutlivingRiverTilesClearsBothReflectionValidityFlags()
        {
            string path=Application.dataPath+"/_Game/Tests/Editor/Fixtures/SoftRiverV9/authored-map.json";
            var session=new MapEditSession(JsonUtility.FromJson<MapAuthoringDocument>(System.IO.File.ReadAllText(path)));
            var cameraRoot=new GameObject("Empty river resident reflection test");
            try
            {
                var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;
                using(var view=new MapRiverPresentation(session,MapRockCatalog.Default,true))
                {
                    var endpoint=session.RiverNetwork.Endpoint(810001,false);
                    view.Waterfalls.Tick(new Vector3(endpoint.position.x,endpoint.waterHeight+1,endpoint.position.y),true);
                    Assert.That(view.WaterRenderers,Is.Empty);Assert.That(view.Waterfalls.LastError,Is.Null);
                    var renderer=view.Waterfalls.Renderers.First(r=>r.gameObject.name=="Contact and free-fall volume");
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                    block.SetFloat("_RiverReflectionValid",1);block.SetFloat("_RiverLipReflectionValid",1);renderer.SetPropertyBlock(block);
                    typeof(MapRiverReflections).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view.Reflections,new object[]{default(UnityEngine.Rendering.ScriptableRenderContext),camera});
                    renderer.GetPropertyBlock(block);
                    Assert.That(block.GetFloat("_RiverReflectionValid"),Is.Zero);Assert.That(block.GetFloat("_RiverLipReflectionValid"),Is.Zero);
                    Assert.That(view.Reflections.RenderedSlots,Is.Zero);
                }
            }
            finally{Object.DestroyImmediate(cameraRoot);}
        }
    }
}
