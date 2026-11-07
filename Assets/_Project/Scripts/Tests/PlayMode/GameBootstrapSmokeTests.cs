using System.Collections;
using System.IO;
using FarmGame.Bootstrap;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Grid;
using FarmGame.Input;
using FarmGame.Save;
using FarmGame.UI;
using FarmGame.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FarmGame.Tests
{
    public sealed class GameBootstrapSmokeTests
    {
        private string _directory;
        private GameObject _bootstrapObject;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "FarmGamePlayModeTests",
                System.Guid.NewGuid().ToString("N"));
            GameBootstrap.SaveDirectoryOverride = _directory;

            _bootstrapObject = new GameObject("Bootstrap Test");
            _bootstrapObject.SetActive(false);
            var bootstrap = _bootstrapObject.AddComponent<GameBootstrap>();
            bootstrap.SaveFileName = "test-save.json";
            bootstrap.LoadSavedGame = false;
            _bootstrapObject.SetActive(true);

            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_CreatesFarmAndSupportsCoreLoop()
        {
            var bootstrap = _bootstrapObject.GetComponent<GameBootstrap>();
            Assert.IsTrue(bootstrap.IsInitialized, "Bootstrap did not initialize.");
            Assert.IsNotNull(bootstrap.Session);
            Assert.AreEqual(9, bootstrap.Session.Farm.PlotCount);
            Assert.IsNotNull(GameObject.Find("Farm World"));
            Assert.IsNotNull(GameObject.Find("HUD Canvas"));

            var safeArea = GameObject.Find("SafeArea");
            Assert.IsNotNull(safeArea);
            Assert.IsNotNull(safeArea.GetComponent<SafeAreaFitter>());
            Assert.AreEqual(safeArea.transform, GameObject.Find("TopBar").transform.parent);
            var cropDock = safeArea.transform.Find("CropDock");
            Assert.IsNotNull(cropDock);
            Assert.AreEqual(safeArea.transform, cropDock.parent);
            Assert.IsFalse(
                cropDock.gameObject.activeSelf,
                "Crop shop should be hidden until an empty plot is tapped.");

            var grassRenderer = GameObject.Find("Grass_0_0").GetComponent<SpriteRenderer>();
            Assert.AreEqual("Ground", grassRenderer.sortingLayerName);
            Assert.IsNotNull(grassRenderer.sprite, "Every farm cell should have a grass tile.");
            Assert.AreEqual(
                "Entities",
                GameObject.Find("Plot_0").GetComponent<SortingGroup>().sortingLayerName);

            var startingCoins = bootstrap.Session.Content.StartingCoins;
            var crop = bootstrap.Session.Content.Crops[0];
            Assert.IsTrue(bootstrap.Session.TryPlant(0, crop.Id, out var plantMessage), plantMessage);
            Assert.AreEqual(startingCoins - crop.SeedCost, bootstrap.Session.Wallet.Coins);

            bootstrap.Session.DebugAdvanceAll(crop.GrowSeconds + 1d);
            Assert.IsTrue(
                bootstrap.Session.TryHarvest(0, out var outcome, out var harvestMessage),
                harvestMessage);
            Assert.AreEqual(crop.YieldAmount, outcome.Amount);
            Assert.AreEqual(crop.YieldAmount, bootstrap.Session.Inventory.GetCount(crop.Id));

            var ui = GameObject.Find("Game UI").GetComponent<GameUI>();
            var farmView = GameObject.Find("Farm World").GetComponent<FarmView>();
            var camera = Camera.main;
            Assert.IsNotNull(ui);
            Assert.IsNotNull(farmView);
            Assert.IsNotNull(camera);

            var input = GameObject.Find("Game Input").GetComponent<FarmInputController>();
            Assert.IsNotNull(input);

            input.HandleWorldTap(camera.WorldToScreenPoint(farmView.GetPlotWorldPosition(1)));
            Assert.IsTrue(
                cropDock.gameObject.activeSelf,
                "Tapping an empty plot should open the crop shop.");

            var cropCard = cropDock.Find($"CropCardsViewport/CropCards/Crop_{crop.Id}").GetComponent<CropDragHandle>();
            Assert.IsNotNull(cropCard);

            var dragPlots = new[] { 0, 1, 2 };
            for (var i = 0; i < dragPlots.Length; i++)
            {
                var pointer = CreatePointer(camera, farmView, dragPlots[i]);
                if (i == 0)
                {
                    cropCard.OnBeginDrag(pointer);
                }
                else
                {
                    cropCard.OnDrag(pointer);
                }
            }

            cropCard.OnEndDrag(CreatePointer(camera, farmView, dragPlots[dragPlots.Length - 1]));

            for (var i = 0; i < dragPlots.Length; i++)
            {
                Assert.IsFalse(
                    bootstrap.Session.Farm.GetSnapshot(dragPlots[i]).IsEmpty,
                    $"Drag planting did not fill plot {dragPlots[i]}.");
            }

            Assert.AreEqual(
                startingCoins - crop.SeedCost - dragPlots.Length * crop.SeedCost,
                bootstrap.Session.Wallet.Coins);
            Assert.IsNull(GameObject.Find("CropDragGhost"));

            input.HandleWorldTap(camera.WorldToScreenPoint(farmView.GetPlotWorldPosition(dragPlots[0])));
            Assert.IsFalse(
                cropDock.gameObject.activeSelf,
                "Tapping a growing plot should close the crop shop.");

            bootstrap.Session.DebugAdvanceAll(crop.GrowSeconds + 1d);
            ui.ShowScythe(dragPlots[0]);

            var scytheObject = GameObject.Find("Scythe");
            Assert.IsNotNull(scytheObject, "Scythe popup was not shown.");
            var scythe = scytheObject.GetComponent<ScytheDragHandle>();
            Assert.IsNotNull(scythe);

            scythe.OnBeginDrag(CreatePointer(camera, farmView, dragPlots[0]));
            for (var i = 1; i < dragPlots.Length; i++)
            {
                scythe.OnDrag(CreatePointer(camera, farmView, dragPlots[i]));
            }

            scythe.OnEndDrag(CreatePointer(camera, farmView, dragPlots[dragPlots.Length - 1]));

            for (var i = 0; i < dragPlots.Length; i++)
            {
                Assert.IsTrue(
                    bootstrap.Session.Farm.GetSnapshot(dragPlots[i]).IsEmpty,
                    $"Scythe sweep did not harvest plot {dragPlots[i]}.");
            }

            Assert.AreEqual(
                crop.YieldAmount * (1 + dragPlots.Length),
                bootstrap.Session.Inventory.GetCount(crop.Id));
            Assert.IsNull(GameObject.Find("Scythe"));
            Assert.IsNotNull(GameObject.Find("BuildingShopButton"));

            Assert.IsTrue(
                bootstrap.Session.TrySell(crop.Id, out _, out _, out var sellMessage),
                sellMessage);

            // A new farm is level 1, so buy something available from the start.
            BuildingDefinition building = null;
            foreach (var candidate in bootstrap.Session.Content.Buildings)
            {
                if (candidate != null &&
                    candidate.Category != BuildingCategory.Landmark &&
                    bootstrap.Session.IsUnlocked(candidate))
                {
                    building = candidate;
                    break;
                }
            }

            Assert.IsNotNull(building, "No building is available at level 1.");
            var coinsBeforeBuilding = bootstrap.Session.Wallet.Coins;
            ui.BeginBuildingPlacement(building.Id);
            Assert.IsTrue(ui.IsBuildingPlacementActive);

            const int buildingCellX = 20;
            const int buildingCellY = 20;
            var buildingScreen = camera.WorldToScreenPoint(
                farmView.GetCellWorldPosition(new Vector2Int(buildingCellX, buildingCellY)));
            Assert.Greater(buildingScreen.z, 0f);
            Assert.IsTrue(
                ui.TryPlaceBuildingAtScreen(buildingScreen),
                "Building placement did not complete.");
            Assert.IsFalse(ui.IsBuildingPlacementActive);
            // The notice board is always Placed[0] on a new farm.
            Assert.AreEqual(2, bootstrap.Session.Buildings.Placed.Count);
            Assert.AreEqual(
                new Vector2Int(buildingCellX, buildingCellY),
                bootstrap.Session.Buildings.Placed[1].Origin);
            Assert.AreEqual(
                coinsBeforeBuilding - building.Cost,
                bootstrap.Session.Wallet.Coins);
            var buildingObject = GameObject.Find(
                $"Building_{building.Id}_{buildingCellX}_{buildingCellY}");
            Assert.IsNotNull(buildingObject, "Placed building was not rendered.");

            var buildingSprite = buildingObject.transform.Find("Sprite").GetComponent<SpriteRenderer>();
            Assert.IsNotNull(buildingSprite);
            Assert.AreEqual(
                IsoMath.FootprintWorldWidth(building.Footprint),
                buildingSprite.bounds.size.x,
                0.01f,
                "Building visual width does not match its slot footprint.");

            var buildButton = GameObject.Find("BuildingShopButton").GetComponent<Button>();
            Assert.IsNotNull(buildButton);
            buildButton.onClick.Invoke();

            var shop = GameObject.Find("BuildingShopOverlay");
            Assert.IsNotNull(shop);
            Assert.IsTrue(shop.activeSelf, "Building shop did not open.");
            var shopPanel = shop.transform.Find("BuildingShopPanel");
            Assert.IsNotNull(shopPanel);
            var slotTab = shopPanel.Find("SlotTab").GetComponent<Button>();
            var animalsTab = shopPanel.Find("AnimalsTab").GetComponent<Button>();
            var productionTab = shopPanel.Find("ProductionTab").GetComponent<Button>();
            Assert.IsNotNull(slotTab);
            Assert.IsNotNull(animalsTab);
            Assert.IsNotNull(productionTab);
            Assert.IsNotNull(
                shopPanel.Find("BuildingCardsViewport/BuildingCards/SlotCard_new_slot"),
                "Slot tab should show the new slot card.");

            animalsTab.onClick.Invoke();
            Assert.IsNotNull(
                shopPanel.Find("BuildingCardsViewport/BuildingCards/BuildingCard_fish_pond"),
                "Animals tab is missing the fish pond.");
            Assert.IsNotNull(
                shopPanel.Find("BuildingCardsViewport/BuildingCards/BuildingCard_coop"),
                "Animals tab is missing the coop.");
            Assert.IsNotNull(
                shopPanel.Find("BuildingCardsViewport/BuildingCards/BuildingCard_cow_stable"),
                "Animals tab is missing the cow stable.");
            Assert.IsNull(
                shopPanel.Find("BuildingCardsViewport/BuildingCards/BuildingCard_feed_grinder"),
                "Feed grinder should not appear in the Animals tab.");

            productionTab.onClick.Invoke();
            Assert.IsNotNull(
                shopPanel.Find("BuildingCardsViewport/BuildingCards/BuildingCard_feed_grinder"),
                "Production tab is missing the feed grinder.");

            slotTab.onClick.Invoke();
            var slotCard = shopPanel.Find("BuildingCardsViewport/BuildingCards/SlotCard_new_slot");
            Assert.IsNotNull(slotCard, "Slot tab did not restore the new slot card.");

            Assert.IsTrue(bootstrap.Session.Wallet.TrySpend(
                bootstrap.Session.Wallet.Coins - (GameSession.NewSlotCost - 1)));
            Assert.Less(bootstrap.Session.Wallet.Coins, GameSession.NewSlotCost);
            slotCard.Find("SlotBuyButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(
                ui.IsPlacementActive,
                "Slot placement should not start without enough coins.");
            Assert.IsTrue(shop.activeSelf, "Building shop should stay open when coins are insufficient.");
            var toastText = GameObject.Find("ToastText").GetComponent<Text>();
            StringAssert.Contains("Need 100 coins", toastText.text);

            bootstrap.Session.Wallet.Add(200);
            slotCard.Find("SlotBuyButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(ui.IsPlacementActive, "Slot placement did not start.");
            Assert.IsFalse(shop.activeSelf, "Building shop should close when slot placement starts.");

            const int slotCellX = 22;
            const int slotCellY = 22;
            var slotScreen = camera.WorldToScreenPoint(
                farmView.GetCellWorldPosition(new Vector2Int(slotCellX, slotCellY)));
            Assert.Greater(slotScreen.z, 0f);
            var coinsBeforeSlot = bootstrap.Session.Wallet.Coins;
            Assert.IsTrue(ui.TryPlaceSlotAtScreen(slotScreen), "Slot placement did not complete.");
            Assert.IsFalse(ui.IsPlacementActive);
            Assert.AreEqual(10, bootstrap.Session.Farm.PlotCount);
            Assert.AreEqual(
                new Vector2Int(slotCellX, slotCellY),
                bootstrap.Session.Farm.PlotCells[9]);
            Assert.AreEqual(
                coinsBeforeSlot - GameSession.NewSlotCost,
                bootstrap.Session.Wallet.Coins);
            Assert.IsNotNull(GameObject.Find("Plot_9"), "Added plot was not rendered.");

            var feedGrinder = bootstrap.Session.Content.GetBuilding(GameSession.FeedGrinderBuildingId);
            Assert.IsNotNull(feedGrinder, "Feed grinder content was not generated.");

            bootstrap.Session.Wallet.Add(300);
            buildButton.onClick.Invoke();
            shop = GameObject.Find("BuildingShopOverlay");
            Assert.IsNotNull(shop);
            shopPanel = shop.transform.Find("BuildingShopPanel");
            productionTab = shopPanel.Find("ProductionTab").GetComponent<Button>();
            productionTab.onClick.Invoke();
            var grinderCard = shopPanel.Find("BuildingCardsViewport/BuildingCards/BuildingCard_feed_grinder");
            Assert.IsNotNull(grinderCard, "Production tab is missing the feed grinder.");

            var coinsBeforeGrinder = bootstrap.Session.Wallet.Coins;
            grinderCard.Find("BuyButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(ui.IsPlacementActive, "Feed grinder placement did not start.");
            Assert.IsFalse(shop.activeSelf);

            const int grinderCellX = 14;
            const int grinderCellY = 20;
            var grinderScreen = camera.WorldToScreenPoint(
                farmView.GetCellWorldPosition(new Vector2Int(grinderCellX, grinderCellY)));
            Assert.Greater(grinderScreen.z, 0f);
            Assert.IsTrue(
                ui.TryPlaceBuildingAtScreen(grinderScreen),
                "Feed grinder placement did not complete.");
            Assert.IsFalse(ui.IsPlacementActive);
            Assert.AreEqual(3, bootstrap.Session.Buildings.Placed.Count);
            Assert.AreEqual(
                new Vector2Int(grinderCellX, grinderCellY),
                bootstrap.Session.Buildings.Placed[2].Origin);
            Assert.AreEqual(
                coinsBeforeGrinder - feedGrinder.Cost,
                bootstrap.Session.Wallet.Coins);
            Assert.IsNotNull(
                GameObject.Find($"Building_feed_grinder_{grinderCellX}_{grinderCellY}"),
                "Placed feed grinder was not rendered.");

            var fishFeed = bootstrap.Session.Content.GetFeedRecipe(GameSession.FishFeedId);
            var wheatBefore = bootstrap.Session.Inventory.GetCount(fishFeed.CropId);
            bootstrap.Session.Inventory.Add(fishFeed.CropId, fishFeed.CropAmount + 1);
            var grinderWorld = farmView.GetBuildingWorldPosition(
                feedGrinder,
                new Vector2Int(grinderCellX, grinderCellY));
            input.HandleWorldTap(camera.WorldToScreenPoint(grinderWorld));
            Assert.IsTrue(ui.IsFeedGrinderVisible, "Tapping the feed grinder did not open production.");

            for (var i = 0; i < bootstrap.Session.Content.FeedRecipes.Count; i++)
            {
                Assert.IsNotNull(
                    GameObject.Find($"FeedRow_{bootstrap.Session.Content.FeedRecipes[i].Id}"),
                    $"Feed grinder is missing the {bootstrap.Session.Content.FeedRecipes[i].DisplayName} row.");
            }

            var cowRow = GameObject.Find($"FeedRow_{GameSession.CowFeedId}");
            Assert.IsFalse(
                cowRow.transform.Find("GrindButton").GetComponent<Button>().interactable,
                "Cow feed should not be makeable without carrots.");

            var grindRow = GameObject.Find($"FeedRow_{GameSession.FishFeedId}");
            grindRow.transform.Find("GrindButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(wheatBefore + 1, bootstrap.Session.Inventory.GetCount(fishFeed.CropId));
            Assert.AreEqual(
                0,
                bootstrap.Session.Inventory.GetCount(GameSession.FishFeedId),
                "Feed should not appear until production finishes.");
            Assert.AreEqual(1, ui.ActiveFeedGrinder.Production.Count);

            var collectButton = GameObject.Find("CollectButton").GetComponent<Button>();
            yield return null;
            StringAssert.Contains(
                ":",
                GameObject.Find("QueueSlot_0").transform.Find("Status").GetComponent<Text>().text,
                "Queue slot should show a countdown.");
            Assert.IsFalse(collectButton.interactable, "Nothing is ready to collect yet.");

            bootstrap.Session.DebugAdvanceAll(fishFeed.ProductionSeconds + 1d);
            yield return null;
            Assert.IsTrue(collectButton.interactable, "Finished feed should be collectable.");
            collectButton.onClick.Invoke();
            Assert.AreEqual(1, bootstrap.Session.Inventory.GetCount(GameSession.FishFeedId));
            Assert.AreEqual(0, ui.ActiveFeedGrinder.Production.Count);
            var feedCountText = GameObject.Find($"FeedRow_{GameSession.FishFeedId}").transform.Find("Owned/Text").GetComponent<Text>();
            Assert.AreEqual("x1", feedCountText.text);

            GameObject.Find("InventoryButton").GetComponent<Button>().onClick.Invoke();
            var fishFeedTile = GameObject.Find($"Row_{GameSession.FishFeedId}");
            Assert.IsNotNull(fishFeedTile, "Fish feed is missing from the inventory.");

            // A tap inside a window must not reach the "tap outside to close" button.
            // Unity sends a click to the nearest object up the hierarchy that handles clicks.
            Assert.IsNull(
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(fishFeedTile),
                "Tapping an inventory tile would close the window.");
            Assert.IsNull(
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(GameObject.Find("InventoryPanel")),
                "Tapping the window background would close it.");

            var inventoryOverlay = GameObject.Find("InventoryOverlay");
            inventoryOverlay.transform.Find("Dim").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(inventoryOverlay.activeSelf, "Tapping outside the window should close it.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator SellingFish_LeavesTheOtherFishWhereTheyAre()
        {
            var session = _bootstrapObject.GetComponent<GameBootstrap>().Session;
            var ui = GameObject.Find("Game UI").GetComponent<GameUI>();
            var farmView = GameObject.Find("Farm World").GetComponent<FarmView>();
            var input = GameObject.Find("Game Input").GetComponent<FarmInputController>();
            var camera = Camera.main;

            session.Wallet.Add(2000);
            Assert.IsTrue(PlaceBuilding(ui, farmView, camera, "fish_pond", 8, 8), "Fish pond placement failed.");
            var pond = session.Buildings.Placed[1];
            input.HandleWorldTap(camera.WorldToScreenPoint(farmView.GetBuildingWorldPosition(pond.Definition, pond.Origin)));
            Assert.IsTrue(ui.IsAnimalPenVisible);

            var panel = GameObject.Find("AnimalPenOverlay").transform.Find("AnimalPenPanel");
            var stage = panel.Find("AnimalPenStage");
            var addButton = GameObject.Find("AddAnimalButton").GetComponent<Button>();
            for (var i = 0; i < 4; i++)
            {
                // The list makes its cards each time it opens.
                addButton.onClick.Invoke();
                panel.Find("FishPicker/FishCards/FishCard_goldfish/BuyFishButton").GetComponent<Button>().onClick.Invoke();
                yield return null;
            }

            // Fish 0, 1 and 3 grow up; fish 2 stays hungry.
            session.Inventory.Add(GameSession.FishFeedId, 3);
            Assert.IsTrue(session.TryFeedAnimal(pond, 0, out var message), message);
            Assert.IsTrue(session.TryFeedAnimal(pond, 1, out message), message);
            Assert.IsTrue(session.TryFeedAnimal(pond, 3, out message), message);
            session.DebugAdvanceAll(session.Content.GetFish("goldfish").GrowSeconds + 1d);
            yield return null;

            var fish = new RectTransform[4];
            for (var i = 0; i < 4; i++)
            {
                fish[i] = stage.Find($"AnimalPenAnimals/Animal_fish_{i}").GetComponent<RectTransform>();
            }

            // Selling one fish by tapping it leaves the other three where they are.
            var before = new[] { fish[1].anchoredPosition, fish[2].anchoredPosition, fish[3].anchoredPosition };
            fish[0].GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(3, pond.AnimalCount);
            Assert.IsTrue(fish[0] == null, "The sold fish should leave the pond.");
            for (var i = 1; i < 4; i++)
            {
                Assert.IsTrue(fish[i] != null, $"Fish {i} was recreated.");
                Assert.Less(Vector2.Distance(before[i - 1], fish[i].anchoredPosition), 15f, $"Fish {i} jumped.");
            }

            Assert.AreEqual("Animal_fish_1", fish[2].name, "The fish after the sold one move up a place.");
            Assert.AreEqual("Animal_fish_2", fish[3].name);
            Assert.IsNull(stage.Find("AnimalStatusBubbles/AnimalStatus_3"), "The sold fish's bubble should go too.");

            // A tap still reaches the right fish: the hungry one is now at index 1.
            session.Inventory.Add(GameSession.FishFeedId, 1);
            fish[2].GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(3, pond.AnimalCount, "Tapping the hungry fish must not sell another one.");
            Assert.AreEqual(AnimalStatus.Hungry, session.GetAnimalStatus(pond, 1, out _), "A tap only shows the feed.");
            TapFeedOn(fish[2]);
            Assert.AreEqual(AnimalStatus.Producing, session.GetAnimalStatus(pond, 1, out _));

            // Selling both grown fish leaves the growing one where it is.
            var position = fish[2].anchoredPosition;
            fish[3].GetComponent<Button>().onClick.Invoke();
            fish[1].GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, pond.AnimalCount);
            Assert.IsTrue(fish[1] == null && fish[3] == null, "Both grown fish should leave the pond.");
            Assert.IsTrue(fish[2] != null, "The growing fish was recreated.");
            Assert.Less(Vector2.Distance(position, fish[2].anchoredPosition), 15f, "The growing fish jumped.");
            Assert.AreEqual("Animal_fish_0", fish[2].name);
            Assert.IsNotNull(stage.Find("AnimalStatusBubbles/AnimalStatus_0"));
        }

        [UnityTest]
        public IEnumerator AnimalPens_OpenAndFillToCapacity()
        {
            var bootstrap = _bootstrapObject.GetComponent<GameBootstrap>();
            var session = bootstrap.Session;
            var ui = GameObject.Find("Game UI").GetComponent<GameUI>();
            var farmView = GameObject.Find("Farm World").GetComponent<FarmView>();
            var input = GameObject.Find("Game Input").GetComponent<FarmInputController>();
            var camera = Camera.main;
            Assert.IsNotNull(session);
            Assert.IsNotNull(ui);
            Assert.IsNotNull(farmView);
            Assert.IsNotNull(input);
            Assert.IsNotNull(camera);

            session.Wallet.Add(2000);

            Assert.IsTrue(
                PlaceBuilding(ui, farmView, camera, "fish_pond", 8, 8),
                "Fish pond placement failed.");
            var pond = session.Buildings.Placed[1];
            Assert.AreEqual(AnimalKind.Fish, pond.Definition.AnimalKind);

            input.HandleWorldTap(camera.WorldToScreenPoint(
                farmView.GetBuildingWorldPosition(pond.Definition, pond.Origin)));
            Assert.IsTrue(ui.IsAnimalPenVisible, "Tapping the fish pond did not open its pen.");

            var overlay = GameObject.Find("AnimalPenOverlay");
            Assert.IsNotNull(overlay);
            var stage = overlay.transform.Find("AnimalPenPanel/AnimalPenStage");
            Assert.IsNotNull(stage);
            var background = stage.Find("PenBackground").GetComponent<Image>();
            Assert.IsNotNull(background);
            Assert.AreEqual("pen_underwater", background.sprite.name);
            Assert.AreEqual(
                "Fish 0 / 4",
                GameObject.Find("AnimalPenCount").GetComponent<Text>().text);

            // The pond's button opens a list of fish; each kind has its own price and level.
            var addButton = GameObject.Find("AddAnimalButton").GetComponent<Button>();
            Assert.AreEqual("Buy Fish", addButton.GetComponentInChildren<Text>().text);
            var picker = overlay.transform.Find("AnimalPenPanel/FishPicker");
            Assert.IsNotNull(picker, "The fish pond has no fish list.");

            addButton.onClick.Invoke();
            Assert.IsTrue(ui.IsFishPickerVisible, "Buy Fish should open the list of fish.");
            var troutBuy = picker.Find("FishCards/FishCard_trout/BuyFishButton");
            Assert.IsNotNull(troutBuy, "Every fish should have a card.");
            Assert.IsFalse(troutBuy.GetComponent<Button>().interactable, "Trout is locked on a new farm.");
            StringAssert.Contains("Level", troutBuy.GetComponentInChildren<Text>().text);

            var goldfish = session.Content.GetFish("goldfish");
            var coinsBeforeFish = session.Wallet.Coins;
            for (var i = 1; i <= 4; i++)
            {
                if (!ui.IsFishPickerVisible)
                {
                    addButton.onClick.Invoke();
                }

                picker.Find("FishCards/FishCard_goldfish/BuyFishButton").GetComponent<Button>().onClick.Invoke();
                yield return null;

                Assert.IsFalse(ui.IsFishPickerVisible, "Buying a fish should close the list.");
                Assert.AreEqual(i, pond.AnimalCount);
                Assert.AreEqual("goldfish", pond.GetAnimalKindId(i - 1));
                Assert.AreEqual(
                    i,
                    CountActiveAnimals(stage, "Animal_fish_"),
                    $"Expected {i} fish in the pond.");
                Assert.AreEqual(
                    $"Fish {i} / 4",
                    GameObject.Find("AnimalPenCount").GetComponent<Text>().text);
            }

            Assert.AreEqual(coinsBeforeFish - 4 * goldfish.Cost, session.Wallet.Coins);
            Assert.IsFalse(addButton.interactable, "Buy Fish should disable when the pond is full.");
            Assert.AreEqual("Full", addButton.GetComponentInChildren<Text>().text);

            var fish = FindActiveAnimal(stage, "Animal_fish_");
            Assert.IsNotNull(fish, "The fish pond has no fish visual.");
            Assert.AreEqual(goldfish.Sprite, fish.GetComponent<Image>().sprite, "Each fish shows its own kind.");
            var fishRect = fish.GetComponent<RectTransform>();
            var fishStart = fishRect.anchoredPosition;
            for (var i = 0; i < 12; i++)
            {
                yield return null;
            }

            Assert.Greater(
                Vector2.Distance(fishStart, fishRect.anchoredPosition),
                0.5f,
                "Fish did not swim around the pond.");

            // The window has no stock counts, stats or bulk buttons: each fish has its own button.
            var panelTransform = overlay.transform.Find("AnimalPenPanel");
            foreach (var removed in new[] { "FeedAllButton", "CollectProductsButton", "FeedStock", "ProductStock", "AnimalPenHint" })
            {
                Assert.IsNull(panelTransform.Find(removed), $"{removed} should be gone.");
            }

            // A hungry fish shows nothing above it, and the feed stays hidden until a fish is tapped.
            yield return null;
            Assert.IsFalse(
                stage.Find("AnimalStatusBubbles/AnimalStatus_0").gameObject.activeSelf,
                "A hungry fish should not show its feed.");
            var feedTray = stage.Find("AnimalFeedTray");
            Assert.IsNotNull(feedTray, "The window has no feed tray.");
            Assert.IsFalse(feedTray.gameObject.activeSelf, "The feed shows only after tapping an animal.");

            // Tapping a fish shows the feed and its stock, without feeding it.
            stage.Find("AnimalPenAnimals/Animal_fish_0").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(feedTray.gameObject.activeSelf, "Tapping an animal should show the feed.");
            Assert.AreEqual(
                feedTray.gameObject,
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(feedTray.gameObject),
                "The feed must take taps.");
            Assert.IsNull(feedTray.GetComponent<IBeginDragHandler>(), "The feed is tapped, not dragged.");
            Assert.Less(
                Vector2.Distance(
                    feedTray.GetComponent<RectTransform>().anchoredPosition,
                    stage.Find("AnimalPenAnimals/Animal_fish_0").GetComponent<RectTransform>().anchoredPosition),
                200f,
                "The feed should appear on the tapped animal.");
            Assert.AreEqual(session.GetFeedFor(pond).Icon, feedTray.Find("Icon").GetComponent<Image>().sprite);
            Assert.AreEqual("0", feedTray.Find("CountPill/Text").GetComponent<Text>().text);
            Assert.AreEqual(AnimalStatus.Hungry, session.GetAnimalStatus(pond, 0, out _));

            // With no feed in storage, a tap changes nothing.
            TapFeedOn(stage.Find("AnimalPenAnimals/Animal_fish_0"));
            Assert.AreEqual(AnimalStatus.Hungry, session.GetAnimalStatus(pond, 0, out _));

            // Each tap feeds one fish with one pack, so 3 packs feed 3 of the 4 fish.
            session.Inventory.Add(GameSession.FishFeedId, 3);
            yield return null;
            Assert.AreEqual("3", feedTray.Find("CountPill/Text").GetComponent<Text>().text);

            for (var i = 0; i < 3; i++)
            {
                TapFeedOn(stage.Find($"AnimalPenAnimals/Animal_fish_{i}"));
                Assert.AreEqual(AnimalStatus.Producing, session.GetAnimalStatus(pond, i, out _));
                Assert.AreEqual(2 - i, session.Inventory.GetCount(GameSession.FishFeedId), "Each tap uses one pack.");
            }

            Assert.AreEqual(AnimalStatus.Hungry, session.GetAnimalStatus(pond, 3, out _));
            yield return null;
            Assert.IsTrue(
                stage.Find("AnimalStatusBubbles/AnimalStatus_0").gameObject.activeSelf,
                "A fed fish shows its timer.");
            Assert.IsFalse(feedTray.gameObject.activeSelf, "The feed should go away once the animal is fed.");

            // A fed fish has no use for the feed, so tapping it doesn't bring it back.
            stage.Find("AnimalPenAnimals/Animal_fish_0").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsFalse(feedTray.gameObject.activeSelf, "Tapping a fed animal should not show the feed.");

            // Grown fish are sold for coins and XP and leave the pond; nothing goes to storage.
            session.DebugAdvanceAll(goldfish.GrowSeconds + 1d);
            yield return null;
            Assert.AreEqual(
                $"+{UIFactory.FormatNumber(goldfish.SellPrice)}",
                stage.Find("AnimalStatusBubbles/AnimalStatus_0/Background/Label").GetComponent<Text>().text,
                "Fish have no product, so a grown fish shows its price.");
            var coinsBeforeSale = session.Wallet.Coins;
            for (var i = 2; i >= 0; i--)
            {
                stage.Find($"AnimalPenAnimals/Animal_fish_{i}").GetComponent<Button>().onClick.Invoke();
            }

            yield return null;
            Assert.AreEqual(coinsBeforeSale + 3 * goldfish.SellPrice, session.Wallet.Coins);
            Assert.AreEqual(1, pond.AnimalCount, "Sold fish leave the pond.");
            Assert.AreEqual(1, CountActiveAnimals(stage, "Animal_fish_"));
            Assert.AreEqual("Fish 1 / 4", GameObject.Find("AnimalPenCount").GetComponent<Text>().text);
            Assert.AreEqual(0, session.Storage.Used, "Selling fish puts nothing in storage.");

            // Tapping the feed on the one hungry fish feeds just that fish.
            session.Inventory.Add(GameSession.FishFeedId, 1);
            TapFeedOn(stage.Find("AnimalPenAnimals/Animal_fish_0"));
            Assert.AreEqual(AnimalStatus.Producing, session.GetAnimalStatus(pond, 0, out _));
            Assert.AreEqual(0, session.Inventory.GetCount(GameSession.FishFeedId));

            // A newly bought fish is hungry; once fed, the bubble above it takes taps.
            addButton.onClick.Invoke();
            picker.Find("FishCards/FishCard_goldfish/BuyFishButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(2, pond.AnimalCount);
            session.Inventory.Add(GameSession.FishFeedId, 1);
            TapFeedOn(stage.Find("AnimalPenAnimals/Animal_fish_1"));
            Assert.AreEqual(AnimalStatus.Producing, session.GetAnimalStatus(pond, 1, out _));
            Assert.AreEqual(0, session.Inventory.GetCount(GameSession.FishFeedId));
            yield return null;
            var bubble = stage.Find("AnimalStatusBubbles/AnimalStatus_1/Background").gameObject;
            Assert.AreEqual(
                bubble,
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(bubble),
                "The status bubble should receive taps.");
            Assert.IsTrue(bubble.GetComponent<Image>().raycastTarget, "The status bubble must not let taps through.");

            overlay.transform.Find("AnimalPenPanel/CloseButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.IsFalse(ui.IsAnimalPenVisible, "Close did not dismiss the fish pond.");

            // The coop and cow stable unlock at later levels than a new farm starts on.
            var stableLevel = session.Content.GetBuilding("cow_stable").UnlockLevel;
            while (session.Level.Level < stableLevel)
            {
                session.Level.AddXp(session.Level.XpToNextLevel);
            }

            Assert.IsTrue(ui.IsLevelUpVisible, "Levelling up should open the level-up window.");
            Assert.Greater(ui.LevelUpUnlockCount, 0, "The level-up window should list the new unlocks.");
            Assert.IsNotNull(
                GameObject.Find("Unlock_Building_cow_stable"),
                "The cow stable should have a card in the level-up window.");
            ui.CloseLevelUp();
            Assert.IsFalse(ui.IsLevelUpVisible);

            Assert.IsTrue(
                PlaceBuilding(ui, farmView, camera, "coop", 8, 4),
                "Coop placement failed.");
            var coop = session.Buildings.Placed[2];
            Assert.AreEqual(AnimalKind.Chicken, coop.Definition.AnimalKind);

            input.HandleWorldTap(camera.WorldToScreenPoint(
                farmView.GetBuildingWorldPosition(coop.Definition, coop.Origin)));
            Assert.IsTrue(ui.IsAnimalPenVisible, "Tapping the coop did not open its pen.");
            Assert.AreEqual("pen_coop", background.sprite.name);
            Assert.AreEqual(
                "Chicken 0 / 6",
                GameObject.Find("AnimalPenCount").GetComponent<Text>().text);

            addButton = GameObject.Find("AddAnimalButton").GetComponent<Button>();
            for (var i = 1; i <= 6; i++)
            {
                addButton.onClick.Invoke();
                yield return null;

                Assert.AreEqual(i, coop.AnimalCount);
                Assert.AreEqual(
                    i,
                    CountActiveAnimals(stage, "Animal_chicken_"),
                    $"Expected {i} chickens in the coop.");
            }

            Assert.IsFalse(addButton.interactable, "Add button should disable when the coop is full.");

            // No two chickens stand on the same spot, even in a full coop.
            yield return null;
            for (var i = 0; i < 6; i++)
            {
                for (var j = i + 1; j < 6; j++)
                {
                    Assert.Greater(
                        Vector2.Distance(
                            stage.Find($"AnimalPenAnimals/Animal_chicken_{i}").GetComponent<RectTransform>().anchoredPosition,
                            stage.Find($"AnimalPenAnimals/Animal_chicken_{j}").GetComponent<RectTransform>().anchoredPosition),
                        120f,
                        $"Chickens {i} and {j} overlap.");
                }
            }

            // Tap a hungry chicken, then the feed; when it's done, it shows its product. Tap to collect.
            var chickenFeed = session.GetFeedFor(coop);
            var egg = session.GetProductFor(coop);
            session.Inventory.Add(chickenFeed.Id, 1);
            yield return null;
            var chickenBubble = stage.Find("AnimalStatusBubbles/AnimalStatus_0/Background");
            TapFeedOn(stage.Find("AnimalPenAnimals/Animal_chicken_0"));
            Assert.AreEqual(AnimalStatus.Producing, session.GetAnimalStatus(coop, 0, out _));

            session.DebugAdvanceAll(coop.Definition.ProductionSeconds + 1d);
            yield return null;
            Assert.AreEqual(egg.Icon, chickenBubble.Find("Icon").GetComponent<Image>().sprite);
            Assert.AreEqual(string.Empty, chickenBubble.Find("Label").GetComponent<Text>().text);

            // Opening the window doesn't collect: the product waits under its icon.
            overlay.transform.Find("AnimalPenPanel/CloseButton").GetComponent<Button>()
                .onClick.Invoke();
            yield return null;
            ui.ShowAnimalPen(coop);
            yield return null;
            Assert.AreEqual(AnimalStatus.Ready, session.GetAnimalStatus(coop, 0, out _));
            Assert.AreEqual(0, session.Inventory.GetCount(egg.Id));
            stage.Find("AnimalStatusBubbles/AnimalStatus_0/Background").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, session.Inventory.GetCount(egg.Id));
            Assert.AreEqual(AnimalStatus.Hungry, session.GetAnimalStatus(coop, 0, out _));

            // Tap a hungry animal for the feed, then tap the feed. With none in storage it stays up.
            stage.Find("AnimalPenAnimals/Animal_chicken_0").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(feedTray.gameObject.activeSelf, "Tapping a hungry animal should show the feed.");
            Assert.AreEqual(0, session.Inventory.GetCount(chickenFeed.Id));
            var feedTap = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(feedTray.gameObject, feedTap, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(AnimalStatus.Hungry, session.GetAnimalStatus(coop, 0, out _));
            Assert.IsTrue(feedTray.gameObject.activeSelf, "With no feed, the popup should stay up.");

            session.Inventory.Add(chickenFeed.Id, 1);
            ExecuteEvents.Execute(feedTray.gameObject, feedTap, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(AnimalStatus.Producing, session.GetAnimalStatus(coop, 0, out _));
            Assert.AreEqual(0, session.Inventory.GetCount(chickenFeed.Id), "Tapping the feed uses one pack.");
            Assert.IsFalse(feedTray.gameObject.activeSelf, "The feed should go away once the animal is fed.");

            overlay.transform.Find("AnimalPenPanel/CloseButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.IsFalse(ui.IsAnimalPenVisible, "Close did not dismiss the coop.");

            Assert.IsTrue(
                PlaceBuilding(ui, farmView, camera, "cow_stable", 1, 8),
                "Cow stable placement failed.");
            var stable = session.Buildings.Placed[3];
            Assert.AreEqual(AnimalKind.Cow, stable.Definition.AnimalKind);

            input.HandleWorldTap(camera.WorldToScreenPoint(
                farmView.GetBuildingWorldPosition(stable.Definition, stable.Origin)));
            Assert.IsTrue(ui.IsAnimalPenVisible, "Tapping the cow stable did not open its pen.");
            Assert.AreEqual("pen_stable", background.sprite.name);
            Assert.AreEqual(
                "Cow 0 / 3",
                GameObject.Find("AnimalPenCount").GetComponent<Text>().text);

            addButton = GameObject.Find("AddAnimalButton").GetComponent<Button>();
            for (var i = 1; i <= 3; i++)
            {
                addButton.onClick.Invoke();
                yield return null;

                Assert.AreEqual(i, stable.AnimalCount);
                Assert.AreEqual(
                    i,
                    CountActiveAnimals(stage, "Animal_cow_"),
                    $"Expected {i} cows in the stable.");
            }

            Assert.IsFalse(addButton.interactable, "Add button should disable when the stable is full.");

            // The game writes pending changes once at the end of each frame.
            yield return null;
            Assert.IsFalse(session.HasUnsavedChanges, "Changes should be saved at the end of the frame.");
            var saveService = new SaveService(_directory, "test-save.json");
            var saveData = saveService.TryLoad(out var loadError);
            Assert.IsNull(loadError);
            Assert.IsNotNull(saveData);
            Assert.AreEqual(SaveDataV1.CurrentVersion, saveData.version);
            Assert.AreEqual(4, saveData.buildings.Count, "The notice board plus three animal buildings.");
            Assert.AreEqual(GameSession.NoticeBoardBuildingId, saveData.buildings[0].buildingId);
            Assert.AreEqual(2, saveData.buildings[1].animalCount, "Two fish left after selling three.");
            CollectionAssert.AreEqual(new[] { "goldfish", "goldfish" }, saveData.buildings[1].animalKinds);
            Assert.AreEqual(6, saveData.buildings[2].animalCount);
            Assert.AreEqual(3, saveData.buildings[3].animalCount);

            yield return null;
        }

        [UnityTest]
        public IEnumerator Beach_CanBeVisitedAndKeepsTheFarmSafe()
        {
            var ui = GameObject.Find("Game UI").GetComponent<GameUI>();
            var input = GameObject.Find("Game Input").GetComponent<FarmInputController>();
            var farmView = GameObject.Find("Farm World").GetComponent<FarmView>();
            var camera = Camera.main;

            foreach (var part in new[] { "BeachLand", "BeachShore", "BeachSea", "BeachSurf", "BeachDock", "BeachBoat", "OpenSeaWater", "OpenSeaBoat" })
            {
                Assert.IsNotNull(GameObject.Find(part)?.GetComponent<SpriteRenderer>()?.sprite, $"{part} has no art.");
            }

            var travel = GameObject.Find("BeachButton").GetComponent<Button>();
            var shopButton = GameObject.Find("BuildingShopButton");
            Assert.AreEqual("Beach", travel.transform.Find("Caption").GetComponent<Text>().text);

            // An open window doesn't come along.
            GameObject.Find("InventoryButton").GetComponent<Button>().onClick.Invoke();
            camera.orthographicSize = 5f;
            var farmPosition = camera.transform.position;
            travel.onClick.Invoke();
            yield return null;

            Assert.IsTrue(ui.IsAtBeach);
            Assert.IsFalse(GameObject.Find("InventoryPanel") != null, "The inventory should close when leaving.");
            Assert.IsFalse(shopButton.activeSelf, "The Shop places things on the farm, so it hides at the beach.");
            Assert.AreEqual("Farm", travel.transform.Find("Caption").GetComponent<Text>().text);
            Assert.IsTrue(FarmView.BeachBounds.Contains(new Vector3(camera.transform.position.x, camera.transform.position.y, 0f)));

            // The camera stays at the beach, and the beach is not farmland.
            yield return null;
            Assert.IsTrue(FarmView.BeachBounds.Contains(new Vector3(camera.transform.position.x, camera.transform.position.y, 0f)));
            Assert.IsFalse(farmView.TryGetCellAtWorld(camera.transform.position, out _));
            input.HandleWorldTap(camera.WorldToScreenPoint(FarmView.BeachCenter));
            Assert.IsFalse(ui.IsAnimalPenVisible || ui.IsFeedGrinderVisible || ui.IsNoticeBoardVisible);

            // Framed like the farm, with sea filling the view even zoomed all the way out.
            Assert.AreEqual(new IsoGrid(12, 12).WorldBounds.size, FarmView.BeachBounds.size);
            var sea = GameObject.Find("BeachSea").GetComponent<SpriteRenderer>().bounds;
            Assert.GreaterOrEqual(sea.size.y, IsoCameraRig.MaxZoom * 2f);
            Assert.GreaterOrEqual(sea.size.x, IsoCameraRig.MaxZoom * 2f * (32f / 9f), "Even on an ultrawide screen.");

            // Back to the same view of the farm.
            camera.orthographicSize = 7f;
            travel.onClick.Invoke();
            yield return null;
            Assert.IsFalse(ui.IsAtBeach);
            Assert.IsTrue(shopButton.activeSelf);
            Assert.AreEqual(5f, camera.orthographicSize, 0.001f);
            Assert.AreEqual(farmPosition.x, camera.transform.position.x, 0.001f);
            Assert.AreEqual(farmPosition.y, camera.transform.position.y, 0.001f);

            // A level-up's Shop button at the beach goes back to the farm with the shop open.
            ui.GoToBeach();
            ui.ShowLevelUp(4);
            GameObject.Find("LevelUpShopButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(ui.IsAtBeach);
            Assert.IsNotNull(GameObject.Find("BuildingShopOverlay"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Beach_HasTheSeaOnOneSideOnly()
        {
            var land = GameObject.Find("BeachLand").GetComponent<SpriteRenderer>();

            bool IsSand(Vector2 offset)
            {
                var local = land.transform.InverseTransformPoint(FarmView.BeachCenter + (Vector3)offset);
                return Mathf.Abs(local.x) <= land.size.x * 0.5f && Mathf.Abs(local.y) <= land.size.y * 0.5f;
            }

            // What an ultrawide screen shows, zoomed all the way out.
            var halfHeight = IsoCameraRig.MaxZoom;
            var halfWidth = halfHeight * (32f / 9f);
            Assert.IsTrue(IsSand(new Vector2(-halfWidth, halfHeight)), "Sand should reach the top left.");
            Assert.IsTrue(IsSand(new Vector2(-halfWidth, -halfHeight)), "Sand should reach the bottom left.");
            Assert.IsTrue(IsSand(new Vector2(0f, halfHeight)), "Sand should reach the top.");
            Assert.IsFalse(IsSand(new Vector2(halfWidth, -halfHeight)), "The sea should be at the bottom right.");
            Assert.IsFalse(IsSand(FarmView.DockWorldPosition - FarmView.BeachCenter), "The dock should reach over the water.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dock_GoesToTheOpenSeaAndBack()
        {
            var ui = GameObject.Find("Game UI").GetComponent<GameUI>();
            var input = GameObject.Find("Game Input").GetComponent<FarmInputController>();
            var camera = Camera.main;
            var travel = GameObject.Find("BeachButton").GetComponent<Button>();
            var caption = travel.transform.Find("Caption").GetComponent<Text>();
            var shopButton = GameObject.Find("BuildingShopButton");
            var farmPosition = camera.transform.position;

            // Only from the beach: the farm can't sail, whatever the tap lands on.
            ui.GoToOpenSea();
            Assert.IsFalse(ui.IsAtSea);

            ui.GoToBeach();
            yield return null;
            var beachPosition = camera.transform.position;

            // A tap on the open sand doesn't sail.
            input.HandleWorldTap(camera.WorldToScreenPoint(FarmView.BeachCenter + new Vector3(-1f, -2.5f, 0f)));
            Assert.IsFalse(ui.IsAtSea);

            input.HandleWorldTap(camera.WorldToScreenPoint(FarmView.DockWorldPosition));
            yield return null;
            Assert.IsTrue(ui.IsAtSea, "Tapping the dock should go to the open sea.");
            Assert.IsFalse(ui.IsAtBeach);
            Assert.AreEqual(FarmView.OpenSeaCenter.x, camera.transform.position.x, 0.001f);
            Assert.AreEqual(FarmView.OpenSeaCenter.y, camera.transform.position.y, 0.001f);
            Assert.AreEqual("Beach", caption.text, "The way back is to the beach.");
            Assert.IsFalse(shopButton.activeSelf, "The Shop places things on the farm.");

            // Water to every edge, zoomed all the way out on an ultrawide screen, and no shore.
            var water = GameObject.Find("OpenSeaWater").GetComponent<SpriteRenderer>().bounds;
            var view = new Bounds(
                FarmView.OpenSeaCenter,
                new Vector3(IsoCameraRig.MaxZoom * 2f * (32f / 9f), IsoCameraRig.MaxZoom * 2f, 0f));
            Assert.IsTrue(water.Contains(new Vector3(view.min.x, view.min.y, water.center.z)));
            Assert.IsTrue(water.Contains(new Vector3(view.max.x, view.max.y, water.center.z)));
            foreach (var shore in new[] { "BeachLand", "BeachSea", "BeachSurf", "BeachShore" })
            {
                Assert.Less(view.max.y, GameObject.Find(shore).GetComponent<SpriteRenderer>().bounds.min.y, $"{shore} shows from the open sea.");
            }

            // The dock isn't out here, and the button goes back to the same spot on the sand.
            input.HandleWorldTap(camera.WorldToScreenPoint(FarmView.OpenSeaCenter));
            Assert.IsTrue(ui.IsAtSea);
            travel.onClick.Invoke();
            yield return null;
            Assert.IsTrue(ui.IsAtBeach);
            Assert.IsFalse(ui.IsAtSea);
            Assert.AreEqual("Farm", caption.text);
            Assert.AreEqual(beachPosition.x, camera.transform.position.x, 0.001f);
            Assert.AreEqual(beachPosition.y, camera.transform.position.y, 0.001f);

            // A level-up's Shop button at sea goes all the way home.
            ui.GoToOpenSea();
            ui.ShowLevelUp(4);
            GameObject.Find("LevelUpShopButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(ui.IsAtSea);
            Assert.IsFalse(ui.IsAtBeach);
            Assert.IsTrue(shopButton.activeSelf);
            Assert.AreEqual(farmPosition.x, camera.transform.position.x, 0.001f);
            Assert.AreEqual(farmPosition.y, camera.transform.position.y, 0.001f);
            yield return null;
        }

        private static bool PlaceBuilding(
            GameUI ui,
            FarmView farmView,
            Camera camera,
            string buildingId,
            int cellX,
            int cellY)
        {
            ui.BeginBuildingPlacement(buildingId);
            if (!ui.IsBuildingPlacementActive)
            {
                return false;
            }

            // Cells are given from the corner of the start area.
            var start = Resources.Load<ContentDatabase>(GameBootstrap.ContentResourcePath).Layout.StartArea.min;
            var screen = camera.WorldToScreenPoint(
                farmView.GetCellWorldPosition(start + new Vector2Int(cellX, cellY)));
            return screen.z > 0f && ui.TryPlaceBuildingAtScreen(screen);
        }

        /// <summary>Taps an animal to pop up the feed, then taps the feed.</summary>
        private static void TapFeedOn(Transform animal)
        {
            animal.GetComponent<Button>().onClick.Invoke();
            var feedTray = animal.parent.parent.Find("AnimalFeedTray");
            ExecuteEvents.Execute(feedTray.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }

        private static Transform FindActiveAnimal(Transform stage, string namePrefix)
        {
            var animalsRoot = stage.Find("AnimalPenAnimals");
            if (animalsRoot == null)
            {
                return null;
            }

            for (var i = 0; i < animalsRoot.childCount; i++)
            {
                var child = animalsRoot.GetChild(i);
                if (child.gameObject.activeSelf && child.name.StartsWith(namePrefix))
                {
                    return child;
                }
            }

            return null;
        }

        private static int CountActiveAnimals(Transform stage, string namePrefix)
        {
            var animalsRoot = stage.Find("AnimalPenAnimals");
            if (animalsRoot == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < animalsRoot.childCount; i++)
            {
                var child = animalsRoot.GetChild(i);
                if (child.gameObject.activeSelf && child.name.StartsWith(namePrefix))
                {
                    count++;
                }
            }

            return count;
        }

        private static PointerEventData CreatePointer(Camera camera, FarmView farmView, int plotId)
        {
            var screenPosition = camera.WorldToScreenPoint(farmView.GetPlotWorldPosition(plotId));
            Assert.Greater(screenPosition.z, 0f);
            return new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameBootstrap.SaveDirectoryOverride = null;

            if (_bootstrapObject != null)
            {
                Object.Destroy(_bootstrapObject);
            }

            foreach (var objectName in new[] { "Farm World", "Game UI", "Game Input", "EventSystem" })
            {
                var sceneObject = GameObject.Find(objectName);
                if (sceneObject != null)
                {
                    Object.Destroy(sceneObject);
                }
            }

            if (Camera.main != null)
            {
                Object.Destroy(Camera.main.gameObject);
            }

            yield return null;

            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }
    }
}
