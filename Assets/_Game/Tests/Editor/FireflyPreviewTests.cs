using System;
using System.Collections.Generic;
using LittleCastle.Rendering.Fireflies;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class FireflyPreviewTests
    {
        FireflyPreviewProfile profile;

        [SetUp]
        public void Setup()
        {
            profile=ScriptableObject.CreateInstance<FireflyPreviewProfile>();
            profile.mapBounds=new Vector4(-64,-64,128,128);
            profile.densityPer100SquareMetres=8f;
        }

        [TearDown]
        public void TearDown()
        {
            if(profile)UnityEngine.Object.DestroyImmediate(profile);
        }

        static FireflyPreviewZone Zone(string id,int priority,
            FireflyPreviewDensityMode mode,float amount)
        {
            return new FireflyPreviewZone {
                id=id,displayName=id,center=Vector2.zero,
                radii=new Vector2(20,14),
                densityMode=mode,densityValue=amount,
                priority=priority
            };
        }

        [Test]
        public void Distribution_SeedIsStableAndNegativeCoordinatesWork()
        {
            uint h=FireflyPreviewDistribution.Hash(-51,-901,73421,7);
            Assert.AreEqual(h,FireflyPreviewDistribution.Hash(-51,-901,73421,7));
            Assert.AreNotEqual(h,FireflyPreviewDistribution.Hash(-51,-901,73421,8));
            Assert.That(FireflyPreviewDistribution.Random01(-51,-901,73421),
                Is.InRange(0f,1f));
        }

        [Test]
        public void Areas_PriorityOrderAndExcludeAreDeterministic()
        {
            profile.areas.Add(Zone("low",1,FireflyPreviewDensityMode.Set,20f));
            profile.areas.Add(Zone("high",2,FireflyPreviewDensityMode.Set,45f));
            float first=FireflyPreviewDistribution.Density(profile,Vector2.zero,out var selected);
            Assert.That(first,Is.EqualTo(45f).Within(.001f));
            Assert.AreEqual("high",selected.id);
            profile.areas.Reverse();
            Assert.That(FireflyPreviewDistribution.Density(profile,Vector2.zero,out _),
                Is.EqualTo(first).Within(.001f));
            profile.areas.Add(Zone("exclude",-10,FireflyPreviewDensityMode.Exclude,1f));
            Assert.That(FireflyPreviewDistribution.Density(profile,Vector2.zero,out _),Is.Zero);
            Assert.That(FireflyPreviewDistribution.Density(profile,new Vector2(40,0),out _),
                Is.EqualTo(8f).Within(.001f));
        }

        [Test]
        public void Areas_EqualPriorityUsesStableIdTieBreaker()
        {
            profile.areas.Add(Zone("a",2,FireflyPreviewDensityMode.Set,9));
            profile.areas.Add(Zone("b",2,FireflyPreviewDensityMode.Set,35));
            Assert.That(FireflyPreviewDistribution.Density(profile,Vector2.zero,out _),
                Is.EqualTo(35f).Within(.001f));
            profile.areas.Reverse();
            Assert.That(FireflyPreviewDistribution.Density(profile,Vector2.zero,out _),
                Is.EqualTo(35f).Within(.001f));
        }

        [Test]
        public void Areas_OutsideMapAlwaysEmpty()
        {
            Assert.That(FireflyPreviewDistribution.Density(profile,new Vector2(64.01f,0),out _),
                Is.Zero);
        }

        [Test]
        public void Areas_FalloffMakesBoundarySmooth()
        {
            var a=Zone("edge",0,FireflyPreviewDensityMode.Set,30f);
            profile.areas.Add(a);
            Assert.That(FireflyPreviewDistribution.Influence(a,new Vector2(0,0)),
                Is.EqualTo(1f).Within(.001f));
            Assert.That(FireflyPreviewDistribution.Influence(a,new Vector2(20,0)),Is.Zero);
            float between=FireflyPreviewDistribution.Influence(a,new Vector2(18,0));
            Assert.That(between,Is.GreaterThan(0f).And.LessThan(1f));
        }

        [Test]
        public void Areas_PolygonUsesOwnVerticesAndDoesNotLeak()
        {
            var a=Zone("polygon",0,FireflyPreviewDensityMode.Multiply,3);
            a.shape=FireflyPreviewZoneShape.Polygon;
            a.polygon=new List<Vector2>{
                new Vector2(-10,-10),new Vector2(10,-10),
                new Vector2(10,10),new Vector2(-10,10)};
            profile.areas.Add(a);
            Assert.That(FireflyPreviewDistribution.Influence(a,Vector2.zero),Is.EqualTo(1).Within(.001));
            Assert.That(FireflyPreviewDistribution.Influence(a,new Vector2(30,0)),Is.Zero);
            Assert.That(FireflyPreviewDistribution.Influence(a,new Vector2(9.5f,0)),
                Is.InRange(0f,1f));
        }

        [Test]
        public void Budget_OffAlwaysZeroAndSliderIsMonotonic()
        {
            var b=new FireflyPreviewBudget{quality=FireflyPreviewQuality.Off,
                graphicsVsPerformance=1f};
            b.Resolve(out int worldOff,out int nearOff,out float rangeOff);
            Assert.Zero(worldOff);Assert.Zero(nearOff);Assert.Zero(rangeOff);
            b.quality=FireflyPreviewQuality.High;
            b.graphicsVsPerformance=0;
            b.Resolve(out int low,out int nearLow,out float shortRange);
            b.graphicsVsPerformance=1;
            b.Resolve(out int high,out int nearHigh,out float longRange);
            Assert.Greater(high,low);
            Assert.Greater(nearHigh,nearLow);
            Assert.Greater(longRange,shortRange);
        }

        [Test]
        public void Profile_ValidateDoesNotModifyOtherGameplayAssets()
        {
            profile.densityPer100SquareMetres=100;
            profile.desiredCellCap=5000;
            profile.maximumHeight=2;
            profile.minimumHeight=4;
            profile.Validate();
            Assert.That(profile.densityPer100SquareMetres,Is.EqualTo(80));
            Assert.That(profile.desiredCellCap,Is.EqualTo(1023));
            Assert.That(profile.maximumHeight,Is.GreaterThanOrEqualTo(profile.minimumHeight));
        }

        [Test]
        public void LOD_IsMonotonicAndNeverExceedsAvailableInstances()
        {
            const int cellCount=200;
            int nearby=FireflyPreviewDistribution.LodCount(cellCount,0f,.42f,.88f,.23f);
            int halfway=FireflyPreviewDistribution.LodCount(cellCount,.65f,.42f,.88f,.23f);
            int distant=FireflyPreviewDistribution.LodCount(cellCount,1f,.42f,.88f,.23f);
            Assert.AreEqual(cellCount,nearby);
            Assert.Less(halfway,nearby);
            Assert.Less(distant,halfway);
            Assert.That(distant,Is.GreaterThan(0));
            Assert.AreEqual(0,FireflyPreviewDistribution.LodCount(0,1f,.42f,.88f,.23f));
            Assert.AreEqual(1,FireflyPreviewDistribution.LodCount(1,1f,.42f,.88f,.23f));
        }

        [Test]
        public void LOD_FractionalPopulationFadesBoundaryInsteadOfDroppingIt()
        {
            float population=FireflyPreviewDistribution.LodPopulation(16,.65f,.42f,.88f,.23f);
            int drawCount=FireflyPreviewDistribution.LodCount(16,.65f,.42f,.88f,.23f);
            Assert.Greater(population,0f);
            Assert.AreEqual(Mathf.CeilToInt(population),drawCount);
            Assert.That(drawCount-population,Is.InRange(0f,1f));
            Assert.That(FireflyPreviewDistribution.LodPopulation(16,0f,.42f,.88f,.23f),
                Is.EqualTo(16f).Within(.0001f));
        }

        [Test]
        public void AdaptiveBudget_DropsUnderPressureAndRecoversGradually()
        {
            var controller=new FireflyPreviewAdaptiveBudget();
            Assert.That(controller.Scale,Is.EqualTo(1f));
            for(int i=0;i<150;i++)controller.Step(.05f,i*.05f,60,.4f);
            float throttled=controller.Scale;
            Assert.That(throttled,Is.InRange(.4f,.99f));
            for(int i=0;i<600;i++)controller.Step(.008f,8f+i*.008f,60,.4f);
            Assert.Greater(controller.Scale,throttled);
            Assert.LessOrEqual(controller.Scale,1f);
            controller.Reset();
            Assert.AreEqual(1f,controller.Scale);
            Assert.AreEqual(0f,controller.SmoothedFps);
        }

        [Test]
        public void AdaptiveBudget_IgnoresInvalidSamplesAndObeysMinimum()
        {
            var controller=new FireflyPreviewAdaptiveBudget();
            controller.Step(float.NaN,1f,60,.4f);
            controller.Step(-1f,2f,60,.4f);
            controller.Step(float.PositiveInfinity,3f,60,.4f);
            Assert.AreEqual(1f,controller.Scale);
            for(int i=0;i<250;i++)controller.Step(.08f,i*.08f,60,.45f);
            Assert.That(controller.Scale,Is.GreaterThanOrEqualTo(.45f));
            Assert.That(controller.Scale,Is.LessThan(1f));
        }

        [Test]
        public void StressZone_IsLimitedByExistingGlobalDensityRules()
        {
            profile.densityPer100SquareMetres=4f;
            var zone=Zone("firefly-stress-test",1000,FireflyPreviewDensityMode.Set,100f);
            zone.radii=new Vector2(18,14);
            profile.areas.Add(zone);
            Assert.AreEqual(100f,FireflyPreviewDistribution.Density(profile,Vector2.zero,out _));
            Assert.AreEqual(4f,FireflyPreviewDistribution.Density(profile,new Vector2(30,0),out _));
        }

        [Test]
        public void Profile_LODValidationPreventsInvertedFadeBands()
        {
            profile.lodStartFraction=1f;
            profile.lodEndFraction=0f;
            profile.lodMinimumFraction=-1f;
            profile.Validate();
            Assert.Greater(profile.lodEndFraction,profile.lodStartFraction);
            Assert.That(profile.lodMinimumFraction,Is.InRange(.05f,1f));
        }

        [Test]
        public void Profile_StandaloneJsonRoundtripPreservesAreas()
        {
            profile.areas.Add(Zone("enchanted",5,FireflyPreviewDensityMode.Multiply,6));
            string json=JsonUtility.ToJson(profile);
            var restored=ScriptableObject.CreateInstance<FireflyPreviewProfile>();
            try
            {
                JsonUtility.FromJsonOverwrite(json,restored);
                Assert.That(restored.areas.Count,Is.EqualTo(1));
                Assert.That(restored.areas[0].id,Is.EqualTo("enchanted"));
                Assert.That(restored.densityPer100SquareMetres,Is.EqualTo(8f));
            }
            finally{UnityEngine.Object.DestroyImmediate(restored);}
        }
    }
}
