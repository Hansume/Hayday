using System.IO;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Inventory;
using FarmGame.Save;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class StorageTests
    {
        private FakeClock _clock;
        private CropDefinition _wheat;
        private BuildingDefinition _coop;
        private BuildingDefinition _dairy;
        private GoodsRecipeDefinition _butter;
        private ContentDatabase _database;
        private GameSession _session;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _wheat = TestContent.CreateCrop("wheat", growSeconds: 60f, seedCost: 0, yieldAmount: 2);
            _coop = TestContent.CreateBuilding(
                "coop",
                cost: 0,
                animalKind: AnimalKind.Chicken,
                animalCapacity: 3,
                productId: "egg",
                productionSeconds: 60f);
            _dairy = TestContent.CreateBuilding("dairy", cost: 0, category: BuildingCategory.Goods);
            _butter = TestContent.CreateGoodsRecipe("butter", "dairy", 60f, new ItemAmount("milk", 1));
            _database = TestContent.CreateDatabase(new[] { _wheat }, new[] { _coop, _dairy }, new[] { _butter });
            _database.ConfigureStorage(new StorageBalance
            {
                baseCapacity = 10,
                capacityPerUpgrade = 5,
                upgradeBaseCost = 1000,
                upgradeCostGrowth = 2f,
                maxUpgrades = 2
            });
            _session = new GameSession(_database, _clock, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            Object.DestroyImmediate(_wheat);
            Object.DestroyImmediate(_coop);
            Object.DestroyImmediate(_dairy);
        }

        [Test]
        public void EverythingHeldCountsTowardsTheLimit()
        {
            _session.Inventory.Add("wheat", 3);
            _session.Inventory.Add("egg", 2);
            _session.Inventory.Add(GameSession.FishFeedId, 1);
            Assert.AreEqual(10, _session.Storage.Capacity);
            Assert.AreEqual(6, _session.Storage.Used);
            Assert.AreEqual(4, _session.Storage.Free);
        }

        [Test]
        public void Harvest_WaitsWhenTheCropWouldNotFit()
        {
            _session.Inventory.Add("egg", 9);
            Assert.IsTrue(_session.TryPlant(0, "wheat", out var message), message);
            _clock.AdvanceSeconds(61);

            Assert.IsFalse(_session.TryHarvest(0, out _, out message));
            Assert.AreEqual(Storage.FullMessage, message);
            Assert.IsTrue(_session.Farm.GetSnapshot(0).IsReady, "The crop stays in the field until there's room.");

            _session.Inventory.TryRemove("egg", 1);
            Assert.IsTrue(_session.TryHarvest(0, out _, out message), message);
            Assert.AreEqual(10, _session.Storage.Used);
        }

        [Test]
        public void AnimalProducts_OnlyAsManyAsFit_TheRestWait()
        {
            Assert.IsTrue(_session.TryPlaceBuilding("coop", new Vector2Int(5, 5), out var message), message);
            var coop = _session.Buildings.Placed[_session.Buildings.Placed.Count - 1];
            for (var i = 0; i < 3; i++)
            {
                Assert.IsTrue(_session.TryAddAnimal(coop, out message), message);
            }

            _session.Inventory.Add(GameSession.ChickenFeedId, 3);
            Assert.AreEqual(3, _session.FeedAllAnimals(coop, out message), message);
            _session.Inventory.Add("wheat", 8); // 8 of 10 used once the feed is eaten.
            _clock.AdvanceSeconds(61);

            Assert.AreEqual(2, _session.CollectAnimalProducts(coop, out message));
            StringAssert.Contains("Storage is full", message);
            Assert.AreEqual(1, CountReady(coop), "One chicken keeps its egg.");

            Assert.AreEqual(0, _session.CollectAnimalProducts(coop, out message));
            Assert.AreEqual(Storage.FullMessage, message);
            Assert.IsFalse(_session.TryCollectAnimalProduct(coop, FirstReady(coop), out message));
            Assert.AreEqual(Storage.FullMessage, message);
        }

        [Test]
        public void Goods_StayInTheQueueWhenFull_OldestCollectedFirst()
        {
            Assert.IsTrue(_session.TryPlaceBuilding("dairy", new Vector2Int(5, 5), out var message), message);
            var dairy = _session.Buildings.Placed[_session.Buildings.Placed.Count - 1];
            _session.Inventory.Add("milk", 2);
            Assert.IsTrue(_session.TryStartGoods(dairy, "butter", out message), message);
            Assert.IsTrue(_session.TryStartGoods(dairy, "butter", out message), message);
            _session.Inventory.Add("wheat", 9); // 9 of 10 used: room for one butter.
            _clock.AdvanceSeconds(200);

            Assert.AreEqual(1, _session.CollectReadyGoods(dairy, out message));
            StringAssert.Contains("Storage is full", message);
            Assert.AreEqual(1, dairy.Production.Count, "The second butter waits in the dairy.");

            Assert.AreEqual(0, _session.CollectReadyGoods(dairy, out message));
            Assert.AreEqual(Storage.FullMessage, message);

            _session.TrySell("wheat", out _, out _, out _);
            Assert.AreEqual(1, _session.CollectReadyGoods(dairy, out message), message);
            Assert.AreEqual(0, dairy.Production.Count);
        }

        [Test]
        public void Upgrade_CostsCoins_GrowsSteeply_AndStops()
        {
            var storage = _session.Storage;
            Assert.AreEqual(1000, storage.NextUpgradeCost);
            Assert.IsFalse(_session.TryUpgradeStorage(out var message), "100 coins can't buy a 1,000 coin upgrade.");
            Assert.AreEqual(10, storage.Capacity);

            _session.Wallet.Add(5000);
            Assert.IsTrue(_session.TryUpgradeStorage(out message), message);
            Assert.AreEqual(15, storage.Capacity);
            Assert.AreEqual(4100, _session.Wallet.Coins);
            Assert.AreEqual(2000, storage.NextUpgradeCost);

            Assert.IsTrue(_session.TryUpgradeStorage(out message), message);
            Assert.AreEqual(20, storage.Capacity);
            Assert.IsTrue(storage.IsMaxed);
            Assert.IsFalse(_session.TryUpgradeStorage(out _));
            Assert.AreEqual(2100, _session.Wallet.Coins);
        }

        [Test]
        public void Upgrades_AreSavedAndRestored()
        {
            var directory = Path.Combine(Path.GetTempPath(), "FarmGameTests", System.Guid.NewGuid().ToString("N"));
            try
            {
                var saves = new SaveService(directory, "save.json");
                var session = new GameSession(_database, _clock, saves, null);
                session.Wallet.Add(1000);
                Assert.IsTrue(session.TryUpgradeStorage(out var message), message);

                var data = saves.TryLoad(out var error);
                Assert.IsNotNull(data, error);
                Assert.AreEqual(1, data.storageUpgrades);
                var reloaded = new GameSession(_database, _clock, saves, data);
                Assert.AreEqual(15, reloaded.Storage.Capacity);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void ShippedUpgradeCosts_AreHuge()
        {
            var shipped = new StorageBalance();
            Assert.GreaterOrEqual(shipped.UpgradeCost(0), 10000);
            Assert.Greater(shipped.UpgradeCost(5), shipped.UpgradeCost(0) * 5, "Costs should climb steeply.");
        }

        private int CountReady(Buildings.PlacedBuilding building)
        {
            var ready = 0;
            for (var i = 0; i < building.AnimalCount; i++)
            {
                if (_session.GetAnimalStatus(building, i, out _) == AnimalStatus.Ready)
                {
                    ready++;
                }
            }

            return ready;
        }

        private int FirstReady(Buildings.PlacedBuilding building)
        {
            for (var i = 0; i < building.AnimalCount; i++)
            {
                if (_session.GetAnimalStatus(building, i, out _) == AnimalStatus.Ready)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
