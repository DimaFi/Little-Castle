using LittleCastle.Experimental.MountainBorder.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder.Tests
{
    public sealed class WaterfallLinkGeometryTests
    {
        [Test] public void ExplicitLevelsKeepTheirEndpointsAndRecomputeArcLength()
        {
            var upper=new Vector3(20,31,-12);var lower=new Vector3(8,4,-19);
            using(var water=WaterfallGeometryV1.Between(upper,lower,8))
            {
                Assert.That(water.lip,Is.EqualTo(upper));Assert.That(water.landing,Is.EqualTo(lower));
                foreach(var mesh in new[]{water.upper,water.fall,water.lower})
                {
                    var uv=mesh.uv;var vertices=mesh.vertices;
                    for(int row=1;row<97;row++)Assert.That(uv[row*17].y-uv[(row-1)*17].y,
                        Is.EqualTo(Vector3.Distance(vertices[row*17+8],vertices[(row-1)*17+8])).Within(.001f));
                    foreach(var normal in mesh.normals)Assert.That(normal.sqrMagnitude,Is.EqualTo(1).Within(.001f));
                }
            }
            Assert.Throws<System.ArgumentException>(()=>WaterfallGeometryV1.Between(Vector3.zero,Vector3.up,5));
        }
    }
}
