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
    /// <summary>
    /// The production window: recipe cards and the production queue. Used by the Feed Grinder
    /// and by every goods building (Dairy, Bakery, ...); the title shows which.
    /// </summary>
    public sealed partial class GameUI
    {
        private Text _feedGrinderTitle;

        private bool ActiveIsGoodsBuilding => _session.IsGoodsBuilding(_activeGrinder);

        private void BuildFeedGrinder()
        {
            var panel = UIFactory.CreateWindow(
                _canvasRect,
                "FeedGrinderOverlay",
                "FeedGrinderPanel",
                "Feed Grinder",
                new Vector2(1200f, 790f),
                CloseFeedGrinder,
                out _feedGrinderTitle);
            _feedGrinderOverlay = panel.parent.gameObject;

            // Left column: one card per recipe, scrolling when they don't all fit.
            var viewport = UIFactory.CreateImage("FeedRowsViewport", panel, null, new Color(1f, 1f, 1f, 0f));
            viewport.raycastTarget = true;
            UIFactory.Place(viewport.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -76f), new Vector2(680f, 684f));
            viewport.gameObject.AddComponent<RectMask2D>();

            _feedGrinderRows = UIFactory.CreateRect("FeedRows", viewport.transform);
            UIFactory.SetAnchored(
                _feedGrinderRows,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                Vector2.zero);

            var rowsLayout = _feedGrinderRows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 14f;
            rowsLayout.childAlignment = TextAnchor.UpperCenter;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;
            var rowsFitter = _feedGrinderRows.gameObject.AddComponent<ContentSizeFitter>();
            rowsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _feedGrinderScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _feedGrinderScroll.viewport = viewport.rectTransform;
            _feedGrinderScroll.content = _feedGrinderRows;
            _feedGrinderScroll.horizontal = false;
            _feedGrinderScroll.movementType = ScrollRect.MovementType.Clamped;
            _feedGrinderScroll.scrollSensitivity = 40f;

            // A slim wooden scrollbar so it's clear there is more below.
            var track = UIFactory.CreateImage("FeedRowsScrollbar", panel, UIFactory.RoundedSprite, new Color(0.36f, 0.22f, 0.1f, 0.25f));
            UIFactory.Place(track.rectTransform, new Vector2(0f, 1f), new Vector2(723f, -84f), new Vector2(14f, 668f));
            var handle = UIFactory.CreateImage("Handle", track.transform, UIFactory.RoundedSprite, UIFactory.Wood);
            UIFactory.Stretch(handle.rectTransform);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            _feedGrinderScroll.verticalScrollbar = scrollbar;
            _feedGrinderScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // Right column: the production queue.
            BuildFeedQueue(panel);
        }

        private void BuildFeedQueue(Transform panel)
        {
            var well = UIFactory.CreateWell("QueueWell", panel);
            UIFactory.Place(well.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -76f), new Vector2(420f, 684f));

            _feedQueueTitle = UIFactory.CreateSectionLabel("QueueTitle", well.transform, string.Empty);
            _feedQueueTitle.alignment = TextAnchor.MiddleCenter;
            UIFactory.Place(_feedQueueTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(380f, 40f));

            _feedQueueNames = new Text[GameSession.FeedQueueCapacity];
            _feedQueueStatuses = new Text[GameSession.FeedQueueCapacity];
            _feedQueueIcons = new Image[GameSession.FeedQueueCapacity];
            _feedQueueFills = new Image[GameSession.FeedQueueCapacity];
            _feedQueueSlots = new Image[GameSession.FeedQueueCapacity];

            for (var i = 0; i < GameSession.FeedQueueCapacity; i++)
            {
                var slot = UIFactory.CreateImage($"QueueSlot_{i}", well.transform, UIFactory.CardSprite, Color.white);
                slot.raycastTarget = false;
                UIFactory.Place(slot.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -64f - i * 162f), new Vector2(384f, 148f));
                _feedQueueSlots[i] = slot;

                var iconWell = UIFactory.CreateWell("IconWell", slot.transform);
                UIFactory.Place(iconWell.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 2f), new Vector2(112f, 112f));
                var icon = UIFactory.CreateIcon("Icon", iconWell.transform, null, new Vector2(92f, 92f));
                UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));
                _feedQueueIcons[i] = icon;

                var slotName = UIFactory.CreateText("Name", slot.transform, string.Empty, 28, UIFactory.Ink, TextAnchor.MiddleLeft);
                slotName.fontStyle = FontStyle.Bold;
                UIFactory.Place(slotName.rectTransform, new Vector2(0f, 0.5f), new Vector2(140f, 36f), new Vector2(230f, 34f));
                _feedQueueNames[i] = slotName;

                var status = UIFactory.CreateText("Status", slot.transform, string.Empty, 26, UIFactory.InkSoft, TextAnchor.MiddleLeft);
                UIFactory.Place(status.rectTransform, new Vector2(0f, 0.5f), new Vector2(140f, 2f), new Vector2(230f, 30f));
                _feedQueueStatuses[i] = status;

                var fill = UIFactory.CreateProgressBar("Progress", slot.transform, new Vector2(220f, 22f), UIFactory.Gold);
                UIFactory.Place(
                    fill.transform.parent as RectTransform,
                    new Vector2(0f, 0.5f),
                    new Vector2(140f, -36f),
                    new Vector2(220f, 22f));
                _feedQueueFills[i] = fill;
            }

            _feedCollectButton = UIFactory.CreateButton(
                "CollectButton",
                well.transform,
                "Collect",
                new Vector2(300f, 80f),
                34,
                ButtonStyle.Gold);
            UIFactory.Place(
                _feedCollectButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 22f),
                new Vector2(300f, 80f));
            _feedCollectButton.onClick.AddListener(CollectFeed);
        }

        private static Sprite LoadFeedIcon(FeedRecipeDefinition recipe)
        {
            return recipe != null && recipe.Icon != null ? recipe.Icon : UIFactory.Icon("icon_feed");
        }

        private Sprite LoadItemIcon(string itemId)
        {
            var icon = _session.Content.GetItemIcon(itemId);
            return icon != null ? icon : UIFactory.Icon($"icon_{itemId}");
        }

        /// <summary>Collects whatever is finished in the open building, feed or goods.</summary>
        private int CollectReadyProduction(PlacedBuilding building, out string message)
        {
            return _session.IsGoodsBuilding(building)
                ? _session.CollectReadyGoods(building, out message)
                : _session.CollectReadyFeed(building, out message);
        }

        private void UpdateFeedQueue()
        {
            if (!IsFeedGrinderVisible || _activeGrinder == null || _feedQueueStatuses == null)
            {
                return;
            }

            var jobs = _activeGrinder.Production;
            var anyReady = false;
            for (var i = 0; i < _feedQueueStatuses.Length; i++)
            {
                var progressBar = _feedQueueFills[i].transform.parent.gameObject;
                if (i >= jobs.Count)
                {
                    _feedQueueSlots[i].color = new Color(1f, 1f, 1f, 0.45f);
                    _feedQueueNames[i].text = "Empty";
                    _feedQueueNames[i].color = UIFactory.Muted;
                    SetPlainText(_feedQueueStatuses[i], string.Empty);
                    _feedQueueIcons[i].enabled = false;
                    progressBar.SetActive(false);
                    continue;
                }

                _feedQueueSlots[i].color = Color.white;
                _feedQueueNames[i].text = _session.Content.GetItemDisplayName(jobs[i].ItemId);
                _feedQueueNames[i].color = UIFactory.Ink;
                _feedQueueIcons[i].enabled = true;
                _feedQueueIcons[i].sprite = LoadItemIcon(jobs[i].ItemId);
                progressBar.SetActive(true);

                var remaining = _session.GetRemainingSeconds(jobs[i]);
                var fill = _feedQueueFills[i];
                if (remaining <= 0d)
                {
                    anyReady = true;
                    SetPlainText(_feedQueueStatuses[i], "Ready!");
                    _feedQueueStatuses[i].color = UIFactory.GreenDark;
                    fill.fillAmount = 1f;
                    fill.color = UIFactory.Green;
                }
                else if (i == 0 || _session.GetRemainingSeconds(jobs[i - 1]) <= 0d)
                {
                    // The job being worked on: show its countdown and how far along it is.
                    SetCountdownText(_feedQueueStatuses[i], remaining);
                    _feedQueueStatuses[i].color = UIFactory.InkSoft;
                    var duration = _session.GetJobDuration(jobs[i]);
                    fill.fillAmount = Mathf.Clamp01(1f - (float)(remaining / duration));
                    fill.color = UIFactory.Gold;
                }
                else
                {
                    SetCountdownText(_feedQueueStatuses[i], remaining, queued: true);
                    _feedQueueStatuses[i].color = UIFactory.Muted;
                    fill.fillAmount = 0f;
                }
            }

            if (_feedQueueTitleCount != jobs.Count)
            {
                _feedQueueTitleCount = jobs.Count;
                _feedQueueTitle.text = $"Making  {jobs.Count} / {GameSession.FeedQueueCapacity}";
            }

            _feedCollectButton.interactable = anyReady;
            UIFactory.SetButtonStyle(_feedCollectButton, anyReady ? ButtonStyle.Gold : ButtonStyle.Muted);
        }

        private void CollectFeed()
        {
            if (_activeGrinder == null)
            {
                return;
            }

            CollectReadyProduction(_activeGrinder, out var message);
            if (message != null)
            {
                ShowToast(message);
            }

            RebuildFeedGrinderRows();
        }

        public bool IsFeedGrinderVisible =>
            _feedGrinderOverlay != null && _feedGrinderOverlay.activeSelf;

        public PlacedBuilding ActiveFeedGrinder => _activeGrinder;

        public void ShowFeedGrinder(PlacedBuilding grinder)
        {
            if (_feedGrinderOverlay == null || grinder == null)
            {
                return;
            }

            CloseBuildingShop();
            CloseInventory();
            HideCropShop();
            HideScythe();
            CloseAnimalPen();
            if (IsPlacementActive)
            {
                CancelBuildingPlacement();
            }

            _activeGrinder = grinder;
            _feedQueueTitleCount = -1;
            if (_feedGrinderTitle != null && grinder.Definition != null)
            {
                _feedGrinderTitle.text = grinder.Definition.DisplayName;
            }

            CollectReadyProduction(grinder, out var collectMessage);
            if (collectMessage != null)
            {
                ShowToast(collectMessage);
            }

            RebuildFeedGrinderRows();
            _feedGrinderOverlay.SetActive(true);
            _feedGrinderScroll.verticalNormalizedPosition = 1f;
            UpdateFeedQueue();
        }

        public void CloseFeedGrinder()
        {
            if (_feedGrinderOverlay != null)
            {
                _feedGrinderOverlay.SetActive(false);
            }

            _activeGrinder = null;
        }

        private void RebuildFeedGrinderRows()
        {
            if (_feedGrinderRows == null)
            {
                return;
            }

            for (var i = _feedGrinderRows.childCount - 1; i >= 0; i--)
            {
                var child = _feedGrinderRows.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            if (ActiveIsGoodsBuilding)
            {
                RebuildGoodsRows();
                return;
            }

            var recipes = _session.Content.FeedRecipes;
            for (var i = 0; i < recipes.Count; i++)
            {
                var crop = _session.Content.GetCrop(recipes[i].CropId);
                if (crop == null)
                {
                    continue;
                }

                CreateFeedGrinderRow(recipes[i], crop);
            }
        }

        private void CreateFeedGrinderRow(FeedRecipeDefinition recipe, CropDefinition crop)
        {
            var count = _session.Inventory.GetCount(crop.Id);
            var enoughCrops = count >= recipe.CropAmount;
            var rowImage = UIFactory.CreateCard($"FeedRow_{recipe.Id}", _feedGrinderRows);

            var rowElement = rowImage.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 176f;
            rowElement.flexibleWidth = 1f;

            var iconWell = UIFactory.CreateWell("IconWell", rowImage.transform);
            UIFactory.Place(iconWell.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 2f), new Vector2(146f, 146f));
            var icon = UIFactory.CreateIcon("Icon", iconWell.transform, LoadFeedIcon(recipe), new Vector2(124f, 124f));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(124f, 124f));

            var nameText = UIFactory.CreateText("Name", rowImage.transform, recipe.DisplayName, 32, UIFactory.Ink, TextAnchor.MiddleLeft);
            nameText.fontStyle = FontStyle.Bold;
            UIFactory.Place(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(184f, 50f), new Vector2(260f, 40f));

            UIFactory.CreatePill(
                "Owned",
                rowImage.transform,
                $"x{_session.Inventory.GetCount(recipe.Id)}",
                26,
                new Vector2(84f, 40f),
                out var ownedPill);
            // Count badge on the bottom-right corner of the icon, like the inventory tiles.
            UIFactory.Place(ownedPill.rectTransform, new Vector2(0f, 0.5f), new Vector2(98f, -56f), new Vector2(84f, 40f));

            // "[crop] 5/3" (have/need), like the goods recipes: red when short.
            var cropIcon = UIFactory.CreateIcon("CropIcon", rowImage.transform, crop.Icon, new Vector2(46f, 46f));
            UIFactory.Place(cropIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(182f, 2f), new Vector2(46f, 46f));
            var detailText = UIFactory.CreateText(
                "Detail",
                rowImage.transform,
                $"{count}/{recipe.CropAmount}",
                28,
                enoughCrops ? UIFactory.InkSoft : UIFactory.Berry,
                TextAnchor.MiddleLeft);
            detailText.fontStyle = FontStyle.Bold;
            UIFactory.Place(detailText.rectTransform, new Vector2(0f, 0.5f), new Vector2(236f, 2f), new Vector2(260f, 34f));

            var clock = UIFactory.CreateIcon("ClockIcon", rowImage.transform, UIFactory.Icon("icon_clock"), new Vector2(38f, 38f));
            UIFactory.Place(clock.rectTransform, new Vector2(0f, 0.5f), new Vector2(186f, -48f), new Vector2(38f, 38f));
            var timeText = UIFactory.CreateText(
                "Time",
                rowImage.transform,
                FarmSimulation.FormatTime(recipe.ProductionSeconds),
                28,
                UIFactory.InkSoft,
                TextAnchor.MiddleLeft);
            UIFactory.Place(timeText.rectTransform, new Vector2(0f, 0.5f), new Vector2(236f, -48f), new Vector2(200f, 34f));

            var queueFull = _activeGrinder != null &&
                            _activeGrinder.Production.Count >= GameSession.FeedQueueCapacity;
            var canMake = enoughCrops && !queueFull;
            var grindButton = UIFactory.CreateButton(
                "GrindButton",
                rowImage.transform,
                queueFull ? "Full" : "Make",
                new Vector2(170f, 92f),
                34,
                canMake ? ButtonStyle.Primary : ButtonStyle.Muted);
            grindButton.interactable = canMake;
            UIFactory.Place(
                grindButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(-22f, 2f),
                new Vector2(170f, 92f));

            var feedId = recipe.Id;
            grindButton.onClick.AddListener(() => GrindFeed(feedId));
        }

        private void GrindFeed(string feedId)
        {
            _session.TryStartFeed(_activeGrinder, feedId, out var message);
            ShowToast(message);
            RebuildFeedGrinderRows();
            UpdateFeedQueue();
        }

        // ------------------------------------------------------------------ Goods recipes

        private void RebuildGoodsRows()
        {
            var recipes = _session.Content.GetGoodsRecipesFor(_activeGrinder.Definition.Id);
            for (var i = 0; i < recipes.Count; i++)
            {
                CreateGoodsRow(recipes[i]);
            }
        }

        /// <summary>
        /// A goods recipe card: the good, its ingredients as "[icon] 2/5" chips (red when short),
        /// the time it takes, what it sells for and a Make button.
        /// </summary>
        private void CreateGoodsRow(GoodsRecipeDefinition recipe)
        {
            var content = _session.Content;
            var product = content.GetProduct(recipe.OutputId);
            var rowImage = UIFactory.CreateCard($"GoodsRow_{recipe.Id}", _feedGrinderRows);
            var rowElement = rowImage.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 176f;
            rowElement.flexibleWidth = 1f;

            var iconWell = UIFactory.CreateWell("IconWell", rowImage.transform);
            UIFactory.Place(iconWell.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 2f), new Vector2(146f, 146f));
            var icon = UIFactory.CreateIcon("Icon", iconWell.transform, LoadItemIcon(recipe.OutputId), new Vector2(124f, 124f));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(124f, 124f));

            UIFactory.CreatePill(
                "Owned",
                rowImage.transform,
                $"x{_session.Inventory.GetCount(recipe.OutputId)}",
                26,
                new Vector2(84f, 40f),
                out var ownedPill);
            UIFactory.Place(ownedPill.rectTransform, new Vector2(0f, 0.5f), new Vector2(98f, -56f), new Vector2(84f, 40f));

            var nameText = UIFactory.CreateText(
                "Name",
                rowImage.transform,
                content.GetItemDisplayName(recipe.OutputId),
                32,
                UIFactory.Ink,
                TextAnchor.MiddleLeft);
            nameText.fontStyle = FontStyle.Bold;
            UIFactory.Place(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(184f, 54f), new Vector2(300f, 40f));

            // Ingredients: one chip per input, "have/need" in red when there isn't enough.
            // Three chips sit closer together, to stay clear of the Make button.
            var threeInputs = recipe.Inputs.Count >= 3;
            var chipSpacing = threeInputs ? 106f : 118f;
            var chipTextWidth = threeInputs ? 58f : 70f;
            var enough = true;
            for (var i = 0; i < recipe.Inputs.Count; i++)
            {
                var input = recipe.Inputs[i];
                var have = _session.Inventory.GetCount(input.itemId);
                var ok = have >= input.amount;
                enough &= ok;

                var x = 182f + i * chipSpacing;
                var inputIcon = UIFactory.CreateIcon($"Input_{input.itemId}", rowImage.transform, LoadItemIcon(input.itemId), new Vector2(46f, 46f));
                UIFactory.Place(inputIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 4f), new Vector2(46f, 46f));
                var inputText = UIFactory.CreateText(
                    $"InputCount_{input.itemId}",
                    rowImage.transform,
                    $"{have}/{input.amount}",
                    26,
                    ok ? UIFactory.InkSoft : UIFactory.Berry,
                    TextAnchor.MiddleLeft);
                inputText.fontStyle = FontStyle.Bold;
                UIFactory.Place(inputText.rectTransform, new Vector2(0f, 0.5f), new Vector2(x + 48f, 4f), new Vector2(chipTextWidth, 32f));
            }

            var clock = UIFactory.CreateIcon("ClockIcon", rowImage.transform, UIFactory.Icon("icon_clock"), new Vector2(38f, 38f));
            UIFactory.Place(clock.rectTransform, new Vector2(0f, 0.5f), new Vector2(186f, -50f), new Vector2(38f, 38f));
            var timeText = UIFactory.CreateText(
                "Time",
                rowImage.transform,
                FarmSimulation.FormatTime(recipe.ProductionSeconds),
                28,
                UIFactory.InkSoft,
                TextAnchor.MiddleLeft);
            UIFactory.Place(timeText.rectTransform, new Vector2(0f, 0.5f), new Vector2(230f, -50f), new Vector2(120f, 34f));

            if (product != null)
            {
                var coin = UIFactory.CreateIcon("CoinIcon", rowImage.transform, UIFactory.Icon("icon_coin"), new Vector2(36f, 36f));
                UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(350f, -50f), new Vector2(36f, 36f));
                var priceText = UIFactory.CreateText(
                    "Price",
                    rowImage.transform,
                    UIFactory.FormatNumber(product.SellPrice),
                    28,
                    UIFactory.InkSoft,
                    TextAnchor.MiddleLeft);
                UIFactory.Place(priceText.rectTransform, new Vector2(0f, 0.5f), new Vector2(392f, -50f), new Vector2(90f, 34f));
            }

            var unlocked = _session.IsUnlocked(recipe);
            if (!unlocked)
            {
                rowImage.gameObject.AddComponent<CanvasGroup>().alpha = 0.6f;
            }

            var queueFull = _activeGrinder.Production.Count >= GameSession.FeedQueueCapacity;
            var canMake = unlocked && enough && !queueFull;
            var makeButton = UIFactory.CreateButton(
                "MakeButton",
                rowImage.transform,
                !unlocked ? $"Lvl {recipe.UnlockLevel}" : queueFull ? "Full" : "Make",
                new Vector2(150f, 92f),
                34,
                canMake ? ButtonStyle.Primary : ButtonStyle.Muted);
            makeButton.interactable = canMake;
            UIFactory.Place(makeButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-22f, 2f), new Vector2(150f, 92f));

            var recipeId = recipe.Id;
            makeButton.onClick.AddListener(() => MakeGoods(recipeId));
        }

        private void MakeGoods(string recipeId)
        {
            _session.TryStartGoods(_activeGrinder, recipeId, out var message);
            ShowToast(message);
            RebuildFeedGrinderRows();
            UpdateFeedQueue();
        }
    }
}
