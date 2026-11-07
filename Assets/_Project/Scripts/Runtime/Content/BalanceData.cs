using System;
using System.Collections.Generic;

namespace FarmGame.Content
{
    /// <summary>
    /// The game's tunable numbers: crops, buildings, animals, feed, products and goods recipes,
    /// merged from every file under Assets/_Project/Config/Balance (see <see cref="BalanceLoader"/>).
    /// The editor copies them into the content assets whenever a file is saved. Times are in
    /// seconds, prices in coins.
    ///
    /// Field names match the JSON keys. Enums (category, animal) are written as names so the
    /// files stay readable: "Animals", "Production", "Goods", "Fruit", "Landmark"; "Fish", "Chicken",
    /// "Cow", "Goat" or "" for none.
    /// </summary>
    [Serializable]
    public sealed class BalanceData
    {
        /// <summary>Free text shown at the top of a file. Ignored by the game.</summary>
        public string[] notes = Array.Empty<string>();

        public int startingCoins;
        public StorageBalance storage = new StorageBalance();
        public NoticeBalance notices = new NoticeBalance();
        public VisitorBalance visitors = new VisitorBalance();
        public LandBalance land = new LandBalance();
        public CocktailBarBalance cocktailBar = new CocktailBarBalance();
        public CropBalance[] crops = Array.Empty<CropBalance>();
        public BuildingBalance[] buildings = Array.Empty<BuildingBalance>();
        public FeedBalance[] feeds = Array.Empty<FeedBalance>();
        public ProductBalance[] products = Array.Empty<ProductBalance>();
        public GoodsRecipeBalance[] goodsRecipes = Array.Empty<GoodsRecipeBalance>();
        public FishBalance[] fish = Array.Empty<FishBalance>();
    }

    /// <summary>
    /// One balance file. It may hold any of the sections, so a file can be "all the crops" or
    /// "everything about the Dairy" (the building, its goods and its recipes).
    /// </summary>
    [Serializable]
    public sealed class BalanceFile
    {
        public string[] notes = Array.Empty<string>();
        public CropBalance[] crops = Array.Empty<CropBalance>();
        public BuildingBalance[] buildings = Array.Empty<BuildingBalance>();
        public FeedBalance[] feeds = Array.Empty<FeedBalance>();
        public ProductBalance[] products = Array.Empty<ProductBalance>();
        public GoodsRecipeBalance[] goodsRecipes = Array.Empty<GoodsRecipeBalance>();
        public FishBalance[] fish = Array.Empty<FishBalance>();

        public bool IsEmpty =>
            (crops?.Length ?? 0) + (buildings?.Length ?? 0) + (feeds?.Length ?? 0) +
            (products?.Length ?? 0) + (goodsRecipes?.Length ?? 0) + (fish?.Length ?? 0) == 0;
    }

    /// <summary>settings.json: game-wide values that aren't part of any one item.</summary>
    [Serializable]
    public sealed class BalanceSettings
    {
        public string[] notes = Array.Empty<string>();
        public int startingCoins;
        public StorageBalance storage = new StorageBalance();
        public NoticeBalance notices = new NoticeBalance();
        public VisitorBalance visitors = new VisitorBalance();
        public LandBalance land = new LandBalance();
        public CocktailBarBalance cocktailBar = new CocktailBarBalance();
    }

    /// <summary>
    /// The Cocktail Bar on the beach: how many customers line up, how long the line lasts, the wait
    /// for the next one, and what serving a customer pays compared with a juice's sell price and
    /// the XP for making it.
    /// </summary>
    [Serializable]
    public sealed class CocktailBarBalance
    {
        public const int MaxLineSize = 7;

        public int lineSize = MaxLineSize;
        public float lineSeconds = 18000f;
        public float cooldownSeconds = 10800f;
        public float coinMultiplier = 1.5f;
        public float xpMultiplier = 1.5f;

        public int LineSize => Math.Max(1, Math.Min(MaxLineSize, lineSize));
        public TimeSpan Line => TimeSpan.FromSeconds(Math.Max(1f, lineSeconds));
        public TimeSpan Cooldown => TimeSpan.FromSeconds(Math.Max(0f, cooldownSeconds));

        public CocktailBarBalance Clone()
        {
            return (CocktailBarBalance)MemberwiseClone();
        }
    }

    /// <summary>The chance (0..1) of getting a Land Deed for filling an order or serving a visitor.</summary>
    [Serializable]
    public sealed class LandBalance
    {
        public float orderDeedChance = 0.05f;
        public float visitorDeedChance = 0.05f;

        public LandBalance Clone()
        {
            return (LandBalance)MemberwiseClone();
        }
    }

