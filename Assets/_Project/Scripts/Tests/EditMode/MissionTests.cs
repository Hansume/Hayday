using System.Collections.Generic;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Missions;
using FarmGame.Save;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class MissionTests
    {
        private FakeClock _clock;
        private CropDefinition _wheat;
        private CropDefinition _corn;
        private CropDefinition _carrot;
        private BuildingDefinition _coop;
        private BuildingDefinition _dairy;
        private BuildingDefinition _board;
        private ContentDatabase _database;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _wheat = TestContent.CreateCrop("wheat", 60f, 1, 2, 2, 1);
            _corn = TestContent.CreateCrop("corn", 60f, 5, 8, 2, 3);
            _carrot = TestContent.CreateCrop("carrot", 60f, 10, 18, 2, 6);
            _coop = TestContent.CreateBuilding(
                "coop", 0, 2, 2, BuildingCategory.Animals, AnimalKind.Chicken, 3, 0, "egg", 480f);
            _dairy = TestContent.CreateBuilding("dairy", 0, 2, 2, BuildingCategory.Goods);
            _board = TestContent.CreateBuilding(GameSession.NoticeBoardBuildingId, 0, 1, 1, BuildingCategory.Landmark);
            var recipes = new[]
            {
                TestContent.CreateGoodsRecipe("butter", "dairy", 480f, new ItemAmount("milk", 2))
            };
            _database = TestContent.CreateDatabase(
                new[] { _wheat, _corn, _carrot },
                new[] { _coop, _dairy, _board },
                recipes);
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            Object.DestroyImmediate(_coop);
            Object.DestroyImmediate(_dairy);
            Object.DestroyImmediate(_board);
            Object.DestroyImmediate(_wheat);
            Object.DestroyImmediate(_corn);
            Object.DestroyImmediate(_carrot);
        }

        private GameSession NewSession(SaveDataV1 save = null)
        {
            return new GameSession(_database, _clock, null, save ?? new SaveDataV1 { coins = 1000 });
        }

        // ------------------------------------------------------------------ The board

        [Test]
        public void NewFarm_HasOneNoticeBoard_EvenAfterReloading()
        {
            var session = NewSession();
            Assert.IsNotNull(session.NoticeBoard);
            Assert.AreEqual(GameSession.NoticeBoardCell, session.NoticeBoard.Origin);

            var reloaded = NewSession(session.BuildSaveData());
            Assert.AreEqual(1, reloaded.Buildings.GetCount(GameSession.NoticeBoardBuildingId));
        }

        [Test]
        public void NewFarm_PostsAFullBoardOfNotices()
        {
            var session = NewSession();
            for (var i = 0; i < session.Missions.SlotCount; i++)
            {
                var notice = session.Missions.GetSlot(i);
                Assert.IsNotNull(notice, $"Slot {i} is empty.");
                Assert.That(notice.Requirements.Count, Is.InRange(1, 2), "At level 1 a notice has one or two requirements.");
                Assert.Greater(notice.RewardCoins, 0);
                Assert.Greater(notice.RewardXp, 0);

                var goals = new HashSet<string>();
                foreach (var requirement in notice.Requirements)
                {
                    Assert.IsNotNull(
                        _database.GetCrop(requirement.ItemId),
                        "With no animals or goods buildings, only crops can be asked for.");
                    Assert.IsTrue(goals.Add(requirement.ItemId), "A notice repeats a requirement.");
                }
            }
        }

        [Test]
        public void Notices_AreTheSameForTheSameSave()
        {
            var a = NewSession().Missions;
            var b = NewSession().Missions;
            for (var i = 0; i < a.SlotCount; i++)
            {
                Assert.AreEqual(a.GetSlot(i).Requirements.Count, b.GetSlot(i).Requirements.Count);
                Assert.AreEqual(a.GetSlot(i).Requirements[0].ItemId, b.GetSlot(i).Requirements[0].ItemId);
                Assert.AreEqual(a.GetSlot(i).RewardCoins, b.GetSlot(i).RewardCoins);
            }
        }

        // ------------------------------------------------------------------ Giving items

        [Test]
        public void GivingAnOrder_TakesTheItems_PaysTheReward_AndEmptiesTheSlot()
        {
            var session = NewSession();
            var notice = session.Missions.GetSlot(0);
            Stock(session, notice, extra: 2);

            var coins = session.Wallet.Coins;
            Assert.IsTrue(session.TryDeliverMission(0, out var message), message);

            foreach (var requirement in notice.Requirements)
            {
                Assert.AreEqual(2, session.Inventory.GetCount(requirement.ItemId), "Only what the order asks for is taken.");
            }

            Assert.AreEqual(coins + notice.RewardCoins, session.Wallet.Coins);
            Assert.AreEqual(1, session.Missions.CompletedCount);
            Assert.IsNull(session.Missions.GetSlot(0), "The filled order comes down.");
        }

        [Test]
        public void HavingTheItems_OnlyMakesAnOrderReady_GivingFillsIt()
        {
            var session = NewSession();
            var notice = session.Missions.GetSlot(0);
            Assert.IsFalse(session.CanDeliverMission(notice));
            Assert.IsFalse(session.HasDeliverableMission);

            Stock(session, notice);
            Assert.IsTrue(session.CanDeliverMission(notice));
            Assert.IsTrue(session.HasDeliverableMission);

            Assert.AreSame(notice, session.Missions.GetSlot(0), "An order only comes down when it's given.");
            Assert.AreEqual(0, session.Missions.CompletedCount);
        }

        [Test]
        public void GivingWithoutEnough_TakesNothing_AndSaysWhatIsMissing()
        {
            var session = NewSession();
            var notice = session.Missions.GetSlot(0);
            Stock(session, notice);
            var last = notice.Requirements[notice.Requirements.Count - 1];
            Assert.IsTrue(session.Inventory.TryRemove(last.ItemId, 1));
            var coins = session.Wallet.Coins;

            Assert.IsFalse(session.TryDeliverMission(0, out var message));

            StringAssert.Contains("Need 1", message);
            Assert.AreEqual(coins, session.Wallet.Coins);
            Assert.AreSame(notice, session.Missions.GetSlot(0));
            Assert.AreEqual(last.Required - 1, session.Inventory.GetCount(last.ItemId));
            foreach (var requirement in notice.Requirements)
            {
                if (requirement != last)
                {
                    Assert.AreEqual(requirement.Required, session.Inventory.GetCount(requirement.ItemId), "Nothing was taken.");
                }
            }
        }

        [Test]
        public void FilledSlot_GetsANewOrderAfterThreeMinutes()
        {
            _database.ConfigureNotices(new NoticeBalance());
            var session = NewSession();
            Assert.AreEqual(180d, session.Missions.RefillAfterClaim.TotalSeconds);
            Stock(session, session.Missions.GetSlot(0));
            Assert.IsTrue(session.TryDeliverMission(0, out var message), message);
            Assert.AreEqual(
                _clock.UtcNow.Ticks + System.TimeSpan.FromMinutes(3).Ticks,
                session.Missions.GetNextPostAt(0));

            _clock.AdvanceSeconds(179);
            session.UpdateMissions();
            Assert.IsNull(session.Missions.GetSlot(0), "Too early for a new order.");

            _clock.AdvanceSeconds(2);
            session.UpdateMissions();
            Assert.IsNotNull(session.Missions.GetSlot(0));
        }

        [Test]
        public void TheCountdown_SurvivesSaveAndLoad()
        {
            var session = NewSession();
            Stock(session, session.Missions.GetSlot(0));
            Assert.IsTrue(session.TryDeliverMission(0, out var message), message);
            _clock.AdvanceSeconds(60);

            var restored = NewSession(session.BuildSaveData());
            Assert.IsNull(restored.Missions.GetSlot(0), "Reloading doesn't skip the wait.");
            Assert.AreEqual(session.Missions.GetNextPostAt(0), restored.Missions.GetNextPostAt(0));
        }

        [Test]
        public void UnfilledOrder_NeverExpires_AndCanStillBeGiven()
        {
            var session = NewSession();
            var notice = session.Missions.GetSlot(0);
            Stock(session, notice);

            _clock.AdvanceSeconds(60 * 60 * 24 * 365);
            session.UpdateMissions();
            Assert.AreSame(notice, session.Missions.GetSlot(0), "Time alone never takes an order down.");

            var coins = session.Wallet.Coins;
            Assert.IsTrue(session.TryDeliverMission(0, out var message), message);
            Assert.AreEqual(coins + notice.RewardCoins, session.Wallet.Coins);
        }

        [Test]
        public void NewOrders_NeverAskForLockedItems_UntilTheyUnlock()
        {
            var wheat = TestContent.CreateCrop("wheat", 60f, 1, 2, 2, 1);
            var locked = TestContent.CreateCrop("melon", 60f, 1, 2, 2, 1, 3);
            var database = TestContent.CreateDatabase(new[] { wheat, locked }, new[] { _board }, null);
            try
            {
                var session = new GameSession(database, _clock, null, new SaveDataV1 { coins = 10 });
                Assert.IsFalse(DrawItems(session, 40).Contains("melon"), "Melon is still locked.");

                while (session.Level.Level < 3)
                {
                    session.Level.AddXp(session.Level.XpToNextLevel);
                }

                Assert.IsTrue(DrawItems(session, 40).Contains("melon"), "Melon should come up once unlocked.");
            }
            finally
            {
                TestContent.DestroyDatabase(database);
                Object.DestroyImmediate(wheat);
                Object.DestroyImmediate(locked);
            }
        }

        [Test]
        public void OwnedAnimals_DoNotBringOrders_WhileTheirBuildingIsLocked()
        {
            var session = NewSession();
            Assert.IsTrue(session.TryPlaceBuilding("coop", new Vector2Int(8, 2), out var message), message);
            var coop = session.Buildings.Placed[session.Buildings.Placed.Count - 1];
            Assert.IsTrue(session.TryAddAnimal(coop, out message), message);
            var data = session.BuildSaveData();

            // The same save, where the coop now needs a higher level than the player has.
            var lockedCoop = TestContent.CreateBuilding(
                "coop", 0, 2, 2, BuildingCategory.Animals, AnimalKind.Chicken, 3, 0, "egg", 480f, 5);
            var database = TestContent.CreateDatabase(new[] { _wheat }, new[] { lockedCoop, _board }, null);
            try
            {
                var restored = new GameSession(database, _clock, null, data);
                Assert.IsFalse(DrawItems(restored, 40).Contains("egg"), "Eggs are still locked.");
            }
            finally
            {
                TestContent.DestroyDatabase(database);
                Object.DestroyImmediate(lockedCoop);
            }
        }

        [Test]
        public void SavedOrderForALockedItem_IsReplacedOnLoad()
        {
            var wheat = TestContent.CreateCrop("wheat", 60f, 1, 2, 2, 1);
            var locked = TestContent.CreateCrop("melon", 60f, 1, 2, 2, 1, 3);
            var database = TestContent.CreateDatabase(new[] { wheat, locked }, new[] { _board }, null);
            try
            {
                var data = new GameSession(database, _clock, null, new SaveDataV1 { coins = 10 }).BuildSaveData();
                var slot = data.notices[0];
                slot.requirements = new List<RequirementSaveDto>
                {
                    new RequirementSaveDto { itemId = "melon", required = 2 }
                };
                data.notices[0] = slot;

                var restored = new GameSession(database, _clock, null, data);
                var notice = restored.Missions.GetSlot(0);
                Assert.IsNotNull(notice, "The slot should get a new order straight away.");
                foreach (var requirement in notice.Requirements)
                {
                    Assert.AreEqual("wheat", requirement.ItemId);
                }
            }
            finally
            {
                TestContent.DestroyDatabase(database);
                Object.DestroyImmediate(wheat);
                Object.DestroyImmediate(locked);
            }
        }

        [Test]
        public void FarmWithNothingToAskFor_PostsNoFreeOrders()
        {
            var locked = TestContent.CreateCrop("wheat", 60f, 1, 2, 2, 1, 50);
            var database = TestContent.CreateDatabase(new[] { locked }, new[] { _board }, null);
            try
            {
                var session = new GameSession(database, _clock, null, new SaveDataV1 { coins = 10 });
                Assert.AreEqual(0, session.Missions.ActiveCount);
                Assert.IsFalse(session.HasDeliverableMission);
            }
            finally
            {
                TestContent.DestroyDatabase(database);
                Object.DestroyImmediate(locked);
            }
        }

        [Test]
        public void Discarding_EmptiesTheSlotForAWhile()
        {
            var session = NewSession();
            Assert.IsTrue(session.TryDiscardMission(2, out var message), message);
            Assert.IsNull(session.Missions.GetSlot(2));
            Assert.AreEqual(0, session.Missions.CompletedCount, "Discarding isn't completing.");

            _clock.AdvanceSeconds(session.Missions.RefillAfterDiscard.TotalSeconds - 1);
            session.UpdateMissions();
            Assert.IsNull(session.Missions.GetSlot(2));
            _clock.AdvanceSeconds(2);
            session.UpdateMissions();
            Assert.IsNotNull(session.Missions.GetSlot(2));
        }

        [Test]
        public void NewNotices_OnlyAskForProductsAndGoodsThePlayerCanMake()
        {
            var session = NewSession();
            Assert.IsTrue(session.TryPlaceBuilding("coop", new Vector2Int(8, 2), out var place), place);
            var coop = session.Buildings.Placed[session.Buildings.Placed.Count - 1];
            Assert.IsTrue(session.TryAddAnimal(coop, out var add), add);
            Assert.IsTrue(session.TryPlaceBuilding("dairy", new Vector2Int(8, 8), out place), place);

            var seen = new HashSet<string>();
            for (var round = 0; round < 40; round++)
            {
                foreach (var requirement in session.Missions.GetSlot(0).Requirements)
                {
                    seen.Add(requirement.ItemId);
                }

                Assert.IsTrue(session.TryDiscardMission(0, out var discard), discard);
                _clock.AdvanceSeconds(session.Missions.RefillAfterDiscard.TotalSeconds + 1);
                session.UpdateMissions();
            }

            foreach (var itemId in seen)
            {
                if (_database.GetCrop(itemId) == null)
                {
                    Assert.That(itemId == "egg" || itemId == "butter", $"{itemId} can't be made: only chickens and a Dairy are owned.");
                }
            }

            Assert.That(seen.Contains("egg"), "Owning chickens should bring egg requests.");
            Assert.That(seen.Contains("butter"), "Owning a Dairy should bring butter requests.");
        }

        // ------------------------------------------------------------------ Saving

        [Test]
        public void Board_IsSavedAndRestored()
        {
            var session = NewSession();
            Assert.IsTrue(session.TryDiscardMission(3, out var message), message);

            var restored = NewSession(session.BuildSaveData());
            Assert.AreEqual(session.Missions.GeneratedCount, restored.Missions.GeneratedCount);
            Assert.IsNull(restored.Missions.GetSlot(3), "The discarded slot is still waiting.");
            Assert.AreEqual(session.Missions.GetNextPostAt(3), restored.Missions.GetNextPostAt(3));

            for (var i = 0; i < 3; i++)
            {
                var before = session.Missions.GetSlot(i);
                var after = restored.Missions.GetSlot(i);
                Assert.AreEqual(before.RewardCoins, after.RewardCoins);
                Assert.AreEqual(before.RewardXp, after.RewardXp);
                Assert.AreEqual(before.Requirements.Count, after.Requirements.Count);
                for (var r = 0; r < before.Requirements.Count; r++)
                {
                    Assert.AreEqual(before.Requirements[r].ItemId, after.Requirements[r].ItemId);
                    Assert.AreEqual(before.Requirements[r].Required, after.Requirements[r].Required);
                }
            }
        }

        [Test]
        public void TimeAway_KeepsOrders_AndFillsSlotsThatWaitedLongEnough()
        {
            var session = NewSession();
            var kept = session.Missions.GetSlot(0);
            Assert.IsTrue(session.TryDiscardMission(1, out var message), message);
            var data = session.BuildSaveData();

            _clock.AdvanceSeconds(60 * 60 * 24 * 30);
            var restored = NewSession(data);
            Assert.AreEqual(session.Missions.GetSlot(0).RewardCoins, restored.Missions.GetSlot(0).RewardCoins);
            Assert.AreEqual(kept.Requirements[0].ItemId, restored.Missions.GetSlot(0).Requirements[0].ItemId);
            Assert.AreEqual(restored.Missions.SlotCount, restored.Missions.ActiveCount, "The discarded slot has refilled.");
        }

        [Test]
        public void BoardSize_AndRefillWaits_ComeFromTheBalanceSettings()
        {
            _database.ConfigureNotices(new NoticeBalance
            {
                slots = 2,
                refillAfterClaimSeconds = 10f,
                refillAfterDiscardSeconds = 30f
            });

            var session = NewSession();
            Assert.AreEqual(2, session.Missions.SlotCount);
            Assert.AreEqual(2, session.Missions.ActiveCount);
            Assert.AreEqual(2, session.BuildSaveData().notices.Count);

            Assert.IsTrue(session.TryDiscardMission(1, out var message), message);
            _clock.AdvanceSeconds(29);
            session.UpdateMissions();
            Assert.IsNull(session.Missions.GetSlot(1), "Still waiting for the 30 second refill.");
            _clock.AdvanceSeconds(1);
            session.UpdateMissions();
            Assert.IsNotNull(session.Missions.GetSlot(1));
        }

        [Test]
        public void SaveWithMoreSlotsThanTheBoard_KeepsTheOnesThatFit()
        {
            var data = NewSession().BuildSaveData();
            Assert.AreEqual(4, data.notices.Count);

            _database.ConfigureNotices(new NoticeBalance { slots = 3 });
            var restored = NewSession(data);
            Assert.AreEqual(3, restored.Missions.SlotCount);
            Assert.AreEqual(3, restored.Missions.ActiveCount);
        }

        [Test]
        public void OldSaveWithoutNotices_GetsAFreshBoard()
        {
            var restored = NewSession(new SaveDataV1 { version = 8, coins = 50, notices = null });
            Assert.IsNotNull(restored.NoticeBoard);
            Assert.AreEqual(restored.Missions.SlotCount, restored.Missions.ActiveCount);
        }

        // ------------------------------------------------------------------ Helpers

        /// <summary>Discards slot 0 over and over, collecting everything the new orders ask for.</summary>
        private HashSet<string> DrawItems(GameSession session, int rounds)
        {
            var seen = new HashSet<string>();
            for (var round = 0; round < rounds; round++)
            {
                foreach (var requirement in session.Missions.GetSlot(0).Requirements)
                {
                    seen.Add(requirement.ItemId);
                }

                Assert.IsTrue(session.TryDiscardMission(0, out var message), message);
                _clock.AdvanceSeconds(session.Missions.RefillAfterDiscard.TotalSeconds + 1);
                session.UpdateMissions();
            }

            return seen;
        }

        /// <summary>Puts everything an order asks for into storage, plus a few of each.</summary>
        private static void Stock(GameSession session, Mission notice, int extra = 0)
        {
            foreach (var requirement in notice.Requirements)
            {
                session.Inventory.Add(requirement.ItemId, requirement.Required + extra);
            }
        }
    }
}
