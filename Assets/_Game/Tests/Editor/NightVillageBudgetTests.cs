using System;
using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class NightVillageBudgetTests
    {
        private const string Root =
            "Assets/_Game/Settings/World/ConceptWorld_v001/";

        private static VillageNightLightingBudget Config(
            int lights = 2, int pools = 5)
        {
            return new VillageNightLightingBudget
            {
                realtimeDistance = 34f,
                groundPoolDistance = 92f,
                emissiveDistance = 200f,
                minimumNightAmount = 0.18f,
                maxVillageRealtimeRequests = lights,
                maxVillageGroundPools = pools
            };
        }

        private static VillageNightMarker Marker(
            long id, float x,
            VillageNightMarkerKind kind =
                VillageNightMarkerKind.PathEntrance)
        {
            return new VillageNightMarker(
                id, kind, new Vector2(x, 0f));
        }

        [Test]
        public void IsolatedLightingAsset_RespectsExistingSmallBudget()
        {
            var asset =
                AssetDatabase.LoadAssetAtPath<ConceptVillageNightLightingSettings>(
                    Root + "ConceptVillageNightLighting.asset");
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.TryValidate(out string error), Is.True, error);
            Assert.That(asset.Budget.maxVillageRealtimeRequests,
                Is.EqualTo(2));
            Assert.That(asset.Budget.maxVillageGroundPools,
                Is.EqualTo(5));
            Assert.That(asset.Budget.realtimeDistance,
                Is.LessThan(asset.Budget.groundPoolDistance));
            Assert.That(asset.Budget.groundPoolDistance,
                Is.LessThan(asset.Budget.emissiveDistance));
        }

        [Test]
        public void Daylight_DoesNotRequestAnyRealtimeOrGlow()
        {
            var result = new List<VillageNightPresentation>();
            var policy = new VillageNightLightingPolicy();
            var metrics = policy.Evaluate(
                new[] { Marker(10, 2), Marker(11, 50) },
                Config(), Vector2.zero, 0f, 12, result);

            Assert.That(metrics.realtimeRequests, Is.Zero);
            Assert.That(metrics.groundPools, Is.Zero);
            Assert.That(metrics.emissive, Is.Zero);
            for (int i = 0; i < result.Count; i++)
            {
                Assert.That(result[i].requestRealtimeLight, Is.False);
                Assert.That(result[i].groundPoolVisible, Is.False);
                Assert.That(result[i].emissiveVisible, Is.False);
            }
        }

        [Test]
        public void NightNearMidFar_LodDoesNotRequestDistantLight()
        {
            var policy = new VillageNightLightingPolicy();
            var choices = new List<VillageNightPresentation>();
            var metrics = policy.Evaluate(
                new[]
                {
                    Marker(1, 5),
                    Marker(2, 50),
                    Marker(3, 130),
                    Marker(4, 250),
                    Marker(5, 7, VillageNightMarkerKind.WindowEmissive)
                },
                Config(), Vector2.zero, 1f, 12, choices);

            Assert.That(choices[0].requestRealtimeLight, Is.True);
            Assert.That(choices[0].groundPoolVisible, Is.True);
            Assert.That(choices[1].requestRealtimeLight, Is.False);
            Assert.That(choices[1].groundPoolVisible, Is.True);
            Assert.That(choices[2].requestRealtimeLight, Is.False);
            Assert.That(choices[2].groundPoolVisible, Is.False);
            Assert.That(choices[2].emissiveVisible, Is.True);
            Assert.That(choices[3].emissiveVisible, Is.False);
            Assert.That(choices[4].requestRealtimeLight, Is.False);
            Assert.That(choices[4].groundPoolVisible, Is.False);
            Assert.That(choices[4].emissiveVisible, Is.True);
            Assert.That(metrics.realtimeRequests, Is.EqualTo(1));
            Assert.That(metrics.groundPools, Is.EqualTo(2));
        }

        [Test]
        public void RealtimeRequests_NeverExceedLocalOrGlobalAdvisorySlots()
        {
            var input = new List<VillageNightMarker>();
            for (int i = 0; i < 16; i++)
                input.Add(Marker(100L + i, 1f + i * 0.5f));
            var choices = new List<VillageNightPresentation>();
            var policy = new VillageNightLightingPolicy();

            var regular = policy.Evaluate(
                input, Config(), Vector2.zero, 1f, 12, choices);
            Assert.That(regular.realtimeRequests, Is.EqualTo(2));
            Assert.That(regular.groundPools, Is.EqualTo(5));
            Assert.That(regular.globalBudgetDenied,
                Is.GreaterThanOrEqualTo(3));

            var scarce = policy.Evaluate(
                input, Config(), Vector2.zero, 1f, 1, choices);
            Assert.That(scarce.realtimeRequests, Is.EqualTo(1));

            var none = policy.Evaluate(
                input, Config(), Vector2.zero, 1f, 0, choices);
            Assert.That(none.realtimeRequests, Is.Zero);
            Assert.That(none.groundPools, Is.EqualTo(5),
                "Glow is independent of expensive realtime light.");
        }

        [Test]
        public void InputEnumerationOrder_DoesNotAffectStableAssignments()
        {
            var a = new List<VillageNightMarker>
            {
                Marker(15, 4),
                Marker(-7, 4),
                Marker(9, 7),
                Marker(200, 18, VillageNightMarkerKind.CourtyardWell),
                Marker(11, 4, VillageNightMarkerKind.WindowEmissive)
            };
            var b = new List<VillageNightMarker>(a);
            b.Reverse();
            var policy = new VillageNightLightingPolicy();
            var outA = new List<VillageNightPresentation>();
            var outB = new List<VillageNightPresentation>();
            policy.Evaluate(a, Config(1, 2), Vector2.zero, 0.9f, 2, outA);
            policy.Evaluate(b, Config(1, 2), Vector2.zero, 0.9f, 2, outB);

            Assert.That(outA.Count, Is.EqualTo(outB.Count));
            for (int i = 0; i < outA.Count; i++)
            {
                Assert.That(outB[i].stableId, Is.EqualTo(outA[i].stableId));
                Assert.That(outB[i].requestRealtimeLight,
                    Is.EqualTo(outA[i].requestRealtimeLight));
                Assert.That(outB[i].groundPoolVisible,
                    Is.EqualTo(outA[i].groundPoolVisible));
                Assert.That(outB[i].emissiveVisible,
                    Is.EqualTo(outA[i].emissiveVisible));
            }
        }

        [Test]
        public void InvalidInputs_DoNotTouchPreviousOutput()
        {
            var policy = new VillageNightLightingPolicy();
            var output = new List<VillageNightPresentation>();
            policy.Evaluate(new[] { Marker(1, 2) }, Config(),
                Vector2.zero, 1f, 2, output);

            var invalid = Config();
            invalid.maxVillageRealtimeRequests = 13;
            Assert.Throws<ArgumentException>(() =>
                policy.Evaluate(new[] { Marker(3, 2) }, invalid,
                    Vector2.zero, 1f, 2, output));
            Assert.Throws<ArgumentException>(() =>
                policy.Evaluate(new[] { Marker(3, 2) }, Config(),
                    new Vector2(float.NaN, 0f), 1f, 2, output));
            Assert.Throws<ArgumentException>(() =>
                policy.Evaluate(new[] { Marker(3, 2) }, Config(),
                    Vector2.zero, 1f, -1, output));
            Assert.Throws<ArgumentException>(() =>
                policy.Evaluate(new[] { Marker(3, 2), Marker(3, 4) },
                    Config(), Vector2.zero, 1f, 2, output));
            Assert.Throws<ArgumentException>(() =>
                policy.Evaluate(new[] { new VillageNightMarker(
                    3, VillageNightMarkerKind.PathEntrance,
                    new Vector2(float.PositiveInfinity, 0f)) },
                    Config(), Vector2.zero, 1f, 2, output));

            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].stableId, Is.EqualTo(1));
        }

        [Test]
        public void InvalidKindOrThreshold_RefusedBeforeChangingOutput()
        {
            var policy = new VillageNightLightingPolicy();
            var choices = new List<VillageNightPresentation>();
            var cfg = Config();
            cfg.minimumNightAmount = 1.1f;
            Assert.Throws<ArgumentException>(() =>
                policy.Evaluate(new[] { Marker(1, 1) }, cfg,
                    Vector2.zero, 1f, 2, choices));
            Assert.Throws<ArgumentException>(() =>
                policy.Evaluate(new[] { Marker(
                    1, 1, (VillageNightMarkerKind)99) }, Config(),
                    Vector2.zero, 1f, 2, choices));
            Assert.That(choices, Is.Empty);
        }

        [Test]
        public void RepeatedCalls_NoStaleMarkersOrPriorityLeak()
        {
            var policy = new VillageNightLightingPolicy();
            var output = new List<VillageNightPresentation>();
            policy.Evaluate(new[] { Marker(1, 2), Marker(2, 2) },
                Config(), Vector2.zero, 1f, 2, output);
            Assert.That(output.Count, Is.EqualTo(2));

            var updated = policy.Evaluate(
                new[] { Marker(-1, 300) }, Config(),
                Vector2.zero, 1f, 2, output);
            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].stableId, Is.EqualTo(-1));
            Assert.That(output[0].requestRealtimeLight, Is.False);
            Assert.That(updated.realtimeRequests, Is.Zero);
        }

        [Test]
        public void AcceptedQ11Layout_ProducesWellAndEntranceMarkers()
        {
            const BuildingSiteFlags safe =
                BuildingSiteFlags.Ready |
                BuildingSiteFlags.Playable |
                BuildingSiteFlags.Buildable |
                BuildingSiteFlags.Walkable;
            var settings = new VillageLayoutSettings
            {
                houseCount = 3,
                houseFootprint = new BuildingFootprintDefinition(
                    new Vector2(1.5f, 1.8f),
                    5, 0.35f, 12f, 2f, 2.5f, 0.45f),
                wellRadius = 1f,
                courtyardRadius = 4f,
                minimumBuildingClearance = 0.5f,
                approachHalfWidth = 0.75f,
                pathSampleInterval = 0.75f,
                maximumPathStep = 0.5f,
                maximumCourtyardSlope = 12f,
                maximumWellHeightSpread = 0.35f
            };
            Assert.That(VillageLayoutPlanner.TryPlan(
                -10101, 4242L, new Vector2(-80f, 32f),
                settings,
                _ => new BuildingSiteSample(0f, 0f, safe),
                out VillageLayoutData layout,
                out VillageLayoutFailure failure),
                Is.True, failure.reason.ToString());

            var a = new List<VillageNightMarker>();
            var b = new List<VillageNightMarker>();
            VillageNightLightingPolicy.BuildMarkers(layout, a);
            VillageNightLightingPolicy.BuildMarkers(layout, b);
            Assert.That(a.Count, Is.EqualTo(4));
            Assert.That(a[0].kind,
                Is.EqualTo(VillageNightMarkerKind.CourtyardWell));
            Assert.That(a[0].worldXZ,
                Is.EqualTo(layout.WellPosition));

            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b[i].stableId, Is.EqualTo(a[i].stableId));
                Assert.That(b[i].worldXZ, Is.EqualTo(a[i].worldXZ));
                if (i > 0)
                    Assert.That(a[i].worldXZ,
                        Is.EqualTo(layout.Approaches[i - 1].doorEdgeWorld));
            }
        }

        [Test]
        public void ConceptLanternPrefab_HasDisabledShadowlessManagedLight()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "ConceptVillageNightLantern.prefab");
            Assert.That(prefab, Is.Not.Null);
            var light = prefab.GetComponentInChildren<Light>(true);
            var emitter = prefab.GetComponentInChildren<NightLightEmitter>(true);
            var pool = prefab.GetComponentInChildren<NightLightPoolVisual>(true);

            Assert.That(light, Is.Not.Null);
            Assert.That(emitter, Is.Not.Null);
            Assert.That(pool, Is.Not.Null);
            Assert.That(emitter.TargetLight, Is.SameAs(light));
            Assert.That(emitter.PoolVisual, Is.SameAs(pool));
            Assert.That(light.type, Is.EqualTo(LightType.Point));
            Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(light.enabled, Is.False,
                "Imported prefab must never have an unmanaged active Point Light.");
            Assert.That(emitter.MaxDistance, Is.LessThanOrEqualTo(34f));
            Assert.That(pool.TargetRenderer, Is.Not.Null);
            Assert.That(pool.TargetRenderer.sharedMaterial, Is.Not.Null);
            Assert.That(pool.TargetRenderer.sharedMaterial.shader.name,
                Is.EqualTo("Little Castle/Effects/LC Night Light Pool"));
            Assert.That(pool.TargetRenderer.shadowCastingMode,
                Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            Assert.That(prefab.GetComponentsInChildren<Light>(true).Length,
                Is.EqualTo(1));
        }

        [Test]
        public void WindowGlowPrefab_HasNoRealtimeLightOrCollider()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "ConceptVillageNightWindowGlow.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Light>(true),
                Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            var renderer = prefab.GetComponent<MeshRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.shader.name,
                Is.EqualTo("Little Castle/Effects/LC Emissive Glow"));
            Assert.That(renderer.shadowCastingMode,
                Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
        }

        [Test]
        public void ExistingGlobalLightBudget_IsNotSilentlyRaised()
        {
            var managerObject = new GameObject("Q13NightBudgetProbe");
            try
            {
                var manager = managerObject.AddComponent<NightLightBudgetManager>();
                var serialized = new SerializedObject(manager);
                SerializedProperty global =
                    serialized.FindProperty("maxActiveRealtimeLights");
                Assert.That(global, Is.Not.Null);
                Assert.That(global.intValue, Is.EqualTo(12),
                    "This PR does not increase the existing global cap.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }
    }
}
