using FarmGame.Visitors;
using UnityEngine;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>
    /// The Cocktail Bar's window, opened by tapping the bar on the beach: the bartender behind the
    /// counter and the line of customers, each showing the juice they want. Only the front customer
    /// can be served. A badge on the Beach button shows while a line is waiting.
    /// </summary>
    public sealed partial class GameUI
    {
        private const float BarTickInterval = 1f;
        private const float BarRefreshInterval = 0.5f;
        private const float BarFrontX = 440f;
        private const float BarSecondOffset = 130f;
        private const float BarCustomerSpacing = 92f;
        private const string BartenderSpritePath = "Art/Placeholders/bar_bartender";

        private GameObject _barOverlay;
        private RectTransform _barLineRoot;
        private Text _barTimerText;
        private Text _barEmptyText;
        private GameObject _barFrontGroup;
        private Image _barJuiceIcon;
        private Text _barJuiceName;
        private Text _barJuiceCount;
        private Text _barCoinsText;
        private Text _barXpText;
        private Button _barServeButton;
        private GameObject _travelBadge;
        private float _barTickTimer;
        private float _barRefreshTimer;

        public bool IsCocktailBarVisible => _barOverlay != null && _barOverlay.activeSelf;

        private void BuildCocktailBarUi()
        {
            var panel = UIFactory.CreateWindow(
                _canvasRect,
                "CocktailBarOverlay",
                "CocktailBarPanel",
                "Cocktail Bar",
                new Vector2(1180f, 800f),
                CloseCocktailBar,
                out _);
            _barOverlay = panel.parent.gameObject;

            _barTimerText = UIFactory.CreatePill("BarTimer", panel, string.Empty, 28, new Vector2(320f, 50f), out var timerPill);
            UIFactory.Place(timerPill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(320f, 50f));

            // The stage: the bartender behind the counter on the left, the line going off to the right.
            var stage = UIFactory.CreateWell("BarStage", panel);
            UIFactory.Place(stage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(1100f, 400f));

            var bartender = UIFactory.CreateIcon("Bartender", stage.transform, Resources.Load<Sprite>(BartenderSpritePath), new Vector2(210f, 280f));
            UIFactory.Place(bartender.rectTransform, new Vector2(0f, 0f), new Vector2(60f, 60f), new Vector2(210f, 280f));

            var counter = UIFactory.CreateDecor("Counter", stage.transform, UIFactory.RoundedSprite, UIFactory.Wood);
            UIFactory.Place(counter.rectTransform, new Vector2(0f, 0f), new Vector2(20f, 16f), new Vector2(310f, 130f));
            UIFactory.AddOutline(counter, UIFactory.OutlineDark, 2f);
            var counterTop = UIFactory.CreateDecor("CounterTop", counter.transform, UIFactory.RoundedSprite, UIFactory.WoodDark);
            UIFactory.Place(counterTop.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 8f), new Vector2(330f, 26f));

            _barLineRoot = UIFactory.CreateRect("BarLine", stage.transform);
            UIFactory.Stretch(_barLineRoot);

            _barEmptyText = UIFactory.CreateText("BarEmpty", stage.transform, "No one here", 36, UIFactory.InkSoft, TextAnchor.MiddleCenter);
            _barEmptyText.fontStyle = FontStyle.Bold;
            UIFactory.Place(_barEmptyText.rectTransform, new Vector2(0f, 0.5f), new Vector2(360f, 0f), new Vector2(700f, 60f));

            // What the front customer wants, and what they pay.
            var front = UIFactory.CreateRect("BarFront", panel);
            UIFactory.Stretch(front);
            _barFrontGroup = front.gameObject;

            var juiceWell = UIFactory.CreateWell("JuiceWell", front);
            UIFactory.Place(juiceWell.rectTransform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(180f, 180f));
            _barJuiceIcon = UIFactory.CreateIcon("JuiceIcon", juiceWell.transform, null, new Vector2(150f, 150f));
            UIFactory.Place(_barJuiceIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));

            _barJuiceName = UIFactory.CreateText("JuiceName", front, string.Empty, 34, UIFactory.Ink, TextAnchor.MiddleLeft);
            _barJuiceName.fontStyle = FontStyle.Bold;
            UIFactory.Place(_barJuiceName.rectTransform, new Vector2(0f, 0f), new Vector2(240f, 160f), new Vector2(340f, 44f));

            _barJuiceCount = UIFactory.CreateText("JuiceCount", front, string.Empty, 32, UIFactory.GreenDark, TextAnchor.MiddleLeft);
            _barJuiceCount.fontStyle = FontStyle.Bold;
            UIFactory.Place(_barJuiceCount.rectTransform, new Vector2(0f, 0f), new Vector2(240f, 112f), new Vector2(340f, 40f));

            _barCoinsText = UIFactory.CreateText("BarCoins", front, string.Empty, 30, UIFactory.InkSoft, TextAnchor.MiddleLeft);
            _barCoinsText.fontStyle = FontStyle.Bold;
            UIFactory.Place(_barCoinsText.rectTransform, new Vector2(0f, 0f), new Vector2(292f, 56f), new Vector2(130f, 40f));
            var coin = UIFactory.CreateIcon("BarCoinIcon", front, UIFactory.Icon("icon_coin"), new Vector2(44f, 44f));
            UIFactory.Place(coin.rectTransform, new Vector2(0f, 0f), new Vector2(240f, 54f), new Vector2(44f, 44f));

            _barXpText = UIFactory.CreateText("BarXp", front, string.Empty, 30, UIFactory.InkSoft, TextAnchor.MiddleLeft);
            _barXpText.fontStyle = FontStyle.Bold;
            UIFactory.Place(_barXpText.rectTransform, new Vector2(0f, 0f), new Vector2(430f, 56f), new Vector2(150f, 40f));

            _barServeButton = UIFactory.CreateButton("BarServeButton", front, "Serve", new Vector2(240f, 92f), 34, ButtonStyle.Primary);
            UIFactory.Place(_barServeButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(-40f, 60f), new Vector2(240f, 92f));
            _barServeButton.onClick.AddListener(ServeBarCustomer);

            // A badge on the Beach button, so a line isn't missed while the player is at the farm.
            var badge = UIFactory.CreateDecor("TravelBadge", _travelButton.transform, UIFactory.CircleSprite, UIFactory.Berry);
            UIFactory.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(52f, 52f));
            UIFactory.AddOutline(badge, UIFactory.OutlineDark, 2f);
            var badgeText = UIFactory.CreateTitleText("TravelBadgeText", badge.transform, "!", 36);
            UIFactory.Stretch(badgeText.rectTransform);
            _travelBadge = badge.gameObject;

            _session.CocktailBar.Changed += OnCocktailBarChanged;
            UpdateTravelBadge();
        }

        // ------------------------------------------------------------------ Per frame

        private void UpdateCocktailBarUi()
        {
            _barTickTimer -= Time.unscaledDeltaTime;
            if (_barTickTimer <= 0f)
            {
                _barTickTimer = BarTickInterval;
                _session.UpdateCocktailBar();
            }

            if (!IsCocktailBarVisible)
            {
                return;
            }

            UpdateBarTimer();
            _barRefreshTimer -= Time.unscaledDeltaTime;
            if (_barRefreshTimer <= 0f)
            {
                _barRefreshTimer = BarRefreshInterval;
                RefreshBarFront();
            }
        }

        private void OnCocktailBarChanged()
        {
            UpdateTravelBadge();
            if (IsCocktailBarVisible)
            {
                RebuildBarLine();
            }
        }

        private void UpdateTravelBadge()
        {
            if (_travelBadge != null)
            {
                _travelBadge.SetActive(_session.CocktailBar.Line.Count > 0 && !IsAtBeach);
            }
        }

        private void UpdateBarTimer()
        {
            var bar = _session.CocktailBar;
            if (bar.Line.Count > 0)
            {
                SetCountdownText(_barTimerText, SecondsUntil(bar.LineEndsAtUtcTicks), prefix: "Ends in ");
            }
            else if (bar.LineEndsAtUtcTicks == 0L && SecondsUntil(bar.NextLineAtUtcTicks) <= 0d)
            {
                SetPlainText(_barTimerText, "Closed");
            }
            else
            {
                SetCountdownText(_barTimerText, SecondsUntil(bar.NextLineAtUtcTicks), prefix: "Opens in ");
            }
        }

        // ------------------------------------------------------------------ Window

        public void ShowCocktailBar()
        {
            if (_barOverlay == null)
            {
                return;
            }

            _session.UpdateCocktailBar();
            RebuildBarLine();
            _barOverlay.SetActive(true);
            UpdateBarTimer();
        }

        public void CloseCocktailBar()
        {
            if (_barOverlay != null)
            {
                _barOverlay.SetActive(false);
            }
        }

        /// <summary>Stands the customers in line, front first, each with the juice they want over their head.</summary>
        private void RebuildBarLine()
        {
            for (var i = _barLineRoot.childCount - 1; i >= 0; i--)
            {
                var child = _barLineRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            // Back to front, so those nearer the counter stand in front of those behind them.
            var line = _session.CocktailBar.Line;
            for (var i = line.Count - 1; i >= 0; i--)
            {
                CreateBarCustomer(line[i], i);
            }

            _barEmptyText.gameObject.SetActive(line.Count == 0);
            RefreshBarFront();
        }

        private void CreateBarCustomer(BarCustomer customer, int place)
        {
            // The one being served stands bigger, right at the counter.
            var isFront = place == 0;
            var size = isFront ? new Vector2(195f, 260f) : new Vector2(135f, 180f);
            var x = BarFrontX + (isFront ? 0f : BarSecondOffset + (place - 1) * BarCustomerSpacing);

            var figure = UIFactory.CreateIcon(
                $"BarCustomer_{place}",
                _barLineRoot,
                UIFactory.Icon($"{VisitorPortraitPrefix}{Mathf.Min(customer.Look, VisitorService.LookCount - 1)}"),
                size);
            UIFactory.Place(figure.rectTransform, new Vector2(0f, 0f), new Vector2(x - size.x * 0.5f, 18f), size);

            var bubbleSize = isFront ? 92f : 72f;
            var bubble = UIFactory.CreateDecor("Bubble", figure.transform, UIFactory.CardSprite, Color.white);
            UIFactory.Place(bubble.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, bubbleSize - 6f), new Vector2(bubbleSize, bubbleSize));
            var juice = UIFactory.CreateIcon("Juice", bubble.transform, LoadItemIcon(customer.JuiceId), new Vector2(bubbleSize - 14f, bubbleSize - 14f));
            UIFactory.Place(juice.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(bubbleSize - 14f, bubbleSize - 14f));
        }

        /// <summary>The front customer's juice, whether it's in storage, and the reward.</summary>
        private void RefreshBarFront()
        {
            var customer = _session.CocktailBar.Front;
            if (_barFrontGroup.activeSelf != (customer != null))
            {
                _barFrontGroup.SetActive(customer != null);
            }

            if (customer == null)
            {
                return;
            }

            var have = _session.Inventory.GetCount(customer.JuiceId);
            _barJuiceIcon.sprite = LoadItemIcon(customer.JuiceId);
            _barJuiceName.text = _session.Content.GetItemDisplayName(customer.JuiceId);
            _barJuiceCount.text = $"{UIFactory.FormatNumber(have)}/1";
            _barJuiceCount.color = have > 0 ? UIFactory.GreenDark : UIFactory.Berry;
            _barCoinsText.text = $"+{UIFactory.FormatNumber(customer.RewardCoins)}";
            _barXpText.text = $"+{customer.RewardXp} XP";

            var canServe = _session.CanServeBarCustomer;
            _barServeButton.interactable = canServe;
            UIFactory.SetButtonStyle(_barServeButton, canServe ? ButtonStyle.Primary : ButtonStyle.Muted);
        }

        private void ServeBarCustomer()
        {
            _session.TryServeBarCustomer(out var message);
            ShowToast(message);
            RefreshBarFront();
        }
    }
}
