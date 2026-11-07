using System;
using System.Collections.Generic;
using FarmGame.Buildings;
using FarmGame.Content;

namespace FarmGame.Missions
{
    /// <summary>One line of an order: how many of a crop or product it asks for.</summary>
    public sealed class MissionRequirement
    {
        /// <summary>The crop or product.</summary>
        public string ItemId { get; }

        public int Required { get; }

        public MissionRequirement(string itemId, int required)
        {
            ItemId = itemId;
            Required = Math.Max(1, required);
        }
    }

    /// <summary>
    /// An order pinned to the board: one to three crops or products to hand over, and a reward.
    /// It stays up until the player fills or discards it.
    /// </summary>
    public sealed class Mission
    {
        private readonly List<MissionRequirement> _requirements;

        public IReadOnlyList<MissionRequirement> Requirements => _requirements;
        public int RewardCoins { get; }
        public int RewardXp { get; }

        public Mission(IEnumerable<MissionRequirement> requirements, int rewardCoins, int rewardXp)
        {
            _requirements = new List<MissionRequirement>(requirements ?? Array.Empty<MissionRequirement>());
            RewardCoins = Math.Max(0, rewardCoins);
            RewardXp = Math.Max(0, rewardXp);
        }
    }

    /// <summary>
    /// The order board. It has <see cref="SlotCount"/> slots; each holds an order or waits to be
    /// filled. A slot gets a new order a while after its last one was filled or discarded. The
    /// slot count and waits come from the balance settings (<see cref="ContentDatabase.Notices"/>).
    ///
    /// Orders only ask for unlocked things the player can make right now: unlocked crops, unlocked
    /// products of animals they own, and unlocked goods of goods buildings they own. A good made
    /// from fruit (a juice) also needs that fruit's tree on the farm. Orders tend to be themed: after
    /// the first item, later ones often belong to the same production chain (Goat Yogurt with the
    /// strawberries that go into it).
    /// </summary>
    public sealed class MissionService
    {
        /// <summary>How many orders the board holds.</summary>
        public int SlotCount { get; }

        /// <summary>Wait before a slot gets a new order after one is filled.</summary>
        public TimeSpan RefillAfterClaim { get; }

        /// <summary>Wait after an order is discarded, so it isn't free to reroll.</summary>
        public TimeSpan RefillAfterDiscard { get; }

        /// <summary>Chance that each extra item is picked from the same chain as the others.</summary>
        private const double RelatedChance = 0.6;

        /// <summary>
        /// Coins paid per coin of sell value handed over. The items are given up, so this has to
        /// beat selling them or nobody would fill an order.
        /// </summary>
        private const double RewardPerSellValue = 1.6;

        private readonly ContentDatabase _content;
        private readonly BuildingService _buildings;
        private readonly Func<int> _level;
        private readonly Func<long> _now;
        private readonly Mission[] _slots;
        private readonly long[] _nextPostAt;

        /// <summary>Raised whenever a notice is posted, claimed or discarded.</summary>
        public event Action Changed;

        /// <summary>How many notices the player has claimed so far.</summary>
        public int CompletedCount { get; private set; }

        /// <summary>
        /// Counts notices ever generated. It seeds the random choice of the next notice, so the
        /// sequence is the same after reloading a save.
        /// </summary>
        public int GeneratedCount { get; private set; }

        public MissionService(ContentDatabase content, BuildingService buildings, Func<int> level, Func<long> nowTicks)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _level = level ?? (() => 1);
            _now = nowTicks ?? throw new ArgumentNullException(nameof(nowTicks));

            var settings = _content.Notices;
            SlotCount = settings.Slots;
            RefillAfterClaim = settings.RefillAfterClaim;
            RefillAfterDiscard = settings.RefillAfterDiscard;
            _slots = new Mission[SlotCount];
            _nextPostAt = new long[SlotCount];
        }

        /// <summary>The notice in a slot, or null while the slot waits for a new one.</summary>
        public Mission GetSlot(int index)
        {
            return index >= 0 && index < SlotCount ? _slots[index] : null;
        }

        /// <summary>When an empty slot gets its next notice (UTC ticks).</summary>
        public long GetNextPostAt(int index)
        {
            return index >= 0 && index < SlotCount ? _nextPostAt[index] : 0L;
        }

