using System.Collections.Generic;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Save;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    /// <summary>Orders and visitors asking for juices: only with the fruit's tree, and only a few.</summary>
    public sealed class JuiceRequestTests
    {
        private const int HighLevel = 18;
        private const int Rounds = 150;

        // Standard test products stand in: egg and milk as fruit, butter and fish_pie as their juices,
        // and goat_cheese as an ordinary good made in the same building.
        private const string OwnedFruitJuice = "butter";
        private const string UnownedFruitJuice = "fish_pie";
        private const string OrdinaryGood = "goat_cheese";

        private FakeClock _clock;
        private CropDefinition _wheat;
        private BuildingDefinition _maker;
        private BuildingDefinition _ownedTree;
        private BuildingDefinition _otherTree;
        private ContentDatabase _database;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _wheat = TestContent.CreateCrop();
            _maker = TestContent.CreateBuilding("juice_maker", 0, 2, 2, BuildingCategory.Goods);
            _ownedTree = TestContent.CreateBuilding("egg_tree", 0, 1, 1, BuildingCategory.Fruit, AnimalKind.None, 0, 0, "egg", 7200f);
            _otherTree = TestContent.CreateBuilding("milk_tree", 0, 1, 1, BuildingCategory.Fruit, AnimalKind.None, 0, 0, "milk", 7200f);
            _database = TestContent.CreateDatabase(
                new[] { _wheat },
                new[] { _maker, _ownedTree, _otherTree },
                new[]
                {
                    TestContent.CreateGoodsRecipe(OwnedFruitJuice, "juice_maker", 60f, new ItemAmount("egg", 1), new ItemAmount("wheat", 1)),
                    TestContent.CreateGoodsRecipe(UnownedFruitJuice, "juice_maker", 60f, new ItemAmount("milk", 1), new ItemAmount("wheat", 1)),
                    TestContent.CreateGoodsRecipe(OrdinaryGood, "juice_maker", 60f, new ItemAmount("wheat", 3))
                });
            _database.ConfigureVisitors(new VisitorBalance { sellerChance = 0f });
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            Object.DestroyImmediate(_maker);
            Object.DestroyImmediate(_ownedTree);
            Object.DestroyImmediate(_otherTree);
            Object.DestroyImmediate(_wheat);
        }

        private GameSession NewFarm(int level = 1)
        {
            var session = new GameSession(_database, _clock, null, new SaveDataV1 { coins = 100000, level = level });
            Assert.IsTrue(session.TryPlaceBuilding("juice_maker", new Vector2Int(5, 5), out var message), message);
            Assert.IsTrue(session.TryPlaceBuilding("egg_tree", new Vector2Int(8, 8), out message), message);
            return session;
        }

        /// <summary>The largest amount each item is asked for, over many new orders.</summary>
        private Dictionary<string, int> SampleOrders(GameSession session)
        {
            var seen = new Dictionary<string, int>();
            for (var round = 0; round < Rounds; round++)
            {
                Assert.IsTrue(session.TryDiscardMission(0, out var message), message);
                _clock.AdvanceSeconds(session.Missions.RefillAfterDiscard.TotalSeconds + 1);
                session.UpdateMissions();
                foreach (var requirement in session.Missions.GetSlot(0).Requirements)
                {
                    seen[requirement.ItemId] = Mathf.Max(seen.TryGetValue(requirement.ItemId, out var most) ? most : 0, requirement.Required);
                }
            }

            return seen;
        }

        /// <summary>The largest amount each item is asked for, over many visitors.</summary>
        private Dictionary<string, int> SampleVisitors(GameSession session)
        {
            var seen = new Dictionary<string, int>();
            for (var round = 0; round < Rounds; round++)
            {
                _clock.AdvanceSeconds(_database.Visitors.arrivalMaxSeconds + 1);
                session.UpdateVisitors();
                var visitor = session.Visitors.Visitors[0];
                foreach (var request in visitor.Requests)
                {
                    seen[request.itemId] = Mathf.Max(seen.TryGetValue(request.itemId, out var most) ? most : 0, request.amount);
                }

                Assert.IsTrue(session.TrySendAwayVisitor(visitor.Id, out var message), message);
            }

            return seen;
        }

        [Test]
        public void Orders_AskForJuiceOnlyWithItsTree()
        {
            var seen = SampleOrders(NewFarm());

            Assert.That(seen.ContainsKey(OwnedFruitJuice), "Orders should ask for the juice whose tree is owned.");
            Assert.IsFalse(seen.ContainsKey(UnownedFruitJuice), "Nobody can make that juice without its tree.");
        }

        [Test]
        public void Orders_AskForAJuiceOnceItsTreeIsPlanted()
        {
            var session = NewFarm();
            Assert.IsTrue(session.TryPlaceBuilding("milk_tree", new Vector2Int(10, 10), out var message), message);

            Assert.That(SampleOrders(session).ContainsKey(UnownedFruitJuice));
        }

        [Test]
        public void Orders_AskForOnlyAFewJuices_EvenAtHighLevels()
        {
            var seen = SampleOrders(NewFarm(HighLevel));

            Assert.LessOrEqual(seen[OwnedFruitJuice], ContentDatabase.MaxFruitGoodsPerRequest);
            Assert.Greater(seen[OrdinaryGood], ContentDatabase.MaxFruitGoodsPerRequest, "Other goods still grow with the level.");
        }

        [Test]
        public void Orders_ForJuice_AreNamedAfterItsBuilding()
        {
            var session = NewFarm();
            for (var round = 0; round < Rounds; round++)
            {
                var order = session.Missions.GetSlot(0);
                if (order.Requirements.Count == 1 && order.Requirements[0].ItemId == OwnedFruitJuice)
                {
                    Assert.AreEqual("juice_maker order", session.Missions.GetTitle(order));
                    return;
                }

                session.TryDiscardMission(0, out _);
                _clock.AdvanceSeconds(session.Missions.RefillAfterDiscard.TotalSeconds + 1);
                session.UpdateMissions();
            }

            Assert.Fail("No juice order turned up.");
        }

        [Test]
        public void Orders_PayMoreThanSellingTheJuice()
        {
            var session = NewFarm();
            for (var round = 0; round < Rounds; round++)
            {
                var order = session.Missions.GetSlot(0);
                if (order.Requirements.Count == 1 && order.Requirements[0].ItemId == OwnedFruitJuice)
                {
                    var sellValue = order.Requirements[0].Required * _database.GetProduct(OwnedFruitJuice).SellPrice;
                    Assert.Greater(order.RewardCoins, sellValue);
                    return;
                }

                session.TryDiscardMission(0, out _);
                _clock.AdvanceSeconds(session.Missions.RefillAfterDiscard.TotalSeconds + 1);
                session.UpdateMissions();
            }

            Assert.Fail("No juice order turned up.");
        }

        [Test]
        public void Visitors_AskForJuiceOnlyWithItsTree()
        {
            var seen = SampleVisitors(NewFarm());

            Assert.That(seen.ContainsKey(OwnedFruitJuice), "Visitors should ask for the juice whose tree is owned.");
            Assert.IsFalse(seen.ContainsKey(UnownedFruitJuice), "Nobody can make that juice without its tree.");
        }

        [Test]
        public void Visitors_AskForOnlyAFewJuices_EvenAtHighLevels()
        {
            var seen = SampleVisitors(NewFarm(HighLevel));

            Assert.LessOrEqual(seen[OwnedFruitJuice], ContentDatabase.MaxFruitGoodsPerRequest);
            Assert.Greater(seen[OrdinaryGood], ContentDatabase.MaxFruitGoodsPerRequest, "Other goods still grow with the level.");
        }

        [Test]
        public void ServingAVisitor_TakesTheJuiceAndPays()
        {
            var session = NewFarm();
            for (var round = 0; round < Rounds; round++)
            {
                _clock.AdvanceSeconds(_database.Visitors.arrivalMaxSeconds + 1);
                session.UpdateVisitors();
                var visitor = session.Visitors.Visitors[0];
                var amount = 0;
                foreach (var request in visitor.Requests)
                {
                    if (request.itemId == OwnedFruitJuice)
                    {
                        amount = request.amount;
                    }

                    session.Inventory.Add(request.itemId, request.amount);
                }

                if (amount == 0)
                {
                    session.TrySendAwayVisitor(visitor.Id, out _);
                    continue;
                }

                var before = session.Inventory.GetCount(OwnedFruitJuice);
                var coins = session.Wallet.Coins;
                Assert.IsTrue(session.TryServeVisitor(visitor.Id, out var message), message);
                Assert.AreEqual(before - amount, session.Inventory.GetCount(OwnedFruitJuice));
                Assert.Greater(session.Wallet.Coins, coins);
                return;
            }

            Assert.Fail("No juice visitor turned up.");
        }

        [Test]
        public void RealGame_OrdersAskForTheJuicesThePlayerCanMake()
        {
            // The real content: level 18 with a Juice Maker and only an Apple Tree.
            var content = Resources.Load<ContentDatabase>("Data/ContentDatabase");
            var session = new GameSession(content, _clock, null, new SaveDataV1 { coins = 100000, level = HighLevel });
            var start = content.Layout.StartArea.min;
            Assert.IsTrue(session.TryPlaceBuilding("juice_maker", start + new Vector2Int(8, 8), out var message), message);
            Assert.IsTrue(session.TryPlaceBuilding("apple_tree", start + new Vector2Int(1, 1), out message), message);

            var seen = SampleOrders(session);

            Assert.That(seen.ContainsKey("apple_juice"), "An Apple Tree and a Juice Maker make Apple Juice.");
            foreach (var other in new[] { "lemonade", "peach_smoothie", "orange_juice", "mango_lassi", "coconut_smoothie" })
            {
                Assert.IsFalse(seen.ContainsKey(other), $"{other} needs a tree this farm doesn't have.");
            }
        }
    }
}
