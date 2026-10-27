using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class FlowerGenerationTests
    {
        readonly List<Object> owned=new List<Object>();
        T Own<T>(T o) where T:Object{owned.Add(o);return o;}
        [TearDown] public void Cleanup(){foreach(var o in owned)if(o)Object.DestroyImmediate(o);owned.Clear();}
        WorldGenerationSettings Settings()
        {var s=Own(ScriptableObject.CreateInstance<WorldGenerationSettings>());Set(s,"cellsPerSide",8);Set(s,"chunkWorldSize",8f);return s;}
        static void Set(Object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
        static WorldChunkData Chunk(ChunkCoordinate coord)
        {var c=new WorldChunkData(coord,8);for(int z=0;z<8;z++)for(int x=0;x<8;x++){c.SetSurface(x,z,SurfaceKind.Grass);c.SetBiome(x,z,BiomeKind.TemperateGrassland);c.SetTerrainClass(x,z,TerrainClass.Plains);}return c;}
        [Test] public void Flowers_HeightAndSlopeMatchActualTerrainTrianglesIncludingNegativeChunks()
        {
            var settings=Settings();var c=Chunk(new ChunkCoordinate(-1,-1));
            for(int z=0;z<=8;z++)for(int x=0;x<=8;x++)c.SetHeight(x,z,Mathf.Sin(x*1.1f)*2+Mathf.Cos(z*.8f)*x*.3f);
            var mesh=Own(ChunkMeshBuilder.Build(c,8));var go=Own(new GameObject("Actual terrain triangles"));go.transform.position=new Vector3(-8,0,-8);
            var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();
            for(float z=-7.83f;z<-.1f;z+=.43f)for(float x=-7.89f;x<-.1f;x+=.37f)
            {
                Assert.That(collider.Raycast(new Ray(new Vector3(x,50,z),Vector3.down),out var hit,100),Is.True);
                Assert.That(WorldChunkSampling.SampleHeight(c,settings,x,z),Is.EqualTo(hit.point.y).Within(.0001f));
                var triangles=mesh.triangles;var vertices=mesh.vertices;int t=hit.triangleIndex*3;
                var face=Vector3.Cross(vertices[triangles[t+1]]-vertices[triangles[t]],vertices[triangles[t+2]]-vertices[triangles[t]]).normalized;
                float diagonal=(x+8-Mathf.Floor(x+8))+(z+8-Mathf.Floor(z+8));
                // An exact shared edge belongs to either face in the collider BVH.
                if(Mathf.Abs(diagonal-1)>.0001f)Assert.That(Vector3.Angle(WorldChunkSampling.SampleTriangleNormal(c,settings,x,z),face),Is.LessThan(.05f),$"at {x},{z}, triangle {hit.triangleIndex}");
            }
        }
        [Test] public void Flowers_ReplaySeedExclusionsZeroChanceAndBudget()
        {
            var settings=Settings();var stage=Own(ScriptableObject.CreateInstance<ObjectScatterStage>());
            var rule=new ScatterSpawnRule{ruleId="flowers",archetypeId="quaternius.Flower_3_Single",flowerMeadow=true,spacing=.5f,
                baseChance=1,isolatedDensity=1,clearingDensity=1,multiplyByForestDensity=false,maxSlope=25,maximumFlowersPerChunk=35,
                flowerVariants=new[]{"quaternius.Flower_3_Single","quaternius.Flower_4_Single"},flowerWeights=new[]{1f,1f}};
            Set(stage,"rules",new List<ScatterSpawnRule>{rule});
            WorldChunkData Generate(int seed)
            {
                var c=Chunk(new ChunkCoordinate(-1,0));
                for(int z=0;z<8;z++)for(int x=0;x<8;x++)
                {if(x==2)c.SetSurface(x,z,SurfaceKind.DirtRoad);if(x==3)c.AddPlacementBlocks(x,z,PlacementBlockFlags.All);if(x==4)c.SetSurface(x,z,SurfaceKind.Riverbed);}
                stage.Generate(new GenerationContext(seed,settings),c);return c;
            }
            var a=Generate(71);var b=Generate(71);Assert.That(a.Spawns.Count,Is.EqualTo(35));
            Assert.That(a.Spawns.Select(s=>s.stableId),Is.EqualTo(b.Spawns.Select(s=>s.stableId)));
            Assert.That(a.Spawns.Select(s=>s.worldPosition),Is.EqualTo(b.Spawns.Select(s=>s.worldPosition)));
            Assert.That(a.Spawns.Select(s=>s.stableId),Is.Not.EqualTo(Generate(72).Spawns.Select(s=>s.stableId)));
            foreach(var s in a.Spawns){int cell=Mathf.FloorToInt(s.worldPosition.x+8);Assert.That(cell==2||cell==3||cell==4,Is.False);}
            rule.baseChance=0;Assert.That(Generate(71).Spawns,Is.Empty);rule.enabled=false;rule.baseChance=1;Assert.That(Generate(71).Spawns,Is.Empty);
        }
        [Test] public void Flowers_ActualQuaterniusAdaptersAndChunkReloadUseOneInstancedPresenter()
        {
            var library=AssetDatabase.LoadAssetAtPath<MeadowScatterLibrary>("Assets/_Game/Art/QuaterniusMeadow/Quaternius_MeadowLibrary.asset");Assert.That(library,Is.Not.Null);
            foreach(var p in library.prototypes)
            {Assert.That(p.localBounds.min.y,Is.EqualTo(0).Within(.0001f));foreach(var m in p.materials){Assert.That(m.enableInstancing,Is.True);Assert.That(m.shader.isSupported,Is.True);}}
            var catalog=Own(ScriptableObject.CreateInstance<WorldSpawnCatalog>());catalog.groundCoverLibrary=library;
            var c=Chunk(new ChunkCoordinate(0,0));for(int i=0;i<50;i++)c.AddSpawn(new WorldSpawnData(i+1,"quaternius.Flower_3_Single",SpawnCategory.Decoration,new Vector3(i%8,0,i/8),i*30,1));
            var root=Own(new GameObject("Chunk flower presentation"));Assert.That(ChunkSpawnPresenter.Populate(root.transform,c,catalog,Vector3.zero),Is.EqualTo(50));
            var presenter=root.GetComponent<MeadowScatterRenderer>();ulong before=presenter.placementDigest;
            ChunkSpawnPresenter.Populate(root.transform,c,catalog,Vector3.zero);
            Assert.That(root.transform.childCount,Is.Zero);Assert.That(root.GetComponents<MeadowScatterRenderer>().Length,Is.EqualTo(1));Assert.That(presenter.authoredInstances,Is.EqualTo(50));Assert.That(presenter.placementDigest,Is.EqualTo(before));
        }
        [Test] public void Flowers_ReservedBuildingFootprintIsSharedWithVegetationAndTrees()
        {
            var map=new MapAuthoringDocument{mapId="flower-building",bounds=new Vector4(-16,-16,32,32)};map.placementExclusions.Add(new Rect(-2,-2,4,4));
            var session=new MapEditSession(map);Assert.That(session.VegetationAllowed(0,0),Is.False);Assert.That(session.VegetationAllowed(2.1f,0,.3f),Is.False);Assert.That(session.VegetationAllowed(3,0,.3f),Is.True);
        }
        [Test] public void Flowers_BrushGroupsPreserveOldProfileIdsAndRoundTripNewGroups()
        {
            Assert.That((int)MeadowScatterProfile.Mixed,Is.Zero);Assert.That((int)MeadowScatterProfile.Flowers,Is.EqualTo(1));Assert.That((int)MeadowScatterProfile.ForestFloor,Is.EqualTo(2));
            var map=new MapAuthoringDocument{mapId="flower-groups",bounds=new Vector4(-16,-16,32,32)};
            var session=new MapEditSession(map);
            foreach(var profile in new[]{MeadowScatterProfile.MeadowGrass,MeadowScatterProfile.Stones,MeadowScatterProfile.Understory})
                session.AddStroke(new SurfacePaintStroke{id="group-"+profile,road=false,scatter=true,scatterProfile=profile,radius=2,points=new List<Vector2>{Vector2.zero}});
            var restored=new MapEditSession(map);restored.LoadJson(session.ExportJson());Assert.That(restored.ExportJson(),Is.EqualTo(session.ExportJson()));
            Assert.That(MeadowScatterRenderer.ProfileWeight(MeadowScatterProfile.MeadowGrass,MeadowScatterCategory.Grass),Is.EqualTo(1));
            Assert.That(MeadowScatterRenderer.ProfileWeight(MeadowScatterProfile.Stones,MeadowScatterCategory.Flower),Is.Zero);
            var trees=MapTreeCatalog.Default;var indices=new HashSet<int>();
            for(int group=0;group<MapTreeCatalog.GroupNames.Length;group++)
            {var members=trees.GroupIndices(group);Assert.That(members.Count,Is.GreaterThan(0));foreach(int member in members)Assert.That(indices.Add(member),Is.True);}
            Assert.That(indices.Count,Is.EqualTo(trees.entries.Length));
            var shared=AssetDatabase.LoadAssetAtPath<MeadowScatterLibrary>("Assets/_Game/Art/Studies/Meadow_20261025/Meadow_ScatterLibrary.asset");
            Assert.That(shared.prototypes.Count(p=>p.category==MeadowScatterCategory.Grass),Is.EqualTo(6));
            Assert.That(shared.prototypes.Any(p=>p.stableId=="quaternius.Bush_Common"&&p.category==MeadowScatterCategory.Shrub),Is.True);
            Assert.That(shared.prototypes.Any(p=>p.stableId=="quaternius.Rock_Medium_3"&&p.category==MeadowScatterCategory.Stone),Is.True);
        }
    }
}
