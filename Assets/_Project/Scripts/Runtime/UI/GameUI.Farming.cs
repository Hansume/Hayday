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
    /// <summary>Farming on the map: the crop tray, drag-to-plant, and the scythe.</summary>
    public sealed partial class GameUI
    {
        private void BuildInteractionVisuals()
        {
            var ghost = UIFactory.CreateRect("CropDragGhost", _canvasRect);
            UIFactory.SetAnchored(
                ghost,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(160f, 160f));

            var halo = UIFactory.CreateImage(
                "Halo",
                ghost,
                UIFactory.CircleSprite,
                new Color(1f, 1f, 1f, 0.32f));
            halo.raycastTarget = false;
            UIFactory.SetAnchored(
                halo.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(146f, 146f));

            _cropGhostIcon = UIFactory.CreateImage("Icon", ghost, null, Color.white);
            _cropGhostIcon.preserveAspect = true;
            _cropGhostIcon.raycastTarget = false;
            UIFactory.SetAnchored(
                _cropGhostIcon.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(126f, 126f));

            var ghostGroup = ghost.gameObject.AddComponent<CanvasGroup>();
            ghostGroup.blocksRaycasts = false;
            ghostGroup.interactable = false;
            ghostGroup.alpha = 0.95f;
            ghost.gameObject.SetActive(false);
            _cropGhostRoot = ghost;

            var soilSprite = Resources.Load<Sprite>("Art/Placeholders/soil");
            _dropHighlight = UIFactory.CreateImage(
                "DropHighlight",
                _canvasRect,
                soilSprite,
                new Color(0.55f, 1f, 0.4f, 0.55f));
            _dropHighlight.preserveAspect = true;
            _dropHighlight.raycastTarget = false;
            UIFactory.SetAnchored(
                _dropHighlight.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(164f, 92f));
            _dropHighlight.gameObject.SetActive(false);

            BuildScythe();
        }

        private void BuildScythe()
        {
            var scytheSprite = Resources.Load<Sprite>("Art/Placeholders/icon_scythe");
            if (scytheSprite == null)
            {
                Debug.LogWarning("Missing scythe sprite at Resources/Art/Placeholders/icon_scythe.");
            }

            var root = UIFactory.CreateImage(
                "Scythe",
                _canvasRect,
                UIFactory.CircleSprite,
                new Color(0.96f, 0.76f, 0.24f, 1f));
            root.raycastTarget = true;
            UIFactory.SetAnchored(
                root.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(156f, 156f));

            var inner = UIFactory.CreateImage(
                "Inner",
                root.transform,
                UIFactory.CircleSprite,
                new Color(0.2f, 0.14f, 0.08f, 0.96f));
            inner.raycastTarget = false;
            UIFactory.SetAnchored(
                inner.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(142f, 142f));

            var icon = UIFactory.CreateImage("Icon", root.transform, scytheSprite, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UIFactory.SetAnchored(
                icon.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(118f, 118f));

            root.gameObject.AddComponent<ScytheDragHandle>().Initialize(this);
            root.gameObject.SetActive(false);
            _scytheRoot = root.rectTransform;
        }

        public void BeginCropDrag(string cropId, PointerEventData eventData, GameObject source)
        {
            var crop = _session.Content.GetCrop(cropId);
            if (crop == null || _cropGhostRoot == null)
            {
                return;
            }

            if (!_session.IsUnlocked(crop))
            {
                ShowToast(GameSession.LockedMessage(crop.DisplayName, crop.UnlockLevel));
                return;
            }

            if (IsPlacementActive)
            {
                CancelBuildingPlacement();
            }

            HideScythe();
            SelectCrop(crop.Id);
            _draggedCropId = crop.Id;
            _cropDragSource = source;
            _cropDragLastScreenPosition = eventData.position;
            _cropDragPlanted.Clear();
            _cropDragBlockedByCoins = false;
            _cropGhostIcon.sprite = crop.Icon;
            _cropGhostRoot.gameObject.SetActive(true);

            MoveCropGhost(eventData.position);
            TryPlantAtScreen(eventData.position);
            UpdateCropHighlight(eventData.position);
        }

        public void DragCrop(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(_draggedCropId))
            {
                return;
            }

            MoveCropGhost(eventData.position);
            TryPlantSweep(_cropDragLastScreenPosition, eventData.position);
            _cropDragLastScreenPosition = eventData.position;
            UpdateCropHighlight(eventData.position);
        }

        public void EndCropDrag(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(_draggedCropId) &&
                !IsPointerOverBlockingUi(eventData.position, _cropDragSource))
            {
                TryPlantSweep(_cropDragLastScreenPosition, eventData.position);
            }

            EndCropDragVisuals();
        }

        public void ShowScythe(int plotId)
        {
            if (_scytheRoot == null || _camera == null || _farmView == null)
            {
                return;
            }

            var snapshot = _session.Farm.GetSnapshot(plotId);
            if (snapshot.IsEmpty || !snapshot.IsReady)
            {
                return;
            }

            HideCropShop();
            CloseFeedGrinder();
            CloseAnimalPen();
            _scythePlotId = plotId;
            _scytheDragging = false;
            _scytheHarvested.Clear();
            _scytheRoot.gameObject.SetActive(true);
            _scytheRoot.localScale = Vector3.one;
            PositionScytheAtPlot();

            if (!_scytheTutorialShown)
            {
                _scytheTutorialShown = true;
                ShowToast("Drag over ripe crops");
            }
        }

        public void HideScythe()
        {
            if (_scytheRoot != null)
            {
                _scytheRoot.gameObject.SetActive(false);
            }

            _scythePlotId = -1;
            _scytheDragging = false;
            _scytheHarvested.Clear();
        }

        public void BeginScytheDrag(PointerEventData eventData)
        {
            if (_scytheRoot == null || !_scytheRoot.gameObject.activeSelf || _scythePlotId < 0)
            {
                return;
            }

            _scytheDragging = true;
            _scytheLastScreenPosition = eventData.position;
            MoveScytheWithPointer(eventData.position);
            HarvestScythePlot(_scythePlotId);
        }

        public void DragScythe(PointerEventData eventData)
        {
            if (!_scytheDragging)
            {
                return;
            }

            HarvestScytheSweep(_scytheLastScreenPosition, eventData.position);
            _scytheLastScreenPosition = eventData.position;
            MoveScytheWithPointer(eventData.position);
        }

        public void EndScytheDrag(PointerEventData eventData)
        {
            if (!_scytheDragging)
            {
                return;
            }

            HideScythe();
        }

        public void NotifyScytheTapped()
        {
            ShowToast("Drag over ripe crops");
        }

        private void UpdateScythe()
        {
            if (_scytheRoot == null ||
                !_scytheRoot.gameObject.activeSelf ||
                _scytheDragging)
            {
                return;
            }

            if (_scythePlotId < 0)
            {
                HideScythe();
                return;
            }

            var snapshot = _session.Farm.GetSnapshot(_scythePlotId);
            if (snapshot.IsEmpty || !snapshot.IsReady)
            {
                HideScythe();
                return;
            }

            PositionScytheAtPlot();
        }

        private void PositionScytheAtPlot()
        {
            if (_scytheRoot == null || _scythePlotId < 0)
            {
                return;
            }

            var screenPoint = _camera.WorldToScreenPoint(
                _farmView.GetPlotWorldPosition(_scythePlotId) + Vector3.up * 0.9f);
            if (screenPoint.z <= 0f)
            {
                HideScythe();
                return;
            }

            var bob = Mathf.Sin(Time.unscaledTime * 3.5f) * 5f;
            _scytheRoot.anchoredPosition =
                ScreenToCanvasPoint(screenPoint) + new Vector2(0f, 42f + bob);
        }

        private void MoveScytheWithPointer(Vector2 screenPosition)
        {
            if (_scytheRoot == null)
            {
                return;
            }

            _scytheRoot.anchoredPosition =
                ScreenToCanvasPoint(screenPosition) + new Vector2(0f, 30f);
        }

        private void HarvestScytheSweep(Vector2 from, Vector2 to)
        {
            var distance = Vector2.Distance(from, to);
            if (distance < 0.5f)
            {
                return;
            }

            var steps = Mathf.Clamp(Mathf.CeilToInt(distance / 12f), 1, 32);
            for (var i = 0; i <= steps; i++)
            {
                var point = Vector2.Lerp(from, to, i / (float)steps);
                if (IsPointerOverBlockingUi(point, _scytheRoot.gameObject))
                {
                    continue;
                }

                if (!TryGetPlotAtScreen(point, out var plotId, out _))
                {
                    continue;
                }

                HarvestScythePlot(plotId);
            }
        }

        private void HarvestScythePlot(int plotId)
        {
            if (plotId < 0 || _scytheHarvested.Contains(plotId))
            {
                return;
            }

            var snapshot = _session.Farm.GetSnapshot(plotId);
            if (!snapshot.IsReady)
            {
                return;
            }

            if (!_session.TryHarvest(plotId, out var outcome, out var xpGained, out var message))
            {
                // A sweep over many plots with full storage fails on each; say it once.
                var alreadyShowing = _toastGroup != null && _toastGroup.alpha > 0f && _toastText.text == message;
                if (!alreadyShowing)
                {
                    ShowToast(message);
                }

                return;
            }

            _scytheHarvested.Add(plotId);
            ShowFloatingText(
                _farmView.GetPlotWorldPosition(plotId) + Vector3.up * 0.7f,
                xpGained > 0
                    ? $"+{outcome.Amount} {outcome.CropName}  +{xpGained} XP"
                    : $"+{outcome.Amount} {outcome.CropName}",
                new Color(0.16f, 0.42f, 0.1f, 1f));
        }

        private void MoveCropGhost(Vector2 screenPosition)
        {
            if (_cropGhostRoot == null)
            {
                return;
            }

            _cropGhostRoot.anchoredPosition =
                ScreenToCanvasPoint(screenPosition) + new Vector2(0f, 58f);
        }

        private void TryPlantSweep(Vector2 from, Vector2 to)
        {
            var distance = Vector2.Distance(from, to);
            if (distance < 0.5f)
            {
                TryPlantAtScreen(to);
                return;
            }

            var steps = Mathf.Clamp(Mathf.CeilToInt(distance / 16f), 1, 32);
            for (var i = 0; i <= steps; i++)
            {
                TryPlantAtScreen(Vector2.Lerp(from, to, i / (float)steps));
            }
        }

        private void TryPlantAtScreen(Vector2 screenPosition)
        {
            if (string.IsNullOrEmpty(_draggedCropId) ||
                IsPointerOverBlockingUi(screenPosition, _cropDragSource))
            {
                return;
            }

            var crop = _session.Content.GetCrop(_draggedCropId);
            if (crop == null || !TryGetPlotAtScreen(screenPosition, out var plotId, out var plotWorld))
            {
                return;
            }

            if (_cropDragPlanted.Contains(plotId))
            {
                return;
            }

            var snapshot = _session.Farm.GetSnapshot(plotId);
            if (!snapshot.IsEmpty)
            {
                return;
            }

            if (_session.Wallet.Coins < crop.SeedCost)
            {
                if (!_cropDragBlockedByCoins)
                {
                    _cropDragBlockedByCoins = true;
                    ShowToast($"Need {FormatCoins(crop.SeedCost)}");
                }

                return;
            }

            if (!_session.TryPlant(plotId, crop.Id, out var message))
            {
                ShowToast(message);
                return;
            }

            _cropDragPlanted.Add(plotId);
            ShowFloatingText(
                plotWorld + Vector3.up * 0.7f,
                $"-{FormatCoins(crop.SeedCost)}",
                new Color(0.72f, 0.32f, 0.06f, 1f));
        }

        private void UpdateCropHighlight(Vector2 screenPosition)
        {
            if (_dropHighlight == null ||
                string.IsNullOrEmpty(_draggedCropId) ||
                IsPointerOverBlockingUi(screenPosition, _cropDragSource) ||
                !TryGetPlotAtScreen(screenPosition, out var plotId, out var plotWorld))
            {
                HideCropHighlight();
                return;
            }

            var snapshot = _session.Farm.GetSnapshot(plotId);
            if (!snapshot.IsEmpty)
            {
                HideCropHighlight();
                return;
            }

            var crop = _session.Content.GetCrop(_draggedCropId);
            var affordable = crop != null && _session.Wallet.Coins >= crop.SeedCost;
            var screenPoint = _camera.WorldToScreenPoint(plotWorld);
            if (screenPoint.z <= 0f)
            {
                HideCropHighlight();
                return;
            }

            _dropHighlight.color = affordable
                ? new Color(0.55f, 1f, 0.4f, 0.55f)
                : new Color(1f, 0.55f, 0.25f, 0.5f);
            _dropHighlight.rectTransform.anchoredPosition = ScreenToCanvasPoint(screenPoint);
            if (!_dropHighlight.gameObject.activeSelf)
            {
                _dropHighlight.gameObject.SetActive(true);
            }
        }

        private void HideCropHighlight()
        {
            if (_dropHighlight != null && _dropHighlight.gameObject.activeSelf)
            {
                _dropHighlight.gameObject.SetActive(false);
            }
        }

        private void EndCropDragVisuals()
        {
            _draggedCropId = null;
            _cropDragSource = null;
            _cropDragLastScreenPosition = Vector2.zero;
            _cropDragPlanted.Clear();
            _cropDragBlockedByCoins = false;

            if (_cropGhostRoot != null && _cropGhostRoot.gameObject.activeSelf)
            {
                _cropGhostRoot.gameObject.SetActive(false);
            }

            HideCropHighlight();
        }

        private bool TryGetPlotAtScreen(
            Vector2 screenPosition,
            out int plotId,
            out Vector3 plotWorldPosition)
        {
            plotId = -1;
            plotWorldPosition = Vector3.zero;
            if (_camera == null || _farmView == null)
            {
                return false;
            }

            var worldPoint = _camera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -_camera.transform.position.z));
            if (!_farmView.TryGetPlotAtWorld(worldPoint, out plotId))
            {
                return false;
            }

            plotWorldPosition = _farmView.GetPlotWorldPosition(plotId);
            return true;
        }

        private void BuildCropDock()
        {
            // A wooden tray along the bottom with a crop card for each seed.
            var dock = UIFactory.CreateImage("CropDock", _safeAreaRect, UIFactory.HeaderSprite, Color.white);
            dock.raycastTarget = true;
            _cropDock = dock.gameObject;
            // Wide enough for nine compact cards between the screen edge and the action buttons.
            UIFactory.Place(dock.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1500f, 226f));

            var hintPill = UIFactory.CreateDecor("HintPill", dock.transform, UIFactory.PillSprite, Color.white);
            UIFactory.Place(hintPill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 46f), new Vector2(460f, 60f));
            _hintText = UIFactory.CreateTitleText("Hint", hintPill.transform, string.Empty, 32);
            UIFactory.Stretch(_hintText.rectTransform, 24f, 2f, 24f, 2f);

            // Scrolls (mouse wheel or a drag between cards) if more crops are added than fit.
            var cardsRoot = UIFactory.CreateHorizontalScroll(
                "CropCardsViewport",
                dock.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(1450f, 196f),
                10f,
                out _);
            cardsRoot.name = "CropCards";

            var crops = _session.Content.Crops;
            for (var i = 0; i < crops.Count; i++)
            {
                var crop = crops[i];
                if (crop == null)
                {
                    continue;
                }

                var card = CreateCropCard(crop, cardsRoot);
                _cropCards.Add(card);
            }

            _cropDock.SetActive(false);
        }

        private CropCard CreateCropCard(CropDefinition crop, Transform parent)
        {
            // A compact upright card: icon, name, then seed cost and grow time.
            var button = UIFactory.CreateButton(
                $"Crop_{crop.Id}",
                parent,
                string.Empty,
                new Vector2(150f, 186f),
                24,
                Color.white,
                UIFactory.Ink);
            button.transition = Selectable.Transition.None;

            var layoutElement = button.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 150f;
            layoutElement.preferredHeight = 186f;

            var background = button.GetComponent<Image>();
            background.sprite = UIFactory.CardSprite;
            background.color = Color.white;

            var well = UIFactory.CreateWell("IconWell", button.transform);
            UIFactory.Place(well.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(92f, 92f));
            var icon = UIFactory.CreateIcon("Icon", well.transform, crop.Icon, new Vector2(78f, 78f));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78f, 78f));

            var nameText = UIFactory.CreateText(
                "Name",
                button.transform,
                crop.DisplayName,
                26,
                UIFactory.Ink,
                TextAnchor.MiddleCenter);
            nameText.fontStyle = FontStyle.Bold;
            UIFactory.Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(148f, 28f));

            var coin = UIFactory.CreateIcon("CoinIcon", button.transform, UIFactory.Icon("icon_coin"), new Vector2(24f, 24f));
            UIFactory.Place(coin.rectTransform, new Vector2(0.5f, 1f), new Vector2(-40f, -129f), new Vector2(24f, 24f));
            var costText = UIFactory.CreateText(
                "Cost",
                button.transform,
                UIFactory.FormatNumber(crop.SeedCost),
                25,
                UIFactory.InkSoft,
                TextAnchor.MiddleLeft);
            costText.fontStyle = FontStyle.Bold;
            UIFactory.Place(costText.rectTransform, new Vector2(0.5f, 1f), new Vector2(20f, -128f), new Vector2(92f, 26f));

            var clock = UIFactory.CreateIcon("ClockIcon", button.transform, UIFactory.Icon("icon_clock"), new Vector2(22f, 22f));
            UIFactory.Place(clock.rectTransform, new Vector2(0.5f, 1f), new Vector2(-40f, -155f), new Vector2(22f, 22f));
            var timeText = UIFactory.CreateText(
                "GrowTime",
                button.transform,
                FarmSimulation.FormatTime(crop.GrowSeconds),
                23,
                UIFactory.InkSoft,
                TextAnchor.MiddleLeft);
            timeText.fontStyle = FontStyle.Bold;
            UIFactory.Place(timeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(20f, -154f), new Vector2(92f, 24f));

            var cropId = crop.Id;
            button.onClick.AddListener(() => SelectCrop(cropId));
            button.gameObject.AddComponent<CropDragHandle>().Initialize(this, cropId);

            // A gold rim shown around the selected seed.
            var selection = button.gameObject.AddComponent<Outline>();
            selection.effectColor = new Color(1f, 0.74f, 0.12f, 1f);
            selection.effectDistance = new Vector2(4f, -4f);
            selection.enabled = false;

            // Shown over the icon while the crop is still locked.
            var lockText = UIFactory.CreatePill(
                "LockBadge",
                button.transform,
                $"Lvl {crop.UnlockLevel}",
                26,
                new Vector2(104f, 42f),
                out var lockPill);
            lockText.fontStyle = FontStyle.Bold;
            UIFactory.Place(lockPill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(104f, 42f));

            var card = new CropCard
            {
                CropId = crop.Id,
                Background = background,
                Name = nameText,
                Cost = costText,
                Selection = selection,
                UnlockLevel = crop.UnlockLevel,
                Group = button.gameObject.AddComponent<CanvasGroup>(),
                LockBadge = lockPill.gameObject
            };
            ApplyCropLock(card);
            return card;
        }

        private void ApplyCropLock(CropCard card)
        {
            var locked = !_session.IsUnlocked(card.UnlockLevel);
            card.LockBadge.SetActive(locked);
            // Locked cards stay clickable so a tap can say when they unlock.
            card.Group.alpha = locked ? 0.7f : 1f;
        }

        /// <summary>Updates the crop dock's lock badges, e.g. after a level up.</summary>
        private void RefreshCropLocks()
        {
            for (var i = 0; i < _cropCards.Count; i++)
            {
                ApplyCropLock(_cropCards[i]);
            }
        }

        /// <summary>The first crop the player may plant, or null.</summary>
        private CropDefinition FirstUnlockedCrop()
        {
            var crops = _session.Content.Crops;
            for (var i = 0; i < crops.Count; i++)
            {
                if (_session.IsUnlocked(crops[i]))
                {
                    return crops[i];
                }
            }

            return null;
        }

        public void ShowCropShop()
        {
            if (_cropDock == null)
            {
                return;
            }

            CloseInventory();
            CloseBuildingShop();
            CloseFeedGrinder();
            CloseAnimalPen();
            if (IsPlacementActive)
            {
                CancelBuildingPlacement();
            }

            HideScythe();
            _cropDock.SetActive(true);

            if (string.IsNullOrEmpty(_selectedCropId) && FirstUnlockedCrop() != null)
            {
                SelectCrop(FirstUnlockedCrop().Id);
            }
        }

        public void HideCropShop()
        {
            if (_cropDock != null)
            {
                _cropDock.SetActive(false);
            }

            EndCropDragVisuals();
        }

        private void SelectCrop(string cropId)
        {
            var crop = _session.Content.GetCrop(cropId);
            if (crop == null)
            {
                return;
            }

            if (!_session.IsUnlocked(crop))
            {
                ShowToast(GameSession.LockedMessage(crop.DisplayName, crop.UnlockLevel));
                return;
            }

            if (IsPlacementActive)
            {
                CancelBuildingPlacement();
            }

            HideScythe();
            _selectedCropId = crop.Id;
            for (var i = 0; i < _cropCards.Count; i++)
            {
                var card = _cropCards[i];
                var selected = card.CropId == _selectedCropId;
                card.Background.color = selected ? new Color(1f, 0.9f, 0.62f, 1f) : Color.white;
                card.Selection.enabled = selected;
                card.Name.color = selected ? new Color(0.4f, 0.22f, 0.04f, 1f) : UIFactory.Ink;
                card.Cost.color = selected ? new Color(0.54f, 0.32f, 0.08f, 1f) : UIFactory.InkSoft;
            }

            if (_hintText != null)
            {
                _hintText.text = "Drag onto empty plots";
            }
        }
    }
}
