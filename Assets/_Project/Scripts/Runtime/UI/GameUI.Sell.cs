using UnityEngine;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>
    /// The sell dialog, opened from an inventory tile: pick how many to sell with -/+ or the
    /// slider (or All), then Sell. It opens at 1 so nothing is sold by accident.
    /// </summary>
    public sealed partial class GameUI
    {
        private GameObject _sellOverlay;
        private Text _sellTitle;
        private Image _sellIcon;
        private Text _sellOwnedText;
        private Text _sellAmountText;
        private Slider _sellSlider;
        private Button _sellMinusButton;
        private Button _sellPlusButton;
        private Button _sellAllButton;
        private Button _sellConfirmButton;
        private Text _sellConfirmLabel;

        private string _sellItemId;
        private string _sellItemName;
        private int _sellPrice;
        private int _sellMax;
        private int _sellAmount;
        private bool _sellRefreshing;

        public bool IsSellDialogVisible => _sellOverlay != null && _sellOverlay.activeSelf;

        private void BuildSellDialog()
        {
            var panel = UIFactory.CreateWindow(
                _canvasRect,
                "SellOverlay",
                "SellPanel",
                "Sell",
                new Vector2(820f, 640f),
                CloseSellDialog,
                out _sellTitle);
            _sellOverlay = panel.parent.gameObject;

            var well = UIFactory.CreateWell("IconWell", panel);
            UIFactory.Place(well.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(170f, 140f));
            _sellIcon = UIFactory.CreateIcon("Icon", well.transform, null, new Vector2(118f, 118f));
            UIFactory.Place(_sellIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118f, 118f));

            _sellOwnedText = UIFactory.CreateText("Owned", panel, string.Empty, 32, UIFactory.InkSoft, TextAnchor.MiddleCenter);
            _sellOwnedText.fontStyle = FontStyle.Bold;
            UIFactory.Place(_sellOwnedText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(740f, 36f));

            // - [amount] +
            _sellMinusButton = UIFactory.CreateButton("MinusButton", panel, "-", new Vector2(100f, 84f), 44, ButtonStyle.Secondary);
            UIFactory.Place(_sellMinusButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-230f, -306f), new Vector2(100f, 84f));
            _sellMinusButton.onClick.AddListener(() => SetSellAmount(_sellAmount - 1));

            var amountWell = UIFactory.CreateWell("AmountWell", panel);
            UIFactory.Place(amountWell.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -306f), new Vector2(300f, 84f));
            _sellAmountText = UIFactory.CreateText("Amount", amountWell.transform, "1", 44, UIFactory.Ink, TextAnchor.MiddleCenter);
            _sellAmountText.fontStyle = FontStyle.Bold;
            UIFactory.Stretch(_sellAmountText.rectTransform);

            _sellPlusButton = UIFactory.CreateButton("PlusButton", panel, "+", new Vector2(100f, 84f), 44, ButtonStyle.Secondary);
            UIFactory.Place(_sellPlusButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(230f, -306f), new Vector2(100f, 84f));
            _sellPlusButton.onClick.AddListener(() => SetSellAmount(_sellAmount + 1));

            _sellSlider = CreateAmountSlider(panel);
            _sellSlider.onValueChanged.AddListener(value =>
            {
                if (!_sellRefreshing)
                {
                    SetSellAmount(Mathf.RoundToInt(value));
                }
            });

            _sellAllButton = UIFactory.CreateButton("SellAllAmountButton", panel, "All", new Vector2(220f, 88f), 30, ButtonStyle.Secondary);
            UIFactory.Place(_sellAllButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-230f, 40f), new Vector2(220f, 88f));
            _sellAllButton.onClick.AddListener(() => SetSellAmount(_sellMax));

            _sellConfirmButton = UIFactory.CreateButton("ConfirmSellButton", panel, string.Empty, new Vector2(420f, 88f), 36, ButtonStyle.Gold);
            UIFactory.Place(_sellConfirmButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(110f, 40f), new Vector2(420f, 88f));
            _sellConfirmLabel = _sellConfirmButton.GetComponentInChildren<Text>();
            _sellConfirmLabel.rectTransform.offsetMin = new Vector2(64f, _sellConfirmLabel.rectTransform.offsetMin.y);
            var coin = UIFactory.CreateIcon("CoinIcon", _sellConfirmButton.transform, UIFactory.Icon("icon_coin"), new Vector2(52f, 52f));
            UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 4f), new Vector2(52f, 52f));
            _sellConfirmButton.onClick.AddListener(ConfirmSell);
        }

        /// <summary>A wooden slider from 1 to the number owned.</summary>
        private static Slider CreateAmountSlider(Transform panel)
        {
            var root = UIFactory.CreateRect("AmountSlider", panel);
            UIFactory.Place(root, new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(620f, 48f));

            var track = UIFactory.CreateImage("Track", root, UIFactory.RoundedSprite, new Color(0.36f, 0.22f, 0.1f, 0.3f));
            track.raycastTarget = true;
            UIFactory.SetAnchored(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 20f));

            var fillArea = UIFactory.CreateRect("FillArea", root);
            UIFactory.SetAnchored(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, 20f));
            var fill = UIFactory.CreateDecor("Fill", fillArea, UIFactory.RoundedSprite, UIFactory.Gold);
            UIFactory.Stretch(fill.rectTransform);

            var handleArea = UIFactory.CreateRect("HandleArea", root);
            UIFactory.Stretch(handleArea, 24f, 0f, 24f, 0f);
            var handle = UIFactory.CreateImage("Handle", handleArea, UIFactory.CircleSprite, UIFactory.Wood);
            handle.raycastTarget = true;
            UIFactory.SetAnchored(handle.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 0f));
            UIFactory.AddOutline(handle, UIFactory.OutlineDark, 2f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = true;
            slider.minValue = 1f;
            slider.maxValue = 1f;
            return slider;
        }

        /// <summary>Opens the dialog for a crop or product the player has at least one of.</summary>
        private void OpenSellDialog(string itemId)
        {
            if (_sellOverlay == null ||
                !_session.TryGetSellPrice(itemId, out var displayName, out var price))
            {
                return;
            }

            var owned = _session.Inventory.GetCount(itemId);
            if (owned <= 0)
            {
                ShowToast($"No {displayName} to sell.");
                return;
            }

            _sellItemId = itemId;
            _sellItemName = displayName;
            _sellPrice = price;
            _sellMax = owned;

            _sellTitle.text = $"Sell {displayName}";
            _sellIcon.sprite = _session.Content.GetItemIcon(itemId);
            _sellIcon.enabled = _sellIcon.sprite != null;
            _sellOwnedText.text = $"You have {UIFactory.FormatNumber(owned)}";

            _sellRefreshing = true;
            _sellSlider.minValue = 1f;
            _sellSlider.maxValue = Mathf.Max(1, owned);
            _sellSlider.interactable = owned > 1;
            _sellRefreshing = false;

            SetSellAmount(1);
            _sellOverlay.SetActive(true);
        }

        private void CloseSellDialog()
        {
            if (_sellOverlay != null)
            {
                _sellOverlay.SetActive(false);
            }

            _sellItemId = null;
        }

        private void SetSellAmount(int amount)
        {
            _sellAmount = Mathf.Clamp(amount, 1, Mathf.Max(1, _sellMax));
            var total = (int)System.Math.Min((long)_sellAmount * _sellPrice, int.MaxValue);

            _sellAmountText.text = UIFactory.FormatNumber(_sellAmount);
            _sellConfirmLabel.text = $"Sell  +{UIFactory.FormatNumber(total)}";
            _sellMinusButton.interactable = _sellAmount > 1;
            _sellPlusButton.interactable = _sellAmount < _sellMax;
            _sellAllButton.interactable = _sellAmount < _sellMax;

            _sellRefreshing = true;
            _sellSlider.value = _sellAmount;
            _sellRefreshing = false;
        }

        private void ConfirmSell()
        {
            if (string.IsNullOrEmpty(_sellItemId))
            {
                return;
            }

            _session.TrySell(_sellItemId, _sellAmount, out _, out _, out var message);
            ShowToast(message);
            CloseSellDialog();
            RebuildInventoryRows();
        }
    }
}
