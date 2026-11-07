using FarmGame.Content;
using FarmGame.Economy;
using FarmGame.Farming;
using FarmGame.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class FarmSimulationTests
    {
        private FakeClock _clock;
        private CropDefinition _crop;
        private ContentDatabase _database;
        private FarmSimulation _farm;
        private Wallet _wallet;
        private InventoryService _inventory;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _crop = TestContent.CreateCrop();
            _database = TestContent.CreateDatabase(_crop);
            _farm = new FarmSimulation(_clock, _database, _database.Layout);
            _wallet = new Wallet(100);
            _inventory = new InventoryService();
        }

        [TearDown]
        public void TearDown()
        {
            TestContent.DestroyDatabase(_database);
            Object.DestroyImmediate(_crop);
        }

        [Test]
        public void TryPlant_SpendsCoins_AndStartsGrowing()
        {
            Assert.IsTrue(_farm.TryPlant(0, _crop, _wallet, out var reason), reason);
            Assert.AreEqual(99, _wallet.Coins);

            var snapshot = _farm.GetSnapshot(0);
            Assert.AreEqual(PlotPhase.Growing, snapshot.Phase);
            Assert.AreEqual(0f, snapshot.Progress, 0.001f);
        }

        [Test]
        public void CropBecomesReady_AfterGrowthTime()
        {
            Assert.IsTrue(_farm.TryPlant(0, _crop, _wallet, out _));

            _clock.AdvanceSeconds(30);
            var half = _farm.GetSnapshot(0);
            Assert.AreEqual(PlotPhase.Growing, half.Phase);
            Assert.AreEqual(0.5f, half.Progress, 0.01f);

            _clock.AdvanceSeconds(31);
            var ready = _farm.GetSnapshot(0);
            Assert.AreEqual(PlotPhase.Ready, ready.Phase);
            Assert.AreEqual(1f, ready.Progress, 0.001f);
            Assert.AreEqual(0d, ready.RemainingSeconds, 0.001d);
        }

        [Test]
        public void Harvest_AddsYieldAndClearsPlot()
        {
            Assert.IsTrue(_farm.TryPlant(0, _crop, _wallet, out _));
            _clock.AdvanceSeconds(61);

            Assert.IsTrue(_farm.TryHarvest(0, _inventory, out var outcome, out var reason), reason);
            Assert.AreEqual(2, outcome.Amount);
            Assert.AreEqual(2, _inventory.GetCount(_crop.Id));
            Assert.AreEqual(PlotPhase.Empty, _farm.GetSnapshot(0).Phase);
        }

        [Test]
        public void TryPlant_WithInsufficientCoins_Fails()
        {
            _wallet = new Wallet(0);

            Assert.IsFalse(_farm.TryPlant(0, _crop, _wallet, out var reason));
            Assert.IsNotEmpty(reason);
            Assert.AreEqual(PlotPhase.Empty, _farm.GetSnapshot(0).Phase);
        }

        [Test]
        public void TryPlant_OnOccupiedPlot_Fails()
        {
            Assert.IsTrue(_farm.TryPlant(0, _crop, _wallet, out _));

            Assert.IsFalse(_farm.TryPlant(0, _crop, _wallet, out var reason));
            Assert.IsNotEmpty(reason);
            Assert.AreEqual(99, _wallet.Coins);
        }

        [Test]
        public void TryHarvest_WhileGrowing_Fails()
        {
            Assert.IsTrue(_farm.TryPlant(0, _crop, _wallet, out _));
            _clock.AdvanceSeconds(10);

            Assert.IsFalse(_farm.TryHarvest(0, _inventory, out _, out var reason));
            Assert.IsNotEmpty(reason);
            Assert.AreEqual(0, _inventory.GetCount(_crop.Id));
        }

        [Test]
        public void RestorePlot_WithPastPlanting_IsReady()
        {
            _farm.RestorePlot(0, _crop.Id, _clock.UtcNow.AddSeconds(-120).Ticks, _crop.GrowSeconds);

            Assert.AreEqual(PlotPhase.Ready, _farm.GetSnapshot(0).Phase);
        }

        [Test]
        public void TryAddPlot_AppendsEmptyPlotAndTracksAddedCell()
        {
            Assert.IsTrue(_farm.TryAddPlot(new Vector2Int(9, 9), out var plotId));
            Assert.AreEqual(9, plotId);
            Assert.AreEqual(10, _farm.PlotCount);
            Assert.AreEqual(new Vector2Int(9, 9), _farm.PlotCells[plotId]);
            Assert.AreEqual(1, _farm.AddedPlotCells.Count);
            Assert.IsTrue(_farm.GetSnapshot(plotId).IsEmpty);

            Assert.IsFalse(_farm.TryAddPlot(new Vector2Int(0, 0), out _));
            Assert.IsFalse(_farm.TryAddPlot(new Vector2Int(12, 0), out _));
            Assert.AreEqual(10, _farm.PlotCount);
        }

        [Test]
        public void FormatTime_FormatsMinutesAndSeconds()
        {
            Assert.AreEqual("1:05", FarmSimulation.FormatTime(65d));
            Assert.AreEqual("1:06", FarmSimulation.FormatTime(65.1d));
            Assert.AreEqual("0:00", FarmSimulation.FormatTime(0d));
            Assert.AreEqual("1:00:00", FarmSimulation.FormatTime(3600d));
        }
    }
}
