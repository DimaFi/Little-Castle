using System.Collections;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LittleCastle.Tests
{
    public sealed class MapToolPlayModeTests
    {
        [UnityTest] public IEnumerator MapTools_OneBatchedRevisionUpdatesResidentTreesOnBothSidesOfZero()
        {
            var source=GameObject.CreatePrimitive(PrimitiveType.Cube);source.AddComponent<LODGroup>();
            var catalog=ScriptableObject.CreateInstance<MapTreeCatalog>();var definition=new MapTreeDefinition{archetypeId="shared.oak"};
            catalog.entries=new[]{new MapTreeCatalog.Entry{title="Fixture",definition=definition,prefab=source,localBounds=new Bounds(Vector3.zero,Vector3.one)}};
            try
            {
                var session=new MapEditSession(new MapAuthoringDocument{mapId="trees-batch",bounds=new Vector4(-64,-64,128,128),heightField=new MapHeightFieldData()});
                session.RegisterTreeCatalog(catalog.Definitions,new MapTreePack[0]);
                long left=session.PlaceTree(definition.archetypeId,new Vector2(-6,-6)),right=session.PlaceTree(definition.archetypeId,new Vector2(6,6));
                using(var view=new MapTreePresentation(session,catalog,false))
                {
                    view.Tick(Vector3.zero,true);var c=new MapToolController(session,MapToolRegistry.ExistingBrushes());c.Select("terrain.raise");c.context.settings.radius=16;
                    c.Begin(new MapToolPointer(default,true,Vector2.zero),0);c.Release();
                    Assert.That(view.Position(left).y,Is.EqualTo(session.SampleHeight(-6,-6)).Within(.0001f));
                    Assert.That(view.Position(right).y,Is.EqualTo(session.SampleHeight(6,6)).Within(.0001f));
                    Assert.That(view.Position(right).y,Is.GreaterThan(0));
                }
            }
            finally{Object.DestroyImmediate(source);Object.DestroyImmediate(catalog);}
            yield return null;
        }
        [UnityTest] public IEnumerator MapTools_RealMeadowAdapterUsesRegistryForSurfaceHeightAndSaveReplay()
        {
            var root=new GameObject("Shared tool adapter fixture");var terrain=new GameObject("Terrain");var cameraRoot=new GameObject("Editor camera");
            var mesh=Grid();var data=ScriptableObject.CreateInstance<SurfacePaintData>();
            var material=new Material(Shader.Find("Little Castle/Terrain/LC Terrain"));data.bounds=new Vector4(-24,-24,48,48);
            try
            {
                terrain.AddComponent<MeshFilter>().sharedMesh=mesh;terrain.AddComponent<MeshRenderer>().sharedMaterial=material;
                terrain.AddComponent<MeshCollider>().sharedMesh=mesh;
                var painter=root.AddComponent<MeadowSurfacePainter>();painter.source=data;painter.terrainMaterial=material;painter.brushMaterial=material;
                painter.studyCamera=cameraRoot.AddComponent<Camera>();yield return null;
                var tools=painter.Tools;Assert.That(tools,Is.Not.Null);tools.Select("terrain.raise");tools.context.settings.radius=6;
                var p=new MapToolPointer(new Ray(new Vector3(0,1000,0),Vector3.down),true,Vector2.zero);
                tools.Begin(p,0);tools.Tick(1);tools.Release();Assert.That(painter.Session.SampleHeight(0,0),Is.GreaterThan(0));
                int checkedVertices=0;
                foreach(var filter in terrain.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=mesh)
                    foreach(var vertex in filter.sharedMesh.vertices)if(Mathf.Abs(vertex.x)<=2&&Mathf.Abs(vertex.z)<=2)
                    {Assert.That(vertex.y,Is.EqualTo(painter.Session.SampleHeight(vertex.x,vertex.z)).Within(.0001f),"All regions of the batched revision must update");checkedVertices++;}
                Assert.That(checkedVertices,Is.GreaterThan(4));
                Assert.That(painter.TerrainView.PendingColliders,Is.GreaterThan(0));
                foreach(float altitude in new[]{5,500,50000})
                {
                    Assert.That(painter.RaycastGround(new Ray(new Vector3(0,altitude,0),Vector3.down),out var hit),Is.True);
                    Assert.That(hit.point.y,Is.EqualTo(painter.Session.SampleHeight(0,0)).Within(.01f));
                }
                tools.Select("surface.road");tools.context.settings.radius=7;tools.context.settings.strength=.7f;
                tools.Begin(p,2);tools.Move(new MapToolPointer(p.ray,true,new Vector2(3,3)));tools.Tick(3);tools.Release();
                Assert.That(painter.lastUpdatedPixels,Is.GreaterThan(0).And.LessThan(MeadowSurfacePainter.Resolution*MeadowSurfacePainter.Resolution));
                ulong digest=painter.MaskDigest();string saved=painter.ExportJson();painter.Undo();Assert.That(painter.MaskDigest(),Is.Not.EqualTo(digest));
                painter.Redo();Assert.That(painter.MaskDigest(),Is.EqualTo(digest));painter.ResetEdits();painter.LoadJson(saved);
                Assert.That(painter.MaskDigest(),Is.EqualTo(digest));yield return null;
            }
            finally
            {
                Object.DestroyImmediate(root);Object.DestroyImmediate(terrain);Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(mesh);Object.DestroyImmediate(material);Object.DestroyImmediate(data);
            }
        }
        static Mesh Grid()
        {
            const int n=49;var vertices=new Vector3[n*n];var triangles=new int[(n-1)*(n-1)*6];
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)vertices[z*n+x]=new Vector3(x-24,0,z-24);
            int t=0;for(int z=0;z<n-1;z++)for(int x=0;x<n-1;x++){int i=z*n+x;triangles[t++]=i;triangles[t++]=i+n;triangles[t++]=i+1;triangles[t++]=i+1;triangles[t++]=i+n;triangles[t++]=i+n+1;}
            var mesh=new Mesh{vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
