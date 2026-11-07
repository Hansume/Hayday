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
    /// <summary>The Cocktail Bar on the beach: tapping it, the line in its window, and serving.</summary>
    public sealed class CocktailBarPlayModeTests
    {
        private string _directory;
        private GameObject _bootstrapObject;
        private GameSession _session;
        private GameUI _ui;
        private FarmView _farmView;
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
            _ui = GameObject.Find("Game UI").GetComponent<GameUI>();
            _farmView = GameObject.Find("Farm World").GetComponent<FarmView>();
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

        private void TapTheBar()
        {
            _input.HandleWorldTap(Camera.main.WorldToScreenPoint(_farmView.CocktailBarWorldPosition));
        }

        [UnityTest]
        public IEnumerator TheBar_IsOnTheBeachFromTheStart_AndNotInTheShop()
        {
            Assert.IsNotNull(GameObject.Find("CocktailBar"), "It spawns by itself.");
            Assert.IsNull(_session.Content.GetBuilding("cocktail_bar"), "It isn't a building the shop could sell.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TappingTheBar_ShowsTheBartenderAndTheLine()
        {
            _ui.GoToBeach();
            yield return null;

            TapTheBar();
            yield return null;

            Assert.IsTrue(_ui.IsCocktailBarVisible);
            Assert.IsNotNull(GameObject.Find("Bartender"));
            Assert.AreEqual(7, _session.CocktailBar.Settings.LineSize);
            for (var i = 0; i < _session.CocktailBar.Settings.LineSize; i++)
            {
                Assert.IsNotNull(GameObject.Find($"BarCustomer_{i}"), $"Customer {i} should be in line.");
            }
        }

        [UnityTest]
        public IEnumerator TheServeButton_ServesOnlyTheFrontCustomer()
        {
            _ui.GoToBeach();
            _session.Inventory.Add("apple_juice", 10);
            TapTheBar();
            yield return null;
            var size = _session.CocktailBar.Line.Count;

            var serve = GameObject.Find("BarServeButton").GetComponent<Button>();
            Assert.IsTrue(serve.interactable);
            serve.onClick.Invoke();
            yield return null;

            Assert.AreEqual(size - 1, _session.CocktailBar.Line.Count);
            Assert.AreEqual(9, _session.Inventory.GetCount("apple_juice"));
            Assert.IsNull(GameObject.Find($"BarCustomer_{size - 1}"), "The line gets one shorter on screen.");
        }

        [UnityTest]
        public IEnumerator WithoutTheJuice_ServeIsOff_AndThereIsNoSkip()
        {
            _ui.GoToBeach();
            TapTheBar();
            yield return null;

            Assert.IsFalse(GameObject.Find("BarServeButton").GetComponent<Button>().interactable);
            Assert.IsNull(GameObject.Find("BarSkipButton"));
        }

        [UnityTest]
        public IEnumerator WithTheLineServed_TheWindowStillOpens_AndSaysSo()
        {
            _ui.GoToBeach();
            yield return null;
            _session.Inventory.Add("apple_juice", 10);
            while (_session.CocktailBar.Front != null)
            {
                Assert.IsTrue(_session.TryServeBarCustomer(out var message), message);
            }

            TapTheBar();
            yield return null;

            Assert.IsTrue(_ui.IsCocktailBarVisible);
            Assert.AreEqual("No one here", GameObject.Find("BarEmpty").GetComponent<Text>().text);
            Assert.IsNull(GameObject.Find("BarServeButton"), "Nobody to serve.");
        }

        [UnityTest]
        public IEnumerator TheBeachButton_ShowsABadgeWhileALineWaits()
        {
            yield return null;
            Assert.IsNotNull(GameObject.Find("TravelBadge"));

            _ui.GoToBeach();
            yield return null;
            Assert.IsNull(GameObject.Find("TravelBadge"), "Already at the beach.");
        }

        [UnityTest]
        public IEnumerator TheFarm_DoesNotOpenTheBar()
        {
            _input.HandleWorldTap(Camera.main.WorldToScreenPoint(_farmView.GetCellWorldPosition(_session.Content.Layout.StartArea.min)));
            yield return null;

            Assert.IsFalse(_ui.IsCocktailBarVisible);
        }
    }
}
