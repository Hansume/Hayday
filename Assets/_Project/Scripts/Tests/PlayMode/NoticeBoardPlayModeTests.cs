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
    /// <summary>
    /// Clicking the notice board on the farm opens a window listing every notice on the board.
    /// Set FARM_SCREENSHOT_DIR to save a picture of the open window.
    /// </summary>
    public sealed class NoticeBoardPlayModeTests
    {
        private string _directory;
        private GameObject _bootstrapObject;

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
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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

        [UnityTest]
        public IEnumerator ClickingTheBoard_OpensAWindowListingEveryNotice()
        {
            var session = _bootstrapObject.GetComponent<GameBootstrap>().Session;
            var ui = GameObject.Find("Game UI").GetComponent<GameUI>();
            var farmView = GameObject.Find("Farm World").GetComponent<FarmView>();
            var input = GameObject.Find("Game Input").GetComponent<FarmInputController>();
            var camera = Camera.main;

            // The board is on the farm and on screen when the game starts.
            var board = session.NoticeBoard;
            Assert.IsNotNull(board, "Every farm should have a notice board.");
            var boardObject = GameObject.Find($"Building_{board.Definition.Id}_{board.Origin.x}_{board.Origin.y}");
            Assert.IsNotNull(boardObject, "The notice board isn't drawn on the farm.");
            var boardSprite = boardObject.GetComponentInChildren<SpriteRenderer>();
            var onScreen = camera.WorldToViewportPoint(boardSprite.bounds.center);
            Assert.That(
                onScreen.x > 0f && onScreen.x < 1f && onScreen.y > 0f && onScreen.y < 1f,
                $"The notice board is off screen at the start (viewport {onScreen}).");

            // Click the board itself (its noticeboard, well above the cell it stands on).
            var bounds = boardSprite.bounds;
            var boardFace = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.72f, bounds.center.z);
            Assert.IsFalse(
                farmView.TryGetCellAtWorld(boardFace, out var faceCell) && faceCell == board.Origin,
                "Test setup: the click should land above the board's own cell.");
            input.HandleWorldTap(camera.WorldToScreenPoint(boardFace));
            yield return null;
            Assert.IsTrue(ui.IsNoticeBoardVisible, "Clicking the notice board did not open its window.");

            // One note per slot, each showing its notice in full.
            var cork = GameObject.Find("NoticeBoardOverlay").transform.Find("NoticeBoardPanel/Cork");
            Assert.IsNotNull(cork);
            var missions = session.Missions;
            Assert.AreEqual(missions.SlotCount, missions.ActiveCount, "A new board should be full.");
            for (var i = 0; i < missions.SlotCount; i++)
            {
                var card = cork.Find($"Notice_{i}");
                Assert.IsNotNull(card, $"Notice {i} has no card.");
                var notice = missions.GetSlot(i);
                var content = card.Find("Content");
                Assert.IsTrue(content.gameObject.activeSelf, $"Notice {i} is hidden.");
                Assert.AreEqual(missions.GetTitle(notice), content.Find("Title").GetComponent<Text>().text);
                Assert.IsNull(content.Find("Timer"), "Orders have no time limit.");
                Assert.IsNull(content.Find("Clock"), "Orders have no time limit.");
                Assert.AreEqual(
                    UIFactory.FormatNumber(notice.RewardCoins),
                    content.Find("Coins").GetComponent<Text>().text);

                for (var r = 0; r < 3; r++)
                {
                    var row = content.Find($"Requirement_{r}");
                    var shown = r < notice.Requirements.Count;
                    Assert.AreEqual(shown, row.gameObject.activeSelf, $"Notice {i} requirement row {r}.");
                    if (!shown)
                    {
                        continue;
                    }

                    var requirement = notice.Requirements[r];
                    Assert.AreEqual(
                        session.Content.GetItemDisplayName(requirement.ItemId),
                        row.Find("Name").GetComponent<Text>().text);
                    Assert.AreEqual(
                        $"0 / {UIFactory.FormatNumber(requirement.Required)}",
                        row.Find("Progress/Count").GetComponent<Text>().text);
                    Assert.IsNotNull(row.Find("IconWell/Icon").GetComponent<Image>().sprite, $"Notice {i} row {r} has no icon.");
                }

                Assert.IsFalse(content.Find("GiveButton").GetComponent<Button>().interactable, "Nothing is in storage yet.");
                Assert.IsTrue(content.Find("DiscardButton").gameObject.activeSelf);
            }

            yield return SaveScreenshot("notice_board_open.png");

            // With the items in storage Give lights up; giving takes them, pays and empties the slot.
            var order = missions.GetSlot(0);
            foreach (var requirement in order.Requirements)
            {
                session.Inventory.Add(requirement.ItemId, requirement.Required);
            }

            ui.ShowNoticeBoard();
            var give = cork.Find("Notice_0/Content/GiveButton");
            Assert.IsTrue(give.GetComponent<Button>().interactable, "Give didn't light up with the items in storage.");
            var coins = session.Wallet.Coins;
            give.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(coins + order.RewardCoins, session.Wallet.Coins, "Giving didn't pay the reward.");
            foreach (var requirement in order.Requirements)
            {
                Assert.AreEqual(0, session.Inventory.GetCount(requirement.ItemId), "Giving didn't take the items.");
            }

            Assert.IsNull(missions.GetSlot(0));
            var empty = cork.Find("Notice_0/Empty");
            Assert.IsTrue(empty.gameObject.activeSelf);
            StringAssert.Contains("New order in", empty.GetComponent<Text>().text);

            // Discard tears a notice down.
            cork.Find("Notice_1/Content/DiscardButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsNull(missions.GetSlot(1));
            Assert.IsFalse(cork.Find("Notice_1/Content").gameObject.activeSelf);

            // Close dismisses the window.
            GameObject.Find("NoticeBoardOverlay").transform.Find("NoticeBoardPanel/CloseButton")
                .GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(ui.IsNoticeBoardVisible);
        }

        [UnityTest]
        public IEnumerator BubbleOverTheBoard_ShowsWhenANoticeIsReady()
        {
            var session = _bootstrapObject.GetComponent<GameBootstrap>().Session;
            var bubble = GameObject.Find("HUD Canvas").transform.Find("NoticeBubble");
            Assert.IsNotNull(bubble);
            yield return null;
            Assert.IsFalse(bubble.gameObject.activeSelf, "No order can be filled yet.");

            foreach (var requirement in session.Missions.GetSlot(2).Requirements)
            {
                session.Inventory.Add(requirement.ItemId, requirement.Required);
            }

            yield return null;
            Assert.IsTrue(bubble.gameObject.activeSelf, "The ! bubble should show over the board.");
        }

        private static IEnumerator SaveScreenshot(string fileName)
        {
            // WaitForEndOfFrame never fires in batch mode, so only capture in the editor.
            var directory = System.Environment.GetEnvironmentVariable("FARM_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(directory) || Application.isBatchMode)
            {
                yield break;
            }

            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            if (texture != null)
            {
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, fileName), texture.EncodeToPNG());
                Object.Destroy(texture);
            }
        }
    }
}
