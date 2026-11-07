using System;
using System.Collections.Generic;
using FarmGame.Content;
using FarmGame.Core;
using UnityEngine;

namespace FarmGame.Tests
{
    internal sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public void AdvanceSeconds(double seconds)
        {
            UtcNow = UtcNow.AddSeconds(seconds);
        }
    }

    internal static class TestContent
    {
        public static CropDefinition CreateCrop(
            string id = "wheat",
            float growSeconds = 60f,
            int seedCost = 1,
            int sellPrice = 2,
            int yieldAmount = 2,
            int harvestXp = 1,
            int unlockLevel = 1)
        {
            var crop = ScriptableObject.CreateInstance<CropDefinition>();
            crop.Configure(id, id, null, null, growSeconds, seedCost, sellPrice, yieldAmount, harvestXp, unlockLevel);
            return crop;
        }

        public static BuildingDefinition CreateBuilding(
            string id = "fish_pond",
            int cost = 100,
            int footprintWidth = 2,
            int footprintHeight = 2,
            BuildingCategory category = BuildingCategory.Animals,
            AnimalKind animalKind = AnimalKind.None,
            int animalCapacity = 0,
            int animalCost = 0,
            string productId = "",
            float productionSeconds = 300f,
            int unlockLevel = 1)
        {
            var building = ScriptableObject.CreateInstance<BuildingDefinition>();
            building.Configure(
                id,
                id,
                string.Empty,
                null,
                null,
                new Vector2Int(footprintWidth, footprintHeight),
                cost,
                category,
                animalKind,
                animalCapacity,
                animalCost,
                productId,
                productionSeconds,
                unlockLevel);
            return building;
        }

        public static ContentDatabase CreateDatabase(params CropDefinition[] crops)
        {
            return CreateDatabase(crops, null);
        }

        public static ContentDatabase CreateDatabase(
            CropDefinition[] crops,
            BuildingDefinition[] buildings)
        {
            return CreateDatabase(crops, buildings, null);
        }

        public static ContentDatabase CreateDatabase(
            CropDefinition[] crops,
            BuildingDefinition[] buildings,
            GoodsRecipeDefinition[] goodsRecipes)
        {
            var layout = ScriptableObject.CreateInstance<FarmLayout>();
            var cells = new List<Vector2Int>();
            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }

            layout.Configure(12, 12, cells);
            var database = ScriptableObject.CreateInstance<ContentDatabase>();
            database.Configure(
                crops,
                layout,
                100,
                buildings,
                CreateStandardFeedRecipes(),
                CreateStandardProducts(),
                goodsRecipes);
            return database;
        }

        /// <summary>
        /// Two fish for the pond, like the game's: Goldfish (level 1, costs 20, grows in 240 s,
        /// sells for 40 and 4 XP) and Trout (level 7, costs 75, grows in 900 s, sells for 145 and 10 XP).
        /// </summary>
        public static FishDefinition[] CreateStandardFish()
        {
            return new[]
            {
                CreateFish("goldfish", "Goldfish", 20, 1, 240f, 40, 4),
                CreateFish("trout", "Trout", 75, 7, 900f, 145, 10)
            };
        }

        public static FishDefinition CreateFish(
            string id,
            string displayName,
            int cost,
            int unlockLevel,
            float growSeconds,
            int sellPrice,
            int sellXp)
        {
            var fish = ScriptableObject.CreateInstance<FishDefinition>();
            fish.Configure(id, displayName, null, cost, unlockLevel, growSeconds, sellPrice, sellXp);
            return fish;
        }

        /// <summary>
        /// Fish Fillet (20 coins, 4 XP), Egg (45, 7) and Milk (110, 12), like the game, plus two
        /// goods: Butter (280, 15) and Fish Pie (400, 25).
        /// </summary>
        public static ProductDefinition[] CreateStandardProducts()
        {
            return new[]
            {
                CreateProduct("fish_fillet", "Fish Fillet", 20, 4),
                CreateProduct("egg", "Egg", 45, 7),
                CreateProduct("milk", "Milk", 110, 12),
                CreateProduct("goat_milk", "Goat Milk", 140, 15),
                CreateProduct("butter", "Butter", 280, 15),
                CreateProduct("fish_pie", "Fish Pie", 400, 25),
                CreateProduct("goat_cheese", "Goat Cheese", 360, 24),
                CreateProduct("goat_yogurt", "Goat Yogurt", 270, 18)
            };
        }

        public static GoodsRecipeDefinition CreateGoodsRecipe(
            string outputId,
            string buildingId,
            float seconds,
            params ItemAmount[] inputs)
        {
            var recipe = ScriptableObject.CreateInstance<GoodsRecipeDefinition>();
            recipe.Configure(outputId, buildingId, inputs, seconds);
            return recipe;
        }

        private static ProductDefinition CreateProduct(string id, string displayName, int sellPrice, int collectXp)
        {
            var product = ScriptableObject.CreateInstance<ProductDefinition>();
            product.Configure(id, displayName, null, sellPrice, collectXp);
            return product;
        }

        /// <summary>The same three recipes the game ships with.</summary>
        public static FeedRecipeDefinition[] CreateStandardFeedRecipes()
        {
            return new[]
            {
                CreateFeedRecipe(GameSession.FishFeedId, "Fish Feed", AnimalKind.Fish, "wheat", 120f),
                CreateFeedRecipe(GameSession.ChickenFeedId, "Chicken Feed", AnimalKind.Chicken, "corn", 300f),
                CreateFeedRecipe(GameSession.CowFeedId, "Cow Feed", AnimalKind.Cow, "carrot", 600f),
                CreateFeedRecipe("goat_feed", "Goat Feed", AnimalKind.Goat, "potato", 900f)
            };
        }

        private static FeedRecipeDefinition CreateFeedRecipe(
            string id,
            string displayName,
            AnimalKind animal,
            string cropId,
            float seconds)
        {
            var recipe = ScriptableObject.CreateInstance<FeedRecipeDefinition>();
            recipe.Configure(id, displayName, null, animal, cropId, 3, seconds);
            return recipe;
        }

        public static void DestroyDatabase(ContentDatabase database)
        {
            if (database == null)
            {
                return;
            }

            var layout = database.Layout;
            var owned = new List<UnityEngine.Object>(database.FeedRecipes);
            owned.AddRange(database.Products);
            owned.AddRange(database.GoodsRecipes);
            owned.AddRange(database.Fish);
            UnityEngine.Object.DestroyImmediate(database);
            for (var i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned[i]);
                }
            }

            if (layout != null)
            {
                UnityEngine.Object.DestroyImmediate(layout);
            }
        }
    }
}
