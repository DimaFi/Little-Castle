using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class RiverShorePresentationTests
    {
        [Test] public void DerivedShoreChannel_UpdatesWithRiverUndoWithoutChangingAuthoredMesh()
        {
            var root=new GameObject("Isolated river shore terrain");var mesh=new Mesh();
            var vertices=new Vector3[17*17];var triangles=new int[16*16*6];int t=0;
            for(int z=0;z<17;z++)for(int x=0;x<17;x++)vertices[z*17+x]=new Vector3(-16+x*2,0,-16+z*2);
            for(int z=0;z<16;z++)for(int x=0;x<16;x++)
            {int i=z*17+x;triangles[t++]=i;triangles[t++]=i+17;triangles[t++]=i+1;triangles[t++]=i+1;triangles[t++]=i+17;triangles[t++]=i+18;}
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();
            var filter=root.AddComponent<MeshFilter>();filter.sharedMesh=mesh;root.AddComponent<MeshRenderer>();
            var map=new MapAuthoringDocument{mapId="shore-presentation",bounds=new Vector4(-16,-16,32,32),
                heightField=MapHeightMeshImport.Read(mesh,root.transform,out _,out _)};
            var session=new MapEditSession(map);
            try
            {
                using(var view=new MapTerrainPresentation(session,filter,projectSceneTrees:false))
                {
                    float MaximumSand()
                    {
                        float maximum=0;foreach(var child in root.GetComponentsInChildren<MeshFilter>())
                        {if(child==filter)continue;foreach(var uv in child.sharedMesh.uv3)maximum=Mathf.Max(maximum,uv.x);}
                        return maximum;
                    }
                    Assert.That(MaximumSand(),Is.Zero);
                    session.UpsertRiver(new MapRiverRecord{id=81,layoutVersion=2,nodes=new List<MapRiverNode>{
                        new MapRiverNode{id=1,position=new Vector2(0,-6),width=4,leftTransition=3,rightTransition=3,leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand},
                        new MapRiverNode{id=2,position=new Vector2(0,6),width=4,leftTransition=3,rightTransition=3,leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand}}});
                    Assert.That(MaximumSand(),Is.EqualTo(1));session.Undo();Assert.That(MaximumSand(),Is.Zero);
                    session.Redo();Assert.That(MaximumSand(),Is.EqualTo(1));
                    Assert.That(mesh.uv3,Is.Empty);foreach(var vertex in mesh.vertices)Assert.That(vertex.y,Is.Zero);
                }
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
        }
    }
}
