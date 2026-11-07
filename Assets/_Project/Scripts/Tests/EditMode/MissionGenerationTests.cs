using System;
using System.Collections.Generic;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Missions;
using FarmGame.Save;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmGame.Tests
{
    /// <summary>
    /// What the notice board asks for once the player has a full goat chain: potatoes into
    /// Goat Feed, goats into Goat Milk, Goat Milk and strawberries into Goat Yogurt.
    /// </summary>
    public sealed class MissionGenerationTests
    {
        private const float GoatSeconds = 1200f;
        private const float YogurtSeconds = 900f;
        private const float StrawberrySeconds = 2400f;

        private FakeClock _clock;
        private readonly List<Object> _owned = new List<Object>();
        private ContentDatabase _database;
        private GameSession _session;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            var crops = new[]
            {
                Own(TestContent.CreateCrop("wheat", 60f, 1, 2, 2, 1)),
                Own(TestContent.CreateCrop("corn", 300f, 5, 8, 2, 3)),
                Own(TestContent.CreateCrop("carrot", 900f, 10, 18, 2, 6)),
                Own(TestContent.CreateCrop("potato", 1800f, 15, 30, 2, 9)),
                Own(TestContent.CreateCrop("strawberry", StrawberrySeconds, 20, 40, 2, 11))
            };
            var buildings = new[]
            {
                Own(TestContent.CreateBuilding(
                    "goat_stable", 0, 2, 2, BuildingCategory.Animals, AnimalKind.Goat, 3, 0, "goat_milk", GoatSeconds)),
                Own(TestContent.CreateBuilding("dairy", 0, 2, 2, BuildingCategory.Goods)),
                Own(TestContent.CreateBuilding(GameSession.FeedGrinderBuildingId, 0, 2, 2, BuildingCategory.Production)),
                Own(TestContent.CreateBuilding(GameSession.NoticeBoardBuildingId, 0, 1, 1, BuildingCategory.Landmark))
            };
            var recipes = new[]
            {
                TestContent.CreateGoodsRecipe(
                    "goat_yogurt", "dairy", YogurtSeconds, new ItemAmount("goat_milk", 1), new ItemAmount("strawberry", 2)),
                TestContent.CreateGoodsRecipe("goat_cheese", "dairy", 1200f, new ItemAmount("goat_milk", 2))
            };
            _database = TestContent.CreateDatabase(crops, buildings, recipes);

            _session = new GameSession(_database, _clock, null, new SaveDataV1 { coins = 5000 });
            Assert.IsTrue(_session.TryPlaceBuilding("goat_stable", new Vector2Int(7, 0), out var message), message);
            Assert.IsTrue(_session.TryAddAnimal(_session.Buildings.Placed[_session.Buildings.Placed.Count - 1], out message), message);
            Assert.IsTrue(_session.TryPlaceBuilding("dairy", new Vector2Int(7, 7), out message), message);
            Assert.IsTrue(_session.TryPlaceBuilding(GameSession.FeedGrinderBuildingId, new Vector2Int(4, 7), out message), message);

            // Level 3 so notices can carry three requirements.
            _session.Level.AddXp(30);
            Assert.AreEqual(3, _session.Level.Level);
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            foreach (var item in _owned)
            {
                Object.DestroyImmediate(item);
            }

            _owned.Clear();
        }

        private T Own<T>(T item) where T : Object
        {
            _owned.Add(item);
            return item;
        }

        /// <summary>Discards slot 0 over and over, collecting each new notice.</summary>
        private List<Mission> DrawNotices(int count)
        {
            var notices = new List<Mission>();
            for (var i = 0; i < count; i++)
            {
                notices.Add(_session.Missions.GetSlot(0));
                Assert.IsTrue(_session.TryDiscardMission(0, out var message), message);
                _clock.AdvanceSeconds(_session.Missions.RefillAfterDiscard.TotalSeconds + 1);
                _session.UpdateMissions();
            }

            return notices;
        }

        private static bool Has(Mission notice, string itemId)
        {
            foreach (var requirement in notice.Requirements)
            {
                if (requirement.ItemId == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void Board_AsksForProductsAndGoods_NotJustCrops()
        {
            var crops = 0;
            var total = 0;
            var seen = new HashSet<string>();
            foreach (var notice in DrawNotices(80))
            {
                foreach (var requirement in notice.Requirements)
                {
                    seen.Add(requirement.ItemId);
                    total++;
                    if (_database.GetCrop(requirement.ItemId) != null)
                    {
                        crops++;
                    }
                }
            }

            Assert.That(seen.Contains("goat_milk"), "Goat Milk requests are missing.");
            Assert.That(seen.Contains("goat_yogurt") || seen.Contains("goat_cheese"), "Dairy requests are missing.");
            Assert.Less(crops / (double)total, 0.6, "Five crops should not crowd out everything else.");
        }

        [Test]
        public void Board_OnlyAsksForCropsAndProducts_NeverFeed()
        {
            foreach (var notice in DrawNotices(80))
            {
                foreach (var requirement in notice.Requirements)
                {
                    Assert.IsNull(_database.GetFeedRecipe(requirement.ItemId), $"Asked for feed: {requirement.ItemId}.");
                    Assert.That(
                        _database.GetCrop(requirement.ItemId) != null || _database.GetProduct(requirement.ItemId) != null,
                        $"{requirement.ItemId} is not a crop or product.");
                }
            }
        }

        [Test]
        public void Notices_OftenGroupAProductWithItsIngredients()
        {
            var themed = 0;
            foreach (var notice in DrawNotices(80))
            {
                if (Has(notice, "goat_yogurt") && (Has(notice, "strawberry") || Has(notice, "goat_milk")))
                {
                    themed++;
                }
            }

            Assert.Greater(themed, 0, "No notice ever paired a product with what goes into it.");
        }

        [Test]
        public void Rewards_PayMoreThanSellingTheItems()
        {
            foreach (var notice in DrawNotices(80))
            {
                var value = 0;
                foreach (var requirement in notice.Requirements)
                {
                    Assert.IsTrue(_session.TryGetSellPrice(requirement.ItemId, out _, out var price));
                    value += price * requirement.Required;
                }

                Assert.Greater(notice.RewardCoins, value);
            }
        }

        [Test]
        public void Titles_DescribeTheNotice()
        {
            var missions = _session.Missions;
            var order = new Mission(new[] { new MissionRequirement("goat_yogurt", 1) }, 1, 1);
            var animal = new Mission(new[] { new MissionRequirement("goat_milk", 2) }, 1, 1);
            var produce = new Mission(new[] { new MissionRequirement("wheat", 6) }, 1, 1);
            var mixed = new Mission(
                new[]
                {
                    new MissionRequirement("strawberry", 2),
                    new MissionRequirement("goat_yogurt", 1)
                },
                1,
                1);

            Assert.AreEqual("dairy order", missions.GetTitle(order), "Named after the building that makes it.");
            Assert.AreEqual("Goat request", missions.GetTitle(animal));
            Assert.AreEqual("Fresh produce", missions.GetTitle(produce));
            Assert.AreEqual("dairy order", missions.GetTitle(mixed), "The goods order outranks the crop.");
        }

        [Test]
        public void Orders_SurviveSaveAndLoad()
        {
            var data = _session.BuildSaveData();
            var reloaded = new GameSession(_database, _clock, null, data);
            for (var i = 0; i < reloaded.Missions.SlotCount; i++)
            {
                Assert.IsNotNull(reloaded.Missions.GetSlot(i), $"Slot {i} was dropped on load.");
            }
        }
    }
}