    /// <summary>
    /// Visitors: townsfolk who walk up to the farm and ask for goods. Serving one hands the
    /// goods over for fewer coins than selling them (<see cref="coinMultiplier"/> of their sell
    /// value), but more XP (<see cref="xpMultiplier"/> times what making them gives) and
    /// reputation, which only visitors give.
    /// </summary>
    [Serializable]
    public sealed class VisitorBalance
    {
        /// <summary>The most visitors at once: there are this many places for them to stand.</summary>
        public const int MaxSpots = 4;

        public int maxVisitors = 3;
        public float arrivalMinSeconds = 120f;
        public float arrivalMaxSeconds = 300f;
        public float staySeconds = 1800f;
        public float coinMultiplier = 0.6f;
        public float xpMultiplier = 2f;
        public int reputationBase = 2;
        public int reputationPerItem = 1;

        /// <summary>Chance (0..1) that a visitor comes to sell rather than to ask for goods.</summary>
        public float sellerChance = 0.15f;

        /// <summary>A seller's bundle costs this many times what selling it would pay, before discount.</summary>
        public float sellerPriceMultiplier = 2f;

        /// <summary>The biggest reputation discount on sellers' prices (0..1).</summary>
        public float maxDiscount = 0.4f;

        /// <summary>Reputation that earns the full discount; less earns a proportional share.</summary>
        public int fullDiscountReputation = 200;

        public int MaxVisitors => Math.Max(1, Math.Min(MaxSpots, maxVisitors));

        /// <summary>The discount (0..maxDiscount) on sellers' prices at this much reputation.</summary>
        public float DiscountFor(int reputation)
        {
            var cap = Math.Max(0f, Math.Min(1f, maxDiscount));
            if (reputation <= 0 || cap <= 0f)
            {
                return 0f;
            }

            var share = reputation / (float)Math.Max(1, fullDiscountReputation);
            return Math.Min(cap, cap * share);
        }
        public TimeSpan ArrivalMin => TimeSpan.FromSeconds(Math.Max(0f, arrivalMinSeconds));
        public TimeSpan ArrivalMax => TimeSpan.FromSeconds(Math.Max(Math.Max(0f, arrivalMinSeconds), arrivalMaxSeconds));
        public TimeSpan Stay => TimeSpan.FromSeconds(Math.Max(1f, staySeconds));

        public VisitorBalance Clone()
        {
            return (VisitorBalance)MemberwiseClone();
        }
    }

    /// <summary>
    /// The order board: how many orders it holds, and how long an empty slot waits for a new
    /// one after an order is filled, or discarded / runs out of time.
    /// </summary>
    [Serializable]
    public sealed class NoticeBalance
    {
        /// <summary>The most slots the order board window has room for.</summary>
        public const int MaxSlots = 6;

        public int slots = 4;
        public float refillAfterClaimSeconds = 180f;
        public float refillAfterDiscardSeconds = 180f;

        public int Slots => Math.Max(1, Math.Min(MaxSlots, slots));
        public TimeSpan RefillAfterClaim => TimeSpan.FromSeconds(Math.Max(0f, refillAfterClaimSeconds));
        public TimeSpan RefillAfterDiscard => TimeSpan.FromSeconds(Math.Max(0f, refillAfterDiscardSeconds));

        public NoticeBalance Clone()
        {
            return (NoticeBalance)MemberwiseClone();
        }
    }

    /// <summary>
    /// The farm's storage: how many items (crops, feed, products and goods together) it holds,
    /// and what upgrading it costs. Upgrade n (counting from 0) costs
    /// upgradeBaseCost * upgradeCostGrowth^n coins, rounded to the nearest 100.
    /// </summary>
    [Serializable]
    public sealed class StorageBalance
    {
        public int baseCapacity = 50;
        public int capacityPerUpgrade = 25;
        public int upgradeBaseCost = 10000;
        public float upgradeCostGrowth = 1.6f;
        public int maxUpgrades = 20;

        public int CapacityAt(int upgrades)
        {
            var clamped = Math.Max(0, Math.Min(upgrades, Math.Max(0, maxUpgrades)));
            return (int)Math.Min((long)Math.Max(1, baseCapacity) + (long)Math.Max(0, capacityPerUpgrade) * clamped, int.MaxValue);
        }

        /// <summary>Coins for the upgrade after <paramref name="upgrades"/> done; 0 when there are no more.</summary>
        public int UpgradeCost(int upgrades)
        {
            if (upgrades < 0 || upgrades >= maxUpgrades)
            {
                return 0;
            }

            var cost = Math.Max(0, upgradeBaseCost) * Math.Pow(Math.Max(1f, upgradeCostGrowth), upgrades);
            cost = Math.Round(cost / 100.0) * 100.0;
            return (int)Math.Min(cost, int.MaxValue);
        }

