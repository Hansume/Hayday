using System.Collections.Generic;
using FarmGame.Content;
using FarmGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class UnlockTests
    {
        private FakeClock _clock;
        private CropDefinition _wheat;
        private CropDefinition _carrot;
        private BuildingDefinition _dairy;
        private GoodsRecipeDefinition _butter;
        private ContentDatabase _database;
        private GameSession _session;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _wheat = TestContent.CreateCrop("wheat");
            _carrot = TestContent.CreateCrop("carrot", unlockLevel: 3);
            _dairy = TestContent.CreateBuilding("dairy", cost: 10, category: BuildingCategory.Goods, unlockLevel: 2);
            _butter = ScriptableObject.CreateInstance<GoodsRecipeDefinition>();
            _butter.Configure("butter", "dairy", new[] { new ItemAmount("milk", 2) }, 60f, 1, 3);
            _database = TestContent.CreateDatabase(new[] { _wheat, _carrot }, new[] { _dairy }, new[] { _butter });
            _session = new GameSession(_database, _clock, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            Object.DestroyImmediate(_wheat);
            Object.DestroyImmediate(_carrot);
            Object.DestroyImmediate(_dairy);
        }

        private void ReachLevel(int level)
        {
            while (_session.Level.Level < level)
            {
                _session.Level.AddXp(_session.Level.XpToNextLevel);
            }
        }

        [Test]
        public void LockedCrop_CannotBePlanted_UntilItsLevel()
        {
            Assert.IsTrue(_session.TryPlant(0, "wheat", out var message), message);
            Assert.IsFalse(_session.TryPlant(1, "carrot", out message));
            StringAssert.Contains("level 3", message);

            ReachLevel(3);
            Assert.IsTrue(_session.TryPlant(1, "carrot", out message), message);
        }

        [Test]
        public void LockedBuilding_CannotBeBought_UntilItsLevel()
        {
            var coins = _session.Wallet.Coins;
            Assert.IsFalse(_session.CanPlaceBuilding("dairy", new Vector2Int(5, 5), out _));
            Assert.IsFalse(_session.TryPlaceBuilding("dairy", new Vector2Int(5, 5), out var message));
            StringAssert.Contains("level 2", message);
            Assert.AreEqual(coins, _session.Wallet.Coins, "A refused purchase must not cost anything.");

            ReachLevel(2);
            Assert.IsTrue(_session.TryPlaceBuilding("dairy", new Vector2Int(5, 5), out message), message);
        }

        [Test]
        public void LockedRecipe_CannotBeStarted_AndKeepsItsIngredients()
        {
            ReachLevel(2);
            Assert.IsTrue(_session.TryPlaceBuilding("dairy", new Vector2Int(5, 5), out var message), message);
            var dairy = _session.Buildings.Placed[_session.Buildings.Placed.Count - 1];
            _session.Inventory.Add("milk", 2);

            Assert.IsFalse(_session.TryStartGoods(dairy, "butter", out message));
            StringAssert.Contains("level 3", message);
            Assert.AreEqual(2, _session.Inventory.GetCount("milk"));

            ReachLevel(3);
            Assert.IsTrue(_session.TryStartGoods(dairy, "butter", out message), message);
        }

        [Test]
        public void Notices_NeverAskForLockedCrops()
        {
            for (var round = 0; round < 30; round++)
            {
                for (var slot = 0; slot < _session.Missions.SlotCount; slot++)
                {
                    var mission = _session.Missions.GetSlot(slot);
                    if (mission == null)
                    {
                        continue;
                    }

                    foreach (var requirement in mission.Requirements)
                    {
                        Assert.AreNotEqual("carrot", requirement.ItemId, "A level 1 farm was asked for a locked crop.");
                    }

                    _session.Missions.TryDiscard(slot);
                }

                _clock.AdvanceSeconds(_session.Missions.RefillAfterDiscard.TotalSeconds + 1);
                _session.UpdateMissions();
            }
        }

        [Test]
        public void GetUnlocksAt_ListsWhatOpensAtThatLevel()
        {
            CollectionAssert.AreEqual(new[] { "dairy" }, _database.GetUnlocksAt(2));
            CollectionAssert.AreEqual(new[] { "carrot", "Butter" }, _database.GetUnlocksAt(3));
            CollectionAssert.IsEmpty(_database.GetUnlocksAt(4));
        }

        [Test]
        public void GetUnlockDetailsAt_DescribesEachUnlockForTheLevelUpWindow()
        {
            var level2 = _database.GetUnlockDetailsAt(2);
            Assert.AreEqual(1, level2.Count);
            Assert.AreEqual(UnlockKind.Building, level2[0].Kind);
            Assert.AreEqual("dairy", level2[0].Id);
            Assert.AreEqual(BuildingCategory.Goods, level2[0].Category);
            Assert.AreEqual("New goods building", level2[0].Description);

            var level3 = _database.GetUnlockDetailsAt(3);
            Assert.AreEqual(2, level3.Count);
            Assert.AreEqual(UnlockKind.Crop, level3[0].Kind);
            Assert.AreEqual("carrot", level3[0].Id);
            Assert.AreEqual("New crop", level3[0].Description);
            Assert.AreEqual(UnlockKind.Recipe, level3[1].Kind);
            Assert.AreEqual("butter", level3[1].Id);
            Assert.AreEqual("New recipe: dairy", level3[1].Description);
        }

        [Test]
        public void GetNextUnlockLevel_FindsTheNextLevelWithSomethingNew()
        {
            Assert.AreEqual(2, _database.GetNextUnlockLevel(1));
            Assert.AreEqual(3, _database.GetNextUnlockLevel(2));
            Assert.AreEqual(0, _database.GetNextUnlockLevel(3), "Nothing is left after level 3.");
        }

        // ------------------------------------------------------------------ Balance checks

        private static BalanceData ValidData()
        {
            return new BalanceData
            {
                crops = new[]
                {
                    new CropBalance { id = "wheat", name = "Wheat", growSeconds = 60, yield = 2, unlockLevel = 1 },
                    new CropBalance { id = "carrot", name = "Carrot", growSeconds = 60, yield = 2, unlockLevel = 3 }
                },
                products = new[]
                {
                    new ProductBalance { id = "milk", name = "Milk" },
                    new ProductBalance { id = "butter", name = "Butter" }
                },
                feeds = new[]
                {
                    new FeedBalance { id = "cow_feed", name = "Cow Feed", animal = "Cow", cropId = "carrot", cropAmount = 3, productionSeconds = 60 }
                },
                buildings = new[]
                {
                    new BuildingBalance { id = "feed_grinder", name = "Grinder", category = "Production", unlockLevel = 1 },
                    new BuildingBalance
                    {
                        id = "cow_stable", name = "Cow Stable", category = "Animals", animal = "Cow", animalCapacity = 2,
                        productId = "milk", productionSeconds = 60, unlockLevel = 4
                    },
                    new BuildingBalance { id = "dairy", name = "Dairy", category = "Goods", unlockLevel = 5 }
                },
                goodsRecipes = new[]
                {
                    new GoodsRecipeBalance
                    {
                        outputId = "butter", buildingId = "dairy", productionSeconds = 60, unlockLevel = 5,
                        inputs = new[] { new ItemAmount("milk", 2) }
                    }
                }
            };
        }

        private static void AssertHasError(BalanceData data, string fragment)
        {
            var errors = BalanceValidator.Validate(data);
            Assert.IsTrue(
                errors.Exists(e => e.Contains(fragment)),
                $"Expected an error containing \"{fragment}\", got:\n" + string.Join("\n", errors));
        }

        [Test]
        public void Validator_AcceptsAConsistentUnlockOrder()
        {
            var errors = BalanceValidator.Validate(ValidData());
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Validator_RejectsAnimalsBeforeTheirFeedCrop()
        {
            var data = ValidData();
            data.buildings[1].unlockLevel = 2;
            data.buildings[2].unlockLevel = 2;
            data.goodsRecipes[0].unlockLevel = 2;
            AssertHasError(data, "feed crop \"carrot\"");
        }

        [Test]
        public void Validator_RejectsRecipeBeforeItsBuilding()
        {
            var data = ValidData();
            data.goodsRecipes[0].unlockLevel = 4;
            AssertHasError(data, "before its building \"dairy\"");
        }

        [Test]
        public void Validator_RejectsRecipeBeforeItsIngredients()
        {
            var data = ValidData();
            data.buildings[1].unlockLevel = 6;
            AssertHasError(data, "before its input \"milk\"");
        }

        [Test]
        public void Validator_RejectsOutOfRangeLevels()
        {
            var data = ValidData();
            data.crops[0].unlockLevel = 0;
            AssertHasError(data, "crops[wheat].unlockLevel must be between 1");
        }
    }
}
