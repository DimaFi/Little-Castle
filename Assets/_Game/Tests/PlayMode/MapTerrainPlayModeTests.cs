using System.Collections;
using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LittleCastle.Tests
{
    public sealed class MapTerrainPlayModeTests
    {
        [UnityTest] public IEnumerator MapTerrain_ChunksRootsTreesCollisionAndCleanupFollowHeightCommands()
        {
            var ground=new GameObject("Isolated editable terrain");var plant=new GameObject("Instanced roots");var tree=new GameObject("Existing tree anchor");var bridge=new GameObject("LOD crossing must not follow terrain");
            var material=new Material(Shader.Find("Unlit/Color"));var mesh=Grid();var data=ScriptableObject.CreateInstance<MeadowGrassData>();
            MapTerrainPresentation view=null;
            try
            {
                ground.AddComponent<MeshFilter>().sharedMesh=mesh;ground.AddComponent<MeshRenderer>().sharedMaterial=material;ground.AddComponent<MeshCollider>().sharedMesh=mesh;
                tree.transform.position=new Vector3(-8,0,-8);tree.AddComponent<LODGroup>();tree.AddComponent<MapTerrainAnchor>();bridge.transform.position=tree.transform.position;bridge.AddComponent<LODGroup>();
                var grass=plant.AddComponent<MeadowGrassRenderer>();grass.enabled=false;
                var originalMatrix=Matrix4x4.TRS(new Vector3(-8,-.025f,-8),Quaternion.identity,Vector3.one);
                data.patches=new[]{new MeadowGrassData.Patch{bounds=new Bounds(new Vector3(-8,0,-8),Vector3.one*8),matrices=new[]{originalMatrix}}};data.authoredClumps=1;grass.data=data;
                var definition=MapHeightMeshImport.Read(mesh,ground.transform,out _,out _);
                var session=new MapEditSession(new MapAuthoringDocument{mapId="terrain-play",bounds=new Vector4(-24,-24,48,48),heightField=definition});
                view=new MapTerrainPresentation(session,ground.GetComponent<MeshFilter>(),grass);
                Assert.That(view.ChunkCount,Is.EqualTo(9));Assert.That(ground.GetComponent<MeshRenderer>().enabled,Is.False);
                session.BeginHeightStroke(3,1);session.AppendHeightPoint(new Vector2(-8,-8));session.CommitStroke();
                Assert.That(view.LastTerrainChunks,Is.EqualTo(4));Assert.That(view.LastGrassPatches,Is.EqualTo(1));
                Assert.That(grass.data.patches[0].matrices[0].m13,Is.EqualTo(.975f).Within(.0001f));Assert.That(tree.transform.position.y,Is.EqualTo(1).Within(.0001f));
                Assert.That(bridge.transform.position.y,Is.Zero,"An unrelated LOD object must keep its own placement contract.");
                Assert.That(view.PendingColliders,Is.EqualTo(4));view.FlushColliderBudget(2);Assert.That(view.PendingColliders,Is.EqualTo(2));view.FlushColliderBudget(2);Physics.SyncTransforms();
                Assert.That(view.Raycast(new Ray(new Vector3(-8,10,-8),Vector3.down),out var hit),Is.True);Assert.That(hit.point.y,Is.EqualTo(1).Within(.001f));
                var shared=new Dictionary<Vector2,Vector3>();int sharedCount=0;
                foreach(var mf in ground.GetComponentsInChildren<MeshFilter>())
                {
                    if(mf.sharedMesh==mesh)continue;var vertices=mf.sharedMesh.vertices;var normals=mf.sharedMesh.normals;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        var p=vertices[i];var key=new Vector2(p.x,p.z);
                        if(shared.TryGetValue(key,out var previous)){Assert.That(p.y,Is.EqualTo(previous.y).Within(.00001f));sharedCount++;}
                        else shared.Add(key,p);
                        Assert.That(normals[i],Is.EqualTo(session.SampleNormal(p.x,p.z)));
                    }
                }
                Assert.That(sharedCount,Is.GreaterThan(0));session.Undo();Assert.That(tree.transform.position.y,Is.Zero);
                Assert.That(grass.data.patches[0].matrices[0],Is.EqualTo(originalMatrix));session.Redo();
                string json=session.ExportJson();session.ClearEdits();session.LoadJson(json);Assert.That(tree.transform.position.y,Is.EqualTo(1));
                view.Dispose();view=null;Assert.That(grass.data,Is.SameAs(data));Assert.That(tree.transform.position.y,Is.Zero);
                Assert.That(ground.GetComponent<MeshRenderer>().enabled,Is.True);Assert.That(ground.GetComponent<MeshCollider>().enabled,Is.True);
                foreach(var p in mesh.vertices)Assert.That(p.y,Is.Zero);yield return null;
            }
            finally{view?.Dispose();Object.Destroy(ground);Object.Destroy(plant);Object.Destroy(tree);Object.Destroy(bridge);Object.Destroy(mesh);Object.Destroy(data);Object.Destroy(material);}
            yield return null;
        }
        static Mesh Grid()
        {
            const int n=48;var vertices=new Vector3[(n+1)*(n+1)];var triangles=new int[n*n*6];
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)vertices[z*(n+1)+x]=new Vector3(x-24,0,z-24);
            int t=0;for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {int i=z*(n+1)+x;triangles[t++]=i;triangles[t++]=i+n+1;triangles[t++]=i+1;triangles[t++]=i+1;triangles[t++]=i+n+1;triangles[t++]=i+n+2;}
            var mesh=new Mesh{vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