        public StorageBalance Clone()
        {
            return (StorageBalance)MemberwiseClone();
        }
    }

    /// <summary>
    /// Reads a balance folder: settings.json at its root, plus every other .json file in it or
    /// its subfolders, merged in path order. Problems are reported with the file they're in.
    /// </summary>
    public static class BalanceLoader
    {
        public const string SettingsFileName = "settings.json";

        public static bool TryLoad(string directory, out BalanceData data, out List<string> errors)
        {
            data = null;
            errors = new List<string>();
            if (!System.IO.Directory.Exists(directory))
            {
                errors.Add($"The balance folder {directory} doesn't exist.");
                return false;
            }

            var settingsPath = System.IO.Path.Combine(directory, SettingsFileName);
            BalanceSettings settings = null;
            if (!System.IO.File.Exists(settingsPath))
            {
                errors.Add($"{SettingsFileName} is missing from {directory}.");
            }
            else
            {
                settings = Parse<BalanceSettings>(settingsPath, SettingsFileName, errors);
            }

            var crops = new List<CropBalance>();
            var buildings = new List<BuildingBalance>();
            var feeds = new List<FeedBalance>();
            var products = new List<ProductBalance>();
            var recipes = new List<GoodsRecipeBalance>();
            var fish = new List<FishBalance>();

            // Where each entry came from, so a problem can name its file.
            var sources = new Dictionary<string, string>();

            var files = System.IO.Directory.GetFiles(directory, "*.json", System.IO.SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var path in files)
            {
                var relative = Relative(directory, path);
                if (relative == SettingsFileName)
                {
                    continue;
                }

                var file = Parse<BalanceFile>(path, relative, errors);
                if (file == null)
                {
                    continue;
                }

                Add(crops, file.crops, "crops", c => c.id, relative, sources);
                Add(buildings, file.buildings, "buildings", b => b.id, relative, sources);
                Add(feeds, file.feeds, "feeds", f => f.id, relative, sources);
                Add(products, file.products, "products", p => p.id, relative, sources);
                Add(recipes, file.goodsRecipes, "goodsRecipes", r => r.outputId, relative, sources);
                Add(fish, file.fish, "fish", f => f.id, relative, sources);
            }

            if (errors.Count > 0)
            {
                return false;
            }

            data = new BalanceData
            {
                startingCoins = settings != null ? settings.startingCoins : 0,
                storage = settings?.storage ?? new StorageBalance(),
                notices = settings?.notices ?? new NoticeBalance(),
                visitors = settings?.visitors ?? new VisitorBalance(),
                land = settings?.land ?? new LandBalance(),
                cocktailBar = settings?.cocktailBar ?? new CocktailBarBalance(),
                crops = crops.ToArray(),
                buildings = buildings.ToArray(),
                feeds = feeds.ToArray(),
                products = products.ToArray(),
                goodsRecipes = recipes.ToArray(),
                fish = fish.ToArray()
            };

            foreach (var error in BalanceValidator.Validate(data))
            {
                errors.Add(WithSource(error, sources));
            }

            return errors.Count == 0;
        }

        private static T Parse<T>(string path, string relative, List<string> errors) where T : class
        {
            try
            {
                var parsed = UnityEngine.JsonUtility.FromJson<T>(System.IO.File.ReadAllText(path));
                if (parsed == null)
                {
                    errors.Add($"{relative} is empty.");
                }

                return parsed;
            }
            catch (Exception exception)
            {
                errors.Add($"{relative} isn't valid JSON: {exception.Message}");
                return null;
            }
        }

        private static void Add<T>(
            List<T> into,
            T[] entries,
            string section,
            Func<T, string> getId,
            string file,
            Dictionary<string, string> sources)
            where T : class
        {
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                into.Add(entry);
                var key = $"{section}[{getId(entry) ?? "?"}]";
                if (!sources.ContainsKey(key))
                {
                    sources[key] = file;
                }
                else
                {
                    // The validator reports the duplicate id; say where both copies are.
                    sources[key] = $"{sources[key]} and {file}";
                }
            }
        }

        /// <summary>"crops[wheat].growSeconds ..." becomes "crops.json: crops[wheat].growSeconds ...".</summary>
        private static string WithSource(string error, Dictionary<string, string> sources)
        {
            var close = error.IndexOf(']');
            if (close > 0 && sources.TryGetValue(error.Substring(0, close + 1), out var file))
            {
                return $"{file}: {error}";
            }

            return error;
        }

