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
    public sealed class AnimalFeedingTests
    {
        private const float EggSeconds = 480f;

        private string _directory;
        private FakeClock _clock;
        private CropDefinition _crop;
        private BuildingDefinition _coop;
        private ContentDatabase _database;
        private SaveService _saveService;
        private GameSession _session;
        private PlacedBuilding _placedCoop;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "FarmGameTests", Guid.NewGuid().ToString("N"));
            _clock = new FakeClock();
            _crop = TestContent.CreateCrop();
            _coop = TestContent.CreateBuilding(
                "coop",
                0,
                2,
                2,
                BuildingCategory.Animals,
                AnimalKind.Chicken,
                3,
                0,
                "egg",
                EggSeconds);
            _database = TestContent.CreateDatabase(new[] { _crop }, new[] { _coop });
            _saveService = new SaveService(_directory, "save.json");

            _session = new GameSession(_database, _clock, _saveService, new SaveDataV1 { coins = 1000 });
            Assert.IsTrue(_session.TryPlaceBuilding("coop", new Vector2Int(5, 5), out var message), message);
            _placedCoop = _session.Buildings.Placed[0];
            for (var i = 0; i < 3; i++)
            {
                Assert.IsTrue(_session.TryAddAnimal(_placedCoop, out var addMessage), addMessage);
            }
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            UnityEngine.Object.DestroyImmediate(_coop);
            UnityEngine.Object.DestroyImmediate(_crop);
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void NewAnimals_StartHungry()
        {
            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(AnimalStatus.Hungry, _session.GetAnimalStatus(_placedCoop, i, out _));
            }

            Assert.AreEqual(GameSession.ChickenFeedId, _session.GetFeedFor(_placedCoop).Id);
            Assert.AreEqual("egg", _session.GetProductFor(_placedCoop).Id);
        }

        [Test]
        public void FeedingAnimal_UsesOnePackThenProducesAfterItsTime()
        {
            _session.Inventory.Add(GameSession.ChickenFeedId, 2);

            Assert.IsTrue(_session.TryFeedAnimal(_placedCoop, 0, out var message), message);
            Assert.AreEqual(1, _session.Inventory.GetCount(GameSession.ChickenFeedId), "Feeding costs exactly 1 pack.");
            Assert.AreEqual(AnimalStatus.Producing, _session.GetAnimalStatus(_placedCoop, 0, out var remaining));
            Assert.AreEqual(EggSeconds, remaining, 0.01d);

            Assert.IsFalse(_session.TryFeedAnimal(_placedCoop, 0, out _), "An animal that has eaten can't eat again.");
            Assert.AreEqual(1, _session.Inventory.GetCount(GameSession.ChickenFeedId));

            _clock.AdvanceSeconds(EggSeconds - 1);
            Assert.IsFalse(_session.TryCollectAnimalProduct(_placedCoop, 0, out var tooEarly));
            StringAssert.Contains("ready in", tooEarly);

            _clock.AdvanceSeconds(2);
            Assert.AreEqual(AnimalStatus.Ready, _session.GetAnimalStatus(_placedCoop, 0, out _));
            Assert.IsTrue(_session.TryCollectAnimalProduct(_placedCoop, 0, out var collected), collected);
            Assert.AreEqual(1, _session.Inventory.GetCount("egg"));
            Assert.AreEqual(AnimalStatus.Hungry, _session.GetAnimalStatus(_placedCoop, 0, out _), "Collecting makes it hungry again.");
        }

        [Test]
        public void FeedingAnimal_WithoutFeed_Fails()
        {
            Assert.IsFalse(_session.TryFeedAnimal(_placedCoop, 0, out var message));
            StringAssert.Contains("Chicken Feed", message);
            Assert.AreEqual(AnimalStatus.Hungry, _session.GetAnimalStatus(_placedCoop, 0, out _));
        }

        [Test]
        public void FeedAll_FeedsAsManyAnimalsAsTheFeedAllows()
        {
            _session.Inventory.Add(GameSession.ChickenFeedId, 2);
            _session.Inventory.Add(GameSession.FishFeedId, 5);

            Assert.AreEqual(2, _session.FeedAllAnimals(_placedCoop, out var message));
            StringAssert.Contains("Need 1 more Chicken Feed", message);
            Assert.AreEqual(0, _session.Inventory.GetCount(GameSession.ChickenFeedId));
            Assert.AreEqual(5, _session.Inventory.GetCount(GameSession.FishFeedId), "Chickens only eat chicken feed.");
            Assert.AreEqual(AnimalStatus.Producing, _session.GetAnimalStatus(_placedCoop, 0, out _));
            Assert.AreEqual(AnimalStatus.Producing, _session.GetAnimalStatus(_placedCoop, 1, out _));
            Assert.AreEqual(AnimalStatus.Hungry, _session.GetAnimalStatus(_placedCoop, 2, out _));
        }

        [Test]
        public void CollectAll_GathersEveryReadyProduct()
        {
            _session.Inventory.Add(GameSession.ChickenFeedId, 3);
            Assert.AreEqual(3, _session.FeedAllAnimals(_placedCoop, out _));
            Assert.AreEqual(0, _session.CollectAnimalProducts(_placedCoop, out _));

            _clock.AdvanceSeconds(EggSeconds + 1);
            Assert.AreEqual(3, _session.CollectAnimalProducts(_placedCoop, out var message));
            StringAssert.Contains("3 Egg", message);
            Assert.AreEqual(3, _session.Inventory.GetCount("egg"));
        }

        [Test]
        public void Products_CanBeSold()
        {
            _session.Inventory.Add("egg", 4);
            var coinsBefore = _session.Wallet.Coins;

            Assert.IsTrue(_session.TrySell("egg", out var earned, out var quantity, out var message), message);
            Assert.AreEqual(4, quantity);
            Assert.AreEqual(4 * 45, earned);
            Assert.AreEqual(coinsBefore + 180, _session.Wallet.Coins);
        }

        [Test]
        public void Selling_SomeOfAStack_KeepsTheRest()
        {
            _session.Inventory.Add("egg", 5);
            var coinsBefore = _session.Wallet.Coins;

            Assert.IsTrue(_session.TrySell("egg", 2, out var earned, out var quantity, out var message), message);
            Assert.AreEqual(2, quantity);
            Assert.AreEqual(2 * 45, earned);
            Assert.AreEqual(3, _session.Inventory.GetCount("egg"));
            Assert.AreEqual(coinsBefore + 90, _session.Wallet.Coins);
            StringAssert.Contains("Sold 2 Egg", message);
        }

        [Test]
        public void Selling_MoreThanOwned_SellsWhatThereIs()
        {
            _session.Inventory.Add("egg", 3);

            Assert.IsTrue(_session.TrySell("egg", 10, out var earned, out var quantity, out var message), message);
            Assert.AreEqual(3, quantity);
            Assert.AreEqual(3 * 45, earned);
            Assert.AreEqual(0, _session.Inventory.GetCount("egg"));
        }

        [Test]
        public void Selling_NothingOrNone_Fails()
        {
            _session.Inventory.Add("egg", 3);
            var coinsBefore = _session.Wallet.Coins;

            Assert.IsFalse(_session.TrySell("egg", 0, out _, out var quantity, out var message));
            Assert.AreEqual(0, quantity);
            Assert.IsNotEmpty(message);
            Assert.IsFalse(_session.TrySell("milk", 1, out _, out _, out message), "No milk owned.");
            Assert.IsFalse(_session.TrySell("mystery", 1, out _, out _, out message));

            Assert.AreEqual(3, _session.Inventory.GetCount("egg"));
            Assert.AreEqual(coinsBefore, _session.Wallet.Coins);
        }

        [Test]
        public void MovingABuilding_KeepsItsAnimalsAndIsSaved()
        {
            _session.Inventory.Add(GameSession.ChickenFeedId, 1);
            Assert.IsTrue(_session.TryFeedAnimal(_placedCoop, 0, out var message), message);
            var readyAt = _placedCoop.GetAnimalReadyAt(0);

            Assert.IsFalse(_session.CanMoveBuilding(_placedCoop, new Vector2Int(1, 1), out _), "Field plots are in the way.");
            Assert.IsTrue(_session.TryMoveBuilding(_placedCoop, new Vector2Int(8, 8), out message), message);
            Assert.AreEqual(new Vector2Int(8, 8), _placedCoop.Origin);
            Assert.AreEqual(3, _placedCoop.AnimalCount);
            Assert.AreEqual(readyAt, _placedCoop.GetAnimalReadyAt(0), "The fed chicken keeps its timer.");

            var restored = new GameSession(_database, _clock, _saveService, _saveService.TryLoad(out var error));
            Assert.IsNull(error);
            Assert.AreEqual(new Vector2Int(8, 8), restored.Buildings.Placed[0].Origin);
            Assert.AreEqual(3, restored.Buildings.Placed[0].AnimalCount);
            Assert.IsTrue(restored.CanPlaceBuilding("coop", new Vector2Int(5, 5), out var reason), reason);
        }

        [Test]
        public void AnimalTimersAndProducts_SaveAndKeepRunningWhileClosed()
        {
            _session.Inventory.Add(GameSession.ChickenFeedId, 1);
            _session.Inventory.Add("egg", 2);
            Assert.IsTrue(_session.TryFeedAnimal(_placedCoop, 1, out var message), message);

            var loaded = _saveService.TryLoad(out var error);
            Assert.IsNull(error);

            _clock.AdvanceSeconds(EggSeconds + 1);
            var restored = new GameSession(_database, _clock, _saveService, loaded);
            var coop = restored.Buildings.Placed[0];

            Assert.AreEqual(3, coop.AnimalCount);
            Assert.AreEqual(AnimalStatus.Hungry, restored.GetAnimalStatus(coop, 0, out _));
            Assert.AreEqual(AnimalStatus.Ready, restored.GetAnimalStatus(coop, 1, out _), "The egg finished while the game was closed.");
            Assert.AreEqual(2, restored.Inventory.GetCount("egg"), "Products in the inventory are saved.");
        }

        [Test]
        public void DebugAdvanceAll_SpeedsUpAnimals()
        {
            _session.Inventory.Add(GameSession.ChickenFeedId, 1);
            Assert.IsTrue(_session.TryFeedAnimal(_placedCoop, 0, out var message), message);

            _session.DebugAdvanceAll(EggSeconds + 1);
            Assert.AreEqual(AnimalStatus.Ready, _session.GetAnimalStatus(_placedCoop, 0, out _));
        }
    }
}
