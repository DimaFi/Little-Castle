using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class TerrainStarterPlanTests
    {
        [Test] public void SeedReproducesScatterAndReservesHouseFootprints()
        {
            var a = new TerrainStarterPlan(314159);
            var b = new TerrainStarterPlan(314159);
            CollectionAssert.AreEqual(a.Trees, b.Trees);
            Assert.Greater(a.Trees.Count, 30);
            foreach (Vector3 h in a.Houses)
                Assert.IsTrue(a.CanPlaceFootprint(new Vector2(h.x, h.z), new Vector2(8, 7)));
            foreach (Vector3 t in a.Trees)
                foreach (Vector3 h in a.Houses)
                    Assert.Greater(Vector2.Distance(new Vector2(t.x,t.z), new Vector2(h.x,h.z)), 8);
            Assert.IsFalse(a.CanPlaceFootprint(new Vector2(47,0), new Vector2(8,7)));
        }

        [Test] public void NegativeChunkBorderMatchesHeightNormalAndRoadMask()
        {
            var plan = new TerrainStarterPlan(314159);
            Mesh left = plan.BuildChunk(-1, -1), right = plan.BuildChunk(0, -1);
            try
            {
                for (int z = 0; z <= 32; z++)
                {
                    int a = z * 33 + 32, b = z * 33;
                    Assert.AreEqual(left.vertices[a].y, right.vertices[b].y);
                    Assert.AreEqual(left.normals[a], right.normals[b]);
                    Assert.AreEqual(left.colors[a], right.colors[b]);
                }
                Assert.Greater(plan.PathMask(0, -20), .99f);
                Assert.AreEqual(0, plan.PathMask(15, -20));
            }
            finally { Object.DestroyImmediate(left); Object.DestroyImmediate(right); }
        }
    }
}
