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
        public void AdaptiveBudget_LowFpsReducesOnlyLocalLimits()
        {
            float scale=1f;
            for(int i=0;i<25;i++)
                scale=FireflyPreviewAdaptation.NextScale(scale,22f,60f,.35f);
            Assert.That(scale,Is.EqualTo(.35f).Within(.0001f));
            int world=1800,near=8;
            float range=95f;
            FireflyPreviewAdaptation.ApplyScale(scale,ref world,ref near,ref range);
            Assert.That(world,Is.LessThan(1800).And.GreaterThan(0));
            Assert.That(near,Is.LessThan(8));
            Assert.That(range,Is.LessThan(95f));
            Assert.That(world,Is.EqualTo(Mathf.RoundToInt(1800f*.35f)));
        }

        [Test]
        public void AdaptiveBudget_DeadBandAndRecoveryAreStable()
        {
            float scale=.65f;
            for(int i=0;i<20;i++)
                scale=FireflyPreviewAdaptation.NextScale(scale,60f,60f,.35f);
            Assert.That(scale,Is.EqualTo(.65f).Within(.0001f));
            Assert.That(FireflyPreviewAdaptation.NextScale(scale,120f,60f,.35f),
                Is.GreaterThan(scale));
            Assert.That(FireflyPreviewAdaptation.NextScale(1f,120f,60f,.35f),
                Is.EqualTo(1f));
            Assert.That(FireflyPreviewAdaptation.NextScale(scale,float.NaN,60f,.35f),
                Is.EqualTo(scale));
        }

        [Test]
        public void NaturalLook_NewPreviewSettingsValidateSafely()
        {
            profile.motionPauseFraction=5f;
            profile.motionCycleSeconds=-10f;
            profile.brightCoreRadius=2f;
            profile.haloStrength=-1f;
            profile.maxDiameterPixels=100f;
            profile.farLodSurvivalFraction=0f;
            profile.farLodStartFraction=2f;
            profile.farBrightnessFraction=0f;
            profile.Validate();
            Assert.That(profile.motionPauseFraction,Is.EqualTo(.8f));
            Assert.That(profile.motionCycleSeconds,Is.EqualTo(2f));
            Assert.That(profile.brightCoreRadius,Is.EqualTo(.32f));
            Assert.That(profile.haloStrength,Is.Zero);
            Assert.That(profile.maxDiameterPixels,Is.EqualTo(20f));
            Assert.That(profile.farLodSurvivalFraction,Is.EqualTo(.05f));
            Assert.That(profile.farLodStartFraction,Is.EqualTo(.95f));
            Assert.That(profile.farBrightnessFraction,Is.EqualTo(.1f));
        }

        [Test]
        public void StressMeadow_CannotEscapeMapAndGlobalExclusion()
        {
            profile.areas.Add(new FireflyPreviewZone {
                id="stress",center=Vector2.zero,radii=new Vector2(24f,18f),
                falloff=.35f,densityMode=FireflyPreviewDensityMode.Set,
                densityValue=80f,priority=100
            });
            Assert.That(FireflyPreviewDistribution.Density(profile,Vector2.zero,out _),
                Is.EqualTo(80f).Within(.001f));
            profile.areas.Add(Zone("exclude",101,FireflyPreviewDensityMode.Exclude,1f));
            Assert.That(FireflyPreviewDistribution.Density(profile,Vector2.zero,out _),Is.Zero);
            Assert.That(FireflyPreviewDistribution.Density(profile,new Vector2(1000f,0),out _),Is.Zero);
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
