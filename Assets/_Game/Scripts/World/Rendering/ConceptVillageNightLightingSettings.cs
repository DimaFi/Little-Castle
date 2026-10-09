using System;
using UnityEngine;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Optional PRESENTATION-only knobs for one concept settlement.
    /// The existing NightLightBudgetManager remains the sole arbiter of
    /// global realtime Point/Spot lights (currently capped at 12).
    /// No seed, placement, player start, resource or save data lives here.
    /// </summary>
    [Serializable]
    public struct VillageNightLightingBudget
    {
        [Min(0f)] public float realtimeDistance;
        [Min(0f)] public float groundPoolDistance;
        [Min(0f)] public float emissiveDistance;
        [Range(0f, 1f)] public float minimumNightAmount;
        [Range(0, 2)] public int maxVillageRealtimeRequests;
        [Min(0)] public int maxVillageGroundPools;

        public bool IsValid =>
            Finite(realtimeDistance) && realtimeDistance >= 0f &&
            Finite(groundPoolDistance) &&
            groundPoolDistance >= realtimeDistance &&
            Finite(emissiveDistance) &&
            emissiveDistance >= groundPoolDistance &&
            Finite(minimumNightAmount) &&
            minimumNightAmount >= 0f && minimumNightAmount <= 1f &&
            maxVillageRealtimeRequests >= 0 &&
            maxVillageRealtimeRequests <= 2 &&
            maxVillageGroundPools >= 0 &&
            maxVillageGroundPools <= 16;

        private static bool Finite(float x) =>
            !float.IsNaN(x) && !float.IsInfinity(x);
    }

    [CreateAssetMenu(
        fileName = "ConceptVillageNightLighting",
        menuName = "Little Castle/World/Concept Village Night Lighting")]
    public sealed class ConceptVillageNightLightingSettings : ScriptableObject
    {
        [SerializeField]
        private VillageNightLightingBudget budget =
            new VillageNightLightingBudget
            {
                realtimeDistance = 34f,
                groundPoolDistance = 92f,
                emissiveDistance = 200f,
                minimumNightAmount = 0.18f,
                maxVillageRealtimeRequests = 2,
                maxVillageGroundPools = 5
            };

        public VillageNightLightingBudget Budget => budget;

        public bool TryValidate(out string error)
        {
            if (!budget.IsValid)
            {
                error =
                    "Invalid concept village light distances/night threshold " +
                    "or per-village budget. Limits must not exceed 2 realtime " +
                    "requests and 16 inexpensive ground pools.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
