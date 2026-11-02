using System;
using System.Linq;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class RegionalPreviewTests
    {
        [Test]
        public void UnloadedSourceDiscardsPendingPresentationWithoutChangingSession()
        {
            var root=new GameObject("Unloaded terrain preview test");var mesh=new Mesh
            {vertices=new[]{Vector3.zero,Vector3.right,Vector3.forward,Vector3.right+Vector3.forward},triangles=new[]{0,2,1,1,2,3}};
            MapTerrainPresentation view=null;
            try
            {
                var filter=root.AddComponent<MeshFilter>();filter.sharedMesh=mesh;root.AddComponent<MeshRenderer>();
                var session=new MapEditSession(new MapAuthoringDocument{mapId="unloaded",bounds=new Vector4(-4,-4,8,8)});
                view=new MapTerrainPresentation(session,filter,projectSceneTrees:false){BudgetedUpdates=true};
                session.BeginHeightStroke(2,1,1);session.AppendHeightPoint(Vector2.zero);session.CommitStroke();
                Assert.That(view.HasPendingPreview,Is.True);string before=session.ExportJson();
                Object.DestroyImmediate(root);
                Assert.That(view.IsValid,Is.False);
                Assert.That(view.FlushPreviewBudget(),Is.Zero);Assert.That(view.HasPendingPreview,Is.False);
                Assert.That(view.FlushColliderBudget(),Is.Zero);
                Assert.Throws<InvalidOperationException>(()=>view.RebindSession(session,new Rect(-1,-1,2,2)));
                Assert.DoesNotThrow(()=>view.Dispose());Assert.That(session.ExportJson(),Is.EqualTo(before));
                Assert.DoesNotThrow(()=>view.Dispose());
            }
            finally{view?.Dispose();if(root)Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
        }
        [TestCase(false)] [TestCase(true)]
        public void SessionSwapKeepsResidentMeshesAndRestoresOnlyChangedTile(bool budgeted)
        {
            var root=new GameObject("Regional terrain test");var mesh=new Mesh();
            var filter=root.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            var renderer=root.AddComponent<MeshRenderer>();
            var vertices=new Vector3[65*65];var indices=new int[64*64*6];
            for(int z=0;z<65;z++)for(int x=0;x<65;x++)vertices[z*65+x]=new Vector3(x,0,z);
            int t=0;for(int z=0;z<64;z++)for(int x=0;x<64;x++)
            {int i=z*65+x;indices[t++]=i;indices[t++]=i+65;indices[t++]=i+1;indices[t++]=i+1;indices[t++]=i+65;indices[t++]=i+66;}
            mesh.vertices=vertices;mesh.triangles=indices;
            var map=new MapAuthoringDocument{mapId="region",bounds=new Vector4(0,0,64,64),heightField=new MapHeightFieldData{minimumHeight=-10,maximumHeight=24}};
            var live=new MapEditSession(map);var detached=new MapEditSession(map);
            detached.BeginHeightStroke(3,1,1);detached.AppendHeightPoint(new Vector2(8,8));detached.CommitStroke();
            MapTerrainPresentation view=null;
            try
            {
                view=new MapTerrainPresentation(live,filter);
                var children=root.GetComponentsInChildren<MeshFilter>().Where(f=>f!=filter).ToArray();
                Assert.That(children.Length,Is.EqualTo(16));
                var resident=children.Select(f=>f.sharedMesh).ToArray();
                var before=resident.Select(m=>m.vertices).ToArray();
                var area=new Rect(3,3,10,10);
                view.BudgetedUpdates=budgeted;
                view.RebindSession(detached,area);
                if(budgeted)
                {
                    Assert.That(view.HasPendingPreview,Is.True);
                    Assert.That(view.FlushPreviewBudget(3,double.PositiveInfinity),Is.EqualTo(3));
                    for(int i=0;i<resident.Length;i++)Assert.That(resident[i].vertices,Is.EqualTo(before[i]),"Incomplete tile must not be published.");
                    view.FlushColliderBudget(100);
                    Assert.That(root.GetComponentsInChildren<MeshCollider>().All(c=>c.sharedMesh.vertices.All(v=>v.y==0)),Is.True,"Collider reads complete published geometry.");
                    detached.BeginHeightStroke(3,1,1);detached.AppendHeightPoint(new Vector2(8,8));detached.CommitStroke();
                    int updates=0;
                    while(view.HasPendingPreview&&updates++<1000)Assert.That(view.FlushPreviewBudget(7,double.PositiveInfinity),Is.LessThanOrEqualTo(7));
                    Assert.That(view.HasPendingPreview,Is.False,"Coalesced preview must eventually complete.");
                    foreach(var m in resident)foreach(var v in m.vertices)Assert.That(v.y,Is.EqualTo(detached.SampleHeight(v.x,v.z)),"Latest revision wins over unfinished work.");
                }
                Assert.That(view.LastTerrainChunks,Is.EqualTo(1));
                for(int i=0;i<children.Length;i++)Assert.That(children[i].sharedMesh,Is.SameAs(resident[i]));
                Assert.That(resident[0].vertices.Any(v=>v.y>.9f),Is.True);
                for(int i=1;i<resident.Length;i++)Assert.That(resident[i].vertices,Is.EqualTo(before[i]));
                if(budgeted)
                {
                    detached.BeginHeightStroke(3,.25f,1);detached.AppendHeightPoint(new Vector2(8,8));detached.CommitStroke();
                    view.FlushPreviewBudget(3,double.PositiveInfinity);
                    view.BudgetedUpdates=false;
                }
                view.RebindSession(live,area);
                Assert.That(view.HasPendingPreview,Is.False,"Sync rollback drains pending detached work against the live session.");
                for(int i=0;i<resident.Length;i++)Assert.That(resident[i].vertices,Is.EqualTo(before[i]));
                detached.BeginHeightStroke(3,1,1);detached.AppendHeightPoint(new Vector2(8,8));detached.CommitStroke();
                Assert.That(resident[0].vertices,Is.EqualTo(before[0]),"Detached session no longer owns this presentation");
                Assert.That(filter.sharedMesh,Is.SameAs(mesh));
                view.Dispose();view=null;Assert.That(renderer.enabled,Is.True);
            }
            finally{view?.Dispose();Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
        }
    }
}
