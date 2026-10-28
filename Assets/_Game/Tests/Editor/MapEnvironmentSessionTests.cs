using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.World;
using LittleCastle.World.Environment;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapEnvironmentSessionTests
    {
        static MapAuthoringDocument Map()=>new MapAuthoringDocument{mapId="environment-session",bounds=new Vector4(-64,-64,128,128)};
        static MapEditSession Session()=>new MapEditSession(Map());
        static void Paint(MapEditSession s,Vector2 p,bool snow=false)
        {s.BeginEnvironmentStroke(3,snow);s.ApplyEnvironmentBrush(p,4,1,.4f);s.CommitStroke();}
        [Test] public void StrokeHasOneUndoAndCancelRestoresTileData()
        {
            var s=Session();s.BeginEnvironmentStroke(3);s.ApplyEnvironmentBrush(new Vector2(-32,-32),4,1,.5f);s.ApplyEnvironmentBrush(new Vector2(-30,-30),4,1,.5f);s.CommitStroke();
            Assert.That(s.EnvironmentThemes.Sample(3,-32,-32),Is.GreaterThan(.9));s.Undo();Assert.That(s.EnvironmentThemes.Capture(),Is.Empty);Assert.That(s.CanUndo,Is.False);
            s.Redo();string before=s.ExportJson();s.BeginEnvironmentStroke(1);s.ApplyEnvironmentBrush(new Vector2(-32,-32),4,1,.3f);s.CancelStroke();Assert.That(s.ExportJson(),Is.EqualTo(before));
        }
        [Test] public void FiniteMapClipsCellsAtNegativeEdgeWithoutTouchingDistantTiles()
        {
            var s=Session();Paint(s,new Vector2(-64,-64));
            Assert.That(s.EnvironmentThemes.GetCell(3,-129,-129),Is.Zero);Assert.That(s.EnvironmentThemes.GetCell(3,-128,-128),Is.GreaterThan(0));
            Assert.That(s.EnvironmentThemes.Sample(3,40,40),Is.Zero);
            Assert.DoesNotThrow(()=>new MapEditSession(s.ExportMap()));
        }
        [Test] public void SaveLoadAndAuthoredBasePreserveBiomesSnowAndWeather()
        {
            var s=Session();Paint(s,Vector2.zero);Paint(s,new Vector2(20,20),true);
            var weather=s.EnvironmentSettings;weather.simulateWeather=true;weather.winterSnowPreview=false;weather.snowfallIntensity=.8f;s.SetEnvironmentSettings(weather);
            var loaded=Session();loaded.LoadJson(s.ExportJson());Assert.That(JsonUtility.ToJson(loaded.ExportEnvironment()),Is.EqualTo(JsonUtility.ToJson(s.ExportEnvironment())));
            var rebased=new MapEditSession(s.ExportMap());Assert.That(JsonUtility.ToJson(rebased.ExportEnvironment()),Is.EqualTo(JsonUtility.ToJson(s.ExportEnvironment())));
        }
        [Test] public void InvalidSaveIsAtomicAndRejectedBeforeAnyWorldMutation()
        {
            var s=Session();Paint(s,Vector2.zero);string before=s.ExportJson();var bad=s.ExportSave();bad.environment.snow.Add(new ScalarTileSnapshot{tileX=0,tileZ=0,values=new[]{float.NaN}});
            Assert.Catch(()=>s.LoadSave(bad));Assert.That(s.ExportJson(),Is.EqualTo(before));
        }
        [Test] public void ClearEditsAndUndoRestoreBothIndependentFields()
        {
            var s=Session();Paint(s,Vector2.zero);Paint(s,Vector2.zero,true);string before=JsonUtility.ToJson(s.ExportEnvironment());s.ClearEdits();
            Assert.That(s.EnvironmentThemes.Capture(),Is.Empty);Assert.That(s.EnvironmentSnow.Capture(),Is.Empty);s.Undo();Assert.That(JsonUtility.ToJson(s.ExportEnvironment()),Is.EqualTo(before));
        }
        [Test] public void BiomeReplicaDoesNotSynchronizeLocalSnowOrWeatherAndRejectsStaleState()
        {
            var host=Session();var client=Session();Paint(client,new Vector2(16,16),true);var settings=client.EnvironmentSettings;settings.winterSnowPreview=false;client.SetEnvironmentSettings(settings);
            float snow=client.EnvironmentSnow.Sample(16,16);Assert.That(snow,Is.GreaterThan(.9f));Paint(host,Vector2.zero);client.ApplyBiomeSnapshot(host.ExportBiomeSnapshot());
            Assert.That(client.EnvironmentThemes.Sample(3,0,0),Is.GreaterThan(.9));Assert.That(client.EnvironmentSnow.Sample(16,16),Is.EqualTo(snow));Assert.That(client.EnvironmentSettings.winterSnowPreview,Is.False);
            var stale=host.ExportBiomeSnapshot();stale.revision=-1;Assert.Throws<InvalidDataException>(()=>client.ApplyBiomeSnapshot(stale));
        }
        [Test] public void PlayerCannotUseDeveloperEnvironmentBrushes()
        {
            var s=new MapEditSession(Map(),MapEditAccess.Player);Assert.Throws<UnauthorizedAccessException>(()=>s.BeginEnvironmentStroke(3));Assert.Throws<UnauthorizedAccessException>(()=>s.BeginEnvironmentStroke(3,true));
        }
        [Test] public void SnowEraserClearsWinterPreviewAndPersistsWithoutChangingBiome()
        {
            var s=Session();Paint(s,Vector2.zero);Assert.That(s.SampleEnvironmentSnow(0,0),Is.GreaterThan(.9));
            long revision=s.BiomeRevision;s.BeginEnvironmentStroke(3,true,true);s.ApplyEnvironmentBrush(Vector2.zero,3,1,.2f);s.CommitStroke();
            Assert.That(s.SampleEnvironmentSnow(0,0),Is.Zero);Assert.That(s.EnvironmentThemes.Sample(3,0,0),Is.GreaterThan(.9));Assert.That(s.BiomeRevision,Is.EqualTo(revision));
            var loaded=Session();loaded.LoadJson(s.ExportJson());Assert.That(loaded.SampleEnvironmentSnow(0,0),Is.Zero);
            s.Undo();Assert.That(s.SampleEnvironmentSnow(0,0),Is.GreaterThan(.9));s.Redo();Assert.That(s.SampleEnvironmentSnow(0,0),Is.Zero);
        }
        [TestCase(false)] [TestCase(true)] public void ClearAndUndoRequireOnlyTheEditedEnvironmentDomain(bool snow)
        {
            var s=new MapEditSession(Map(),snow?MapEditAccess.Snow:MapEditAccess.Biomes);Paint(s,Vector2.zero,snow);
            Assert.DoesNotThrow(()=>s.ClearEdits());Assert.That(s.EnvironmentThemes.Sample(3,0,0),Is.Zero);Assert.That(s.EnvironmentSnow.Sample(0,0),Is.Zero);
            s.Undo();Assert.That(snow?s.EnvironmentSnow.Sample(0,0):s.EnvironmentThemes.Sample(3,0,0),Is.GreaterThan(.9));
        }
        [Test] public void NotificationsAreRegionalAndDoNotRegenerateTerrain()
        {
            var s=Session();var changes=new List<MapRegionChange>();s.Changed+=changes.Add;long height=s.HeightRevision;
            using(s.BatchNotifications()){Paint(s,new Vector2(-32,-32));}
            Assert.That(changes.Count,Is.InRange(1,4));foreach(var c in changes){Assert.That(c.kind,Is.EqualTo(MapChangeKind.Biomes));Assert.That(c.bounds.width,Is.LessThanOrEqualTo(32));}
            Assert.That(s.HeightRevision,Is.EqualTo(height));
        }
        [Test] public void RegisteredToolsUseSharedControllerAndSaveAsOneStroke()
        {
            var s=Session();var controller=new MapToolController(s,MapToolRegistry.ExistingBrushes());controller.Select("environment.biome");controller.context.settings.radius=4;
            controller.Begin(new MapToolPointer(new Ray(),true,Vector2.zero),0);controller.Complete();Assert.That(s.EnvironmentThemes.Sample(3,0,0),Is.GreaterThan(.9));s.Undo();Assert.That(s.EnvironmentThemes.Capture(),Is.Empty);
        }
        [Test] public void LegacyV5FingerprintAndSurfaceSaveRemainLoadable()
        {
            var map=Map();string oldJson=JsonUtility.ToJson(map);
            int field=oldJson.LastIndexOf(",\"environment\":",StringComparison.Ordinal);
            Assert.That(field,Is.GreaterThan(0));oldJson=oldJson.Substring(0,field)+"}";
            ulong hash=14695981039346656037UL;foreach(char c in oldJson){hash^=c;hash=unchecked(hash*1099511628211UL);}
            var save=new MapEditSave{version=5,mapId=map.mapId,baseRevision=map.revision,baseFingerprint=hash.ToString("x16")};
            save.surface.strokes.Add(new Rendering.SurfacePaintStroke{id="legacy-path",points=new List<Vector2>{Vector2.zero}});
            var s=new MapEditSession(map);Paint(s,Vector2.zero);s.LoadSave(save);
            Assert.That(s.ExportEdits().strokes.Count,Is.EqualTo(1));Assert.That(s.EnvironmentThemes.Capture(),Is.Empty);
        }
    }
}
