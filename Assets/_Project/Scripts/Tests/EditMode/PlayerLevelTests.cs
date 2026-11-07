using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Progression;
using FarmGame.Save;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class PlayerLevelTests
    {
        // ------------------------------------------------------------------ Level maths

        [Test]
        public void NewPlayer_StartsAtLevelOneWithNoXp()
        {
            var level = new PlayerLevel();
            Assert.AreEqual(1, level.Level);
            Assert.AreEqual(0, level.Xp);
            Assert.AreEqual(10, level.XpToNextLevel);
        }

        [Test]
        public void XpRequired_GrowsEachLevel()
        {
            Assert.AreEqual(10, PlayerLevel.XpRequiredFor(1));
            Assert.AreEqual(20, PlayerLevel.XpRequiredFor(2));
            Assert.AreEqual(35, PlayerLevel.XpRequiredFor(3));
            Assert.AreEqual(55, PlayerLevel.XpRequiredFor(4));
        }

        [Test]
        public void AddXp_LevelsUpAndKeepsTheRemainder()
        {
            var level = new PlayerLevel();
            var leveledTo = 0;
            level.LeveledUp += l => leveledTo = l;

            Assert.AreEqual(0, level.AddXp(9));
            Assert.AreEqual(1, level.Level);

            Assert.AreEqual(1, level.AddXp(3));
            Assert.AreEqual(2, level.Level);
            Assert.AreEqual(2, level.Xp);
            Assert.AreEqual(2, leveledTo);
        }

        [Test]
        public void AddXp_CanGainSeveralLevelsAtOnce()
        {
            var level = new PlayerLevel();
            var events = 0;
            level.LeveledUp += _ => events++;

            // 10 + 20 + 35 = 65 reaches level 4 exactly.
            Assert.AreEqual(3, level.AddXp(65));
            Assert.AreEqual(4, level.Level);
            Assert.AreEqual(0, level.Xp);
            Assert.AreEqual(3, events, "One LeveledUp event per level gained.");
        }

        [Test]
        public void AddXp_IgnoresZeroAndNegative()
        {
            var level = new PlayerLevel();
            var changed = 0;
            level.Changed += (_, __) => changed++;

            Assert.AreEqual(0, level.AddXp(0));
            Assert.AreEqual(0, level.AddXp(-5));
            Assert.AreEqual(0, level.Xp);
            Assert.AreEqual(0, changed);
        }

        [Test]
        public void Level_StopsAtMax()
        {
            var level = new PlayerLevel(PlayerLevel.MaxLevel);
            level.AddXp(1000000);
            Assert.AreEqual(PlayerLevel.MaxLevel, level.Level);
            Assert.AreEqual(0, level.Xp);
            Assert.AreEqual(0, level.XpToNextLevel);
            Assert.AreEqual(1f, level.Progress);
        }

        [Test]
        public void Constructor_ClampsBadSaveValues()
        {
            var fromOldSave = new PlayerLevel(0, 0);
            Assert.AreEqual(1, fromOldSave.Level, "Saves without a level load as level 1.");

            var overflowing = new PlayerLevel(1, 15);
            Assert.AreEqual(2, overflowing.Level, "Stored XP above the threshold rolls over.");
            Assert.AreEqual(5, overflowing.Xp);
        }

        // ------------------------------------------------------------------ Earning XP in the game

        [Test]
        public void Harvesting_GivesTheCropsXp()
        {
            var crop = TestContent.CreateCrop("corn", 60f, 1, 2, 2, 3);
            var database = TestContent.CreateDatabase(crop);
            var clock = new FakeClock();
            try
            {
                var session = new GameSession(database, clock, null, new SaveDataV1 { coins = 100 });
                Assert.IsTrue(session.TryPlant(0, "corn", out var plantMessage), plantMessage);
                Assert.AreEqual(0, session.Level.Xp, "Planting gives no XP.");

                clock.AdvanceSeconds(61);
                Assert.IsTrue(session.TryHarvest(0, out _, out var xp, out var message), message);
                Assert.AreEqual(3, xp);
                Assert.AreEqual(3, session.Level.Xp);
            }
            finally
            {
                TestContent.DestroyDatabase(database);
                Object.DestroyImmediate(crop);
            }
        }

        [Test]
        public void CollectingAnimalProducts_GivesTheProductsXp()
        {
            var crop = TestContent.CreateCrop();
            var coop = TestContent.CreateBuilding(
                "coop", 0, 2, 2, BuildingCategory.Animals, AnimalKind.Chicken, 3, 0, "egg", 480f);
            var database = TestContent.CreateDatabase(new[] { crop }, new[] { coop });
            var clock = new FakeClock();
            try
            {
                var session = new GameSession(database, clock, null, new SaveDataV1 { coins = 100 });
                Assert.IsTrue(session.TryPlaceBuilding("coop", new Vector2Int(5, 5), out var placeMessage), placeMessage);
                var placed = session.Buildings.Placed[0];
                for (var i = 0; i < 3; i++)
                {
                    Assert.IsTrue(session.TryAddAnimal(placed, out var addMessage), addMessage);
                }

                session.Inventory.Add(GameSession.ChickenFeedId, 3);
                Assert.AreEqual(3, session.FeedAllAnimals(placed, out _));
                clock.AdvanceSeconds(481);

                // One egg (7 XP) on its own, then the other two (14 XP) together.
                Assert.IsTrue(session.TryCollectAnimalProduct(placed, 0, out var single), single);
                StringAssert.Contains("+7 XP", single);
                Assert.AreEqual(2, session.CollectAnimalProducts(placed, out var all));
                StringAssert.Contains("+14 XP", all);

                // 21 XP: level 2 (10) with 11 left over.
                Assert.AreEqual(2, session.Level.Level);
                Assert.AreEqual(11, session.Level.Xp);
            }
            finally
            {
                TestContent.DestroyDatabase(database);
                Object.DestroyImmediate(coop);
                Object.DestroyImmediate(crop);
            }
        }

        [Test]
        public void LevelAndXp_AreSavedAndRestored()
        {
            var crop = TestContent.CreateCrop();
            var database = TestContent.CreateDatabase(crop);
            var clock = new FakeClock();
            try
            {
                var session = new GameSession(database, clock, null, new SaveDataV1 { coins = 100 });
                session.Level.AddXp(37); // level 3 (10 + 20) with 7 XP

                var data = session.BuildSaveData();
                Assert.AreEqual(3, data.level);
                Assert.AreEqual(7, data.xp);

                var restored = new GameSession(database, clock, null, data);
                Assert.AreEqual(3, restored.Level.Level);
                Assert.AreEqual(7, restored.Level.Xp);
            }
            finally
            {
                TestContent.DestroyDatabase(database);
                Object.DestroyImmediate(crop);
            }
        }
    }
}
