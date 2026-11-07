using System.Collections;
using System.Collections.Generic;
using System.IO;
using FarmGame.Bootstrap;
using FarmGame.Input;
using FarmGame.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FarmGame.Tests
{
    /// <summary>The Fruit Plants shop tab, and tapping a tree on the farm to pick its fruit.</summary>
    public sealed class FruitTreePlayModeTests
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

        [UnityTest]
        public IEnumerator FruitPlantsTab_ListsEveryTree_InUnlockOrder()
        {
            GameObject.Find("BuildingShopButton").GetComponent<Button>().onClick.Invoke();
            var panel = GameObject.Find("BuildingShopOverlay").transform.Find("BuildingShopPanel");
            panel.Find("FruitTab").GetComponent<Button>().onClick.Invoke();
            yield return null;

            var shown = new List<string>();
            var cards = GameObject.Find("BuildingCards").transform;
            for (var i = 0; i < cards.childCount; i++)
            {
                shown.Add(cards.GetChild(i).name);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "BuildingCard_apple_tree",
                    "BuildingCard_lemon_tree",
                    "BuildingCard_peach_tree",
                    "BuildingCard_orange_tree",
                    "BuildingCard_mango_tree",
                    "BuildingCard_coconut_palm"
                },
                shown);
        }

        [UnityTest]
        public IEnumerator TappingATree_PicksThreeFruit_AndItsPictureFollows()
        {
            var session = _bootstrapObject.GetComponent<GameBootstrap>().Session;
            var farmView = GameObject.Find("Farm World").GetComponent<FarmView>();
            var input = GameObject.Find("Game Input").GetComponent<FarmInputController>();
            var definition = session.Content.GetBuilding("apple_tree");

            while (session.Level.Level < definition.UnlockLevel)
            {
                session.Level.AddXp(100);
            }

            var cell = session.Content.Layout.StartArea.min + new Vector2Int(8, 8);
            var pictureName = $"Building_apple_tree_{cell.x}_{cell.y}";
            Assert.IsTrue(session.TryPlaceBuilding("apple_tree", cell, out var message), message);
            yield return null;

            var picture = GameObject.Find(pictureName).GetComponentInChildren<SpriteRenderer>();
            var tap = (Vector2)Camera.main.WorldToScreenPoint(farmView.GetCellWorldPosition(cell));
            Assert.AreEqual(definition.GrowingSprite, picture.sprite, "A new tree has no fruit yet.");
            Assert.AreNotEqual(definition.WorldSprite, picture.sprite);

            input.HandleWorldTap(tap);
            Assert.AreEqual(0, session.Inventory.GetCount("apple"), "Tapping too early picks nothing.");

            session.DebugAdvanceAll(definition.ProductionSeconds);
            yield return null;
            picture = GameObject.Find(pictureName).GetComponentInChildren<SpriteRenderer>();
            Assert.AreEqual(definition.WorldSprite, picture.sprite, "A ready tree shows its fruit.");

            input.HandleWorldTap(tap);
            Assert.AreEqual(3, session.Inventory.GetCount("apple"));
            yield return null;
            Assert.AreEqual(definition.GrowingSprite, picture.sprite, "A picked tree is bare again.");
        }
    }
}
