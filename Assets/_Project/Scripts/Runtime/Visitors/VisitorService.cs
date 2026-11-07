using System;
using System.Collections.Generic;
using FarmGame.Buildings;
using FarmGame.Content;

namespace FarmGame.Visitors
{
    public enum VisitorKind
    {
        /// <summary>Asks for goods and pays coins, XP and reputation for them.</summary>
        Request = 0,

        /// <summary>Sells bundles of produce; reputation brings the price down.</summary>
        Seller = 1
    }

    /// <summary>One bundle a seller offers, e.g. 3 Egg Tart.</summary>
    public sealed class VisitorOffer
    {
        public string ItemId { get; }
        public int Amount { get; }

        /// <summary>The bundle's price before any reputation discount.</summary>
        public int BasePrice { get; }

        /// <summary>
        /// The lowest the bundle can go: what selling it would pay. Buying is never cheaper
        /// than selling, so a bundle can't be flipped for profit.
        /// </summary>
        public int MinPrice { get; }

        public bool Sold { get; internal set; }

        public VisitorOffer(string itemId, int amount, int basePrice, int minPrice, bool sold = false)
        {
            ItemId = itemId;
            Amount = Math.Max(1, amount);
            MinPrice = Math.Max(0, minPrice);
            BasePrice = Math.Max(MinPrice, basePrice);
            Sold = sold;
        }

        /// <summary>The price with a reputation discount (0..1) applied.</summary>
        public int PriceWithDiscount(float discount)
        {
            var price = (int)Math.Ceiling(BasePrice * (1.0 - Math.Max(0f, Math.Min(1f, discount))));
            return Math.Max(MinPrice, price);
        }
    }

    /// <summary>
    /// A townsperson standing at the farm. A <see cref="VisitorKind.Request"/> visitor asks for
    /// goods: serving them hands the goods over for the rewards. A <see cref="VisitorKind.Seller"/>
    /// offers bundles to buy. Either way, they leave at <see cref="LeavesAtUtcTicks"/>.
    /// </summary>
    public sealed class Visitor
    {
        private readonly List<ItemAmount> _requests;
        private readonly List<VisitorOffer> _offers;

        public VisitorKind Kind { get; }
        public bool IsSeller => Kind == VisitorKind.Seller;

        /// <summary>What a seller has for sale. Empty for request visitors.</summary>
        public IReadOnlyList<VisitorOffer> Offers => _offers;

