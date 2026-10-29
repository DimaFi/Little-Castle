using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class RiverSurfaceEffectsTests
    {
        [TestCase(1)] [TestCase(2)]
        public void OverlaySharesWaterMeshAndTracksEditUndoAndQuality(int version)
        {
            var session=new MapEditSession(new MapAuthoringDocument{mapId="effects-test",bounds=new Vector4(-128,-128,256,256),heightField=new MapHeightFieldData()});
            var shader=Shader.Find("Little Castle/Rivers/Surface Effects");Assert.That(shader,Is.Not.Null);
            var material=new Material(shader);var catalog=ScriptableObject.CreateInstance<MapRockCatalog>();catalog.waterMaterial=material;catalog.bankMaterial=material;
            var profile=ScriptableObject.CreateInstance<RiverVisualQualityProfile>();catalog.riverVisualQuality=profile;
            var river=new MapRiverRecord{id=10,layoutVersion=version,nodes=new List<MapRiverNode>{new MapRiverNode{id=1,position=new Vector2(0,-12)},new MapRiverNode{id=2,position=new Vector2(0,12)}}};
            var view=new MapRiverPresentation(session,catalog,true,catalog){creationBudget=100};
            Material ownedEffectMaterial=null;
            try
            {
                session.UpsertRiver(river);view.Tick(Vector3.zero,true);
                Mesh water=null;int count=0;
                foreach(var renderer in view.WaterRenderers)
                {
                    var root=renderer.transform.parent;var effect=root.Find("Living river surface");Assert.That(effect,Is.Not.Null);
                    water=renderer.GetComponent<MeshFilter>().sharedMesh;ownedEffectMaterial=effect.GetComponent<MeshRenderer>().sharedMaterial;Assert.That(effect.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(water));
                    Assert.That(effect.GetComponentsInChildren<Collider>(),Is.Empty);count++;
                }
                Assert.That(count,Is.GreaterThan(0));
                profile.quality=RiverEffectsQuality.Off;view.Tick(Vector3.zero);
                foreach(var renderer in view.WaterRenderers)Assert.That(renderer.transform.parent.Find("Living river surface"),Is.Null);
                Assert.That(water==null,Is.True);
                profile.quality=RiverEffectsQuality.High;profile.maximumTrianglesPerTile=0;view.Tick(Vector3.zero);
                foreach(var renderer in view.WaterRenderers)Assert.That(renderer.transform.parent.Find("Living river surface"),Is.Null);
                profile.maximumTrianglesPerTile=8192;view.Tick(Vector3.zero);session.Undo();view.Tick(Vector3.zero);
                Assert.That(view.ResidentTiles,Is.Zero);session.Redo();view.Tick(Vector3.zero);Assert.That(view.ResidentTiles,Is.GreaterThan(0));
            }
            finally {view.Dispose();Assert.That(ownedEffectMaterial==null,Is.True);Object.DestroyImmediate(profile);Object.DestroyImmediate(catalog);Object.DestroyImmediate(material);}
        }
        [Test] public void NearbyRocksAreBoundedAndOffDoesNotAttach()
        {
            var session=new MapEditSession(new MapAuthoringDocument{mapId="effects-rocks",bounds=new Vector4(-128,-128,256,256),heightField=new MapHeightFieldData()});
            var path=new MapRiverPath(new MapRiverRecord{id=10,nodes=new List<MapRiverNode>{new MapRiverNode{id=1,position=new Vector2(-8,-12)},new MapRiverNode{id=2,position=new Vector2(8,12)}}});
            var profile=ScriptableObject.CreateInstance<RiverVisualQualityProfile>();var catalog=ScriptableObject.CreateInstance<MapRockCatalog>();
            var prefab=new GameObject("effect rock prefab");catalog.entries=new[]{new MapRockCatalog.Entry{prefab=prefab,definition=new MapRockDefinition{id="rock"},bounds=new Bounds(Vector3.zero,Vector3.one*2)}};
            var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.forward,Vector3.right};mesh.triangles=new[]{0,1,2};
            var root=new GameObject("effect test");var effects=new RiverSurfaceEffects(session,catalog,profile);
            try
            {
                var rocks=new List<MapRockInstance>();for(int i=0;i<8;i++)rocks.Add(new MapRockInstance{id=i+1,archetypeId="rock",manual=true,position=new Vector2(0,i-4)});
                var index=new RiverSurfaceEffects.RockIndex();foreach(var rock in rocks)index.Add(rock);
                var bounds=new Rect(-10,-15,20,30);Assert.That(effects.Attach(root,mesh,path,bounds,index.Query(bounds),null,null,false),Is.True);
                var renderer=root.transform.Find("Living river surface").GetComponent<MeshRenderer>();var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);Assert.That(block.GetFloat("_ObstacleCount"),Is.EqualTo(4));Assert.That(root.GetComponentsInChildren<Collider>(),Is.Empty);
                var positions=block.GetVectorArray("_Obstacles");var flows=block.GetVectorArray("_ObstacleFlows");
                Assert.That(positions.Length,Is.EqualTo(8));Assert.That(flows.Length,Is.EqualTo(8));
                for(int i=0;i<4;i++)
                {
                    Assert.That(path.Nearest(new Vector2(positions[i].x,positions[i].y),out var frame,out _),Is.True);
                    Assert.That(flows[i].x,Is.EqualTo(frame.tangent.x).Within(.0001f));
                    Assert.That(flows[i].y,Is.EqualTo(frame.tangent.y).Within(.0001f));
                    Assert.That(flows[i].z,Is.EqualTo(frame.speed).Within(.0001f));
                }
                profile.quality=RiverEffectsQuality.Off;Assert.That(effects.Attach(root,mesh,path,bounds,index.Query(bounds),null,null,false),Is.False);
            }
            finally {effects.Dispose();Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);Object.DestroyImmediate(prefab);Object.DestroyImmediate(catalog);Object.DestroyImmediate(profile);}
        }
        [Test] public void QualityCapsObstacleCount()
        {
            var p=ScriptableObject.CreateInstance<RiverVisualQualityProfile>();
            try {p.maximumObstaclesPerTile=20;p.quality=RiverEffectsQuality.Off;Assert.That(p.ObstacleLimit,Is.Zero);p.quality=RiverEffectsQuality.Low;Assert.That(p.ObstacleLimit,Is.EqualTo(2));p.quality=RiverEffectsQuality.Medium;Assert.That(p.ObstacleLimit,Is.EqualTo(4));p.quality=RiverEffectsQuality.High;Assert.That(p.ObstacleLimit,Is.EqualTo(8));}
            finally {Object.DestroyImmediate(p);}
        }
    }
}
