using System;
using System.Collections.Generic;

namespace LittleCastle.Gameplay
{
    public enum GameplayResourceType : byte
    {
        Wheat = 0,
        Bread = 1,
        Wood = 2,
        Stone = 3,
        Iron = 4,
        Coin = 5
    }

    [Serializable]
    public struct ResourceAmount
    {
        public GameplayResourceType resource;
        public int amount;

        public ResourceAmount(
            GameplayResourceType resource,
            int amount)
        {
            this.resource = resource;
            this.amount = amount;
        }
    }

    /// <summary>
    /// Small serializable authoritative inventory.
    ///
    /// The list is intentionally the save/network payload. Gameplay code must
    /// not store references to scene objects or UI inventory widgets here.
    /// </summary>
    [Serializable]
    public sealed class ResourceInventory
    {
        public List<ResourceAmount> entries =
            new List<ResourceAmount>();

        public int Get(GameplayResourceType resource)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].resource == resource)
                    return Math.Max(0, entries[i].amount);
            }

            return 0;
        }

        public void Set(
            GameplayResourceType resource,
            int amount)
        {
            int safeAmount =
                Math.Max(0, amount);

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].resource != resource)
                    continue;

                if (safeAmount == 0)
                {
                    entries.RemoveAt(i);
                    return;
                }

                ResourceAmount entry =
                    entries[i];

                entry.amount = safeAmount;
                entries[i] = entry;
                return;
            }

            if (safeAmount > 0)
            {
                entries.Add(
                    new ResourceAmount(
                        resource,
                        safeAmount));
            }
        }

        public void Add(
            GameplayResourceType resource,
            int amount)
        {
            if (amount == 0)
                return;

            long next =
                (long)Get(resource) +
                amount;

            if (next < 0)
                next = 0;

            if (next > int.MaxValue)
                next = int.MaxValue;

            Set(
                resource,
                (int)next);
        }

        public bool TryRemove(
            GameplayResourceType resource,
            int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            int current =
                Get(resource);

            if (current < amount)
                return false;

            Set(
                resource,
                current - amount);

            return true;
        }

        public bool CanAfford(
            IReadOnlyList<ResourceAmount> cost)
        {
            if (cost == null)
                return true;

            for (int resourceIndex = 0;
                 resourceIndex <= (int)GameplayResourceType.Coin;
                 resourceIndex++)
            {
                GameplayResourceType resource =
                    (GameplayResourceType)resourceIndex;

                long requiredTotal = 0;

                for (int i = 0; i < cost.Count; i++)
                {
                    ResourceAmount required =
                        cost[i];

                    if (required.resource == resource &&
                        required.amount > 0)
                    {
                        requiredTotal +=
                            required.amount;
                    }
                }

                if (requiredTotal >
                    Get(resource))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TrySpend(
            IReadOnlyList<ResourceAmount> cost)
        {
            if (!CanAfford(cost))
                return false;

            if (cost == null)
                return true;

            for (int resourceIndex = 0;
                 resourceIndex <= (int)GameplayResourceType.Coin;
                 resourceIndex++)
            {
                GameplayResourceType resource =
                    (GameplayResourceType)resourceIndex;

                long requiredTotal = 0;

                for (int i = 0; i < cost.Count; i++)
                {
                    ResourceAmount required =
                        cost[i];

                    if (required.resource == resource &&
                        required.amount > 0)
                    {
                        requiredTotal +=
                            required.amount;
                    }
                }

                if (requiredTotal > 0)
                {
                    TryRemove(
                        resource,
                        requiredTotal > int.MaxValue
                            ? int.MaxValue
                            : (int)requiredTotal);
                }
            }

            return true;
        }
    }
}
