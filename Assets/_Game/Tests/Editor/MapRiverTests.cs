using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
namespace LittleCastle.Tests
{
    public sealed class MapRiverTests
    {
        static MapAuthoringDocument Map()=>new MapAuthoringDocument{mapId="river-test",bounds=new Vector4(-128,-128,256,256),heightField=new MapHeightFieldData()};
        static MapRiverRecord River()=>new MapRiverRecord{id=10,nodes=new List<MapRiverNode>{new MapRiverNode{id=1,position=new Vector2(0,-30)},new MapRiverNode{id=2,position=Vector2.zero},new MapRiverNode{id=3,position=new Vector2(0,30)}}};
        static MapEditSession Session(){var s=new MapEditSession(Map());s.RegisterRockCatalog(new[]{new MapRockDefinition{id="cliff.a"},new MapRockDefinition{id="cliff.b"}});return s;}
        [Test] public void River_MoveDeleteRestoreUnderlyingHeightEditsAndSharedHistory()
        {
            var s=Session();s.BeginHeightStroke(4,1);s.AppendHeightPoint(Vector2.zero);s.CommitStroke();s.UpsertRiver(River());Assert.That(s.SampleHeight(0,0),Is.LessThan(-2));Assert.That(s.SampleBaseHeight(0,0),Is.EqualTo(1));
            s.TranslateRiver(10,new Vector2(30,0));Assert.That(s.SampleHeight(0,0),Is.EqualTo(1));s.Undo();Assert.That(s.SampleHeight(0,0),Is.LessThan(-2));s.DeleteRiver(10);Assert.That(s.SampleHeight(0,0),Is.EqualTo(1));s.Undo();s.Undo();Assert.That(s.SampleHeight(0,0),Is.EqualTo(1));s.Undo();Assert.That(s.SampleHeight(0,0),Is.Zero);
        }
        [Test] public void River_SingleRampIsNotFord_BothRampsAndCrossBandAreTraversable()
        {
            var s=Session();s.UpsertRiver(River());Assert.That(s.CanTraverse(new Vector2(-12,0),new Vector2(12,0)),Is.False);
            s.PaintBank(10,30,7,true,false);Assert.That(s.CanTraverse(new Vector2(-12,0),new Vector2(12,0)),Is.False);
            s.PaintBank(10,30,7,true,true,true,.25f);Assert.That(s.CanTraverse(new Vector2(-12,0),new Vector2(12,0)),Is.True);
            Assert.That(s.QueryWater(Vector2.zero,out var level,out var depth,out var flow),Is.True);Assert.That(level,Is.EqualTo(-1));Assert.That(depth,Is.EqualTo(.25f).Within(.01));Assert.That(flow.y,Is.GreaterThan(0));
            s.Undo();Assert.That(s.CanTraverse(new Vector2(-12,0),new Vector2(12,0)),Is.False);
        }
        [Test] public void River_SaveLoadAndCatalogUpdateRetainFinalCompositionAndManualRockEdits()
        {
            var s=Session();s.UpsertRiver(River());var r=s.GetRiver(10);Assert.That(r.rocks.Count,Is.GreaterThan(10));var rock=r.rocks[0];rock.topOffset=.2f;rock.removed=true;s.EditRiverRock(10,rock);
            string json=s.ExportJson();var fresh=Session();fresh.RegisterRockCatalog(new[]{new MapRockDefinition{id="cliff.new",weight=100}});fresh.LoadJson(json);Assert.That(fresh.ExportJson(),Is.EqualTo(json));fresh.UpsertRiver(fresh.GetRiver(10));Assert.That(fresh.ExportJson(),Is.EqualTo(json));
            fresh.ClearEdits();Assert.That(fresh.RiverCount,Is.Zero);fresh.Undo();Assert.That(fresh.ExportJson(),Is.EqualTo(json));
        }
        [Test] public void River_FailedLoadIsAtomicIncludingTerrainAndHistory()
        {
            var s=Session();s.UpsertRiver(River());string old=s.ExportJson();var bad=s.ExportSave();bad.rivers[0].nodes[1].waterHeight=2;Assert.Throws<InvalidDataException>(()=>s.LoadSave(bad));Assert.That(s.ExportJson(),Is.EqualTo(old));Assert.That(s.CanUndo,Is.True);
            bad=s.ExportSave();bad.rivers.Add(bad.rivers[0]);Assert.Throws<InvalidDataException>(()=>s.LoadSave(bad));Assert.That(s.ExportJson(),Is.EqualTo(old));
        }
        [Test] public void River_PlayerCannotEditOrUndoDeveloperCommands()
        {
            var s=Session();s.UpsertRiver(River());s.Access=MapEditAccess.Player;Assert.Throws<UnauthorizedAccessException>(()=>s.DeleteRiver(10));Assert.Throws<UnauthorizedAccessException>(()=>s.Undo());Assert.Throws<UnauthorizedAccessException>(()=>s.PaintBank(10,30,5,true,true,true));Assert.That(s.RiverCount,Is.EqualTo(1));s.LoadJson(s.ExportJson());Assert.That(s.RiverCount,Is.EqualTo(1));
        }
        [Test] public void River_CurveHasContinuousDownhillFlowAndVariableIndependentBanks()
        {
            var r=River();r.nodes[1].position=new Vector2(7,0);r.nodes[2].waterHeight=-1.4f;r.nodes[2].width=12;r.nodes[1].leftBank=MapBankKind.Sand;r.nodes[1].leftTransition=9;
            var p=new MapRiverPath(r);Assert.That(p.frames[0].tangent.x,Is.GreaterThan(0));Assert.That(p.frames[p.frames.Count-1].tangent.x,Is.LessThan(0));
            float previous=float.PositiveInfinity;foreach(var f in p.frames){Assert.That(f.water,Is.LessThanOrEqualTo(previous+.001));Assert.That(f.tangent.magnitude,Is.EqualTo(1).Within(.001));previous=f.water;}Assert.That(p.At(p.Length*.5f).leftSand,Is.GreaterThan(.95));Assert.That(p.At(p.Length*.5f).rightSand,Is.Zero);
        }
        [Test] public void River_RejectsCrossingsUpstreamWaterBadProfilesAndProtectedAreas()
        {
            var r=River();r.nodes[1].waterHeight=0;Assert.Throws<InvalidDataException>(()=>new MapRiverPath(r));
            r=River();r.nodes[1].width=float.NaN;Assert.Throws<InvalidDataException>(()=>new MapRiverPath(r));
            r=River();r.nodes=new List<MapRiverNode>{new MapRiverNode{id=1,position=new Vector2(-25,-25)},new MapRiverNode{id=2,position=new Vector2(25,25)},new MapRiverNode{id=3,position=new Vector2(-25,25)},new MapRiverNode{id=4,position=new Vector2(25,-25)}};Assert.Throws<InvalidDataException>(()=>new MapRiverPath(r));
            var map=Map();map.heightField.protectedAreas.Add(new Rect(-2,-2,4,4));var s=new MapEditSession(map);Assert.Throws<InvalidDataException>(()=>s.UpsertRiver(River()));Assert.That(s.RiverCount,Is.Zero);
        }
        [Test] public void River_ExportAuthoringPreservesBaseAndRiverLayersWithoutDoubleCarving()
        {
            var s=Session();s.UpsertRiver(River());s.PaintBank(10,30,7,true,true,true);var map=s.ExportMap();var copy=new MapEditSession(map);Assert.That(copy.SampleHeight(0,0),Is.EqualTo(s.SampleHeight(0,0)));Assert.That(copy.SampleBaseHeight(0,0),Is.Zero);copy.DeleteRiver(10);Assert.That(copy.SampleHeight(0,0),Is.Zero);copy.ClearEdits();Assert.That(copy.RiverCount,Is.EqualTo(1));
        }
        [Serializable] sealed class LegacyV4 {public int version=1;public string mapId;public int revision=1,decorationRulesVersion=1;public Vector4 bounds;public LittleCastle.Rendering.SurfacePaintDocument surface;public MapHeightFieldData heightField;public List<MapTreeInstance> trees;public List<Rect> placementExclusions;}
        [Test] public void River_V4MigrationRetainsImportedTreesAndHeightBase()
        {
            var m=Map();m.trees.Add(new MapTreeInstance{stableId=99,archetypeId="oak.a",position=new Vector2(40,40)});var legacy=new LegacyV4{mapId=m.mapId,bounds=m.bounds,surface=m.surface,heightField=m.heightField,trees=m.trees,placementExclusions=m.placementExclusions};
            ulong h=14695981039346656037UL;foreach(char c in JsonUtility.ToJson(legacy)){h^=c;h=unchecked(h*1099511628211UL);}var save=new MapEditSave{version=4,mapId=m.mapId,baseRevision=1,baseFingerprint=h.ToString("x16")};var s=new MapEditSession(m);s.UpsertRiver(River());s.LoadSave(save);Assert.That(s.RiverCount,Is.Zero);Assert.That(s.TreeCount,Is.EqualTo(1));Assert.That(s.HasTree(99),Is.True);
        }
        [Test] public void River_DerivedVegetationExclusionRestoresWithoutDeletingTreeData()
        {
            var s=Session();s.UpsertRiver(River());Assert.That(s.VegetationAllowed(0,0),Is.False);Assert.That(s.VegetationAllowed(40,0),Is.True);s.DeleteRiver(10);Assert.That(s.VegetationAllowed(0,0),Is.True);
        }
        [Test] public void River_StandaloneCliffsShareCatalogAndHistoryAndPersist()
        {
            var s=Session();s.UpsertRockSection(new MapRockSection{id=20,points=new List<Vector2>{new Vector2(-30,-20),new Vector2(-30,20)}});Assert.That(s.ExportRockSections()[0].rocks.Count,Is.GreaterThan(4));string json=s.ExportJson();s.Undo();Assert.That(s.ExportRockSections(),Is.Empty);s.Redo();Assert.That(s.ExportJson(),Is.EqualTo(json));var fresh=Session();fresh.LoadJson(json);Assert.That(fresh.ExportJson(),Is.EqualTo(json));
        }
        [Test] public void River_LocalPatchNotificationAndSparseLargeMapStayBounded()
        {
            var map=Map();map.bounds=new Vector4(-32768,-32768,65536,65536);var s=new MapEditSession(map);s.UpsertRiver(River());MapRegionChange last=default;s.Changed+=c=>last=c;s.PaintBank(10,30,5,true,true,true);Assert.That(last.bounds.height,Is.LessThan(45));Assert.That(last.bounds.width,Is.LessThan(45));Assert.That(s.EditedHeightSamples,Is.Zero);Assert.That(s.HeightSettings.tiles,Is.Empty);
        }
    }
}