        public bool IsSoldOut
        {
            get
            {
                for (var i = 0; i < _offers.Count; i++)
                {
                    if (!_offers[i].Sold)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Unique for the farm; stays the same across saves.</summary>
        public int Id { get; }

        public string Name { get; }

        /// <summary>Which of the <see cref="VisitorService.LookCount"/> character pictures to use.</summary>
        public int Look { get; }

        /// <summary>Where they stand: 0 to <see cref="VisitorBalance.MaxSpots"/> - 1.</summary>
        public int Spot { get; }

        public IReadOnlyList<ItemAmount> Requests => _requests;
        public int RewardCoins { get; }
        public int RewardXp { get; }
        public int RewardReputation { get; }
        public long ArrivedAtUtcTicks { get; }
        public long LeavesAtUtcTicks { get; internal set; }

        public Visitor(
            int id,
            string name,
            int look,
            int spot,
            IEnumerable<ItemAmount> requests,
            int rewardCoins,
            int rewardXp,
            int rewardReputation,
            long arrivedAtUtcTicks,
            long leavesAtUtcTicks,
            VisitorKind kind = VisitorKind.Request,
            IEnumerable<VisitorOffer> offers = null)
        {
            Kind = kind;
            _offers = new List<VisitorOffer>(offers ?? Array.Empty<VisitorOffer>());
            Id = id;
            Name = string.IsNullOrEmpty(name) ? "Visitor" : name;
            Look = Math.Max(0, look);
            Spot = Math.Max(0, spot);
            _requests = new List<ItemAmount>(requests ?? Array.Empty<ItemAmount>());
            RewardCoins = Math.Max(0, rewardCoins);
            RewardXp = Math.Max(0, rewardXp);
            RewardReputation = Math.Max(0, rewardReputation);
            ArrivedAtUtcTicks = arrivedAtUtcTicks;
            LeavesAtUtcTicks = leavesAtUtcTicks;
        }
    }

    /// <summary>
    /// Visitors come to the farm every few minutes (up to <see cref="MaxVisitors"/> at a time)
    /// and ask for goods or fruit: things made in goods buildings the player owns, from recipes
    /// they've unlocked, and fruit from trees they own. Until the player owns one of those, only
    /// sellers come. Each visitor waits <see cref="VisitorBalance.Stay"/> and then leaves; the
    /// player can also send them away.
    ///
    /// Serving a visitor pays fewer coins than selling the goods would, but more XP than making
    /// them, plus reputation. The amounts come from the balance settings
    /// (<see cref="ContentDatabase.Visitors"/>).
    /// </summary>
    public sealed class VisitorService
    {
        /// <summary>How many different character pictures there are (visitor_0 ... visitor_3).</summary>
        public const int LookCount = 4;

        internal static readonly string[] Names =
        {
            "Mabel", "Otis", "Rosa", "Felix", "Hazel", "Jasper", "Ivy", "Theo",
            "Clementine", "Barnaby", "Poppy", "Rufus", "Wren", "Edgar", "Juniper", "Silas"
        };

        private readonly ContentDatabase _content;
        private readonly BuildingService _buildings;
        private readonly Func<int> _level;
        private readonly Func<long> _now;
        private readonly VisitorBalance _settings;
        private readonly List<Visitor> _visitors = new List<Visitor>();

        /// <summary>Raised whenever a visitor arrives, is served, is sent away or leaves.</summary>
        public event Action Changed;

        public IReadOnlyList<Visitor> Visitors => _visitors;
        public int MaxVisitors => _settings.MaxVisitors;
        public VisitorBalance Settings => _settings;

        /// <summary>When the next visitor may arrive (UTC ticks).</summary>
        public long NextArrivalAtUtcTicks { get; private set; }

        /// <summary>How many visitors the player has helped.</summary>
        public int ServedCount { get; private set; }

        /// <summary>Visitors ever generated; seeds the next one, so a reload gives the same sequence.</summary>
        public int GeneratedCount { get; private set; }

        public VisitorService(ContentDatabase content, BuildingService buildings, Func<int> level, Func<long> nowTicks)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _level = level ?? (() => 1);
            _now = nowTicks ?? throw new ArgumentNullException(nameof(nowTicks));
            _settings = _content.Visitors;
        }

        public Visitor Find(int id)
        {
            for (var i = 0; i < _visitors.Count; i++)
            {
                if (_visitors[i].Id == id)
                {
                    return _visitors[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Sends off visitors who waited too long and lets a new one in when one is due and
        /// there's room. Call regularly; it also catches up on time that passed while the game
        /// was closed. Returns true when anything changed.
        /// </summary>
        public bool Update()
        {
            var now = _now();
            var changed = false;
            for (var i = _visitors.Count - 1; i >= 0; i--)
            {
                if (now >= _visitors[i].LeavesAtUtcTicks)
                {
                    _visitors.RemoveAt(i);
                    DelayNextArrival(now);
                    changed = true;
                }
            }

            if (_visitors.Count < MaxVisitors && now >= NextArrivalAtUtcTicks)
            {
                var visitor = Generate(now);
                if (visitor != null)
                {
                    _visitors.Add(visitor);
                    NextArrivalAtUtcTicks = now + RandomArrivalGap(visitor.Id).Ticks;
                    changed = true;
                }
                else
                {
                    // Nothing to ask for or sell yet. Check again in a minute.
                    NextArrivalAtUtcTicks = now + TimeSpan.TicksPerMinute;
                }
            }

            if (changed)
            {
                Changed?.Invoke();
            }

            return changed;
        }

        /// <summary>Takes a visitor off the farm because they were served. Rewards are paid by the caller.</summary>
        public bool TryCompleteServe(int id, out Visitor served)
        {
            served = Find(id);
            if (served == null)
            {
                return false;
            }

            _visitors.Remove(served);
            ServedCount++;
            DelayNextArrival(_now());
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Marks one of a seller's bundles as sold (the caller takes the coins and adds the
        /// items). A seller with nothing left goes home. Returns false if it can't be sold.
        /// </summary>
        public bool TryMarkSold(int id, int offerIndex, out bool soldOut)
        {
            soldOut = false;
            var visitor = Find(id);
            if (visitor == null || !visitor.IsSeller || offerIndex < 0 || offerIndex >= visitor.Offers.Count ||
                visitor.Offers[offerIndex].Sold)
            {
                return false;
            }

            visitor.Offers[offerIndex].Sold = true;
            if (visitor.IsSoldOut)
            {
                soldOut = true;
                _visitors.Remove(visitor);
                DelayNextArrival(_now());
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Sends a visitor away without serving them.</summary>
        public bool TrySendAway(int id)
        {
            var visitor = Find(id);
            if (visitor == null)
            {
                return false;
            }

            _visitors.Remove(visitor);
            DelayNextArrival(_now());
            Changed?.Invoke();
            return true;
        }

        /// <summary>Puts back a saved visitor. One asking for unknown items, or with no room, is dropped.</summary>
        public void Restore(Visitor visitor)
        {
            if (visitor == null || _visitors.Count >= MaxVisitors ||
                visitor.Spot >= VisitorBalance.MaxSpots || Find(visitor.Id) != null || IsSpotTaken(visitor.Spot))
            {
                return;
            }

            if (visitor.IsSeller)
            {
                if (visitor.Offers.Count == 0 || visitor.IsSoldOut)
                {
                    return;
                }

                for (var i = 0; i < visitor.Offers.Count; i++)
                {
                    if (!IsKnownItem(visitor.Offers[i].ItemId))
                    {
                        return;
                    }
                }
            }
            else
            {
                if (visitor.Requests.Count == 0)
                {
                    return;
                }

                for (var i = 0; i < visitor.Requests.Count; i++)
                {
                    if (_content.GetProduct(visitor.Requests[i].itemId) == null)
                    {
                        return;
                    }
                }
            }

            _visitors.Add(visitor);
        }

        public void RestoreState(long nextArrivalAtUtcTicks, int servedCount, int generatedCount)
        {
            NextArrivalAtUtcTicks = Math.Max(0L, nextArrivalAtUtcTicks);
            ServedCount = Math.Max(0, servedCount);
            GeneratedCount = Math.Max(0, generatedCount);
        }

        /// <summary>Moves every arrival and departure earlier, as if time had passed (debug).</summary>
        public void DebugAdvance(long ticks)
        {
            NextArrivalAtUtcTicks -= ticks;
            for (var i = 0; i < _visitors.Count; i++)
            {
                _visitors[i].LeavesAtUtcTicks -= ticks;
            }
        }

        // ------------------------------------------------------------------ Generation

        /// <summary>After someone leaves, the next visitor takes a little while to turn up.</summary>
        private void DelayNextArrival(long now)
        {
            var soonest = now + _settings.ArrivalMin.Ticks / 2;
            if (NextArrivalAtUtcTicks < soonest)
            {
                NextArrivalAtUtcTicks = soonest;
            }
        }

        private TimeSpan RandomArrivalGap(int seed)
        {
            var random = new Random(unchecked(seed * 104729 + 3));
            var min = _settings.ArrivalMin.Ticks;
            var max = _settings.ArrivalMax.Ticks;
            return TimeSpan.FromTicks(min + (long)(random.NextDouble() * (max - min)));
        }

        private bool IsSpotTaken(int spot)
        {
            for (var i = 0; i < _visitors.Count; i++)
            {
                if (_visitors[i].Spot == spot)
                {
                    return true;
                }
            }

            return false;
        }

        private Visitor Generate(long now)
        {
            var options = AvailableRequests();
            var wares = AvailableWares();
            if (options.Count == 0 && wares.Count == 0)
            {
                return null;
            }

            var spot = -1;
            for (var s = 0; s < MaxVisitors; s++)
            {
                if (!IsSpotTaken(s))
                {
                    spot = s;
                    break;
                }
            }

            if (spot < 0)
            {
                return null;
            }

            var id = GeneratedCount;
            var random = new Random(unchecked(id * 7907 + 31));
            GeneratedCount++;
            var level = Math.Max(1, _level());

            // Some visitors come to sell. Until the farm has a goods building or a fruit tree
            // there's nothing to ask for, so everyone who comes is selling.
            var seller = options.Count == 0 ||
                         (wares.Count > 0 && random.NextDouble() < Math.Max(0f, _settings.sellerChance));
            if (seller)
            {
                return GenerateSeller(id, spot, now, random, wares);
            }

            // One kind of good early on; up to two, then three, as the farm grows.
            var maxKinds = level >= 8 ? 3 : 2;
            var kinds = Math.Min(options.Count, 1 + random.Next(maxKinds));

            var requests = new List<ItemAmount>();
            var value = 0L;
            var makeXp = 0L;
            var items = 0;
            for (var i = 0; i < kinds; i++)
            {
                var product = options[random.Next(options.Count)];
                options.Remove(product);

                // Cheap goods are asked for in bigger batches, plus one more every few levels.
                var amount = Math.Max(1, Math.Min(3, (int)Math.Round(500.0 / Math.Max(1, product.SellPrice))));
                amount += (level - 1) / 6;
                if (_content.GetFruitInput(_content.GetGoodsRecipe(product.Id)) != null)
                {
                    amount = Math.Min(amount, ContentDatabase.MaxFruitGoodsPerRequest);
                }

                requests.Add(new ItemAmount(product.Id, amount));

                value += (long)product.SellPrice * amount;
                makeXp += (long)Math.Max(1, product.CollectXp) * amount;
                items += amount;
            }

            var coins = (int)Math.Min(int.MaxValue, Math.Round(value * Math.Max(0f, _settings.coinMultiplier)));
            var xp = (int)Math.Min(int.MaxValue, Math.Round((5 + makeXp) * Math.Max(0f, _settings.xpMultiplier)));
            var reputation = Math.Max(0, _settings.reputationBase) + Math.Max(0, _settings.reputationPerItem) * items;

            return new Visitor(
                id,
                Names[random.Next(Names.Length)],
                random.Next(LookCount),
                spot,
                requests,
                coins,
                xp,
                reputation,
                now,
                now + _settings.Stay.Ticks);
        }

        /// <summary>Something a seller can bring: an item and what selling one pays.</summary>
        private sealed class Ware
        {
            public string Id;
            public int SellPrice;
            public bool IsCrop;
        }

        /// <summary>
        /// A seller with one to three bundles. A bundle costs <see cref="VisitorBalance.sellerPriceMultiplier"/>
        /// times what selling it would pay, before the reputation discount, and never less than that.
        /// </summary>
        private Visitor GenerateSeller(int id, int spot, long now, Random random, List<Ware> wares)
        {
            var count = Math.Min(wares.Count, 1 + random.Next(3));
            var offers = new List<VisitorOffer>();
            for (var i = 0; i < count; i++)
            {
                var ware = wares[random.Next(wares.Count)];
                wares.Remove(ware);

                var price = Math.Max(1, ware.SellPrice);
                var amount = ware.IsCrop
                    ? Math.Max(3, Math.Min(10, (int)Math.Round(80.0 / price)))
                    : Math.Max(1, Math.Min(5, (int)Math.Round(400.0 / price)));
                var sellValue = (int)Math.Min(int.MaxValue, (long)price * amount);
                var basePrice = (int)Math.Min(int.MaxValue, Math.Round(sellValue * Math.Max(1f, _settings.sellerPriceMultiplier)));
                offers.Add(new VisitorOffer(ware.Id, amount, basePrice, sellValue));
            }

            return new Visitor(
                id,
                Names[random.Next(Names.Length)],
                random.Next(LookCount),
                spot,
                null,
                0,
                0,
                0,
                now,
                now + _settings.Stay.Ticks,
                VisitorKind.Seller,
                offers);
        }

        /// <summary>
        /// What sellers can bring: unlocked crops, products of unlocked animals and unlocked goods,
        /// whether or not the player owns the buildings yet.
        /// </summary>
        private List<Ware> AvailableWares()
        {
            var level = Math.Max(1, _level());
            var wares = new List<Ware>();
            var seen = new HashSet<string>();

            for (var i = 0; i < _content.Crops.Count; i++)
            {
                var crop = _content.Crops[i];
                if (crop != null && crop.UnlockLevel <= level && seen.Add(crop.Id))
                {
                    wares.Add(new Ware { Id = crop.Id, SellPrice = crop.SellPrice, IsCrop = true });
                }
            }

            for (var i = 0; i < _content.Buildings.Count; i++)
            {
                var building = _content.Buildings[i];
                if (building == null || building.UnlockLevel > level || !(building.CanHouseAnimals || building.IsFruitTree))
                {
                    continue;
                }

                var product = _content.GetProduct(building.ProductId);
                if (product != null && seen.Add(product.Id))
                {
                    wares.Add(new Ware { Id = product.Id, SellPrice = product.SellPrice });
                }
            }

            for (var i = 0; i < _content.GoodsRecipes.Count; i++)
            {
                var recipe = _content.GoodsRecipes[i];
                var building = recipe != null ? _content.GetBuilding(recipe.BuildingId) : null;
                var product = recipe != null ? _content.GetProduct(recipe.OutputId) : null;
                if (product != null && building != null && recipe.UnlockLevel <= level &&
                    building.UnlockLevel <= level && seen.Add(product.Id))
                {
                    wares.Add(new Ware { Id = product.Id, SellPrice = product.SellPrice });
                }
            }

            return wares;
        }

        private bool IsKnownItem(string itemId)
        {
            return _content.GetCrop(itemId) != null || _content.GetProduct(itemId) != null;
        }

        /// <summary>
        /// What visitors can ask for right now: unlocked recipes in goods buildings the player
        /// owns (for a juice, only with its fruit's tree), and the fruit of unlocked trees they own.
        /// </summary>
        private List<ProductDefinition> AvailableRequests()
        {
            var level = Math.Max(1, _level());
            var goods = new List<ProductDefinition>();
            var seenBuildings = new HashSet<string>();
            for (var i = 0; i < _buildings.Placed.Count; i++)
            {
                var definition = _buildings.Placed[i].Definition;
                if (definition == null || !seenBuildings.Add(definition.Id))
                {
                    continue;
                }

                if (definition.IsFruitTree)
                {
                    var fruit = _content.GetProduct(definition.ProductId);
                    if (fruit != null && definition.UnlockLevel <= level && !goods.Contains(fruit))
                    {
                        goods.Add(fruit);
                    }

                    continue;
                }

                if (definition.Category != BuildingCategory.Goods)
                {
                    continue;
                }

                foreach (var recipe in _content.GetGoodsRecipesFor(definition.Id))
                {
                    // A juice can only be made with its fruit, so it takes that fruit's tree too.
                    var fruit = _content.GetFruitInput(recipe);
                    if (fruit != null && !_buildings.OwnsFruitTree(fruit))
                    {
                        continue;
                    }

                    var product = _content.GetProduct(recipe.OutputId);
                    if (product != null && recipe.UnlockLevel <= level && !goods.Contains(product))
                    {
                        goods.Add(product);
                    }
                }
            }

            return goods;
        }
    }
}
