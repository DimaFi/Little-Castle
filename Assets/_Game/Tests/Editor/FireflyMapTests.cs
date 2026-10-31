using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class FireflyMapTests
    {
        static MapAuthoringDocument Map() => new MapAuthoringDocument
        {
            mapId="fireflies-test",bounds=new Vector4(-64,-64,128,128),
            heightField=new MapHeightFieldData()
        };

        static FireflyAreaData Area(string id, FireflyDensityMode mode, int priority=0) =>
            new FireflyAreaData{id=id,name=id,center=Vector2.zero,
                radius=new Vector2(20,12),densityMode=mode,densityValue=3,priority=priority};

        [Test]
        public void Hash_IsStableAcrossNegativeCoordinatesAndStreams()
        {
            uint a=FireflyDistribution.Hash(-1024,-7,777,1);
            Assert.AreEqual(a,FireflyDistribution.Hash(-1024,-7,777,1));
            Assert.AreNotEqual(a,FireflyDistribution.Hash(-1024,-7,778,1));
            Assert.AreNotEqual(a,FireflyDistribution.Hash(-1024,-7,777,2));
            Assert.That(FireflyDistribution.Random01(-1024,-7,777),Is.InRange(0f,1f));
        }

        [Test]
        public void Areas_ExclusionBeatsSetAndTieOrderIsStable()
        {
            var data=new FireflyMapData{densityPer100m2=10};
            var low=Area("a",FireflyDensityMode.Set);
            low.densityValue=30;
            var high=Area("z",FireflyDensityMode.Set);
            high.densityValue=40;
            data.areas=new List<FireflyAreaData>{high,low};
            Assert.That(FireflyDistribution.Density(data,Vector2.zero),Is.EqualTo(40).Within(.001));
            data.areas.Reverse();
            Assert.That(FireflyDistribution.Density(data,Vector2.zero),Is.EqualTo(40).Within(.001));
            data.areas.Add(Area("exclude",FireflyDensityMode.Exclude,-999));
            Assert.That(FireflyDistribution.Density(data,Vector2.zero),Is.EqualTo(0).Within(.001));
            Assert.That(FireflyDistribution.Density(data,new Vector2(30,0)),Is.EqualTo(10).Within(.001));
        }

        [Test]
        public void PolygonBoundary_AndFalloffWork()
        {
            var polygon=Area("polygon",FireflyDensityMode.Set);
            polygon.shape=FireflyAreaShape.Polygon;
            polygon.polygon=new List<Vector2>{new Vector2(-10,-10),new Vector2(10,-10),
                new Vector2(10,10),new Vector2(-10,10)};
            Assert.That(FireflyDistribution.Influence(polygon,Vector2.zero),Is.EqualTo(1).Within(.001));
            Assert.That(FireflyDistribution.Influence(polygon,new Vector2(12,0)),Is.Zero);
            Assert.That(FireflyDistribution.Influence(polygon,new Vector2(9.5f,0)),Is.InRange(0,1));
        }

        [Test]
        public void MapFireflies_SaveLoadUndoAndClearPreserveOtherFields()
        {
            var map=Map();
            var session=new MapEditSession(map);
            string baseline=JsonUtility.ToJson(session.ExportFireflies());
            var updated=session.ExportFireflies();
            updated.densityPer100m2=16;
            session.SetFireflies(updated);
            session.UpsertFireflyArea(Area("forest",FireflyDensityMode.Multiply));
            Assert.AreEqual(1,session.ExportFireflies().areas.Count);
            string state=JsonUtility.ToJson(session.ExportFireflies());
            session.Undo();
            Assert.IsEmpty(session.ExportFireflies().areas);
            session.Redo();
            Assert.AreEqual(state,JsonUtility.ToJson(session.ExportFireflies()));

            string json=session.ExportJson();
            var restored=new MapEditSession(map);
            restored.LoadJson(json);
            Assert.AreEqual(state,JsonUtility.ToJson(restored.ExportFireflies()));
            Assert.AreEqual(JsonUtility.ToJson(session.ExportEnvironment()),JsonUtility.ToJson(restored.ExportEnvironment()));
            Assert.AreEqual(JsonUtility.ToJson(session.ExportRivers()),JsonUtility.ToJson(restored.ExportRivers()));

            restored.ClearEdits();
            Assert.AreEqual(baseline,JsonUtility.ToJson(restored.ExportFireflies()));
            restored.Undo();
            Assert.AreEqual(state,JsonUtility.ToJson(restored.ExportFireflies()));
        }

        [Test]
        public void MapFireflies_RejectsUnauthorizedAndBrokenDocumentsAtomically()
        {
            var map=Map();
            var player=new MapEditSession(map,MapEditAccess.Player);
            Assert.Throws<UnauthorizedAccessException>(()=>player.UpsertFireflyArea(Area("blocked",FireflyDensityMode.Multiply)));
            var session=new MapEditSession(map);
            var before=JsonUtility.ToJson(session.ExportFireflies());
            var invalid=session.ExportFireflies();
            invalid.areas.Add(Area("duplicate",FireflyDensityMode.Set));
            invalid.areas.Add(Area("duplicate",FireflyDensityMode.Set));
            Assert.Throws<InvalidDataException>(()=>session.SetFireflies(invalid));
            Assert.AreEqual(before,JsonUtility.ToJson(session.ExportFireflies()));
            Assert.IsFalse(session.CanUndo);
        }

        [Test]
        public void Quality_OffNeverEmitsAndSliderGrowsBudget()
        {
            var q=new FireflyRuntimeQuality{preset=FireflyQuality.Off,graphicsVsFps=1};
            q.ResolveAdaptive(out int off,out int nearOff,out _);
            Assert.Zero(off);Assert.Zero(nearOff);
            q.preset=FireflyQuality.Medium;q.graphicsVsFps=0;
            q.ResolveAdaptive(out int small,out int nearSmall,out float close);
            q.graphicsVsFps=1;
            q.ResolveAdaptive(out int large,out int nearLarge,out float far);
            Assert.Greater(large,small);Assert.Greater(nearLarge,nearSmall);Assert.Greater(far,close);
        }
    }
}
