using System.Collections.Generic;
using FarmGame.Content;
using FarmGame.Visitors;

namespace FarmGame.Core
{
    /// <summary>Visitors: helping them with goods, or sending them on their way.</summary>
    public sealed partial class GameSession
    {
        /// <summary>Lets visitors arrive and leave as time passes. Call regularly (the HUD does).</summary>
        public void UpdateVisitors()
        {
            if (Visitors.Update())
            {
                SaveSoon();
            }
        }

        /// <summary>Whether the player has everything this visitor asked for. Cheap enough to call every frame.</summary>
        public bool CanServeVisitor(Visitor visitor)
        {
            if (visitor == null || visitor.IsSeller)
            {
                return false;
            }

            for (var i = 0; i < visitor.Requests.Count; i++)
            {
                if (Inventory.GetCount(visitor.Requests[i].itemId) < visitor.Requests[i].amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether the player has everything this visitor asked for. <paramref name="missing"/>
        /// lists what's short, as (item, how many more are needed).
        /// </summary>
        public bool CanServeVisitor(Visitor visitor, out List<ItemAmount> missing)
        {
            missing = new List<ItemAmount>();
            if (visitor == null || visitor.IsSeller)
            {
                return false;
            }

            for (var i = 0; i < visitor.Requests.Count; i++)
            {
                var request = visitor.Requests[i];
                var short_ = request.amount - Inventory.GetCount(request.itemId);
                if (short_ > 0)
                {
                    missing.Add(new ItemAmount(request.itemId, short_));
                }
            }

            return missing.Count == 0;
        }

        /// <summary>
        /// Hands a visitor what they asked for and pays their reward: coins, XP and reputation.
        /// Nothing changes unless the player has every item.
        /// </summary>
        public bool TryServeVisitor(int visitorId, out string message)
        {
            var visitor = Visitors.Find(visitorId);
            if (visitor == null)
            {
                message = "That visitor has already left.";
                return false;
            }

            if (visitor.IsSeller)
            {
                message = $"{visitor.Name} is here to sell, not to buy.";
                return false;
            }

            if (!CanServeVisitor(visitor, out var missing))
            {
                var first = missing[0];
                message = $"Need {first.amount} more {_content.GetItemDisplayName(first.itemId)}";
                return false;
            }

            for (var i = 0; i < visitor.Requests.Count; i++)
            {
                Inventory.TryRemove(visitor.Requests[i].itemId, visitor.Requests[i].amount);
            }

            Visitors.TryCompleteServe(visitorId, out _);
            Wallet.Add(visitor.RewardCoins);
            Reputation.Add(visitor.RewardReputation);
            Level.AddXp(visitor.RewardXp);

            message =
                $"{visitor.Name} thanks you! +{FormatCoins(visitor.RewardCoins)} +{visitor.RewardXp} XP +{visitor.RewardReputation} reputation" +
                RollForDeed(_content.Land.visitorDeedChance);
            SaveSoon();
            return true;
        }

        // ------------------------------------------------------------------ Sellers

        /// <summary>The discount (0..1) sellers give right now, from the player's reputation.</summary>
        public float VisitorDiscount => Visitors.Settings.DiscountFor(Reputation.Points);

        /// <summary>What one of a seller's bundles costs right now, with the reputation discount.</summary>
        public int GetOfferPrice(VisitorOffer offer)
        {
            return offer != null ? offer.PriceWithDiscount(VisitorDiscount) : 0;
        }

        /// <summary>
        /// Buys one of a seller's bundles: pays the discounted price and puts the items in
        /// storage. Nothing changes if the player can't afford it or storage is too full.
        /// A seller with nothing left goes home. Buying never gives reputation.
        /// </summary>
        public bool TryBuyFromVisitor(int visitorId, int offerIndex, out string message)
        {
            var visitor = Visitors.Find(visitorId);
            if (visitor == null || !visitor.IsSeller)
            {
                message = "That visitor has already left.";
                return false;
            }

            if (offerIndex < 0 || offerIndex >= visitor.Offers.Count || visitor.Offers[offerIndex].Sold)
            {
                message = "That's already sold.";
                return false;
            }

            var offer = visitor.Offers[offerIndex];
            var name = _content.GetItemDisplayName(offer.ItemId);
            if (!Storage.CanFit(offer.Amount))
            {
                message = "Not enough storage.";
                return false;
            }

            var price = GetOfferPrice(offer);
            if (!Wallet.TrySpend(price))
            {
                message = $"Need {FormatCoins(price)}";
                return false;
            }

            Visitors.TryMarkSold(visitorId, offerIndex, out var soldOut);
            Inventory.Add(offer.ItemId, offer.Amount);

            var saved = offer.BasePrice - price;
            message = saved > 0
                ? $"Bought {offer.Amount} {name} for {FormatCoins(price)} (saved {FormatCoins(saved)})."
                : $"Bought {offer.Amount} {name} for {FormatCoins(price)}.";
            if (soldOut)
            {
                message += $" {visitor.Name} sold out and heads home.";
            }

            SaveSoon();
            return true;
        }

        /// <summary>Sends a visitor away without helping. No reward, no penalty.</summary>
        public bool TrySendAwayVisitor(int visitorId, out string message)
        {
            var visitor = Visitors.Find(visitorId);
            if (visitor == null || !Visitors.TrySendAway(visitorId))
            {
                message = "That visitor has already left.";
                return false;
            }

            message = $"{visitor.Name} waves goodbye.";
            SaveSoon();
            return true;
        }
    }
}
