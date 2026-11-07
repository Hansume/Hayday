using System;
using System.IO;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Save;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class GoodsProductionTests
    {
        private const float ButterSeconds = 480f;
        private const float PieSeconds = 720f;

        private string _directory;
        private FakeClock _clock;
        private CropDefinition _wheat;
        private BuildingDefinition _dairy;
        private BuildingDefinition _smokehouse;
        private ContentDatabase _database;
        private SaveService _saveService;
        private GameSession _session;
        private PlacedBuilding _placedDairy;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "FarmGameTests", Guid.NewGuid().ToString("N"));
            _clock = new FakeClock();
            _wheat = TestContent.CreateCrop();
            _dairy = TestContent.CreateBuilding("dairy", 0, 2, 2, BuildingCategory.Goods);
            _smokehouse = TestContent.CreateBuilding("smokehouse", 0, 2, 2, BuildingCategory.Goods);
            var recipes = new[]
            {
                TestContent.CreateGoodsRecipe("butter", "dairy", ButterSeconds, new ItemAmount("milk", 2)),
                TestContent.CreateGoodsRecipe(
                    "fish_pie",
                    "smokehouse",
                    PieSeconds,
                    new ItemAmount("fish_fillet", 2),
                    new ItemAmount("butter", 1),
                    new ItemAmount("wheat", 2))
            };
            _database = TestContent.CreateDatabase(new[] { _wheat }, new[] { _dairy, _smokehouse }, recipes);
            _saveService = new SaveService(_directory, "save.json");

            _session = new GameSession(_database, _clock, _saveService, new SaveDataV1 { coins = 1000 });
            Assert.IsTrue(_session.TryPlaceBuilding("dairy", new Vector2Int(5, 5), out var message), message);
            _placedDairy = _session.Buildings.Placed[0];
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            UnityEngine.Object.DestroyImmediate(_dairy);
            UnityEngine.Object.DestroyImmediate(_smokehouse);
            UnityEngine.Object.DestroyImmediate(_wheat);
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void GoodsBuilding_IsAProductionBuilding()
        {
            Assert.IsTrue(_session.IsGoodsBuilding(_placedDairy));
            Assert.IsTrue(_session.IsProductionBuilding(_placedDairy));
            Assert.IsFalse(_session.IsFeedGrinder(_placedDairy));
            CollectionAssert.AreEqual(
                new[] { "butter" },
                _database.GetGoodsRecipesFor("dairy").ConvertAll(recipe => recipe.Id));
        }

        [Test]
        public void StartingGoods_TakesIngredientsAndFinishesAfterItsTime()
        {
            _session.Inventory.Add("milk", 3);

            Assert.IsTrue(_session.TryStartGoods(_placedDairy, "butter", out var message), message);
            Assert.AreEqual(1, _session.Inventory.GetCount("milk"), "Butter takes 2 milk.");
            Assert.AreEqual(1, _placedDairy.Production.Count);

            _clock.AdvanceSeconds(ButterSeconds - 1);
            Assert.AreEqual(0, _session.CollectReadyGoods(_placedDairy, out _), "Not ready yet.");

            _clock.AdvanceSeconds(2);
            Assert.AreEqual(1, _session.CollectReadyGoods(_placedDairy, out var collected));
            StringAssert.Contains("1 Butter", collected);
            StringAssert.Contains("+15 XP", collected);
            Assert.AreEqual(1, _session.Inventory.GetCount("butter"));
            // 15 XP: level 2 (10) with 5 left over.
            Assert.AreEqual(2, _session.Level.Level);
            Assert.AreEqual(5, _session.Level.Xp);
            Assert.AreEqual(0, _placedDairy.Production.Count);
        }

        [Test]
        public void StartingGoods_WithoutIngredients_FailsAndTakesNothing()
        {
            _session.Inventory.Add("milk", 1);

            Assert.IsFalse(_session.TryStartGoods(_placedDairy, "butter", out var message));
            StringAssert.Contains("1 Milk", message);
            Assert.AreEqual(1, _session.Inventory.GetCount("milk"));
            Assert.AreEqual(0, _placedDairy.Production.Count);
        }

        [Test]
        public void MultiIngredientRecipe_NamesEveryMissingIngredient()
        {
            Assert.IsTrue(_session.TryPlaceBuilding("smokehouse", new Vector2Int(8, 8), out var placeMessage), placeMessage);
            var smokehouse = _session.Buildings.Placed[1];
            _session.Inventory.Add("fish_fillet", 2);
            _session.Inventory.Add("wheat", 1);

            Assert.IsFalse(_session.TryStartGoods(smokehouse, "fish_pie", out var message));
            StringAssert.Contains("1 Butter", message);
            StringAssert.Contains("1 wheat", message);
            Assert.AreEqual(2, _session.Inventory.GetCount("fish_fillet"), "Nothing is taken when short.");

            _session.Inventory.Add("butter", 1);
            _session.Inventory.Add("wheat", 1);
            Assert.IsTrue(_session.TryStartGoods(smokehouse, "fish_pie", out message), message);
            Assert.AreEqual(0, _session.Inventory.GetCount("fish_fillet"));
            Assert.AreEqual(0, _session.Inventory.GetCount("butter"));
            Assert.AreEqual(0, _session.Inventory.GetCount("wheat"));
        }

        [Test]
        public void Building_OnlyMakesItsOwnRecipes()
        {
            _session.Inventory.Add("fish_fillet", 2);
            _session.Inventory.Add("butter", 1);
            _session.Inventory.Add("wheat", 2);

            Assert.IsFalse(_session.TryStartGoods(_placedDairy, "fish_pie", out var message));
            StringAssert.Contains("can't make", message);
            Assert.AreEqual(2, _session.Inventory.GetCount("fish_fillet"));
        }

        [Test]
        public void Queue_RunsJobsOneAfterAnotherAndStopsAtCapacity()
        {
            _session.Inventory.Add("milk", 10);
            for (var i = 0; i < GameSession.FeedQueueCapacity; i++)
            {
                Assert.IsTrue(_session.TryStartGoods(_placedDairy, "butter", out var message), message);
            }

            Assert.IsFalse(_session.TryStartGoods(_placedDairy, "butter", out var full));
            StringAssert.Contains("full", full);
            Assert.AreEqual(4, _session.Inventory.GetCount("milk"), "A rejected job takes no milk.");

            // The second job starts when the first finishes.
            _clock.AdvanceSeconds(ButterSeconds + 1);
            Assert.AreEqual(1, _session.CollectReadyGoods(_placedDairy, out _));
            _clock.AdvanceSeconds(ButterSeconds);
            Assert.AreEqual(1, _session.CollectReadyGoods(_placedDairy, out _));
            Assert.AreEqual(1, _placedDairy.Production.Count);
        }

        [Test]
        public void GoodsQueue_IsSavedAndRestored()
        {
            _session.Inventory.Add("milk", 2);
            Assert.IsTrue(_session.TryStartGoods(_placedDairy, "butter", out var message), message);

            var restored = new GameSession(_database, _clock, null, _session.BuildSaveData());
            var dairy = restored.Buildings.Placed[0];
            Assert.AreEqual(1, dairy.Production.Count);
            Assert.AreEqual("butter", dairy.Production[0].ItemId);

            _clock.AdvanceSeconds(ButterSeconds + 1);
            Assert.AreEqual(1, restored.CollectReadyGoods(dairy, out _));
            Assert.AreEqual(1, restored.Inventory.GetCount("butter"));
        }

        [Test]
        public void Goats_EatGoatFeed_GiveGoatMilk_ThatBecomesGoatCheese()
        {
            var goatStable = TestContent.CreateBuilding(
                "goat_stable", 0, 2, 2, BuildingCategory.Animals, AnimalKind.Goat, 3, 0, "goat_milk", 1200f);
            var dairyDefinition = TestContent.CreateBuilding("dairy", 0, 2, 2, BuildingCategory.Goods);
            var recipes = new[]
            {
                TestContent.CreateGoodsRecipe("goat_cheese", "dairy", 1200f, new ItemAmount("goat_milk", 2))
            };
            var database = TestContent.CreateDatabase(new[] { _wheat }, new[] { goatStable, dairyDefinition }, recipes);
            try
            {
                var session = new GameSession(database, _clock, null, new SaveDataV1 { coins = 1000 });
                Assert.IsTrue(session.TryPlaceBuilding("goat_stable", new Vector2Int(5, 5), out var message), message);
                Assert.IsTrue(session.TryPlaceBuilding("dairy", new Vector2Int(8, 8), out message), message);
                var stable = session.Buildings.Placed[0];
                var dairy = session.Buildings.Placed[1];
                Assert.AreEqual("Goat", stable.Definition.AnimalDisplayName);
                Assert.AreEqual("goat_feed", session.GetFeedFor(stable).Id);

                Assert.IsTrue(session.TryAddAnimal(stable, out message), message);
                Assert.IsTrue(session.TryAddAnimal(stable, out message), message);
                session.Inventory.Add("goat_feed", 2);
                Assert.AreEqual(2, session.FeedAllAnimals(stable, out message), message);
                StringAssert.Contains("goats", message);

                _clock.AdvanceSeconds(1201);
                Assert.AreEqual(2, session.CollectAnimalProducts(stable, out message), message);
                Assert.AreEqual(2, session.Inventory.GetCount("goat_milk"));

                Assert.IsTrue(session.TryStartGoods(dairy, "goat_cheese", out message), message);
                _clock.AdvanceSeconds(1201);
                Assert.AreEqual(1, session.CollectReadyGoods(dairy, out message), message);
                Assert.AreEqual(1, session.Inventory.GetCount("goat_cheese"));
            }
            finally
            {
                TestContent.DestroyDatabase(database);
                UnityEngine.Object.DestroyImmediate(goatStable);
                UnityEngine.Object.DestroyImmediate(dairyDefinition);
            }
        }

        [Test]
        public void Goods_CanBeSold()
        {
            _session.Inventory.Add("butter", 2);
            var coinsBefore = _session.Wallet.Coins;

            Assert.IsTrue(_session.TrySell("butter", out var total, out var quantity, out var message), message);
            Assert.AreEqual(2, quantity);
            Assert.AreEqual(560, total);
            Assert.AreEqual(coinsBefore + 560, _session.Wallet.Coins);
        }
    }
}
