using System;
using System.Collections.Generic;
using System.IO;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Save;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    /// <summary>
    /// The fish pond: the player buys a kind of fish (each with its own price and unlock level),
    /// feeds it once, and sells it when grown for that kind's coins and XP. Fish make no product.
    /// Test fish: Goldfish (level 1, 20 coins, 240 s, sells 40 / 4 XP) and Trout (level 7, 75 coins,
    /// 900 s, sells 145 / 10 XP).
    /// </summary>
    public sealed class FishPondTests
    {
        private string _directory;
        private FakeClock _clock;
        private CropDefinition _crop;
        private BuildingDefinition _pond;
        private ContentDatabase _database;
        private SaveService _saveService;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "FarmGameTests", Guid.NewGuid().ToString("N"));
            _clock = new FakeClock();
            _crop = TestContent.CreateCrop();
            _pond = TestContent.CreateBuilding("fish_pond", 0, 2, 2, BuildingCategory.Animals, AnimalKind.Fish, 4);
            _database = TestContent.CreateDatabase(new[] { _crop }, new[] { _pond });
            _database.ConfigureFish(TestContent.CreateStandardFish());
            _saveService = new SaveService(_directory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            UnityEngine.Object.DestroyImmediate(_crop);
            UnityEngine.Object.DestroyImmediate(_pond);
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        private GameSession NewSession(int coins, int level, out PlacedBuilding pond)
        {
            var session = new GameSession(_database, _clock, _saveService, new SaveDataV1 { coins = coins, level = level });
            Assert.IsTrue(session.TryPlaceBuilding("fish_pond", new Vector2Int(5, 5), out var message), message);
            pond = session.Buildings.Placed[0];
            return session;
        }

        [Test]
        public void Pond_SellsFishAndMakesNoProduct()
        {
            Assert.IsTrue(_pond.SellsAnimals);
            var session = NewSession(100, 1, out var pond);
            Assert.IsNull(session.GetProductFor(pond));
            Assert.IsFalse(session.TryAddAnimal(pond, out var message), "A pond needs a kind of fish picked.");
            StringAssert.Contains("Choose", message);
            Assert.AreEqual(0, pond.AnimalCount);
        }

        [Test]
        public void TryAddFish_ChargesThatFishsCostAndRespectsLevelAndSpace()
        {
            var session = NewSession(200, 1, out var pond);

            Assert.IsFalse(session.TryAddFish(pond, "trout", out var locked), "Trout unlocks at level 7.");
            StringAssert.Contains("level 7", locked);
            Assert.AreEqual(200, session.Wallet.Coins);

            for (var i = 0; i < 4; i++)
            {
                Assert.IsTrue(session.TryAddFish(pond, "goldfish", out var message), message);
            }

            Assert.AreEqual(4, pond.AnimalCount);
            Assert.AreEqual(120, session.Wallet.Coins, "4 goldfish at 20 coins each.");
            Assert.AreEqual("Goldfish", session.GetFishAt(pond, 0).DisplayName);
            Assert.IsFalse(session.TryAddFish(pond, "goldfish", out var full));
            StringAssert.Contains("full", full);
            Assert.AreEqual(120, session.Wallet.Coins);
        }

        [Test]
        public void FishUnlocks_AreAnnouncedOnLevelUp()
        {
            Assert.AreEqual(7, _database.GetNextUnlockLevel(1), "Trout is the next thing to unlock.");
            var unlocks = _database.GetUnlockDetailsAt(7);
            Assert.AreEqual(1, unlocks.Count);
            Assert.AreEqual(UnlockKind.Fish, unlocks[0].Kind);
            Assert.AreEqual("trout", unlocks[0].Id);
            Assert.AreEqual("New fish", unlocks[0].Description);
        }

        [Test]
        public void TryAddFish_WithTooFewCoins_FailsWithoutSpending()
        {
            var session = NewSession(10, 1, out var pond);
            Assert.IsFalse(session.TryAddFish(pond, "goldfish", out var message));
            StringAssert.Contains("20 coins", message);
            Assert.AreEqual(0, pond.AnimalCount);
            Assert.AreEqual(10, session.Wallet.Coins);
        }

        [Test]
        public void FedFish_GrowsForItsOwnTime_ThenSellsForItsPriceAndXp()
        {
            var session = NewSession(20, 1, out var pond);
            Assert.IsTrue(session.TryAddFish(pond, "goldfish", out var addMessage), addMessage);
            Assert.AreEqual(0, session.Wallet.Coins);

            Assert.IsFalse(session.TrySellFish(pond, 0, out var hungry), "A hungry fish hasn't grown.");
            StringAssert.Contains("Feed", hungry);

            session.Inventory.Add(GameSession.FishFeedId, 1);
            Assert.IsTrue(session.TryFeedAnimal(pond, 0, out var fedMessage), fedMessage);
            StringAssert.Contains("Grown in", fedMessage);
            Assert.AreEqual(AnimalStatus.Producing, session.GetAnimalStatus(pond, 0, out var remaining));
            Assert.AreEqual(240d, remaining, 0.5d);

            _clock.AdvanceSeconds(239);
            Assert.IsFalse(session.TrySellFish(pond, 0, out _), "Not grown yet.");

            _clock.AdvanceSeconds(2);
            Assert.AreEqual(AnimalStatus.Ready, session.GetAnimalStatus(pond, 0, out _));
            Assert.IsTrue(session.TryCollectAnimalProduct(pond, 0, out var sold), "Tapping a grown fish sells it.");
            StringAssert.Contains("Sold a Goldfish for 40 coins", sold);
            Assert.AreEqual(40, session.Wallet.Coins);
            Assert.AreEqual(4, session.Level.Xp);
            Assert.AreEqual(0, pond.AnimalCount, "A sold fish leaves the pond.");
            Assert.AreEqual(0, session.Storage.Used, "Fish never go into storage.");
        }

        [Test]
        public void SellGrownFish_SellsOnlyGrownFish_AndKeepsTheRestInOrder()
        {
            var session = NewSession(1000, 7, out var pond);
            Assert.IsTrue(session.TryAddFish(pond, "goldfish", out var m1), m1);
            Assert.IsTrue(session.TryAddFish(pond, "trout", out var m2), m2);
            Assert.IsTrue(session.TryAddFish(pond, "goldfish", out var m3), m3);
            Assert.AreEqual(1000 - 20 - 75 - 20, session.Wallet.Coins);
            var coinsBefore = session.Wallet.Coins;

            session.Inventory.Add(GameSession.FishFeedId, 2);
            Assert.AreEqual(2, session.FeedAllAnimals(pond, out var feedMessage), feedMessage);

            // Goldfish (240 s) grow first; the trout (900 s) is still growing; the last goldfish is hungry.
            _clock.AdvanceSeconds(300);
            Assert.AreEqual(1, session.SellGrownFish(pond, out var soldMessage), soldMessage);
            Assert.AreEqual(coinsBefore + 40, session.Wallet.Coins);
            Assert.AreEqual(2, pond.AnimalCount);
            Assert.AreEqual("trout", pond.GetAnimalKindId(0));
            Assert.AreEqual("goldfish", pond.GetAnimalKindId(1));
            Assert.AreEqual(AnimalStatus.Producing, session.GetAnimalStatus(pond, 0, out _));
            Assert.AreEqual(AnimalStatus.Hungry, session.GetAnimalStatus(pond, 1, out _));

            _clock.AdvanceSeconds(600);
            Assert.AreEqual(1, session.SellGrownFish(pond, out _));
            Assert.AreEqual(coinsBefore + 40 + 145, session.Wallet.Coins);
            Assert.AreEqual(1, pond.AnimalCount);

            Assert.AreEqual(0, session.SellGrownFish(pond, out var none));
            StringAssert.Contains("No fish", none);
        }

        [Test]
        public void FishKindsAndGrowth_SurviveSaveAndLoad()
        {
            var session = NewSession(1000, 7, out var pond);
            Assert.IsTrue(session.TryAddFish(pond, "trout", out var m1), m1);
            Assert.IsTrue(session.TryAddFish(pond, "goldfish", out var m2), m2);
            session.Inventory.Add(GameSession.FishFeedId, 1);
            Assert.IsTrue(session.TryFeedAnimal(pond, 0, out var fed), fed);
            _clock.AdvanceSeconds(100);
            Assert.IsTrue(session.SaveNow());

            var loaded = _saveService.TryLoad(out var error);
            Assert.IsNull(error);
            CollectionAssert.AreEqual(new[] { "trout", "goldfish" }, loaded.buildings[0].animalKinds);

            var restored = new GameSession(_database, _clock, _saveService, loaded);
            var restoredPond = restored.Buildings.Placed[0];
            Assert.AreEqual("Trout", restored.GetFishAt(restoredPond, 0).DisplayName);
            Assert.AreEqual("Goldfish", restored.GetFishAt(restoredPond, 1).DisplayName);
            Assert.AreEqual(AnimalStatus.Producing, restored.GetAnimalStatus(restoredPond, 0, out var remaining));
            Assert.AreEqual(800d, remaining, 1d);
            Assert.AreEqual(AnimalStatus.Hungry, restored.GetAnimalStatus(restoredPond, 1, out _));
        }

        [Test]
        public void OldSaveFish_WithoutKinds_BecomeTheStarterFish()
        {
            var saveData = new SaveDataV1
            {
                version = 12,
                coins = 50,
                buildings = new List<BuildingSaveDto>
                {
                    new BuildingSaveDto { buildingId = "fish_pond", cellX = 5, cellY = 5, animalCount = 2 }
                }
            };

            var session = new GameSession(_database, _clock, _saveService, saveData);
            var pond = session.Buildings.Placed[0];
            Assert.AreEqual(2, pond.AnimalCount);
            Assert.AreEqual("goldfish", pond.GetAnimalKindId(0));
            Assert.AreEqual("goldfish", pond.GetAnimalKindId(1));
        }

        [Test]
        public void Validator_RequiresFishToBeProfitableAndPondsToHaveNoProduct()
        {
            BalanceData Data(int sellPrice, string productId) => new BalanceData
            {
                crops = new[] { new CropBalance { id = "wheat", name = "Wheat", growSeconds = 60, yield = 2 } },
                feeds = new[]
                {
                    new FeedBalance { id = "fish_feed", name = "Fish Feed", animal = "Fish", cropId = "wheat", cropAmount = 3, productionSeconds = 120 }
                },
                buildings = new[]
                {
                    new BuildingBalance
                    {
                        id = "fish_pond", name = "Fish Pond", category = "Animals", width = 2, height = 2,
                        animal = "Fish", animalCapacity = 4, productId = productId
                    },
                    new BuildingBalance { id = BalanceValidator.FeedGrinderId, name = "Grinder", category = "Production", width = 2, height = 2 }
                },
                fish = new[]
                {
                    new FishBalance { id = "goldfish", name = "Goldfish", cost = 20, unlockLevel = 1, growSeconds = 240, sellPrice = sellPrice, sellXp = 4 }
                }
            };

            CollectionAssert.IsEmpty(BalanceValidator.Validate(Data(40, "")));

            var unprofitable = BalanceValidator.Validate(Data(20, ""));
            Assert.IsTrue(unprofitable.Exists(e => e.Contains("sellPrice")), string.Join("\n", unprofitable));

            var withProduct = BalanceValidator.Validate(Data(40, "fish_fillet"));
            Assert.IsTrue(withProduct.Exists(e => e.Contains("productId")), string.Join("\n", withProduct));

            var noFish = Data(40, "");
            noFish.fish = Array.Empty<FishBalance>();
            Assert.IsTrue(BalanceValidator.Validate(noFish).Exists(e => e.StartsWith("fish:")));
        }
    }
}
