using System.IO;
using FarmGame.Content;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class BalanceConfigTests
    {
        private static string BalanceDirectory => Path.Combine(Application.dataPath, "_Project/Config/Balance");

        private static BalanceData LoadShipped()
        {
            var ok = BalanceLoader.TryLoad(BalanceDirectory, out var data, out var errors);
            Assert.IsTrue(ok, "Balance files have problems:\n" + string.Join("\n", errors));
            return data;
        }

        [Test]
        public void ShippedBalance_HasNoProblems()
        {
            LoadShipped();
        }

        [Test]
        public void ShippedBalance_IsSplitIntoSmallFiles()
        {
            var files = Directory.GetFiles(BalanceDirectory, "*.json", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 5, "Balance should be spread over several files.");
            Assert.IsTrue(File.Exists(Path.Combine(BalanceDirectory, BalanceLoader.SettingsFileName)));
            Assert.IsFalse(
                File.Exists(Path.Combine(Application.dataPath, "_Project/Config/balance.json")),
                "The old single balance.json should be gone, or its values would be counted twice.");
        }

        // ------------------------------------------------------------------ The loader

        private sealed class TempBalance : System.IDisposable
        {
            public readonly string Directory = Path.Combine(Path.GetTempPath(), "FarmGameBalance", System.Guid.NewGuid().ToString("N"));

            public TempBalance()
            {
                System.IO.Directory.CreateDirectory(Directory);
                Write(BalanceLoader.SettingsFileName, "{ \"startingCoins\": 250 }");
            }

            public void Write(string relative, string json)
            {
                var path = Path.Combine(Directory, relative);
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json);
            }

            public void Dispose()
            {
                if (System.IO.Directory.Exists(Directory))
                {
                    System.IO.Directory.Delete(Directory, true);
                }
            }
        }

        private const string CropsJson =
            "{ \"crops\": [ { \"id\": \"wheat\", \"name\": \"Wheat\", \"growSeconds\": 60, \"seedCost\": 1, \"sellPrice\": 2, \"yield\": 2, \"harvestXp\": 1 } ] }";

        private const string GrinderJson =
            "{ \"buildings\": [ { \"id\": \"feed_grinder\", \"name\": \"Grinder\", \"category\": \"Production\", \"width\": 2, \"height\": 2 } ] }";

        private const string CoopJson =
            "{ \"buildings\": [ { \"id\": \"coop\", \"name\": \"Coop\", \"category\": \"Animals\", \"width\": 2, \"height\": 2," +
            " \"animal\": \"Chicken\", \"animalCapacity\": 3, \"productId\": \"egg\", \"productionSeconds\": 480 } ]," +
            " \"feeds\": [ { \"id\": \"chicken_feed\", \"name\": \"Chicken Feed\", \"animal\": \"Chicken\", \"cropId\": \"wheat\", \"cropAmount\": 3, \"productionSeconds\": 300 } ]," +
            " \"products\": [ { \"id\": \"egg\", \"name\": \"Egg\", \"sellPrice\": 45, \"collectXp\": 7 } ] }";

        [Test]
        public void Loader_MergesEveryFileInTheFolderAndSubfolders()
        {
            using (var balance = new TempBalance())
            {
                balance.Write("crops.json", CropsJson);
                balance.Write("production/feed_grinder.json", GrinderJson);
                balance.Write("animals/coop.json", CoopJson);

                Assert.IsTrue(BalanceLoader.TryLoad(balance.Directory, out var data, out var errors), string.Join("\n", errors));
                Assert.AreEqual(250, data.startingCoins);
                Assert.AreEqual(1, data.crops.Length);
                Assert.AreEqual(2, data.buildings.Length);
                Assert.AreEqual(1, data.feeds.Length);
                Assert.AreEqual("wheat", data.feeds[0].cropId, "A feed in one file can use a crop from another.");
                Assert.AreEqual(1, data.products.Length);
            }
        }

        [Test]
        public void Loader_NamesTheFileAProblemIsIn()
        {
            using (var balance = new TempBalance())
            {
                balance.Write("crops.json", CropsJson);
                balance.Write("production/feed_grinder.json", GrinderJson);
                balance.Write("animals/coop.json", CoopJson.Replace("\"cropId\": \"wheat\"", "\"cropId\": \"rye\""));

                Assert.IsFalse(BalanceLoader.TryLoad(balance.Directory, out _, out var errors));
                Assert.That(
                    errors.Exists(e => e.StartsWith("animals/coop.json: feeds[chicken_feed]") && e.Contains("\"rye\" is not a crop")),
                    string.Join("\n", errors));
            }
        }

        [Test]
        public void Loader_ReportsTheSameIdInTwoFiles()
        {
            using (var balance = new TempBalance())
            {
                balance.Write("crops.json", CropsJson);
                balance.Write("more_crops.json", CropsJson);
                balance.Write("production/feed_grinder.json", GrinderJson);

                Assert.IsFalse(BalanceLoader.TryLoad(balance.Directory, out _, out var errors));
                Assert.That(
                    errors.Exists(e => e.Contains("crops.json and more_crops.json") && e.Contains("used more than once")),
                    string.Join("\n", errors));
            }
        }

        [Test]
        public void Loader_ReportsBrokenJsonAndAMissingSettingsFile()
        {
            using (var balance = new TempBalance())
            {
                balance.Write("crops.json", "{ \"crops\": [ { \"id\": \"wheat\", ");
                Assert.IsFalse(BalanceLoader.TryLoad(balance.Directory, out _, out var errors));
                Assert.That(errors.Exists(e => e.StartsWith("crops.json isn't valid JSON")), string.Join("\n", errors));

                File.Delete(Path.Combine(balance.Directory, BalanceLoader.SettingsFileName));
                Assert.IsFalse(BalanceLoader.TryLoad(balance.Directory, out _, out errors));
                Assert.That(errors.Exists(e => e.Contains("settings.json is missing")), string.Join("\n", errors));
            }
        }

        [Test]
        public void Validate_RejectsBadNoticeSettings()
        {
            var data = new BalanceData
            {
                notices = new NoticeBalance { slots = 0, refillAfterClaimSeconds = -1f, refillAfterDiscardSeconds = 60f }
            };

            var errors = BalanceValidator.Validate(data);
            Assert.That(errors.Exists(e => e.StartsWith("notices.slots")), string.Join("\n", errors));
            Assert.That(errors.Exists(e => e.StartsWith("notices.refillAfterClaimSeconds")), string.Join("\n", errors));
            Assert.IsFalse(errors.Exists(e => e.StartsWith("notices.refillAfterDiscardSeconds")), string.Join("\n", errors));

            data.notices.slots = NoticeBalance.MaxSlots + 1;
            Assert.That(BalanceValidator.Validate(data).Exists(e => e.StartsWith("notices.slots")));
        }

        /// <summary>Catches a balance.json edit that was never applied to the game data.</summary>
        [Test]
        public void GameData_MatchesShippedBalance()
        {
            var data = LoadShipped();
            var content = Resources.Load<ContentDatabase>("Data/ContentDatabase");
            Assert.IsNotNull(content, "No content database. Run Tools > Farm Game > Rebuild Everything.");

            Assert.AreEqual(data.startingCoins, content.StartingCoins, "startingCoins");
            Assert.AreEqual(data.notices.slots, content.Notices.slots, "notices.slots");
            Assert.AreEqual(data.notices.refillAfterClaimSeconds, content.Notices.refillAfterClaimSeconds, 0.001f, "notices.refillAfterClaimSeconds");
            Assert.AreEqual(data.notices.refillAfterDiscardSeconds, content.Notices.refillAfterDiscardSeconds, 0.001f, "notices.refillAfterDiscardSeconds");
            Assert.AreEqual(data.land.orderDeedChance, content.Land.orderDeedChance, 0.001f, "land.orderDeedChance");
            Assert.AreEqual(data.land.visitorDeedChance, content.Land.visitorDeedChance, 0.001f, "land.visitorDeedChance");
            Assert.AreEqual(data.cocktailBar.lineSeconds, content.CocktailBar.lineSeconds, 0.001f, "cocktailBar.lineSeconds");
            Assert.AreEqual(data.cocktailBar.cooldownSeconds, content.CocktailBar.cooldownSeconds, 0.001f, "cocktailBar.cooldownSeconds");
            Assert.AreEqual(data.crops.Length, content.Crops.Count, "crop count");
            for (var i = 0; i < data.crops.Length; i++)
            {
                var expected = data.crops[i];
                var crop = content.GetCrop(expected.id);
                Assert.IsNotNull(crop, $"crop {expected.id} missing from the game data");
                Assert.AreEqual(expected.name, crop.DisplayName, $"{expected.id}.name");
                Assert.AreEqual(expected.growSeconds, crop.GrowSeconds, 0.001f, $"{expected.id}.growSeconds");
                Assert.AreEqual(expected.seedCost, crop.SeedCost, $"{expected.id}.seedCost");
                Assert.AreEqual(expected.sellPrice, crop.SellPrice, $"{expected.id}.sellPrice");
                Assert.AreEqual(expected.yield, crop.YieldAmount, $"{expected.id}.yield");
                Assert.AreEqual(expected.harvestXp, crop.HarvestXp, $"{expected.id}.harvestXp");
            }

            Assert.AreEqual(data.buildings.Length, content.Buildings.Count, "building count");
            foreach (var expected in data.buildings)
            {
                var building = content.GetBuilding(expected.id);
                Assert.IsNotNull(building, $"building {expected.id} missing from the game data");
                Assert.AreEqual(expected.cost, building.Cost, $"{expected.id}.cost");
                Assert.AreEqual(new Vector2Int(expected.width, expected.height), building.Footprint, $"{expected.id} size");
                Assert.AreEqual(expected.animalCapacity, building.AnimalCapacity, $"{expected.id}.animalCapacity");
                Assert.AreEqual(expected.animalCost, building.AnimalCost, $"{expected.id}.animalCost");
                if (building.CanHouseAnimals && !building.SellsAnimals)
                {
                    Assert.AreEqual(expected.productionSeconds, building.ProductionSeconds, 0.001f, $"{expected.id}.productionSeconds");
                }
            }

            Assert.AreEqual(data.fish.Length, content.Fish.Count, "fish count");
            foreach (var expected in data.fish)
            {
                var fish = content.GetFish(expected.id);
                Assert.IsNotNull(fish, $"fish {expected.id} missing from the game data");
                Assert.AreEqual(expected.cost, fish.Cost, $"{expected.id}.cost");
                Assert.AreEqual(expected.unlockLevel, fish.UnlockLevel, $"{expected.id}.unlockLevel");
                Assert.AreEqual(expected.growSeconds, fish.GrowSeconds, 0.001f, $"{expected.id}.growSeconds");
                Assert.AreEqual(expected.sellPrice, fish.SellPrice, $"{expected.id}.sellPrice");
                Assert.AreEqual(expected.sellXp, fish.SellXp, $"{expected.id}.sellXp");
                Assert.IsNotNull(fish.Sprite, $"fish {expected.id} has no picture");
            }

            Assert.AreEqual(data.feeds.Length, content.FeedRecipes.Count, "feed count");
            foreach (var expected in data.feeds)
            {
                var feed = content.GetFeedRecipe(expected.id);
                Assert.IsNotNull(feed, $"feed {expected.id} missing from the game data");
                Assert.AreEqual(expected.cropId, feed.CropId, $"{expected.id}.cropId");
                Assert.AreEqual(expected.cropAmount, feed.CropAmount, $"{expected.id}.cropAmount");
                Assert.AreEqual(expected.productionSeconds, feed.ProductionSeconds, 0.001f, $"{expected.id}.productionSeconds");
            }

            Assert.AreEqual(data.products.Length, content.Products.Count, "product count");
            foreach (var expected in data.products)
            {
                var product = content.GetProduct(expected.id);
                Assert.IsNotNull(product, $"product {expected.id} missing from the game data");
                Assert.AreEqual(expected.sellPrice, product.SellPrice, $"{expected.id}.sellPrice");
                Assert.AreEqual(expected.collectXp, product.CollectXp, $"{expected.id}.collectXp");
            }

            Assert.AreEqual(data.goodsRecipes.Length, content.GoodsRecipes.Count, "goods recipe count");
            foreach (var expected in data.goodsRecipes)
            {
                var recipe = content.GetGoodsRecipe(expected.outputId);
                Assert.IsNotNull(recipe, $"recipe {expected.outputId} missing from the game data");
                Assert.AreEqual(expected.buildingId, recipe.BuildingId, $"{expected.outputId}.buildingId");
                Assert.AreEqual(expected.productionSeconds, recipe.ProductionSeconds, 0.001f, $"{expected.outputId}.productionSeconds");
                Assert.AreEqual(expected.inputs.Length, recipe.Inputs.Count, $"{expected.outputId} input count");
            }
        }

        // ------------------------------------------------------------------ The validator

        private static BalanceData Minimal()
        {
            return new BalanceData
            {
                startingCoins = 100,
                crops = new[] { new CropBalance { id = "wheat", name = "Wheat", growSeconds = 60, sellPrice = 2, yield = 2 } },
                products = new[]
                {
                    new ProductBalance { id = "egg", name = "Egg", sellPrice = 45 },
                    new ProductBalance { id = "tart", name = "Tart", sellPrice = 130 }
                },
                feeds = new[]
                {
                    new FeedBalance { id = "chicken_feed", name = "Chicken Feed", animal = "Chicken", cropId = "wheat", cropAmount = 3, productionSeconds = 300 }
                },
                buildings = new[]
                {
                    new BuildingBalance
                    {
                        id = "coop", name = "Coop", category = "Animals", width = 2, height = 2, animal = "Chicken",
                        animalCapacity = 3, productId = "egg", productionSeconds = 480
                    },
                    new BuildingBalance { id = BalanceValidator.FeedGrinderId, name = "Grinder", category = "Production", width = 2, height = 2 },
                    new BuildingBalance { id = "kitchen", name = "Kitchen", category = "Goods", width = 2, height = 2 }
                },
                goodsRecipes = new[]
                {
                    new GoodsRecipeBalance
                    {
                        outputId = "tart", buildingId = "kitchen", productionSeconds = 360,
                        inputs = new[] { new ItemAmount("egg", 2), new ItemAmount("wheat", 2) }
                    }
                }
            };
        }

        private static void AssertProblem(BalanceData data, string expected)
        {
            var errors = BalanceValidator.Validate(data);
            Assert.That(errors.Exists(e => e.Contains(expected)), $"Expected a problem mentioning \"{expected}\", got:\n" + string.Join("\n", errors));
        }

        [Test]
        public void MinimalBalance_IsValid()
        {
            Assert.IsEmpty(BalanceValidator.Validate(Minimal()));
        }

        [Test]
        public void Validator_CatchesBrokenLinks()
        {
            var data = Minimal();
            data.feeds[0].cropId = "rye";
            AssertProblem(data, "\"rye\" is not a crop");

            data = Minimal();
            data.goodsRecipes[0].inputs[0] = new ItemAmount("egg_yolk", 1);
            AssertProblem(data, "\"egg_yolk\" is not a crop, product or feed");

            data = Minimal();
            data.buildings[0].productId = "feather";
            AssertProblem(data, "\"feather\" is not a product");

            data = Minimal();
            data.goodsRecipes[0].buildingId = "coop";
            AssertProblem(data, "is not a Goods building");
        }

        [Test]
        public void Validator_CatchesBadValues()
        {
            var data = Minimal();
            data.crops[0].growSeconds = 0;
            AssertProblem(data, "growSeconds must be more than 0");

            data = Minimal();
            data.buildings[0].category = "Animal";
            AssertProblem(data, "category \"Animal\"");

            data = Minimal();
            data.feeds[0].animal = "Horse";
            AssertProblem(data, "animal \"Horse\"");

            data = Minimal();
            data.products[1].id = "wheat";
            AssertProblem(data, "\"wheat\" is used more than once");

            data = Minimal();
            data.feeds = new FeedBalance[0];
            AssertProblem(data, "no feed in \"feeds\" is for Chicken");
        }

        [Test]
        public void Validator_ReportsInvalidJsonAsNull()
        {
            Assert.IsNotEmpty(BalanceValidator.Validate(null));
        }
    }
}
