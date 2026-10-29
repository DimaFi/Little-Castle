using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using LittleCastle.Experimental.MountainBorder.Editor;
using LittleCastle.Experimental.MountainBorder.Presentation;
using Object=UnityEngine.Object;

namespace LittleCastle.Experimental.MountainBorder.Tests
{
    public sealed class MountainArtV1Tests
    {
        [Test] public void MacroMeshQueriesMatchRenderedFacesAndContainNoInvalidTriangles()
        {
            var mesh=MountainMassBlockoutV1.Build(1729);
            try
            {
                var v=mesh.vertices;var triangles=mesh.triangles;
                Assert.That(v.Length,Is.GreaterThan(100));Assert.That(v.Length,Is.LessThan(100000));
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=v[triangles[i]];var b=v[triangles[i+1]];var c=v[triangles[i+2]];
                    Assert.That(Vector3.Cross(b-a,c-a).y,Is.GreaterThan(0));
                    if(i%39!=0)continue;var p=(a+b+c)/3;
                    Assert.That(MountainMassBlockoutV1.Height(1729,new Vector2(p.x,p.z)),Is.EqualTo(p.y).Within(.002f));
                }
            }
            finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void SeedVariationDoesNotMoveProtectedWaterConnections()
        {
            float lip=MountainMassBlockoutV1.Height(1729,new Vector2(-58,24));
            float landing=MountainMassBlockoutV1.Height(1729,new Vector2(-58,8));
            Assert.That(lip,Is.InRange(22f,23.60f));
            Assert.That(landing,Is.InRange(-2f,.45f));
            foreach(int seed in new[]{1729,1730,-87})
            {
                Assert.That(MountainMassBlockoutV1.Height(seed,new Vector2(-58,24)),Is.EqualTo(lip).Within(.002));
                Assert.That(MountainMassBlockoutV1.Height(seed,new Vector2(-58,8)),Is.EqualTo(landing).Within(.002));
            }
        }
        [Test] public void CurvedWaterHasContinuousArcUvsFadeAndTangents()
        {
            using(var water=WaterfallGeometryV1.Build())
            {
                foreach(var mesh in new[]{water.upper,water.fall,water.lower})
                {
                    Assert.That(mesh.vertexCount,Is.GreaterThan(100));Assert.That(mesh.tangents.Length,Is.EqualTo(mesh.vertexCount));
                    var u=mesh.uv;var c=mesh.colors;var n=mesh.normals;
                    for(int i=17;i<u.Length;i++)Assert.That(u[i].y,Is.GreaterThan(u[i-17].y));
                    Assert.That(c[0].r,Is.Zero);Assert.That(c[c.Length-1].r,Is.Zero);
                    foreach(var normal in n)Assert.That(normal.sqrMagnitude,Is.EqualTo(1).Within(.002));
                }
            }
        }
        [Test] public void TerrainClearsActualCurvedCascadeAndPoolFootprint()
        {
            using(var water=WaterfallGeometryV1.Build())
                foreach(var mesh in new[]{water.upper,water.fall,water.lower})
                    foreach(var vertex in mesh.vertices)
                        Assert.That(MountainMassBlockoutV1.Height(1729,new Vector2(vertex.x,vertex.z)),
                            Is.LessThan(vertex.y-.24f),"Water must not disappear into the bank: "+vertex);
        }
        [Test] public void PrivateArtPreviewLeavesSceneAndProductionAssetsAlone()
        {
            if(!UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>("Assets/_Game/Experimental/MountainBorder/Textures/UnitySampleV1/Water_NormalPacked.png"))
                Assert.Ignore("Local art fixture not imported. Import the official Shader Graph Production Ready water maps before checking this preview; geometry tests remain independent.");
            var active=SceneManager.GetActiveScene().handle;int count=SceneManager.sceneCount;
            using(var preview=new MountainArtPreviewV1())
            {
                Assert.That(UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(preview.Scene),Is.True);
                Assert.That(UnityEditor.EditorUtility.IsPersistent(preview.Land),Is.False);
                Assert.That(preview.Surface.GetTexture("_NormalTex"),Is.Not.Null);
                preview.SetClay(true);preview.SetView(MountainArtView.RTS60);
            }
            Assert.That(SceneManager.sceneCount,Is.EqualTo(count));Assert.That(SceneManager.GetActiveScene().handle,Is.EqualTo(active));
        }
    }
}
