using System.Collections.Generic;
using System.Linq;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class RiverShoreDecorationTests
    {
        MeadowScatterLibrary library;
        Mesh mesh;
        Material material;

        [SetUp] public void SetUp()
        {
            mesh=new Mesh();
            mesh.vertices=new[]{Vector3.zero,Vector3.right*.2f,Vector3.forward*.2f,Vector3.up*.15f};
            mesh.triangles=new[]{0,2,1,0,1,3,1,2,3,2,0,3};mesh.RecalculateBounds();
            material=new Material(Shader.Find("Hidden/InternalErrorShader"));
            library=ScriptableObject.CreateInstance<MeadowScatterLibrary>();
            library.prototypes=new[]{new MeadowScatterLibrary.Prototype{
                stableId="shore.test.stone",category=MeadowScatterCategory.Stone,
                lodMeshes=new[]{mesh,mesh,mesh,mesh},materials=new[]{material},localBounds=mesh.bounds,
                maximumSlope=60,scaleRange=Vector2.one}};
        }
        [TearDown] public void TearDown()
        {Object.DestroyImmediate(library);Object.DestroyImmediate(material);Object.DestroyImmediate(mesh);}

        static MapEditSession Session()
        {
            var session=new MapEditSession(new MapAuthoringDocument{mapId="derived-shore-tests",
                bounds=new Vector4(-96,-96,192,192),heightField=new MapHeightFieldData()});
            session.UpsertRiver(new MapRiverRecord{id=82,seed=417,layoutVersion=2,nodes=new List<MapRiverNode>{
                new MapRiverNode{id=1,position=new Vector2(-1,-60),width=5,leftTransition=8,rightTransition=8,leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand},
                new MapRiverNode{id=2,position=new Vector2(-1,60),width=5,leftTransition=8,rightTransition=8,leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand}}});
            return session;
        }
        RiverShoreDecorationPresentation View(MapEditSession session)=>new RiverShoreDecorationPresentation(session,library){
            maximumDistance=48,maximumRegions=36,rebuildBudget=81,density=1,candidateSpacing=.75f,
            maximumInstancesPerRegion=256,shoreInset=.05f};

        [Test] public void RebuildAndEvictionPreserveIdsAcrossNegativeCoordinatesAndRegionSeams()
        {
            var session=Session();string save=session.ExportJson();
            using(var view=View(session))
            {
                view.Tick(Vector3.zero);
                var first=view.Placements.ToArray();
                Assert.That(first.Length,Is.GreaterThan(0));
                Assert.That(first.Any(p=>p.worldPosition.x<0&&p.worldPosition.z<0),Is.True);
                Assert.That(first.Select(p=>p.stableId).Distinct().Count(),Is.EqualTo(first.Length));
                view.Tick(new Vector3(90,0,90));view.Tick(Vector3.zero);
                CollectionAssert.AreEqual(first,view.Placements);
                view.Tick(Vector3.zero,true);CollectionAssert.AreEqual(first,view.Placements);
                Assert.That(session.ExportJson(),Is.EqualTo(save));
                Assert.That(view.SourceLibrary,Is.SameAs(library));
            }
        }

        [Test] public void RiverDeleteClearsImmediatelyAndUndoRestoresDerivedStones()
        {
            var session=Session();
            using(var view=View(session))
            {
                view.Tick(Vector3.zero);var first=view.Placements.ToArray();Assert.That(first,Is.Not.Empty);
                session.DeleteRiver(82);Assert.That(view.ResidentInstances,Is.Zero);
                view.Tick(Vector3.zero);Assert.That(view.ResidentInstances,Is.Zero);
                session.Undo();view.Tick(Vector3.zero);CollectionAssert.AreEqual(first,view.Placements);
                Assert.That(session.GetRiver(82).rocks,Is.Empty);
            }
        }

        [Test] public void DryFootprintsAndPaintedPathStayClearAndPathUndoRestoresPlacements()
        {
            var session=Session();
            using(var view=View(session))
            {
                view.Tick(Vector3.zero);var first=view.Placements.ToArray();Assert.That(first,Is.Not.Empty);
                Assert.That(first.Any(p=>Mathf.Abs(p.worldPosition.z)<4),Is.True);
                session.AddStroke(new SurfacePaintStroke{id="shore-path",road=true,radius=4,strength=1,
                    points=new List<Vector2>{new Vector2(-20,0),new Vector2(20,0)}});
                view.Tick(Vector3.zero);
                Assert.That(view.ResidentInstances,Is.GreaterThan(0));
                foreach(var p in view.Placements)
                {
                    Assert.That(Mathf.Abs(p.worldPosition.z),Is.GreaterThan(3));
                    float radius=library.prototypes[0].localBounds.extents.magnitude*p.uniformScale+view.shoreInset;
                    for(int i=0;i<8;i++)
                    {
                        var point=new Vector2(p.worldPosition.x+Mathf.Cos(i*Mathf.PI/4)*radius,
                            p.worldPosition.z+Mathf.Sin(i*Mathf.PI/4)*radius);
                        Assert.That(session.QueryWater(point,out _,out _,out _),Is.False);
                        Assert.That(session.SandCoverage(point.x,point.y),Is.GreaterThanOrEqualTo(view.minimumSandCoverage));
                    }
                }
                session.Undo();view.Tick(Vector3.zero);CollectionAssert.AreEqual(first,view.Placements);
            }
        }

        [Test] public void OffscreenSurfaceEditDoesNotRebuildResidentRegionsAndBudgetIsBounded()
        {
            var session=Session();
            using(var view=View(session))
            {
                view.maximumRegions=4;view.rebuildBudget=2;view.maximumDistance=12;
                view.Tick(Vector3.zero);Assert.That(view.LastRebuiltRegions,Is.EqualTo(2));
                view.Tick(Vector3.zero);Assert.That(view.ResidentRegions,Is.LessThanOrEqualTo(4));
                view.Tick(Vector3.zero);Assert.That(view.LastRebuiltRegions,Is.Zero);
                session.AddStroke(new SurfacePaintStroke{id="far-path",road=true,radius=2,strength=1,
                    points=new List<Vector2>{new Vector2(85,85)}});
                view.Tick(Vector3.zero);Assert.That(view.LastRebuiltRegions,Is.Zero);
                Assert.That(view.ResidentInstances,Is.LessThanOrEqualTo(4*view.maximumInstancesPerRegion));
            }
        }
    }
}
