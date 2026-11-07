using System.Collections.Generic;
using FarmGame.Visitors;
using UnityEngine;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>
    /// Visitors in the HUD:
    /// <list type="bullet">
    /// <item>A reputation counter in the top bar (reputation only comes from visitors).</item>
    /// <item>A bubble over each waiting visitor showing what they want, with a tick when the
    /// player has it all. Tapping the bubble (or the visitor) opens their request.</item>
    /// <item>The request window: who they are, what they want (have / need), how long they'll
    /// wait, the reward, and Give / Send away.</item>
    /// </list>
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>A visitor look's picture (UIFactory.Icon prefers the painted one).</summary>
        private const string VisitorPortraitPrefix = "visitor_";
        private const float VisitorTickInterval = 1f;
        private const float VisitorWindowRefreshInterval = 0.5f;
        private const int MaxRequestRows = 3;

        private sealed class VisitorBubble
        {
            public int VisitorId;
            public RectTransform Root;
            public Image Icon;
            public GameObject Tick;
        }

        /// <summary>One line in the window: something a visitor wants, or a bundle a seller offers.</summary>
        private sealed class VisitorRequestRow
        {
            public GameObject Root;
            public Image Icon;
            public Text Name;
            public Text Count;

            // Seller rows only.
            public Text Price;
            public Text WasPrice;
            public Button Buy;
            public Text BuyLabel;
        }

        // Top bar.
        private Text _reputationText;
        private UIPunch _reputationPunch;

        // Bubbles.
        private RectTransform _visitorBubblesRoot;
        private readonly List<VisitorBubble> _visitorBubbles = new List<VisitorBubble>();
        private float _visitorTickTimer;

        // Window.
        private GameObject _visitorOverlay;
        private Text _visitorTitle;
        private Image _visitorPortrait;
        private Text _visitorLeavesText;
        private readonly List<VisitorRequestRow> _visitorRows = new List<VisitorRequestRow>();
        private Text _visitorCoinsText;
        private Text _visitorXpText;
        private Text _visitorReputationText;
        private Button _visitorGiveButton;
        private Text _visitorGiveLabel;
        private Button _visitorSendAwayButton;
        private Text _visitorSendAwayLabel;
        private Text _visitorWantsLabel;
        private GameObject _visitorRewardGroup;
        private GameObject _visitorDiscountGroup;
        private Text _visitorDiscountText;
        private int _shownVisitorId = -1;
        private float _visitorWindowTimer;

        public bool IsVisitorWindowVisible => _visitorOverlay != null && _visitorOverlay.activeSelf;

        /// <summary>The visitor whose request is open, or -1.</summary>
        public int ShownVisitorId => IsVisitorWindowVisible ? _shownVisitorId : -1;

        // ------------------------------------------------------------------ Building

        /// <summary>A reputation counter in the top bar, after the level badge.</summary>
        private void BuildReputationCounter(RectTransform bar)
        {
            var pill = UIFactory.CreateImage("ReputationPill", bar, UIFactory.PillSprite, Color.white);
            pill.raycastTarget = true;
            UIFactory.Place(pill.rectTransform, new Vector2(0f, 1f), new Vector2(880f, -30f), new Vector2(230f, 78f));

            var icon = UIFactory.CreateIcon("ReputationIcon", pill.transform, UIFactory.Icon("icon_reputation"), new Vector2(96f, 96f));
            UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(-44f, 0f), new Vector2(96f, 96f));

            _reputationText = UIFactory.CreateTitleText("ReputationCount", pill.transform, "0", 40, TextAnchor.MiddleRight);
            UIFactory.Stretch(_reputationText.rectTransform, 60f, 4f, 28f, 2f);
            _reputationPunch = pill.gameObject.AddComponent<UIPunch>();

            _session.Reputation.Changed += OnReputationChanged;
            OnReputationChanged(_session.Reputation.Points);
        }

        private void BuildVisitorUi()
        {
            // Bubbles sit behind every window, like the notice board's.
            _visitorBubblesRoot = UIFactory.CreateRect("VisitorBubbles", _canvasRect);
            UIFactory.Stretch(_visitorBubblesRoot);
            _visitorBubblesRoot.SetAsFirstSibling();

            var panel = UIFactory.CreateWindow(
                _canvasRect,
                "VisitorOverlay",
                "VisitorPanel",
                "Visitor",
                new Vector2(1020f, 740f),
                CloseVisitor,
                out _visitorTitle);
            _visitorOverlay = panel.parent.gameObject;

            // Left: the visitor, standing in a spotlight.
            var well = UIFactory.CreateWell("PortraitWell", panel);
            UIFactory.Place(well.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -110f), new Vector2(270f, 400f));
            var glow = UIFactory.CreateDecor("Glow", well.transform, UIFactory.CircleSprite, new Color(1f, 0.98f, 0.86f, 0.75f));
            UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(220f, 220f));
            _visitorPortrait = UIFactory.CreateIcon("Portrait", well.transform, null, new Vector2(230f, 306f));
            UIFactory.Place(_visitorPortrait.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(230f, 306f));

            // Right: patience, and what they'd like.
            _visitorLeavesText = UIFactory.CreatePill("LeavesIn", panel, string.Empty, 24, new Vector2(300f, 46f), out var leavesPill);
            UIFactory.Place(leavesPill.rectTransform, new Vector2(0f, 1f), new Vector2(350f, -112f), new Vector2(300f, 46f));

            _visitorWantsLabel = UIFactory.CreateSectionLabel("WantsLabel", panel, "Wants");
            UIFactory.Place(_visitorWantsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(352f, -170f), new Vector2(600f, 34f));

            for (var i = 0; i < MaxRequestRows; i++)
            {
                var row = CreateVisitorRequestRow(panel, i);
                var index = i;
                row.Buy.onClick.AddListener(() => BuyFromShownVisitor(index));
                _visitorRows.Add(row);
            }

            // Request visitors: the reward (coins, XP and reputation).
            var rewardGroup = UIFactory.CreateRect("RewardGroup", panel);
            UIFactory.Stretch(rewardGroup);
            _visitorRewardGroup = rewardGroup.gameObject;

            _visitorCoinsText = CreateRewardPill(rewardGroup, "RewardCoins", UIFactory.Icon("icon_coin"), new Vector2(-270f, 150f));
            _visitorXpText = CreateRewardPill(rewardGroup, "RewardXp", null, new Vector2(0f, 150f));
            _visitorReputationText = CreateRewardPill(rewardGroup, "RewardReputation", UIFactory.Icon("icon_reputation"), new Vector2(270f, 150f));

            // Sellers: the reputation discount the player gets.
            var discountGroup = UIFactory.CreateRect("DiscountGroup", panel);
            UIFactory.Stretch(discountGroup);
            _visitorDiscountGroup = discountGroup.gameObject;
            _visitorDiscountText = UIFactory.CreatePill("Discount", discountGroup, string.Empty, 32, new Vector2(300f, 60f), out var discountPill);
            UIFactory.Place(discountPill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(300f, 60f));
            UIFactory.Stretch(_visitorDiscountText.rectTransform, 60f, 2f, 20f, 2f);
            var discountIcon = UIFactory.CreateIcon("Icon", discountPill.transform, UIFactory.Icon("icon_reputation"), new Vector2(76f, 76f));
            UIFactory.Place(discountIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(-26f, 0f), new Vector2(76f, 76f));

            _visitorSendAwayButton = UIFactory.CreateButton("VisitorSendAwayButton", panel, "Send away", new Vector2(330f, 92f), 32, ButtonStyle.Secondary);
            UIFactory.Place(_visitorSendAwayButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-190f, 36f), new Vector2(330f, 92f));
            _visitorSendAwayLabel = _visitorSendAwayButton.GetComponentInChildren<Text>();
            _visitorSendAwayButton.onClick.AddListener(SendAwayShownVisitor);

            _visitorGiveButton = UIFactory.CreateButton("VisitorGiveButton", panel, "Give", new Vector2(330f, 92f), 34, ButtonStyle.Primary);
            UIFactory.Place(_visitorGiveButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(190f, 36f), new Vector2(330f, 92f));
            _visitorGiveLabel = _visitorGiveButton.GetComponentInChildren<Text>();
            _visitorGiveButton.onClick.AddListener(ServeShownVisitor);
        }

        private static VisitorRequestRow CreateVisitorRequestRow(Transform panel, int index)
        {
            var row = UIFactory.CreateWell($"Request{index}", panel);
            UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(350f, -210f - index * 80f), new Vector2(620f, 72f));

            var icon = UIFactory.CreateIcon("Icon", row.transform, null, new Vector2(58f, 58f));
            UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(58f, 58f));

            var name = UIFactory.CreateText("Name", row.transform, string.Empty, 28, UIFactory.Ink, TextAnchor.MiddleLeft);
            name.fontStyle = FontStyle.Bold;
            UIFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(86f, 0f), new Vector2(330f, 50f));

            var count = UIFactory.CreateText("Count", row.transform, string.Empty, 28, UIFactory.GreenDark, TextAnchor.MiddleRight);
            count.fontStyle = FontStyle.Bold;
            UIFactory.Place(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(170f, 50f));

            // Seller rows: the discounted price (with the full price under it when there's a
            // discount) and a Buy button.
            var price = UIFactory.CreateText("Price", row.transform, string.Empty, 28, UIFactory.Ink, TextAnchor.MiddleRight);
            price.fontStyle = FontStyle.Bold;
            UIFactory.Place(price.rectTransform, new Vector2(1f, 0.5f), new Vector2(-162f, 9f), new Vector2(150f, 34f));

            var was = UIFactory.CreateText("WasPrice", row.transform, string.Empty, 22, UIFactory.Muted, TextAnchor.MiddleRight);
            UIFactory.Place(was.rectTransform, new Vector2(1f, 0.5f), new Vector2(-162f, -18f), new Vector2(150f, 24f));

            var buy = UIFactory.CreateButton($"BuyButton{index}", row.transform, "Buy", new Vector2(140f, 58f), 26, ButtonStyle.Gold);
            UIFactory.Place(buy.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(140f, 58f));

            return new VisitorRequestRow
            {
                Root = row.gameObject,
                Icon = icon,
                Name = name,
                Count = count,
                Price = price,
                WasPrice = was,
                Buy = buy,
                BuyLabel = buy.GetComponentInChildren<Text>()
            };
        }

        private static Text CreateRewardPill(Transform panel, string name, Sprite icon, Vector2 position)
        {
            var text = UIFactory.CreatePill(name, panel, string.Empty, 30, new Vector2(230f, 60f), out var pill);
            UIFactory.Place(pill.rectTransform, new Vector2(0.5f, 0f), position, new Vector2(230f, 60f));
            if (icon != null)
            {
                var image = UIFactory.CreateIcon("Icon", pill.transform, icon, new Vector2(70f, 70f));
                UIFactory.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(-24f, 0f), new Vector2(70f, 70f));
                UIFactory.Stretch(text.rectTransform, 50f, 2f, 14f, 2f);
            }

            return text;
        }

        // ------------------------------------------------------------------ Per frame

        /// <summary>Lets visitors come and go, keeps bubbles over their heads and the open request live.</summary>
        private void UpdateVisitorUi()
        {
            _visitorTickTimer -= Time.unscaledDeltaTime;
            if (_visitorTickTimer <= 0f)
            {
                _visitorTickTimer = VisitorTickInterval;
                _session.UpdateVisitors();
            }

            UpdateVisitorBubbles();

            if (IsVisitorWindowVisible)
            {
                var visitor = _session.Visitors.Find(_shownVisitorId);
                if (visitor == null)
                {
                    CloseVisitor();
                    ShowToast("A visitor left.");
                    return;
                }

                SetCountdownText(_visitorLeavesText, SecondsUntil(visitor.LeavesAtUtcTicks), prefix: "Leaves in ");
                _visitorWindowTimer -= Time.unscaledDeltaTime;
                if (_visitorWindowTimer <= 0f)
                {
                    _visitorWindowTimer = VisitorWindowRefreshInterval;
                    RefreshVisitorRequests(visitor);
                }
            }
        }

        private void UpdateVisitorBubbles()
        {
            var visitors = _session.Visitors.Visitors;

            // Drop bubbles of visitors who left.
            for (var i = _visitorBubbles.Count - 1; i >= 0; i--)
            {
                if (_session.Visitors.Find(_visitorBubbles[i].VisitorId) == null)
                {
                    Destroy(_visitorBubbles[i].Root.gameObject);
                    _visitorBubbles.RemoveAt(i);
                }
            }

            for (var i = 0; i < visitors.Count; i++)
            {
                var visitor = visitors[i];
                var bubble = FindVisitorBubble(visitor.Id) ?? CreateVisitorBubble(visitor);

                var show = !IsVisitorWindowVisible || _shownVisitorId != visitor.Id;
                Vector3 screen = default;
                if (show && _farmView != null && _farmView.TryGetVisitorBubblePosition(visitor.Id, out var world))
                {
                    screen = _camera.WorldToScreenPoint(world);
                    show = screen.z > 0f;
                }
                else
                {
                    show = false;
                }

                if (bubble.Root.gameObject.activeSelf != show)
                {
                    bubble.Root.gameObject.SetActive(show);
                }

                if (!show)
                {
                    continue;
                }

                var ready = _session.CanServeVisitor(visitor);
                var bob = Mathf.Sin(Time.unscaledTime * 2.6f + visitor.Id) * 5f;
                bubble.Root.anchoredPosition = ScreenToCanvasPoint(screen) + new Vector2(0f, 44f + bob);
                if (bubble.Tick.activeSelf != ready)
                {
                    bubble.Tick.SetActive(ready);
                }
            }
        }

        private VisitorBubble FindVisitorBubble(int visitorId)
        {
            for (var i = 0; i < _visitorBubbles.Count; i++)
            {
                if (_visitorBubbles[i].VisitorId == visitorId)
                {
                    return _visitorBubbles[i];
                }
            }

            return null;
        }

        /// <summary>A white speech bubble with the first thing they want, and a tick when it's all in storage.</summary>
        private VisitorBubble CreateVisitorBubble(Visitor visitor)
        {
            var button = UIFactory.CreateButton($"VisitorBubble_{visitor.Id}", _visitorBubblesRoot, string.Empty, new Vector2(96f, 96f), 20, ButtonStyle.Neutral);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(96f, 96f);
            button.GetComponent<Image>().sprite = UIFactory.CircleSprite;
            UIFactory.AddOutline(button.GetComponent<Image>(), UIFactory.OutlineDark, 2.5f);

            // Sellers get a gold bubble with a coin badge; the icon is the first thing they want or sell.
            var itemCount = visitor.IsSeller ? visitor.Offers.Count : visitor.Requests.Count;
            var first = visitor.IsSeller
                ? (visitor.Offers.Count > 0 ? visitor.Offers[0].ItemId : null)
                : (visitor.Requests.Count > 0 ? visitor.Requests[0].itemId : null);
            if (visitor.IsSeller)
            {
                button.GetComponent<Image>().color = UIFactory.Gold;
            }

            var icon = UIFactory.CreateIcon("Icon", rect, first != null ? _session.Content.GetItemIcon(first) : null, new Vector2(66f, 66f));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(66f, 66f));

            if (itemCount > 1)
            {
                UIFactory.CreatePill("More", rect, $"+{itemCount - 1}", 20, new Vector2(48f, 30f), out var morePill);
                UIFactory.Place(morePill.rectTransform, new Vector2(1f, 0f), new Vector2(10f, -6f), new Vector2(48f, 30f));
            }

            if (visitor.IsSeller)
            {
                var coin = UIFactory.CreateIcon("ForSale", rect, UIFactory.Icon("icon_coin"), new Vector2(44f, 44f));
                UIFactory.Place(coin.rectTransform, new Vector2(1f, 1f), new Vector2(14f, 14f), new Vector2(44f, 44f));
            }

            // A green badge with a white tick, drawn as two strokes (the UI font has no tick glyph).
            var tick = UIFactory.CreateDecor("Ready", rect, UIFactory.CircleSprite, UIFactory.Green);
            UIFactory.Place(tick.rectTransform, new Vector2(1f, 1f), new Vector2(12f, 12f), new Vector2(42f, 42f));
            UIFactory.AddOutline(tick, UIFactory.OutlineDark, 2f);
            var shortStroke = UIFactory.CreateDecor("TickShort", tick.transform, UIFactory.SquareSprite, Color.white);
            UIFactory.Place(shortStroke.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-7f, -2f), new Vector2(6f, 14f));
            shortStroke.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            shortStroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var longStroke = UIFactory.CreateDecor("TickLong", tick.transform, UIFactory.SquareSprite, Color.white);
            UIFactory.Place(longStroke.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(4f, 1f), new Vector2(6f, 24f));
            longStroke.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            longStroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -38f);
            tick.gameObject.SetActive(false);

            var id = visitor.Id;
            button.onClick.AddListener(() => ShowVisitor(id));

            var bubble = new VisitorBubble { VisitorId = id, Root = rect, Icon = icon, Tick = tick.gameObject };
            _visitorBubbles.Add(bubble);
            return bubble;
        }

        // ------------------------------------------------------------------ Window

        /// <summary>Opens a visitor's request.</summary>
        public void ShowVisitor(int visitorId)
        {
            var visitor = _session.Visitors.Find(visitorId);
            if (_visitorOverlay == null || visitor == null)
            {
                return;
            }

            CloseInventory();
            CloseBuildingShop();
            CloseFeedGrinder();
            CloseAnimalPen();
            CloseNoticeBoard();
            HideCropShop();
            HideScythe();

            _shownVisitorId = visitorId;
            _visitorTitle.text = visitor.Name;
            _visitorPortrait.sprite = UIFactory.Icon(VisitorPortraitPrefix + visitor.Look);
            _visitorPortrait.enabled = _visitorPortrait.sprite != null;

            // Requests show their reward and Give; sellers show the reputation discount.
            _visitorWantsLabel.text = visitor.IsSeller ? "For sale" : "Wants";
            _visitorRewardGroup.SetActive(!visitor.IsSeller);
            _visitorDiscountGroup.SetActive(visitor.IsSeller);
            _visitorGiveButton.gameObject.SetActive(!visitor.IsSeller);
            _visitorSendAwayLabel.text = visitor.IsSeller ? "No thanks" : "Send away";
            UIFactory.Place(
                _visitorSendAwayButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(visitor.IsSeller ? 0f : -190f, 36f),
                new Vector2(330f, 92f));

            _visitorCoinsText.text = UIFactory.FormatNumber(visitor.RewardCoins);
            _visitorXpText.text = $"+{UIFactory.FormatNumber(visitor.RewardXp)} XP";
            _visitorReputationText.text = $"+{UIFactory.FormatNumber(visitor.RewardReputation)}";

            SetPlainText(_visitorLeavesText, string.Empty);
            SetCountdownText(_visitorLeavesText, SecondsUntil(visitor.LeavesAtUtcTicks), prefix: "Leaves in ");
            RefreshVisitorRequests(visitor);
            _visitorWindowTimer = VisitorWindowRefreshInterval;
            _visitorOverlay.SetActive(true);
        }

        private void RefreshVisitorRequests(Visitor visitor)
        {
            if (visitor.IsSeller)
            {
                RefreshSellerOffers(visitor);
                return;
            }

            for (var i = 0; i < _visitorRows.Count; i++)
            {
                var row = _visitorRows[i];
                var active = i < visitor.Requests.Count;
                if (row.Root.activeSelf != active)
                {
                    row.Root.SetActive(active);
                }

                if (!active)
                {
                    continue;
                }

                SetSellerRowParts(row, false);
                var request = visitor.Requests[i];
                var have = _session.Inventory.GetCount(request.itemId);
                row.Icon.sprite = _session.Content.GetItemIcon(request.itemId);
                row.Icon.enabled = row.Icon.sprite != null;
                row.Name.text = _session.Content.GetItemDisplayName(request.itemId);
                row.Count.text = $"{UIFactory.FormatNumber(have)} / {UIFactory.FormatNumber(request.amount)}";
                row.Count.color = have >= request.amount ? UIFactory.GreenDark : UIFactory.Berry;
            }

            var ready = _session.CanServeVisitor(visitor);
            _visitorGiveButton.interactable = ready;
            UIFactory.SetButtonStyle(_visitorGiveButton, ready ? ButtonStyle.Primary : ButtonStyle.Muted);
            _visitorGiveLabel.text = ready ? "Give" : "Not enough";
        }

        /// <summary>A seller's bundles: "3 x Egg Tart", today's price (and the full price), and Buy.</summary>
        private void RefreshSellerOffers(Visitor visitor)
        {
            var discount = _session.VisitorDiscount;
            for (var i = 0; i < _visitorRows.Count; i++)
            {
                var row = _visitorRows[i];
                var active = i < visitor.Offers.Count;
                if (row.Root.activeSelf != active)
                {
                    row.Root.SetActive(active);
                }

                if (!active)
                {
                    continue;
                }

                SetSellerRowParts(row, true);
                var offer = visitor.Offers[i];
                var price = _session.GetOfferPrice(offer);
                row.Icon.sprite = _session.Content.GetItemIcon(offer.ItemId);
                row.Icon.enabled = row.Icon.sprite != null;
                row.Name.text = $"{offer.Amount} × {_session.Content.GetItemDisplayName(offer.ItemId)}";
                row.Price.text = offer.Sold ? string.Empty : UIFactory.FormatNumber(price);
                row.WasPrice.text = !offer.Sold && price < offer.BasePrice
                    ? $"was {UIFactory.FormatNumber(offer.BasePrice)}"
                    : string.Empty;

                var affordable = _session.Wallet.Coins >= price;
                var canBuy = !offer.Sold && affordable && _session.Storage.CanFit(offer.Amount);
                row.Buy.interactable = canBuy;
                UIFactory.SetButtonStyle(row.Buy, canBuy ? ButtonStyle.Gold : ButtonStyle.Muted);
                row.BuyLabel.text = offer.Sold ? "Sold" : !affordable ? "No coins" : !_session.Storage.CanFit(offer.Amount) ? "No room" : "Buy";
            }

            _visitorDiscountText.text = $"{Mathf.RoundToInt(discount * 100f)}% off";
        }

        /// <summary>Request rows show have / need; seller rows show price and Buy.</summary>
        private static void SetSellerRowParts(VisitorRequestRow row, bool seller)
        {
            // Seller rows need room on the right for the price and Buy.
            row.Name.rectTransform.sizeDelta = new Vector2(seller ? 220f : 330f, 50f);
            row.Name.resizeTextForBestFit = seller;
            row.Name.resizeTextMinSize = 22;
            row.Name.resizeTextMaxSize = 28;

            if (row.Count.gameObject.activeSelf == seller)
            {
                row.Count.gameObject.SetActive(!seller);
            }

            if (row.Buy.gameObject.activeSelf != seller)
            {
                row.Buy.gameObject.SetActive(seller);
                row.Price.gameObject.SetActive(seller);
                row.WasPrice.gameObject.SetActive(seller);
            }
        }

        private void BuyFromShownVisitor(int offerIndex)
        {
            var id = _shownVisitorId;
            if (!_session.TryBuyFromVisitor(id, offerIndex, out var message))
            {
                ShowToast(message);
                return;
            }

            var visitor = _session.Visitors.Find(id);
            if (visitor == null)
            {
                // Sold out: they head home.
                CloseVisitor();
            }
            else
            {
                RefreshVisitorRequests(visitor);
            }

            ShowToast(message);
        }

        private void ServeShownVisitor()
        {
            var id = _shownVisitorId;
            if (_session.TryServeVisitor(id, out var message))
            {
                CloseVisitor();
                _reputationPunch?.Play();
            }

            ShowToast(message);
        }

        private void SendAwayShownVisitor()
        {
            _session.TrySendAwayVisitor(_shownVisitorId, out var message);
            CloseVisitor();
            ShowToast(message);
        }

        public void CloseVisitor()
        {
            if (_visitorOverlay != null)
            {
                _visitorOverlay.SetActive(false);
            }

            _shownVisitorId = -1;
        }

        private void OnReputationChanged(int points)
        {
            if (_reputationText != null)
            {
                _reputationText.text = UIFactory.FormatNumber(points);
            }
        }

        private double SecondsUntil(long utcTicks)
        {
            return System.Math.Max(0d, (utcTicks - _session.UtcNow.Ticks) / (double)System.TimeSpan.TicksPerSecond);
        }
    }
}
