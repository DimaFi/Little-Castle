using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapTreeTests
    {
        static MapAuthoringDocument Map()=>new MapAuthoringDocument{mapId="trees",bounds=new Vector4(-64,-64,128,128),heightField=new MapHeightFieldData()};
        static MapTreeDefinition[] Definitions()=>new[]{new MapTreeDefinition{archetypeId="oak.a"},new MapTreeDefinition{archetypeId="oak.b"},new MapTreeDefinition{archetypeId="oak.c"},new MapTreeDefinition{archetypeId="oak.d"}};
        static MapTreePack Pack()=>new MapTreePack{id="oaks",entries=new List<MapTreePackEntry>{new MapTreePackEntry{archetypeId="oak.a"},new MapTreePackEntry{archetypeId="oak.b"},new MapTreePackEntry{archetypeId="oak.c"},new MapTreePackEntry{archetypeId="oak.d"}}};
        static MapEditSession Session(MapAuthoringDocument map=null,MapEditAccess access=MapEditAccess.All)
        {var session=new MapEditSession(map??Map(),access);session.RegisterTreeCatalog(Definitions(),new[]{Pack()});return session;}
        static string Trees(MapEditSession s)=>JsonUtility.ToJson(new TreeList{records=s.ExportTrees()});
        [Serializable] sealed class TreeList {public List<MapTreeInstance> records;}
        [Test] public void MapTrees_FinalRecordsStableThroughMoveRotateDeleteUndoAndSave()
        {
            var s=Session();long id=s.PlaceTree("oak.a",new Vector2(-8,-8),15,1,123);s.TransformTree(id,new Vector2(8,-8),400,1.1f);
            Assert.That(s.GetTree(id).yawDegrees,Is.EqualTo(40));string expected=Trees(s);s.DeleteTree(id);Assert.That(s.TreeCount,Is.Zero);s.Undo();Assert.That(Trees(s),Is.EqualTo(expected));
            s.Redo();s.Undo();var saved=s.ExportJson();var fresh=Session();fresh.LoadJson(saved);Assert.That(Trees(fresh),Is.EqualTo(expected));
            s.GetTree(id).position=Vector2.zero;Assert.That(s.GetTree(id).position,Is.EqualTo(new Vector2(8,-8)));
        }
        [Test] public void MapTrees_BrushIsDeterministicAndOneTransaction()
        {
            var a=Session();var b=Session();foreach(var s in new[]{a,b}){s.BeginTreeBrush("oaks",18,.8f,6,123,"same-command");s.AppendTreeBrush(new Vector2(-5,-5));s.AppendTreeBrush(new Vector2(4,4));s.CommitStroke();}
            Assert.That(a.TreeCount,Is.GreaterThan(5));Assert.That(Trees(a),Is.EqualTo(Trees(b)));string expected=Trees(a);a.Undo();Assert.That(a.TreeCount,Is.Zero);a.Redo();Assert.That(Trees(a),Is.EqualTo(expected));
            int before=a.TreeCount;a.BeginTreeBrush("oaks",18,.8f,6,123,"repeat");a.AppendTreeBrush(new Vector2(-5,-5));a.AppendTreeBrush(new Vector2(4,4));a.CommitStroke();Assert.That(a.TreeCount,Is.EqualTo(before));
        }
        [Test] public void MapTrees_PlayerCannotUseAuthorPacksOrAuthorOnlyEntries()
        {
            var s=Session(access:MapEditAccess.Player);s.PlaceTree("oak.a",new Vector2(8,8));Assert.Throws<UnauthorizedAccessException>(()=>s.BeginTreeBrush("oaks",8,1,6,1));
            var defs=Definitions();defs[0].authorOnly=true;s.RegisterTreeCatalog(defs,new[]{Pack()});Assert.That(s.CanPlaceTree("oak.a"),Is.False);
            Assert.Throws<UnauthorizedAccessException>(()=>s.PlaceTree("oak.a",new Vector2(-8,-8)));Assert.That(s.TreeCount,Is.EqualTo(1));
        }
        [Test] public void MapTrees_RevokedBrushAccessCancelsPreviewWithoutHistory()
        {
            var s=Session();s.BeginTreeBrush("oaks",15,1,6,1);s.AppendTreeBrush(Vector2.zero);Assert.That(s.TreeCount,Is.GreaterThan(0));s.Access=MapEditAccess.Player;
            Assert.Throws<UnauthorizedAccessException>(()=>s.CommitStroke());Assert.That(s.TreeCount,Is.Zero);Assert.That(s.CanUndo,Is.False);
        }
        [Test] public void MapTrees_RoadWaterBoundsSlopeAndOccupancyUseCommonValidation()
        {
            var map=Map();map.placementExclusions.Add(new Rect(18,18,12,12));var s=Session(map);
            s.AddStroke(new LittleCastle.Rendering.SurfacePaintStroke{id="road",radius=2,points=new List<Vector2>{new Vector2(-20,0),new Vector2(20,0)}});
            Assert.Throws<InvalidOperationException>(()=>s.PlaceTree("oak.a",Vector2.zero));Assert.Throws<InvalidOperationException>(()=>s.PlaceTree("oak.a",new Vector2(20,20)));
            Assert.Throws<InvalidOperationException>(()=>s.PlaceTree("oak.a",new Vector2(63,63)));long id=s.PlaceTree("oak.a",new Vector2(-12,-12));
            Assert.Throws<InvalidOperationException>(()=>s.PlaceTree("oak.b",new Vector2(-11,-12)));Assert.Throws<InvalidOperationException>(()=>s.TransformTree(id,Vector2.zero,90,1));Assert.That(s.GetTree(id).position,Is.EqualTo(new Vector2(-12,-12)));
            Assert.Throws<InvalidDataException>(()=>s.PlaceTree("oak.a",new Vector2(float.NaN,0)));
            var steep=Map();MapHeightField.SetBase(steep.heightField,0,0,4);MapHeightField.SetBase(steep.heightField,1,0,4);var steepSession=Session(steep);
            Assert.Throws<InvalidOperationException>(()=>steepSession.PlaceTree("oak.a",new Vector2(-.5f,0)));
        }
        [Test] public void MapTrees_CatalogReweightAndMembershipChangeNeverMoveSavedInstances()
        {
            var s=Session();s.BeginTreeBrush("oaks",20,1,6,12);s.AppendTreeBrush(Vector2.zero);s.CommitStroke();string expected=Trees(s);string json=s.ExportJson();
            var defs=Definitions();foreach(var d in defs){d.version=9;d.minimumScale=.5f;d.maximumScale=2;}
            var pack=Pack();pack.entries.Reverse();pack.entries[0].weight=100;s.RegisterTreeCatalog(defs,new[]{pack});Assert.That(Trees(s),Is.EqualTo(expected));
            var fresh=Session();fresh.RegisterTreeCatalog(new[]{defs[0]},new[]{new MapTreePack{id="new",entries=new List<MapTreePackEntry>{new MapTreePackEntry{archetypeId="oak.a"}}}});fresh.LoadJson(json);Assert.That(Trees(fresh),Is.EqualTo(expected));
        }
        [Test] public void MapTrees_BaseDeletionClearAndLoadAreAtomicAndPreserveIds()
        {
            var map=Map();map.trees.Add(new MapTreeInstance{stableId=7,archetypeId="oak.a",position=new Vector2(-12,-12)});var s=Session(map);
            s.DeleteTree(7);s.PlaceTree("oak.b",new Vector2(12,12),stableId:8);string expected=Trees(s);var save=s.ExportSave();Assert.That(save.removedTreeIds,Is.EquivalentTo(new long[]{7}));
            s.ClearEdits();Assert.That(s.HasTree(7),Is.True);Assert.That(s.HasTree(8),Is.False);s.Undo();Assert.That(Trees(s),Is.EqualTo(expected));
            var bad=s.ExportSave();bad.treeOverrides.Add(bad.treeOverrides[0]);Assert.Throws<InvalidDataException>(()=>s.LoadSave(bad));Assert.That(Trees(s),Is.EqualTo(expected));
            var fresh=Session(map);fresh.LoadSave(save);Assert.That(Trees(fresh),Is.EqualTo(expected));var baked=Session(s.ExportMap());Assert.That(baked.ExportSave().treeOverrides,Is.Empty);
        }
        [Test] public void MapTrees_TerrainAndTreeHistoryRemainIndependentAndAdaptToWorldSpawn()
        {
            var s=Session();long id=s.PlaceTree("oak.a",new Vector2(-8,-8));s.BeginHeightStroke(4,1);s.AppendHeightPoint(new Vector2(-8,-8));s.CommitStroke();
            var r=s.GetTree(id);var spawn=r.ToWorldSpawn(s.SampleHeight(r.position.x,r.position.y));Assert.That(spawn.stableId,Is.EqualTo(id));Assert.That(spawn.category,Is.EqualTo(SpawnCategory.Tree));Assert.That(spawn.worldPosition.y,Is.EqualTo(1));
            s.Undo();Assert.That(s.TreeCount,Is.EqualTo(1));s.Undo();Assert.That(s.TreeCount,Is.Zero);s.Redo();s.Redo();Assert.That(s.SampleHeight(-8,-8),Is.EqualTo(1));
        }
        [Test] public void MapTrees_LegacyV3KeepsImportedBaseObjectsAndClearsOnlyNewOverrides()
        {
            var map=Map();map.trees.Add(new MapTreeInstance{stableId=7,archetypeId="oak.a",position=new Vector2(-12,-12)});
            string legacy=JsonUtility.ToJson(map);legacy=legacy.Substring(0,legacy.IndexOf(",\"trees\":",StringComparison.Ordinal))+"}";
            ulong hash=14695981039346656037UL;foreach(char c in legacy){hash^=c;hash=unchecked(hash*1099511628211UL);}
            var save=new MapEditSave{version=3,mapId=map.mapId,baseRevision=map.revision,baseFingerprint=hash.ToString("x16")};
            var s=Session(map);s.DeleteTree(7);s.PlaceTree("oak.b",new Vector2(12,12));s.LoadSave(save);Assert.That(s.TreeCount,Is.EqualTo(1));Assert.That(s.HasTree(7),Is.True);
        }
    }
}
