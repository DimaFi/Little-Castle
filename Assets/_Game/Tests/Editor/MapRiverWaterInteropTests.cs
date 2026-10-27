using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapRiverWaterInteropTests
    {
        const string CatalogPath="Assets/_Game/Art/Studies/RiverWater_20261026/RiverWater_Catalog.asset";
        static MapAuthoringDocument Map()=>new MapAuthoringDocument{mapId="water-interop",bounds=new Vector4(-128,-128,256,256)};
        static MapRiverRecord River(int version=2)=>new MapRiverRecord{id=88,layoutVersion=version,nodes=new List<MapRiverNode>
        {
            new MapRiverNode{id=1,position=new Vector2(-18,-40),leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand},
            new MapRiverNode{id=2,position=new Vector2(-18,40),leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand}
        }};
        [Test] public void LegacyV1_ProfileAndSaveReplayKeepOriginalTerrainAndWidths()
        {
            var session=new MapEditSession(Map());session.UpsertRiver(River(1));string saved=session.ExportJson();
            var path=new MapRiverPath(session.GetRiver(88));foreach(var frame in path.frames)Assert.That(frame.width,Is.EqualTo(9));
            Assert.That(session.SampleHeight(-18,0),Is.EqualTo(-3).Within(.001));
            Assert.That(session.SandCoverage(-18,0),Is.EqualTo(1));
            Assert.That(session.SampleHeight(0,0),Is.Zero);
            session.LoadJson(saved);Assert.That(session.ExportJson(),Is.EqualTo(saved));
            Assert.That(session.GetRiver(88).layoutVersion,Is.EqualTo(1));
            session.DeleteRiver(88);Assert.That(session.SampleHeight(-18,0),Is.Zero);
            session.Undo();Assert.That(session.ExportJson(),Is.EqualTo(saved));
        }
        [Test] public void ExplicitV2_SaveMoveDeleteUndoRedoPreserveAuthoritativeState()
        {
            var session=new MapEditSession(Map());session.UpsertRiver(River());string saved=session.ExportJson();
            session.TranslateRiver(88,new Vector2(24,0));Assert.That(session.SampleHeight(-18,0),Is.Zero);
            session.Undo();Assert.That(session.ExportJson(),Is.EqualTo(saved));
            session.DeleteRiver(88);session.Undo();Assert.That(session.ExportJson(),Is.EqualTo(saved));
            session.Redo();Assert.That(session.RiverCount,Is.Zero);session.LoadJson(saved);
            Assert.That(session.ExportJson(),Is.EqualTo(saved));Assert.That(session.GetRiver(88).layoutVersion,Is.EqualTo(2));
        }
        [Test] public void V2_DoesNotErasePlacementExclusionsOutsideRiverCorridor()
        {
            var map=Map();map.placementExclusions.Add(new Rect(50,50,8,8));var session=new MapEditSession(map);
            session.UpsertRiver(River());Assert.That(session.VegetationAllowed(54,54),Is.False);
            Assert.That(session.VegetationAllowed(70,70),Is.True);
        }
        [Test] public void GlobalFlowCoordinatesRemainContinuousWithVariableSpeedAndNegativeStations()
        {
            var record=River();record.nodes[0].speed=record.nodes[1].speed=2;var path=new MapRiverPath(record);
            foreach(var frame in path.frames)Assert.That(path.TravelTime(frame.station),Is.EqualTo(frame.station/2).Within(.0001));
            var texture=RiverBankMeshBuilder.FlowMap(path,new Rect(-32,-48,32,32),out var bounds);
            try
            {
                Assert.That(texture.format,Is.EqualTo(TextureFormat.RGBAFloat));Assert.That(texture.wrapMode,Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(bounds.x,Is.LessThan(-32));Assert.That(bounds.y,Is.LessThan(-48));
                Assert.That(bounds.z,Is.GreaterThan(0));Assert.That(bounds.w,Is.GreaterThan(0));
            }
            finally{Object.DestroyImmediate(texture);}
        }
        [Test] public void WaterMaterial_ActiveTextureClosureAndRequiredFeaturePropertiesResolve()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<MapRockCatalog>(CatalogPath);Assert.That(catalog,Is.Not.Null);
            Assert.That(catalog.entries,Is.Empty);var material=catalog.waterMaterial;Assert.That(material,Is.Not.Null);
            Assert.That(material.shader.name,Is.EqualTo("Little Castle/Rivers/Uber Water Adapted"));
            foreach(var property in new[]{"_Normal_Map","_SurfFoam_Map","_SL_Dissolve_Mask","_Caustics_Distortion_Map","_SurfaceDistortion_Map","_Caustics_Map"})
            {Assert.That(material.HasProperty(property),Is.True,property);Assert.That(material.GetTexture(property),Is.Not.Null,property);}
            foreach(var property in new[]{"_LC_RiverFlowMap","_LC_FlowBounds","_LC_UseFlowMap"})Assert.That(material.HasProperty(property),Is.True,property);
        }
        [Test] public void WaterCatalogOverride_RetainsLegacyRigidRocksAndStandaloneSectionsAcrossHistoryAndSave()
        {
            var waterCatalog=AssetDatabase.LoadAssetAtPath<MapRockCatalog>(CatalogPath);
            var legacyCatalog=MapRockCatalog.Default;Assert.That(legacyCatalog,Is.Not.Null);Assert.That(waterCatalog.entries,Is.Empty);
            var map=Map();var authored=new MapEditSession(map);authored.RegisterRockCatalog(legacyCatalog.Definitions);
            var river=River(1);foreach(var node in river.nodes)node.leftBank=node.rightBank=MapBankKind.Cliff;
            authored.UpsertRiver(river);string legacySaved=authored.ExportJson();
            var session=new MapEditSession(map);session.LoadJson(legacySaved);int legacyRocks=session.GetRiver(88).rocks.Count;
            Assert.That(legacyRocks,Is.GreaterThan(0));
            using(var view=new MapRiverPresentation(session,waterCatalog,true))
            {
                view.creationBudget=256;view.maximumRocks=256;view.Tick(Vector3.zero,true);
                Assert.That(view.WaterMaterial,Is.SameAs(waterCatalog.waterMaterial));
                Assert.That(view.ResidentRocks,Is.EqualTo(legacyRocks),"A water-material override must retain saved v1 rigid rocks.");
                foreach(var renderer in view.WaterRenderers)
                    Assert.That(renderer.transform.parent.Find("Bed, bank and earth shoulder").GetComponent<MeshRenderer>().sharedMaterial,Is.SameAs(legacyCatalog.bankMaterial));
                session.UpsertRockSection(new MapRockSection{id=990,points=new List<Vector2>{new Vector2(40,-30),new Vector2(40,0)}});
                var section=session.ExportRockSections()[0];int sectionRocks=section.rocks.Count;Assert.That(sectionRocks,Is.GreaterThan(0),"Fallback definitions must support the standalone authoring tool.");
                view.Tick(Vector3.zero,true);Assert.That(view.ResidentRocks,Is.EqualTo(legacyRocks+sectionRocks));
                string saved=session.ExportJson();section.rocks[0].manual=true;section.rocks[0].topOffset=.2f;session.UpsertRockSection(section);
                session.Undo();Assert.That(session.ExportJson(),Is.EqualTo(saved));
                session.DeleteRockSection(990);view.Tick(Vector3.zero,true);Assert.That(view.ResidentRocks,Is.EqualTo(legacyRocks));
                session.Undo();view.Tick(Vector3.zero,true);Assert.That(view.ResidentRocks,Is.EqualTo(legacyRocks+sectionRocks));
                session.Redo();view.Tick(Vector3.zero,true);Assert.That(view.ResidentRocks,Is.EqualTo(legacyRocks));
                session.Undo();Assert.That(session.ExportJson(),Is.EqualTo(saved));
                session.LoadJson(saved);Assert.That(session.ExportJson(),Is.EqualTo(saved));view.Tick(Vector3.zero,true);
                Assert.That(view.ResidentRocks,Is.EqualTo(legacyRocks+sectionRocks));
                var sand=River();sand.id=777;foreach(var node in sand.nodes)node.position+=new Vector2(78,0);
                session.UpsertRiver(sand);Assert.That(session.GetRiver(777).rocks,Is.Empty,"The fallback must not auto-populate new v2 sand rivers.");
                view.Tick(Vector3.zero,true);Assert.That(view.ResidentRocks,Is.EqualTo(legacyRocks+sectionRocks));
            }
        }
    }
}
