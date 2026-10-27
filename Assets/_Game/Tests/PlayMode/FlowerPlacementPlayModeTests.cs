using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LittleCastle.Tests
{
    public sealed class FlowerPlacementPlayModeTests
    {
        [UnityTest] public IEnumerator Flowers_RealModelsRespectSlopeRoadsBuildingsAndReplay()
        {
#if UNITY_EDITOR
            var root=new GameObject("Quaternius flower placement acceptance");
            var data=ScriptableObject.CreateInstance<SurfacePaintData>();data.mapId="quaternius-verification";data.bounds=new Vector4(-24,-24,48,48);
            data.hasHeightBase=true;data.heightField=new MapHeightFieldData{minimumHeight=-10,maximumHeight=40};
            data.placementExclusions.Add(new Rect(-3,-3,6,6));
            for(int z=-24;z<=24;z++)for(int x=-24;x<=24;x++)MapHeightField.SetBase(data.heightField,x,z,x>8?(x-8)*.9f:.08f*x+.3f*Mathf.Sin(z*.2f));
            var source=AssetDatabase.LoadAssetAtPath<MeadowScatterLibrary>("Assets/_Game/Art/Studies/Meadow_20261025/Meadow_ScatterLibrary.asset");
            var library=Object.Instantiate(source);
            foreach(var p in library.prototypes){p.enabled=p.stableId.Contains("Flower_")&&!p.stableId.Contains("Group");p.allowedBiomes=BiomeMask.TemperateGrassland;}
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                var painter=root.AddComponent<MeadowSurfacePainter>();painter.source=data;painter.brushMaterial=material;
                var renderer=root.AddComponent<MeadowScatterRenderer>();renderer.library=library;renderer.candidateSpacing=.8f;renderer.clearingChance=1;
                renderer.densityMultiplier=2;renderer.painter=painter;painter.scatterRenderer=renderer;renderer.biomeSampler=(x,z)=>BiomeKind.TemperateGrassland;
                yield return null;
                painter.AddStroke(new SurfacePaintStroke{id="flower-clearing",road=false,scatter=true,scatterProfile=MeadowScatterProfile.Flowers,radius=8,points=new List<Vector2>{new Vector2(-18,-14),new Vector2(18,-14),new Vector2(18,0),new Vector2(-18,0),new Vector2(-18,14),new Vector2(18,14)}});
                painter.AddStroke(new SurfacePaintStroke{id="road",road=true,radius=1.5f,points=new List<Vector2>{new Vector2(-23,6),new Vector2(23,6)}});
                Assert.That(renderer.authoredInstances,Is.GreaterThan(30));
                var positions=Positions(renderer);
                foreach(var p in positions)
                {
                    Assert.That(painter.Session.VegetationAllowed(p.x,p.z),Is.True);
                    Assert.That(painter.SampleRoad01(p.x,p.z),Is.LessThan(.15f));
                    // The transition cell [8,9] is partly gentle; the steep interior starts at 9.
                    Assert.That(p.x,Is.LessThan(9),"Steep hill must reject flowers.");
                    Assert.That(p.y,Is.EqualTo(painter.Session.SampleHeight(p.x,p.z)-.004f).Within(.001f));
                }
                for(int a=0;a<positions.Count;a++)for(int b=a+1;b<positions.Count;b++)Assert.That(Vector2.Distance(new Vector2(positions[a].x,positions[a].z),new Vector2(positions[b].x,positions[b].z)),Is.GreaterThanOrEqualTo(renderer.minimumSeparation-.001f));
                ulong digest=renderer.placementDigest;string save=painter.ExportJson();renderer.Rebuild(painter);Assert.That(renderer.placementDigest,Is.EqualTo(digest));
                painter.ResetEdits();painter.LoadJson(save);Assert.That(renderer.placementDigest,Is.EqualTo(digest));
                painter.AddStroke(new SurfacePaintStroke{id="undo-road",road=true,radius=3,points=new List<Vector2>{new Vector2(-10,-10)}});
                ulong roadDigest=renderer.placementDigest;painter.Undo();Assert.That(renderer.placementDigest,Is.EqualTo(digest));painter.Redo();Assert.That(renderer.placementDigest,Is.EqualTo(roadDigest));painter.Undo();
                renderer.seed++;renderer.Rebuild(painter);Assert.That(renderer.placementDigest,Is.Not.EqualTo(digest));renderer.seed--;
                renderer.biomeSampler=(x,z)=>BiomeKind.RockyHighland;renderer.Rebuild(painter);Assert.That(renderer.authoredInstances,Is.Zero);
                renderer.biomeSampler=(x,z)=>BiomeKind.TemperateGrassland;renderer.Rebuild(painter);Assert.That(renderer.placementDigest,Is.EqualTo(digest));
                string output=Path.GetFullPath(Application.dataPath+"/../../Research/Flowers_20261026");Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output,"placement-acceptance.txt"),$"PASS actual Quaternius models: {positions.Count} placements; root height, slope, road, reserved building, minimum spacing, seed, full rebuild, save/load, Undo/Redo and biome filtering.\n");
                var profiles=new[]{MeadowScatterProfile.MeadowGrass,MeadowScatterProfile.Stones,MeadowScatterProfile.Understory};
                var categories=new[]{MeadowScatterCategory.Grass,MeadowScatterCategory.Stone,MeadowScatterCategory.Shrub};
                for(int group=0;group<profiles.Length;group++)
                {
                    foreach(var p in library.prototypes)p.enabled=p.category==categories[group];
                    // Library configuration changes require the same explicit refresh offered in Inspector.
                    renderer.RebuildCurrent();
                    painter.AddStroke(new SurfacePaintStroke{id="group-test-"+group,road=false,scatter=true,scatterProfile=profiles[group],radius=8,points=new List<Vector2>{new Vector2(-12,-10)}});
                    int count=(int)typeof(MeadowScatterRenderer).GetMethod("CountCategoryInCircle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(renderer,new object[]{categories[group],new Vector2(-12,-10),6f});
                    Assert.That(count,Is.GreaterThan(0),"Brush group "+profiles[group]);
                    string grouped=painter.ExportJson();ulong groupedDigest=renderer.placementDigest;painter.ResetEdits();painter.LoadJson(grouped);Assert.That(renderer.placementDigest,Is.EqualTo(groupedDigest));
                }
                File.AppendAllText(Path.Combine(output,"placement-acceptance.txt"),"PASS grass/clover, stones and understory brush groups place real models and replay after save/load.\n");
            }
            finally{Object.Destroy(root);Object.Destroy(data);Object.Destroy(library);Object.Destroy(material);}
            yield return null;
#else
            yield return null;
#endif
        }
        static List<Vector3> Positions(MeadowScatterRenderer renderer)
        {
            var result=new List<Vector3>();
            var regions=(IDictionary)typeof(MeadowScatterRenderer).GetField("regionPlacements",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
            foreach(var values in regions.Values)foreach(var p in (IEnumerable)values)result.Add((Vector3)p.GetType().GetField("position").GetValue(p));
            return result;
        }
    }
}
