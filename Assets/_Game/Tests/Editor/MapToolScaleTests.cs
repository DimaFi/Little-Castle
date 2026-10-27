using System.Collections.Generic;
using System.Diagnostics;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapToolScaleTests
    {
        [Test] public void MapTools_4096ObjectsKeepIDsAndLocalInvalidationAcrossHeightUndo()
        {
            var map=new MapAuthoringDocument{mapId="object-scale",bounds=new Vector4(-512,-512,1024,1024),heightField=new MapHeightFieldData()};
            var definition=new MapTreeDefinition{archetypeId="scale.oak"};
            for(int z=0;z<64;z++)for(int x=0;x<64;x++)map.trees.Add(new MapTreeInstance{stableId=z*64+x+1,archetypeId=definition.archetypeId,catalogVersion=1,position=new Vector2(-500+x*16,-500+z*16),uniformScale=1,clearanceRadius=2});
            var s=new MapEditSession(map);s.RegisterTreeCatalog(new[]{definition},new MapTreePack[0]);
            int events=0;s.Changed+=change=>{Assert.That(change.bounds.width,Is.LessThanOrEqualTo(32));events++;};
            var c=new MapToolController(s,MapToolRegistry.ExistingBrushes());c.Select("terrain.raise");c.context.settings.radius=12;
            var watch=Stopwatch.StartNew();c.Begin(new MapToolPointer(default,true,Vector2.zero),0);c.Release();watch.Stop();
            Assert.That(s.TreeCount,Is.EqualTo(4096));Assert.That(events,Is.LessThanOrEqualTo(8));
            s.Undo();s.Redo();Assert.That(s.TreeCount,Is.EqualTo(4096));Assert.That(s.GetTree(4096).stableId,Is.EqualTo(4096));
            TestContext.Out.WriteLine("4096 logical tree records; local radius 12 m command: "+watch.Elapsed.TotalMilliseconds.ToString("F3")+" ms (Editor, no renderer). No full forest rendering claim.");
        }
        [Test] public void MapTools_ThinDiagonalStrokeBatchesDoNotNotifyUnvisitedFarChunksDuringPreview()
        {
            var s=new MapEditSession(new MapAuthoringDocument{mapId="diagonal",bounds=new Vector4(-256,-256,512,512)});
            var changes=new List<MapRegionChange>();s.Changed+=changes.Add;
            var c=new MapToolController(s,MapToolRegistry.ExistingBrushes());c.Select("surface.road");c.context.settings.radius=1;
            c.Begin(new MapToolPointer(default,true,new Vector2(-100,-100)),0);changes.Clear();
            c.Move(new MapToolPointer(default,true,new Vector2(100,100)));c.Tick(1);
            foreach(var change in changes)Assert.That(Mathf.Abs(change.bounds.center.x-change.bounds.center.y),Is.LessThan(34));
            c.Cancel();
        }
        [Test] public void MapTools_DiagonalCommitUndoRedoAndCancelNotifyCorridorOnly()
        {
            var s=new MapEditSession(new MapAuthoringDocument{mapId="diagonal-history",bounds=new Vector4(-256,-256,512,512)});
            var c=new MapToolController(s,MapToolRegistry.ExistingBrushes());c.Select("surface.road");c.context.settings.radius=2;
            s.Changed+=change=>Assert.That(Mathf.Abs(change.bounds.center.x-change.bounds.center.y),Is.LessThan(34),"History/cancel must not expand a thin corridor to its complete rectangle");
            c.Begin(new MapToolPointer(default,true,new Vector2(-100,-100)),0);c.Move(new MapToolPointer(default,true,new Vector2(100,100)));c.Complete();
            c.Undo();c.Redo();c.Begin(new MapToolPointer(default,true,new Vector2(-100,-100)),0);c.Move(new MapToolPointer(default,true,new Vector2(100,100)));c.Tick(1);c.Cancel();
        }
    }
}
