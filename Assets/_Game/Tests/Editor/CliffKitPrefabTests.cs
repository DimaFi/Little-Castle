using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using LittleCastle.World;

namespace LittleCastle.Tests
{
    public sealed class CliffKitPrefabTests
    {
        private const string Root="Assets/_Game/Art/Imported/CliffKit/v001/";
        [Test] public void SeparateCatalogResolvesFourteenThreeTierDecorativePrefabs()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<WorldSpawnCatalog>(Root+"CliffKitCatalog.asset");
            Assert.That(catalog,Is.Not.Null);Assert.That(catalog.Entries.Count,Is.EqualTo(14));
            foreach(var entry in catalog.Entries)
            {
                Assert.That(catalog.TryResolve(entry.archetypeId,-123,out _,out var prefab),Is.True);
                Assert.That(prefab.GetComponentsInChildren<Collider>().Length,Is.Zero);
                var lods=prefab.GetComponent<LODGroup>().GetLODs();Assert.That(lods.Length,Is.EqualTo(3));
                int last=int.MaxValue;
                for(int lod=0;lod<3;lod++)
                {
                    int triangles=0;
                    foreach(var r in lods[lod].renderers)
                    {
                        var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                        for(int s=0;s<mesh.subMeshCount;s++)triangles+=(int)mesh.GetIndexCount(s)/3;
                        foreach(var m in r.sharedMaterials){Assert.That(m,Is.Not.Null);Assert.That(m.enableInstancing,Is.True);}
                        if(lod==2){Assert.That(r.shadowCastingMode,Is.EqualTo(ShadowCastingMode.Off));Assert.That(r.receiveShadows,Is.False);}
                    }
                    Assert.That(triangles,Is.LessThan(last));last=triangles;
                }
            }
        }
        [Test] public void RampGeometryAndSocketsActuallyAscendAlongPositiveZ()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Ramp_Hike_A.prefab");
            var instance=Object.Instantiate(prefab);
            try
            {
                var entry=instance.transform.Find("Socket_Entry");var exit=instance.transform.Find("Socket_Exit");
                Assert.That(entry.localPosition,Is.EqualTo(Vector3.zero));Assert.That(exit.localPosition,Is.EqualTo(new Vector3(0,6.5f,24)));
                var renderer=instance.GetComponent<LODGroup>().GetLODs()[0].renderers[0];
                Assert.That(renderer.bounds.center.z,Is.EqualTo(12).Within(.01f));
                Assert.That(renderer.bounds.size.z,Is.EqualTo(24).Within(.01f));
            }
            finally{Object.DestroyImmediate(instance);}
        }
    }
}
