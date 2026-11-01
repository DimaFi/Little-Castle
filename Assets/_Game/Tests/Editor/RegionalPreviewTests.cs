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
        public void SessionSwapKeepsResidentMeshesAndRestoresOnlyChangedTile()
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
                view.RebindSession(detached,area);
                Assert.That(view.LastTerrainChunks,Is.EqualTo(1));
                for(int i=0;i<children.Length;i++)Assert.That(children[i].sharedMesh,Is.SameAs(resident[i]));
                Assert.That(resident[0].vertices.Any(v=>v.y>.9f),Is.True);
                for(int i=1;i<resident.Length;i++)Assert.That(resident[i].vertices,Is.EqualTo(before[i]));
                view.RebindSession(live,area);
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
