using System;
using System.Linq;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    /// <summary>
    /// These tests intentionally remain ignored before a real hydrated FBX
    /// release has been imported by the opt-in Editor command.
    /// An ignored test is NOT an integration approval.
    /// </summary>
    public sealed class BridgeStonePrefabTests
    {
        private const string PrefabPath =
            "Assets/_Game/Models/Concept/Bridge_Stone_A/Bridge_Stone_A_v002.prefab";
        private const string CatalogPath =
            "Assets/_Game/Settings/World/ConceptWorld_v001/Concept_MainWorldSpawnCatalog.asset";

        private GameObject RequireRealPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                Assert.Ignore("Bridge v002 FBX not imported. Hydrate art LFS and " +
                    "run the verified concept import command; this is NOT a PASS.");
            return prefab;
        }

        [Test]
        public void ConceptCatalog_ResolvesOnlyRealFixedBridgePrefab()
        {
            GameObject prefab = RequireRealPrefab();
            WorldSpawnCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WorldSpawnCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryResolve(FixedBridgeSiteProfile.AssetId, 41,
                out WorldSpawnCatalogEntry entry, out GameObject selected), Is.True);
            Assert.That(selected, Is.SameAs(prefab));
            Assert.That(entry.scaleMultiplier, Is.EqualTo(1f));
            Assert.That(entry.rotationOffsetEuler, Is.EqualTo(Vector3.zero));
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void AuthoredLODGroups_UseReviewedTriangleCountsAndCheapFarTier()
        {
            GameObject prefab = RequireRealPrefab();
            LODGroup[] groups = prefab.GetComponentsInChildren<LODGroup>(true);
            Assert.That(groups.Length, Is.EqualTo(2),
                "Separate structural and dressing LOD groups are required.");

            int[][] expectations = {
                new[] { 13598, 7704, 1736 },
                new[] { 12428, 5022, 80 }
            };
            for (int g = 0; g < groups.Length; g++)
            {
                LOD[] lods = groups[g].GetLODs();
                Assert.That(lods.Length, Is.EqualTo(3));
                for (int level = 0; level < lods.Length; level++)
                {
                    int triangles = 0;
                    foreach (Renderer renderer in lods[level].renderers)
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        Assert.That(filter, Is.Not.Null);
                        Assert.That(filter.sharedMesh, Is.Not.Null);
                        for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                            triangles += (int)(filter.sharedMesh.GetIndexCount(sub) / 3);

                        if (level == 2)
                        {
                            Assert.That(renderer.shadowCastingMode,
                                Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                            Assert.That(renderer.receiveShadows, Is.False);
                        }
                    }
                    Assert.That(triangles, Is.EqualTo(expectations[g][level]));
                }
            }
        }

        [Test]
        public void Prefab_HasNearCollisionAndNoDuplicateWaterOrTerrain()
        {
            GameObject prefab = RequireRealPrefab();
            var colliders = prefab.GetComponentsInChildren<MeshCollider>(true);
            Assert.That(colliders.Length, Is.GreaterThan(0));
            int triangles = 0;
            foreach (MeshCollider collider in colliders)
            {
                Assert.That(collider.convex, Is.False);
                Assert.That(collider.sharedMesh, Is.Not.Null);
                Assert.That(collider.enabled, Is.False,
                    "Near-distance collision must not be on permanently.");
                Mesh mesh = collider.sharedMesh;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    triangles += (int)(mesh.GetIndexCount(sub) / 3);
            }
            Assert.That(triangles, Is.EqualTo(286));

            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.name, Does.Not.Contain("Water"),
                    "Imported prefab must not overlap global river water.");
                Assert.That(renderer.name, Does.Not.Contain("TerrainReference"),
                    "Reference terrain should not duplicate generated heightfield.");
            }
        }

        [Test]
        public void NearCollisionBudget_TogglesWithoutMaterialClones()
        {
            GameObject prefab = RequireRealPrefab();
            GameObject runtime = Object.Instantiate(prefab);
            try
            {
                Component controller = runtime.GetComponent("BridgeStoneNearColliderBudget");
                Assert.That(controller, Is.Not.Null);
                MethodInfo apply = controller.GetType().GetMethod(
                    "ApplyDistanceSquared", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(apply, Is.Not.Null);
                apply.Invoke(controller, new object[] { 4f });

                MeshCollider[] colliders = runtime.GetComponentsInChildren<MeshCollider>(true);
                Assert.That(colliders.All(c => c.enabled), Is.True);

                apply.Invoke(controller, new object[] { 1000000f });
                Assert.That(colliders.All(c => !c.enabled), Is.True);

                foreach (Renderer renderer in runtime.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                        Assert.That(material, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(runtime);
            }
        }
    }
}