        public int ActiveCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < SlotCount; i++)
                {
                    if (_slots[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Puts back a saved slot. Orders never time out, so one with unknown or still locked
        /// items (from an older save) would stay forever: it's dropped and replaced straight away.
        /// </summary>
        public void RestoreSlot(int index, Mission mission, long nextPostAtTicks)
        {
            if (index < 0 || index >= SlotCount)
            {
                return;
            }

            var dropped = mission != null && !AllItemsUnlocked(mission);
            _slots[index] = dropped ? null : mission;
            _nextPostAt[index] = dropped ? 0L : Math.Max(0L, nextPostAtTicks);
        }

        public void RestoreCounters(int completedCount, int generatedCount)
        {
            CompletedCount = Math.Max(0, completedCount);
            GeneratedCount = Math.Max(0, generatedCount);
        }

        /// <summary>
        /// Posts new notices where a slot is due. Call regularly; it also catches up on time that
        /// passed while the game was closed. Returns true when anything changed.
        /// </summary>
        public bool Update()
        {
            var now = _now();
            var changed = false;
            for (var i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == null && now >= _nextPostAt[i])
                {
                    _slots[i] = Generate();
                    if (_slots[i] != null)
                    {
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                Changed?.Invoke();
            }

            return changed;
        }

        /// <summary>Takes a filled order off the board. Its slot waits for a new one.</summary>
        public bool TryClaim(int index, out Mission claimed)
        {
            claimed = GetSlot(index);
            if (claimed == null)
            {
                return false;
            }

            _slots[index] = null;
            _nextPostAt[index] = _now() + RefillAfterClaim.Ticks;
            CompletedCount++;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Tears down a notice the player doesn't want. The slot refills after a wait.</summary>
        public bool TryDiscard(int index)
        {
            if (GetSlot(index) == null)
            {
                return false;
            }

            _slots[index] = null;
            _nextPostAt[index] = _now() + RefillAfterDiscard.Ticks;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Moves every refill time earlier, as if time had passed (debug).</summary>
        public void DebugAdvance(long ticks)
        {
            for (var i = 0; i < SlotCount; i++)
            {
                _nextPostAt[i] -= ticks;
            }
        }

        /// <summary>
        /// A heading for an order, from its most involved item: a goods building's order
        /// ("Dairy order"), an animal request ("Goat request") or fresh produce.
        /// </summary>
        public string GetTitle(Mission mission)
        {
            string best = null;
            for (var i = 0; i < mission.Requirements.Count; i++)
            {
                var itemId = mission.Requirements[i].ItemId;
                if (best == null || SourceOf(itemId) > SourceOf(best))
                {
                    best = itemId;
                }
            }

            if (best == null)
            {
                return "Order";
            }

            switch (SourceOf(best))
            {
                case ItemSource.FruitGoods:
                case ItemSource.Goods:
                    var building = _content.GetBuilding(_content.GetGoodsRecipe(best).BuildingId);
                    return building != null ? $"{building.DisplayName} order" : "Goods order";
                case ItemSource.Animal:
                    return $"{FindAnimalBuildingFor(best).AnimalDisplayName} request";
                default:
                    return "Fresh produce";
            }
        }

        /// <summary>Where an item comes from, from simplest to most involved.</summary>
        private enum ItemSource
        {
            Crop,
            Fruit,
            Animal,
            FruitGoods,
            Goods
        }

        private ItemSource SourceOf(string itemId)
        {
            var recipe = _content.GetGoodsRecipe(itemId);
            if (recipe != null)
            {
                return _content.GetFruitInput(recipe) != null ? ItemSource.FruitGoods : ItemSource.Goods;
            }

            if (FindAnimalBuildingFor(itemId) != null)
            {
                return ItemSource.Animal;
            }

            return FindFruitTreeFor(itemId) != null ? ItemSource.Fruit : ItemSource.Crop;
        }

        // ------------------------------------------------------------------ Generation

        private sealed class Option
        {
            public ItemSource Source;
            public MissionRequirement Requirement;
            public int Coins;
            public int Xp;

            /// <summary>Items in the same chain: ingredients, the product it feeds into, etc.</summary>
            public HashSet<string> Related = new HashSet<string>();
        }

        /// <summary>
        /// How often each source of item comes up. Weighting by source rather than by item
        /// stops the nine crops crowding out animal products and goods, and the many Bakery and Dairy
        /// goods crowding out juices.
        /// </summary>
        private static double SourceWeight(ItemSource source)
        {
            switch (source)
            {
                case ItemSource.Crop:
                case ItemSource.Fruit:
                    return 1.0;
                case ItemSource.Animal:
                    return 1.2;
                default:
                    return 1.3;
            }
        }

        /// <summary>A new order, or null if the player can't make anything yet.</summary>
        private Mission Generate()
        {
            var level = Math.Max(1, _level());
            var options = BuildOptions(level);
            if (options.Count == 0)
            {
                return null;
            }

            var random = new Random(unchecked(GeneratedCount * 7919 + 17));
            GeneratedCount++;
            var maxRequirements = level >= 3 ? 3 : 2;
            var count = Math.Min(options.Count, 1 + random.Next(maxRequirements));

            var chosen = new List<Option>();
            for (var i = 0; i < count; i++)
            {
                Option pick = null;
                if (chosen.Count > 0 && random.NextDouble() < RelatedChance)
                {
                    pick = PickRelated(options, chosen, random);
                }

                if (pick == null)
                {
                    pick = PickWeighted(options, random);
                }

                options.Remove(pick);
                chosen.Add(pick);
            }

            var requirements = new List<MissionRequirement>();
            var coins = 0;
            var xp = 0;
            for (var i = 0; i < chosen.Count; i++)
            {
                requirements.Add(chosen[i].Requirement);
                coins += chosen[i].Coins;
                xp += chosen[i].Xp;
            }

            // Bigger notices pay a little extra for the effort.
            var bonus = 1.0 + 0.2 * (requirements.Count - 1);
            return new Mission(requirements, (int)Math.Round(coins * bonus), (int)Math.Round(xp * bonus));
        }

        private static Option PickWeighted(List<Option> options, Random random)
        {
            var total = 0.0;
            var perSource = new Dictionary<ItemSource, int>();
            for (var i = 0; i < options.Count; i++)
            {
                var source = options[i].Source;
                perSource[source] = perSource.TryGetValue(source, out var n) ? n + 1 : 1;
            }

            // Each source gets its weight in total, shared between its items.
            foreach (var pair in perSource)
            {
                total += SourceWeight(pair.Key);
            }

            var roll = random.NextDouble() * total;
            for (var i = 0; i < options.Count; i++)
            {
                var source = options[i].Source;
                roll -= SourceWeight(source) / perSource[source];
                if (roll <= 0)
                {
                    return options[i];
                }
            }

            return options[options.Count - 1];
        }

        private static Option PickRelated(List<Option> options, List<Option> chosen, Random random)
        {
            var candidates = new List<Option>();
            for (var i = 0; i < options.Count; i++)
            {
                for (var c = 0; c < chosen.Count; c++)
                {
                    if (AreRelated(options[i], chosen[c]))
                    {
                        candidates.Add(options[i]);
                        break;
                    }
                }
            }

            return candidates.Count > 0 ? candidates[random.Next(candidates.Count)] : null;
        }

        private static bool AreRelated(Option a, Option b)
        {
            return a.Related.Contains(b.Requirement.ItemId) || b.Related.Contains(a.Requirement.ItemId);
        }

        /// <summary>Every item the player can get right now, with links between chains.</summary>
        private List<Option> BuildOptions(int level)
        {
            var options = new List<Option>();

            for (var i = 0; i < _content.Crops.Count; i++)
            {
                var crop = _content.Crops[i];
                if (crop != null && crop.UnlockLevel <= level)
                {
                    options.Add(MakeOption(ItemSource.Crop, crop.Id, crop.SellPrice, crop.HarvestXp, 40, 2, 12, level));
                }
            }

            foreach (var tree in OwnedFruitTrees())
            {
                var fruit = _content.GetProduct(tree.ProductId);
                if (fruit != null && tree.UnlockLevel <= level)
                {
                    options.Add(MakeOption(ItemSource.Fruit, fruit.Id, fruit.SellPrice, fruit.CollectXp, 240, 2, 6, level));
                }
            }

            foreach (var building in OwnedAnimalBuildings())
            {
                // An older save can own a building the player hasn't reached the level for yet.
                var product = _content.GetProduct(building.ProductId);
                if (product == null || building.UnlockLevel > level)
                {
                    continue;
                }

                var feed = _content.GetFeedRecipeFor(building.AnimalKind);
                var option = MakeOption(ItemSource.Animal, product.Id, product.SellPrice, product.CollectXp, 120, 2, 8, level);
                if (feed != null)
                {
                    option.Related.Add(feed.CropId);
                }

                options.Add(option);
            }

            foreach (var recipe in OwnedGoodsRecipes())
            {
                var product = _content.GetProduct(recipe.OutputId);
                if (product == null || recipe.UnlockLevel > level)
                {
                    continue;
                }

                // A juice can only be made with its fruit, and fruit is slow to come by.
                var fruit = _content.GetFruitInput(recipe);
                if (fruit != null && !_buildings.OwnsFruitTree(fruit))
                {
                    continue;
                }

                var option = fruit != null
                    ? MakeOption(ItemSource.FruitGoods, product.Id, product.SellPrice, product.CollectXp, 600, 1, 4, level, ContentDatabase.MaxFruitGoodsPerRequest)
                    : MakeOption(ItemSource.Goods, product.Id, product.SellPrice, product.CollectXp, 600, 1, 4, level);
                for (var i = 0; i < recipe.Inputs.Count; i++)
                {
                    option.Related.Add(recipe.Inputs[i].itemId);
                }

                options.Add(option);
            }

            return options;
        }

        /// <summary>
        /// A requirement for one item. Cheap items are asked for in larger numbers:
        /// valueBudget / sellPrice, clamped, plus a little more every few levels, up to
        /// <paramref name="cap"/>. The reward pays more than selling the items would.
        /// </summary>
        private static Option MakeOption(
            ItemSource source,
            string itemId,
            int sellPrice,
            int unitXp,
            int valueBudget,
            int min,
            int max,
            int level,
            int cap = int.MaxValue)
        {
            var baseAmount = Math.Max(min, Math.Min(max, (int)Math.Round(valueBudget / (double)Math.Max(1, sellPrice))));
            var required = Math.Min(cap, baseAmount + (level - 1) / 3);
            return new Option
            {
                Source = source,
                Requirement = new MissionRequirement(itemId, required),
                Coins = (int)Math.Round(required * Math.Max(1, sellPrice) * RewardPerSellValue) + 20,
                Xp = 5 + required * Math.Max(1, unitXp)
            };
        }

        /// <summary>The animal building whose animals make this product, from content (owned or not).</summary>
        private BuildingDefinition FindAnimalBuildingFor(string productId)
        {
            for (var i = 0; i < _content.Buildings.Count; i++)
            {
                var building = _content.Buildings[i];
                if (building != null && building.CanHouseAnimals && building.ProductId == productId)
                {
                    return building;
                }
            }

            return null;
        }

        /// <summary>The fruit tree that grows this fruit, from content (owned or not).</summary>
        private BuildingDefinition FindFruitTreeFor(string productId)
        {
            for (var i = 0; i < _content.Buildings.Count; i++)
            {
                var building = _content.Buildings[i];
                if (building != null && building.IsFruitTree && building.ProductId == productId)
                {
                    return building;
                }
            }

            return null;
        }

        private IEnumerable<BuildingDefinition> OwnedFruitTrees()
        {
            var seen = new HashSet<string>();
            for (var i = 0; i < _buildings.Placed.Count; i++)
            {
                var definition = _buildings.Placed[i].Definition;
                if (definition != null && definition.IsFruitTree && seen.Add(definition.ProductId))
                {
                    yield return definition;
                }
            }
        }

        private IEnumerable<BuildingDefinition> OwnedAnimalBuildings()
        {
            var seen = new HashSet<string>();
            for (var i = 0; i < _buildings.Placed.Count; i++)
            {
                var building = _buildings.Placed[i];
                var definition = building.Definition;
                if (definition != null &&
                    definition.CanHouseAnimals &&
                    building.AnimalCount > 0 &&
                    !string.IsNullOrEmpty(definition.ProductId) &&
                    seen.Add(definition.ProductId))
                {
                    yield return definition;
                }
            }
        }

        private IEnumerable<GoodsRecipeDefinition> OwnedGoodsRecipes()
        {
            var seen = new HashSet<string>();
            for (var i = 0; i < _buildings.Placed.Count; i++)
            {
                var definition = _buildings.Placed[i].Definition;
                if (definition == null || definition.Category != BuildingCategory.Goods || !seen.Add(definition.Id))
                {
                    continue;
                }

                foreach (var recipe in _content.GetGoodsRecipesFor(definition.Id))
                {
                    yield return recipe;
                }
            }
        }

        private bool AllItemsUnlocked(Mission mission)
        {
            if (mission.Requirements.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < mission.Requirements.Count; i++)
            {
                if (!IsItemUnlocked(mission.Requirements[i].ItemId))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A crop, fruit, animal product or good the player's level has reached. False for unknown items.</summary>
        private bool IsItemUnlocked(string itemId)
        {
            var level = Math.Max(1, _level());
            var crop = _content.GetCrop(itemId);
            if (crop != null)
            {
                return crop.UnlockLevel <= level;
            }

            var recipe = _content.GetGoodsRecipe(itemId);
            if (recipe != null)
            {
                return recipe.UnlockLevel <= level;
            }

            var source = FindAnimalBuildingFor(itemId) ?? FindFruitTreeFor(itemId);
            return source != null && source.UnlockLevel <= level;
        }
    }
}
