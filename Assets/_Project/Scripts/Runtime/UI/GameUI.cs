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
    public sealed partial class GameUI : MonoBehaviour
    {
        private const string FishPenBackgroundPath = "Art/Placeholders/pen_underwater";
        private const string CoopPenBackgroundPath = "Art/Placeholders/pen_coop";
        private const string StablePenBackgroundPath = "Art/Placeholders/pen_stable";
        private const string GoatPenBackgroundPath = "Art/Placeholders/pen_goat";

        private sealed class CropCard
        {
            public string CropId;
            public Image Background;
            public Text Name;
            public Text Cost;
            public Outline Selection;
            public int UnlockLevel;
            public CanvasGroup Group;
            public GameObject LockBadge;
        }

        private sealed class BuildingCard
        {
            public BuildingDefinition Building;
            public Text Owned;
            public Button BuyButton;
            public Text BuyLabel;
        }

        private sealed class AnimalVisual
        {
            public RectTransform Rect;
            public AnimalKind Kind;
            public Vector2 Home;
            public Vector2 Velocity;
            public float Phase;
            public float Scale = 1f;
            public float TurnTimer;

            // The bubble over a fed animal: its timer or its product. Hungry animals show none.
            public int Index;
            public Vector2 Size;
            public RectTransform StatusRoot;
            public Image StatusBackground;
            public Image StatusIcon;
            public Text StatusLabel;
        }

        private enum BuildingShopTab
        {
            Slot,
            Animals,
            Production,
            Goods,
            Fruit
        }

        private enum PlacementMode
        {
            None,
            Building,
            Slot
        }

        private GameSession _session;
        private FarmView _farmView;
        private Camera _camera;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _safeAreaRect;
        private Text _coinsText;
        private UIPunch _coinPunch;
        private int _lastCoins = -1;
        private Text _levelText;
        private Text _xpText;
        private Image _xpFill;
        private UIPunch _levelPunch;
        private Text _hintText;
        private GameObject _cropDock;
        private GameObject _inventoryOverlay;
        private RectTransform _inventoryRows;
        private ScrollRect _inventoryScroll;
        private Text _storageText;
        private Image _storageFill;
        private Button _storageUpgradeButton;
        private Text _storageUpgradeLabel;
        private Image _storageUpgradeCoin;
        private GameObject _buildingShopOverlay;
        private RectTransform _buildingShopCardsRoot;
        private ScrollRect _buildingShopScroll;
        private Button _slotTabButton;
        private Button _animalsTabButton;
        private Button _productionTabButton;
        private Button _goodsTabButton;
        private Button _fruitTabButton;
        private BuildingShopTab _selectedBuildingShopTab = BuildingShopTab.Slot;
        private GameObject _feedGrinderOverlay;
        private RectTransform _feedGrinderRows;
        private ScrollRect _feedGrinderScroll;
        private Text _feedQueueTitle;
        private int _feedQueueTitleCount = -1;
        private Text[] _feedQueueNames;
        private Text[] _feedQueueStatuses;
        private Image[] _feedQueueIcons;
        private Image[] _feedQueueFills;
        private Image[] _feedQueueSlots;
        private Button _feedCollectButton;
        private PlacedBuilding _activeGrinder;
        private GameObject _animalPenOverlay;
        private RectTransform _animalPenStage;
        private RectTransform _animalPenAnimals;
        private Image _animalPenBackground;
        private Text _animalPenTitle;
        private Text _animalPenCountText;
        private Button _animalPenAddButton;
        private Text _animalPenAddLabel;
        private PlacedBuilding _activeAnimalBuilding;
        private RectTransform _animalPenStatusRoot;
        private float _animalPenTime;
        private readonly List<AnimalVisual> _animalPenVisuals = new List<AnimalVisual>();
        private GameObject _animalFeedTray;
        private Image _animalFeedIcon;
        private Text _animalFeedCount;
        private AnimalVisual _animalFeedTarget;
        private GameObject _placementBanner;
        private Text _placementBannerText;
        private GameObject _placementPreviewRoot;
        private SpriteRenderer _placementPreviewRenderer;
        private GameObject _placementHighlightRoot;
        private Sprite _placementTileSprite;
        private Sprite _slotPreviewSprite;
        private PlacementMode _placementMode;
        private BuildingDefinition _placingBuilding;
        private Vector2Int _placementOrigin;
        private bool _placementValid;
        private string _placementReason;
        private const float ToastRestY = -10f;
        private CanvasGroup _toastGroup;
        private RectTransform _toastRect;
        private Text _toastText;
        private Coroutine _toastRoutine;
        private string _selectedCropId;

        private readonly List<CropCard> _cropCards = new List<CropCard>();
        private readonly List<BuildingCard> _buildingShopCards = new List<BuildingCard>();
        private readonly List<SpriteRenderer> _placementCellHighlights = new List<SpriteRenderer>();

        /// <summary>What each live countdown label shows now, so it's only rebuilt when that changes.</summary>
        private readonly Dictionary<Text, int> _countdownShown = new Dictionary<Text, int>();

        /// <summary>
        /// Shows a countdown ("1:05", or "Wait 1:05" with <paramref name="queued"/>). Countdowns
        /// are refreshed every frame, but the text (a new string) is only built once a second.
        /// </summary>
        private void SetCountdownText(Text label, double seconds, bool queued = false, string prefix = null)
        {
            var key = Mathf.Max(0, Mathf.CeilToInt((float)seconds)) * 2 + (queued ? 1 : 0);
            if (_countdownShown.TryGetValue(label, out var shown) && shown == key)
            {
                return;
            }

            _countdownShown[label] = key;
            var time = FarmSimulation.FormatTime(seconds);
            label.text = queued ? $"Wait {time}" : prefix + time;
        }

        /// <summary>Sets a label that may also show a countdown at other times.</summary>
        private void SetPlainText(Text label, string text)
        {
            _countdownShown.Remove(label);
            label.text = text;
        }
        private readonly HashSet<int> _cropDragPlanted = new HashSet<int>();
        private readonly HashSet<int> _scytheHarvested = new HashSet<int>();
        private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();

        private RectTransform _cropGhostRoot;
        private Image _cropGhostIcon;
        private Image _dropHighlight;
        private RectTransform _scytheRoot;
        private GameObject _cropDragSource;
        private PointerEventData _uiPointerData;
        private string _draggedCropId;
        private Vector2 _cropDragLastScreenPosition;
        private bool _cropDragBlockedByCoins;
        private int _scythePlotId = -1;
        private bool _scytheDragging;
        private bool _scytheTutorialShown;
        private Vector2 _scytheLastScreenPosition;

        public bool IsBuildingPlacementActive => _placementMode == PlacementMode.Building;
        public bool IsPlacementActive => _placementMode != PlacementMode.None;

        public void Initialize(GameSession session, FarmView farmView, Camera camera)
        {
            _session = session;
            _farmView = farmView;
            _camera = camera;

            EnsureEventSystem();
            BuildCanvas();
            BuildTopBar();
            BuildCropDock();
            BuildInventory();
            BuildBuildingShop();
            BuildFeedGrinder();
            BuildAnimalPen();
            BuildNoticeBoard();
            BuildVisitorUi();
            BuildLandWindow();
            BuildCocktailBarUi();
            BuildLevelUpWindow();
            BuildToast();
            BuildInteractionVisuals();
            BuildBuildingPlacementVisuals();

            _session.Wallet.Changed += OnWalletChanged;
            OnWalletChanged(_session.Wallet.Coins);
            _session.Level.Changed += OnLevelChanged;
            _session.Level.LeveledUp += OnLeveledUp;
            OnLevelChanged(_session.Level.Level, _session.Level.Xp);

            var firstCrop = FirstUnlockedCrop();
            if (firstCrop != null)
            {
                SelectCrop(firstCrop.Id);
            }
        }

        private void Update()
        {
            if (_session == null)
            {
                return;
            }

            UpdateScythe();
            UpdateBuildingPlacement();
            UpdateAnimalPen();
            UpdateFeedQueue();
            UpdateNoticeBoard();
            UpdateVisitorUi();
            UpdateCocktailBarUi();
            HandleEscapeToClose();
        }

        private void HandleEscapeToClose()
        {
            if (IsPlacementActive || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (IsMovingBuilding)
            {
                CancelBuildingMove();
                return;
            }

            if (IsLevelUpVisible)
            {
                CloseLevelUp();
                return;
            }

            if (IsSellDialogVisible)
            {
                CloseSellDialog();
                return;
            }

            if (IsVisitorWindowVisible)
            {
                CloseVisitor();
                return;
            }

            CloseLandWindow();
            CloseCocktailBar();
            CloseInventory();
            CloseBuildingShop();
            CloseFeedGrinder();
            CloseAnimalPen();
            CloseNoticeBoard();
            HideCropShop();
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.Wallet.Changed -= OnWalletChanged;
                _session.Level.Changed -= OnLevelChanged;
                _session.Level.LeveledUp -= OnLeveledUp;
                _session.Missions.Changed -= OnMissionsChanged;
                _session.Reputation.Changed -= OnReputationChanged;
                _session.CocktailBar.Changed -= OnCocktailBarChanged;
            }
        }

        private Vector2 ScreenToCanvasPoint(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPosition,
                null,
                out var localPoint);
            return localPoint;
        }

        private bool IsPointerOverBlockingUi(Vector2 screenPosition, GameObject ignored)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (_uiPointerData == null)
            {
                _uiPointerData = new PointerEventData(eventSystem);
            }

            _uiPointerData.position = screenPosition;
            UiRaycastResults.Clear();
            eventSystem.RaycastAll(_uiPointerData, UiRaycastResults);

            for (var i = 0; i < UiRaycastResults.Count; i++)
            {
                var target = UiRaycastResults[i].gameObject;
                if (target == null)
                {
                    continue;
                }

                if (ignored != null &&
                    (target == ignored || target.transform.IsChildOf(ignored.transform)))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public void ShowToast(string message)
        {
            if (_toastGroup == null || _toastText == null)
            {
                return;
            }

            _toastText.text = message;
            _toastGroup.alpha = 1f;

            if (_toastRoutine != null)
            {
                StopCoroutine(_toastRoutine);
            }

            _toastRoutine = StartCoroutine(FadeToast());
        }

        public void ShowFloatingText(Vector3 worldPosition, string message, Color color)
        {
            if (_canvasRect == null || _camera == null)
            {
                return;
            }

            var screenPoint = _camera.WorldToScreenPoint(worldPosition);
            if (screenPoint.z <= 0f)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPoint,
                null,
                out var localPoint);

            var text = UIFactory.CreateText(
                "FloatingText",
                _canvasRect,
                message,
                36,
                color,
                TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            UIFactory.AddOutline(text, UIFactory.OutlineDark, 2f);

            var textRect = text.rectTransform;
            UIFactory.SetAnchored(
                textRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                localPoint,
                new Vector2(420f, 64f));

            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(2f, -2f);

            var group = text.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            StartCoroutine(AnimateFloatingText(textRect, group, localPoint));
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                var eventSystemObject = new GameObject(
                    "EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                var module = eventSystemObject.GetComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
                return;
            }

            if (EventSystem.current.GetComponent<BaseInputModule>() == null)
            {
                var module = EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject(
                "HUD Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            _canvasRect = canvasObject.GetComponent<RectTransform>();

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _safeAreaRect = UIFactory.CreateRect("SafeArea", canvasObject.transform);
            UIFactory.Stretch(_safeAreaRect);
            _safeAreaRect.gameObject.AddComponent<SafeAreaFitter>();
        }

        private void BuildTopBar()
        {
            // The top bar is a transparent strip: taps between its items reach the farm.
            var bar = UIFactory.CreateRect("TopBar", _safeAreaRect);
            UIFactory.SetAnchored(
                bar,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 130f));

            // Coin counter: a dark wooden capsule with a big coin overlapping its left end.
            var coinPill = UIFactory.CreateImage("CoinPill", bar, UIFactory.PillSprite, Color.white);
            coinPill.raycastTarget = true;
            UIFactory.Place(coinPill.rectTransform, new Vector2(0f, 1f), new Vector2(76f, -30f), new Vector2(330f, 78f));

            var coinIcon = UIFactory.CreateIcon(
                "CoinIcon",
                coinPill.transform,
                UIFactory.Icon("icon_coin"),
                new Vector2(104f, 104f));
            UIFactory.Place(coinIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(-50f, 0f), new Vector2(104f, 104f));

            _coinsText = UIFactory.CreateTitleText("CoinCount", coinPill.transform, "0", 40, TextAnchor.MiddleRight);
            UIFactory.Stretch(_coinsText.rectTransform, 64f, 4f, 28f, 2f);
            _coinPunch = coinPill.gameObject.AddComponent<UIPunch>();

            // Level badge: a gold circle with the level number, and an XP bar beside it.
            var levelPill = UIFactory.CreateImage("LevelPill", bar, UIFactory.PillSprite, Color.white);
            levelPill.raycastTarget = true;
            UIFactory.Place(levelPill.rectTransform, new Vector2(0f, 1f), new Vector2(470f, -30f), new Vector2(330f, 78f));

            var levelBadge = UIFactory.CreateDecor("LevelBadge", levelPill.transform, UIFactory.CircleSprite, UIFactory.Gold);
            UIFactory.Place(levelBadge.rectTransform, new Vector2(0f, 0.5f), new Vector2(-40f, 0f), new Vector2(96f, 96f));
            UIFactory.AddOutline(levelBadge, UIFactory.OutlineDark, 3f);
            _levelText = UIFactory.CreateTitleText("LevelNumber", levelBadge.transform, "1", 40);
            UIFactory.Stretch(_levelText.rectTransform);

            _xpFill = UIFactory.CreateProgressBar("XpBar", levelPill.transform, new Vector2(250f, 30f), UIFactory.Green);
            UIFactory.Place(_xpFill.transform.parent as RectTransform, new Vector2(0f, 0.5f), new Vector2(66f, 10f), new Vector2(250f, 30f));
            _xpText = UIFactory.CreateTitleText("XpCount", levelPill.transform, "0 / 10", 24);
            UIFactory.Place(_xpText.rectTransform, new Vector2(0f, 0.5f), new Vector2(66f, -20f), new Vector2(250f, 28f));
            _levelPunch = levelPill.gameObject.AddComponent<UIPunch>();

            BuildReputationCounter(bar);

            // Big round action buttons in the bottom-right corner, like a mobile farm game.
            var actions = UIFactory.CreateRect("ActionButtons", _safeAreaRect);
            UIFactory.Place(actions, new Vector2(1f, 0f), new Vector2(-28f, 28f), new Vector2(170f, 360f));

            var inventoryButton = CreateActionButton(
                "InventoryButton",
                actions,
                "Inventory",
                "icon_inventory",
                ButtonStyle.Secondary,
                new Vector2(0f, 0f));
            inventoryButton.onClick.AddListener(OpenInventory);

            var buildButton = CreateActionButton(
                "BuildingShopButton",
                actions,
                "Shop",
                "icon_build",
                ButtonStyle.Primary,
                new Vector2(0f, 186f));
            buildButton.onClick.AddListener(OpenBuildingShop);

            BuildTravelButton(buildButton);
        }

        private static Button CreateActionButton(
            string name,
            Transform parent,
            string caption,
            string iconName,
            ButtonStyle style,
            Vector2 position)
        {
            var button = UIFactory.CreateButton(name, parent, string.Empty, new Vector2(164f, 164f), 24, style);
            UIFactory.Place(
                button.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                position,
                new Vector2(164f, 164f));

            var icon = UIFactory.CreateIcon("Icon", button.transform, UIFactory.Icon(iconName), new Vector2(104f, 104f));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(104f, 104f));

            var label = UIFactory.CreateTitleText("Caption", button.transform, caption, 26);
            UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(160f, 36f));
            return button;
        }

        private void BuildToast()
        {
            // Messages drop in at the top centre, clear of the crop tray and the action buttons.
            // Its own layer: created last so it draws over windows, and fitted to the safe area
            // so a phone's notch can't cover it.
            var toastLayer = UIFactory.CreateRect("ToastLayer", _canvasRect);
            UIFactory.Stretch(toastLayer);
            toastLayer.gameObject.AddComponent<SafeAreaFitter>();

            var toastImage = UIFactory.CreateDecor("Toast", toastLayer, UIFactory.PillSprite, Color.white);
            UIFactory.Place(toastImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, ToastRestY), new Vector2(640f, 60f));
            _toastRect = toastImage.rectTransform;

            _toastText = UIFactory.CreateTitleText("ToastText", toastImage.transform, string.Empty, 32);
            // Messages are short; the rare long one shrinks to fit.
            _toastText.verticalOverflow = VerticalWrapMode.Truncate;
            _toastText.resizeTextForBestFit = true;
            _toastText.resizeTextMinSize = 24;
            _toastText.resizeTextMaxSize = 32;
            UIFactory.Stretch(_toastText.rectTransform, 30f, 4f, 30f, 2f);

            _toastGroup = toastImage.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;
            _toastGroup.interactable = false;
        }

        private IEnumerator FadeToast()
        {
            // Drop in from just above its resting place.
            var elapsed = 0f;
            const float dropDuration = 0.18f;
            while (elapsed < dropDuration && _toastRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / dropDuration);
                var eased = 1f - (1f - t) * (1f - t);
                _toastRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(ToastRestY + 40f, ToastRestY, eased));
                yield return null;
            }

            yield return new WaitForSecondsRealtime(2.2f);

            elapsed = 0f;
            const float fadeDuration = 0.35f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _toastGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            _toastGroup.alpha = 0f;
            _toastRoutine = null;
        }

        private IEnumerator AnimateFloatingText(RectTransform rect, CanvasGroup group, Vector2 startPosition)
        {
            const float duration = 1.1f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = startPosition + new Vector2(0f, 84f * t);
                group.alpha = 1f - t;
                var scale = Mathf.Lerp(1.08f, 0.92f, t);
                rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            Destroy(rect.gameObject);
        }

        private void OnWalletChanged(int coins)
        {
            if (_coinsText != null)
            {
                _coinsText.text = UIFactory.FormatNumber(coins);
            }

            if (_coinPunch != null && _lastCoins >= 0 && coins != _lastCoins)
            {
                _coinPunch.Play();
            }

            _lastCoins = coins;

            if (IsAnimalPenVisible)
            {
                UpdateAnimalPenControls();
            }
        }

        private void OnLevelChanged(int level, int xp)
        {
            var needed = _session.Level.XpToNextLevel;
            if (_levelText != null)
            {
                _levelText.text = level.ToString();
            }

            if (_xpText != null)
            {
                _xpText.text = needed > 0
                    ? $"{UIFactory.FormatNumber(xp)} / {UIFactory.FormatNumber(needed)} XP"
                    : "MAX";
            }

            if (_xpFill != null)
            {
                _xpFill.fillAmount = _session.Level.Progress;
            }
        }

        private void OnLeveledUp(int level)
        {
            if (_levelPunch != null)
            {
                _levelPunch.Play();
            }

            // The level-up window lists what this level unlocked.
            ShowLevelUp(level);

            // Open windows show what just unlocked.
            RefreshCropLocks();
            if (_buildingShopOverlay != null && _buildingShopOverlay.activeSelf)
            {
                RefreshBuildingShop();
            }

            if (_feedGrinderOverlay != null && _feedGrinderOverlay.activeSelf)
            {
                RebuildFeedGrinderRows();
            }
        }

        private static string FormatCoins(int amount)
        {
            return amount == 1 ? "1 coin" : $"{UIFactory.FormatNumber(amount)} coins";
        }
    }
}
