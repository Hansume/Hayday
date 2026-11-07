using FarmGame.Input;
using FarmGame.View;
using UnityEngine;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>
    /// Travelling between the farm, the Beach and, from the dock there, the open sea. The button
    /// shows where it goes next: Beach from the farm or the sea, Farm from the Beach. The camera
    /// comes back to the same spot and zoom on the farm and the Beach. The Shop button hides away
    /// from the farm, because anything bought from it is placed on the farm.
    /// </summary>
    public sealed partial class GameUI
    {
        private const float BeachZoom = 7f;

        private IsoCameraRig _cameraRig;
        private Button _shopActionButton;
        private Button _travelButton;
        private Image _travelIcon;
        private Text _travelCaption;
        private Vector3 _farmCameraPosition;
        private float _farmCameraZoom;
        private Vector3 _beachCameraPosition;
        private float _beachCameraZoom;

        public bool IsAtBeach { get; private set; }

        public bool IsAtSea { get; private set; }

        /// <summary>The travel button has the bottom-left corner to itself, matching the actions on the right.</summary>
        private void BuildTravelButton(Button shopButton)
        {
            _cameraRig = _camera != null ? _camera.GetComponent<IsoCameraRig>() : null;
            _shopActionButton = shopButton;

            var corner = UIFactory.CreateRect("TravelButtonCorner", _safeAreaRect);
            UIFactory.Place(corner, new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(170f, 170f));

            _travelButton = CreateActionButton(
                "BeachButton",
                corner,
                "Beach",
                "icon_beach",
                ButtonStyle.Secondary,
                Vector2.zero);
            _travelButton.onClick.AddListener(ToggleBeach);
            _travelIcon = _travelButton.transform.Find("Icon").GetComponent<Image>();
            _travelCaption = _travelButton.transform.Find("Caption").GetComponent<Text>();
        }

        private void ToggleBeach()
        {
            if (IsAtBeach)
            {
                ReturnToFarm();
            }
            else
            {
                GoToBeach();
            }
        }

        public void GoToBeach()
        {
            if (IsAtBeach || _cameraRig == null || _farmView == null)
            {
                return;
            }

            // Sailing back from the sea, to where the player was on the sand.
            if (IsAtSea)
            {
                _cameraRig.MoveTo(FarmView.BeachBounds, _beachCameraPosition, _beachCameraZoom);
                SetLocation(true, false);
                return;
            }

            // Nothing that belongs to the farm stays open.
            CancelBuildingMove();
            CancelBuildingPlacement();
            CloseInventory();
            CloseBuildingShop();
            CloseFeedGrinder();
            CloseAnimalPen();
            CloseNoticeBoard();
            CloseVisitor();
            CloseLandWindow();
            _farmView.HideLandSign();
            HideCropShop();
            HideScythe();

            _farmCameraPosition = _camera.transform.position;
            _farmCameraZoom = _camera.orthographicSize;
            _cameraRig.MoveTo(FarmView.BeachBounds, FarmView.BeachCenter, BeachZoom);
            SetLocation(true, false);
        }

        /// <summary>From the dock to the middle of the sea.</summary>
        public void GoToOpenSea()
        {
            if (!IsAtBeach)
            {
                return;
            }

            _beachCameraPosition = _camera.transform.position;
            _beachCameraZoom = _camera.orthographicSize;
            _cameraRig.MoveTo(FarmView.OpenSeaBounds, FarmView.OpenSeaCenter, BeachZoom);
            SetLocation(false, true);
        }

        public void ReturnToFarm()
        {
            if (!IsAtBeach && !IsAtSea)
            {
                return;
            }

            _cameraRig.MoveTo(_farmView.Grid.WorldBounds, _farmCameraPosition, _farmCameraZoom);
            SetLocation(false, false);
        }

        private void SetLocation(bool atBeach, bool atSea)
        {
            IsAtBeach = atBeach;
            IsAtSea = atSea;
            _shopActionButton.gameObject.SetActive(!atBeach && !atSea);
            _travelCaption.text = atBeach ? "Farm" : "Beach";
            _travelIcon.sprite = UIFactory.Icon(atBeach ? "icon_wheat" : "icon_beach");
            CloseCocktailBar();
            UpdateTravelBadge();
        }
    }
}
