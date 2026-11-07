using System;
using System.IO;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Save;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class VisitorTests
    {
        private string _directory;
        private FakeClock _clock;
        private CropDefinition _wheat;
        private BuildingDefinition _dairy;
        private BuildingDefinition _smokehouse;
        private ContentDatabase _database;
        private SaveService _saveService;
        private GameSession _session;

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
                TestContent.CreateGoodsRecipe("butter", "dairy", 480f, new ItemAmount("milk", 2)),
                TestContent.CreateGoodsRecipe("fish_pie", "smokehouse", 720f, new ItemAmount("fish_fillet", 2))
            };
            _database = TestContent.CreateDatabase(new[] { _wheat }, new[] { _dairy, _smokehouse }, recipes);
            _saveService = new SaveService(_directory, "save.json");

            // Most tests are about visitors who ask for goods; the seller tests opt in.
            _session = CreateSession(sellerChance: 0f);
        }

        private GameSession CreateSession(float sellerChance, int coins = 1000)
        {
            _database.ConfigureVisitors(new VisitorBalance { sellerChance = sellerChance });
            return new GameSession(_database, _clock, _saveService, new SaveDataV1 { coins = coins });
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

        private void PlaceDairy()
        {
            Assert.IsTrue(_session.TryPlaceBuilding("dairy", new Vector2Int(5, 5), out var message), message);
        }

        private void WaitForVisitor()
        {
            _clock.AdvanceSeconds(_database.Visitors.arrivalMaxSeconds + 1);
            _session.UpdateVisitors();
        }

        private void GiveEverythingAskedFor(Visitors.Visitor visitor)
        {
            for (var i = 0; i < visitor.Requests.Count; i++)
            {
                _session.Inventory.Add(visitor.Requests[i].itemId, visitor.Requests[i].amount);
            }
        }

        [Test]
        public void WithoutAGoodsBuilding_OnlySellersCome()
        {
            // There's nothing to ask for yet, so even with no seller chance the visitor sells.
            WaitForVisitor();
            Assert.AreEqual(1, _session.Visitors.Visitors.Count);
            Assert.IsTrue(_session.Visitors.Visitors[0].IsSeller);

            PlaceDairy();
            WaitForVisitor();
            Assert.AreEqual(2, _session.Visitors.Visitors.Count);
            Assert.IsFalse(_session.Visitors.Visitors[1].IsSeller, "With a goods building, request visitors come.");
        }

        // ------------------------------------------------------------------ Sellers

        private Visitors.Visitor WaitForSeller()
        {
            _session = CreateSession(sellerChance: 1f);
            PlaceDairy();
            WaitForVisitor();
            var seller = _session.Visitors.Visitors[0];
            Assert.IsTrue(seller.IsSeller);
            Assert.Greater(seller.Offers.Count, 0);
            return seller;
        }

        [Test]
        public void Sellers_OfferUnlockedThings_AtAMarkupOverSellValue()
        {
            var seller = WaitForSeller();
            foreach (var offer in seller.Offers)
            {
                var crop = _database.GetCrop(offer.ItemId);
                var product = _database.GetProduct(offer.ItemId);
                Assert.IsTrue(crop != null || product != null, $"Unknown item {offer.ItemId}.");
                var sellValue = (crop != null ? crop.SellPrice : product.SellPrice) * offer.Amount;
                Assert.AreEqual(sellValue, offer.MinPrice, "The floor is what selling the bundle pays.");
                Assert.AreEqual(sellValue * 2, offer.BasePrice, "Before discount, a bundle costs twice its sell value.");
            }

            Assert.AreEqual(0, seller.RewardReputation);
        }

        [Test]
        public void Discount_GrowsWithReputation_UpToTheCap()
        {
            var seller = WaitForSeller();
            var offer = seller.Offers[0];

            Assert.AreEqual(0f, _session.VisitorDiscount);
            Assert.AreEqual(offer.BasePrice, _session.GetOfferPrice(offer));

            _session.Reputation.Add(50);
            Assert.AreEqual(0.1f, _session.VisitorDiscount, 0.0001f, "50 of 200 reputation gives a quarter of the 40%.");
            var quarterPrice = _session.GetOfferPrice(offer);
            Assert.Less(quarterPrice, offer.BasePrice);

            _session.Reputation.Add(1000);
            Assert.AreEqual(0.4f, _session.VisitorDiscount, 0.0001f, "The discount stops at the maximum.");
            var bestPrice = _session.GetOfferPrice(offer);
            Assert.Less(bestPrice, quarterPrice);
            Assert.GreaterOrEqual(bestPrice, offer.MinPrice, "Never cheaper than selling.");
        }

        [Test]
        public void Buying_PaysTheDiscountedPrice_AndAddsTheItems_ButNoReputation()
        {
            var seller = WaitForSeller();
            _session.Reputation.Add(100);
            var offer = seller.Offers[0];
            var price = _session.GetOfferPrice(offer);
            var coins = _session.Wallet.Coins;
            var had = _session.Inventory.GetCount(offer.ItemId);

            Assert.IsTrue(_session.TryBuyFromVisitor(seller.Id, 0, out var message), message);
            Assert.AreEqual(coins - price, _session.Wallet.Coins);
            Assert.AreEqual(had + offer.Amount, _session.Inventory.GetCount(offer.ItemId));
            Assert.AreEqual(100, _session.Reputation.Points, "Buying must not give reputation.");
            Assert.IsTrue(offer.Sold);

            Assert.IsFalse(_session.TryBuyFromVisitor(seller.Id, 0, out _), "A bundle can only be bought once.");
        }

        [Test]
        public void SellerWhoSellsOut_GoesHome()
        {
            var seller = WaitForSeller();
            _session.Wallet.Add(100000);
            for (var i = 0; i < seller.Offers.Count; i++)
            {
                Assert.IsTrue(_session.TryBuyFromVisitor(seller.Id, i, out var message), message);
            }

            Assert.IsNull(_session.Visitors.Find(seller.Id), "A sold-out seller leaves.");
        }

        [Test]
        public void CannotBuy_WithoutTheCoins_OrStorageRoom()
        {
            var seller = WaitForSeller();
            var offer = seller.Offers[0];

            _session.Wallet.TrySpend(_session.Wallet.Coins);
            Assert.IsFalse(_session.TryBuyFromVisitor(seller.Id, 0, out var message));
            StringAssert.Contains("Need", message);
            Assert.AreEqual(0, _session.Inventory.GetCount(offer.ItemId));

            _session.Wallet.Add(100000);
            _session.Inventory.Add("wheat", _session.Storage.Free);
            Assert.IsFalse(_session.TryBuyFromVisitor(seller.Id, 0, out message));
            StringAssert.Contains("storage", message);
            Assert.IsFalse(offer.Sold);
        }

        [Test]
        public void Sellers_SurviveSaveAndLoad_WithWhatTheySold()
        {
            var seller = WaitForSeller();
            _session.Wallet.Add(100000);
            if (seller.Offers.Count > 1)
            {
                Assert.IsTrue(_session.TryBuyFromVisitor(seller.Id, 0, out var message), message);
            }

            var restored = new GameSession(_database, _clock, null, _session.BuildSaveData());
            var copy = restored.Visitors.Find(seller.Id);
            Assert.IsNotNull(copy);
            Assert.IsTrue(copy.IsSeller);
            Assert.AreEqual(seller.Offers.Count, copy.Offers.Count);
            for (var i = 0; i < seller.Offers.Count; i++)
            {
                Assert.AreEqual(seller.Offers[i].ItemId, copy.Offers[i].ItemId);
                Assert.AreEqual(seller.Offers[i].BasePrice, copy.Offers[i].BasePrice);
                Assert.AreEqual(seller.Offers[i].Sold, copy.Offers[i].Sold);
            }
        }

        [Test]
        public void Balance_RejectsPricesThatCouldBeResoldForProfit()
        {
            var data = new BalanceData { visitors = new VisitorBalance { sellerPriceMultiplier = 1.2f, maxDiscount = 0.4f } };
            var errors = BalanceValidator.Validate(data);
            Assert.IsTrue(errors.Exists(e => e.Contains("sellerPriceMultiplier")), string.Join("\n", errors));
        }

        [Test]
        public void Visitors_OnlyAskForGoodsFromOwnedBuildings()
        {
            PlaceDairy();
            for (var i = 0; i < 6; i++)
            {
                WaitForVisitor();
                foreach (var visitor in _session.Visitors.Visitors)
                {
                    foreach (var request in visitor.Requests)
                    {
                        Assert.AreEqual("butter", request.itemId, "Only the Dairy is owned, so only Butter can be asked for.");
                        Assert.Greater(request.amount, 0);
                    }
                }
            }
        }

        [Test]
        public void Visitors_ArriveOneAtATime_UpToTheLimit()
        {
            PlaceDairy();
            // One more wait than there are spots; still well inside how long each visitor stays.
            var max = _session.Visitors.MaxVisitors;
            for (var i = 0; i < max + 1; i++)
            {
                WaitForVisitor();
            }

            Assert.AreEqual(max, _session.Visitors.Visitors.Count);

            // Each stands in their own spot.
            var spots = new System.Collections.Generic.HashSet<int>();
            foreach (var visitor in _session.Visitors.Visitors)
            {
                Assert.IsTrue(spots.Add(visitor.Spot), "Two visitors share a spot.");
            }
        }

        [Test]
        public void ServingAVisitor_TakesTheGoods_AndPaysCoinsXpAndReputation()
        {
            PlaceDairy();
            WaitForVisitor();
            var visitor = _session.Visitors.Visitors[0];
            GiveEverythingAskedFor(visitor);

            var coins = _session.Wallet.Coins;
            var xpBefore = _session.Level.Xp;
            Assert.AreEqual(0, _session.Reputation.Points);

            Assert.IsTrue(_session.TryServeVisitor(visitor.Id, out var message), message);
            Assert.AreEqual(0, _session.Inventory.GetCount("butter"));
            Assert.AreEqual(coins + visitor.RewardCoins, _session.Wallet.Coins);
            Assert.AreEqual(visitor.RewardReputation, _session.Reputation.Points);
            Assert.IsTrue(_session.Level.Level > 1 || _session.Level.Xp > xpBefore, "Serving should give XP.");
            Assert.AreEqual(0, _session.Visitors.Visitors.Count);
            Assert.AreEqual(1, _session.Visitors.ServedCount);
        }

        [Test]
        public void Rewards_PayLessCoinsThanSelling_ButMoreXpThanMaking()
        {
            PlaceDairy();
            WaitForVisitor();
            var visitor = _session.Visitors.Visitors[0];

            var sellValue = 0;
            var makeXp = 0;
            foreach (var request in visitor.Requests)
            {
                var product = _database.GetProduct(request.itemId);
                sellValue += product.SellPrice * request.amount;
                makeXp += product.CollectXp * request.amount;
            }

            Assert.Less(visitor.RewardCoins, sellValue, "Visitors pay less than selling the goods.");
            Assert.Greater(visitor.RewardXp, makeXp, "Visitors give more XP than making the goods.");
            Assert.Greater(visitor.RewardReputation, 0);
        }

        [Test]
        public void CannotServe_WithoutEverythingAskedFor()
        {
            PlaceDairy();
            WaitForVisitor();
            var visitor = _session.Visitors.Visitors[0];
            var coins = _session.Wallet.Coins;

            Assert.IsFalse(_session.CanServeVisitor(visitor, out var missing));
            Assert.AreEqual(visitor.Requests.Count, missing.Count);
            Assert.IsFalse(_session.TryServeVisitor(visitor.Id, out var message));
            StringAssert.Contains("more", message);
            Assert.AreEqual(coins, _session.Wallet.Coins);
            Assert.AreEqual(0, _session.Reputation.Points);
            Assert.AreEqual(1, _session.Visitors.Visitors.Count);
        }

        [Test]
        public void Reputation_OnlyComesFromVisitors()
        {
            PlaceDairy();
            _session.Inventory.Add("butter", 5);
            Assert.IsTrue(_session.TrySell("butter", 5, out _, out _, out var message), message);
            Assert.AreEqual(0, _session.Reputation.Points, "Selling must not give reputation.");
        }

        [Test]
        public void Visitor_LeavesAfterWaitingTooLong()
        {
            PlaceDairy();
            WaitForVisitor();
            Assert.AreEqual(1, _session.Visitors.Visitors.Count);
            var first = _session.Visitors.Visitors[0].Id;

            _clock.AdvanceSeconds(_database.Visitors.staySeconds + 1);
            _session.UpdateVisitors();
            Assert.IsNull(_session.Visitors.Find(first), "The visitor should have left.");
        }

        [Test]
        public void SendingAway_RemovesTheVisitorWithoutReward()
        {
            PlaceDairy();
            WaitForVisitor();
            var visitor = _session.Visitors.Visitors[0];
            var coins = _session.Wallet.Coins;

            Assert.IsTrue(_session.TrySendAwayVisitor(visitor.Id, out var message), message);
            Assert.AreEqual(0, _session.Visitors.Visitors.Count);
            Assert.AreEqual(coins, _session.Wallet.Coins);
            Assert.AreEqual(0, _session.Reputation.Points);
            Assert.AreEqual(0, _session.Visitors.ServedCount);
        }

        [Test]
        public void VisitorsAndReputation_SurviveSaveAndLoad()
        {
            PlaceDairy();
            WaitForVisitor();
            var served = _session.Visitors.Visitors[0];
            GiveEverythingAskedFor(served);
            Assert.IsTrue(_session.TryServeVisitor(served.Id, out var message), message);
            WaitForVisitor();
            var waiting = _session.Visitors.Visitors[0];

            var data = _session.BuildSaveData();
            Assert.AreEqual(SaveDataV1.CurrentVersion, data.version);
            var restored = new GameSession(_database, _clock, null, data);

            Assert.AreEqual(_session.Reputation.Points, restored.Reputation.Points);
            Assert.AreEqual(1, restored.Visitors.ServedCount);
            var copy = restored.Visitors.Find(waiting.Id);
            Assert.IsNotNull(copy, "The waiting visitor should still be there.");
            Assert.AreEqual(waiting.Name, copy.Name);
            Assert.AreEqual(waiting.Spot, copy.Spot);
            Assert.AreEqual(waiting.RewardCoins, copy.RewardCoins);
            Assert.AreEqual(waiting.Requests.Count, copy.Requests.Count);
            Assert.AreEqual(waiting.Requests[0].itemId, copy.Requests[0].itemId);
            Assert.AreEqual(waiting.LeavesAtUtcTicks, copy.LeavesAtUtcTicks);
        }

        [Test]
        public void OldSaves_LoadWithNoReputationOrVisitors()
        {
            var old = new SaveDataV1 { version = 10, coins = 50, visitors = null };
            var session = new GameSession(_database, _clock, null, old);
            Assert.AreEqual(0, session.Reputation.Points);
            Assert.AreEqual(0, session.Visitors.Visitors.Count);
        }

        [Test]
        public void Balance_RejectsBadVisitorSettings()
        {
            var data = new BalanceData { visitors = new VisitorBalance { maxVisitors = 9, arrivalMinSeconds = 100f, arrivalMaxSeconds = 50f } };
            var errors = BalanceValidator.Validate(data);
            Assert.IsTrue(errors.Exists(e => e.Contains("visitors.maxVisitors")), string.Join("\n", errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("visitors.arrivalMaxSeconds")), string.Join("\n", errors));
        }
    }
}
