using System;
using System.Collections.Generic;
using System.Linq;
using LittleCastle.Rendering;
using LittleCastle.World;
using LittleCastle.World.Environment;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class MapPathMaterialTests
    {
        static MapAuthoringDocument Map()=>new MapAuthoringDocument{mapId="path-test",bounds=new Vector4(-64,-64,128,128)};
        static SurfacePaintStroke Stroke(string id,string material,Vector2 center,float radius=3)=>new SurfacePaintStroke
        {id=id,pathMaterialId=material,radius=radius,brushSoftness=.601f,points=new List<Vector2>{center}};
        static MapPathCatalog Catalog()
        {
            var c=ScriptableObject.CreateInstance<MapPathCatalog>();
            c.entries=new[]{new MapPathCatalog.Entry{id=MapPathCatalog.LegacyId},new MapPathCatalog.Entry{id="cobble.1"},new MapPathCatalog.Entry{id="ground.2"}};return c;
        }
        static byte[][] Raster(MapPathCatalog c,Vector4 b,int size,List<SurfacePaintStroke> strokes)
        {
            var data=new byte[MapPathRasterizer.SliceCount(c.Count)][];
            for(int i=0;i<data.Length;i++)data[i]=new byte[size*size*4];
            MapPathRasterizer.Rebuild(data,b,size,size,strokes,c,new RectInt(0,0,size,size));return data;
        }
        [Test] public void SurfaceIdSurvivesSaveLoadUndoRedoAndBiomeReplacement()
        {
            var map=Map();var s=new MapEditSession(map);s.AddStroke(Stroke("a","ground.2",new Vector2(-2,-2)));
            s.Undo();Assert.That(s.ExportEdits().strokes,Is.Empty);s.Redo();
            s.BeginEnvironmentStroke(EnvironmentThemeIds.Winter);s.ApplyEnvironmentBrush(new Vector2(-2,-2),6,1,.5f);s.CommitStroke();
            var restored=new MapEditSession(map);restored.LoadJson(s.ExportJson());
            Assert.That(restored.ExportEdits().strokes.Single().pathMaterialId,Is.EqualTo("ground.2"));
            Assert.That(restored.EnvironmentThemes.Sample(EnvironmentThemeIds.Winter,-2,-2),Is.GreaterThan(.5f));
        }
        [Test] public void OldJsonAndUnknownFutureMaterialsRemainLoadable()
        {
            var s=new MapEditSession(Map());s.AddStroke(Stroke("a","future.obsidian",Vector2.zero));
            var restored=new MapEditSession(Map());restored.LoadJson(s.ExportJson());
            Assert.That(restored.ExportEdits().strokes[0].pathMaterialId,Is.EqualTo("future.obsidian"));
            var c=Catalog();try{Assert.That(c.IndexOf(null),Is.Zero);Assert.That(c.IndexOf("future.obsidian"),Is.Zero);}finally{Object.DestroyImmediate(c);}
        }
        [Test] public void PaintingAnotherMaterialKeepsBothAtFeatheredIntersection()
        {
            var c=Catalog();try
            {
                var a=Stroke("a","cobble.1",Vector2.zero,6);var b=Stroke("b","ground.2",new Vector2(2,0),3);
                var data=Raster(c,new Vector4(-8,-8,16,16),64,new List<SurfacePaintStroke>{a,b});
                int center=(32*64+40)*4;Assert.That(data[0][center+2],Is.EqualTo(255));Assert.That(data[0][center+1],Is.Zero);
                Assert.That(Enumerable.Range(0,4096).Any(p=>data[0][p*4+1]>20&&data[0][p*4+2]>20),Is.True);
            }finally{Object.DestroyImmediate(c);}
        }
        [Test] public void ExtraPointsWithinOneStrokeDoNotHardenMaterialEdges()
        {
            var c=Catalog();try
            {
                var stroke=Stroke("a","cobble.1",Vector2.zero);
                var single=Raster(c,new Vector4(-4,-4,8,8),32,new List<SurfacePaintStroke>{stroke});
                for(int i=0;i<40;i++)stroke.points.Add(Vector2.zero);
                var many=Raster(c,new Vector4(-4,-4,8,8),32,new List<SurfacePaintStroke>{stroke});
                CollectionAssert.AreEqual(single[0],many[0]);
            }finally{Object.DestroyImmediate(c);}
        }
        [Test] public void WorldSpaceWeightsMatchAcrossNegativeChunkBorder()
        {
            var c=Catalog();try
            {
                var strokes=new List<SurfacePaintStroke>{Stroke("a","cobble.1",new Vector2(-.1f,-4),5)};
                // Two 32 m tiles with a one-pixel gutter, identical sample at the shared border.
                var left=Raster(c,new Vector4(-33,-33,34,34),34,strokes);
                var right=Raster(c,new Vector4(-1,-33,34,34),34,strokes);
                for(int y=0;y<34;y++)for(int layer=0;layer<4;layer++)
                    Assert.That(left[0][(y*34+32)*4+layer],Is.EqualTo(right[0][y*34*4+layer]));
            }finally{Object.DestroyImmediate(c);}
        }
        [Test] public void GrassEraseKeepsPathButMeadowReplacesItsMaterial()
        {
            var c=Catalog();try
            {
                var a=Stroke("a","cobble.1",Vector2.zero);var erase=Stroke("b",null,Vector2.zero);erase.road=false;erase.erase=true;
                var data=Raster(c,new Vector4(-4,-4,8,8),16,new List<SurfacePaintStroke>{a,erase});int k=(8*16+8)*4+1;
                Assert.That(data[0][k],Is.EqualTo(255));erase.erase=false;
                data=Raster(c,new Vector4(-4,-4,8,8),16,new List<SurfacePaintStroke>{a,erase});Assert.That(data[0][k],Is.Zero);
            }finally{Object.DestroyImmediate(c);}
        }
        [Test] public void CatalogReorderingResolvesSavedIdsAndSupportsAdditionalWeightSlices()
        {
            var c=Catalog();try
            {
                Array.Reverse(c.entries,1,2);Assert.That(c.IndexOf("cobble.1"),Is.EqualTo(2));
                var all=new MapPathCatalog.Entry[12];for(int i=0;i<all.Length;i++)all[i]=new MapPathCatalog.Entry{id="test."+i};c.entries=all;
                var data=Raster(c,new Vector4(-4,-4,8,8),16,new List<SurfacePaintStroke>{Stroke("a","test.11",Vector2.zero)});
                Assert.That(data.Length,Is.EqualTo(3));Assert.That(data[2][(8*16+8)*4+3],Is.EqualTo(255));
            }finally{Object.DestroyImmediate(c);}
        }
        [Test] public void PaletteContainsAllRequestedLocalMaterials()
        {
            var c=MapPathCatalog.Default;Assert.That(c,Is.Not.Null);Assert.That(c.Count,Is.GreaterThanOrEqualTo(10));
            foreach(string id in new[]{"ground.2","ground.5","cobble.1","cobble.2","cobble.3","cobble.4","cobble.5","cobble.6","cobble.7"})
            {int index=c.IndexOf(id);Assert.That(index,Is.GreaterThan(0),id);Assert.That(c.entries[index].thumbnail,Is.Not.Null,id);}
            Assert.That(c.albedo.depth,Is.EqualTo(c.Count));Assert.That(c.normal.depth,Is.EqualTo(c.Count));Assert.That(c.surface.depth,Is.EqualTo(c.Count));
        }
        [Test] public void HiddenVisibleTerrainChunksReceivePathBindingsAndRestoreThem()
        {
            var material=new Material(Shader.Find("Little Castle/Terrain/LC Terrain"));
            var previewScene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var chunk=new GameObject("Derived hidden terrain test"){hideFlags=HideFlags.HideAndDontSave};
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chunk,previewScene);
            try
            {
                var renderer=chunk.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                using(var view=new MapPathPresentation(new MapEditSession(Map()),material,16))
                {
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                    Assert.That(block.GetTexture("_LC_PathWeights"),Is.Not.Null,"The visible HideAndDontSave chunk must receive the material weights.");
                    Assert.That(block.GetFloat("_LC_PathCount"),Is.GreaterThan(1));
                }
                var restored=new MaterialPropertyBlock();renderer.GetPropertyBlock(restored);Assert.That(restored.GetTexture("_LC_PathWeights"),Is.Null);
            }
            finally{Object.DestroyImmediate(chunk);Object.DestroyImmediate(material);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene);}
        }
    }
}
