using System.Collections.Generic;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Land;
using FarmGame.Save;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class FarmLandTests
    {
        private const int LockedSectionsOnANewFarm = 288;

        // Cell (11, 12) is just left of the start area, in the section (5, 6).
        private static readonly Vector2Int LockedCell = new Vector2Int(11, 12);
        private static readonly Vector2Int LeftOfStart = new Vector2Int(5, 6);

        private FakeClock _clock;
        private ContentDatabase _content;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _content = TestContent.CreateDatabase(
                new[] { TestContent.CreateCrop() },
                new[] { TestContent.CreateBuilding() });

            // The game's map: 36 x 36 in sections of 2, owning the middle 12 x 12.
            var layout = ScriptableObject.CreateInstance<FarmLayout>();
            layout.Configure(36, 36, new[] { new Vector2Int(16, 16) }, new RectInt(12, 12, 12, 12), 2);
            _content.Configure(_content.Crops, layout, 100000, _content.Buildings, _content.FeedRecipes, _content.Products);
        }

        private GameSession NewSession(SaveDataV1 save = null)
        {
            return new GameSession(_content, _clock, null, save);
        }

        private void SetDeedChance(float chance)
        {
            _content.ConfigureLand(new LandBalance { orderDeedChance = chance, visitorDeedChance = chance });
        }

        [Test]
        public void NewFarm_OwnsOnlyTheStartArea()
        {
            var land = NewSession().Land;

            Assert.IsTrue(land.IsCellOwned(new Vector2Int(12, 12)));
            Assert.IsTrue(land.IsCellOwned(new Vector2Int(23, 23)));
            Assert.IsFalse(land.IsCellOwned(LockedCell));
            Assert.IsFalse(land.IsCellOwned(new Vector2Int(24, 23)));
            Assert.IsFalse(land.IsCellOwned(new Vector2Int(-1, 12)));
            Assert.IsFalse(land.IsCellOwned(new Vector2Int(36, 12)));
            Assert.AreEqual(LockedSectionsOnANewFarm, land.LockedSectionCount);
            Assert.AreEqual(0, land.Deeds);
        }

        [Test]
        public void DefaultSection_IsTwoByTwo()
        {
            Assert.AreEqual(2, FarmLayout.DefaultSectionSize);
        }

        [Test]
        public void LockedLand_CantBeBuiltOnOrPlanted()
        {
            var session = NewSession();

            Assert.IsFalse(session.CanPlaceBuilding("fish_pond", LockedCell, out var reason));
            Assert.AreEqual("Land locked", reason);
            Assert.IsFalse(session.TryPlaceBuilding("fish_pond", LockedCell, out _));
            Assert.IsFalse(session.CanPlaceBuilding("fish_pond", new Vector2Int(23, 20), out _), "It would stick out onto locked land.");
            Assert.IsFalse(session.CanPlaceSlot(LockedCell, out reason));
            Assert.AreEqual("Land locked", reason);
            Assert.IsFalse(session.TryPlaceSlot(LockedCell, out _, out _));
            Assert.IsTrue(session.CanPlaceBuilding("fish_pond", new Vector2Int(20, 20), out reason), reason);
        }

        [Test]
        public void LockedLand_BuildingsCantBeMovedOntoIt()
        {
            var session = NewSession();
            Assert.IsTrue(session.TryPlaceBuilding("fish_pond", new Vector2Int(20, 20), out var message), message);

            Assert.IsFalse(session.CanMoveBuilding(session.Buildings.Placed[0], LockedCell, out _));
        }

        [Test]
        public void BuyingLand_NeedsADeed()
        {
            var session = NewSession();

            Assert.IsFalse(session.TryBuyLand(LeftOfStart, out var message));
            Assert.AreEqual("Need 1 Land Deed", message);
            Assert.IsFalse(session.Land.IsOwned(LeftOfStart));
        }

        [Test]
        public void BuyingLand_SpendsADeedAndOpensTwoByTwoCells()
        {
            var session = NewSession();
            session.Land.AddDeed();
            var expanded = 0;
            session.Land.Expanded += () => expanded++;

            Assert.IsTrue(session.TryBuyLand(LeftOfStart, out var message), message);

            Assert.AreEqual(0, session.Land.Deeds);
            Assert.AreEqual(1, expanded);
            Assert.AreEqual(LockedSectionsOnANewFarm - 1, session.Land.LockedSectionCount);

            foreach (var cell in new[] { new Vector2Int(10, 12), new Vector2Int(11, 12), new Vector2Int(10, 13), new Vector2Int(11, 13) })
            {
                Assert.IsTrue(session.Land.IsCellOwned(cell), $"{cell} should be bought.");
            }

            Assert.IsFalse(session.Land.IsCellOwned(new Vector2Int(9, 12)), "Only 2 x 2 cells.");
            Assert.IsFalse(session.Land.IsCellOwned(new Vector2Int(10, 14)), "Only 2 x 2 cells.");
            Assert.IsTrue(session.CanPlaceBuilding("fish_pond", new Vector2Int(10, 12), out var reason), reason);
            Assert.IsTrue(session.CanPlaceSlot(LockedCell, out reason), reason);
        }

        [Test]
        public void BuyingLand_OnlyNextToOwnedLand()
        {
            var session = NewSession();
            session.Land.AddDeed();
            session.Land.AddDeed();

            Assert.IsFalse(session.TryBuyLand(new Vector2Int(4, 6), out var message), "Not touching.");
            Assert.AreEqual("Too far", message);
            Assert.IsFalse(session.TryBuyLand(new Vector2Int(5, 5), out _), "Touching only at a corner.");
            Assert.IsFalse(session.TryBuyLand(new Vector2Int(6, 6), out message), "Already owned.");
            Assert.AreEqual("Already yours", message);
            Assert.AreEqual(2, session.Land.Deeds, "A failed purchase keeps the deed.");

            Assert.IsTrue(session.TryBuyLand(LeftOfStart, out message), message);
            Assert.IsTrue(session.TryBuyLand(new Vector2Int(4, 6), out message), "Now it touches the new land.");
        }

        [Test]
        public void Land_IsSavedAndRestored()
        {
            var session = NewSession();
            session.Land.AddDeed();
            session.Land.AddDeed();
            Assert.IsTrue(session.TryBuyLand(LeftOfStart, out var message), message);
            Assert.IsTrue(session.TryPlaceBuilding("fish_pond", new Vector2Int(10, 12), out message), message);

            var restored = NewSession(session.BuildSaveData());

            Assert.AreEqual(1, restored.Land.Deeds);
            Assert.IsTrue(restored.Land.IsOwned(LeftOfStart));
            Assert.AreEqual(LockedSectionsOnANewFarm - 1, restored.Land.LockedSectionCount);
            Assert.AreEqual(session.Buildings.Placed.Count, restored.Buildings.Placed.Count, "A building on bought land comes back.");
        }

        [Test]
        public void OldSave_MovesOntoTheStartArea()
        {
            // A save from the 12 x 12 map, with no start area: a plot, a crop and a building.
            var save = new SaveDataV1
            {
                version = 16,
                coins = 500,
                slots = new List<SlotSaveDto> { new SlotSaveDto { cellX = 1, cellY = 1 } },
                plots = new List<PlotSaveDto>
                {
                    new PlotSaveDto
                    {
                        hasCell = true,
                        cellX = 1,
                        cellY = 1,
                        cropId = "wheat",
                        plantedAtUtcTicks = _clock.UtcNow.Ticks,
                        growSeconds = 60f
                    }
                },
                buildings = new List<BuildingSaveDto>
                {
                    new BuildingSaveDto { buildingId = "fish_pond", cellX = 8, cellY = 8 }
                }
            };

            var session = NewSession(save);

            Assert.IsTrue(session.Farm.TryGetPlotId(new Vector2Int(13, 13), out var plotId), "The bought plot moved with the farm.");
            Assert.IsFalse(session.Farm.GetSnapshot(plotId).IsEmpty, "Its crop came too.");
            Assert.AreEqual(new Vector2Int(20, 20), session.Buildings.Placed[0].Origin);
        }

        [Test]
        public void SavedFarm_StaysWhereItIsWhenLoadedAgain()
        {
            var session = NewSession();
            Assert.IsTrue(session.TryPlaceBuilding("fish_pond", new Vector2Int(20, 20), out var message), message);

            var again = NewSession(NewSession(session.BuildSaveData()).BuildSaveData());

            Assert.AreEqual(1, again.Buildings.GetCount("fish_pond"));
            Assert.IsFalse(again.CanPlaceBuilding("fish_pond", new Vector2Int(20, 20), out _), "Still on the same cells.");
        }

        [Test]
        public void Deeds_ComeFromOrdersAtTheirChance()
        {
            SetDeedChance(1f);
            var session = NewSession();
            FillFirstOrder(session, out var message);

            Assert.AreEqual(1, session.Land.Deeds);
            StringAssert.Contains("+1 Land Deed", message);
        }

        [Test]
        public void Deeds_NeverComeAtChanceZero()
        {
            SetDeedChance(0f);
            var session = NewSession();
            FillFirstOrder(session, out var message);

            Assert.AreEqual(0, session.Land.Deeds);
            StringAssert.DoesNotContain("Land Deed", message);
        }

        [Test]
        public void Deeds_StopWhenThereAreEnoughForAllTheLand()
        {
            SetDeedChance(1f);
            var session = NewSession();
            for (var i = 0; i < session.Land.SectionsWide * session.Land.SectionsHigh; i++)
            {
                session.Land.AddDeed();
            }

            var held = session.Land.Deeds;
            FillFirstOrder(session, out _);

            Assert.AreEqual(held, session.Land.Deeds);
        }

        [Test]
        public void Deeds_TakeNoStorage()
        {
            var session = NewSession();
            session.Land.AddDeed();

            Assert.AreEqual(0, session.Storage.Used);
        }

        [Test]
        public void Validator_RejectsChancesOutsideZeroToOne()
        {
            var data = new BalanceData { land = new LandBalance { orderDeedChance = 1.5f, visitorDeedChance = -0.1f } };
            var errors = BalanceValidator.Validate(data);

            Assert.That(errors.Exists(e => e.StartsWith("land.orderDeedChance")));
            Assert.That(errors.Exists(e => e.StartsWith("land.visitorDeedChance")));
        }

        private static void FillFirstOrder(GameSession session, out string message)
        {
            var order = session.Missions.GetSlot(0);
            Assert.IsNotNull(order);
            for (var i = 0; i < order.Requirements.Count; i++)
            {
                session.Inventory.Add(order.Requirements[i].ItemId, order.Requirements[i].Required);
            }

            Assert.IsTrue(session.TryDeliverMission(0, out message), message);
        }
    }
}
