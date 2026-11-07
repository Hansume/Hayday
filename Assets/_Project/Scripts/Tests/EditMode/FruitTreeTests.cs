using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Save;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class FruitTreeTests
    {
        private const float GrowSeconds = 7200f;
        private const string FruitId = "egg"; // Any standard test product will do; egg gives XP.
        private const string UnownedFruitId = "milk";

        private FakeClock _clock;
        private CropDefinition _wheat;
        private BuildingDefinition _treeDefinition;
        private BuildingDefinition _unownedTree;
        private ContentDatabase _database;
        private GameSession _session;
        private PlacedBuilding _tree;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _wheat = TestContent.CreateCrop();
            _treeDefinition = ScriptableObject.CreateInstance<BuildingDefinition>();
            _treeDefinition.Configure(
                "apple_tree",
                "Apple Tree",
                string.Empty,
                null,
                null,
                new Vector2Int(2, 2),
                100,
                BuildingCategory.Fruit,
                AnimalKind.None,
                0,
                0,
                FruitId,
                GrowSeconds,
                1,
                3);
            _unownedTree = TestContent.CreateBuilding(
                "milk_tree", 100, 1, 1, BuildingCategory.Fruit, AnimalKind.None, 0, 0, UnownedFruitId, GrowSeconds);
            _database = TestContent.CreateDatabase(new[] { _wheat }, new[] { _treeDefinition, _unownedTree });
            _session = new GameSession(_database, _clock, null, new SaveDataV1 { coins = 1000 });
            Assert.IsTrue(_session.TryPlaceBuilding("apple_tree", new Vector2Int(5, 5), out var message), message);
            _tree = _session.Buildings.Placed[0];
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            Object.DestroyImmediate(_treeDefinition);
            Object.DestroyImmediate(_unownedTree);
            Object.DestroyImmediate(_wheat);
        }

        [Test]
        public void NewTree_HasToGrowItsFruitFirst()
        {
            Assert.IsTrue(_session.IsFruitTree(_tree));
            Assert.IsFalse(_session.IsFruitReady(_tree));
            Assert.AreEqual(GrowSeconds, _session.GetFruitRemainingSeconds(_tree), 0.01d);

            Assert.IsFalse(_session.TryHarvestFruit(_tree, out var message));
            StringAssert.Contains("ready in", message);
            Assert.AreEqual(0, _session.Inventory.GetCount(FruitId));
        }

        [Test]
        public void Harvest_GivesThreeFruitAndXp_ThenRegrows()
        {
            var unitXp = _database.GetProduct(FruitId).CollectXp;
            var levelBefore = _session.Level.Level;
            var xpBefore = _session.Level.Xp;
            _clock.AdvanceSeconds(GrowSeconds);
            Assert.IsTrue(_session.IsFruitReady(_tree));

            Assert.IsTrue(_session.TryHarvestFruit(_tree, out var message), message);

            Assert.AreEqual(3, _session.Inventory.GetCount(FruitId));
            StringAssert.StartsWith("+3 ", message);
            Assert.That(
                _session.Level.Level > levelBefore || _session.Level.Xp == xpBefore + 3 * unitXp,
                "Each fruit gives its XP.");

            Assert.IsFalse(_session.IsFruitReady(_tree), "The tree has to grow new fruit.");
            Assert.AreEqual(GrowSeconds, _session.GetFruitRemainingSeconds(_tree), 0.01d);
            Assert.IsFalse(_session.TryHarvestFruit(_tree, out _), "One harvest per growing time.");
            Assert.AreEqual(3, _session.Inventory.GetCount(FruitId));
        }

        [Test]
        public void Harvest_WithoutRoomForAllThree_ChangesNothing()
        {
            _session.Inventory.Add("wheat", _session.Storage.Capacity - 2);
            _clock.AdvanceSeconds(GrowSeconds);

            Assert.IsFalse(_session.TryHarvestFruit(_tree, out _));

            Assert.AreEqual(0, _session.Inventory.GetCount(FruitId));
            Assert.IsTrue(_session.IsFruitReady(_tree), "The fruit stays on the tree until there is room.");
        }

        [Test]
        public void SaveAndLoad_KeepsTheGrowingTime()
        {
            _clock.AdvanceSeconds(3600d);

            var restored = new GameSession(_database, _clock, null, _session.BuildSaveData());

            Assert.AreEqual(GrowSeconds - 3600d, restored.GetFruitRemainingSeconds(restored.Buildings.Placed[0]), 0.01d);
        }

        [Test]
        public void Load_CapsATimeWrittenWithTheClockAhead()
        {
            var data = _session.BuildSaveData();
            var entry = data.buildings[0];
            entry.fruitReadyAtUtcTicks = _clock.UtcNow.AddDays(30).Ticks;
            data.buildings[0] = entry;

            var restored = new GameSession(_database, _clock, null, data);

            Assert.AreEqual(GrowSeconds, restored.GetFruitRemainingSeconds(restored.Buildings.Placed[0]), 0.01d);
        }

        [Test]
        public void DebugAdvance_GrowsFruit()
        {
            _session.DebugAdvanceAll(GrowSeconds);
            Assert.IsTrue(_session.IsFruitReady(_tree));
        }

        [Test]
        public void Orders_AskForFruitFromOwnedTreesOnly()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (var round = 0; round < 40; round++)
            {
                var order = _session.Missions.GetSlot(0);
                foreach (var requirement in order.Requirements)
                {
                    seen.Add(requirement.ItemId);
                    if (requirement.ItemId == FruitId)
                    {
                        Assert.AreEqual("Fresh produce", _session.Missions.GetTitle(order));
                    }
                }

                Assert.IsTrue(_session.TryDiscardMission(0, out var discard), discard);
                _clock.AdvanceSeconds(_session.Missions.RefillAfterDiscard.TotalSeconds + 1);
                _session.UpdateMissions();
            }

            Assert.That(seen.Contains(FruitId), "Owning a tree should bring orders for its fruit.");
            Assert.IsFalse(seen.Contains(UnownedFruitId), "No tree of this kind is owned.");
        }

        [Test]
        public void Orders_ForFruit_CanBeFilledFromStorage()
        {
            bool AsksForFruit(FarmGame.Missions.Mission mission)
            {
                foreach (var requirement in mission.Requirements)
                {
                    if (requirement.ItemId == FruitId)
                    {
                        return true;
                    }
                }

                return false;
            }

            var order = _session.Missions.GetSlot(0);
            for (var round = 0; round < 40 && !AsksForFruit(order); round++)
            {
                Assert.IsTrue(_session.TryDiscardMission(0, out _));
                _clock.AdvanceSeconds(_session.Missions.RefillAfterDiscard.TotalSeconds + 1);
                _session.UpdateMissions();
                order = _session.Missions.GetSlot(0);
            }

            Assert.That(AsksForFruit(order), "No fruit order turned up.");
            foreach (var requirement in order.Requirements)
            {
                _session.Inventory.Add(requirement.ItemId, requirement.Required);
            }

            var coins = _session.Wallet.Coins;
            Assert.IsTrue(_session.TryDeliverMission(0, out var message), message);

            Assert.AreEqual(coins + order.RewardCoins, _session.Wallet.Coins);
            Assert.AreEqual(0, _session.Inventory.GetCount(FruitId));
        }

        [Test]
        public void Visitors_AskForFruitFromOwnedTrees_AndPayForIt()
        {
            _database.ConfigureVisitors(new VisitorBalance { sellerChance = 0f });
            var session = new GameSession(_database, _clock, null, new SaveDataV1 { coins = 1000 });
            Assert.IsTrue(session.TryPlaceBuilding("apple_tree", new Vector2Int(5, 5), out var placed), placed);

            _clock.AdvanceSeconds(_database.Visitors.arrivalMaxSeconds + 1);
            session.UpdateVisitors();

            Assert.AreEqual(1, session.Visitors.Visitors.Count, "A tree gives visitors something to ask for.");
            var visitor = session.Visitors.Visitors[0];
            Assert.IsFalse(visitor.IsSeller);
            foreach (var request in visitor.Requests)
            {
                Assert.AreEqual(FruitId, request.itemId, "Only this tree is owned.");
                Assert.Greater(request.amount, 0);
                session.Inventory.Add(request.itemId, request.amount);
            }

            var coins = session.Wallet.Coins;
            Assert.IsTrue(session.TryServeVisitor(visitor.Id, out var message), message);

            Assert.AreEqual(coins + visitor.RewardCoins, session.Wallet.Coins);
            Assert.Greater(visitor.RewardCoins, 0);
            Assert.AreEqual(0, session.Inventory.GetCount(FruitId));
        }

        [Test]
        public void Validator_RequiresAFruitTimeAndHarvest()
        {
            var data = new BalanceData
            {
                buildings = new[]
                {
                    new BuildingBalance { id = BalanceValidator.FeedGrinderId, name = "Grinder", category = "Production" },
                    new BuildingBalance { id = "apple_tree", name = "Apple Tree", category = "Fruit", productId = "apple" }
                }
            };

            var errors = BalanceValidator.Validate(data);

            Assert.That(errors.Exists(e => e.Contains("apple_tree") && e.Contains("productionSeconds")));
            Assert.That(errors.Exists(e => e.Contains("apple_tree") && e.Contains("harvestAmount")));
            Assert.That(errors.Exists(e => e.Contains("apple_tree") && e.Contains("is not a product")));
        }
    }
}
