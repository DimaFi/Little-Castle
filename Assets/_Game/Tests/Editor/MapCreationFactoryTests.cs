using System;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class MapCreationFactoryTests
    {
        [TestCase(32,512)]
        [TestCase(33,65)]
        [TestCase(512,32)]
        public void FlatMapsUseOneMetreSparseLatticeAcrossInclusiveEdges(int width,int length)
        {
            var document=MapCreationFactory.Create(new MapCreationOptions{widthMetres=width,lengthMetres=length,baseHeight=3,minimumHeight=-8,maximumHeight=24});
            Assert.That(document.bounds,Is.EqualTo(new Vector4(-width*.5f,-length*.5f,width,length)));
            Assert.That(document.heightField.origin,Is.EqualTo(new Vector2(document.bounds.x,document.bounds.y)));
            Assert.That(document.heightField.spacing,Is.EqualTo(1));
            Assert.That(document.heightField.tiles,Is.Empty,"Flat map creation must not materialize dense height data.");
            var session=new MapEditSession(document);var b=session.Bounds;
            foreach(var point in new[]{new Vector2(b.x,b.y),new Vector2(b.x+b.z,b.y+b.w),Vector2.zero,new Vector2(b.x+.5f,b.y+.5f)})
            {
                Assert.That(session.SampleHeight(point.x,point.y),Is.EqualTo(3));
                Assert.That(session.SampleNormal(point.x,point.y),Is.EqualTo(Vector3.up));
            }
            Assert.That(session.CanUndo,Is.False);Assert.That(session.EditedHeightSamples,Is.Zero);
            Assert.That(document.surface.strokes,Is.Empty);Assert.That(document.trees,Is.Empty);
            Assert.That(document.rivers,Is.Empty);Assert.That(document.fences,Is.Empty);
        }

        [Test]
        public void NewMapsHaveFreshIdentityAndIndependentStateWithoutMutatingOptions()
        {
            var options=new MapCreationOptions();string before=JsonUtility.ToJson(options);
            var first=MapCreationFactory.Create(options);var second=MapCreationFactory.Create(options);
            Assert.That(first.mapId,Is.Not.EqualTo(second.mapId));
            Assert.That(first.mapId,Does.StartWith("map-"));
            Assert.That(JsonUtility.ToJson(options),Is.EqualTo(before));
            first.heightField.protectedAreas.Add(new Rect(-1,-1,2,2));first.environment.settings.snowAmount=.3f;
            Assert.That(second.heightField.protectedAreas,Is.Empty);
            Assert.That(second.environment.settings.snowAmount,Is.EqualTo(1));
            Assert.That(first.riverNetwork,Is.Not.SameAs(second.riverNetwork));
            foreach(var reference in typeof(MapCreationFactory).Assembly.GetReferencedAssemblies())
                Assert.That(reference.Name,Does.Not.StartWith("UnityEditor"));
        }

        [Test]
        public void FlatDocumentSupportsSculptUndoAndExistingSaveTransportWithoutChangingBase()
        {
            var document=MapCreationFactory.Create(new MapCreationOptions{widthMetres=64,lengthMetres=96,mapId="Новая-карта_42"});
            string before=JsonUtility.ToJson(document);var session=new MapEditSession(document);
            session.BeginHeightStroke(4,1);session.AppendHeightPoint(Vector2.zero);session.CommitStroke();
            Assert.That(session.SampleHeight(0,0),Is.GreaterThan(0));
            string save=session.ExportJson();float edited=session.SampleHeight(0,0);
            session.Undo();Assert.That(session.SampleHeight(0,0),Is.Zero);
            session.Redo();Assert.That(session.SampleHeight(0,0),Is.EqualTo(edited));
            var reopenedBase=JsonUtility.FromJson<MapAuthoringDocument>(before);var reopened=new MapEditSession(reopenedBase);
            reopened.LoadJson(save);Assert.That(reopened.SampleHeight(0,0),Is.EqualTo(edited));
            Assert.That(reopened.Bounds,Is.EqualTo(document.bounds));
            Assert.That(JsonUtility.ToJson(document),Is.EqualTo(before),"Session editing must not publish into the new document's base.");
        }

        [TestCase(31,128)]
        [TestCase(513,128)]
        [TestCase(128,0)]
        [TestCase(128,513)]
        public void InvalidDimensionsAreRejectedBeforeDocumentCreation(int width,int length)
        {
            var options=new MapCreationOptions{widthMetres=width,lengthMetres=length};
            Assert.That(MapCreationFactory.TryValidate(options,out string error),Is.False);Assert.That(error,Is.Not.Empty);
            Assert.Throws<ArgumentException>(()=>MapCreationFactory.Create(options));
        }

        [TestCase(float.NaN,-4,12)]
        [TestCase(float.PositiveInfinity,-4,12)]
        [TestCase(0,float.NegativeInfinity,12)]
        [TestCase(0,-4,float.NaN)]
        [TestCase(0,4,4)]
        [TestCase(0,5,-5)]
        [TestCase(13,-4,12)]
        public void InvalidHeightSettingsAreRejected(float height,float minimum,float maximum)
        {
            var options=new MapCreationOptions{baseHeight=height,minimumHeight=minimum,maximumHeight=maximum};
            Assert.That(MapCreationFactory.TryValidate(options,out _),Is.False);
            Assert.Throws<ArgumentException>(()=>MapCreationFactory.Create(options));
        }

        [TestCase(" ")]
        [TestCase("../map")]
        [TestCase("map/name")]
        [TestCase("map\\name")]
        [TestCase("map.id")]
        [TestCase("_map")]
        public void UnsafeExplicitIdentityIsRejected(string id)
        {Assert.That(MapCreationFactory.TryValidate(new MapCreationOptions{mapId=id},out _),Is.False);}

        [Test]
        public void DuplicateLibraryIdsAndOverlongIdsAreRejectedAndGeneratedAllocationIsBounded()
        {
            var options=new MapCreationOptions{mapId="existing"};
            Assert.That(MapCreationFactory.TryValidate(options,out _,id=>id=="existing"),Is.False);
            Assert.Throws<ArgumentException>(()=>MapCreationFactory.Create(options,id=>id=="existing"));
            options.mapId=new string('a',65);Assert.That(MapCreationFactory.TryValidate(options,out _),Is.False);
            options.mapId="";int attempts=0;
            Assert.That(MapCreationFactory.TryValidate(options,out _,id=>throw new Exception("Validation must not allocate an ID")),Is.True);
            Assert.Throws<InvalidOperationException>(()=>MapCreationFactory.Create(options,id=>{attempts++;return true;}));
            Assert.That(attempts,Is.EqualTo(32),"A broken/full caller registry must not spin forever.");
            Assert.That(MapCreationFactory.TryValidate(null,out _),Is.False);
            Assert.Throws<ArgumentNullException>(()=>MapCreationFactory.Create(null));
        }
    }
}
