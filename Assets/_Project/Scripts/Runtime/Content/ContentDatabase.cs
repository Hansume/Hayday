using System.Collections.Generic;
using UnityEngine;

namespace FarmGame.Content
{
    [CreateAssetMenu(menuName = "Farm Game/Content Database", fileName = "ContentDatabase")]
    public sealed class ContentDatabase : ScriptableObject
    {
        [SerializeField] private List<CropDefinition> crops = new List<CropDefinition>();
        [SerializeField] private List<BuildingDefinition> buildings = new List<BuildingDefinition>();
        [SerializeField] private List<FeedRecipeDefinition> feedRecipes = new List<FeedRecipeDefinition>();
        [SerializeField] private List<ProductDefinition> products = new List<ProductDefinition>();
        [SerializeField] private List<GoodsRecipeDefinition> goodsRecipes = new List<GoodsRecipeDefinition>();
        [SerializeField] private List<FishDefinition> fish = new List<FishDefinition>();
        [SerializeField] private FarmLayout layout;
        [SerializeField] private int startingCoins = 100;
        [SerializeField] private StorageBalance storage = new StorageBalance();

        /// <summary>Storage capacity and upgrade costs.</summary>
        public StorageBalance Storage => storage ?? (storage = new StorageBalance());

        public void ConfigureStorage(StorageBalance settings)
        {
            storage = settings != null ? settings.Clone() : new StorageBalance();
        }

        [SerializeField] private NoticeBalance notices = new NoticeBalance();

        /// <summary>Notice board slots and refill timings.</summary>
        public NoticeBalance Notices => notices ?? (notices = new NoticeBalance());

        public void ConfigureNotices(NoticeBalance settings)
        {
            notices = settings != null ? settings.Clone() : new NoticeBalance();
        }

        [SerializeField] private VisitorBalance visitors = new VisitorBalance();

        /// <summary>How often visitors come, how long they stay, and what serving them pays.</summary>
        public VisitorBalance Visitors => visitors ?? (visitors = new VisitorBalance());

        public void ConfigureVisitors(VisitorBalance settings)
        {
            visitors = settings != null ? settings.Clone() : new VisitorBalance();
        }

        [SerializeField] private LandBalance land = new LandBalance();

        /// <summary>How often Land Deeds are given.</summary>
        public LandBalance Land => land ?? (land = new LandBalance());

        public void ConfigureLand(LandBalance settings)
        {
            land = settings != null ? settings.Clone() : new LandBalance();
        }

        [SerializeField] private CocktailBarBalance cocktailBar = new CocktailBarBalance();

        /// <summary>The Cocktail Bar's line: its size, timings and rewards.</summary>
        public CocktailBarBalance CocktailBar => cocktailBar ?? (cocktailBar = new CocktailBarBalance());

        public void ConfigureCocktailBar(CocktailBarBalance settings)
        {
            cocktailBar = settings != null ? settings.Clone() : new CocktailBarBalance();
        }

        public IReadOnlyList<CropDefinition> Crops => crops;
        public IReadOnlyList<BuildingDefinition> Buildings => buildings ?? (buildings = new List<BuildingDefinition>());
        public IReadOnlyList<FeedRecipeDefinition> FeedRecipes =>
            feedRecipes ?? (feedRecipes = new List<FeedRecipeDefinition>());
        public IReadOnlyList<ProductDefinition> Products =>
            products ?? (products = new List<ProductDefinition>());
        public IReadOnlyList<GoodsRecipeDefinition> GoodsRecipes =>
            goodsRecipes ?? (goodsRecipes = new List<GoodsRecipeDefinition>());

        /// <summary>Fish the player can buy into a fish pond, cheapest unlock first.</summary>
        public IReadOnlyList<FishDefinition> Fish => fish ?? (fish = new List<FishDefinition>());

        public FishDefinition GetFish(string fishId)
        {
            return Find(Fish, fishId, f => f.Id);
        }

        /// <summary>The fish with the lowest unlock level, used for saves made before fish had kinds.</summary>
        public FishDefinition GetStarterFish()
        {
            FishDefinition best = null;
            for (var i = 0; i < Fish.Count; i++)
            {
                if (Fish[i] != null && (best == null || Fish[i].UnlockLevel < best.UnlockLevel))
                {
                    best = Fish[i];
                }
            }

            return best;
        }

        public void ConfigureFish(IEnumerable<FishDefinition> fishDefinitions)
        {
            fish = fishDefinitions == null ? new List<FishDefinition>() : new List<FishDefinition>(fishDefinitions);
            fish.Sort((a, b) => a.UnlockLevel != b.UnlockLevel
                ? a.UnlockLevel.CompareTo(b.UnlockLevel)
                : a.Cost.CompareTo(b.Cost));
        }

