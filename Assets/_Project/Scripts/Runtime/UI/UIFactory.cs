using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FarmGame.UI
{
    public enum ButtonStyle
    {
        /// <summary>Green: the main action on a screen (Buy, Make, Add).</summary>
        Primary,

        /// <summary>Wood brown: secondary actions and navigation.</summary>
        Secondary,

        /// <summary>Gold: earning actions (Sell, Collect).</summary>
        Gold,

        /// <summary>Red: cancel or "can't afford".</summary>
        Danger,

        /// <summary>Cream: unselected tabs and quiet buttons.</summary>
        Neutral,

        /// <summary>Grey-brown: an action that can't be taken right now.</summary>
        Muted
    }

    /// <summary>
    /// Builds the "warm wooden farm" UI: parchment panels with wooden rims, wooden title planks,
    /// chunky 3D buttons and soft shadows. All screens in GameUI are made from these pieces.
    /// </summary>
    public static class UIFactory
    {
        private const string ArtPath = "Art/Placeholders/";

        /// <summary>Painted icons (Tools > Farm Game > Rebuild Painted Art), used over the placeholders.</summary>
        private const string PaintedIconPath = "Art/Icons/";

        private static Font _font;
        private static Sprite _roundedSprite;
        private static Sprite _circleSprite;
        private static Sprite _squareSprite;
        private static Sprite _panelSprite;
        private static Sprite _headerSprite;
        private static Sprite _buttonSprite;
        private static Sprite _cardSprite;
        private static Sprite _slotSprite;
        private static Sprite _shadowSprite;
        private static Sprite _closeSprite;
        private static Sprite _pillSprite;
        private static Sprite _tabSprite;

        // Palette.
        public static readonly Color Ink = new Color(0.27f, 0.18f, 0.09f, 1f);
        public static readonly Color InkSoft = new Color(0.37f, 0.25f, 0.12f, 1f);
        public static readonly Color Cream = new Color(1f, 0.97f, 0.9f, 1f);
        public static readonly Color Parchment = new Color(0.99f, 0.94f, 0.83f, 1f);
        public static readonly Color Wood = new Color(0.66f, 0.43f, 0.22f, 1f);
        public static readonly Color WoodDark = new Color(0.36f, 0.22f, 0.1f, 1f);
        public static readonly Color Green = new Color(0.4f, 0.72f, 0.24f, 1f);
        public static readonly Color GreenDark = new Color(0.22f, 0.45f, 0.12f, 1f);
        public static readonly Color Gold = new Color(1f, 0.77f, 0.22f, 1f);
        public static readonly Color Berry = new Color(0.88f, 0.34f, 0.24f, 1f);
        public static readonly Color Muted = new Color(0.55f, 0.47f, 0.38f, 1f);
        public static readonly Color OutlineDark = new Color(0.25f, 0.13f, 0.04f, 0.9f);
        public static readonly Color Overlay = new Color(0.12f, 0.07f, 0.02f, 0.62f);

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null)
                    {
                        _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                    }
                }

                return _font;
            }
        }

        public static Sprite RoundedSprite => Load(ref _roundedSprite, "ui_rounded", null);
        public static Sprite CircleSprite => Load(ref _circleSprite, "ui_circle", null);
        public static Sprite SquareSprite => Load(ref _squareSprite, "ui_square", null);

        // UI kit sprites fall back to the plain rounded sprite if the art has not been generated.
        public static Sprite PanelSprite => Load(ref _panelSprite, "ui_panel", RoundedSprite);
        public static Sprite HeaderSprite => Load(ref _headerSprite, "ui_header", RoundedSprite);
        public static Sprite ButtonSprite => Load(ref _buttonSprite, "ui_button", RoundedSprite);
        public static Sprite CardSprite => Load(ref _cardSprite, "ui_card", RoundedSprite);
        public static Sprite SlotSprite => Load(ref _slotSprite, "ui_slot", RoundedSprite);
        public static Sprite ShadowSprite => Load(ref _shadowSprite, "ui_shadow", RoundedSprite);
        public static Sprite CloseSprite => Load(ref _closeSprite, "ui_close", CircleSprite);
        public static Sprite PillSprite => Load(ref _pillSprite, "ui_pill", RoundedSprite);
        public static Sprite TabSprite => Load(ref _tabSprite, "ui_tab", RoundedSprite);

        public static Sprite Icon(string name)
        {
            var painted = Resources.Load<Sprite>(PaintedIconPath + name);
            return painted != null ? painted : Resources.Load<Sprite>(ArtPath + name);
        }

        private static Sprite Load(ref Sprite cache, string name, Sprite fallback)
        {
            if (cache != null)
            {
                return cache;
            }

            cache = Resources.Load<Sprite>(ArtPath + name);
            if (cache != null)
            {
                return cache;
            }

            if (fallback != null)
            {
                return fallback;
            }

            cache = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return cache;
        }

        public static string FormatNumber(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- Basic elements

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null && sprite.border != Vector4.zero
                ? Image.Type.Sliced
                : Image.Type.Simple;
            return image;
        }

        /// <summary>An image that is only decoration: it never blocks taps.</summary>
        public static Image CreateDecor(string name, Transform parent, Sprite sprite, Color color)
        {
            var image = CreateImage(name, parent, sprite, color);
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateIcon(string name, Transform parent, Sprite sprite, Vector2 size)
        {
            var icon = CreateDecor(name, parent, sprite, Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.sizeDelta = size;
            return icon;
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string content,
            int fontSize,
            Color color,
            TextAnchor alignment)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>Bold light text with a dark outline, for text on wood or over the farm.</summary>
        public static Text CreateTitleText(
            string name,
            Transform parent,
            string content,
            int fontSize,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = CreateText(name, parent, content, fontSize, Cream, alignment);
            text.fontStyle = FontStyle.Bold;
            AddOutline(text, OutlineDark, 2f);
            return text;
        }

        public static void AddOutline(Graphic graphic, Color color, float distance)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        /// <summary>A soft shadow drawn behind a panel. Create it before the panel so it sits underneath.</summary>
        public static Image CreateDropShadow(Transform parent, Vector2 size, Vector2 offset)
        {
            var shadow = CreateDecor("Shadow", parent, ShadowSprite, Color.white);
            SetAnchored(
                shadow.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                offset,
                size + new Vector2(70f, 70f));
            return shadow;
        }

        // ---------------------------------------------------------------- Buttons

        /// <summary>A themed 3D button. Its label is the first Text child, named "Label".</summary>
        public static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            int fontSize,
            ButtonStyle style)
        {
            var button = CreateButton(name, parent, label, size, fontSize, StyleColor(style), StyleTextColor(style));
            ApplyLabelEffect(button, style);
            return button;
        }

        /// <summary>A 3D button tinted with any colour.</summary>
        public static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            int fontSize,
            Color backgroundColor,
            Color textColor)
        {
            var image = CreateImage(name, parent, ButtonSprite, backgroundColor);
            image.raycastTarget = true;
            var rect = image.rectTransform;
            rect.sizeDelta = size;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.75f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            // Lift the label onto the button face, above the darker lip.
            var text = CreateText("Label", rect, label, fontSize, textColor, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            Stretch(text.rectTransform, 12f, 10f, 12f, 2f);

            image.gameObject.AddComponent<UIPressFeedback>();
            return button;
        }

        public static void SetButtonStyle(Button button, ButtonStyle style)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = StyleColor(style);
            }

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = StyleTextColor(style);
                var outline = label.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = HasDarkFace(style);
                }
            }
        }

        public static Color StyleColor(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Primary:
                    return Green;
                case ButtonStyle.Secondary:
                    return Wood;
                case ButtonStyle.Gold:
                    return Gold;
                case ButtonStyle.Danger:
                    return Berry;
                case ButtonStyle.Neutral:
                    return Parchment;
                default:
                    return Muted;
            }
        }

        public static Color StyleTextColor(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Gold:
                case ButtonStyle.Neutral:
                    return Ink;
                default:
                    return Cream;
            }
        }

        private static bool HasDarkFace(ButtonStyle style)
        {
            return style != ButtonStyle.Gold && style != ButtonStyle.Neutral;
        }

        private static void ApplyLabelEffect(Button button, ButtonStyle style)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label == null)
            {
                return;
            }

            AddOutline(label, new Color(0f, 0f, 0f, 0.35f), 1.5f);
            label.GetComponent<Outline>().enabled = HasDarkFace(style);
        }

        /// <summary>The round red close button used on every window.</summary>
        public static Button CreateCloseButton(Transform parent, UnityAction onClick)
        {
            var image = CreateImage("CloseButton", parent, CloseSprite, Color.white);
            image.raycastTarget = true;
            image.preserveAspect = true;
            SetAnchored(
                image.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-22f, -22f),
                new Vector2(86f, 86f));

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            image.gameObject.AddComponent<UIPressFeedback>();
            return button;
        }

        // ---------------------------------------------------------------- Containers

        /// <summary>
        /// A full-screen dimmed overlay holding a parchment window with a wooden title plank and
        /// a close button. Tapping the dim area outside the window also closes it.
        /// Hierarchy: overlay / {Dim, Shadow, panel / {Header / Title, CloseButton, ...}}.
        /// The close-on-tap button lives on the Dim sibling, not on the overlay: Unity sends a
        /// click to the nearest parent that handles clicks, so a button on the overlay would
        /// also catch taps on the panel and close the window.
        /// </summary>
        public static RectTransform CreateWindow(
            Transform canvas,
            string overlayName,
            string panelName,
            string title,
            Vector2 size,
            UnityAction onClose,
            out Text titleText)
        {
            var overlay = CreateRect(overlayName, canvas);
            Stretch(overlay);

            var dim = CreateImage("Dim", overlay, null, Overlay);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(onClose);

            CreateDropShadow(overlay, size, new Vector2(0f, -16f));

            var panel = CreateImage(panelName, overlay, PanelSprite, Color.white);
            panel.raycastTarget = true;
            SetAnchored(
                panel.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                size);

            var header = CreateDecor("Header", panel.transform, HeaderSprite, Color.white);
            SetAnchored(
                header.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -6f),
                new Vector2(Mathf.Min(size.x - 220f, 560f), 86f));

            titleText = CreateTitleText("Title", header.transform, title, 40);
            Stretch(titleText.rectTransform, 24f, 6f, 24f, 4f);

            CreateCloseButton(panel.transform, onClose);

            var popIn = overlay.gameObject.AddComponent<UIPopIn>();
            popIn.Target = panel.rectTransform;
            overlay.gameObject.SetActive(false);
            return panel.rectTransform;
        }

        /// <summary>A recessed area for grouping content inside a window.</summary>
        public static Image CreateWell(string name, Transform parent)
        {
            return CreateDecor(name, parent, SlotSprite, Color.white);
        }

        public static Image CreateCard(string name, Transform parent)
        {
            var card = CreateImage(name, parent, CardSprite, Color.white);
            card.raycastTarget = true;
            return card;
        }

        /// <summary>A dark wooden capsule with light text, for counters and timers.</summary>
        public static Text CreatePill(string name, Transform parent, string content, int fontSize, Vector2 size, out Image background)
        {
            background = CreateDecor(name, parent, PillSprite, Color.white);
            background.rectTransform.sizeDelta = size;
            var text = CreateTitleText("Text", background.transform, content, fontSize);
            Stretch(text.rectTransform, 14f, 2f, 14f, 2f);
            return text;
        }

        /// <summary>A small wooden caption on a section of a window.</summary>
        public static Text CreateSectionLabel(string name, Transform parent, string content)
        {
            var text = CreateText(name, parent, content, 28, InkSoft, TextAnchor.MiddleLeft);
            text.fontStyle = FontStyle.Bold;
            return text;
        }

        /// <summary>A horizontal progress bar: a dark track with a coloured fill (Image.fillAmount).</summary>
        public static Image CreateProgressBar(string name, Transform parent, Vector2 size, Color fillColor)
        {
            var track = CreateDecor(name, parent, RoundedSprite, new Color(0.28f, 0.17f, 0.07f, 0.85f));
            track.rectTransform.sizeDelta = size;

            var fill = CreateDecor("Fill", track.transform, SquareSprite, fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            Stretch(fill.rectTransform, 5f, 5f, 5f, 5f);
            return fill;
        }

        /// <summary>
        /// A masked strip that scrolls sideways when its cards don't fit. Returns the content
        /// rect (with a HorizontalLayoutGroup) to add cards to. The viewport is placed with
        /// <paramref name="anchor"/>, <paramref name="position"/> and <paramref name="size"/>.
        /// </summary>
        public static RectTransform CreateHorizontalScroll(
            string name,
            Transform parent,
            Vector2 anchor,
            Vector2 position,
            Vector2 size,
            float spacing,
            out ScrollRect scroll)
        {
            var viewport = CreateImage(name, parent, null, new Color(1f, 1f, 1f, 0f));
            viewport.raycastTarget = true;
            Place(viewport.rectTransform, anchor, position, size);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = CreateRect("Content", viewport.transform);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(8, 8, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Keep short rows centred: once the layout knows its width, centre the content
            // when it is narrower than the viewport.
            var centre = content.gameObject.AddComponent<CenterWhenNarrow>();
            centre.Viewport = viewport.rectTransform;

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            centre.Scroll = scroll;
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.vertical = false;
            scroll.horizontal = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return content;
        }

        // ---------------------------------------------------------------- Layout helpers

        public static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void SetAnchored(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        public static void SetAnchored(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition)
        {
            SetAnchored(rect, anchorMin, anchorMax, pivot, anchoredPosition, rect.sizeDelta);
        }

        public static void SetAnchored(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition)
        {
            SetAnchored(rect, anchor, anchor, pivot, anchoredPosition, rect.sizeDelta);
        }

        /// <summary>Anchor and pivot at the same normalised point, e.g. (0.5, 1) for top-centre.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            SetAnchored(rect, anchor, anchor, anchor, position, size);
        }
    }
}
