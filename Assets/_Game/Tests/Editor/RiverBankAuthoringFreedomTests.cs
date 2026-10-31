using System;
using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class RiverBankAuthoringFreedomTests
    {
        static MapAuthoringDocument Map() => new MapAuthoringDocument
        {
            mapId = "dry-bank-authoring",
            bounds = new Vector4(-128, -128, 256, 256),
            heightField = new MapHeightFieldData()
        };

        static MapEditSession Session(int version)
        {
            var session = new MapEditSession(Map());
            session.RegisterTreeCatalog(new[] { new MapTreeDefinition
            {
                archetypeId = "oak.bank", minimumSpacing = 4, maxSlopeDegrees = 70
            } }, Array.Empty<MapTreePack>());
            session.UpsertRiver(new MapRiverRecord
            {
                id = 101, layoutVersion = version,
                nodes = new List<MapRiverNode>
                {
                    Node(1, -40), Node(2, 40)
                }
            });
            return session;
        }

        static MapRiverNode Node(long id, float z) => new MapRiverNode
        {
            id = id, position = new Vector2(0, z), width = 9,
            waterHeight = -1, leftBank = MapBankKind.Sand, rightBank = MapBankKind.Sand,
            leftTransition = 20, rightTransition = 20
        };

        [TestCase(1)] [TestCase(2)]
        public void SculptedBankRefreshesResidentGrassMaskAndUndoRestoresIt(int version)
        {
            var session=Session(version);
            using(var cache=new MapSurfaceTileCache(session,32))
            {
                var tile=cache.Get(new ChunkCoordinate(0,0));
                int x=Mathf.FloorToInt((15.5f-tile.bounds.x)*tile.size/tile.bounds.z);
                int z=Mathf.FloorToInt((.5f-tile.bounds.y)*tile.size/tile.bounds.w);
                int index=(z*tile.size+x)*2+1;
                Assert.That(tile.pixels[index],Is.EqualTo(255));int revision=tile.revision;
                session.BeginHeightStroke(4,-2);session.AppendHeightPoint(new Vector2(15,0));session.CommitStroke();
                Assert.That(tile.revision,Is.GreaterThan(revision));Assert.That(tile.pixels[index],Is.Zero);
                session.Undo();Assert.That(tile.pixels[index],Is.EqualTo(255));
            }
        }

        [TestCase(1)] [TestCase(2)]
        public void AuthorHeightBrushMovesVisibleDryBankAndSurvivesHistoryAndSave(int version)
        {
            var session=Session(version);var p=new Vector2(15,0);
            float before=session.SampleHeight(p.x,p.y);string river=JsonUtility.ToJson(session.GetRiver(101));
            session.BeginHeightStroke(2,1);session.AppendHeightPoint(p);session.CommitStroke();
            Assert.That(session.SampleHeight(p.x,p.y),Is.EqualTo(before+1).Within(.0001f));
            session.Undo();Assert.That(session.SampleHeight(p.x,p.y),Is.EqualTo(before).Within(.0001f));
            session.Redo();string save=session.ExportJson();var loaded=Session(version);loaded.LoadJson(save);
            Assert.That(loaded.SampleHeight(p.x,p.y),Is.EqualTo(before+1).Within(.0001f));
            Assert.That(JsonUtility.ToJson(loaded.GetRiver(101)),Is.EqualTo(river));
        }

        [TestCase(1)] [TestCase(2)]
        public void WideSandBank_DryGroundAllowsGrassAndTreesOnBothSides(int version)
        {
            var s = Session(version);
            foreach (float x in new[] { -15f, 15f })
            {
                Assert.That(s.SampleHeight(x, 0), Is.GreaterThan(-1f));
                Assert.That(s.VegetationAllowed(x, 0), Is.True, "dry grass");
                Assert.That(s.VegetationAllowed(x, 0, 2), Is.True, "dry tree footprint");
                Assert.That(s.PlaceTree("oak.bank", new Vector2(x, 0)), Is.Not.Zero);
            }
            Assert.That(s.TreeCount, Is.EqualTo(2));
        }

        [TestCase(1)] [TestCase(2)]
        public void WaterAndFootprintThatReachesWaterAreRejected(int version)
        {
            var s = Session(version);
            Assert.That(s.QueryWater(Vector2.zero, out _, out _, out _), Is.True);
            Assert.That(s.VegetationAllowed(0, 0, 0), Is.False);
            Assert.That(s.SampleHeight(12, 0), Is.GreaterThan(-1f), "center is dry");
            Assert.That(s.VegetationAllowed(12, 0, 6), Is.False, "footprint reaches water");
            Assert.Throws<InvalidOperationException>(() => s.PlaceTree("oak.bank", Vector2.zero));
            Assert.That(s.CanTraverse(new Vector2(-15, 0), new Vector2(15, 0)), Is.False);
        }

        [Test] public void ExistingStandaloneAndRiverRocksAndReservedAreasStillExclude()
        {
            var map = Map();
            map.placementExclusions.Add(new Rect(55, 0, 4, 4));
            var s = new MapEditSession(map);
            var river = new MapRiverRecord { id = 101, nodes = new List<MapRiverNode> { Node(1, -40), Node(2, 40) } };
            river.rocks.Add(new MapRockInstance { id = 20, archetypeId = "rock.a", position = new Vector2(35, 0), manual = true });
            s.UpsertRiver(river);
            s.UpsertRockSection(new MapRockSection
            {
                id = 102, points = new List<Vector2> { new Vector2(45, -3), new Vector2(45, 3) },
                rocks = new List<MapRockInstance> { new MapRockInstance { id = 21, archetypeId = "rock.b", position = new Vector2(45, 0) } }
            });
            Assert.That(s.VegetationAllowed(37.5f, 0, 1), Is.False);
            Assert.That(s.VegetationAllowed(47.5f, 0, 1), Is.False);
            Assert.That(s.VegetationAllowed(56, 1), Is.False);
            Assert.That(s.VegetationAllowed(51, 0), Is.True);
        }

        [TestCase(1)] [TestCase(2)]
        public void PaintAndRiverRecordsSurviveUndoAndReloadAndDryBankRaster(int version)
        {
            var s = Session(version);
            var riverBefore = JsonUtility.ToJson(s.GetRiver(101));
            s.AddStroke(new SurfacePaintStroke { id = "grass-bank", road = false, radius = 3, strength = 1,
                points = new List<Vector2> { new Vector2(15, 0) } });
            Assert.That(s.ExportEdits().strokes.Count, Is.EqualTo(1));
            s.Undo(); Assert.That(s.ExportEdits().strokes, Is.Empty);
            s.Redo(); Assert.That(s.ExportEdits().strokes.Count, Is.EqualTo(1));
            string save = s.ExportJson();
            var restored = Session(version); restored.LoadJson(save);
            Assert.That(restored.ExportJson(), Is.EqualTo(save));
            Assert.That(JsonUtility.ToJson(restored.GetRiver(101)), Is.EqualTo(riverBefore));
            var data = new byte[2];
            var bounds = new Vector4(14.5f, -.5f, 1, 1);
            var dirty = new RectInt(0, 0, 1, 1);
            SurfacePaintRasterizer.Rebuild(data, bounds, 1, 1,
                restored.QuerySurface(new Rect(14, -1, 2, 2)), dirty);
            SurfacePaintRasterizer.ApplyRiverExclusions(data, bounds, 1, 1, restored, dirty);
            Assert.That(data[1], Is.EqualTo(255), "dry-bank grass must not be attenuated by the sand transition");
            data[0] = 123; data[1] = 255;
            SurfacePaintRasterizer.ApplyRiverExclusions(data, new Vector4(-.5f, -.5f, 1, 1), 1, 1, restored, dirty);
            Assert.That(data[0], Is.EqualTo(123), "road channel is preserved");
            Assert.That(data[1], Is.Zero, "underwater grass is suppressed");
        }
    }
}