        public FarmLayout Layout => layout;
        public int StartingCoins => Mathf.Max(0, startingCoins);

        public CropDefinition GetCrop(string cropId)
        {
            return Find(crops, cropId, crop => crop.Id);
        }

        public BuildingDefinition GetBuilding(string buildingId)
        {
            return Find(Buildings, buildingId, building => building.Id);
        }

        public FeedRecipeDefinition GetFeedRecipe(string feedId)
        {
            return Find(FeedRecipes, feedId, recipe => recipe.Id);
        }

        public ProductDefinition GetProduct(string productId)
        {
            return Find(Products, productId, product => product.Id);
        }

        public GoodsRecipeDefinition GetGoodsRecipe(string recipeId)
        {
            return Find(GoodsRecipes, recipeId, recipe => recipe.Id);
        }

        /// <summary>
        /// The most of a fruit good (a juice) one order line or visitor asks for. Trees give a few
        /// fruit every few hours, so these can't grow with the player's level like other goods.
        /// </summary>
        public const int MaxFruitGoodsPerRequest = 2;

        /// <summary>The fruit a recipe needs (a product grown on a tree), or null if it uses none.</summary>
        public string GetFruitInput(GoodsRecipeDefinition recipe)
        {
            if (recipe == null)
            {
                return null;
            }

            for (var i = 0; i < recipe.Inputs.Count; i++)
            {
                for (var b = 0; b < Buildings.Count; b++)
                {
                    var building = Buildings[b];
                    if (building != null && building.IsFruitTree && building.ProductId == recipe.Inputs[i].itemId)
                    {
                        return building.ProductId;
                    }
                }
            }

            return null;
        }

        /// <summary>The recipes one goods building can make, in content order.</summary>
        public List<GoodsRecipeDefinition> GetGoodsRecipesFor(string buildingId)
        {
            var result = new List<GoodsRecipeDefinition>();
            for (var i = 0; i < GoodsRecipes.Count; i++)
            {
                var recipe = GoodsRecipes[i];
                if (recipe != null && recipe.BuildingId == buildingId)
                {
                    result.Add(recipe);
                }
            }

            return result;
        }

        /// <summary>
        /// Names of everything that becomes available at exactly <paramref name="level"/>:
        /// buildings first, then crops, then goods recipes. Landmarks are never listed.
        /// </summary>
        public List<string> GetUnlocksAt(int level)
        {
            var unlocks = GetUnlockDetailsAt(level);
            var names = new List<string>(unlocks.Count);
            for (var i = 0; i < unlocks.Count; i++)
            {
                names.Add(unlocks[i].DisplayName);
            }

            return names;
        }

        /// <summary>
        /// Everything that becomes available at exactly <paramref name="level"/>, with what the
        /// level-up window needs to show it: buildings first, then crops, then goods recipes.
        /// Landmarks are never listed, and a recipe that opens with its own building is left
        /// out (it's part of that building, not separate news).
        /// </summary>
        public List<UnlockInfo> GetUnlockDetailsAt(int level)
        {
            var unlocks = new List<UnlockInfo>();
            for (var i = 0; i < Buildings.Count; i++)
            {
                var building = Buildings[i];
                if (building != null && building.Category != BuildingCategory.Landmark && building.UnlockLevel == level)
                {
                    unlocks.Add(new UnlockInfo(
                        UnlockKind.Building,
                        building.Id,
                        building.DisplayName,
                        building.Icon != null ? building.Icon : building.WorldSprite,
                        DescribeBuilding(building),
                        building.Category));
                }
            }

            for (var i = 0; i < Crops.Count; i++)
            {
                var crop = Crops[i];
                if (crop != null && crop.UnlockLevel == level)
                {
                    unlocks.Add(new UnlockInfo(UnlockKind.Crop, crop.Id, crop.DisplayName, crop.Icon, "New crop"));
                }
            }

            for (var i = 0; i < GoodsRecipes.Count; i++)
            {
                var recipe = GoodsRecipes[i];
                if (recipe == null || recipe.UnlockLevel != level)
                {
                    continue;
                }

                var building = GetBuilding(recipe.BuildingId);
                if (building == null || building.UnlockLevel != level)
                {
                    unlocks.Add(new UnlockInfo(
                        UnlockKind.Recipe,
                        recipe.OutputId,
                        GetItemDisplayName(recipe.OutputId),
                        GetItemIcon(recipe.OutputId),
                        building != null ? $"New recipe: {building.DisplayName}" : "New recipe"));
                }
            }

            for (var i = 0; i < Fish.Count; i++)
            {
                var kind = Fish[i];
                if (kind != null && kind.UnlockLevel == level)
                {
                    unlocks.Add(new UnlockInfo(UnlockKind.Fish, kind.Id, kind.DisplayName, kind.Sprite, "New fish"));
                }
            }

            return unlocks;
        }

