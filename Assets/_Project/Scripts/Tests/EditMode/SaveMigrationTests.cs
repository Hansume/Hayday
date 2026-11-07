using System.Collections.Generic;
using System.IO;
using FarmGame.Save;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class SaveMigrationTests
    {
        private static SaveDataV1 OldSave()
        {
            return new SaveDataV1
            {
                version = 13,
                inventory = new List<ItemStackDto>
                {
                    new ItemStackDto { itemId = "cabbage", count = 3 },
                    new ItemStackDto { itemId = "sunflower", count = 2 },
                    new ItemStackDto { itemId = "wheat", count = 5 }
                },
                plots = new List<PlotSaveDto> { new PlotSaveDto { plotId = 0, cropId = "sunflower" } },
                buildings = new List<BuildingSaveDto>
                {
                    new BuildingSaveDto
                    {
                        buildingId = "creamery",
                        production = new List<ProductionJobDto> { new ProductionJobDto { itemId = "butter" } }
                    },
                    new BuildingSaveDto { buildingId = "goat_dairy" },
                    new BuildingSaveDto { buildingId = "country_kitchen" },
                    new BuildingSaveDto { buildingId = "coop" }
                },
                notices = new List<NoticeSlotSaveDto>
                {
                    new NoticeSlotSaveDto
                    {
                        hasNotice = true,
                        requirements = new List<RequirementSaveDto> { new RequirementSaveDto { itemId = "cabbage", required = 4 } }
                    }
                },
                visitors = new List<VisitorSaveDto>
                {
                    new VisitorSaveDto
                    {
                        requests = new List<ItemStackDto> { new ItemStackDto { itemId = "sunflower", count = 1 } },
                        offers = new List<VisitorOfferDto> { new VisitorOfferDto { itemId = "cabbage", amount = 2 } }
                    }
                }
            };
        }

        [Test]
        public void Version13Save_RenamesRemovedCropsAndBuildings()
        {
            var data = OldSave();
            SaveMigrations.Apply(data);

            Assert.AreEqual(SaveDataV1.CurrentVersion, data.version);
            CollectionAssert.AreEqual(new[] { "soybean", "chili", "wheat" }, data.inventory.ConvertAll(s => s.itemId));
            Assert.AreEqual("chili", data.plots[0].cropId);
            CollectionAssert.AreEqual(
                new[] { "dairy", "dairy", "bakery", "coop" },
                data.buildings.ConvertAll(b => b.buildingId),
                "Creamery and Goat Dairy both become a Dairy; other buildings are untouched.");
            Assert.AreEqual("butter", data.buildings[0].production[0].itemId, "Recipe ids didn't change.");
            Assert.AreEqual("chili", data.visitors[0].requests[0].itemId);
            Assert.AreEqual("soybean", data.visitors[0].offers[0].itemId);
        }

        [Test]
        public void Version14Save_DropsOldNotices_AndKeepsEverythingElse()
        {
            var data = OldSave();
            data.version = 14;
            SaveMigrations.Apply(data);

            Assert.AreEqual(SaveDataV1.CurrentVersion, data.version);
            Assert.IsEmpty(data.notices, "Old notices counted progress; they can't become orders.");
            Assert.AreEqual(3, data.inventory.Count);
            Assert.AreEqual("cabbage", data.inventory[0].itemId, "Ids were already current at version 14.");
        }

        [Test]
        public void CurrentSave_IsLeftAlone()
        {
            // A current save can't hold the old ids, so nothing is renamed even if one appears.
            var data = OldSave();
            data.version = SaveDataV1.CurrentVersion;
            SaveMigrations.Apply(data);

            Assert.AreEqual("cabbage", data.inventory[0].itemId);
            Assert.AreEqual("creamery", data.buildings[0].buildingId);
            Assert.AreEqual(1, data.notices.Count, "A current save keeps its orders.");
        }

        [Test]
        public void LoadingAnOldSaveFile_MigratesIt()
        {
            var directory = Path.Combine(Path.GetTempPath(), "FarmGameMigrationTest_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var service = new SaveService(directory, "save.json");
                File.WriteAllText(service.SavePath, JsonUtility.ToJson(OldSave()));

                var loaded = service.TryLoad(out var error);

                Assert.IsNull(error);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(SaveDataV1.CurrentVersion, loaded.version);
                Assert.AreEqual("bakery", loaded.buildings[2].buildingId);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