        private static string Relative(string directory, string path)
        {
            var root = System.IO.Path.GetFullPath(directory).TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
            var full = System.IO.Path.GetFullPath(path);
            var relative = full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : full;
            return relative.Replace('\\', '/');
        }
    }

    [Serializable]
    public sealed class CropBalance
    {
        public string id;
        public string name;
        public float growSeconds;
        public int seedCost;
        public int sellPrice;

        /// <summary>Crops harvested from one plot.</summary>
        public int yield;

        public int harvestXp;

        /// <summary>Player level at which the crop can be planted.</summary>
        public int unlockLevel = 1;
    }

    [Serializable]
    public sealed class BuildingBalance
    {
        public string id;
        public string name;
        public string description;

        /// <summary>"Animals", "Production", "Goods", "Fruit" or "Landmark".</summary>
        public string category;

        /// <summary>Footprint in grid cells.</summary>
        public int width = 1;
        public int height = 1;
        public int cost;

        /// <summary>Player level at which the building can be bought.</summary>
        public int unlockLevel = 1;

        // Animal buildings only.
        /// <summary>"Fish", "Chicken", "Cow", "Goat", or empty for buildings without animals.</summary>
        public string animal;
        public int animalCapacity;
        /// <summary>Coins to add one animal. Not used by Fish buildings: each fish has its own cost.</summary>
        public int animalCost;

        /// <summary>Product id a fed animal makes, e.g. "egg". Fish buildings have none: fish are sold.</summary>
        public string productId;

        /// <summary>
        /// Seconds from feeding an animal until its product is ready. Fish use their own
        /// growSeconds. For a Fruit building, seconds for its fruit (productId) to grow.
        /// </summary>
        public float productionSeconds;

        /// <summary>Fruit buildings only: how many fruit one harvest gives.</summary>
        public int harvestAmount;
    }

    /// <summary>
    /// A kind of fish the player can buy into a fish pond. Fed once, it grows for
    /// <see cref="growSeconds"/>; then it can be sold, leaving the pond, for
    /// <see cref="sellPrice"/> coins and <see cref="sellXp"/> XP.
    /// </summary>
    [Serializable]
    public sealed class FishBalance
    {
        public string id;
        public string name;

        /// <summary>Coins to buy one into a pond.</summary>
        public int cost;

        /// <summary>Player level at which it can be bought.</summary>
        public int unlockLevel = 1;

        /// <summary>Seconds from feeding until it is grown.</summary>
        public float growSeconds;

        public int sellPrice;
        public int sellXp;
    }

    [Serializable]
    public sealed class FeedBalance
    {
        public string id;
        public string name;

        /// <summary>Which animal eats it: "Fish", "Chicken", "Cow" or "Goat".</summary>
        public string animal;
        public string cropId;
        public int cropAmount;
        public float productionSeconds;
        public int outputAmount = 1;
    }

    [Serializable]
    public sealed class ProductBalance
    {
        public string id;
        public string name;
        public int sellPrice;
        public int collectXp;
    }

    [Serializable]
    public sealed class GoodsRecipeBalance
    {
        /// <summary>Product id made. Also the recipe's id.</summary>
        public string outputId;
        public string buildingId;
        public float productionSeconds;
        public int outputAmount = 1;

        /// <summary>Player level at which the recipe can be made. Not before its building's.</summary>
        public int unlockLevel = 1;
        public ItemAmount[] inputs = Array.Empty<ItemAmount>();
    }

    /// <summary>Checks a <see cref="BalanceData"/> for mistakes before it is applied.</summary>
    public static class BalanceValidator
    {
        public const string FeedGrinderId = "feed_grinder";

        /// <summary>Returns one message per problem; empty when the data is usable.</summary>
        public static List<string> Validate(BalanceData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("The file is empty or isn't valid JSON.");
                return errors;
            }

            if (data.startingCoins < 0)
            {
                errors.Add("startingCoins can't be negative.");
            }

            var storage = data.storage ?? new StorageBalance();
            AtLeastOne(storage.baseCapacity, "storage.baseCapacity", errors);
            AtLeastOne(storage.capacityPerUpgrade, "storage.capacityPerUpgrade", errors);
            NotNegative(storage.upgradeBaseCost, "storage.upgradeBaseCost", errors);
            NotNegative(storage.maxUpgrades, "storage.maxUpgrades", errors);
            if (!(storage.upgradeCostGrowth >= 1f))
            {
                errors.Add($"storage.upgradeCostGrowth must be at least 1 (was {storage.upgradeCostGrowth}).");
            }

            var notices = data.notices ?? new NoticeBalance();
            if (notices.slots < 1 || notices.slots > NoticeBalance.MaxSlots)
            {
                errors.Add($"notices.slots must be between 1 and {NoticeBalance.MaxSlots} (was {notices.slots}).");
            }

            if (!(notices.refillAfterClaimSeconds >= 0f))
            {
                errors.Add($"notices.refillAfterClaimSeconds can't be negative (was {notices.refillAfterClaimSeconds}).");
            }

            if (!(notices.refillAfterDiscardSeconds >= 0f))
            {
                errors.Add($"notices.refillAfterDiscardSeconds can't be negative (was {notices.refillAfterDiscardSeconds}).");
            }

            var visitors = data.visitors ?? new VisitorBalance();
            if (visitors.maxVisitors < 1 || visitors.maxVisitors > VisitorBalance.MaxSpots)
            {
                errors.Add($"visitors.maxVisitors must be between 1 and {VisitorBalance.MaxSpots} (was {visitors.maxVisitors}).");
            }

            if (!(visitors.arrivalMinSeconds >= 0f))
            {
                errors.Add($"visitors.arrivalMinSeconds can't be negative (was {visitors.arrivalMinSeconds}).");
            }

            if (!(visitors.arrivalMaxSeconds >= visitors.arrivalMinSeconds))
            {
                errors.Add(
                    $"visitors.arrivalMaxSeconds must be at least arrivalMinSeconds ({visitors.arrivalMaxSeconds} < {visitors.arrivalMinSeconds}).");
            }

            if (!(visitors.staySeconds >= 1f))
            {
                errors.Add($"visitors.staySeconds must be at least 1 (was {visitors.staySeconds}).");
            }

            if (!(visitors.coinMultiplier >= 0f))
            {
                errors.Add($"visitors.coinMultiplier can't be negative (was {visitors.coinMultiplier}).");
            }

            if (!(visitors.xpMultiplier >= 0f))
            {
                errors.Add($"visitors.xpMultiplier can't be negative (was {visitors.xpMultiplier}).");
            }

            NotNegative(visitors.reputationBase, "visitors.reputationBase", errors);
            NotNegative(visitors.reputationPerItem, "visitors.reputationPerItem", errors);

            if (!(visitors.sellerChance >= 0f && visitors.sellerChance <= 1f))
            {
                errors.Add($"visitors.sellerChance must be between 0 and 1 (was {visitors.sellerChance}).");
            }

            if (!(visitors.maxDiscount >= 0f && visitors.maxDiscount < 1f))
            {
                errors.Add($"visitors.maxDiscount must be at least 0 and below 1 (was {visitors.maxDiscount}).");
            }

            AtLeastOne(visitors.fullDiscountReputation, "visitors.fullDiscountReputation", errors);

            // Even at the full discount, buying must cost at least what selling pays; otherwise
            // players could buy from a visitor and sell straight back for a profit.
            if (!(visitors.sellerPriceMultiplier * (1f - visitors.maxDiscount) >= 1f))
            {
                errors.Add(
                    $"visitors.sellerPriceMultiplier x (1 - maxDiscount) must be at least 1, or goods could be bought and resold " +
                    $"for a profit (was {visitors.sellerPriceMultiplier} x {1f - visitors.maxDiscount:0.##}).");
            }

            var land = data.land ?? new LandBalance();
            if (!(land.orderDeedChance >= 0f && land.orderDeedChance <= 1f))
            {
                errors.Add($"land.orderDeedChance must be between 0 and 1 (was {land.orderDeedChance}).");
            }

            if (!(land.visitorDeedChance >= 0f && land.visitorDeedChance <= 1f))
            {
                errors.Add($"land.visitorDeedChance must be between 0 and 1 (was {land.visitorDeedChance}).");
            }

            var bar = data.cocktailBar ?? new CocktailBarBalance();
            if (bar.lineSize < 1 || bar.lineSize > CocktailBarBalance.MaxLineSize)
            {
                errors.Add($"cocktailBar.lineSize must be between 1 and {CocktailBarBalance.MaxLineSize} (was {bar.lineSize}).");
            }

            if (!(bar.lineSeconds >= 1f))
            {
                errors.Add($"cocktailBar.lineSeconds must be at least 1 (was {bar.lineSeconds}).");
            }

            if (!(bar.cooldownSeconds >= 0f))
            {
                errors.Add($"cocktailBar.cooldownSeconds can't be negative (was {bar.cooldownSeconds}).");
            }

            if (!(bar.coinMultiplier >= 0f))
            {
                errors.Add($"cocktailBar.coinMultiplier can't be negative (was {bar.coinMultiplier}).");
            }

            if (!(bar.xpMultiplier >= 0f))
            {
                errors.Add($"cocktailBar.xpMultiplier can't be negative (was {bar.xpMultiplier}).");
            }

            // Crops, products and feeds share the inventory, so their ids must be unique together.
            var itemIds = new HashSet<string>();
            var cropIds = new HashSet<string>();
            var productIds = new HashSet<string>();
            var feedIds = new HashSet<string>();

            foreach (var crop in data.crops ?? Array.Empty<CropBalance>())
            {
                var where = $"crops[{Name(crop?.id)}]";
                if (!CheckId(crop?.id, where, itemIds, errors))
                {
                    continue;
                }

                cropIds.Add(crop.id);
                CheckName(crop.name, where, errors);
                Positive(crop.growSeconds, $"{where}.growSeconds", errors);
                NotNegative(crop.seedCost, $"{where}.seedCost", errors);
                NotNegative(crop.sellPrice, $"{where}.sellPrice", errors);
                AtLeastOne(crop.yield, $"{where}.yield", errors);
                NotNegative(crop.harvestXp, $"{where}.harvestXp", errors);
            }

            foreach (var product in data.products ?? Array.Empty<ProductBalance>())
            {
                var where = $"products[{Name(product?.id)}]";
                if (!CheckId(product?.id, where, itemIds, errors))
                {
                    continue;
                }

                productIds.Add(product.id);
                CheckName(product.name, where, errors);
                NotNegative(product.sellPrice, $"{where}.sellPrice", errors);
                NotNegative(product.collectXp, $"{where}.collectXp", errors);
            }

            var fedAnimals = new HashSet<AnimalKind>();
            foreach (var feed in data.feeds ?? Array.Empty<FeedBalance>())
            {
                var where = $"feeds[{Name(feed?.id)}]";
                if (!CheckId(feed?.id, where, itemIds, errors))
                {
                    continue;
                }

                feedIds.Add(feed.id);
                CheckName(feed.name, where, errors);
                if (!TryParseAnimal(feed.animal, out var animal) || animal == AnimalKind.None)
                {
                    errors.Add($"{where}.animal \"{feed.animal}\" must be one of {AnimalNames()}.");
                }
                else if (!fedAnimals.Add(animal))
                {
                    errors.Add($"{where}: there is already a feed for {animal}. Each animal eats one feed.");
                }

                if (!cropIds.Contains(feed.cropId ?? string.Empty))
                {
                    errors.Add($"{where}.cropId \"{feed.cropId}\" is not a crop.");
                }

                AtLeastOne(feed.cropAmount, $"{where}.cropAmount", errors);
                Positive(feed.productionSeconds, $"{where}.productionSeconds", errors);
                AtLeastOne(feed.outputAmount, $"{where}.outputAmount", errors);
            }

            var buildingIds = new HashSet<string>();
            var goodsBuildings = new HashSet<string>();
            var hasFishBuilding = false;
            foreach (var building in data.buildings ?? Array.Empty<BuildingBalance>())
            {
                var where = $"buildings[{Name(building?.id)}]";
                if (!CheckId(building?.id, where, buildingIds, errors))
                {
                    continue;
                }

                CheckName(building.name, where, errors);
                AtLeastOne(building.width, $"{where}.width", errors);
                AtLeastOne(building.height, $"{where}.height", errors);
                NotNegative(building.cost, $"{where}.cost", errors);

                if (!TryParseCategory(building.category, out var category))
                {
                    errors.Add($"{where}.category \"{building.category}\" must be one of {CategoryNames()}.");
                    continue;
                }

                if (category == BuildingCategory.Goods)
                {
                    goodsBuildings.Add(building.id);
                }

                if (category == BuildingCategory.Fruit)
                {
                    Positive(building.productionSeconds, $"{where}.productionSeconds", errors);
                    AtLeastOne(building.harvestAmount, $"{where}.harvestAmount", errors);
                    if (!productIds.Contains(building.productId ?? string.Empty))
                    {
                        errors.Add($"{where}.productId \"{building.productId}\" is not a product.");
                    }
                }

                if (!TryParseAnimal(building.animal, out var animal))
                {
                    errors.Add($"{where}.animal \"{building.animal}\" must be empty or one of {AnimalNames()}.");
                    continue;
                }

                if (category == BuildingCategory.Animals)
                {
                    if (animal == AnimalKind.None)
                    {
                        errors.Add($"{where}: an Animals building needs an animal.");
                    }

                    AtLeastOne(building.animalCapacity, $"{where}.animalCapacity", errors);
                    NotNegative(building.animalCost, $"{where}.animalCost", errors);
                    if (animal == AnimalKind.Fish)
                    {
                        // Fish make nothing: they grow and are sold, at the prices in "fish".
                        hasFishBuilding = true;
                        if (!string.IsNullOrEmpty(building.productId))
                        {
                            errors.Add($"{where}.productId must be empty: fish are sold when grown, they make no product.");
                        }
                    }
                    else
                    {
                        Positive(building.productionSeconds, $"{where}.productionSeconds", errors);
                        if (!productIds.Contains(building.productId ?? string.Empty))
                        {
                            errors.Add($"{where}.productId \"{building.productId}\" is not a product.");
                        }
                    }

                    if (animal != AnimalKind.None && !fedAnimals.Contains(animal))
                    {
                        errors.Add($"{where}: no feed in \"feeds\" is for {animal}, so its animals could never eat.");
                    }
                }
                else if (animal != AnimalKind.None)
                {
                    errors.Add($"{where}: only Animals buildings can have an animal.");
                }
            }

            if (!buildingIds.Contains(FeedGrinderId))
            {
                errors.Add($"buildings: \"{FeedGrinderId}\" is missing; the game needs it to make feed.");
            }

            var fishIds = new HashSet<string>();
            foreach (var fish in data.fish ?? Array.Empty<FishBalance>())
            {
                var where = $"fish[{Name(fish?.id)}]";
                if (!CheckId(fish?.id, where, fishIds, errors))
                {
                    continue;
                }

                CheckName(fish.name, where, errors);
                NotNegative(fish.cost, $"{where}.cost", errors);
                Positive(fish.growSeconds, $"{where}.growSeconds", errors);
                NotNegative(fish.sellXp, $"{where}.sellXp", errors);
                if (fish.sellPrice <= fish.cost)
                {
                    errors.Add($"{where}.sellPrice ({fish.sellPrice}) must be more than its cost ({fish.cost}), or raising it loses coins.");
                }
            }

            if (hasFishBuilding && fishIds.Count == 0)
            {
                errors.Add("fish: there is a Fish building but no fish to buy for it. Add at least one to \"fish\".");
            }

            var recipeIds = new HashSet<string>();
            foreach (var recipe in data.goodsRecipes ?? Array.Empty<GoodsRecipeBalance>())
            {
                var where = $"goodsRecipes[{Name(recipe?.outputId)}]";
                if (!CheckId(recipe?.outputId, where, recipeIds, errors))
                {
                    continue;
                }

                if (!productIds.Contains(recipe.outputId))
                {
                    errors.Add($"{where}.outputId \"{recipe.outputId}\" is not a product. Add it to \"products\".");
                }

                if (!goodsBuildings.Contains(recipe.buildingId ?? string.Empty))
                {
                    errors.Add($"{where}.buildingId \"{recipe.buildingId}\" is not a Goods building.");
                }

                Positive(recipe.productionSeconds, $"{where}.productionSeconds", errors);
                AtLeastOne(recipe.outputAmount, $"{where}.outputAmount", errors);

                var inputs = recipe.inputs ?? Array.Empty<ItemAmount>();
                if (inputs.Length == 0)
                {
                    errors.Add($"{where} needs at least one input.");
                }

                foreach (var input in inputs)
                {
                    if (!itemIds.Contains(input.itemId ?? string.Empty))
                    {
                        errors.Add($"{where}: input \"{input.itemId}\" is not a crop, product or feed.");
                    }
                    else if (input.itemId == recipe.outputId)
                    {
                        errors.Add($"{where}: a recipe can't use its own output as an input.");
                    }

                    AtLeastOne(input.amount, $"{where}: input \"{input.itemId}\" amount", errors);
                }
            }

            ValidateUnlockLevels(data, errors);
            return errors;
        }

        /// <summary>
        /// Every unlock level must be a real level, and nothing may unlock before what it needs:
        /// an animal building before the crop its feed is ground from, a recipe before its
        /// building, or a recipe before every one of its ingredients can be had.
        /// </summary>
        private static void ValidateUnlockLevels(BalanceData data, List<string> errors)
        {
            var maxLevel = Progression.PlayerLevel.MaxLevel;
            void InRange(int level, string where)
            {
                if (level < 1 || level > maxLevel)
                {
                    errors.Add($"{where}.unlockLevel must be between 1 and {maxLevel} (was {level}).");
                }
            }

            // The earliest level at which the player can get each item.
            var itemLevels = new Dictionary<string, int>();
            void Offer(string itemId, int level)
            {
                if (string.IsNullOrEmpty(itemId))
                {
                    return;
                }

                itemLevels[itemId] = itemLevels.TryGetValue(itemId, out var known) ? Math.Min(known, level) : level;
            }

            int LevelOf(string itemId)
            {
                return itemId != null && itemLevels.TryGetValue(itemId, out var level) ? level : 1;
            }

            var feedsByAnimal = new Dictionary<AnimalKind, FeedBalance>();
            foreach (var feed in data.feeds ?? Array.Empty<FeedBalance>())
            {
                if (feed != null && TryParseAnimal(feed.animal, out var animal) && !feedsByAnimal.ContainsKey(animal))
                {
                    feedsByAnimal[animal] = feed;
                }
            }

            foreach (var crop in data.crops ?? Array.Empty<CropBalance>())
            {
                if (crop?.id == null)
                {
                    continue;
                }

                InRange(crop.unlockLevel, $"crops[{crop.id}]");
                Offer(crop.id, crop.unlockLevel);
            }

            var buildingLevels = new Dictionary<string, int>();
            foreach (var building in data.buildings ?? Array.Empty<BuildingBalance>())
            {
                if (building?.id == null)
                {
                    continue;
                }

                var where = $"buildings[{building.id}]";
                InRange(building.unlockLevel, where);
                buildingLevels[building.id] = building.unlockLevel;
                if (TryParseCategory(building.category, out var category) && category == BuildingCategory.Fruit)
                {
                    Offer(building.productId, building.unlockLevel);
                }

                if (!TryParseAnimal(building.animal, out var animal) || animal == AnimalKind.None)
                {
                    continue;
                }

                Offer(building.productId, building.unlockLevel);
                if (feedsByAnimal.TryGetValue(animal, out var feed))
                {
                    Offer(feed.id, Math.Max(building.unlockLevel, LevelOf(feed.cropId)));
                    var cropLevel = LevelOf(feed.cropId);
                    if (cropLevel > building.unlockLevel)
                    {
                        errors.Add(
                            $"{where}.unlockLevel {building.unlockLevel} is before its feed crop " +
                            $"\"{feed.cropId}\" unlocks (level {cropLevel}), so its animals couldn't eat.");
                    }
                }
            }

            foreach (var fish in data.fish ?? Array.Empty<FishBalance>())
            {
                if (fish?.id != null)
                {
                    InRange(fish.unlockLevel, $"fish[{fish.id}]");
                }
            }

            // Goods can feed other goods (butter into a pie), so recipes are checked after
            // every product they could use has been offered.
            var recipes = data.goodsRecipes ?? Array.Empty<GoodsRecipeBalance>();
            foreach (var recipe in recipes)
            {
                if (recipe?.outputId != null)
                {
                    var buildingLevel = recipe.buildingId != null && buildingLevels.TryGetValue(recipe.buildingId, out var b) ? b : 1;
                    Offer(recipe.outputId, Math.Max(recipe.unlockLevel, buildingLevel));
                }
            }

            foreach (var recipe in recipes)
            {
                if (recipe?.outputId == null)
                {
                    continue;
                }

                var where = $"goodsRecipes[{recipe.outputId}]";
                InRange(recipe.unlockLevel, where);
                if (recipe.buildingId != null &&
                    buildingLevels.TryGetValue(recipe.buildingId, out var buildingLevel) &&
                    recipe.unlockLevel < buildingLevel)
                {
                    errors.Add(
                        $"{where}.unlockLevel {recipe.unlockLevel} is before its building \"{recipe.buildingId}\" " +
                        $"unlocks (level {buildingLevel}).");
                }

                foreach (var input in recipe.inputs ?? Array.Empty<ItemAmount>())
                {
                    var inputLevel = LevelOf(input.itemId);
                    if (inputLevel > recipe.unlockLevel)
                    {
                        errors.Add(
                            $"{where}.unlockLevel {recipe.unlockLevel} is before its input \"{input.itemId}\" " +
                            $"can be had (level {inputLevel}).");
                    }
                }
            }
        }

        public static bool TryParseCategory(string value, out BuildingCategory category)
        {
            return Enum.TryParse(value ?? string.Empty, true, out category) &&
                   Enum.IsDefined(typeof(BuildingCategory), category);
        }

        /// <summary>Empty means no animal.</summary>
        public static bool TryParseAnimal(string value, out AnimalKind animal)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                animal = AnimalKind.None;
                return true;
            }

            return Enum.TryParse(value, true, out animal) && Enum.IsDefined(typeof(AnimalKind), animal);
        }

        private static bool CheckId(string id, string where, HashSet<string> seen, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"{where} has no id.");
                return false;
            }

            if (!seen.Add(id))
            {
                errors.Add($"{where}: the id \"{id}\" is used more than once.");
                return false;
            }

            return true;
        }

        private static void CheckName(string name, string where, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"{where} has no name.");
            }
        }

        private static void Positive(float value, string what, List<string> errors)
        {
            if (!(value > 0f))
            {
                errors.Add($"{what} must be more than 0 (was {value}).");
            }
        }

        private static void AtLeastOne(int value, string what, List<string> errors)
        {
            if (value < 1)
            {
                errors.Add($"{what} must be at least 1 (was {value}).");
            }
        }

        private static void NotNegative(int value, string what, List<string> errors)
        {
            if (value < 0)
            {
                errors.Add($"{what} can't be negative (was {value}).");
            }
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "?" : id;
        }

        private static string AnimalNames()
        {
            return "Fish, Chicken, Cow, Goat";
        }

        private static string CategoryNames()
        {
            return string.Join(", ", Enum.GetNames(typeof(BuildingCategory)));
        }
    }
}
