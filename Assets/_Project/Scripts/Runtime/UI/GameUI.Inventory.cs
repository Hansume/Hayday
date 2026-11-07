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
    /// <summary>The Inventory window: a scrolling grid of what the player owns, and selling.</summary>
    public sealed partial class GameUI
    {
        private Text _inventoryEmptyText;
        private int _inventoryTileCount;

        private void BuildInventory()
        {
            var panel = UIFactory.CreateWindow(
                _canvasRect,
                "InventoryOverlay",
                "InventoryPanel",
                "Inventory",
                new Vector2(1080f, 800f),
                CloseInventory,
                out _);
            _inventoryOverlay = panel.parent.gameObject;

            BuildStorageHeader(panel);

            var well = UIFactory.CreateWell("StorageWell", panel);
            UIFactory.Place(well.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1000f, 624f));

            // The grid scrolls: crops, animal goods and feeds no longer fit on one screen.
            var viewport = UIFactory.CreateImage("Viewport", panel, null, new Color(1f, 1f, 1f, 0f));
            viewport.raycastTarget = true;
            UIFactory.Place(viewport.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(980f, 604f));
            viewport.gameObject.AddComponent<RectMask2D>();

            _inventoryRows = UIFactory.CreateRect("Rows", viewport.transform);
            UIFactory.SetAnchored(
                _inventoryRows,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                Vector2.zero);

            var grid = _inventoryRows.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300f, 290f);
            grid.spacing = new Vector2(24f, 22f);
            grid.padding = new RectOffset(0, 0, 14, 14);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = _inventoryRows.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _inventoryScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _inventoryScroll.viewport = viewport.rectTransform;
            _inventoryScroll.content = _inventoryRows;
            _inventoryScroll.horizontal = false;
            _inventoryScroll.movementType = ScrollRect.MovementType.Clamped;
            _inventoryScroll.scrollSensitivity = 40f;

            _inventoryEmptyText = UIFactory.CreateText("Empty", panel, "Nothing yet", 36, UIFactory.InkSoft, TextAnchor.MiddleCenter);
            _inventoryEmptyText.fontStyle = FontStyle.Bold;
            UIFactory.Place(_inventoryEmptyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(900f, 60f));

            // A slim wooden scrollbar so it's clear there is more below.
            var track = UIFactory.CreateImage("Scrollbar", panel, UIFactory.RoundedSprite, new Color(0.36f, 0.22f, 0.1f, 0.25f));
            UIFactory.Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(484f, -160f), new Vector2(14f, 584f));
            var handle = UIFactory.CreateImage("Handle", track.transform, UIFactory.RoundedSprite, UIFactory.Wood);
            UIFactory.Stretch(handle.rectTransform);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            _inventoryScroll.verticalScrollbar = scrollbar;
            _inventoryScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // Built after the inventory so it draws on top of it.
            BuildSellDialog();
        }

        /// <summary>
        /// The storage row under the title: how full it is ("Storage 37 / 50" over a bar) and a
        /// gold button to buy the next upgrade.
        /// </summary>
        private void BuildStorageHeader(RectTransform panel)
        {
            var label = UIFactory.CreateText("StorageLabel", panel, "Storage", 30, UIFactory.Ink, TextAnchor.MiddleLeft);
            label.fontStyle = FontStyle.Bold;
            UIFactory.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(-370f, -64f), new Vector2(240f, 34f));

            _storageText = UIFactory.CreateText("StorageCount", panel, string.Empty, 30, UIFactory.Ink, TextAnchor.MiddleRight);
            _storageText.fontStyle = FontStyle.Bold;
            UIFactory.Place(_storageText.rectTransform, new Vector2(0.5f, 1f), new Vector2(-80f, -64f), new Vector2(300f, 34f));

            _storageFill = UIFactory.CreateProgressBar("StorageBar", panel, new Vector2(560f, 26f), UIFactory.Green);
            UIFactory.Place(_storageFill.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(-210f, -104f), new Vector2(560f, 26f));

            _storageUpgradeButton = UIFactory.CreateButton(
                "StorageUpgradeButton",
                panel,
                string.Empty,
                new Vector2(380f, 72f),
                28,
                ButtonStyle.Gold);
            UIFactory.Place(_storageUpgradeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(270f, -60f), new Vector2(380f, 72f));
            _storageUpgradeLabel = _storageUpgradeButton.GetComponentInChildren<Text>();
            _storageUpgradeLabel.rectTransform.offsetMin = new Vector2(62f, _storageUpgradeLabel.rectTransform.offsetMin.y);

            _storageUpgradeCoin = UIFactory.CreateIcon("CoinIcon", _storageUpgradeButton.transform, UIFactory.Icon("icon_coin"), new Vector2(46f, 46f));
            UIFactory.Place(_storageUpgradeCoin.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 3f), new Vector2(46f, 46f));

            _storageUpgradeButton.onClick.AddListener(UpgradeStorage);
        }

        private void RefreshStorageHeader()
        {
            if (_storageText == null)
            {
                return;
            }

            var storage = _session.Storage;
            _storageText.text = $"{UIFactory.FormatNumber(storage.Used)} / {UIFactory.FormatNumber(storage.Capacity)}";
            _storageText.color = storage.IsFull ? UIFactory.Berry : UIFactory.Ink;
            _storageFill.fillAmount = Mathf.Clamp01(storage.Fill);
            _storageFill.color = storage.IsFull ? UIFactory.Berry : storage.Fill >= 0.8f ? UIFactory.Gold : UIFactory.Green;

            if (storage.IsMaxed)
            {
                _storageUpgradeLabel.text = "Max size";
                _storageUpgradeCoin.enabled = false;
                _storageUpgradeButton.interactable = false;
                UIFactory.SetButtonStyle(_storageUpgradeButton, ButtonStyle.Muted);
                return;
            }

            var cost = storage.NextUpgradeCost;
            var affordable = _session.Wallet.Coins >= cost;
            _storageUpgradeLabel.text = $"{UIFactory.FormatNumber(cost)}   +{storage.NextUpgradeSpaces} spaces";
            _storageUpgradeCoin.enabled = true;
            _storageUpgradeButton.interactable = true;
            UIFactory.SetButtonStyle(_storageUpgradeButton, affordable ? ButtonStyle.Gold : ButtonStyle.Danger);
        }

        private void UpgradeStorage()
        {
            _session.TryUpgradeStorage(out var message);
            ShowToast(message);
            RefreshStorageHeader();
        }

        private void OpenInventory()
        {
            if (_inventoryOverlay == null)
            {
                return;
            }

            CloseBuildingShop();
            CancelBuildingPlacement();
            HideCropShop();
            HideScythe();
            CloseFeedGrinder();
            CloseAnimalPen();
            RebuildInventoryRows();
            _inventoryOverlay.SetActive(true);
            _inventoryScroll.verticalNormalizedPosition = 1f;
        }

        private void CloseInventory()
        {
            CloseSellDialog();
            if (_inventoryOverlay != null)
            {
                _inventoryOverlay.SetActive(false);
            }
        }

        private void RebuildInventoryRows()
        {
            if (_inventoryRows == null)
            {
                return;
            }

            for (var i = _inventoryRows.childCount - 1; i >= 0; i--)
            {
                var child = _inventoryRows.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            RefreshStorageHeader();
            _inventoryTileCount = 0;
            CreateDeedInventoryTile();

            var crops = _session.Content.Crops;
            for (var i = 0; i < crops.Count; i++)
            {
                var crop = crops[i];
                if (crop == null)
                {
                    continue;
                }

                CreateSellableTile(crop.Id, crop.Icon, crop.DisplayName, crop.SellPrice);
            }

            var products = _session.Content.Products;
            for (var i = 0; i < products.Count; i++)
            {
                var product = products[i];
                if (product != null)
                {
                    CreateSellableTile(product.Id, product.Icon, product.DisplayName, product.SellPrice);
                }
            }

            var recipes = _session.Content.FeedRecipes;
            for (var i = 0; i < recipes.Count; i++)
            {
                CreateFeedInventoryRow(recipes[i]);
            }

            _inventoryEmptyText.gameObject.SetActive(_inventoryTileCount == 0);
        }

        private void CreateFeedInventoryRow(FeedRecipeDefinition recipe)
        {
            var count = _session.Inventory.GetCount(recipe.Id);
            if (count <= 0)
            {
                return;
            }

            var tile = CreateInventoryTile($"Row_{recipe.Id}", LoadFeedIcon(recipe), recipe.DisplayName, count);

            // Feed can't be sold, so it has no Sell button.
            var usePill = UIFactory.CreateWell("UseFor", tile);
            UIFactory.Place(usePill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(256f, 66f));
            var useText = UIFactory.CreateText(
                "Detail",
                usePill.transform,
                "Not for sale",
                26,
                UIFactory.InkSoft,
                TextAnchor.MiddleCenter);
            useText.fontStyle = FontStyle.Bold;
            UIFactory.Stretch(useText.rectTransform, 8f, 2f, 8f, 2f);
        }

        /// <summary>A tile for something that can be sold: a crop or an animal product.</summary>
        private void CreateSellableTile(string itemId, Sprite icon, string displayName, int sellPrice)
        {
            var count = _session.Inventory.GetCount(itemId);
            if (count <= 0)
            {
                return;
            }

            var tile = CreateInventoryTile($"Row_{itemId}", icon, displayName, count);

            // Opens the sell dialog, where the player picks how many to sell.
            var sellButton = UIFactory.CreateButton(
                "SellButton",
                tile,
                $"Sell {UIFactory.FormatNumber(sellPrice)}",
                new Vector2(256f, 74f),
                32,
                ButtonStyle.Gold);
            UIFactory.Place(sellButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(256f, 74f));
            var label = sellButton.GetComponentInChildren<Text>();
            label.rectTransform.offsetMin = new Vector2(56f, label.rectTransform.offsetMin.y);
            var coin = UIFactory.CreateIcon("CoinIcon", sellButton.transform, UIFactory.Icon("icon_coin"), new Vector2(46f, 46f));
            UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 3f), new Vector2(46f, 46f));

            sellButton.onClick.AddListener(() => OpenSellDialog(itemId));
        }

        /// <summary>An inventory tile: a card with the item in a recessed slot, a count badge and its name.</summary>
        private RectTransform CreateInventoryTile(string name, Sprite iconSprite, string title, int count)
        {
            _inventoryTileCount++;
            var tile = UIFactory.CreateCard(name, _inventoryRows);

            var well = UIFactory.CreateWell("IconWell", tile.transform);
            UIFactory.Place(well.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(170f, 140f));
            var icon = UIFactory.CreateIcon("Icon", well.transform, iconSprite, new Vector2(118f, 118f));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118f, 118f));

            UIFactory.CreatePill("Count", tile.transform, $"x{UIFactory.FormatNumber(count)}", 28, new Vector2(96f, 46f), out var countPill);
            UIFactory.Place(countPill.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -12f), new Vector2(96f, 46f));

            var nameText = UIFactory.CreateText("Name", tile.transform, title, 30, UIFactory.Ink, TextAnchor.MiddleCenter);
            nameText.fontStyle = FontStyle.Bold;
            UIFactory.Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -162f), new Vector2(280f, 38f));
            return tile.rectTransform;
        }
    }
}
