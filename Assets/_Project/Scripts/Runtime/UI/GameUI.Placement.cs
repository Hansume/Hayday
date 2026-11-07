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
    /// <summary>Placing buildings and new field plots: the banner, preview sprite and cell highlights.</summary>
    public sealed partial class GameUI
    {
        private void BuildBuildingPlacementVisuals()
        {
            _placementTileSprite = Resources.Load<Sprite>("Art/Placeholders/placement_tile");
            if (_placementTileSprite == null)
            {
                Debug.LogWarning("Missing placement tile sprite at Resources/Art/Placeholders/placement_tile.");
                _placementTileSprite = Resources.Load<Sprite>("Art/Placeholders/soil");
            }

            _slotPreviewSprite = FarmGame.View.FarmView.LoadSoilSprite();

            var preview = new GameObject("BuildingPlacementPreview");
            preview.transform.SetParent(_farmView.transform, false);
            _placementPreviewRenderer = preview.AddComponent<SpriteRenderer>();
            _placementPreviewRenderer.sortingLayerName = "Entities";
            _placementPreviewRenderer.sortingOrder = 9000;
            preview.SetActive(false);
            _placementPreviewRoot = preview;

            _placementHighlightRoot = new GameObject("BuildingPlacementHighlights");
            _placementHighlightRoot.transform.SetParent(_farmView.transform, false);

            var banner = UIFactory.CreateImage(
                "PlacementBanner",
                _safeAreaRect,
                UIFactory.HeaderSprite,
                Color.white);
            banner.raycastTarget = true;
            UIFactory.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(720f, 92f));

            _placementBannerText = UIFactory.CreateTitleText(
                "PlacementText",
                banner.transform,
                string.Empty,
                32,
                TextAnchor.MiddleLeft);
            UIFactory.Stretch(_placementBannerText.rectTransform, 34f, 8f, 210f, 4f);

            var cancelButton = UIFactory.CreateButton(
                "CancelPlacementButton",
                banner.transform,
                "Cancel",
                new Vector2(170f, 64f),
                30,
                ButtonStyle.Danger);
            UIFactory.Place(
                cancelButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(-22f, 2f),
                new Vector2(170f, 64f));
            cancelButton.onClick.AddListener(CancelBuildingPlacement);

            banner.gameObject.SetActive(false);
            _placementBanner = banner.gameObject;
        }

        public void BeginBuildingPlacement(string buildingId)
        {
            var building = _session.Content.GetBuilding(buildingId);
            if (building == null)
            {
                return;
            }

            if (!_session.IsUnlocked(building))
            {
                ShowToast(GameSession.LockedMessage(building.DisplayName, building.UnlockLevel));
                return;
            }

            if (_session.Wallet.Coins < building.Cost)
            {
                ShowToast($"Need {FormatCoins(building.Cost)}");
                return;
            }

            CancelBuildingMove();
            CloseBuildingShop();
            HideCropShop();
            HideScythe();
            CloseFeedGrinder();
            CloseAnimalPen();

            _farmView.HideLandSign();
            _placementMode = PlacementMode.Building;
            _placingBuilding = building;
            _placementValid = false;
            _placementReason = null;
            _placementOrigin = default;

            if (_placementPreviewRenderer != null)
            {
                _placementPreviewRenderer.sprite = building.WorldSprite;
                _placementPreviewRenderer.color = new Color(1f, 1f, 1f, 0.72f);
            }

            if (_placementPreviewRoot != null && _farmView != null)
            {
                var scale = _farmView.GetBuildingVisualScale(building);
                _placementPreviewRoot.transform.localScale = new Vector3(scale, scale, 1f);
            }

            CreatePlacementHighlights(building.Footprint);
            if (_placementBanner != null)
            {
                _placementBanner.SetActive(true);
            }

            if (_placementBannerText != null)
            {
                _placementBannerText.text = $"Place {building.DisplayName}";
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                UpdateBuildingPlacementPreview(mouse.position.ReadValue());
            }
        }

        public void BeginSlotPlacement()
        {
            if (_session.Wallet.Coins < GameSession.NewSlotCost)
            {
                ShowToast($"Need {FormatCoins(GameSession.NewSlotCost)}");
                return;
            }

            CancelBuildingMove();
            CloseBuildingShop();
            HideCropShop();
            HideScythe();
            CloseFeedGrinder();
            CloseAnimalPen();

            _farmView.HideLandSign();
            _placementMode = PlacementMode.Slot;
            _placingBuilding = null;
            _placementValid = false;
            _placementReason = null;
            _placementOrigin = default;

            if (_placementPreviewRenderer != null)
            {
                _placementPreviewRenderer.sprite = _slotPreviewSprite;
                _placementPreviewRenderer.color = new Color(1f, 1f, 1f, 0.72f);
            }

            if (_placementPreviewRoot != null)
            {
                _placementPreviewRoot.transform.localScale = Vector3.one;
            }

            CreatePlacementHighlights(Vector2Int.one);
            if (_placementBanner != null)
            {
                _placementBanner.SetActive(true);
            }

            if (_placementBannerText != null)
            {
                _placementBannerText.text = "Place New Field";
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                UpdateBuildingPlacementPreview(mouse.position.ReadValue());
            }
        }

        public void CancelBuildingPlacement()
        {
            _placementMode = PlacementMode.None;
            _placingBuilding = null;
            _placementValid = false;
            _placementReason = null;

            if (_placementBanner != null)
            {
                _placementBanner.SetActive(false);
            }

            SetPlacementPreviewVisible(false);
            ClearPlacementHighlights();
        }

        public bool HandlePlacementTap(Vector2 screenPosition)
        {
            if (!IsPlacementActive)
            {
                return false;
            }

            UpdateBuildingPlacementPreview(screenPosition);
            if (!_placementValid)
            {
                if (!string.IsNullOrEmpty(_placementReason))
                {
                    ShowToast(_placementReason);
                }

                return true;
            }

            if (_placementMode == PlacementMode.Slot)
            {
                var cost = GameSession.NewSlotCost;
                if (!_session.TryPlaceSlot(_placementOrigin, out var plotId, out var message))
                {
                    ShowToast(message);
                    return true;
                }

                ShowToast(message);
                ShowFloatingText(
                    _farmView.GetPlotWorldPosition(plotId) + Vector3.up * 0.7f,
                    $"-{FormatCoins(cost)}",
                    new Color(0.72f, 0.32f, 0.06f, 1f));
                CancelBuildingPlacement();
                return true;
            }

            var building = _placingBuilding;
            var buildingCost = building.Cost;
            var worldPosition = _farmView.GetBuildingWorldPosition(building, _placementOrigin);
            if (!_session.TryPlaceBuilding(building.Id, _placementOrigin, out var buildingMessage))
            {
                ShowToast(buildingMessage);
                return true;
            }

            ShowToast(buildingMessage);
            ShowFloatingText(
                worldPosition + Vector3.up * 0.7f,
                $"-{FormatCoins(buildingCost)}",
                new Color(0.72f, 0.32f, 0.06f, 1f));
            CancelBuildingPlacement();
            return true;
        }

        public bool TryPlaceBuildingAtScreen(Vector2 screenPosition)
        {
            return HandlePlacementTap(screenPosition);
        }

        public bool TryPlaceSlotAtScreen(Vector2 screenPosition)
        {
            return HandlePlacementTap(screenPosition);
        }

        private void UpdateBuildingPlacement()
        {
            if (!IsPlacementActive)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            UpdateBuildingPlacementPreview(mouse.position.ReadValue());
        }

        private void UpdateBuildingPlacementPreview(Vector2 screenPosition)
        {
            if (!IsPlacementActive)
            {
                SetPlacementPreviewVisible(false);
                return;
            }

            if (!TryGetFarmCellAtScreen(screenPosition, out var cell))
            {
                _placementValid = false;
                _placementReason = "Tap a clear spot";
                SetPlacementPreviewVisible(false);
                return;
            }

            _placementOrigin = cell;
            _placementValid = _placementMode == PlacementMode.Building
                ? _session.CanPlaceBuilding(_placingBuilding.Id, cell, out _placementReason)
                : _session.CanPlaceSlot(cell, out _placementReason);

            if (_placementMode == PlacementMode.Building)
            {
                ShowFootprintPreview(
                    _placingBuilding.Footprint,
                    cell,
                    _farmView.GetBuildingWorldPosition(_placingBuilding, cell),
                    _placementValid);
            }
            else
            {
                ShowFootprintPreview(Vector2Int.one, cell, _farmView.GetCellWorldPosition(cell), _placementValid);
            }
        }

        /// <summary>The farm cell under a screen point; false when off the farm or over a window or button.</summary>
        private bool TryGetFarmCellAtScreen(Vector2 screenPosition, out Vector2Int cell)
        {
            cell = default;
            if (_camera == null || _farmView == null || IsPointerOverBlockingUi(screenPosition, null))
            {
                return false;
            }

            var worldPoint = _camera.ScreenToWorldPoint(new Vector3(
                screenPosition.x,
                screenPosition.y,
                -_camera.transform.position.z));
            return _farmView.TryGetCellAtWorld(worldPoint, out cell);
        }

        /// <summary>
        /// Puts the preview sprite at <paramref name="previewPosition"/> and the cell highlights
        /// over the footprint starting at <paramref name="origin"/>: green when the spot is
        /// free, red when it isn't.
        /// </summary>
        private void ShowFootprintPreview(Vector2Int footprint, Vector2Int origin, Vector3 previewPosition, bool valid)
        {
            for (var i = 0; i < _placementCellHighlights.Count; i++)
            {
                if (i >= footprint.x * footprint.y)
                {
                    break;
                }

                var offsetX = i % footprint.x;
                var offsetY = i / footprint.x;
                var highlight = _placementCellHighlights[i];
                highlight.transform.position = _farmView.GetCellWorldPosition(
                    new Vector2Int(origin.x + offsetX, origin.y + offsetY));
                highlight.color = valid
                    ? new Color(0.35f, 1f, 0.4f, 0.6f)
                    : new Color(1f, 0.35f, 0.3f, 0.65f);
                if (!highlight.gameObject.activeSelf)
                {
                    highlight.gameObject.SetActive(true);
                }
            }

            if (_placementPreviewRoot != null)
            {
                _placementPreviewRoot.transform.position = previewPosition;
                _placementPreviewRoot.SetActive(true);
            }

            if (_placementPreviewRenderer != null)
            {
                _placementPreviewRenderer.color = valid
                    ? new Color(1f, 1f, 1f, 0.72f)
                    : new Color(1f, 0.55f, 0.45f, 0.55f);
            }
        }

        private void CreatePlacementHighlights(Vector2Int footprint)
        {
            ClearPlacementHighlights();
            if (_placementHighlightRoot == null)
            {
                return;
            }

            var count = footprint.x * footprint.y;
            for (var i = 0; i < count; i++)
            {
                var cellObject = new GameObject($"PlacementCell_{i}");
                cellObject.transform.SetParent(_placementHighlightRoot.transform, false);

                var highlight = cellObject.AddComponent<SpriteRenderer>();
                highlight.sprite = _placementTileSprite;
                highlight.sortingLayerName = "Ground";
                highlight.sortingOrder = 100;
                highlight.color = new Color(0.35f, 1f, 0.4f, 0.6f);
                _placementCellHighlights.Add(highlight);
            }
        }

        private void ClearPlacementHighlights()
        {
            for (var i = 0; i < _placementCellHighlights.Count; i++)
            {
                var highlight = _placementCellHighlights[i];
                if (highlight != null)
                {
                    highlight.gameObject.SetActive(false);
                    Destroy(highlight.gameObject);
                }
            }

            _placementCellHighlights.Clear();
        }

        private void SetPlacementPreviewVisible(bool visible)
        {
            if (_placementPreviewRoot != null)
            {
                _placementPreviewRoot.SetActive(visible);
            }

            for (var i = 0; i < _placementCellHighlights.Count; i++)
            {
                var highlight = _placementCellHighlights[i];
                if (highlight != null && highlight.gameObject.activeSelf != visible)
                {
                    highlight.gameObject.SetActive(visible);
                }
            }
        }
    }
}
