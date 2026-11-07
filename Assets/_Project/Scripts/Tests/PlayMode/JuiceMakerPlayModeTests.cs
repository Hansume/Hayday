using System.Collections;
using System.IO;
using FarmGame.Bootstrap;
using FarmGame.Core;
using FarmGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmGame.Tests
{
    /// <summary>The Juice Maker in the game: its recipe cards, and making a juice.</summary>
    public sealed class JuiceMakerPlayModeTests
    {
        private string _directory;
        private GameObject _bootstrapObject;
        private GameSession _session;
        private GameUI _ui;

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
            _ui = GameObject.Find("Game UI").GetComponent<GameUI>();
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

        private Buildings.PlacedBuilding PlaceMaker()
        {
            var maker = _session.Content.GetBuilding("juice_maker");
            var origin = _session.Content.Layout.StartArea.min + new Vector2Int(8, 8);
            Assert.IsTrue(_session.Buildings.TryPlace(maker, origin, _session.Wallet, out var placed, out var reason), reason);
            return placed;
        }

        [UnityTest]
        public IEnumerator ThreeIngredients_StayClearOfTheMakeButton()
        {
            _ui.ShowFeedGrinder(PlaceMaker());
            yield return null;

            var card = GameObject.Find("GoodsRow_coconut_smoothie");
            Assert.IsNotNull(card, "Every juice should have a card.");
            Assert.IsNotNull(card.transform.Find("Input_coconut"));
            Assert.IsNotNull(card.transform.Find("Input_ice_cream"));
            Assert.IsNotNull(card.transform.Find("Input_strawberry"));

            var corners = new Vector3[4];
            ((RectTransform)card.transform.Find("InputCount_strawberry")).GetWorldCorners(corners);
            var chipRight = corners[2].x;
            ((RectTransform)card.transform.Find("MakeButton")).GetWorldCorners(corners);
            Assert.LessOrEqual(chipRight, corners[0].x, "The last ingredient runs under the Make button.");
        }

        [UnityTest]
        public IEnumerator EveryJuice_HasItsOwnArt()
        {
            yield return null;
            foreach (var id in new[] { "apple_juice", "lemonade", "peach_smoothie", "orange_juice", "mango_lassi", "coconut_smoothie" })
            {
                Assert.AreEqual($"icon_{id}", _session.Content.GetItemIcon(id).name);
            }

            Assert.AreEqual("building_juice_maker", _session.Content.GetBuilding("juice_maker").WorldSprite.name);
        }

        [UnityTest]
        public IEnumerator MakingAJuice_UsesItsIngredients()
        {
            while (_session.Level.Level < 3)
            {
                _session.Level.AddXp(10);
            }

            var maker = PlaceMaker();
            _session.Inventory.Add("apple", 1);
            _session.Inventory.Add("carrot", 1);
            yield return null;

            Assert.IsTrue(_session.TryStartGoods(maker, "apple_juice", out var message), message);
            Assert.AreEqual(0, _session.Inventory.GetCount("apple"));
            Assert.AreEqual(0, _session.Inventory.GetCount("carrot"));

            _session.DebugAdvanceAll(240d);
            Assert.AreEqual(1, _session.CollectReadyGoods(maker, out _));
            Assert.AreEqual(1, _session.Inventory.GetCount("apple_juice"));
        }
    }
}
