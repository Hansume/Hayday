using System.Collections.Generic;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Farming;
using FarmGame.UI;
using FarmGame.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FarmGame.Input
{
    public sealed class FarmInputController : MonoBehaviour
    {
        private const float TapThresholdPixels = 18f;
        private const float TouchTapThresholdPixels = 24f;

        /// <summary>How long a press on a building must stay still before it's picked up to move.</summary>
        private const float HoldToMoveSeconds = 0.45f;

        private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();

        private GameSession _session;
        private FarmView _farmView;
        private GameUI _ui;
        private Camera _camera;
        private IsoCameraRig _cameraRig;
        private PointerEventData _pointerEventData;

        private bool _mouseTracking;
        private bool _mouseDragged;
        private Vector2 _mousePressPosition;
        private Vector2 _mouseLastPosition;

        private bool _touchTracking;
        private bool _touchDragged;
        private bool _touchBlockedByUi;
        private bool _touchPinching;
        private bool _awaitTouchRelease;
        private Vector2 _touchPressPosition;
        private Vector2 _touchLastPosition;
        private Vector2 _pinchLastMidpoint;
        private float _pinchLastDistance;

        // A press that started on a building: if it's held still long enough, the building is
        // picked up and follows the pointer instead of the camera panning.
        private PlacedBuilding _pressBuilding;
        private float _pressTime;

        public void Initialize(GameSession session, FarmView farmView, GameUI ui, Camera camera, IsoCameraRig cameraRig)
        {
            _session = session;
            _farmView = farmView;
            _ui = ui;
            _camera = camera;
            _cameraRig = cameraRig;
        }

        private void Update()
        {
            if (_session == null || _camera == null)
            {
                return;
            }

            HandlePlacementKeys();
            HandleDebugKeys();

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                HandleTouch(touchscreen);
            }

            var mouse = Mouse.current;
            if (mouse != null && (touchscreen == null || !touchscreen.primaryTouch.press.isPressed))
            {
                HandleMouse(mouse);
            }
        }

        private void HandleMouse(Mouse mouse)
        {
            var position = mouse.position.ReadValue();

            if (mouse.rightButton.wasPressedThisFrame && _ui != null)
            {
                if (_ui.IsPlacementActive)
                {
                    _ui.CancelBuildingPlacement();
                }

                if (_ui.IsMovingBuilding)
                {
                    _ui.CancelBuildingMove();
                    _pressBuilding = null;
                }
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (!IsPointerOverUi(position))
                {
                    _mouseTracking = true;
                    _mouseDragged = false;
                    _mousePressPosition = position;
                    _mouseLastPosition = position;
                    BeginPress(position);
                }
            }

            if (_mouseTracking && mouse.leftButton.isPressed)
            {
                if (!UpdateHeldBuilding(position, _mouseDragged))
                {
                    var delta = position - _mouseLastPosition;
                    if (!_mouseDragged)
                    {
                        _mouseDragged = (position - _mousePressPosition).sqrMagnitude >
                                        TapThresholdPixels * TapThresholdPixels;
                    }

                    if (_mouseDragged && delta.sqrMagnitude > 0f)
                    {
                        _cameraRig.PanScreen(delta);
                    }
                }

                _mouseLastPosition = position;
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (EndPress(position))
                {
                    // A held building was dropped; that's not a tap.
                }
                else if (_mouseTracking && !_mouseDragged && !IsPointerOverUi(position))
                {
                    HandleWorldTap(position);
                }

                _mouseTracking = false;
                _mouseDragged = false;
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _cameraRig.ZoomWheel(scroll, position);
            }
        }

        private void HandleTouch(Touchscreen touchscreen)
        {
            var touches = touchscreen.touches;
            var pressedTouches = CountPressedTouches(touches);

            if (pressedTouches >= 2)
            {
                // A second finger means zoom, never a move: put any held building back.
                _pressBuilding = null;
                if (_ui != null && _ui.IsMovingBuilding)
                {
                    _ui.CancelBuildingMove();
                }

                UpdatePinch(touches);
                return;
            }

            if (pressedTouches == 0)
            {
                if (EndPress(_touchLastPosition))
                {
                    // A held building was dropped; that's not a tap.
                }
                else if (_touchTracking && !_touchDragged && !_touchPinching && !_touchBlockedByUi)
                {
                    HandleWorldTap(_touchLastPosition);
                }

                _touchTracking = false;
                _touchDragged = false;
                _touchBlockedByUi = false;
                _touchPinching = false;
                _awaitTouchRelease = false;
                return;
            }

            var primaryTouch = touchscreen.primaryTouch;
            var position = primaryTouch.position.ReadValue();

            if (_awaitTouchRelease || _touchPinching)
            {
                _touchLastPosition = position;
                return;
            }

            if (!_touchTracking && primaryTouch.press.wasPressedThisFrame)
            {
                _touchBlockedByUi = IsPointerOverUi(position);
                if (!_touchBlockedByUi)
                {
                    _touchTracking = true;
                    _touchDragged = false;
                    _touchPressPosition = position;
                    _touchLastPosition = position;
                    BeginPress(position);
                }
            }

            if (_touchTracking && primaryTouch.press.isPressed)
            {
                if (!UpdateHeldBuilding(position, _touchDragged))
                {
                    var delta = position - _touchLastPosition;
                    if (!_touchDragged)
                    {
                        _touchDragged = (position - _touchPressPosition).sqrMagnitude >
                                        TouchTapThresholdPixels * TouchTapThresholdPixels;
                    }

                    if (_touchDragged && delta.sqrMagnitude > 0f)
                    {
                        _cameraRig.PanScreen(delta);
                    }
                }

                _touchLastPosition = position;
            }
        }

        // ------------------------------------------------------------------ Press and hold to move

        /// <summary>Notes the building under a new press, if any; holding it still picks it up.</summary>
        private void BeginPress(Vector2 screenPosition)
        {
            _pressBuilding = null;
            _pressTime = Time.unscaledTime;
            if (_ui == null || _ui.IsPlacementActive || _ui.IsMovingBuilding)
            {
                return;
            }

            if (_farmView.TryGetBuildingAtWorld(ScreenToWorld(screenPosition), out var building))
            {
                _pressBuilding = building;
            }
        }

        /// <summary>
        /// Called every frame while the pointer is down. Picks up the pressed building once it has
        /// been held still long enough, then moves it with the pointer. Returns true while a
        /// building is held (the camera shouldn't pan).
        /// </summary>
        private bool UpdateHeldBuilding(Vector2 screenPosition, bool dragged)
        {
            if (_ui == null)
            {
                return false;
            }

            if (_ui.IsMovingBuilding)
            {
                _ui.UpdateBuildingMove(screenPosition);
                return true;
            }

            if (_pressBuilding == null)
            {
                return false;
            }

            if (dragged)
            {
                // The pointer moved off before the hold finished: this is a pan.
                _pressBuilding = null;
                return false;
            }

            if (Time.unscaledTime - _pressTime < HoldToMoveSeconds)
            {
                return false;
            }

            var building = _pressBuilding;
            _pressBuilding = null;
            return _ui.BeginBuildingMove(building, screenPosition);
        }

        /// <summary>Ends a press. Returns true if it dropped a held building.</summary>
        private bool EndPress(Vector2 screenPosition)
        {
            _pressBuilding = null;
            if (_ui == null || !_ui.IsMovingBuilding)
            {
                return false;
            }

            _ui.EndBuildingMove(screenPosition);
            return true;
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            return _camera.ScreenToWorldPoint(new Vector3(
                screenPosition.x,
                screenPosition.y,
                -_camera.transform.position.z));
        }

        private void UpdatePinch(IReadOnlyList<UnityEngine.InputSystem.Controls.TouchControl> touches)
        {
            if (!_touchPinching)
            {
                _touchPinching = true;
                _touchDragged = true;
                _awaitTouchRelease = true;
                _pinchLastDistance = 0f;
            }

            var first = FindFirstPressed(touches, -1);
            var second = FindFirstPressed(touches, first);
            if (first < 0 || second < 0)
            {
                return;
            }

            var firstPosition = touches[first].position.ReadValue();
            var secondPosition = touches[second].position.ReadValue();
            var distance = Vector2.Distance(firstPosition, secondPosition);
            var midpoint = (firstPosition + secondPosition) * 0.5f;

            if (_pinchLastDistance > 0.01f)
            {
                _cameraRig.PanScreen(midpoint - _pinchLastMidpoint);
                _cameraRig.ZoomPinch(_pinchLastDistance, distance, midpoint);
            }

            _pinchLastMidpoint = midpoint;
            _pinchLastDistance = distance;
            _awaitTouchRelease = true;
        }

        public void HandleWorldTap(Vector2 screenPosition)
        {
            if (_camera == null || _farmView == null || _ui == null || _session == null)
            {
                return;
            }

            if (_ui.HandlePlacementTap(screenPosition))
            {
                return;
            }

            _ui.CloseFeedGrinder();
            _ui.CloseAnimalPen();

            var world = _camera.ScreenToWorldPoint(new Vector3(
                screenPosition.x,
                screenPosition.y,
                -_camera.transform.position.z));

            // A second tap on the deed icon's land opens the window; any other tap puts it away.
            if (_farmView.IsOverLandSign(world))
            {
                _ui.ShowLandWindow(_farmView.LandSignSection);
                return;
            }

            _farmView.HideLandSign();

            if (_farmView.IsCocktailBarAtWorld(world))
            {
                _ui.ShowCocktailBar();
                return;
            }

            if (_farmView.IsDockAtWorld(world))
            {
                _ui.GoToOpenSea();
                return;
            }

            // Visitors stand in front of everything, so they get the tap first.
            if (_farmView.TryGetVisitorAtWorld(world, out var visitorId))
            {
                _ui.HideCropShop();
                _ui.HideScythe();
                _ui.ShowVisitor(visitorId);
                return;
            }

            if (_farmView.TryGetBuildingAtWorld(world, out var building))
            {
                _ui.HideCropShop();
                _ui.HideScythe();
                if (building.Definition.Id == GameSession.NoticeBoardBuildingId)
                {
                    _ui.ShowNoticeBoard();
                }
                else if (building.Definition.Id == GameSession.FeedGrinderBuildingId ||
                    building.Definition.Category == BuildingCategory.Goods)
                {
                    _ui.ShowFeedGrinder(building);
                }
                else if (building.Definition.CanHouseAnimals)
                {
                    _ui.ShowAnimalPen(building);
                }
                else if (building.Definition.IsFruitTree)
                {
                    _session.TryHarvestFruit(building, out var fruitMessage);
                    _ui.ShowToast(fruitMessage);
                }
                else
                {
                    _ui.ShowToast($"{building.Definition.DisplayName} is not producing yet.");
                }

                return;
            }

            if (!_farmView.TryGetPlotAtWorld(world, out var plotId))
            {
                _ui.HideCropShop();
                if (_farmView.TryGetLockedSectionAtWorld(world, out var section))
                {
                    _ui.TapLockedLand(section);
                }

                return;
            }

            var snapshot = _session.Farm.GetSnapshot(plotId);

            if (snapshot.IsEmpty)
            {
                _ui.HideScythe();
                _ui.ShowCropShop();
                return;
            }

            _ui.HideCropShop();

            if (snapshot.IsReady)
            {
                _ui.ShowScythe(plotId);
                return;
            }

            _ui.HideScythe();
            _ui.ShowToast(
                $"{snapshot.Crop.DisplayName} ready in {FarmSimulation.FormatTime(snapshot.RemainingSeconds)}");
        }

        private void HandlePlacementKeys()
        {
            if (_ui == null || !_ui.IsPlacementActive || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _ui.CancelBuildingPlacement();
            }
        }

        private void HandleDebugKeys()
        {
            if (!Application.isEditor || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                _session.DebugAdvanceAll(60d);
                _ui.ShowToast("Debug: crops advanced by 60 seconds.");
            }
        }

        private bool IsPointerOverUi(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (_pointerEventData == null)
            {
                _pointerEventData = new PointerEventData(eventSystem);
            }

            _pointerEventData.position = screenPosition;
            UiRaycastResults.Clear();
            eventSystem.RaycastAll(_pointerEventData, UiRaycastResults);
            return UiRaycastResults.Count > 0;
        }

        private static int CountPressedTouches(IReadOnlyList<UnityEngine.InputSystem.Controls.TouchControl> touches)
        {
            var count = 0;
            for (var i = 0; i < touches.Count; i++)
            {
                if (touches[i].press.isPressed)
                {
                    count++;
                }
            }

            return count;
        }

        private static int FindFirstPressed(
            IReadOnlyList<UnityEngine.InputSystem.Controls.TouchControl> touches,
            int skipIndex)
        {
            for (var i = 0; i < touches.Count; i++)
            {
                if (i != skipIndex && touches[i].press.isPressed)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
