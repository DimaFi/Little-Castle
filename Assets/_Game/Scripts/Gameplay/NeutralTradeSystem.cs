using System;

namespace LittleCastle.Gameplay
{
    public static class NeutralTradeSystem
    {
        public static bool TryBuyFromVillage(
            SettlementGameplayState buyer,
            SettlementGameplayState neutralVillage,
            string offerId,
            int quantity)
        {
            if (buyer == null)
                throw new ArgumentNullException(nameof(buyer));

            if (neutralVillage == null)
                throw new ArgumentNullException(nameof(neutralVillage));

            if (!neutralVillage.isNeutral ||
                quantity <= 0)
            {
                return false;
            }

            TradeOfferState offer =
                FindOffer(
                    neutralVillage,
                    offerId);

            if (offer == null ||
                offer.availableUnits < quantity ||
                offer.priceCoinsPerUnit < 0)
            {
                return false;
            }

            long totalPriceLong =
                (long)offer.priceCoinsPerUnit *
                quantity;

            if (totalPriceLong > int.MaxValue)
                return false;

            int totalPrice =
                (int)totalPriceLong;

            if (!buyer.inventory.TryRemove(
                GameplayResourceType.Coin,
                totalPrice))
            {
                return false;
            }

            buyer.inventory.Add(
                offer.resource,
                quantity);

            neutralVillage.inventory.Add(
                GameplayResourceType.Coin,
                totalPrice);

            offer.availableUnits -=
                quantity;

            return true;
        }

        public static void AdvanceMarket(
            SettlementGameplayState neutralVillage,
            double gameHours)
        {
            if (neutralVillage == null)
                throw new ArgumentNullException(nameof(neutralVillage));

            if (!neutralVillage.isNeutral ||
                gameHours <= 0.0)
            {
                return;
            }

            for (int i = 0;
                 i < neutralVillage.market.offers.Count;
                 i++)
            {
                TradeOfferState offer =
                    neutralVillage.market.offers[i];

                if (offer == null ||
                    offer.restockUnitsPerGameDay <= 0.0 ||
                    offer.availableUnits >= offer.maxUnits)
                {
                    continue;
                }

                offer.restockRemainder +=
                    offer.restockUnitsPerGameDay *
                    (gameHours / 24.0);

                int restocked =
                    (int)Math.Floor(
                        offer.restockRemainder);

                if (restocked <= 0)
                    continue;

                int room =
                    Math.Max(
                        0,
                        offer.maxUnits -
                        offer.availableUnits);

                int applied =
                    Math.Min(
                        restocked,
                        room);

                offer.availableUnits +=
                    applied;

                offer.restockRemainder -=
                    applied;
            }
        }

        private static TradeOfferState FindOffer(
            SettlementGameplayState village,
            string offerId)
        {
            for (int i = 0;
                 i < village.market.offers.Count;
                 i++)
            {
                TradeOfferState offer =
                    village.market.offers[i];

                if (offer != null &&
                    offer.offerId == offerId)
                {
                    return offer;
                }
            }

            return null;
        }
    }
}
