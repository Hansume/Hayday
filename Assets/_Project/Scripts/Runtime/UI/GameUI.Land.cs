using FarmGame.Land;
using UnityEngine;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>
    /// Tapping locked land pops up a deed icon on it; tapping that opens the land window, which
    /// shows the deeds held and buys the section.
    /// </summary>
    public sealed partial class GameUI
    {
        private const string DeedIconName = "icon_land_deed";

        private GameObject _landOverlay;
        private Text _landDeedCount;
        private Button _landBuyButton;
        private Vector2Int _landSection;

        public bool IsLandWindowVisible => _landOverlay != null && _landOverlay.activeSelf;

        private void BuildLandWindow()
        {
            var panel = UIFactory.CreateWindow(
                _canvasRect,
                "LandOverlay",
                "LandPanel",
                "New Land",
                new Vector2(620f, 480f),
                CloseLandWindow,
                out _);
            _landOverlay = panel.parent.gameObject;

            var well = UIFactory.CreateWell("IconWell", panel);
            UIFactory.Place(well.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(170f, 140f));
            var icon = UIFactory.CreateIcon("Icon", well.transform, UIFactory.Icon(DeedIconName), new Vector2(118f, 118f));
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118f, 118f));

            _landDeedCount = UIFactory.CreateText("DeedCount", panel, string.Empty, 32, UIFactory.Ink, TextAnchor.MiddleCenter);
            _landDeedCount.fontStyle = FontStyle.Bold;
            UIFactory.Place(_landDeedCount.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -264f), new Vector2(540f, 40f));

            _landBuyButton = UIFactory.CreateButton("BuyLandButton", panel, "Use 1", new Vector2(320f, 88f), 36, ButtonStyle.Gold);
            UIFactory.Place(_landBuyButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), Vector2.up * 40f, new Vector2(320f, 88f));
            _landBuyButton.onClick.AddListener(BuyLand);
        }

        /// <summary>A first tap on locked land: pops up the deed icon there, or says it's out of reach.</summary>
        public void TapLockedLand(Vector2Int section)
        {
            if (_session.Land.CanBuy(section))
            {
                _farmView.ShowLandSign(section);
            }
            else
            {
                ShowToast("Too far");
            }
        }

        /// <summary>Opens the window for a section that can be bought.</summary>
        public void ShowLandWindow(Vector2Int section)
        {
            if (!_session.Land.CanBuy(section))
            {
                return;
            }

            _landSection = section;
            var deeds = _session.Land.Deeds;
            _landDeedCount.text = $"{FarmLand.DeedName} x{UIFactory.FormatNumber(deeds)}";
            _landBuyButton.interactable = deeds > 0;
            UIFactory.SetButtonStyle(_landBuyButton, deeds > 0 ? ButtonStyle.Gold : ButtonStyle.Muted);
            _landOverlay.SetActive(true);
        }

        private void CloseLandWindow()
        {
            if (_landOverlay != null)
            {
                _landOverlay.SetActive(false);
            }
        }

        private void BuyLand()
        {
            _session.TryBuyLand(_landSection, out var message);
            ShowToast(message);
            CloseLandWindow();
        }

        /// <summary>The deeds' inventory tile: they take no storage and can't be sold.</summary>
        private void CreateDeedInventoryTile()
        {
            var deeds = _session.Land.Deeds;
            if (deeds <= 0)
            {
                return;
            }

            var tile = CreateInventoryTile("Row_land_deed", UIFactory.Icon(DeedIconName), FarmLand.DeedName, deeds);
            var hint = UIFactory.CreateWell("UseFor", tile);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(256f, 66f));
            var hintText = UIFactory.CreateText("Detail", hint.transform, "Tap locked land", 26, UIFactory.InkSoft, TextAnchor.MiddleCenter);
            hintText.fontStyle = FontStyle.Bold;
            UIFactory.Stretch(hintText.rectTransform, 8f, 2f, 8f, 2f);
        }
    }
}
