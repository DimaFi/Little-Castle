using System;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class QualityPresetTests
    {
        private const string QualityPath =
            "Assets/_Game/Settings/World/ConceptWorld_v001/" +
            "ConceptQualitySettings.asset";
        private const string NightPath =
            "Assets/_Game/Settings/World/ConceptWorld_v001/" +
            "ConceptVillageNightLighting.asset";

        private static ConceptQualitySettings Saved()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ConceptQualitySettings>(
                QualityPath);
            Assert.That(asset, Is.Not.Null);
            return asset;
        }

        [Test]
        public void SavedQualityAsset_HasThreeValidMonotonicPresets()
        {
            ConceptQualitySettings quality = Saved();
            Assert.That(quality.TryValidate(out string error), Is.True, error);

            ConceptQualityPreset low = quality.Get(ConceptQualityTier.Low);
            ConceptQualityPreset medium =
                quality.Get(ConceptQualityTier.Medium);
            ConceptQualityPreset high = quality.Get(ConceptQualityTier.High);

            Assert.That(low.shadowDistanceMeters,
                Is.LessThan(medium.shadowDistanceMeters));
            Assert.That(medium.shadowDistanceMeters,
                Is.LessThan(high.shadowDistanceMeters));
            Assert.That(low.decorativeGrassVisibleFraction,
                Is.LessThan(medium.decorativeGrassVisibleFraction));
            Assert.That(medium.decorativeGrassVisibleFraction,
                Is.LessThan(high.decorativeGrassVisibleFraction));
            Assert.That(high.decorativeGrassVisibleFraction, Is.EqualTo(1f));
            Assert.That(low.maxFullDecorativeTreePrefabs,
                Is.LessThan(medium.maxFullDecorativeTreePrefabs));
            Assert.That(medium.maxFullDecorativeTreePrefabs,
                Is.LessThan(high.maxFullDecorativeTreePrefabs));
            Assert.That(low.maxVillageRealtimeLightRequests, Is.Zero);
            Assert.That(medium.maxVillageRealtimeLightRequests, Is.EqualTo(1));
            Assert.That(high.maxVillageRealtimeLightRequests, Is.EqualTo(2));
        }

        [Test]
        public void VisualQualityLookup_DoesNotModifyUnityGlobalQualitySettings()
        {
            float originalShadow = QualitySettings.shadowDistance;
            var quality = Saved();
            foreach (ConceptQualityTier tier in new[] {
                ConceptQualityTier.Low,
                ConceptQualityTier.Medium,
                ConceptQualityTier.High
            })
                Assert.That(quality.Get(tier).IsValid, Is.True);

            Assert.That(QualitySettings.shadowDistance,
                Is.EqualTo(originalShadow),
                "Only root owner is allowed to apply presentation quality.");
        }

        [Test]
        public void DecorativeGrassDecision_IsStableForSameSeedAndId()
        {
            var quality = Saved();
            const long id = -110032770099L;
            bool first = quality.ShouldRenderDecorativeGrass(
                ConceptQualityTier.Medium, -10101, id);
            for (int i = 0; i < 50; i++)
                Assert.That(quality.ShouldRenderDecorativeGrass(
                    ConceptQualityTier.Medium, -10101, id),
                    Is.EqualTo(first));
        }

        [Test]
        public void HigherQualityKeepsEveryLowerTierGrassMarker()
        {
            var quality = Saved();
            int lowCount = 0, mediumCount = 0, highCount = 0;
            for (long id = -1000; id < 1000; id++)
            {
                bool low = quality.ShouldRenderDecorativeGrass(
                    ConceptQualityTier.Low, 41, id);
                bool med = quality.ShouldRenderDecorativeGrass(
                    ConceptQualityTier.Medium, 41, id);
                bool high = quality.ShouldRenderDecorativeGrass(
                    ConceptQualityTier.High, 41, id);
                if (low) lowCount++;
                if (med) mediumCount++;
                if (high) highCount++;
                if (low)
                    Assert.That(med, Is.True,
                        "A visual upgrade cannot remove previous grass.");
                if (med)
                    Assert.That(high, Is.True);
            }

            Assert.That(lowCount, Is.GreaterThan(0));
            Assert.That(lowCount, Is.LessThan(mediumCount));
            Assert.That(mediumCount, Is.LessThan(highCount));
            Assert.That(highCount, Is.EqualTo(2000));
        }

        [Test]
        public void VisualTierSwitch_NeverChangesGeneratedStableId()
        {
            var quality = Saved();
            const int seed = -10101;
            long generatedId = DeterministicHash.StableId(
                seed, -8, 13, 0x1534);
            foreach (ConceptQualityTier tier in new[] {
                ConceptQualityTier.Low,
                ConceptQualityTier.Medium,
                ConceptQualityTier.High
            })
            {
                quality.ShouldRenderDecorativeGrass(
                    tier, seed, generatedId);
                Assert.That(DeterministicHash.StableId(
                    seed, -8, 13, 0x1534),
                    Is.EqualTo(generatedId));
            }
        }

        [Test]
        public void VillageLights_StayAtOrBelowAuthoredQ13Budget()
        {
            var authored =
                AssetDatabase.LoadAssetAtPath<
                    ConceptVillageNightLightingSettings>(NightPath);
            Assert.That(authored, Is.Not.Null);
            var quality = Saved();
            foreach (ConceptQualityTier tier in new[] {
                ConceptQualityTier.Low,
                ConceptQualityTier.Medium,
                ConceptQualityTier.High
            })
            {
                VillageNightLightingBudget result =
                    quality.ResolveVillageBudget(tier, authored.Budget);
                Assert.That(result.IsValid, Is.True);
                Assert.That(result.maxVillageRealtimeRequests,
                    Is.LessThanOrEqualTo(
                        authored.Budget.maxVillageRealtimeRequests));
                Assert.That(result.maxVillageGroundPools,
                    Is.LessThanOrEqualTo(
                        authored.Budget.maxVillageGroundPools));
                Assert.That(result.realtimeDistance,
                    Is.LessThanOrEqualTo(authored.Budget.realtimeDistance));
                Assert.That(result.groundPoolDistance,
                    Is.LessThanOrEqualTo(authored.Budget.groundPoolDistance));
                Assert.That(result.emissiveDistance,
                    Is.LessThanOrEqualTo(authored.Budget.emissiveDistance));
            }
        }

        [Test]
        public void HighPreset_DoesNotExceedQ13AuthoredLightLimits()
        {
            var authored =
                AssetDatabase.LoadAssetAtPath<
                    ConceptVillageNightLightingSettings>(NightPath);
            var quality = Saved();
            var high = quality.ResolveVillageBudget(
                ConceptQualityTier.High, authored.Budget);
            Assert.That(high.maxVillageRealtimeRequests, Is.EqualTo(2));
            Assert.That(high.maxVillageGroundPools, Is.EqualTo(5));
            Assert.That(high.realtimeDistance, Is.EqualTo(34f));
        }

        [Test]
        public void InvalidQualityEnum_DoesNotChooseFallbackPreset()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Saved().Get((ConceptQualityTier)123));
        }

        [Test]
        public void InvalidSavedProfileOrdering_IsRejectedWithoutChangingAssets()
        {
            var temp = ScriptableObject.CreateInstance<ConceptQualitySettings>();
            try
            {
                var serialized = new SerializedObject(temp);
                var lower = serialized.FindProperty("low");
                lower.FindPropertyRelative("shadowDistanceMeters").floatValue =
                    140f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(temp.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("Low <= Medium <= High"));
                Assert.That(Saved().TryValidate(out _), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        [Test]
        public void InvalidNaNAndNegativeGrassDensity_AreRejected()
        {
            var temp = ScriptableObject.CreateInstance<ConceptQualitySettings>();
            try
            {
                var serialized = new SerializedObject(temp);
                var low = serialized.FindProperty("low");
                low.FindPropertyRelative(
                    "decorativeGrassVisibleFraction").floatValue = -1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(temp.TryValidate(out _), Is.False);
                low.FindPropertyRelative(
                    "decorativeGrassVisibleFraction").floatValue = float.NaN;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(temp.TryValidate(out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        [Test]
        public void ApprovedVillageBudgetCannotBeOverriddenWithBadInput()
        {
            var quality = Saved();
            var authored = new VillageNightLightingBudget();
            Assert.Throws<ArgumentException>(() =>
                quality.ResolveVillageBudget(
                    ConceptQualityTier.High, authored));
        }

        [Test]
        public void NightLightingPolicyAndQualityCap_ComposeCorrectly()
        {
            var authored =
                AssetDatabase.LoadAssetAtPath<
                    ConceptVillageNightLightingSettings>(NightPath).Budget;
            var quality = Saved();
            var input = new[]
            {
                new VillageNightMarker(13, VillageNightMarkerKind.PathEntrance,
                    new Vector2(1f, 0f)),
                new VillageNightMarker(14, VillageNightMarkerKind.PathEntrance,
                    new Vector2(2f, 0f)),
                new VillageNightMarker(15, VillageNightMarkerKind.CourtyardWell,
                    new Vector2(3f, 0f))
            };
            var output = new System.Collections.Generic.List<
                VillageNightPresentation>();
            var policy = new VillageNightLightingPolicy();

            var low = policy.Evaluate(input,
                quality.ResolveVillageBudget(ConceptQualityTier.Low, authored),
                Vector2.zero, 1f, 12, output);
            Assert.That(low.realtimeRequests, Is.Zero);
            Assert.That(low.groundPools, Is.EqualTo(2));

            var med = policy.Evaluate(input,
                quality.ResolveVillageBudget(ConceptQualityTier.Medium, authored),
                Vector2.zero, 1f, 12, output);
            Assert.That(med.realtimeRequests, Is.EqualTo(1));
            Assert.That(med.groundPools, Is.EqualTo(3));

            var high = policy.Evaluate(input,
                quality.ResolveVillageBudget(ConceptQualityTier.High, authored),
                Vector2.zero, 1f, 12, output);
            Assert.That(high.realtimeRequests, Is.EqualTo(2));
            Assert.That(high.groundPools, Is.EqualTo(3));
        }
    }
}