        /// <summary>
        /// The first level above <paramref name="level"/> that unlocks something, or 0 if
        /// nothing is left to unlock.
        /// </summary>
        public int GetNextUnlockLevel(int level)
        {
            var next = 0;
            void Consider(int unlockLevel)
            {
                if (unlockLevel > level && (next == 0 || unlockLevel < next))
                {
                    next = unlockLevel;
                }
            }

            for (var i = 0; i < Buildings.Count; i++)
            {
                if (Buildings[i] != null && Buildings[i].Category != BuildingCategory.Landmark)
                {
                    Consider(Buildings[i].UnlockLevel);
                }
            }

            for (var i = 0; i < Crops.Count; i++)
            {
                if (Crops[i] != null)
                {
                    Consider(Crops[i].UnlockLevel);
                }
            }

            for (var i = 0; i < GoodsRecipes.Count; i++)
            {
                if (GoodsRecipes[i] != null)
                {
                    Consider(GoodsRecipes[i].UnlockLevel);
                }
            }

            for (var i = 0; i < Fish.Count; i++)
            {
                if (Fish[i] != null)
                {
                    Consider(Fish[i].UnlockLevel);
                }
            }

            return next;
        }

        private static string DescribeBuilding(BuildingDefinition building)
        {
            switch (building.Category)
            {
                case BuildingCategory.Animals:
                    return "New animal home";
                case BuildingCategory.Production:
                    return "New production building";
                case BuildingCategory.Goods:
                    return "New goods building";
                case BuildingCategory.Fruit:
                    return "New fruit plant";
                default:
                    return "New building";
            }
        }

        /// <summary>Display name of any item: crop, feed or product. Falls back to the id.</summary>
        public string GetItemDisplayName(string itemId)
        {
            var crop = GetCrop(itemId);
            if (crop != null)
            {
                return crop.DisplayName;
            }

            var feed = GetFeedRecipe(itemId);
            if (feed != null)
            {
                return feed.DisplayName;
            }

            var product = GetProduct(itemId);
            return product != null ? product.DisplayName : itemId;
        }

        /// <summary>Icon of any item: crop, feed or product. Null when unknown.</summary>
        public Sprite GetItemIcon(string itemId)
        {
            var crop = GetCrop(itemId);
            if (crop != null)
            {
                return crop.Icon;
            }

            var feed = GetFeedRecipe(itemId);
            if (feed != null)
            {
                return feed.Icon;
            }

            var product = GetProduct(itemId);
            return product != null ? product.Icon : null;
        }

        /// <summary>The feed recipe for an animal, e.g. Chicken Feed for chickens.</summary>
        public FeedRecipeDefinition GetFeedRecipeFor(AnimalKind animal)
        {
            for (var i = 0; i < FeedRecipes.Count; i++)
            {
                var recipe = FeedRecipes[i];
                if (recipe != null && recipe.Animal == animal)
                {
                    return recipe;
                }
            }

            return null;
        }

        public void Configure(
            IEnumerable<CropDefinition> cropDefinitions,
            FarmLayout farmLayout,
            int coins,
            IEnumerable<BuildingDefinition> buildingDefinitions = null,
            IEnumerable<FeedRecipeDefinition> feedRecipeDefinitions = null,
            IEnumerable<ProductDefinition> productDefinitions = null,
            IEnumerable<GoodsRecipeDefinition> goodsRecipeDefinitions = null)
        {
            goodsRecipes = goodsRecipeDefinitions == null
                ? new List<GoodsRecipeDefinition>()
                : new List<GoodsRecipeDefinition>(goodsRecipeDefinitions);
            products = productDefinitions == null
                ? new List<ProductDefinition>()
                : new List<ProductDefinition>(productDefinitions);
            crops = new List<CropDefinition>(cropDefinitions);
            buildings = buildingDefinitions == null
                ? new List<BuildingDefinition>()
                : new List<BuildingDefinition>(buildingDefinitions);
            feedRecipes = feedRecipeDefinitions == null
                ? new List<FeedRecipeDefinition>()
                : new List<FeedRecipeDefinition>(feedRecipeDefinitions);
            layout = farmLayout;
            startingCoins = Mathf.Max(0, coins);
        }

        private static T Find<T>(IReadOnlyList<T> items, string id, System.Func<T, string> getId)
            where T : Object
        {
            if (string.IsNullOrEmpty(id) || items == null)
            {
                return null;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item != null && getId(item) == id)
                {
                    return item;
                }
            }

            return null;
        }
    }
}
