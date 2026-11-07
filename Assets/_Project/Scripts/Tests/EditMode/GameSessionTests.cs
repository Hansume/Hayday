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
    public sealed class GameSessionTests
    {
        private string _directory;
        private FakeClock _clock;
        private CropDefinition _crop;
        private ContentDatabase _database;
        private SaveService _saveService;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "FarmGameTests",
                Guid.NewGuid().ToString("N"));
            _clock = new FakeClock();
            _crop = TestContent.CreateCrop();
            _database = TestContent.CreateDatabase(_crop);
            _saveService = new SaveService(_directory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            UnityEngine.Object.DestroyImmediate(_crop);

            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void PlantHarvestSell_UpdatesEconomyAndSaves()
        {
            var session = new GameSession(_database, _clock, _saveService, null);

            Assert.IsTrue(session.TryPlant(0, _crop.Id, out var plantMessage), plantMessage);
            Assert.AreEqual(99, session.Wallet.Coins);

            _clock.AdvanceSeconds(61);
            Assert.IsTrue(session.TryHarvest(0, out var outcome, out var harvestMessage), harvestMessage);
            Assert.AreEqual(2, outcome.Amount);
            Assert.AreEqual(2, session.Inventory.GetCount(_crop.Id));

            Assert.IsTrue(session.TrySell(
                _crop.Id,
                out var earned,
                out var quantity,
                out var sellMessage), sellMessage);
            Assert.AreEqual(2, quantity);
            Assert.AreEqual(4, earned);
            Assert.AreEqual(103, session.Wallet.Coins);
            Assert.AreEqual(0, session.Inventory.GetCount(_crop.Id));

            var loaded = _saveService.TryLoad(out var error);
            Assert.IsNull(error);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(103, loaded.coins);
            Assert.AreEqual(0, loaded.inventory.Count);
            Assert.AreEqual(0, loaded.plots.Count);
        }

        [Test]
        public void RestoredSave_CompletesOfflineGrowth()
        {
            var saveData = new SaveDataV1
            {
                coins = 50,
                savedAtUtcTicks = _clock.UtcNow.Ticks,
                inventory = new List<ItemStackDto>
                {
                    new ItemStackDto { itemId = _crop.Id, count = 3 }
                },
                plots = new List<PlotSaveDto>
                {
                    new PlotSaveDto
                    {
                        plotId = 0,
                        cropId = _crop.Id,
                        plantedAtUtcTicks = _clock.UtcNow.AddMinutes(-5).Ticks,
                        growSeconds = _crop.GrowSeconds
                    }
                }
            };

            var session = new GameSession(_database, _clock, _saveService, saveData);

            Assert.AreEqual(50, session.Wallet.Coins);
            Assert.AreEqual(3, session.Inventory.GetCount(_crop.Id));
            Assert.IsTrue(session.Farm.GetSnapshot(0).IsReady);
        }

        [Test]
        public void RestoredSave_IgnoresUnknownCrops()
        {
            var saveData = new SaveDataV1
            {
                coins = 25,
                inventory = new List<ItemStackDto>
                {
                    new ItemStackDto { itemId = "mystery", count = 9 }
                },
                plots = new List<PlotSaveDto>
                {
                    new PlotSaveDto
                    {
                        plotId = 1,
                        cropId = "mystery",
                        plantedAtUtcTicks = _clock.UtcNow.Ticks,
                        growSeconds = 60f
                    }
                }
            };

            var session = new GameSession(_database, _clock, _saveService, saveData);

            Assert.AreEqual(25, session.Wallet.Coins);
            Assert.AreEqual(0, session.Inventory.GetCount("mystery"));
            Assert.IsTrue(session.Farm.GetSnapshot(1).IsEmpty);
        }

        [Test]
        public void SellWithoutInventory_Fails()
        {
            var session = new GameSession(_database, _clock, _saveService, null);

            Assert.IsFalse(session.TrySell(_crop.Id, out _, out _, out var message));
            Assert.IsNotEmpty(message);
            Assert.AreEqual(100, session.Wallet.Coins);
        }

        [Test]
        public void RestoredCrops_FollowTheirCellWhenPlotNumbersShift()
        {
            // The first purchased slot is off the farm, so it can't be restored and the second
            // slot becomes plot 9 instead of plot 10. The crop must stay on its cell.
            var saveData = new SaveDataV1
            {
                coins = 10,
                slots = new List<SlotSaveDto>
                {
                    new SlotSaveDto { cellX = 40, cellY = 40 },
                    new SlotSaveDto { cellX = 10, cellY = 10 }
                },
                plots = new List<PlotSaveDto>
                {
                    new PlotSaveDto
                    {
                        plotId = 10,
                        hasCell = true,
                        cellX = 10,
                        cellY = 10,
                        cropId = _crop.Id,
                        plantedAtUtcTicks = _clock.UtcNow.Ticks,
                        growSeconds = _crop.GrowSeconds
                    }
                }
            };

            var session = new GameSession(_database, _clock, _saveService, saveData);

            Assert.AreEqual(10, session.Farm.PlotCount);
            Assert.AreEqual(new Vector2Int(10, 10), session.Farm.PlotCells[9]);
            Assert.IsFalse(session.Farm.GetSnapshot(9).IsEmpty, "The crop should be on cell (10, 10).");
        }

        [Test]
        public void RestoredCrops_FromOldSavesUsePlotNumbers()
        {
            var saveData = new SaveDataV1
            {
                version = 4,
                coins = 10,
                plots = new List<PlotSaveDto>
                {
                    new PlotSaveDto
                    {
                        plotId = 2,
                        cropId = _crop.Id,
                        plantedAtUtcTicks = _clock.UtcNow.Ticks,
                        growSeconds = _crop.GrowSeconds
                    }
                }
            };

            var session = new GameSession(_database, _clock, _saveService, saveData);

            Assert.IsFalse(session.Farm.GetSnapshot(2).IsEmpty);
        }

        [Test]
        public void DeferredSaves_WriteOnceWhenFlushed()
        {
            var session = new GameSession(_database, _clock, _saveService, null) { DeferSaves = true };

            Assert.IsTrue(session.TryPlant(0, _crop.Id, out var m1), m1);
            Assert.IsTrue(session.TryPlant(1, _crop.Id, out var m2), m2);
            Assert.IsTrue(session.HasUnsavedChanges);
            Assert.IsFalse(File.Exists(_saveService.SavePath), "Nothing should be written before the flush.");

            Assert.IsTrue(session.FlushPendingSave());
            Assert.IsFalse(session.HasUnsavedChanges);
            var loaded = _saveService.TryLoad(out var error);
            Assert.IsNull(error);
            Assert.AreEqual(2, loaded.plots.Count);

            Assert.IsFalse(session.FlushPendingSave(), "A flush with no changes should not write.");
        }

        [Test]
        public void PlaceBuilding_SavesAndRestores()
        {
            var building = TestContent.CreateBuilding("fish_pond", 30, 2, 2);
            var database = TestContent.CreateDatabase(new[] { _crop }, new[] { building });

            try
            {
                var session = new GameSession(database, _clock, _saveService, null);
                Assert.IsTrue(
                    session.TryPlaceBuilding("fish_pond", new Vector2Int(5, 5), out var placeMessage),
                    placeMessage);
                Assert.AreEqual(70, session.Wallet.Coins);
                Assert.AreEqual(1, session.Buildings.Placed.Count);

                var loaded = _saveService.TryLoad(out var error);
                Assert.IsNull(error);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(1, loaded.buildings.Count);
                Assert.AreEqual("fish_pond", loaded.buildings[0].buildingId);
                Assert.AreEqual(5, loaded.buildings[0].cellX);
                Assert.AreEqual(5, loaded.buildings[0].cellY);

                var restored = new GameSession(database, _clock, _saveService, loaded);
                Assert.AreEqual(70, restored.Wallet.Coins);
                Assert.AreEqual(1, restored.Buildings.Placed.Count);
                Assert.AreEqual(new Vector2Int(5, 5), restored.Buildings.Placed[0].Origin);
            }
            finally
            {
                TestContent.DestroyDatabase(database);
                UnityEngine.Object.DestroyImmediate(building);
            }
        }

        [Test]
        public void AddSlot_SpendsCoinsSavesAndRestores()
        {
            var session = new GameSession(_database, _clock, _saveService, null);

            Assert.IsFalse(
                session.TryPlaceSlot(new Vector2Int(0, 0), out _, out var overlapMessage));
            Assert.IsNotEmpty(overlapMessage);
            Assert.AreEqual(100, session.Wallet.Coins);

            Assert.IsTrue(
                session.TryPlaceSlot(new Vector2Int(9, 9), out var plotId, out var placeMessage),
                placeMessage);
            Assert.AreEqual(9, plotId);
            Assert.AreEqual(0, session.Wallet.Coins);
            Assert.AreEqual(10, session.Farm.PlotCount);
            Assert.IsTrue(session.Farm.GetSnapshot(plotId).IsEmpty);

            var loaded = _saveService.TryLoad(out var error);
            Assert.IsNull(error);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.slots.Count);
            Assert.AreEqual(9, loaded.slots[0].cellX);
            Assert.AreEqual(9, loaded.slots[0].cellY);

            var restored = new GameSession(_database, _clock, _saveService, loaded);
            Assert.AreEqual(0, restored.Wallet.Coins);
            Assert.AreEqual(10, restored.Farm.PlotCount);
            Assert.AreEqual(new Vector2Int(9, 9), restored.Farm.PlotCells[plotId]);
        }

        [Test]
        public void AddSlot_WithInsufficientCoins_FailsWithoutSpending()
        {
            var session = new GameSession(
                _database,
                _clock,
                _saveService,
                new SaveDataV1 { coins = 50 });

            Assert.IsFalse(
                session.TryPlaceSlot(new Vector2Int(9, 9), out _, out var message));
            Assert.IsNotEmpty(message);
            Assert.AreEqual(50, session.Wallet.Coins);
            Assert.AreEqual(9, session.Farm.PlotCount);
        }

        // These two check the game's real content assets, not the test copies.
        private static ContentDatabase GameContent()
        {
            var content = Resources.Load<ContentDatabase>("Data/ContentDatabase");
            Assert.IsNotNull(content, "Run Tools > Farm Game > Rebuild Everything to generate content.");
            return content;
        }

        [Test]
        public void FeedRecipes_MatchAnimalsToCrops()
        {
            var content = GameContent();
            var fish = content.GetFeedRecipe(GameSession.FishFeedId);
            var chicken = content.GetFeedRecipe(GameSession.ChickenFeedId);
            var cow = content.GetFeedRecipe(GameSession.CowFeedId);

            Assert.IsNotNull(content.GetCrop(fish.CropId), "Fish feed uses a crop that doesn't exist.");
            Assert.IsNotNull(content.GetCrop(chicken.CropId), "Chicken feed uses a crop that doesn't exist.");
            Assert.IsNotNull(content.GetCrop(cow.CropId), "Cow feed uses a crop that doesn't exist.");
            Assert.IsNotNull(fish.Icon, "Fish feed has no icon.");

            Assert.AreEqual(AnimalKind.Fish, fish.Animal);
            Assert.AreEqual("wheat", fish.CropId);
            Assert.AreEqual(3, fish.CropAmount);
            Assert.AreEqual(AnimalKind.Chicken, chicken.Animal);
            Assert.AreEqual("corn", chicken.CropId);
            Assert.AreEqual(3, chicken.CropAmount);
            Assert.AreEqual(AnimalKind.Cow, cow.Animal);
            Assert.AreEqual("carrot", cow.CropId);
            Assert.AreEqual(3, cow.CropAmount);
        }

        [Test]
        public void FeedRecipes_TakeLongerForBiggerAnimals()
        {
            var content = GameContent();
            Assert.AreEqual(120f, content.GetFeedRecipe(GameSession.FishFeedId).ProductionSeconds);
            Assert.AreEqual(300f, content.GetFeedRecipe(GameSession.ChickenFeedId).ProductionSeconds);
            Assert.AreEqual(600f, content.GetFeedRecipe(GameSession.CowFeedId).ProductionSeconds);
        }

        [Test]
        public void StartFeed_TakesCropsNowAndFinishesAfterProductionTime()
        {
            using (var farm = new GrinderFarm(this))
            {
                var session = farm.Session;
                session.Inventory.Add(_crop.Id, 5);

                Assert.IsTrue(
                    session.TryStartFeed(farm.Grinder, GameSession.FishFeedId, out var message),
                    message);
                Assert.AreEqual(2, session.Inventory.GetCount(_crop.Id), "Crops are taken when the job starts.");
                Assert.AreEqual(0, session.Inventory.GetCount(GameSession.FishFeedId));
                Assert.AreEqual(1, farm.Grinder.Production.Count);

                _clock.AdvanceSeconds(119);
                Assert.AreEqual(0, session.CollectReadyFeed(farm.Grinder, out _));
                Assert.AreEqual(0, session.Inventory.GetCount(GameSession.FishFeedId));

                _clock.AdvanceSeconds(2);
                Assert.AreEqual(1, session.CollectReadyFeed(farm.Grinder, out var collectMessage));
                StringAssert.Contains("1 Fish Feed", collectMessage);
                Assert.AreEqual(1, session.Inventory.GetCount(GameSession.FishFeedId));
                Assert.AreEqual(0, farm.Grinder.Production.Count);
            }
        }

        [Test]
        public void StartFeed_QueuedJobsRunOneAfterAnother()
        {
            var carrot = TestContent.CreateCrop("carrot");
            using (var farm = new GrinderFarm(this, carrot))
            {
                var session = farm.Session;
                session.Inventory.Add(_crop.Id, 3);
                session.Inventory.Add(carrot.Id, 3);

                Assert.IsTrue(session.TryStartFeed(farm.Grinder, GameSession.FishFeedId, out var m1), m1);
                Assert.IsTrue(session.TryStartFeed(farm.Grinder, GameSession.CowFeedId, out var m2), m2);
                StringAssert.Contains("queued", m2);

                // Fish feed: 0 -> 120s. Cow feed waits for it: 120 -> 720s.
                _clock.AdvanceSeconds(121);
                Assert.AreEqual(1, session.CollectReadyFeed(farm.Grinder, out _));
                Assert.AreEqual(1, session.Inventory.GetCount(GameSession.FishFeedId));

                _clock.AdvanceSeconds(598);
                Assert.AreEqual(0, session.CollectReadyFeed(farm.Grinder, out _));

                _clock.AdvanceSeconds(2);
                Assert.AreEqual(1, session.CollectReadyFeed(farm.Grinder, out _));
                Assert.AreEqual(1, session.Inventory.GetCount(GameSession.CowFeedId));
            }

            UnityEngine.Object.DestroyImmediate(carrot);
        }

        [Test]
        public void StartFeed_WhenQueueIsFull_FailsWithoutConsuming()
        {
            using (var farm = new GrinderFarm(this))
            {
                var session = farm.Session;
                session.Inventory.Add(_crop.Id, 12);

                for (var i = 0; i < GameSession.FeedQueueCapacity; i++)
                {
                    Assert.IsTrue(session.TryStartFeed(farm.Grinder, GameSession.FishFeedId, out var m), m);
                }

                Assert.IsFalse(session.TryStartFeed(farm.Grinder, GameSession.FishFeedId, out var fullMessage));
                StringAssert.Contains("full", fullMessage);
                Assert.AreEqual(3, session.Inventory.GetCount(_crop.Id));

                // Finished feed still holds its slot until it is collected.
                _clock.AdvanceSeconds(121);
                Assert.IsFalse(session.TryStartFeed(farm.Grinder, GameSession.FishFeedId, out _));
                Assert.AreEqual(1, session.CollectReadyFeed(farm.Grinder, out _));
                Assert.IsTrue(session.TryStartFeed(farm.Grinder, GameSession.FishFeedId, out var again), again);
            }
        }

        [Test]
        public void StartFeed_WithInsufficientCrops_FailsWithoutConsuming()
        {
            using (var farm = new GrinderFarm(this))
            {
                var session = farm.Session;
                session.Inventory.Add(_crop.Id, 2);

                Assert.IsFalse(session.TryStartFeed(farm.Grinder, GameSession.FishFeedId, out var message));
                StringAssert.Contains("Need 3", message);
                Assert.AreEqual(2, session.Inventory.GetCount(_crop.Id));
                Assert.AreEqual(0, farm.Grinder.Production.Count);
            }
        }

        [Test]
        public void StartFeed_UsesOnlyTheRecipeCrop()
        {
            var corn = TestContent.CreateCrop("corn");
            using (var farm = new GrinderFarm(this, corn))
            {
                var session = farm.Session;
                session.Inventory.Add(_crop.Id, 10);

                Assert.IsFalse(session.TryStartFeed(farm.Grinder, GameSession.ChickenFeedId, out var message));
                StringAssert.Contains("corn", message);
                Assert.AreEqual(10, session.Inventory.GetCount(_crop.Id));

                session.Inventory.Add(corn.Id, 3);
                Assert.IsTrue(
                    session.TryStartFeed(farm.Grinder, GameSession.ChickenFeedId, out var madeMessage),
                    madeMessage);
                Assert.AreEqual(0, session.Inventory.GetCount(corn.Id));
                Assert.AreEqual(10, session.Inventory.GetCount(_crop.Id));
            }

            UnityEngine.Object.DestroyImmediate(corn);
        }

        [Test]
        public void StartFeed_WithUnknownFeedOrNoGrinder_Fails()
        {
            using (var farm = new GrinderFarm(this))
            {
                var session = farm.Session;
                session.Inventory.Add(_crop.Id, 10);

                Assert.IsFalse(session.TryStartFeed(farm.Grinder, "mystery_feed", out var unknown));
                Assert.IsNotEmpty(unknown);
                Assert.IsFalse(session.TryStartFeed(null, GameSession.FishFeedId, out var noGrinder));
                Assert.IsNotEmpty(noGrinder);
                Assert.AreEqual(10, session.Inventory.GetCount(_crop.Id));
            }
        }

        [Test]
        public void FeedProduction_SavesAndKeepsRunningWhileClosed()
        {
            using (var farm = new GrinderFarm(this))
            {
                farm.Session.Inventory.Add(_crop.Id, 6);
                Assert.IsTrue(farm.Session.TryStartFeed(farm.Grinder, GameSession.FishFeedId, out var m1), m1);
                Assert.IsTrue(farm.Session.TryStartFeed(farm.Grinder, GameSession.FishFeedId, out var m2), m2);

                var loaded = _saveService.TryLoad(out var error);
                Assert.IsNull(error);
                Assert.AreEqual(2, loaded.buildings[0].production.Count);

                // Game closed for 4 minutes: both 2-minute jobs finish offline.
                _clock.AdvanceSeconds(241);
                var restored = new GameSession(farm.Database, _clock, _saveService, loaded);
                var grinder = restored.Buildings.Placed[0];
                Assert.AreEqual(2, grinder.Production.Count);
                Assert.AreEqual(2, restored.CollectReadyFeed(grinder, out _));
                Assert.AreEqual(2, restored.Inventory.GetCount(GameSession.FishFeedId));
            }
        }

        [Test]
        public void DebugAdvanceAll_SpeedsUpFeedProduction()
        {
            using (var farm = new GrinderFarm(this))
            {
                farm.Session.Inventory.Add(_crop.Id, 3);
                Assert.IsTrue(farm.Session.TryStartFeed(farm.Grinder, GameSession.FishFeedId, out var m), m);

                farm.Session.DebugAdvanceAll(121);
                Assert.AreEqual(1, farm.Session.CollectReadyFeed(farm.Grinder, out _));
            }
        }

        /// <summary>A session with one Feed Grinder already placed.</summary>
        private sealed class GrinderFarm : IDisposable
        {
            private readonly BuildingDefinition _definition;

            public ContentDatabase Database { get; }
            public GameSession Session { get; }
            public PlacedBuilding Grinder { get; }

            public GrinderFarm(GameSessionTests owner, params CropDefinition[] extraCrops)
            {
                _definition = TestContent.CreateBuilding(
                    GameSession.FeedGrinderBuildingId,
                    0,
                    2,
                    2,
                    BuildingCategory.Production);
                var crops = new List<CropDefinition> { owner._crop };
                crops.AddRange(extraCrops);
                Database = TestContent.CreateDatabase(crops.ToArray(), new[] { _definition });
                Session = new GameSession(Database, owner._clock, owner._saveService, null);
                Assert.IsTrue(
                    Session.TryPlaceBuilding(
                        GameSession.FeedGrinderBuildingId,
                        new Vector2Int(5, 5),
                        out var message),
                    message);
                Grinder = Session.Buildings.Placed[0];
            }

            public void Dispose()
            {
                TestContent.DestroyDatabase(Database);
                UnityEngine.Object.DestroyImmediate(_definition);
            }
        }

        [Test]
        public void AddAnimal_SpendsCoinsFillsCapacityAndPersists()
        {
            var pond = TestContent.CreateBuilding(
                "coop",
                30,
                2,
                2,
                BuildingCategory.Animals,
                AnimalKind.Chicken,
                4,
                25,
                "egg");
            var database = TestContent.CreateDatabase(new[] { _crop }, new[] { pond });

            try
            {
                var session = new GameSession(
                    database,
                    _clock,
                    _saveService,
                    new SaveDataV1 { coins = 500 });
                Assert.IsTrue(
                    session.TryPlaceBuilding("coop", new Vector2Int(5, 5), out var placeMessage),
                    placeMessage);
                var placed = session.Buildings.Placed[0];

                for (var i = 0; i < 4; i++)
                {
                    Assert.IsTrue(
                        session.TryAddAnimal(placed, out var message),
                        message);
                }

                Assert.AreEqual(4, placed.AnimalCount);
                Assert.AreEqual(370, session.Wallet.Coins);
                Assert.IsFalse(session.TryAddAnimal(placed, out var fullMessage));
                Assert.IsNotEmpty(fullMessage);
                Assert.AreEqual(4, placed.AnimalCount);
                Assert.AreEqual(370, session.Wallet.Coins);

                var loaded = _saveService.TryLoad(out var error);
                Assert.IsNull(error);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(1, loaded.buildings.Count);
                Assert.AreEqual(4, loaded.buildings[0].animalCount);

                var restored = new GameSession(database, _clock, _saveService, loaded);
                Assert.AreEqual(4, restored.Buildings.Placed[0].AnimalCount);
            }
            finally
            {
                TestContent.DestroyDatabase(database);
                UnityEngine.Object.DestroyImmediate(pond);
            }
        }

        [Test]
        public void AddAnimal_WithInsufficientCoins_FailsWithoutSpending()
        {
            var pond = TestContent.CreateBuilding(
                "coop",
                0,
                2,
                2,
                BuildingCategory.Animals,
                AnimalKind.Chicken,
                4,
                25,
                "egg");
            var database = TestContent.CreateDatabase(new[] { _crop }, new[] { pond });

            try
            {
                var session = new GameSession(
                    database,
                    _clock,
                    _saveService,
                    new SaveDataV1 { coins = 10 });
                Assert.IsTrue(
                    session.TryPlaceBuilding("coop", new Vector2Int(5, 5), out var placeMessage),
                    placeMessage);
                var placed = session.Buildings.Placed[0];

                Assert.IsFalse(session.TryAddAnimal(placed, out var message));
                StringAssert.Contains("25 coins", message);
                Assert.AreEqual(0, placed.AnimalCount);
                Assert.AreEqual(10, session.Wallet.Coins);
            }
            finally
            {
                TestContent.DestroyDatabase(database);
                UnityEngine.Object.DestroyImmediate(pond);
            }
        }

        [Test]
        public void RestoredAnimalCount_IsClampedToCapacity()
        {
            var cowStable = TestContent.CreateBuilding(
                "cow_stable",
                100,
                3,
                3,
                BuildingCategory.Animals,
                AnimalKind.Cow,
                2,
                100);
            var database = TestContent.CreateDatabase(new[] { _crop }, new[] { cowStable });

            try
            {
                var saveData = new SaveDataV1
                {
                    coins = 100,
                    buildings = new List<BuildingSaveDto>
                    {
                        new BuildingSaveDto
                        {
                            buildingId = "cow_stable",
                            cellX = 5,
                            cellY = 5,
                            animalCount = 99
                        }
                    }
                };

                var session = new GameSession(database, _clock, _saveService, saveData);

                Assert.AreEqual(1, session.Buildings.Placed.Count);
                Assert.AreEqual(2, session.Buildings.Placed[0].AnimalCount);
                Assert.IsFalse(session.Buildings.Placed[0].HasAnimalSpace);
            }
            finally
            {
                TestContent.DestroyDatabase(database);
                UnityEngine.Object.DestroyImmediate(cowStable);
            }
        }

        [Test]
        public void RestoredSave_IgnoresUnknownBuildings()
        {
            var saveData = new SaveDataV1
            {
                coins = 25,
                buildings = new List<BuildingSaveDto>
                {
                    new BuildingSaveDto
                    {
                        buildingId = "mystery",
                        cellX = 8,
                        cellY = 8
                    }
                }
            };

            var session = new GameSession(_database, _clock, _saveService, saveData);

            Assert.AreEqual(25, session.Wallet.Coins);
            Assert.AreEqual(0, session.Buildings.Placed.Count);
        }
    }
}
