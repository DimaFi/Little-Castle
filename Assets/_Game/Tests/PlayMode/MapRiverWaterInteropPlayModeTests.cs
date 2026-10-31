using System.Collections;
using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LittleCastle.Tests
{
    public sealed class MapRiverWaterInteropPlayModeTests
    {
        [UnityTest] public IEnumerator WaterOnly_CapsTangentsSeamsAndFlowResidencyUseNoBankOrCliffObjects()
        {
            var session=new MapEditSession(new MapAuthoringDocument{mapId="water-mesh-interop",bounds=new Vector4(-128,-128,256,256)});
            var catalog=ScriptableObject.CreateInstance<MapRockCatalog>();
            catalog.riverVisualQuality=ScriptableObject.CreateInstance<RiverVisualQualityProfile>();
            var shader=Shader.Find("Little Castle/Rivers/Uber Water Adapted");Assert.That(shader,Is.Not.Null);
            catalog.waterMaterial=new Material(shader);catalog.waterMaterial.EnableKeyword("_ENABLEWAVE");
            catalog.waterMaterial.SetFloat("_1st_Wave_Height",.05f);catalog.waterMaterial.SetFloat("_2nd_Wave_Height",.025f);
            Texture flow=null;
            try
            {
                using(var view=new MapRiverPresentation(session,catalog,true))
                {
                    session.UpsertRiver(new MapRiverRecord{id=888,layoutVersion=2,nodes=new List<MapRiverNode>{new MapRiverNode{id=1,position=new Vector2(-18,-40),leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand},new MapRiverNode{id=2,position=new Vector2(-18,40),leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand}}});
                    for(int i=0;i<30;i++){view.Tick(Vector3.zero,true);yield return null;}
                    Assert.That(view.ResidentTiles,Is.GreaterThan(3));Assert.That(view.ResidentRocks,Is.Zero);Assert.That(session.GetRiver(888).rocks,Is.Empty);
                    var shared=new Dictionary<Vector2,float>();int seams=0;bool capped=false;
                    foreach(var renderer in view.WaterRenderers)
                    {
                        Assert.That(renderer.gameObject.layer,Is.EqualTo(4));Assert.That(renderer.transform.parent.childCount,Is.EqualTo(2),"Water and one shared-mesh foam overlay; no opaque bank or cliff object.");
                        var overlay=renderer.transform.parent.Find("Living river surface");Assert.That(overlay,Is.Not.Null);
                        Assert.That(overlay.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(renderer.GetComponent<MeshFilter>().sharedMesh));
                        Assert.That(renderer.transform.parent.GetComponentsInChildren<Collider>(),Is.Empty);
                        Assert.That(renderer.transform.parent.GetComponentsInChildren<Light>(),Is.Empty);
                        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);flow=block.GetTexture("_LC_RiverFlowMap");Assert.That(flow,Is.Not.Null);
                        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var tangents=mesh.tangents;var triangles=mesh.triangles;
                        Assert.That(tangents.Length,Is.EqualTo(vertices.Length));Assert.That(mesh.bounds.size.y,Is.GreaterThan(.14f));
                        for(int i=0;i<vertices.Length;i++)
                        {
                            Assert.That(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z).magnitude,Is.EqualTo(1).Within(.001));
                            var p=vertices[i];if(p.z< -40||p.z>40)capped=true;
                            var key=new Vector2(p.x,p.z);if(shared.TryGetValue(key,out var h)){Assert.That(p.y,Is.EqualTo(h).Within(.00001));seams++;}else shared.Add(key,p.y);
                        }
                        for(int i=0;i<triangles.Length;i+=3)Assert.That(Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]).y,Is.GreaterThan(0));
                    }
                    Assert.That(capped,Is.True);Assert.That(seams,Is.GreaterThan(30));
                    view.Tick(new Vector3(1000,0,1000),true);Assert.That(view.ResidentTiles,Is.Zero);
                }
                yield return null;Assert.That(!flow,Is.True,"Evicted flow textures must be destroyed.");
            }
            finally{Object.Destroy(catalog.riverVisualQuality);Object.Destroy(catalog.waterMaterial);Object.Destroy(catalog);}
        }
    }
}
