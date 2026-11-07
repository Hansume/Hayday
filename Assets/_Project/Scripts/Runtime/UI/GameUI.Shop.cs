using System.Collections;
using System.Collections.Generic;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Farming;
using FarmGame.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>The Shop window: Fields / Animals / Feed / Goods / Fruit Plants tabs and the item cards.</summary>
    public sealed partial class GameUI
    {
        private void BuildBuildingShop()
        {
            var panel = UIFactory.CreateWindow(
                _canvasRect,
                "BuildingShopOverlay",
                "BuildingShopPanel",
                "Shop",
                new Vector2(1200f, 720f),
                CloseBuildingShop,
                out _);
            _buildingShopOverlay = panel.parent.gameObject;

            // Folder tabs sit on top of the recessed card area.
            _slotTabButton = CreateShopTab(panel, "SlotTab", "Fields", -448f, BuildingShopTab.Slot);
            _animalsTabButton = CreateShopTab(panel, "AnimalsTab", "Animals", -224f, BuildingShopTab.Animals);
            _productionTabButton = CreateShopTab(panel, "ProductionTab", "Feed", 0f, BuildingShopTab.Production);
            _goodsTabButton = CreateShopTab(panel, "GoodsTab", "Goods", 224f, BuildingShopTab.Goods);
            _fruitTabButton = CreateShopTab(panel, "FruitTab", "Fruit Plants", 448f, BuildingShopTab.Fruit);

            var well = UIFactory.CreateWell("CardsWell", panel);
            UIFactory.Place(well.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(1120f, 540f));

            // Cards scroll sideways when a tab has more than fit (four animal buildings, say).
            _buildingShopCardsRoot = UIFactory.CreateHorizontalScroll(
                "BuildingCardsViewport",
                panel,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -156f),
                new Vector2(1090f, 520f),
                26f,
                out _buildingShopScroll);
            _buildingShopCardsRoot.name = "BuildingCards";

            UpdateBuildingShopTabVisuals();
        }

        private Button CreateShopTab(RectTransform panel, string name, string label, float x, BuildingShopTab tab)
        {
            var button = UIFactory.CreateButton(name, panel, label, new Vector2(216f, 70f), 32, ButtonStyle.Secondary);
            var image = button.GetComponent<Image>();
            image.sprite = UIFactory.TabSprite;
            UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(x, -80f), new Vector2(216f, 70f));
            button.onClick.AddListener(() => SetBuildingShopTab(tab));
            return button;
        }

        private void SetBuildingShopTab(BuildingShopTab tab)
        {
            if (_selectedBuildingShopTab == tab)
            {
                return;
            }

            _selectedBuildingShopTab = tab;
            UpdateBuildingShopTabVisuals();
            RefreshBuildingShop();
        }

        private void UpdateBuildingShopTabVisuals()
        {
            UpdateBuildingShopTabButton(_slotTabButton, _selectedBuildingShopTab == BuildingShopTab.Slot);
            UpdateBuildingShopTabButton(_animalsTabButton, _selectedBuildingShopTab == BuildingShopTab.Animals);
            UpdateBuildingShopTabButton(
                _productionTabButton,
                _selectedBuildingShopTab == BuildingShopTab.Production);
            UpdateBuildingShopTabButton(_goodsTabButton, _selectedBuildingShopTab == BuildingShopTab.Goods);
            UpdateBuildingShopTabButton(_fruitTabButton, _selectedBuildingShopTab == BuildingShopTab.Fruit);
        }

        private static void UpdateBuildingShopTabButton(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            // The selected tab takes the colour of the card area so it looks joined to it.
            UIFactory.SetButtonStyle(button, selected ? ButtonStyle.Neutral : ButtonStyle.Secondary);
            if (selected)
            {
                button.GetComponent<Image>().color = new Color(0.93f, 0.84f, 0.66f, 1f);
            }
        }

        private void OpenBuildingShop()
        {
            OpenBuildingShop(BuildingShopTab.Slot);
        }

        private void OpenBuildingShop(BuildingShopTab tab)
        {
            if (_buildingShopOverlay == null)
            {
                return;
            }

            // The level-up window's Shop button can be tapped at the beach; what's bought goes on the farm.
            ReturnToFarm();
            CloseInventory();
            CancelBuildingPlacement();
            HideCropShop();
            HideScythe();
            CloseFeedGrinder();
            CloseAnimalPen();
            _selectedBuildingShopTab = tab;
            UpdateBuildingShopTabVisuals();
            RefreshBuildingShop();
            _buildingShopOverlay.SetActive(true);
        }

        private void CloseBuildingShop()
        {
            if (_buildingShopOverlay != null)
            {
                _buildingShopOverlay.SetActive(false);
            }
        }

        private void RefreshBuildingShop()
        {
            if (_buildingShopCardsRoot == null)
            {
                return;
            }

            for (var i = _buildingShopCardsRoot.childCount - 1; i >= 0; i--)
            {
                var child = _buildingShopCardsRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            _buildingShopCards.Clear();
            if (_buildingShopScroll != null)
            {
                _buildingShopScroll.horizontalNormalizedPosition = 0f;
            }

            if (_selectedBuildingShopTab == BuildingShopTab.Slot)
            {
                CreateSlotCard(_buildingShopCardsRoot);
                return;
            }

            var buildings = _session.Content.Buildings;
            BuildingCategory category;
            switch (_selectedBuildingShopTab)
            {
                case BuildingShopTab.Animals:
                    category = BuildingCategory.Animals;
                    break;
                case BuildingShopTab.Goods:
                    category = BuildingCategory.Goods;
                    break;
                case BuildingShopTab.Fruit:
                    category = BuildingCategory.Fruit;
                    break;
                default:
                    category = BuildingCategory.Production;
                    break;
            }

            // In unlock order, so what the player can buy comes before what's still locked.
            var shown = new List<BuildingDefinition>();
            for (var i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && buildings[i].Category == category)
                {
                    shown.Add(buildings[i]);
                }
            }

            foreach (var building in System.Linq.Enumerable.OrderBy(shown, b => b.UnlockLevel))
            {
                _buildingShopCards.Add(CreateBuildingCard(building, _buildingShopCardsRoot));
            }
        }

        private void CreateSlotCard(Transform parent)
        {
            var buyButton = CreateShopCard(
                "SlotCard_new_slot",
                parent,
                FarmGame.View.FarmView.LoadSoilSprite(),
                "New Field",
                $"x{_session.Farm.PlotCount}",
                GameSession.NewSlotCost,
                "SlotBuyButton",
                out _);
            buyButton.onClick.AddListener(BeginSlotPlacement);
        }

        /// <summary>
        /// A shop card: a big picture with a count badge, the name and a price button (red if too dear).
        /// The price button is a direct child of the card named <paramref name="buttonName"/>.
        /// </summary>
        private Button CreateShopCard(
            string cardName,
            Transform parent,
            Sprite iconSprite,
            string title,
            string ownedChip,
            int cost,
            string buttonName,
            out Text ownedText)
        {
            var card = UIFactory.CreateCard(cardName, parent);
            var cardElement = card.gameObject.AddComponent<LayoutElement>();
            cardElement.preferredWidth = 330f;
            cardElement.preferredHeight = 480f;
            cardElement.flexibleWidth = 0f;

            var spotlight = UIFactory.CreateWell("Spotlight", card.transform);
            UIFactory.Place(spotlight.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(294f, 270f));
            var glow = UIFactory.CreateDecor("Glow", spotlight.transform, UIFactory.CircleSprite, new Color(1f, 0.98f, 0.86f, 0.75f));
            UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 240f));
            var icon = UIFactory.CreateIcon("Icon", spotlight.transform, iconSprite, new Vector2(240f, 220f));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 220f));

            var nameText = UIFactory.CreateText("Name", card.transform, title, 36, UIFactory.Ink, TextAnchor.MiddleCenter);
            nameText.fontStyle = FontStyle.Bold;
            UIFactory.Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -298f), new Vector2(310f, 50f));

            ownedText = UIFactory.CreatePill("Owned", card.transform, ownedChip, 28, new Vector2(96f, 50f), out var ownedPill);
            UIFactory.Place(ownedPill.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -12f), new Vector2(96f, 50f));

            var affordable = _session.Wallet.Coins >= cost;
            var buyButton = UIFactory.CreateButton(
                buttonName,
                card.transform,
                UIFactory.FormatNumber(cost),
                new Vector2(272f, 88f),
                36,
                affordable ? ButtonStyle.Primary : ButtonStyle.Danger);
            UIFactory.Place(buyButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(272f, 88f));
            var label = buyButton.GetComponentInChildren<Text>();
            label.rectTransform.offsetMin = new Vector2(64f, label.rectTransform.offsetMin.y);

            var coin = UIFactory.CreateIcon("CoinIcon", buyButton.transform, UIFactory.Icon("icon_coin"), new Vector2(52f, 52f));
            UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 4f), new Vector2(52f, 52f));
            return buyButton;
        }

        private BuildingCard CreateBuildingCard(BuildingDefinition building, Transform parent)
        {
            var buyButton = CreateShopCard(
                $"BuildingCard_{building.Id}",
                parent,
                building.Icon,
                building.DisplayName,
                $"x{_session.Buildings.GetCount(building.Id)}",
                building.Cost,
                "BuyButton",
                out var ownedText);

            if (!_session.IsUnlocked(building))
            {
                // Locked: the price button becomes a "Level N" plate.
                UIFactory.SetButtonStyle(buyButton, ButtonStyle.Muted);
                buyButton.interactable = false;
                var lockLabel = buyButton.GetComponentInChildren<Text>();
                lockLabel.text = $"Level {building.UnlockLevel}";
                lockLabel.rectTransform.offsetMin = new Vector2(0f, lockLabel.rectTransform.offsetMin.y);
                var coin = buyButton.transform.Find("CoinIcon");
                if (coin != null)
                {
                    coin.gameObject.SetActive(false);
                }

                var group = buyButton.transform.parent.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0.7f;
            }

            var buildingId = building.Id;
            buyButton.onClick.AddListener(() => BeginBuildingPlacement(buildingId));

            return new BuildingCard
            {
                Building = building,
                Owned = ownedText,
                BuyButton = buyButton,
                BuyLabel = buyButton.GetComponentInChildren<Text>()
            };
        }
    }
}
