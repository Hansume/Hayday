using System.Collections;
using System.IO;
using FarmGame.Bootstrap;
using FarmGame.Core;
using FarmGame.Input;
using FarmGame.UI;
using FarmGame.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FarmGame.Tests
{
    /// <summary>The 36 x 36 map on screen: tapping locked land, buying it, and what changes when it's bought.</summary>
    public sealed class LandPlayModeTests
    {
        // The section just left of the start area, and a cell in it.
        private static readonly Vector2Int LeftSection = new Vector2Int(5, 6);
        private static readonly Vector2Int LeftCell = new Vector2Int(11, 12);

        // A section in front of the start area, in the row where visitors stand.
        private static readonly Vector2Int FrontSection = new Vector2Int(8, 12);

        private string _directory;
        private GameObject _bootstrapObject;
        private GameSession _session;
        private FarmView _farmView;
        private GameUI _ui;
        private FarmInputController _input;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "FarmGamePlayModeTests", System.Guid.NewGuid().ToString("N"));
            GameBootstrap.SaveDirectoryOverride = _directory;

            _bootstrapObject = new GameObject("Bootstrap Test");
            _bootstrapObject.SetActive(false);
            var bootstrap = _bootstrapObject.AddComponent<GameBootstrap>();
            bootstrap.SaveFileName = "test-save.json";
            bootstrap.LoadSavedGame = false;
            _bootstrapObject.SetActive(true);
            yield return null;

            _session = bootstrap.Session;
            _farmView = GameObject.Find("Farm World").GetComponent<FarmView>();
            _ui = GameObject.Find("Game UI").GetComponent<GameUI>();
            _input = GameObject.Find("Game Input").GetComponent<FarmInputController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameBootstrap.SaveDirectoryOverride = null;
            Object.Destroy(_bootstrapObject);
            foreach (var root in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (root.transform.parent == null)
                {
                    Object.Destroy(root);
                }
            }

            yield return null;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        private void Tap(Vector2Int cell)
        {
            _input.HandleWorldTap(Camera.main.WorldToScreenPoint(_farmView.GetCellWorldPosition(cell)));
        }

        [UnityTest]
        public IEnumerator Map_Is36By36_CentredOnTheStartArea()
        {
            Assert.AreEqual(36, _farmView.Grid.Width);
            Assert.AreEqual(36, _farmView.Grid.Height);
            Assert.AreEqual(new RectInt(12, 12, 12, 12), _session.Content.Layout.StartArea);
            Assert.AreEqual(2, _session.Land.SectionSize);
            Assert.IsNotNull(GameObject.Find("Grass_35_35"));

            var camera = Camera.main.transform.position;
            Assert.AreEqual(0f, camera.x, 0.001f, "The camera starts on the start area.");
            Assert.AreEqual(0f, camera.y, 0.001f);
            Assert.IsTrue(_session.Land.IsCellOwned(_session.NoticeBoard.Origin), "The board stands on owned land.");
            Assert.IsTrue(_session.Land.IsCellOwned(_session.Farm.PlotCells[0]), "The first fields are on owned land.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeedIcon_IsHiddenUntilLockedLandIsTapped()
        {
            Assert.IsFalse(_farmView.IsLandSignVisible, "Nothing shows on a new farm.");

            Tap(LeftCell);
            yield return null;

            Assert.IsTrue(_farmView.IsLandSignVisible);
            Assert.AreEqual(LeftSection, _farmView.LandSignSection);
            Assert.IsFalse(_ui.IsLandWindowVisible, "The first tap only shows the icon.");
            Assert.IsNotNull(GameObject.Find("LandMark_1_1"), "The 2 x 2 cells are marked.");
        }

        [UnityTest]
        public IEnumerator DeedIcon_PopsUpToItsFullSize()
        {
            Tap(LeftCell);
            for (var i = 0; i < 40; i++)
            {
                yield return null;
            }

            Assert.Greater(GameObject.Find("LandSign").transform.localScale.x, 0.5f);
        }

        [UnityTest]
        public IEnumerator DeedIcon_GoesAwayOnATapElsewhere()
        {
            Tap(LeftCell);
            yield return null;

            Tap(new Vector2Int(12, 12));
            yield return null;

            Assert.IsFalse(_farmView.IsLandSignVisible);
            Assert.IsNull(GameObject.Find("LandMark_1_1"));
            Assert.IsFalse(_ui.IsLandWindowVisible);
        }

        [UnityTest]
        public IEnumerator DeedIcon_MovesToAnotherSectionWhenTapped()
        {
            Tap(LeftCell);
            yield return null;

            Tap(new Vector2Int(12, 11));
            yield return null;

            Assert.IsTrue(_farmView.IsLandSignVisible);
            Assert.AreEqual(new Vector2Int(6, 5), _farmView.LandSignSection);
        }

        [UnityTest]
        public IEnumerator TappingTheIcon_WithoutADeed_CantBuyIt()
        {
            Tap(LeftCell);
            yield return null;
            Tap(LeftCell);
            yield return null;

            Assert.IsTrue(_ui.IsLandWindowVisible);
            Assert.IsFalse(GameObject.Find("BuyLandButton").GetComponent<Button>().interactable);
        }

        [UnityTest]
        public IEnumerator TappingFarOffLand_ShowsNoIcon()
        {
            Tap(new Vector2Int(0, 0));
            yield return null;

            Assert.IsFalse(_farmView.IsLandSignVisible);
            Assert.IsFalse(_ui.IsLandWindowVisible);
            StringAssert.Contains("Too far", GameObject.Find("ToastText").GetComponent<Text>().text);
        }

        [UnityTest]
        public IEnumerator TappingOwnedGrass_ShowsNothing()
        {
            Tap(new Vector2Int(12, 12));
            yield return null;

            Assert.IsFalse(_farmView.IsLandSignVisible);
            Assert.IsFalse(_ui.IsLandWindowVisible);
        }

        [UnityTest]
        public IEnumerator BuyingLand_BrightensItAndHidesTheIcon()
        {
            var lockedGrass = GameObject.Find($"Grass_{LeftCell.x}_{LeftCell.y}").GetComponent<SpriteRenderer>();
            var otherCorner = GameObject.Find("Grass_10_13").GetComponent<SpriteRenderer>();
            var outside = GameObject.Find("Grass_9_12").GetComponent<SpriteRenderer>();
            var ownedGrass = GameObject.Find("Grass_12_12").GetComponent<SpriteRenderer>();
            Assert.Less(lockedGrass.color.r, ownedGrass.color.r, "Locked land is darker.");

            _session.Land.AddDeed();
            Tap(LeftCell);
            yield return null;
            Tap(LeftCell);
            yield return null;
            var buy = GameObject.Find("BuyLandButton").GetComponent<Button>();
            Assert.IsTrue(buy.interactable);
            buy.onClick.Invoke();
            yield return null;

            Assert.IsFalse(_ui.IsLandWindowVisible);
            Assert.IsFalse(_farmView.IsLandSignVisible);
            Assert.AreEqual(0, _session.Land.Deeds);
            Assert.IsTrue(_session.Land.IsOwned(LeftSection));
            Assert.AreEqual(ownedGrass.color.r, lockedGrass.color.r, 0.1f, "Bought land looks owned.");
            Assert.AreEqual(ownedGrass.color.r, otherCorner.color.r, 0.1f, "All of the 2 x 2 does.");
            Assert.Less(outside.color.r, ownedGrass.color.r, "The cell next to it is still locked.");
            Assert.IsTrue(_session.CanPlaceSlot(LeftCell, out var reason), reason);
        }

        [UnityTest]
        public IEnumerator BuyingLandInFront_MovesTheVisitorsForward()
        {
            var before = _farmView.GetVisitorSpotWorldPosition(0);
            _session.Land.AddDeed();
            Assert.IsTrue(_session.TryBuyLand(FrontSection, out var message), message);
            yield return null;

            Assert.Less(_farmView.GetVisitorSpotWorldPosition(0).y, before.y, "They stand further forward, off the new land.");
        }

        [UnityTest]
        public IEnumerator Inventory_ShowsDeedsOnlyWhenThereAreSome()
        {
            GameObject.Find("InventoryButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsNull(GameObject.Find("Row_land_deed"));
            _ui.GoToBeach();
            _ui.ReturnToFarm();

            _session.Land.AddDeed();
            GameObject.Find("InventoryButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsNotNull(GameObject.Find("Row_land_deed"));
        }

        [UnityTest]
        public IEnumerator GoingToTheBeach_ClosesTheIconAndWindow()
        {
            Tap(LeftCell);
            yield return null;
            Tap(LeftCell);
            yield return null;
            Assert.IsTrue(_ui.IsLandWindowVisible);

            _ui.GoToBeach();
            yield return null;
            Assert.IsFalse(_ui.IsLandWindowVisible);
            Assert.IsFalse(_farmView.IsLandSignVisible);
        }

        [UnityTest]
        public IEnumerator PlacingABuilding_ClosesTheIcon()
        {
            Tap(LeftCell);
            yield return null;
            _ui.BeginBuildingPlacement("fish_pond");
            yield return null;

            Assert.IsFalse(_farmView.IsLandSignVisible);
            _ui.CancelBuildingPlacement();
        }
    }
}
