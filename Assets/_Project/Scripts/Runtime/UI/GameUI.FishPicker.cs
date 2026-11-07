using System.Collections.Generic;
using FarmGame.Content;
using FarmGame.Farming;
using UnityEngine;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>
    /// The list of fish shown over a fish pond when the player taps "Buy Fish": every kind with
    /// its price, how long it takes to grow and what it sells for. Locked kinds show the level
    /// they unlock at.
    /// </summary>
    public sealed partial class GameUI
    {
        private GameObject _fishPickerRoot;
        private RectTransform _fishPickerCards;

        public bool IsFishPickerVisible => _fishPickerRoot != null && _fishPickerRoot.activeSelf;

        private void BuildFishPicker(RectTransform panel)
        {
            // Covers the whole pen window and swallows taps meant for the pond behind it.
            var root = UIFactory.CreateImage("FishPicker", panel, UIFactory.PanelSprite, UIFactory.Parchment);
            root.raycastTarget = true;
            UIFactory.Stretch(root.rectTransform, 16f, 16f, 16f, 16f);
            _fishPickerRoot = root.gameObject;

            var title = UIFactory.CreateText("Title", root.transform, "Buy a fish", 40, UIFactory.Ink, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(700f, 56f));

            var back = UIFactory.CreateButton("FishPickerBack", root.transform, "Back", new Vector2(150f, 64f), 30, ButtonStyle.Secondary);
            UIFactory.Place(back.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-96f, -50f), new Vector2(150f, 64f));
            back.onClick.AddListener(CloseFishPicker);

            _fishPickerCards = UIFactory.CreateRect("FishCards", root.transform);
            // Place() puts the pivot on the anchor, so this offset is to the grid's top edge:
            // just below the title, leaving room for two rows of cards inside the window.
            UIFactory.Place(_fishPickerCards, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(1040f, 680f));
            var grid = _fishPickerCards.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(196f, 330f);
            grid.spacing = new Vector2(12f, 16f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperCenter;

            _fishPickerRoot.SetActive(false);
        }

        private void OpenFishPicker()
        {
            var pond = _activeAnimalBuilding;
            if (_fishPickerRoot == null || pond == null || !pond.Definition.SellsAnimals)
            {
                return;
            }

            if (!pond.HasAnimalSpace)
            {
                ShowToast($"{pond.Definition.DisplayName} is full.");
                return;
            }

            DestroyChildren(_fishPickerCards);
            var fish = _session.Content.Fish;
            for (var i = 0; i < fish.Count; i++)
            {
                if (fish[i] != null)
                {
                    CreateFishCard(fish[i]);
                }
            }

            _fishPickerRoot.SetActive(true);
            _fishPickerRoot.transform.SetAsLastSibling();
        }

        private void CloseFishPicker()
        {
            if (_fishPickerRoot != null)
            {
                _fishPickerRoot.SetActive(false);
            }
        }

        private void CreateFishCard(FishDefinition fish)
        {
            var card = UIFactory.CreateCard($"FishCard_{fish.Id}", _fishPickerCards);

            var spotlight = UIFactory.CreateWell("Spotlight", card.transform);
            UIFactory.Place(spotlight.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(172f, 112f));
            var icon = UIFactory.CreateIcon("Icon", spotlight.transform, fish.Sprite, new Vector2(150f, 104f));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 104f));

            var nameText = UIFactory.CreateText("Name", card.transform, fish.DisplayName, 30, UIFactory.Ink, TextAnchor.MiddleCenter);
            nameText.fontStyle = FontStyle.Bold;
            UIFactory.Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -144f), new Vector2(184f, 34f));

            // What it sells for and how long it takes, as icon + number.
            CreateFishStat(card.transform, "Sells", "icon_coin", UIFactory.FormatNumber(fish.SellPrice), -180f);
            CreateFishStat(card.transform, "Grows", "icon_clock", FarmSimulation.FormatTime(fish.GrowSeconds), -216f);

            var unlocked = _session.IsUnlocked(fish.UnlockLevel);
            var affordable = _session.Wallet.Coins >= fish.Cost;
            var buy = UIFactory.CreateButton(
                "BuyFishButton",
                card.transform,
                !unlocked ? $"Level {fish.UnlockLevel}" : UIFactory.FormatNumber(fish.Cost),
                new Vector2(170f, 62f),
                30,
                !unlocked ? ButtonStyle.Muted : affordable ? ButtonStyle.Primary : ButtonStyle.Danger);
            UIFactory.Place(buy.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(170f, 62f));

            if (unlocked)
            {
                var label = buy.GetComponentInChildren<Text>();
                label.rectTransform.offsetMin = new Vector2(48f, label.rectTransform.offsetMin.y);
                var coin = UIFactory.CreateIcon("CoinIcon", buy.transform, UIFactory.Icon("icon_coin"), new Vector2(40f, 40f));
                UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 3f), new Vector2(40f, 40f));
                var fishId = fish.Id;
                buy.onClick.AddListener(() => BuyFish(fishId));
            }
            else
            {
                buy.interactable = false;
                card.gameObject.AddComponent<CanvasGroup>().alpha = 0.7f;
            }
        }

        private static void CreateFishStat(Transform card, string name, string iconName, string value, float y)
        {
            var icon = UIFactory.CreateIcon($"{name}Icon", card, UIFactory.Icon(iconName), new Vector2(32f, 32f));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(-54f, y), new Vector2(32f, 32f));
            var text = UIFactory.CreateText(name, card, value, 26, UIFactory.InkSoft, TextAnchor.MiddleLeft);
            text.fontStyle = FontStyle.Bold;
            UIFactory.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(26f, y), new Vector2(116f, 32f));
        }

        private void BuyFish(string fishId)
        {
            var pond = _activeAnimalBuilding;
            if (pond == null)
            {
                CloseFishPicker();
                return;
            }

            if (_session.TryAddFish(pond, fishId, out var message))
            {
                CloseFishPicker();
                RebuildAnimalPenVisuals();
                UpdateAnimalPenControls();
            }

            ShowToast(message);
        }
    }
}
