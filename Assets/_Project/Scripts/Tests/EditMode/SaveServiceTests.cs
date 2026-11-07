using System;
using System.IO;
using FarmGame.Save;
using NUnit.Framework;

namespace FarmGame.Tests
{
    public sealed class SaveServiceTests
    {
        private string _directory;
        private SaveService _saveService;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "FarmGameTests",
                Guid.NewGuid().ToString("N"));
            _saveService = new SaveService(_directory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void SaveAndLoad_RoundTripsData()
        {
            var data = new SaveDataV1
            {
                coins = 123,
                savedAtUtcTicks = 456L,
                inventory = { new ItemStackDto { itemId = "wheat", count = 4 } },
                slots =
                {
                    new SlotSaveDto { cellX = 9, cellY = 8 }
                },
                plots =
                {
                    new PlotSaveDto
                    {
                        plotId = 2,
                        cropId = "corn",
                        plantedAtUtcTicks = 1000L,
                        growSeconds = 300f
                    }
                },
                buildings =
                {
                    new BuildingSaveDto
                    {
                        buildingId = "fish_pond",
                        cellX = 8,
                        cellY = 9,
                        animalCount = 3
                    }
                }
            };

            Assert.IsTrue(_saveService.Save(data, out var saveError), saveError);

            var loaded = _saveService.TryLoad(out var loadError);
            Assert.IsNull(loadError);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(123, loaded.coins);
            Assert.AreEqual(456L, loaded.savedAtUtcTicks);
            Assert.AreEqual(1, loaded.inventory.Count);
            Assert.AreEqual("wheat", loaded.inventory[0].itemId);
            Assert.AreEqual(4, loaded.inventory[0].count);
            Assert.AreEqual(1, loaded.slots.Count);
            Assert.AreEqual(9, loaded.slots[0].cellX);
            Assert.AreEqual(8, loaded.slots[0].cellY);
            Assert.AreEqual(1, loaded.plots.Count);
            Assert.AreEqual(2, loaded.plots[0].plotId);
            Assert.AreEqual("corn", loaded.plots[0].cropId);
            Assert.AreEqual(1000L, loaded.plots[0].plantedAtUtcTicks);
            Assert.AreEqual(300f, loaded.plots[0].growSeconds);
            Assert.AreEqual(1, loaded.buildings.Count);
            Assert.AreEqual("fish_pond", loaded.buildings[0].buildingId);
            Assert.AreEqual(8, loaded.buildings[0].cellX);
            Assert.AreEqual(9, loaded.buildings[0].cellY);
            Assert.AreEqual(3, loaded.buildings[0].animalCount);
        }

        [Test]
        public void MissingFile_ReturnsNullWithoutError()
        {
            var loaded = _saveService.TryLoad(out var error);

            Assert.IsNull(loaded);
            Assert.IsNull(error);
        }

        [Test]
        public void CorruptFile_IsQuarantined()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_saveService.SavePath, "{ not valid json");

            var loaded = _saveService.TryLoad(out var error);

            Assert.IsNull(loaded);
            Assert.IsNotEmpty(error);
            Assert.IsFalse(File.Exists(_saveService.SavePath));
            Assert.AreEqual(1, Directory.GetFiles(_directory, "save.json.corrupt-*").Length);
        }

        [Test]
        public void CorruptPrimary_FallsBackToBackup()
        {
            var data = new SaveDataV1 { coins = 10 };
            Assert.IsTrue(_saveService.Save(data, out _));
            data.coins = 20;
            Assert.IsTrue(_saveService.Save(data, out _));

            File.WriteAllText(_saveService.SavePath, "{ not valid json");

            var loaded = _saveService.TryLoad(out var error);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(10, loaded.coins);
            Assert.IsNotEmpty(error);
            Assert.IsFalse(File.Exists(_saveService.SavePath));
            Assert.IsTrue(File.Exists(_saveService.BackupPath));
            Assert.AreEqual(1, Directory.GetFiles(_directory, "save.json.corrupt-*").Length);
        }

        [Test]
        public void MissingPrimary_LoadsBackup()
        {
            var data = new SaveDataV1 { coins = 15 };
            Assert.IsTrue(_saveService.Save(data, out _));
            data.coins = 25;
            Assert.IsTrue(_saveService.Save(data, out _));

            File.Delete(_saveService.SavePath);

            var loaded = _saveService.TryLoad(out var error);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(15, loaded.coins);
            Assert.IsNull(error);
        }

        [Test]
        public void UnsupportedVersion_IsRejected()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_saveService.SavePath, "{\"version\":999,\"coins\":1}");

            var loaded = _saveService.TryLoad(out var error);

            Assert.IsNull(loaded);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void SecondSave_CreatesBackup()
        {
            var data = new SaveDataV1 { coins = 10 };
            Assert.IsTrue(_saveService.Save(data, out _));
            data.coins = 20;
            Assert.IsTrue(_saveService.Save(data, out _));

            Assert.IsTrue(File.Exists(_saveService.SavePath));
            Assert.IsTrue(File.Exists(_saveService.SavePath + ".bak"));
        }
    }
}
