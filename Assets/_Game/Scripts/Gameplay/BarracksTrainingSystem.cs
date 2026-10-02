using System;

namespace LittleCastle.Gameplay
{
    public static class BarracksTrainingSystem
    {
        public static bool TryEnqueue(
            GameplaySessionState session,
            SettlementGameplayState settlement,
            long barracksBuildingId,
            string unitArchetypeId,
            int quantity,
            GameplayRules rules,
            out long orderId)
        {
            orderId = 0;

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));

            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            if (quantity <= 0)
                return false;

            BuildingRuntimeState barracks =
                settlement.FindBuilding(
                    barracksBuildingId);

            if (barracks == null ||
                barracks.role !=
                    BuildingRole.Barracks ||
                !barracks.IsConstructed ||
                barracks.IsDestroyed)
            {
                return false;
            }

            UnitTrainingDefinition definition =
                rules.FindUnitTraining(
                    unitArchetypeId);

            if (definition == null ||
                definition.trainingGameHours <= 0.0)
            {
                return false;
            }

            ResourceAmount[] totalCost =
                BuildScaledCost(
                    definition,
                    quantity);

            if (!settlement.inventory.TrySpend(
                totalCost))
            {
                return false;
            }

            BarracksQueueState queue =
                GetOrCreateQueue(
                    settlement,
                    barracksBuildingId);

            orderId =
                session.AllocateRuntimeId();

            queue.orders.Add(
                new TrainingOrderState
                {
                    orderId = orderId,
                    unitArchetypeId =
                        unitArchetypeId,
                    quantity = quantity,
                    remainingGameHours =
                        definition.trainingGameHours *
                        quantity
                });

            return true;
        }

        public static void Advance(
            SettlementGameplayState settlement,
            double gameHours)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));

            if (gameHours <= 0.0)
                return;

            for (int q = 0;
                 q < settlement.barracksQueues.Count;
                 q++)
            {
                BarracksQueueState queue =
                    settlement.barracksQueues[q];

                if (queue == null ||
                    queue.orders.Count == 0)
                {
                    continue;
                }

                BuildingRuntimeState barracks =
                    settlement.FindBuilding(
                        queue.barracksBuildingId);

                if (barracks == null ||
                    barracks.IsDestroyed ||
                    !barracks.IsConstructed)
                {
                    continue;
                }

                double remainingStep =
                    gameHours;

                while (remainingStep > 0.0 &&
                       queue.orders.Count > 0)
                {
                    TrainingOrderState order =
                        queue.orders[0];

                    double consumed =
                        Math.Min(
                            remainingStep,
                            order.remainingGameHours);

                    order.remainingGameHours -=
                        consumed;

                    remainingStep -=
                        consumed;

                    if (order.remainingGameHours >
                        0.0000001)
                    {
                        break;
                    }

                    settlement.AddUnit(
                        order.unitArchetypeId,
                        order.quantity);

                    queue.orders.RemoveAt(0);
                }
            }
        }

        private static BarracksQueueState GetOrCreateQueue(
            SettlementGameplayState settlement,
            long barracksBuildingId)
        {
            for (int i = 0;
                 i < settlement.barracksQueues.Count;
                 i++)
            {
                BarracksQueueState queue =
                    settlement.barracksQueues[i];

                if (queue.barracksBuildingId ==
                    barracksBuildingId)
                {
                    return queue;
                }
            }

            var created =
                new BarracksQueueState
                {
                    barracksBuildingId =
                        barracksBuildingId
                };

            settlement.barracksQueues.Add(
                created);

            return created;
        }

        private static ResourceAmount[] BuildScaledCost(
            UnitTrainingDefinition definition,
            int quantity)
        {
            ResourceAmount[] result =
                new ResourceAmount[
                    definition.cost.Count];

            for (int i = 0;
                 i < definition.cost.Count;
                 i++)
            {
                ResourceAmount item =
                    definition.cost[i];

                long scaled =
                    (long)item.amount *
                    quantity;

                result[i] =
                    new ResourceAmount(
                        item.resource,
                        scaled > int.MaxValue
                            ? int.MaxValue
                            : (int)scaled);
            }

            return result;
        }
    }
}
