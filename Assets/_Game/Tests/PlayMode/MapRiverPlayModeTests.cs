using System.Collections;
using System.Collections.Generic;
using LittleCastle.World;
using LittleCastle.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace LittleCastle.Tests
{
    public sealed class MapRiverPlayModeTests
    {
        [UnityTest] public IEnumerator River_RibbonsHaveUpwardTrianglesSharedBordersAndBoundedViews()
        {
            var catalog=MapRockCatalog.Default;Assert.That(catalog,Is.Not.Null,"Prepare the actual cliff catalog before GPU tests.");
            var session=new MapEditSession(new MapAuthoringDocument{mapId="river-play",bounds=new Vector4(-128,-128,256,256)});
            using(var view=new MapRiverPresentation(session,catalog))
            {
                var r=new MapRiverRecord{id=88,nodes=new List<MapRiverNode>{new MapRiverNode{id=1,position=new Vector2(-5,-35)},new MapRiverNode{id=2,position=new Vector2(3,0)},new MapRiverNode{id=3,position=new Vector2(-5,35),width=12}}};session.UpsertRiver(r);
                view.creationBudget=2;view.Tick(Vector3.zero,true);Assert.That(view.ResidentTiles+view.ResidentRocks,Is.LessThanOrEqualTo(2));
                for(int frame=0;frame<40;frame++){view.Tick(Vector3.zero,true);yield return null;}
                Assert.That(view.ResidentRocks,Is.GreaterThan(10));Assert.That(view.ResidentTiles,Is.GreaterThan(3));
                var shared=new Dictionary<Vector2,float>();int sharedCount=0,waterMeshes=0;
                foreach(var filter in Resources.FindObjectsOfTypeAll<MeshFilter>())
                {
                    if(!filter.sharedMesh||filter.sharedMesh.name!="Flow strip")continue;waterMeshes++;var mesh=filter.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
                    for(int i=0;i<triangles.Length;i+=3){var n=Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]);Assert.That(n.y,Is.GreaterThan(0),"Folded water strip.");}
                    for(int i=0;i<vertices.Length;i++){var p=vertices[i];var key=new Vector2(p.x,p.z);if(shared.TryGetValue(key,out var h)){Assert.That(p.y,Is.EqualTo(h).Within(.00001));sharedCount++;}else shared.Add(key,p.y);}
                }
                Assert.That(waterMeshes,Is.GreaterThan(2));Assert.That(sharedCount,Is.GreaterThan(20));view.Tick(new Vector3(1000,0,1000),true);Assert.That(view.ResidentTiles,Is.Zero);Assert.That(view.ResidentRocks,Is.Zero);
            }
            yield return null;
        }
    }
}
