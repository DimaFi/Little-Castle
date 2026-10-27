using System.Collections;
using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace LittleCastle.Tests
{
    public sealed class MapEditPlayModeTests
    {
        [UnityTest] public IEnumerator MapEdit_RealPainterScatterAndGpuReplayAgree()
        {
            var root=new GameObject("Isolated map brush verification");
            var data=ScriptableObject.CreateInstance<SurfacePaintData>();data.bounds=new Vector4(-64,-64,128,128);
            var library=ScriptableObject.CreateInstance<MeadowScatterLibrary>();
            var material=new Material(Shader.Find("Unlit/Color")){enableInstancing=true};
            var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},triangles=new[]{0,1,2}};mesh.RecalculateBounds();
            library.prototypes=new MeadowScatterLibrary.Prototype[3];
            for(int i=0;i<3;i++)library.prototypes[i]=new MeadowScatterLibrary.Prototype{stableId="test-"+i,
                category=(MeadowScatterCategory)i,lodMeshes=new[]{mesh,mesh,mesh,mesh},materials=new[]{material},
                localBounds=mesh.bounds,scaleRange=Vector2.one,weight=1};
            try
            {
                var painter=root.AddComponent<MeadowSurfacePainter>();painter.source=data;painter.brushMaterial=material;
                var renderer=root.AddComponent<MeadowScatterRenderer>();renderer.library=library;renderer.heightSampler=(x,z)=>0;renderer.painter=painter;
                painter.scatterRenderer=renderer;yield return null;
                painter.AddStroke(Stroke("far-flowers",new Vector2(-45,-45),false));
                painter.AddStroke(Stroke("local-flowers",new Vector2(12,12),false));
                int count=renderer.authoredInstances;Assert.That(count,Is.GreaterThan(0));
                var farKey=new ChunkCoordinate(-2,-2);ulong far=renderer.RegionDigest(farKey);
                ulong maskBefore=painter.MaskDigest(),placementsBefore=renderer.placementDigest;
                painter.AddStroke(Stroke("local-road",new Vector2(12,12),true));
                Assert.That(renderer.lastRebuiltRegions,Is.EqualTo(1));Assert.That(renderer.RegionDigest(farKey),Is.EqualTo(far));
                Assert.That(painter.lastUpdatedPixels,Is.LessThan(MeadowSurfacePainter.Resolution*MeadowSurfacePainter.Resolution/16));
                Assert.That(painter.SampleRoad01(12,12),Is.GreaterThan(.95f));Assert.That(painter.SampleGrass01(12,12),Is.LessThan(.05f));
                ulong roadDigest=painter.MaskDigest();painter.Undo();Assert.That(painter.MaskDigest(),Is.EqualTo(maskBefore));
                Assert.That(renderer.placementDigest,Is.EqualTo(placementsBefore));painter.Redo();Assert.That(painter.MaskDigest(),Is.EqualTo(roadDigest));
                ulong local=renderer.placementDigest;renderer.Rebuild(painter);Assert.That(renderer.placementDigest,Is.EqualTo(local),"Incremental scatter equals a full rebuild.");
                string json=painter.ExportJson();painter.ResetEdits();painter.LoadJson(json);Assert.That(painter.MaskDigest(),Is.EqualTo(roadDigest));
                Assert.That(renderer.placementDigest,Is.EqualTo(local));
                using(var tiledGpu=new MapSurfaceGpuCache(painter.Session,64))
                {
                    var near=tiledGpu.Get(new ChunkCoordinate(0,0));var farTile=tiledGpu.Get(farKey);int farRevision=farTile.revision;
                    painter.AddStroke(Stroke("gpu-tile-local",new Vector2(12,12),false));
                    Assert.That(near.revision,Is.EqualTo(2));Assert.That(farTile.revision,Is.EqualTo(farRevision));
                    Assert.That(tiledGpu.ResidentCount,Is.EqualTo(2));Assert.That(near.texture.width,Is.EqualTo(66));
                    painter.Undo();tiledGpu.Release(new ChunkCoordinate(0,0));Assert.That(tiledGpu.ResidentCount,Is.EqualTo(1));
                }
                painter.BeginStroke(false,3);painter.Session.AppendPoint(new Vector2(5,5));painter.Session.AppendPoint(new Vector2(7,7));painter.EndStroke();
                painter.Undo();Assert.That(painter.MaskDigest(),Is.EqualTo(roadDigest),"A multi-point live stroke undoes once.");
                if(SystemInfo.supportsAsyncGPUReadback)
                {
                    var request=AsyncGPUReadback.Request(painter.mask,0);while(!request.done)yield return null;
                    Assert.That(request.hasError,Is.False);var bytes=request.GetData<byte>();ulong gpu=14695981039346656037UL;
                    for(int i=0;i<bytes.Length;i++){gpu^=bytes[i];gpu=unchecked(gpu*1099511628211UL);}
                    Assert.That(gpu,Is.EqualTo(painter.MaskDigest()),"Regional GPU upload equals authoritative CPU raster.");
                }
                else Assert.Ignore("CPU/scatter checks passed; GPU readback unavailable on this device.");
            }
            finally{Object.Destroy(root);Object.Destroy(data);Object.Destroy(library);Object.Destroy(material);Object.Destroy(mesh);}
            yield return null;
        }
        static SurfacePaintStroke Stroke(string id,Vector2 point,bool road)=>new SurfacePaintStroke
        {id=id,road=road,scatter=!road,scatterProfile=MeadowScatterProfile.Flowers,radius=7,points=new List<Vector2>{point}};
    }
}
