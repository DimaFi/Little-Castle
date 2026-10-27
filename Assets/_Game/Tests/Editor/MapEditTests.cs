using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapEditTests
    {
        static MapAuthoringDocument Map()=>new MapAuthoringDocument{mapId="test-map",bounds=new Vector4(-128,-128,256,256)};
        static SurfacePaintStroke Stroke(string id,float x=8,float z=8,bool road=true,float radius=2)=>new SurfacePaintStroke
        {id=id,road=road,scatter=!road,radius=radius,points=new List<Vector2>{new Vector2(x,z)}};
        static byte[] Raster(MapEditSession session,int size=256)
        {
            var b=session.Bounds;var data=new byte[size*size*2];
            SurfacePaintRasterizer.Rebuild(data,b,size,size,session.QuerySurface(new Rect(b.x,b.y,b.z,b.w)),new RectInt(0,0,size,size));return data;
        }
        [Test] public void MapEdit_StrokeIsOneTransactionAndCopiesInputs()
        {
            var source=Map();var session=new MapEditSession(source);source.mapId="mutated";
            session.BeginStroke(new SurfacePaintStroke{radius=2});session.AppendPoint(new Vector2(-3,-2));session.AppendPoint(new Vector2(1,2));
            Assert.That(session.ExportEdits().strokes,Is.Empty,"Live previews are not saved.");
            Assert.That(session.QuerySurface(new Rect(-8,-8,16,16)).Count,Is.EqualTo(1));
            session.CommitStroke();var painted=Raster(session);session.Undo();Assert.That(session.ExportEdits().strokes,Is.Empty);
            session.Redo();CollectionAssert.AreEqual(painted,Raster(session));Assert.That(session.MapId,Is.EqualTo("test-map"));
            var exported=session.ExportEdits();exported.strokes[0].points.Clear();Assert.That(session.ExportEdits().strokes[0].points.Count,Is.EqualTo(2));
        }
        [Test] public void MapEdit_UndoRedoBranchAndClearAreTransactions()
        {
            var session=new MapEditSession(Map());session.AddStroke(Stroke("road"));session.AddStroke(Stroke("grass",8,8,false));
            var before=Raster(session);session.ClearEdits();session.Undo();CollectionAssert.AreEqual(before,Raster(session));session.Redo();
            Assert.That(session.ExportEdits().strokes,Is.Empty);session.Undo();session.Undo();session.AddStroke(Stroke("branch",-8,-8));
            Assert.That(session.CanRedo,Is.False);session.Redo();Assert.That(session.ExportEdits().strokes.Count,Is.EqualTo(2));
        }
        [Test] public void MapEdit_RoundTripWorldSaveAndLegacyPreserveIdsAndMask()
        {
            var session=new MapEditSession(Map());session.AddStroke(Stroke("stable-road",-12,-10));session.AddStroke(Stroke("stable-flower",14,16,false));
            var expected=Raster(session);var world=new WorldSaveState();session.StoreIn(world);
            var restored=JsonUtility.FromJson<WorldSaveState>(JsonUtility.ToJson(world));var fresh=new MapEditSession(Map());fresh.LoadSave(restored.mapEdits);
            CollectionAssert.AreEqual(expected,Raster(fresh));Assert.That(fresh.ExportEdits().strokes[1].id,Is.EqualTo("stable-flower"));
            var legacy="{\"version\":1,\"strokes\":[{\"id\":\"legacy\",\"road\":false,\"scatter\":true,\"radius\":2,\"points\":[{\"x\":-8,\"y\":1}]}]}";
            fresh.LoadJson(legacy);Assert.That(fresh.ExportEdits().strokes[0].scatterProfile,Is.EqualTo(MeadowScatterProfile.Mixed));
            Assert.That(fresh.ExportEdits().strokes[0].strength,Is.EqualTo(1));Assert.That(fresh.ExportEdits().strokes[0].density,Is.EqualTo(1));
            Assert.That(fresh.CanUndo,Is.False);fresh.LoadJson(session.ExportJson());CollectionAssert.AreEqual(expected,Raster(fresh));
        }
        [Test] public void MapEdit_InvalidLoadIsAtomicAndCannotSwitchMapOrBase()
        {
            var session=new MapEditSession(Map());session.AddStroke(Stroke("keep"));string before=session.ExportJson();
            var invalid=session.ExportSave();invalid.mapId="other";Assert.Throws<InvalidDataException>(()=>session.LoadSave(invalid));
            invalid=session.ExportSave();invalid.baseFingerprint="wrong";Assert.Throws<InvalidDataException>(()=>session.LoadSave(invalid));
            invalid=session.ExportSave();invalid.surface.strokes.Add(Stroke("keep"));Assert.Throws<InvalidDataException>(()=>session.LoadSave(invalid));
            invalid=session.ExportSave();invalid.surface.strokes[0].points[0]=new Vector2(float.NaN,0);Assert.Throws<InvalidDataException>(()=>session.LoadSave(invalid));
            Assert.That(session.ExportJson(),Is.EqualTo(before));Assert.That(session.CanUndo,Is.True);
        }
        [Test] public void MapEdit_AccessAndBoundsAreCheckedBeforeMutation()
        {
            var session=new MapEditSession(Map(),MapEditAccess.Meadow);
            Assert.Throws<UnauthorizedAccessException>(()=>session.AddStroke(Stroke("forbidden")));
            Assert.Throws<InvalidDataException>(()=>session.AddStroke(Stroke("outside",200,0,false)));
            session.BeginStroke(new SurfacePaintStroke{road=false,radius=2});session.AppendPoint(Vector2.zero);session.Access=MapEditAccess.None;
            Assert.Throws<UnauthorizedAccessException>(()=>session.CommitStroke());Assert.That(session.ExportEdits().strokes,Is.Empty);
        }
        [Test] public void MapEdit_HistoryRechecksAccessAfterRoleChange()
        {
            var session=new MapEditSession(Map());session.AddStroke(Stroke("road"));session.Access=MapEditAccess.None;
            Assert.Throws<UnauthorizedAccessException>(()=>session.Undo());Assert.That(session.CanUndo,Is.True);
            session.Access=MapEditAccess.AllSurface;session.Undo();session.Access=MapEditAccess.None;
            Assert.Throws<UnauthorizedAccessException>(()=>session.Redo());Assert.That(session.CanRedo,Is.True);
        }
        [Test] public void MapEdit_TileCacheUpdatesTouchedResidentsOnlyAndHandlesNegativeBorders()
        {
            var session=new MapEditSession(Map());using(var cache=new MapSurfaceTileCache(session,64))
            {
                var left=cache.Get(new ChunkCoordinate(-1,-1));var right=cache.Get(new ChunkCoordinate(0,-1));var far=cache.Get(new ChunkCoordinate(3,3));
                int farRevision=far.revision;session.AddStroke(Stroke("border",0,-8));
                Assert.That(cache.LastUpdatedTiles,Is.EqualTo(2));Assert.That(left.revision,Is.EqualTo(2));Assert.That(right.revision,Is.EqualTo(2));
                Assert.That(far.revision,Is.EqualTo(farRevision));Assert.That(cache.ResidentCount,Is.EqualTo(3));
                // Adjacent gutters share the same world sample centers as their neighbour's edge texels.
                for(int y=0;y<left.size;y++)for(int channel=0;channel<2;channel++)
                {
                    Assert.That(left.pixels[(y*left.size+left.size-2)*2+channel],Is.EqualTo(right.pixels[(y*right.size)*2+channel]));
                    Assert.That(left.pixels[(y*left.size+left.size-1)*2+channel],Is.EqualTo(right.pixels[(y*right.size+1)*2+channel]));
                }
                var saved=(byte[])left.pixels.Clone();session.Undo();session.Redo();CollectionAssert.AreEqual(saved,left.pixels);
                cache.Release(new ChunkCoordinate(-1,-1));CollectionAssert.AreEqual(saved,cache.Get(new ChunkCoordinate(-1,-1)).pixels);
            }
        }
        [Test] public void MapEdit_LargeMapNeverMaterializesUntouchedTiles()
        {
            var map=Map();map.bounds=new Vector4(-32768,-32768,65536,65536);var session=new MapEditSession(map);
            using(var cache=new MapSurfaceTileCache(session))
            {
                cache.Get(new ChunkCoordinate(0,0));cache.Get(new ChunkCoordinate(1,0));
                session.AddStroke(Stroke("distant",-16000,-16000));
                Assert.That(cache.ResidentCount,Is.EqualTo(2));Assert.That(cache.LastUpdatedTiles,Is.Zero);
                session.AddStroke(Stroke("near",8,8));Assert.That(cache.LastUpdatedTiles,Is.EqualTo(1));
                Assert.That(cache.ResidentCount,Is.EqualTo(2));
            }
        }
        [Test] public void MapEdit_DirtyReplayMatchesFullRasterForOverlapEraseUndoAndLoad()
        {
            var session=new MapEditSession(Map());const int size=256;byte[] incremental=Raster(session,size);
            session.Changed+=change=>
            {
                var b=session.Bounds;var r=SurfacePaintRasterizer.PixelRect(b,size,size,change.bounds);
                var area=new Rect(b.x+r.x*b.z/size,b.y+r.y*b.w/size,r.width*b.z/size,r.height*b.w/size);
                SurfacePaintRasterizer.Rebuild(incremental,b,size,size,session.QuerySurface(area),r);
            };
            session.AddStroke(Stroke("a",-1,-1));session.AddStroke(Stroke("b",0,0,false));
            var erase=Stroke("erase",1,1,false);erase.erase=true;session.AddStroke(erase);
            CollectionAssert.AreEqual(Raster(session,size),incremental);session.Undo();CollectionAssert.AreEqual(Raster(session,size),incremental);
            session.Redo();session.BeginStroke(new SurfacePaintStroke{road=true,radius=4});session.AppendPoint(new Vector2(-31,-31));session.AppendPoint(new Vector2(31,31));
            CollectionAssert.AreEqual(Raster(session,size),incremental);session.CancelStroke();CollectionAssert.AreEqual(Raster(session,size),incremental);
            string json=session.ExportJson();session.ClearEdits();session.LoadJson(json);CollectionAssert.AreEqual(Raster(session,size),incremental);
        }
        [Test] public void MapEdit_QueryUsesGlobalOrderAcrossChunksAndRoadAdapterIsStable()
        {
            var session=new MapEditSession(Map());var longRoad=Stroke("long",-40,-4);longRoad.points.Add(new Vector2(40,-4));
            session.AddStroke(longRoad);session.AddStroke(Stroke("cover",1,-4,false));
            var nearby=session.QuerySurface(new Rect(-1,-8,4,8));Assert.That(nearby[0].id,Is.EqualTo("long"));Assert.That(nearby[1].id,Is.EqualTo("cover"));
            var road=nearby[0].ToRoadData();Assert.That(road.stableId,Is.EqualTo(longRoad.ToRoadData().stableId));Assert.That(road.centerline.Count,Is.EqualTo(2));
        }
    }
}
