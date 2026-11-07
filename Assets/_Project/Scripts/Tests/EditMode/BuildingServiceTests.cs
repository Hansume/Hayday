using System;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Economy;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class BuildingServiceTests
    {
        private BuildingDefinition _pond;
        private BuildingDefinition _stable;
        private ContentDatabase _database;

        [SetUp]
        public void SetUp()
        {
            _pond = TestContent.CreateBuilding("fish_pond", 100, 2, 2);
            _stable = TestContent.CreateBuilding("cow_stable", 350, 3, 3);
            _database = TestContent.CreateDatabase(
                Array.Empty<CropDefinition>(),
                new[] { _pond, _stable });
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            UnityEngine.Object.DestroyImmediate(_pond);
            UnityEngine.Object.DestroyImmediate(_stable);
        }

        [Test]
        public void TryPlace_SpendsCoinsAndBlocksFootprint()
        {
            var service = new BuildingService(_database);
            var wallet = new Wallet(500);

            Assert.IsTrue(
                service.TryPlace(_pond, new Vector2Int(5, 5), wallet, out var placed, out var reason),
                reason);
            Assert.AreEqual(400, wallet.Coins);
            Assert.AreEqual(1, service.Placed.Count);
            Assert.AreEqual(new Vector2Int(5, 5), placed.Origin);

            Assert.IsFalse(service.CanPlace(_stable, new Vector2Int(6, 6), out var overlapReason));
            Assert.IsNotEmpty(overlapReason);
            Assert.AreEqual(1, service.GetCount("fish_pond"));
        }

        [Test]
        public void TryPlace_OnPlot_FailsWithoutSpending()
        {
            var service = new BuildingService(_database);
            var wallet = new Wallet(500);

            Assert.IsFalse(
                service.TryPlace(_pond, new Vector2Int(0, 0), wallet, out _, out var reason));
            Assert.IsNotEmpty(reason);
            Assert.AreEqual(500, wallet.Coins);
            Assert.AreEqual(0, service.Placed.Count);
        }

        [Test]
        public void TryPlace_OutsideFarm_Fails()
        {
            var service = new BuildingService(_database);
            var wallet = new Wallet(500);

            Assert.IsFalse(
                service.TryPlace(_pond, new Vector2Int(11, 11), wallet, out _, out var reason));
            Assert.IsNotEmpty(reason);
            Assert.AreEqual(500, wallet.Coins);
            Assert.AreEqual(0, service.Placed.Count);
        }

        [Test]
        public void TryPlace_WithInsufficientCoins_Fails()
        {
            var service = new BuildingService(_database);
            var wallet = new Wallet(50);

            Assert.IsFalse(
                service.TryPlace(_pond, new Vector2Int(8, 8), wallet, out _, out var reason));
            Assert.IsNotEmpty(reason);
            Assert.AreEqual(50, wallet.Coins);
            Assert.AreEqual(0, service.Placed.Count);
        }

        [Test]
        public void AddPlotCell_BlocksBuildingPlacement()
        {
            var service = new BuildingService(_database);

            service.AddPlotCell(new Vector2Int(8, 8));

            Assert.IsFalse(service.CanPlace(_pond, new Vector2Int(8, 8), out var reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void TryMove_ToFreeSpot_FreesOldCellsAndBlocksNewOnes()
        {
            var service = new BuildingService(_database);
            Assert.IsTrue(service.TryPlace(_pond, new Vector2Int(5, 5), new Wallet(500), out var pond, out var reason), reason);
            var changed = 0;
            service.Changed += () => changed++;

            Assert.IsTrue(service.TryMove(pond, new Vector2Int(8, 8), out reason), reason);

            Assert.AreEqual(new Vector2Int(8, 8), pond.Origin);
            Assert.AreEqual(1, changed, "Moving should tell the view to redraw.");
            Assert.IsFalse(service.IsOccupied(new Vector2Int(5, 5)));
            Assert.IsFalse(service.IsOccupied(new Vector2Int(6, 6)));
            Assert.IsTrue(service.IsOccupied(new Vector2Int(8, 8)));
            Assert.IsTrue(service.IsOccupied(new Vector2Int(9, 9)));
        }

        [Test]
        public void TryMove_OverlappingItsOwnSpot_IsAllowed()
        {
            var service = new BuildingService(_database);
            Assert.IsTrue(service.TryPlace(_pond, new Vector2Int(5, 5), new Wallet(500), out var pond, out var reason), reason);

            Assert.IsTrue(service.TryMove(pond, new Vector2Int(6, 5), out reason), reason);

            Assert.IsFalse(service.IsOccupied(new Vector2Int(5, 5)));
            Assert.IsTrue(service.IsOccupied(new Vector2Int(7, 6)));
        }

        [Test]
        public void TryMove_OntoAnotherBuildingOrPlot_FailsAndStaysPut()
        {
            var service = new BuildingService(_database);
            var wallet = new Wallet(1000);
            Assert.IsTrue(service.TryPlace(_pond, new Vector2Int(5, 5), wallet, out var pond, out var reason), reason);
            Assert.IsTrue(service.TryPlace(_stable, new Vector2Int(8, 5), wallet, out _, out reason), reason);

            Assert.IsFalse(service.TryMove(pond, new Vector2Int(7, 5), out reason), "Overlaps the stable.");
            Assert.IsNotEmpty(reason);
            Assert.IsFalse(service.TryMove(pond, new Vector2Int(1, 1), out reason), "Overlaps a field plot.");
            Assert.IsFalse(service.TryMove(pond, new Vector2Int(11, 11), out reason), "Off the farm.");

            Assert.AreEqual(new Vector2Int(5, 5), pond.Origin);
            Assert.IsTrue(service.IsOccupied(new Vector2Int(5, 5)), "A failed move keeps the building's cells.");
            Assert.IsTrue(service.IsOccupied(new Vector2Int(6, 6)));
        }

        [Test]
        public void RestorePlacement_RejectsInvalidPlacement()
        {
            var service = new BuildingService(_database);

            Assert.IsFalse(service.RestorePlacement("fish_pond", new Vector2Int(0, 0)));
            Assert.IsFalse(service.RestorePlacement("mystery", new Vector2Int(8, 8)));
            Assert.AreEqual(0, service.Placed.Count);
        }
    }
}
