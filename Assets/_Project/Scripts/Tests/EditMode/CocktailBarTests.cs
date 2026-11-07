using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Save;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class CocktailBarTests
    {
        private const double LineSeconds = 5d * 60d * 60d;
        private const double CooldownSeconds = 3d * 60d * 60d;

        // Standard test products stand in: egg and milk as fruit, butter and fish_pie as their juices.
        private const string Juice = "butter";
        private const string OtherJuice = "fish_pie";

        private FakeClock _clock;
        private CropDefinition _wheat;
        private BuildingDefinition _maker;
        private BuildingDefinition _tree;
        private BuildingDefinition _otherTree;
        private ContentDatabase _database;
        private ContentDatabase _extraDatabase;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _wheat = TestContent.CreateCrop();
            _maker = TestContent.CreateBuilding("juice_maker", 0, 2, 2, BuildingCategory.Goods);
            _tree = TestContent.CreateBuilding("egg_tree", 0, 1, 1, BuildingCategory.Fruit, AnimalKind.None, 0, 0, "egg", 7200f);
            _otherTree = TestContent.CreateBuilding("milk_tree", 0, 1, 1, BuildingCategory.Fruit, AnimalKind.None, 0, 0, "milk", 7200f);
            _database = TestContent.CreateDatabase(
                new[] { _wheat },
                new[] { _maker, _tree, _otherTree },
                new[]
                {
                    TestContent.CreateGoodsRecipe(Juice, "juice_maker", 60f, new ItemAmount("egg", 1), new ItemAmount("wheat", 1)),
                    TestContent.CreateGoodsRecipe(OtherJuice, "juice_maker", 60f, new ItemAmount("milk", 1), new ItemAmount("wheat", 1)),
                    TestContent.CreateGoodsRecipe("goat_cheese", "juice_maker", 60f, new ItemAmount("wheat", 3))
                });
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            if (_extraDatabase != null)
            {
                TestContent.DestroyDatabase(_extraDatabase);
            }

            Object.DestroyImmediate(_maker);
            Object.DestroyImmediate(_tree);
            Object.DestroyImmediate(_otherTree);
            Object.DestroyImmediate(_wheat);
        }

        private GameSession NewSession(SaveDataV1 save = null)
        {
            return new GameSession(_database, _clock, null, save ?? new SaveDataV1 { coins = 100000, level = 1 });
        }

        /// <summary>A new session, whose line forms straight away.</summary>
        private GameSession NewOpenBar()
        {
            var session = NewSession();
            Assert.Greater(session.CocktailBar.Line.Count, 0, "Test setup: the line should have formed.");
            return session;
        }

        /// <summary>A level 1 session whose two juices unlock at the given levels.</summary>
        private GameSession NewSessionWithUnlockLevels(int juiceLevel, int otherJuiceLevel)
        {
            var database = TestContent.CreateDatabase(
                new[] { _wheat },
                new[] { _maker, _tree, _otherTree },
                new[]
                {
                    CreateJuiceRecipe(Juice, "egg", juiceLevel),
                    CreateJuiceRecipe(OtherJuice, "milk", otherJuiceLevel)
                });
            _extraDatabase = database;
            return new GameSession(database, _clock, null, new SaveDataV1 { coins = 100000, level = 1 });
        }

        private static GoodsRecipeDefinition CreateJuiceRecipe(string juice, string fruit, int unlockLevel)
        {
            var recipe = ScriptableObject.CreateInstance<GoodsRecipeDefinition>();
            recipe.Configure(juice, "juice_maker", new[] { new ItemAmount(fruit, 1) }, 60f, recipeUnlockLevel: unlockLevel);
            return recipe;
        }

        private void Pass(GameSession session, double seconds)
        {
            _clock.AdvanceSeconds(seconds);
            session.UpdateCocktailBar();
        }

        [Test]
        public void ShippedSettings_AreSevenCustomers_FiveHours_ThenThreeHours()
        {
            var settings = new CocktailBarBalance();

            Assert.AreEqual(7, settings.LineSize);
            Assert.AreEqual(LineSeconds, settings.Line.TotalSeconds, 0.001d);
            Assert.AreEqual(CooldownSeconds, settings.Cooldown.TotalSeconds, 0.001d);
        }

        [Test]
        public void TheLineForms_WithNoJuice_AndNoWayToMakeOne()
        {
            var session = NewSession();

            Assert.AreEqual(0, session.Inventory.GetCount(Juice));
            Assert.AreEqual(0, session.Buildings.GetCount("juice_maker"));
            Assert.AreEqual(session.CocktailBar.Settings.LineSize, session.CocktailBar.Line.Count);
        }

        [Test]
        public void Customers_AskOnlyForJuices_NotOtherGoods()
        {
            var session = NewOpenBar();
            foreach (var customer in session.CocktailBar.Line)
            {
                Assert.That(customer.JuiceId == Juice || customer.JuiceId == OtherJuice, customer.JuiceId);
            }
        }

        [Test]
        public void Lines_MixLooksAndJuices()
        {
            // A line can be all one juice or look by chance (about 1 in 64 with two juices), but
            // seeding each customer separately made 1 line in 5 all one look.
            const int Lines = 40;
            var session = NewOpenBar();
            var mixedLooks = 0;
            var mixedJuices = 0;
            for (var round = 0; round < Lines; round++)
            {
                var looks = new System.Collections.Generic.HashSet<int>();
                var juices = new System.Collections.Generic.HashSet<string>();
                foreach (var customer in session.CocktailBar.Line)
                {
                    looks.Add(customer.Look);
                    juices.Add(customer.JuiceId);
                }

                mixedLooks += looks.Count > 1 ? 1 : 0;
                mixedJuices += juices.Count > 1 ? 1 : 0;
                Pass(session, LineSeconds);
                Pass(session, CooldownSeconds);
            }

            Assert.GreaterOrEqual(mixedLooks, Lines - 4);
            Assert.GreaterOrEqual(mixedJuices, Lines - 4);
        }

        [Test]
        public void Customers_AskOnlyForUnlockedJuices()
        {
            var session = NewSessionWithUnlockLevels(1, 50);
            foreach (var customer in session.CocktailBar.Line)
            {
                Assert.AreEqual(Juice, customer.JuiceId);
            }
        }

        [Test]
        public void BeforeAnyJuiceUnlocks_CustomersAskForTheFirstToUnlock()
        {
            var session = NewSessionWithUnlockLevels(9, 3);

            Assert.AreEqual(session.CocktailBar.Settings.LineSize, session.CocktailBar.Line.Count);
            foreach (var customer in session.CocktailBar.Line)
            {
                Assert.AreEqual(OtherJuice, customer.JuiceId);
            }
        }

        [Test]
        public void Serving_TakesOneJuiceFromTheFrontCustomerOnly()
        {
            var session = NewOpenBar();
            var bar = session.CocktailBar;
            var count = bar.Line.Count;
            var front = bar.Front;
            var next = bar.Line[1];
            session.Inventory.Add(front.JuiceId, 3);
            var coins = session.Wallet.Coins;

            Assert.IsTrue(session.TryServeBarCustomer(out var message), message);

            Assert.AreEqual(2, session.Inventory.GetCount(front.JuiceId), "One juice per customer.");
            Assert.AreEqual(count - 1, bar.Line.Count, "Only one customer at a time.");
            Assert.AreSame(next, bar.Front, "The next one steps up.");
            Assert.AreEqual(coins + front.RewardCoins, session.Wallet.Coins);
            Assert.Greater(front.RewardCoins, _database.GetProduct(front.JuiceId).SellPrice, "Worth more than selling it.");
            Assert.AreEqual(1, bar.ServedCount);
        }

        [Test]
        public void Serving_WithoutTheJuice_ChangesNothing()
        {
            var session = NewOpenBar();
            var bar = session.CocktailBar;
            var count = bar.Line.Count;
            var front = bar.Front;
            var coins = session.Wallet.Coins;

            Assert.IsFalse(session.CanServeBarCustomer);
            Assert.IsFalse(session.TryServeBarCustomer(out var message));
            StringAssert.StartsWith("Need 1", message);
            Assert.AreEqual(count, bar.Line.Count);
            Assert.AreSame(front, bar.Front, "Nobody gets past the front customer.");
            Assert.AreEqual(coins, session.Wallet.Coins);
        }

        [Test]
        public void TheLine_LeavesAfterFifteenMinutes()
        {
            var session = NewOpenBar();

            Pass(session, LineSeconds - 1d);
            Assert.Greater(session.CocktailBar.Line.Count, 0, "Still there just before the time is up.");

            Pass(session, 1d);
            Assert.AreEqual(0, session.CocktailBar.Line.Count);
        }

        [Test]
        public void ANewLine_ComesThreeHoursAfterTheLastOneEnded()
        {
            var session = NewOpenBar();
            Pass(session, LineSeconds);

            Pass(session, CooldownSeconds - 1d);
            Assert.AreEqual(0, session.CocktailBar.Line.Count, "Still a second to wait.");

            Pass(session, 1d);
            Assert.AreEqual(session.CocktailBar.Settings.LineSize, session.CocktailBar.Line.Count);
        }

        [Test]
        public void ClearingTheLineEarly_DoesNotBringTheNextOneSooner()
        {
            var session = NewOpenBar();
            session.Inventory.Add(Juice, 10);
            session.Inventory.Add(OtherJuice, 10);
            while (session.CocktailBar.Front != null)
            {
                Assert.IsTrue(session.TryServeBarCustomer(out var message), message);
            }

            Pass(session, LineSeconds + CooldownSeconds - 1d);
            Assert.AreEqual(0, session.CocktailBar.Line.Count);

            Pass(session, 1d);
            Assert.AreEqual(session.CocktailBar.Settings.LineSize, session.CocktailBar.Line.Count);
        }

        [Test]
        public void TheLine_IsSavedAndRestored()
        {
            var session = NewOpenBar();
            Pass(session, 60d);
            var front = session.CocktailBar.Front;
            var count = session.CocktailBar.Line.Count;
            var ends = session.CocktailBar.LineEndsAtUtcTicks;

            var restored = NewSession(session.BuildSaveData());

            Assert.AreEqual(count, restored.CocktailBar.Line.Count);
            Assert.AreEqual(front.JuiceId, restored.CocktailBar.Front.JuiceId);
            Assert.AreEqual(front.RewardCoins, restored.CocktailBar.Front.RewardCoins);
            Assert.AreEqual(ends, restored.CocktailBar.LineEndsAtUtcTicks);
        }

        [Test]
        public void ALineThatEndedWhileClosed_IsGoneOnReturn()
        {
            var session = NewOpenBar();
            var save = session.BuildSaveData();

            _clock.AdvanceSeconds(LineSeconds + 60d);
            var restored = NewSession(save);

            Assert.AreEqual(0, restored.CocktailBar.Line.Count);
        }

        [Test]
        public void ALineDueWhileClosed_GetsItsFullTimeOnReturn()
        {
            var session = NewOpenBar();
            var save = session.BuildSaveData();

            _clock.AdvanceSeconds((LineSeconds + CooldownSeconds) * 3d);
            var restored = NewSession(save);

            Assert.AreEqual(restored.CocktailBar.Settings.LineSize, restored.CocktailBar.Line.Count);
            Assert.AreEqual(_clock.UtcNow.AddSeconds(LineSeconds).Ticks, restored.CocktailBar.LineEndsAtUtcTicks);
        }

        [Test]
        public void ASaveFromTheFuture_CantHoldTheBarClosedForever()
        {
            var session = NewOpenBar();
            var save = session.BuildSaveData();
            save.barLine.Clear();
            save.barLineEndsAtUtcTicks = 0L;
            save.barNextLineAtUtcTicks = _clock.UtcNow.AddYears(5).Ticks;

            var restored = NewSession(save);

            Assert.LessOrEqual(
                restored.CocktailBar.NextLineAtUtcTicks,
                _clock.UtcNow.AddSeconds(LineSeconds + CooldownSeconds).Ticks);
        }

        [Test]
        public void AnOlderSave_GetsANewLineUnderTheNewRules()
        {
            var save = new SaveDataV1
            {
                version = 18,
                coins = 100000,
                level = 1,
                barLineEndsAtUtcTicks = _clock.UtcNow.AddMinutes(5).Ticks,
                barNextLineAtUtcTicks = _clock.UtcNow.AddHours(3).Ticks
            };
            save.barLine.Add(new BarCustomerDto { name = "Old", juiceId = Juice, rewardCoins = 1, rewardXp = 1 });

            SaveMigrations.Apply(save);
            var restored = NewSession(save);

            Assert.AreEqual(restored.CocktailBar.Settings.LineSize, restored.CocktailBar.Line.Count);
            Assert.AreEqual(_clock.UtcNow.AddSeconds(LineSeconds).Ticks, restored.CocktailBar.LineEndsAtUtcTicks);
        }

        [Test]
        public void Validator_RejectsBadSettings()
        {
            var data = new BalanceData
            {
                cocktailBar = new CocktailBarBalance { lineSize = 8, lineSeconds = 0f, cooldownSeconds = -1f }
            };
            var errors = BalanceValidator.Validate(data);

            Assert.That(errors.Exists(e => e.StartsWith("cocktailBar.lineSize")));
            Assert.That(errors.Exists(e => e.StartsWith("cocktailBar.lineSeconds")));
            Assert.That(errors.Exists(e => e.StartsWith("cocktailBar.cooldownSeconds")));
        }
    }
}
