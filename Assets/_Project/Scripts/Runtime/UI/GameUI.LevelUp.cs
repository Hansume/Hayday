using System.Collections.Generic;
using FarmGame.Content;
using UnityEngine;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>
    /// The level-up window: pops up whenever the player reaches a new level and shows what that
    /// level unlocked (buildings, crops and recipes) as cards. Gaining several levels at once, or
    /// levelling up again while it's open, folds everything into one window. With nothing new,
    /// it says when the next unlock comes. "Shop" opens the building shop on the tab of
    /// the first unlocked building.
    /// </summary>
    public sealed partial class GameUI
    {
        private const float LevelUpCardWidth = 220f;
        private const float LevelUpCardHeight = 250f;

        private GameObject _levelUpOverlay;
        private Text _levelUpBadgeText;
        private UIPunch _levelUpBadgePunch;
        private RectTransform _levelUpCardsRoot;
        private ScrollRect _levelUpScroll;
        private Text _levelUpEmptyText;
        private Text _levelUpNextText;
        private Button _levelUpShopButton;
        private Button _levelUpContinueButton;

        private int _levelUpFirstLevel;
        private int _levelUpLastLevel;
        private UnlockInfo _levelUpShopTarget;

        public bool IsLevelUpVisible => _levelUpOverlay != null && _levelUpOverlay.activeSelf;

        /// <summary>The unlock cards currently shown (for tests and tutorials).</summary>
        public int LevelUpUnlockCount { get; private set; }

        private void BuildLevelUpWindow()
        {
            var panel = UIFactory.CreateWindow(
                _canvasRect,
                "LevelUpOverlay",
                "LevelUpPanel",
                "Level Up!",
                new Vector2(980f, 790f),
                CloseLevelUp,
                out _);
            _levelUpOverlay = panel.parent.gameObject;

            // Layout, top to bottom (Place puts the pivot on the anchor, so a top-anchored y is
            // the element's top edge): badge, unlock cards; then, from
            // the bottom, the buttons and the "next unlock" line.

            // A gold medal with the new level, on a soft glow.
            var glow = UIFactory.CreateDecor("BadgeGlow", panel, UIFactory.CircleSprite, new Color(1f, 0.93f, 0.6f, 0.55f));
            UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(200f, 200f));

            var badge = UIFactory.CreateDecor("LevelBadge", panel, UIFactory.CircleSprite, UIFactory.Gold);
            UIFactory.Place(badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -127f), new Vector2(150f, 150f));
            UIFactory.AddOutline(badge, UIFactory.OutlineDark, 3f);
            _levelUpBadgeText = UIFactory.CreateTitleText("LevelText", badge.transform, "2", 72);
            UIFactory.Stretch(_levelUpBadgeText.rectTransform);
            _levelUpBadgePunch = badge.gameObject.AddComponent<UIPunch>();

            var well = UIFactory.CreateWell("UnlocksWell", panel);
            UIFactory.Place(well.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -316f), new Vector2(900f, 290f));

            _levelUpCardsRoot = UIFactory.CreateHorizontalScroll(
                "UnlocksScroll",
                well.transform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(870f, LevelUpCardHeight + 20f),
                18f,
                out _levelUpScroll);

            _levelUpEmptyText = UIFactory.CreateText("NothingNew", well.transform, string.Empty, 36, UIFactory.InkSoft, TextAnchor.MiddleCenter);
            _levelUpEmptyText.fontStyle = FontStyle.Bold;
            UIFactory.Stretch(_levelUpEmptyText.rectTransform, 40f, 20f, 40f, 20f);

            _levelUpNextText = UIFactory.CreateText("NextUnlock", panel, string.Empty, 28, UIFactory.InkSoft, TextAnchor.MiddleCenter);
            UIFactory.Place(_levelUpNextText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 138f), new Vector2(880f, 32f));

            _levelUpShopButton = UIFactory.CreateButton("LevelUpShopButton", panel, "Shop", new Vector2(300f, 92f), 36, ButtonStyle.Secondary);
            _levelUpShopButton.onClick.AddListener(OpenShopFromLevelUp);

            _levelUpContinueButton = UIFactory.CreateButton("LevelUpContinueButton", panel, "OK", new Vector2(300f, 92f), 36, ButtonStyle.Primary);
            _levelUpContinueButton.onClick.AddListener(CloseLevelUp);
        }

        /// <summary>
        /// Shows (or extends) the level-up window for <paramref name="level"/>. If it's already
        /// open, the new level's unlocks are added to the ones on show.
        /// </summary>
        public void ShowLevelUp(int level)
        {
            if (_levelUpOverlay == null)
            {
                return;
            }

            if (IsLevelUpVisible && level > _levelUpLastLevel)
            {
                _levelUpLastLevel = level;
            }
            else
            {
                _levelUpFirstLevel = level;
                _levelUpLastLevel = level;
            }

            RefreshLevelUp();

            // Above every other window, but keep toasts readable on top.
            _levelUpOverlay.transform.SetAsLastSibling();
            if (_toastRect != null && _toastRect.parent != null)
            {
                // Toasts live in their own full-screen layer; that layer goes back on top.
                _toastRect.parent.SetAsLastSibling();
            }

            _levelUpOverlay.SetActive(true);
            _levelUpBadgePunch.Play();
        }

        private void RefreshLevelUp()
        {
            var content = _session.Content;
            var level = _levelUpLastLevel;

            _levelUpBadgeText.text = level.ToString();

            var unlocks = new List<UnlockInfo>();
            for (var l = _levelUpFirstLevel; l <= _levelUpLastLevel; l++)
            {
                unlocks.AddRange(content.GetUnlockDetailsAt(l));
            }

            ClearLevelUpCards();
            _levelUpShopTarget = null;
            for (var i = 0; i < unlocks.Count; i++)
            {
                CreateLevelUpCard(unlocks[i]);
                if (_levelUpShopTarget == null && unlocks[i].Kind == UnlockKind.Building)
                {
                    _levelUpShopTarget = unlocks[i];
                }
            }

            LevelUpUnlockCount = unlocks.Count;
            if (_levelUpScroll != null)
            {
                _levelUpScroll.horizontalNormalizedPosition = 0f;
            }

            var next = content.GetNextUnlockLevel(level);
            var hasUnlocks = unlocks.Count > 0;
            _levelUpScroll.gameObject.SetActive(hasUnlocks);
            _levelUpEmptyText.gameObject.SetActive(!hasUnlocks);
            _levelUpEmptyText.text = next > 0
                ? $"Next unlock: level {next}"
                : "All unlocked!";

            _levelUpNextText.gameObject.SetActive(hasUnlocks && next > 0);
            _levelUpNextText.text = next > 0 ? $"Next unlock: level {next}" : string.Empty;

            // One centred button, or "Shop" beside it when a building was unlocked.
            var showShop = _levelUpShopTarget != null;
            _levelUpShopButton.gameObject.SetActive(showShop);
            UIFactory.Place(
                _levelUpShopButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(-170f, 36f),
                new Vector2(300f, 92f));
            UIFactory.Place(
                _levelUpContinueButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(showShop ? 170f : 0f, 36f),
                new Vector2(300f, 92f));
        }

        private void CreateLevelUpCard(UnlockInfo unlock)
        {
            var card = UIFactory.CreateCard($"Unlock_{unlock.Kind}_{unlock.Id}", _levelUpCardsRoot);
            card.raycastTarget = false;
            var element = card.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = LevelUpCardWidth;
            element.preferredHeight = LevelUpCardHeight;
            element.flexibleWidth = 0f;

            var spotlight = UIFactory.CreateWell("Spotlight", card.transform);
            UIFactory.Place(spotlight.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(190f, 160f));
            var glow = UIFactory.CreateDecor("Glow", spotlight.transform, UIFactory.CircleSprite, new Color(1f, 0.98f, 0.86f, 0.75f));
            UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
            var icon = UIFactory.CreateIcon("Icon", spotlight.transform, unlock.Icon, new Vector2(150f, 140f));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 140f));
            icon.enabled = unlock.Icon != null;

            var nameText = UIFactory.CreateText("Name", card.transform, unlock.DisplayName, 30, UIFactory.Ink, TextAnchor.MiddleCenter);
            nameText.fontStyle = FontStyle.Bold;
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 22;
            nameText.resizeTextMaxSize = 30;
            UIFactory.Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -182f), new Vector2(204f, 56f));
        }

        private void ClearLevelUpCards()
        {
            if (_levelUpCardsRoot == null)
            {
                return;
            }

            for (var i = _levelUpCardsRoot.childCount - 1; i >= 0; i--)
            {
                var child = _levelUpCardsRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        public void CloseLevelUp()
        {
            if (_levelUpOverlay != null)
            {
                _levelUpOverlay.SetActive(false);
            }
        }

        private void OpenShopFromLevelUp()
        {
            var target = _levelUpShopTarget;
            CloseLevelUp();
            if (target != null)
            {
                OpenBuildingShop(ShopTabFor(target.Category));
            }
        }

        private static BuildingShopTab ShopTabFor(BuildingCategory category)
        {
            switch (category)
            {
                case BuildingCategory.Production:
                    return BuildingShopTab.Production;
                case BuildingCategory.Goods:
                    return BuildingShopTab.Goods;
                case BuildingCategory.Fruit:
                    return BuildingShopTab.Fruit;
                default:
                    return BuildingShopTab.Animals;
            }
        }
    }
}
