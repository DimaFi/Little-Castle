using System;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    public enum ConceptQualityTier : byte
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>
    /// Presentation-only proposed limits. In particular decorative grass
    /// is NOT wheat, harvestable resources or authoritative world spawns.
    /// Full prefab budgets are advisory and may be exceeded to preserve
    /// choppable/interactive trees.
    /// </summary>
    [Serializable]
    public struct ConceptQualityPreset
    {
        [Min(0f)] public float shadowDistanceMeters;
        [Range(0f, 1f)] public float decorativeGrassVisibleFraction;
        [Min(1f)] public float forestNearMeters;
        [Min(1f)] public float forestFarMeters;
        [Min(0f)] public float optionalDecorativeColliderMeters;
        [Min(0)] public int maxFullDecorativeTreePrefabs;
        [Min(0)] public int maxDecorativeTreeColliders;
        [Range(0f, 1f)] public float farClusterIndividualRetention;

        [Range(0, 2)] public int maxVillageRealtimeLightRequests;
        [Range(0, 16)] public int maxVillageGroundGlows;
        [Min(0f)] public float villageRealtimeDistance;
        [Min(0f)] public float villagePoolDistance;
        [Min(0f)] public float villageEmissiveDistance;

        public bool IsValid =>
            Finite(shadowDistanceMeters) && shadowDistanceMeters >= 0f &&
            Finite(decorativeGrassVisibleFraction) &&
            decorativeGrassVisibleFraction >= 0f &&
            decorativeGrassVisibleFraction <= 1f &&
            Finite(forestNearMeters) && forestNearMeters > 0f &&
            Finite(forestFarMeters) &&
            forestFarMeters > forestNearMeters &&
            Finite(optionalDecorativeColliderMeters) &&
            optionalDecorativeColliderMeters >= 0f &&
            optionalDecorativeColliderMeters <= forestNearMeters &&
            maxFullDecorativeTreePrefabs >= 0 &&
            maxDecorativeTreeColliders >= 0 &&
            Finite(farClusterIndividualRetention) &&
            farClusterIndividualRetention >= 0f &&
            farClusterIndividualRetention <= 1f &&
            maxVillageRealtimeLightRequests >= 0 &&
            maxVillageRealtimeLightRequests <= 2 &&
            maxVillageGroundGlows >= 0 &&
            maxVillageGroundGlows <= 16 &&
            Finite(villageRealtimeDistance) &&
            villageRealtimeDistance >= 0f &&
            Finite(villagePoolDistance) &&
            villagePoolDistance >= villageRealtimeDistance &&
            Finite(villageEmissiveDistance) &&
            villageEmissiveDistance >= villagePoolDistance;

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// These are NOT calibrated minimum-system-requirement profiles.
    /// Nothing automatically edits Unity QualitySettings, URP assets,
    /// runtime scene, streamer, game-generation settings or saves.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ConceptQualitySettings",
        menuName = "Little Castle/World/Concept Visual Quality Settings")]
    public sealed class ConceptQualitySettings : ScriptableObject
    {
        [SerializeField] private ConceptQualityPreset low =
            new ConceptQualityPreset
            {
                shadowDistanceMeters = 28f,
                decorativeGrassVisibleFraction = 0.40f,
                forestNearMeters = 20f,
                forestFarMeters = 68f,
                optionalDecorativeColliderMeters = 6f,
                maxFullDecorativeTreePrefabs = 150,
                maxDecorativeTreeColliders = 10,
                farClusterIndividualRetention = 0.10f,
                maxVillageRealtimeLightRequests = 0,
                maxVillageGroundGlows = 2,
                villageRealtimeDistance = 18f,
                villagePoolDistance = 42f,
                villageEmissiveDistance = 95f
            };

        [SerializeField] private ConceptQualityPreset medium =
            new ConceptQualityPreset
            {
                shadowDistanceMeters = 54f,
                decorativeGrassVisibleFraction = 0.70f,
                forestNearMeters = 32f,
                forestFarMeters = 110f,
                optionalDecorativeColliderMeters = 9f,
                maxFullDecorativeTreePrefabs = 300,
                maxDecorativeTreeColliders = 24,
                farClusterIndividualRetention = 0.35f,
                maxVillageRealtimeLightRequests = 1,
                maxVillageGroundGlows = 3,
                villageRealtimeDistance = 28f,
                villagePoolDistance = 64f,
                villageEmissiveDistance = 140f
            };

        [SerializeField] private ConceptQualityPreset high =
            new ConceptQualityPreset
            {
                shadowDistanceMeters = 85f,
                decorativeGrassVisibleFraction = 1f,
                forestNearMeters = 48f,
                forestFarMeters = 170f,
                optionalDecorativeColliderMeters = 14f,
                maxFullDecorativeTreePrefabs = 600,
                maxDecorativeTreeColliders = 48,
                farClusterIndividualRetention = 1f,
                maxVillageRealtimeLightRequests = 2,
                maxVillageGroundGlows = 5,
                villageRealtimeDistance = 34f,
                villagePoolDistance = 92f,
                villageEmissiveDistance = 200f
            };

        public ConceptQualityPreset Low => low;
        public ConceptQualityPreset Medium => medium;
        public ConceptQualityPreset High => high;

        public ConceptQualityPreset Get(ConceptQualityTier tier)
        {
            switch (tier)
            {
                case ConceptQualityTier.Low:
                    return low;
                case ConceptQualityTier.Medium:
                    return medium;
                case ConceptQualityTier.High:
                    return high;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        /// <summary>
        /// Enforces monotonic visual budgets, without a quality level
        /// inadvertently spending MORE than a higher level or overriding
        /// shared light/fog/fairness policy. Does not assert measured FPS.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (!low.IsValid || !medium.IsValid || !high.IsValid)
            {
                error = "One or more visual-only quality presets is invalid.";
                return false;
            }

            if (!NoMoreThan(low, medium) ||
                !NoMoreThan(medium, high))
            {
                error =
                    "Expected Low <= Medium <= High across visual density, " +
                    "shadow, optional forest and village lighting budgets.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Integrates safely with Q13's approved authoring envelope:
        /// quality never increases emitter/pool distances or light counts
        /// beyond those allowed by the existing village concept budget.
        /// </summary>
        public VillageNightLightingBudget ResolveVillageBudget(
            ConceptQualityTier tier,
            VillageNightLightingBudget authored)
        {
            if (!TryValidate(out string error))
                throw new InvalidOperationException(error);
            if (!authored.IsValid)
                throw new ArgumentException(
                    "Unapproved authored village light budget.",
                    nameof(authored));

            ConceptQualityPreset selected = Get(tier);
            VillageNightLightingBudget value = authored;
            value.maxVillageRealtimeRequests = Mathf.Min(
                authored.maxVillageRealtimeRequests,
                selected.maxVillageRealtimeLightRequests);
            value.maxVillageGroundPools = Mathf.Min(
                authored.maxVillageGroundPools,
                selected.maxVillageGroundGlows);
            value.realtimeDistance = Mathf.Min(
                authored.realtimeDistance,
                selected.villageRealtimeDistance);
            value.groundPoolDistance = Mathf.Min(
                authored.groundPoolDistance,
                selected.villagePoolDistance);
            value.emissiveDistance = Mathf.Min(
                authored.emissiveDistance,
                selected.villageEmissiveDistance);

            if (!value.IsValid)
                throw new InvalidOperationException(
                    "Selected quality violated village lighting constraints.");
            return value;
        }

        /// <summary>
        /// Stable visual-only density screen for APPROVED decoration.
        /// Must never be called on gameplay grass, trees, wheat, resource
        /// spawns, fixed bridge vegetation or save/network entities.
        /// </summary>
        public bool ShouldRenderDecorativeGrass(
            ConceptQualityTier tier,
            int worldSeed,
            long decorativeStableId)
        {
            ConceptQualityPreset selected = Get(tier);
            if (!selected.IsValid)
                throw new InvalidOperationException(
                    "Invalid quality preset.");
            float fraction = selected.decorativeGrassVisibleFraction;
            if (fraction <= 0f)
                return false;
            if (fraction >= 1f)
                return true;
            int lowId = unchecked((int)decorativeStableId);
            int highId = unchecked((int)(decorativeStableId >> 32));
            return DeterministicHash.Hash01(
                worldSeed, lowId, highId, 0x51475253) < fraction;
        }

        private static bool NoMoreThan(
            ConceptQualityPreset lower,
            ConceptQualityPreset higher)
        {
            return lower.shadowDistanceMeters <= higher.shadowDistanceMeters &&
                lower.decorativeGrassVisibleFraction <=
                    higher.decorativeGrassVisibleFraction &&
                lower.forestNearMeters <= higher.forestNearMeters &&
                lower.forestFarMeters <= higher.forestFarMeters &&
                lower.optionalDecorativeColliderMeters <=
                    higher.optionalDecorativeColliderMeters &&
                lower.maxFullDecorativeTreePrefabs <=
                    higher.maxFullDecorativeTreePrefabs &&
                lower.maxDecorativeTreeColliders <=
                    higher.maxDecorativeTreeColliders &&
                lower.farClusterIndividualRetention <=
                    higher.farClusterIndividualRetention &&
                lower.maxVillageRealtimeLightRequests <=
                    higher.maxVillageRealtimeLightRequests &&
                lower.maxVillageGroundGlows <= higher.maxVillageGroundGlows &&
                lower.villageRealtimeDistance <=
                    higher.villageRealtimeDistance &&
                lower.villagePoolDistance <= higher.villagePoolDistance &&
                lower.villageEmissiveDistance <= higher.villageEmissiveDistance;
        }
    }
}
