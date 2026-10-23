using System;

namespace LittleCastle.Gameplay
{
    public static class SettlementBuildingService
    {
        public static bool TryStartBuilding(
            GameplaySessionState session,
            SettlementGameplayState settlement,
            int playerId,
            string archetypeId,
            float worldX,
            float worldY,
            float worldZ,
            float yawDegrees,
            int assignedBuilders,
            GameplayRules rules,
            out long buildingId)
        {
            buildingId = 0;

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));

            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            if (settlement.isNeutral ||
                settlement.ownerPlayerId != playerId)
            {
                return false;
            }

            BuildingDefinition definition =
                rules.FindBuilding(
                    archetypeId);

            if (definition == null)
                return false;

            if (!settlement.inventory.TrySpend(
                definition.cost))
            {
                return false;
            }

            buildingId =
                session.AllocateRuntimeId();

            float maxHp =
                Math.Max(
                    1f,
                    definition.maxHitPoints);

            double requiredWork =
                Math.Max(
                    0.0,
                    definition.constructionWorkHours);

            settlement.buildings.Add(
                new BuildingRuntimeState
                {
                    buildingId = buildingId,
                    ownerPlayerId = playerId,
                    archetypeId =
                        definition.archetypeId,
                    role =
                        definition.role,
                    level =
                        Math.Max(
                            1,
                            definition.level),
                    worldX = worldX,
                    worldY = worldY,
                    worldZ = worldZ,
                    yawDegrees = yawDegrees,
                    maxHitPoints = maxHp,
                    hitPoints =
                        requiredWork <= 0.0
                            ? maxHp
                            : Math.Max(
                                1f,
                                maxHp * 0.25f),
                    requiredConstructionWorkHours =
                        requiredWork,
                    completedConstructionWorkHours = 0.0,
                    assignedBuilders =
                        Math.Max(
                            0,
                            assignedBuilders)
                });

            return true;
        }

        public static bool TryUpgradeBuilding(
            SettlementGameplayState settlement,
            int playerId,
            long buildingId,
            int assignedBuilders,
            GameplayRules rules)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));

            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            if (settlement.isNeutral ||
                settlement.ownerPlayerId != playerId)
            {
                return false;
            }

            BuildingRuntimeState building =
                settlement.FindBuilding(
                    buildingId);

            if (building == null ||
                building.IsDestroyed ||
                !building.IsConstructed)
            {
                return false;
            }

            BuildingUpgradeDefinition upgrade =
                rules.FindUpgrade(
                    building.role,
                    building.level);

            if (upgrade == null ||
                upgrade.toLevel <=
                    building.level)
            {
                return false;
            }

            if (!settlement.inventory.TrySpend(
                upgrade.cost))
            {
                return false;
            }

            float oldMaxHp =
                Math.Max(
                    1f,
                    building.maxHitPoints);

            float healthFraction =
                Clamp01(
                    building.hitPoints /
                    oldMaxHp);

            building.level =
                upgrade.toLevel;

            building.archetypeId =
                string.IsNullOrWhiteSpace(
                    upgrade.targetArchetypeId)
                    ? building.archetypeId
                    : upgrade.targetArchetypeId;

            building.maxHitPoints =
                Math.Max(
                    oldMaxHp,
                    upgrade.targetMaxHitPoints);

            building.hitPoints =
                Math.Max(
                    1f,
                    building.maxHitPoints *
                    healthFraction);

            building.requiredConstructionWorkHours =
                Math.Max(
                    0.0,
                    upgrade.constructionWorkHours);

            building.completedConstructionWorkHours =
                0.0;

            building.assignedBuilders =
                Math.Max(
                    0,
                    assignedBuilders);

            return true;
        }

        public static void SetAssignedBuilders(
            SettlementGameplayState settlement,
            long buildingId,
            int assignedBuilders)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));

            BuildingRuntimeState building =
                settlement.FindBuilding(
                    buildingId);

            if (building == null)
                return;

            building.assignedBuilders =
                Math.Max(
                    0,
                    assignedBuilders);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;

            if (value > 1f)
                return 1f;

            return value;
        }
    }
}
