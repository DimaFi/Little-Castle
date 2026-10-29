using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LittleCastle.Experimental.MountainBorder.Editor;
using Object=UnityEngine.Object;

namespace LittleCastle.Experimental.MountainBorder.Tests
{
    public sealed class MountainPrototypeTests
    {
        static MountainRidgeRecipe Ridge()=>MountainPreviewSettings.ExampleRidge();
        [Test] public void SamplesRepeatAcrossReverseOrderAndJsonRoundtrip()
        {
            var r=Ridge();MountainRidgeSampler.Validate(r);string before=JsonUtility.ToJson(r);
            var points=Enumerable.Range(0,70).Select(i=>new Vector2(-110+i*3.1f,-20+i*.9f)).ToArray();
            var expected=points.Select(p=>MountainRidgeSampler.SampleHeight(r,p)).ToArray();
            var copy=JsonUtility.FromJson<MountainRidgeRecipe>(before);MountainRidgeSampler.Validate(copy);
            for(int i=points.Length-1;i>=0;i--)Assert.That(MountainRidgeSampler.SampleHeight(copy,points[i]),Is.EqualTo(expected[i]));
            Assert.That(JsonUtility.ToJson(r),Is.EqualTo(before));
        }
        [Test] public void SeedAffectsReliefButNotOutsideEnvelope()
        {
            var a=Ridge();var b=Ridge();b.seed++;
            var p=new Vector2(42,54);
            Assert.That(MountainRidgeSampler.SampleHeight(a,p),Is.Not.EqualTo(MountainRidgeSampler.SampleHeight(b,p)));
            Assert.That(MountainRidgeSampler.SampleHeight(a,new Vector2(-999,-999)),Is.EqualTo(a.baseHeight));
            Assert.That(MountainRidgeSampler.SampleHeight(b,new Vector2(-999,-999)),Is.EqualTo(b.baseHeight));
        }
        [Test] public void AdjacentIndependentMeshesShareHeightAndNormalAtNegativeBorder()
        {
            var r=Ridge();Mesh a=null,b=null;
            try
            {
                a=MountainBorderPreview.BuildMesh(r,new Vector2(-80,0),new Vector2(-40,80),2);
                b=MountainBorderPreview.BuildMesh(r,new Vector2(-40,0),new Vector2(0,80),2);
                var av=a.vertices;var bv=b.vertices;var an=a.normals;var bn=b.normals;
                for(int z=0;z<=40;z++){Assert.That(av[z*21+20],Is.EqualTo(bv[z*21]));Assert.That(an[z*21+20],Is.EqualTo(bn[z*21]));}
            }
            finally{if(a)Object.DestroyImmediate(a);if(b)Object.DestroyImmediate(b);}
        }
        [Test] public void PassCoreReachesFloorAndNeverRaisesSurface()
        {
            var a=Ridge();a.roughnessAmplitude=0;var b=Ridge();b.roughnessAmplitude=0;b.pass=null;
            Assert.That(MountainRidgeSampler.SampleHeight(a,new Vector2(0,47)),Is.EqualTo(a.pass.floorHeight));
            for(int x=-50;x<=50;x+=2)for(int z=-50;z<=90;z+=4)
                Assert.That(MountainRidgeSampler.SampleHeight(a,new Vector2(x,z)),Is.LessThanOrEqualTo(MountainRidgeSampler.SampleHeight(b,new Vector2(x,z))));
        }
        [Test] public void FlatOutsideNormalIsUnitUp()
        {Assert.That(MountainRidgeSampler.SampleNormal(Ridge(),new Vector2(-999,1000),1),Is.EqualTo(Vector3.up));}
        [TestCase("version")][TestCase("duplicate")][TestCase("nan")][TestCase("segment")][TestCase("null")]
        public void RejectsMalformedRecipe(string fault)
        {
            var r=Ridge();
            switch(fault){case "version":r.version=99;break;case "duplicate":r.nodes[1].id=r.nodes[0].id;break;
                case "nan":r.nodes[0].height=float.NaN;break;case "segment":r.nodes[1].position=r.nodes[0].position;break;case "null":r.nodes=null;break;}
            Assert.Throws<ArgumentException>(()=>MountainRidgeSampler.Validate(r));
        }
        [Test] public void RejectsExcessiveMeshBeforeAllocation()
        {Assert.Throws<ArgumentException>(()=>MountainBorderPreview.BuildMesh(Ridge(),Vector2.zero,new Vector2(10000,10000),.25f));}
        [Test] public void WaterfallEndpointsAndUvsMatchExplicitLink()
        {
            var link=MountainPreviewSettings.ExampleWaterfall();Assert.That(WaterfallLinkDraft.Validate(link),Is.True);
            var mesh=MountainBorderPreview.BuildWaterfall(link);
            try
            {var v=mesh.vertices;Assert.That((v[0]+v[1])*.5f,Is.EqualTo(link.bottom));Assert.That((v[2]+v[3])*.5f,Is.EqualTo(link.top));
                Assert.That(Vector3.Distance(v[2],v[3]),Is.EqualTo(link.width).Within(.0001));Assert.That(mesh.uv[2].y,Is.EqualTo(1));Assert.That(mesh.uv[0].y,Is.Zero);}
            finally{Object.DestroyImmediate(mesh);}
        }
        [TestCase("uphill")][TestCase("same-river")][TestCase("nan")][TestCase("extreme")]
        public void RejectsInvalidWaterfallWithoutMutation(string fault)
        {
            var link=MountainPreviewSettings.ExampleWaterfall();
            switch(fault){case "uphill":link.bottom.y=link.top.y;break;case "same-river":link.downstreamRiverId=link.upstreamRiverId;break;
                case "nan":link.width=float.NaN;break;case "extreme":link.top.x=float.MaxValue;break;}
            string before=JsonUtility.ToJson(link);Assert.That(WaterfallLinkDraft.Validate(link,out _),Is.False);Assert.That(JsonUtility.ToJson(link),Is.EqualTo(before));
        }
        [Test] public void PrivatePreviewHasNoCollidersAndLeavesActiveSceneUntouched()
        {
            var active=SceneManager.GetActiveScene().handle;int count=SceneManager.sceneCount;
            var settings=ScriptableObject.CreateInstance<MountainPreviewSettings>();
            try
            {
                using(var preview=new MountainBorderPreview(settings))
                {Assert.That(EditorSceneManager.IsPreviewScene(preview.scene),Is.True);Assert.That(SceneManager.GetActiveScene().handle,Is.EqualTo(active));
                    Assert.That(preview.scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Collider>()).Count(),Is.Zero);
                    Assert.That(preview.MountainMaterial.GetFloat("_RockSaturation"),Is.Zero);
                    Assert.That(UnityEditor.EditorUtility.IsPersistent(preview.MountainMaterial),Is.False);
                    Assert.That(preview.MountainMaterial.GetFloat("_UseSharedLighting"),Is.Zero);}
                Assert.That(SceneManager.sceneCount,Is.EqualTo(count));Assert.That(SceneManager.GetActiveScene().handle,Is.EqualTo(active));
            }
            finally{Object.DestroyImmediate(settings);}
        }
    }
}
