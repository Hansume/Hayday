using System;
using System.Collections.Generic;
using FarmGame.Farming;
using UnityEngine;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>
    /// The order board: tapping the board on the farm opens a window of pinned notes, each an
    /// order for up to three crops or products. A "!" bubble floats over the board on the
    /// farm while storage holds everything an order asks for.
    /// </summary>
    public sealed partial class GameUI
    {
        private const int MaxRequirementRows = 3;
        private const float NoticeRefreshInterval = 0.25f;

        private static readonly Color NotePaper = new Color(1f, 0.97f, 0.88f, 1f);
        private static readonly Color NotePaperReady = new Color(0.9f, 1f, 0.84f, 1f);
        private static readonly Color NotePaperEmpty = new Color(1f, 1f, 1f, 0.35f);

        private sealed class RequirementRow
        {
            public GameObject Root;
            public Image Icon;
            public Text Name;
            public Image Fill;
            public Text Count;
        }

        private sealed class NoticeCard
        {
            public Image Paper;
            public GameObject Content;
            public Text Title;
            public RequirementRow[] Rows;
            public Text Coins;
            public Text Xp;
            public Button Give;
            public Text GiveLabel;
            public Button Discard;
            public Text EmptyText;
        }

        private GameObject _noticeOverlay;
        private readonly List<NoticeCard> _noticeCards = new List<NoticeCard>();
        private RectTransform _noticeBubble;
        private float _noticeRefreshTimer;

        public bool IsNoticeBoardVisible => _noticeOverlay != null && _noticeOverlay.activeSelf;

        // ------------------------------------------------------------------ Building the window

        private void BuildNoticeBoard()
        {
            var panel = UIFactory.CreateWindow(
                _canvasRect,
                "NoticeBoardOverlay",
                "NoticeBoardPanel",
                "Order Board",
                new Vector2(1240f, 780f),
                CloseNoticeBoard,
                out _);
            _noticeOverlay = panel.parent.gameObject;

            // A cork-coloured backing for the notes.
            var cork = UIFactory.CreateImage("Cork", panel, UIFactory.SlotSprite, new Color(0.78f, 0.58f, 0.38f, 1f));
            UIFactory.Place(cork.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1170f, 670f));

            for (var i = 0; i < _session.Missions.SlotCount; i++)
            {
                _noticeCards.Add(CreateNoticeCard(cork.transform, i));
            }

            BuildNoticeBubble();
            _session.Missions.Changed += OnMissionsChanged;
        }

        private NoticeCard CreateNoticeCard(Transform parent, int index)
        {
            // Four notes fill the cork; with more slots every note shrinks to fit.
            var slots = _session.Missions.SlotCount;
            var scale = Mathf.Min(1f, 4f / slots);
            var x = (index - (slots - 1) * 0.5f) * 286f * scale;
            var tilt = index % 2 == 0 ? 1.2f : -1f;

            var paper = UIFactory.CreateImage($"Notice_{index}", parent, UIFactory.CardSprite, NotePaper);
            paper.raycastTarget = true;
            UIFactory.Place(paper.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, -4f), new Vector2(270f, 630f));
            paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            paper.rectTransform.localScale = new Vector3(scale, scale, 1f);

            var pin = UIFactory.CreateDecor("Pin", paper.transform, UIFactory.CircleSprite, UIFactory.Berry);
            UIFactory.Place(pin.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(30f, 30f));
            UIFactory.AddOutline(pin, UIFactory.OutlineDark, 2f);

            var content = UIFactory.CreateRect("Content", paper.transform);
            UIFactory.Stretch(content);

            // Heading, e.g. "Dairy order".
            var title = UIFactory.CreateText("Title", content, string.Empty, 28, UIFactory.Ink, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(250f, 34f));

            var rows = new RequirementRow[MaxRequirementRows];
            for (var r = 0; r < MaxRequirementRows; r++)
            {
                rows[r] = CreateRequirementRow(content, r);
            }

            // Reward.
            var coin = UIFactory.CreateIcon("CoinIcon", content, UIFactory.Icon("icon_coin"), new Vector2(40f, 40f));
            UIFactory.Place(coin.rectTransform, new Vector2(0.5f, 0f), new Vector2(-96f, 166f), new Vector2(40f, 40f));
            var coins = UIFactory.CreateText("Coins", content, string.Empty, 30, UIFactory.Ink, TextAnchor.MiddleLeft);
            coins.fontStyle = FontStyle.Bold;
            UIFactory.Place(coins.rectTransform, new Vector2(0.5f, 0f), new Vector2(-26f, 166f), new Vector2(96f, 38f));
            var xpBadge = UIFactory.CreateDecor("XpBadge", content, UIFactory.CircleSprite, UIFactory.Gold);
            UIFactory.Place(xpBadge.rectTransform, new Vector2(0.5f, 0f), new Vector2(40f, 166f), new Vector2(40f, 40f));
            var xpLabel = UIFactory.CreateTitleText("XpLabel", xpBadge.transform, "XP", 20);
            UIFactory.Stretch(xpLabel.rectTransform);
            var xp = UIFactory.CreateText("Xp", content, string.Empty, 30, UIFactory.Ink, TextAnchor.MiddleLeft);
            xp.fontStyle = FontStyle.Bold;
            UIFactory.Place(xp.rectTransform, new Vector2(0.5f, 0f), new Vector2(102f, 166f), new Vector2(80f, 38f));

            var give = UIFactory.CreateButton("GiveButton", content, "Give", new Vector2(226f, 76f), 30, ButtonStyle.Primary);
            UIFactory.Place(give.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(226f, 76f));
            var discard = UIFactory.CreateButton("DiscardButton", content, "Discard", new Vector2(190f, 56f), 24, ButtonStyle.Secondary);
            UIFactory.Place(discard.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(190f, 56f));

            var slot = index;
            give.onClick.AddListener(() => GiveOrder(slot));
            discard.onClick.AddListener(() => DiscardOrder(slot));

            var empty = UIFactory.CreateText("Empty", paper.transform, string.Empty, 26, UIFactory.InkSoft, TextAnchor.MiddleCenter);
            empty.fontStyle = FontStyle.Bold;
            UIFactory.Place(empty.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 140f));

            return new NoticeCard
            {
                Paper = paper,
                Content = content.gameObject,
                Title = title,
                Rows = rows,
                Coins = coins,
                Xp = xp,
                Give = give,
                GiveLabel = give.GetComponentInChildren<Text>(),
                Discard = discard,
                EmptyText = empty
            };
        }

        private static RequirementRow CreateRequirementRow(Transform parent, int index)
        {
            var root = UIFactory.CreateRect($"Requirement_{index}", parent);
            UIFactory.Place(root, new Vector2(0.5f, 1f), new Vector2(0f, -84f - index * 106f), new Vector2(246f, 100f));

            var well = UIFactory.CreateWell("IconWell", root);
            UIFactory.Place(well.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(84f, 84f));
            var icon = UIFactory.CreateIcon("Icon", well.transform, null, new Vector2(70f, 70f));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));

            var name = UIFactory.CreateText("Name", root, string.Empty, 24, UIFactory.Ink, TextAnchor.MiddleLeft);
            name.fontStyle = FontStyle.Bold;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 20;
            name.resizeTextMaxSize = 24;
            UIFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(94f, 22f), new Vector2(152f, 56f));

            var fill = UIFactory.CreateProgressBar("Progress", root, new Vector2(152f, 30f), UIFactory.Green);
            UIFactory.Place(fill.transform.parent as RectTransform, new Vector2(0f, 0.5f), new Vector2(94f, -24f), new Vector2(152f, 30f));
            var count = UIFactory.CreateTitleText("Count", fill.transform.parent, string.Empty, 24);
            UIFactory.Stretch(count.rectTransform);

            return new RequirementRow { Root = root.gameObject, Icon = icon, Name = name, Fill = fill, Count = count };
        }

        /// <summary>A "!" bubble that floats over the board on the farm while an order can be filled.</summary>
        private void BuildNoticeBubble()
        {
            var bubble = UIFactory.CreateDecor("NoticeBubble", _canvasRect, UIFactory.CircleSprite, UIFactory.Gold);
            bubble.rectTransform.sizeDelta = new Vector2(64f, 64f);
            UIFactory.AddOutline(bubble, UIFactory.OutlineDark, 3f);
            var mark = UIFactory.CreateTitleText("Mark", bubble.transform, "!", 44);
            UIFactory.Stretch(mark.rectTransform);
            bubble.gameObject.AddComponent<UIPunch>();
            _noticeBubble = bubble.rectTransform;
            _noticeBubble.SetAsFirstSibling();
            _noticeBubble.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Opening and actions

        public void ShowNoticeBoard()
        {
            if (_noticeOverlay == null)
            {
                return;
            }

            CloseInventory();
            CloseBuildingShop();
            CloseFeedGrinder();
            CloseAnimalPen();
            HideCropShop();
            HideScythe();
            if (IsPlacementActive)
            {
                CancelBuildingPlacement();
            }

            _session.UpdateMissions();
            RefreshNoticeBoard();
            _noticeOverlay.SetActive(true);
        }

        public void CloseNoticeBoard()
        {
            if (_noticeOverlay != null)
            {
                _noticeOverlay.SetActive(false);
            }
        }

        private void GiveOrder(int index)
        {
            _session.TryDeliverMission(index, out var message);
            ShowToast(message);
            RefreshNoticeBoard();
        }

        private void DiscardOrder(int index)
        {
            _session.TryDiscardMission(index, out var message);
            ShowToast(message);
            RefreshNoticeBoard();
        }

        private void OnMissionsChanged()
        {
            if (IsNoticeBoardVisible)
            {
                RefreshNoticeBoard();
            }
        }

        // ------------------------------------------------------------------ Per-frame updates

        /// <summary>Posts orders, keeps the refill countdowns live and moves the bubble.</summary>
        private void UpdateNoticeBoard()
        {
            _noticeRefreshTimer -= Time.unscaledDeltaTime;
            if (_noticeRefreshTimer <= 0f)
            {
                _noticeRefreshTimer = NoticeRefreshInterval;
                _session.UpdateMissions();
                if (IsNoticeBoardVisible)
                {
                    RefreshNoticeBoard();
                }
            }

            UpdateNoticeBubble();
        }

        private void UpdateNoticeBubble()
        {
            if (_noticeBubble == null)
            {
                return;
            }

            var board = _session.NoticeBoard;
            var show = board != null && !IsNoticeBoardVisible && _session.HasDeliverableMission;
            if (show)
            {
                var definition = board.Definition;
                var height = definition.WorldSprite != null
                    ? definition.WorldSprite.bounds.size.y * _farmView.GetBuildingVisualScale(definition)
                    : 1f;
                var world = _farmView.GetBuildingWorldPosition(definition, board.Origin) + Vector3.up * (height * 0.9f + 0.25f);
                var screen = _camera.WorldToScreenPoint(world);
                show = screen.z > 0f;
                if (show)
                {
                    var bob = Mathf.Sin(Time.unscaledTime * 3f) * 6f;
                    _noticeBubble.anchoredPosition = ScreenToCanvasPoint(screen) + new Vector2(0f, bob);
                    _noticeBubble.anchorMin = _noticeBubble.anchorMax = new Vector2(0.5f, 0.5f);
                }
            }

            if (_noticeBubble.gameObject.activeSelf != show)
            {
                _noticeBubble.gameObject.SetActive(show);
            }
        }

        private void RefreshNoticeBoard()
        {
            var missions = _session.Missions;
            var now = _session.UtcNow.Ticks;

            for (var i = 0; i < _noticeCards.Count; i++)
            {
                var card = _noticeCards[i];
                var mission = missions.GetSlot(i);
                card.Content.SetActive(mission != null);
                card.EmptyText.gameObject.SetActive(mission == null);

                if (mission == null)
                {
                    card.Paper.color = NotePaperEmpty;
                    var wait = Math.Max(0d, (missions.GetNextPostAt(i) - now) / (double)TimeSpan.TicksPerSecond);
                    card.EmptyText.text = $"New order in\n{FarmSimulation.FormatTime(wait)}";
                    continue;
                }

                var ready = _session.CanDeliverMission(mission);
                card.Paper.color = ready ? NotePaperReady : NotePaper;
                card.Title.text = missions.GetTitle(mission);

                for (var r = 0; r < card.Rows.Length; r++)
                {
                    var row = card.Rows[r];
                    var has = r < mission.Requirements.Count;
                    row.Root.SetActive(has);
                    if (!has)
                    {
                        continue;
                    }

                    var requirement = mission.Requirements[r];
                    var have = _session.Inventory.GetCount(requirement.ItemId);
                    row.Icon.sprite = LoadItemIcon(requirement.ItemId);
                    row.Name.text = _session.Content.GetItemDisplayName(requirement.ItemId);
                    row.Fill.fillAmount = Mathf.Clamp01(have / (float)requirement.Required);
                    row.Fill.color = have >= requirement.Required ? UIFactory.GreenDark : UIFactory.Green;
                    row.Count.text = $"{UIFactory.FormatNumber(have)} / {UIFactory.FormatNumber(requirement.Required)}";
                }

                card.Coins.text = UIFactory.FormatNumber(mission.RewardCoins);
                card.Xp.text = UIFactory.FormatNumber(mission.RewardXp);
                card.Give.interactable = ready;
                UIFactory.SetButtonStyle(card.Give, ready ? ButtonStyle.Primary : ButtonStyle.Muted);
                card.GiveLabel.text = ready ? "Give" : "Not enough";
            }
        }
    }
}
