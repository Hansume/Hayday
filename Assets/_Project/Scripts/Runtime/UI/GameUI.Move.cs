using FarmGame.Buildings;
using UnityEngine;

namespace FarmGame.UI
{
    /// <summary>
    /// Moving buildings: press and hold a building to pick it up, drag it, and let go to drop it.
    /// The building follows the pointer cell by cell with its footprint shown green (free) or red
    /// (blocked). Dropping on a blocked spot puts it back where it was. The input controller
    /// decides when a press becomes a pick-up and calls Begin/Update/End here.
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>How far above its cells a picked-up building floats, in world units.</summary>
        private const float MoveLift = 0.18f;

        private PlacedBuilding _movingBuilding;
        private Vector2Int _moveGrabOffset;
        private Vector2Int _moveOrigin;
        private bool _moveValid;
        private string _moveReason;
        private bool _moveHintShown;

        public bool IsMovingBuilding => _movingBuilding != null;

        /// <summary>Picks up a building at the pointer. Returns false if nothing could be picked up.</summary>
        public bool BeginBuildingMove(PlacedBuilding building, Vector2 screenPosition)
        {
            if (building == null || building.Definition == null || IsPlacementActive || _farmView == null)
            {
                return false;
            }

            CancelBuildingMove();
            _farmView.HideLandSign();
            HideCropShop();
            HideScythe();
            CloseFeedGrinder();
            CloseAnimalPen();
            CloseNoticeBoard();

            var definition = building.Definition;
            var footprint = definition.Footprint;
            _movingBuilding = building;
            _moveOrigin = building.Origin;
            _moveValid = true;
            _moveReason = null;

            // Keep the cell the player grabbed under their finger, so the building doesn't jump.
            // A building picked by its tall picture (rather than its cells) is held by its middle.
            _moveGrabOffset = new Vector2Int((footprint.x - 1) / 2, (footprint.y - 1) / 2);
            if (TryGetFarmCellAtScreen(screenPosition, out var grabbed))
            {
                var offset = grabbed - building.Origin;
                if (offset.x >= 0 && offset.x < footprint.x && offset.y >= 0 && offset.y < footprint.y)
                {
                    _moveGrabOffset = offset;
                }
            }

            if (_placementPreviewRenderer != null)
            {
                _placementPreviewRenderer.sprite = definition.WorldSprite;
            }

            if (_placementPreviewRoot != null)
            {
                var scale = _farmView.GetBuildingVisualScale(definition) * 1.04f;
                _placementPreviewRoot.transform.localScale = new Vector3(scale, scale, 1f);
            }

            CreatePlacementHighlights(footprint);
            _farmView.SetBuildingHidden(building, true);

            if (!_moveHintShown)
            {
                _moveHintShown = true;
                ShowToast("Drag to a new spot");
            }

            ShowMovePreview();
            return true;
        }

        /// <summary>Follows the pointer while a building is held.</summary>
        public void UpdateBuildingMove(Vector2 screenPosition)
        {
            if (!IsMovingBuilding)
            {
                return;
            }

            if (!TryGetFarmCellAtScreen(screenPosition, out var cell))
            {
                // Off the farm: the building waits at its last spot, and won't drop there.
                _moveValid = false;
                _moveReason = "Drop on a clear spot";
                ShowMovePreview();
                return;
            }

            _moveOrigin = cell - _moveGrabOffset;
            _moveValid = _session.CanMoveBuilding(_movingBuilding, _moveOrigin, out _moveReason);
            ShowMovePreview();
        }

        /// <summary>Drops the held building: moves it if the spot is free, otherwise puts it back.</summary>
        public void EndBuildingMove(Vector2 screenPosition)
        {
            if (!IsMovingBuilding)
            {
                return;
            }

            UpdateBuildingMove(screenPosition);
            var building = _movingBuilding;
            var origin = _moveOrigin;
            var valid = _moveValid;
            var reason = _moveReason;
            StopMoving();

            if (origin == building.Origin)
            {
                return;
            }

            if (!valid)
            {
                ShowToast(string.IsNullOrEmpty(reason) ? "It can't go there." : reason);
                return;
            }

            _session.TryMoveBuilding(building, origin, out var message);
            ShowToast(message);
        }

        /// <summary>Puts a held building back where it was.</summary>
        public void CancelBuildingMove()
        {
            if (IsMovingBuilding)
            {
                StopMoving();
            }
        }

        private void ShowMovePreview()
        {
            var definition = _movingBuilding.Definition;
            ShowFootprintPreview(
                definition.Footprint,
                _moveOrigin,
                _farmView.GetBuildingWorldPosition(definition, _moveOrigin) + Vector3.up * MoveLift,
                _moveValid);
        }

        private void StopMoving()
        {
            if (_farmView != null)
            {
                _farmView.SetBuildingHidden(_movingBuilding, false);
            }

            _movingBuilding = null;
            SetPlacementPreviewVisible(false);
            ClearPlacementHighlights();
        }
    }
}
