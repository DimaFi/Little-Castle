using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapTerrainTests
    {
        static MapAuthoringDocument Map()=>new MapAuthoringDocument{mapId="terrain",bounds=new Vector4(-64,-64,128,128),heightField=new MapHeightFieldData{minimumHeight=-2,maximumHeight=3}};
        static void Sculpt(MapEditSession s,Vector2 p,float delta=1,float radius=4)
        {s.BeginHeightStroke(radius,delta);s.AppendHeightPoint(p);s.CommitStroke();}
        [Test] public void MapTerrain_UnityAssetMigrationReadsExistingLatticeInsteadOfEmptyDefaultField()
        {
            var data=ScriptableObject.CreateInstance<LittleCastle.Rendering.SurfacePaintData>();data.heightField=new MapHeightFieldData();
            var root=new GameObject("Migration fixture");var mesh=new Mesh{vertices=new[]{new Vector3(-100,1,-85),new Vector3(-99,1,-85),new Vector3(-100,1,-84),new Vector3(-99,1,-84)},triangles=new[]{0,2,1,1,2,3}};
            try
            {
                var filter=root.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
                var imported=LittleCastle.Research.MeadowSurfacePainter.CreateDocument(data,filter);var session=new MapEditSession(imported);
                Assert.That(session.SampleHeight(-99.5f,-84.5f),Is.EqualTo(1));Assert.That(session.HeightSettings.origin,Is.EqualTo(new Vector2(-100,-85)));
                data.hasHeightBase=true;var authored=LittleCastle.Research.MeadowSurfacePainter.CreateDocument(data,filter);
                Assert.That(new MapEditSession(authored).SampleHeight(-99.5f,-84.5f),Is.Zero);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(data);}
        }
        [Test] public void MapTerrain_LimitsFalloffAndOneTransaction()
        {
            var session=new MapEditSession(Map());session.BeginHeightStroke(4,1);
            for(int i=0;i<5;i++)session.AppendHeightPoint(new Vector2(-1,-1));
            session.CommitStroke();Assert.That(session.SampleHeight(-1,-1),Is.EqualTo(3));
            Assert.That(session.SampleHeight(3,-1),Is.Zero);Assert.That(session.SampleHeight(2,-1),Is.GreaterThan(0).And.LessThan(3));
            session.Undo();Assert.That(session.EditedHeightSamples,Is.Zero);session.Redo();Assert.That(session.SampleHeight(-1,-1),Is.EqualTo(3));
            for(int i=0;i<8;i++)Sculpt(session,new Vector2(-1,-1),-1);Assert.That(session.SampleHeight(-1,-1),Is.EqualTo(-2));
        }
        [Test] public void MapTerrain_MixedHistoryCancelClearAndRoundTrip()
        {
            var session=new MapEditSession(Map());Sculpt(session,Vector2.zero);
            session.AddStroke(new LittleCastle.Rendering.SurfacePaintStroke{id="road",radius=2,points=new List<Vector2>{Vector2.zero}});
            session.BeginHeightStroke(4,-1);session.AppendHeightPoint(Vector2.zero);session.CancelStroke();Assert.That(session.SampleHeight(0,0),Is.EqualTo(1));
            var json=session.ExportJson();session.ClearEdits();Assert.That(session.SampleHeight(0,0),Is.Zero);
            session.Undo();Assert.That(session.SampleHeight(0,0),Is.EqualTo(1));Assert.That(session.ExportEdits().strokes.Count,Is.EqualTo(1));
            session.Undo();Assert.That(session.ExportEdits().strokes,Is.Empty);Assert.That(session.SampleHeight(0,0),Is.EqualTo(1));
            session.Undo();Assert.That(session.EditedHeightSamples,Is.Zero);session.LoadJson(json);Assert.That(session.SampleHeight(0,0),Is.EqualTo(1));
            var baked=new MapEditSession(session.ExportMap());Assert.That(baked.SampleHeight(0,0),Is.EqualTo(1));Assert.That(baked.EditedHeightSamples,Is.Zero);
        }
        [Test] public void MapTerrain_AtomicValidationAndRoleRevocation()
        {
            var session=new MapEditSession(Map());Sculpt(session,Vector2.zero);string before=session.ExportJson();var invalid=session.ExportSave();
            invalid.heightEdits.Add(new MapHeightSample(0,0,float.NaN));Assert.Throws<InvalidDataException>(()=>session.LoadSave(invalid));Assert.That(session.ExportJson(),Is.EqualTo(before));
            session.Access=MapEditAccess.AllSurface;Assert.Throws<UnauthorizedAccessException>(()=>session.BeginHeightStroke(4,1));
            Assert.Throws<UnauthorizedAccessException>(()=>session.Undo());Assert.That(session.SampleHeight(0,0),Is.EqualTo(1));
            session.Access=MapEditAccess.All;session.BeginHeightStroke(4,1);session.AppendHeightPoint(Vector2.zero);session.Access=MapEditAccess.None;
            Assert.Throws<UnauthorizedAccessException>(()=>session.CommitStroke());Assert.That(session.SampleHeight(0,0),Is.EqualTo(1));
        }
        [Test] public void MapTerrain_ProtectedAreasAndMapEdge()
        {
            var map=Map();map.heightField.protectedAreas.Add(new Rect(-1,-1,2,2));var session=new MapEditSession(map);
            Sculpt(session,Vector2.zero,1,6);Assert.That(session.SampleHeight(0,0),Is.Zero);Assert.That(session.SampleHeight(4,0),Is.GreaterThan(0));
            Sculpt(session,new Vector2(64,0));Assert.That(session.SampleHeight(65,0),Is.Zero);
            Assert.Throws<InvalidDataException>(()=>session.AppendHeightPoint(new Vector2(65,0)));
        }
        [Test] public void MapTerrain_SharedLatticeNegativeTilesAndTriangleSampling()
        {
            var map=Map();MapHeightField.SetBase(map.heightField,-1,-1,1);MapHeightField.SetBase(map.heightField,0,0,2);
            var session=new MapEditSession(map);Assert.That(session.SampleHeight(-.75f,-.75f),Is.EqualTo(.5f).Within(.0001f));
            Assert.That(session.SampleHeight(-.25f,-.25f),Is.EqualTo(1).Within(.0001f));
            Sculpt(session,new Vector2(0,-1));var restored=new MapEditSession(map);restored.LoadJson(session.ExportJson());
            for(float z=-3;z<=3;z+=.25f)for(float x=-3;x<=3;x+=.25f)
            {Assert.That(restored.SampleHeight(x,z),Is.EqualTo(session.SampleHeight(x,z)));Assert.That(restored.SampleNormal(x,z),Is.EqualTo(session.SampleNormal(x,z)));}
        }
        [Test] public void MapTerrain_V2SurfaceMigrationPreservesNewBaseHeights()
        {
            var map=Map();MapHeightField.SetBase(map.heightField,0,0,1);
            string json=JsonUtility.ToJson(map);int at=json.IndexOf(",\"heightField\":",StringComparison.Ordinal);json=json.Substring(0,at)+"}";
            ulong hash=14695981039346656037UL;foreach(char c in json){hash^=c;hash=unchecked(hash*1099511628211UL);}
            var old=new MapEditSave{version=2,mapId=map.mapId,baseRevision=map.revision,baseFingerprint=hash.ToString("x16")};
            var session=new MapEditSession(map);Sculpt(session,Vector2.zero);session.LoadSave(old);
            Assert.That(session.SampleHeight(0,0),Is.EqualTo(1));Assert.That(session.EditedHeightSamples,Is.Zero);
        }
        [Test] public void MapTerrain_LargeMapEditsRemainSparseAndDoNotInvalidateSurfaceMasks()
        {
            var map=Map();map.bounds=new Vector4(-32000,-32000,64000,64000);var session=new MapEditSession(map);
            using(var masks=new LittleCastle.Rendering.MapSurfaceTileCache(session))
            {
                var tile=masks.Get(new ChunkCoordinate(0,0));Sculpt(session,new Vector2(-16000,-16000));
                Assert.That(session.EditedHeightSamples,Is.LessThan(64));Assert.That(tile.revision,Is.EqualTo(1));Assert.That(masks.ResidentCount,Is.EqualTo(1));
            }
        }
    }
}
