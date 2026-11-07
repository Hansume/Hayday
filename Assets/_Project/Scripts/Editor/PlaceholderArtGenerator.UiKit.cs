using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>
    /// Placeholder art for the "warm wooden farm" UI kit: parchment panels with wooden rims,
    /// wooden header planks, tintable 3D buttons, cards, inset slots, shadows and HUD icons.
    /// Sprites marked "tintable" are drawn in greys so Image.color can recolour them.
    /// </summary>
    public static partial class PlaceholderArtGenerator
    {
        private static readonly Color UiOutline = Rgb(86, 52, 24);
        private static readonly Color UiWoodTop = Rgb(204, 140, 76);
        private static readonly Color UiWoodBottom = Rgb(150, 94, 46);
        private static readonly Color UiGroove = Rgb(120, 78, 40);
        private static readonly Color UiParchmentTop = Rgb(255, 246, 222);
        private static readonly Color UiParchmentBottom = Rgb(245, 228, 192);
        private static readonly Color UiInk = Rgb(69, 46, 23);

        private static void WriteUiKitArt()
        {
            WritePng("ui_panel", BuildUiPanel());
            WritePng("ui_header", BuildUiHeader());
            WritePng("ui_button", BuildUiButton());
            WritePng("ui_card", BuildUiCard());
            WritePng("ui_slot", BuildUiSlot());
            WritePng("ui_shadow", BuildUiShadow());
            WritePng("ui_close", BuildUiClose());
            WritePng("ui_pill", BuildUiPill());
            WritePng("ui_tab", BuildUiTab());
            WritePng("icon_build", BuildBuildIcon());
            WritePng("icon_inventory", BuildInventoryIcon());
            WritePng("icon_clock", BuildClockIcon());
            WritePng("icon_fish_feed", BuildFeedVariantIcon(Rgb(76, 150, 206), FeedEmblem.Fish));
            WritePng("icon_chicken_feed", BuildFeedVariantIcon(Rgb(236, 184, 52), FeedEmblem.Egg));
            WritePng("icon_cow_feed", BuildFeedVariantIcon(Rgb(246, 242, 232), FeedEmblem.Spots));
            WritePng("icon_goat_feed", BuildFeedVariantIcon(Rgb(126, 176, 92), FeedEmblem.Horns));
        }

        private static void ConfigureUiKitSprites()
        {
            ConfigureSprite("ui_panel", 100f, SpriteAlignment.Center, new Vector4(34f, 34f, 34f, 34f));
            ConfigureSprite("ui_header", 100f, SpriteAlignment.Center, new Vector4(40f, 30f, 40f, 30f));
            ConfigureSprite("ui_button", 100f, SpriteAlignment.Center, new Vector4(32f, 32f, 32f, 32f));
            ConfigureSprite("ui_card", 100f, SpriteAlignment.Center, new Vector4(30f, 30f, 30f, 30f));
            ConfigureSprite("ui_slot", 100f, SpriteAlignment.Center, new Vector4(30f, 30f, 30f, 30f));
            ConfigureSprite("ui_shadow", 100f, SpriteAlignment.Center, new Vector4(56f, 56f, 56f, 56f));
            ConfigureSprite("ui_close", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("ui_pill", 100f, SpriteAlignment.Center, new Vector4(28f, 26f, 28f, 26f));
            ConfigureSprite("ui_tab", 100f, SpriteAlignment.Center, new Vector4(30f, 8f, 30f, 30f));
            ConfigureSprite("icon_build", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_inventory", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_clock", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_fish_feed", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_chicken_feed", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_cow_feed", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_goat_feed", 100f, SpriteAlignment.Center, Vector4.zero);
        }

        // Parchment panel with a wooden rim. Not tintable.
        private static Texture2D BuildUiPanel()
        {
            var texture = NewTexture(128, 128);
            PaintBox(texture, 64f, 64f, 62f, 62f, 26f, UiOutline, UiOutline);
            PaintBox(texture, 64f, 64f, 59f, 59f, 23f, UiWoodTop, UiWoodBottom);
            PaintBox(texture, 64f, 64f, 50f, 50f, 15f, UiGroove, UiGroove);
            PaintBox(texture, 64f, 64f, 48f, 48f, 13f, UiParchmentTop, UiParchmentBottom);
            texture.Apply(false, false);
            return texture;
        }

        // Wooden title plank. Not tintable.
        private static Texture2D BuildUiHeader()
        {
            var texture = NewTexture(192, 80);
            PaintBox(texture, 96f, 40f, 94f, 38f, 20f, Rgb(80, 46, 20), Rgb(80, 46, 20));
            PaintBox(texture, 96f, 40f, 91f, 35f, 17f, UiWoodTop, UiWoodBottom);
            var grain = new Color(0.47f, 0.29f, 0.14f, 0.4f);
            DrawLine(texture, 22f, 30f, 170f, 30f, 1.5f, grain);
            DrawLine(texture, 22f, 50f, 170f, 50f, 1.5f, grain);
            DrawLine(texture, 24f, 68f, 168f, 68f, 2f, new Color(1f, 0.86f, 0.63f, 0.55f));
            DrawLine(texture, 24f, 11f, 168f, 11f, 2f, new Color(0.35f, 0.21f, 0.09f, 0.5f));
            texture.Apply(false, false);
            return texture;
        }

        // 3D button: face, darker lip below and an outline. Tintable (greys).
        private static Texture2D BuildUiButton()
        {
            var texture = NewTexture(96, 96);
            var outline = new Color(0.32f, 0.32f, 0.32f, 1f);
            PaintBox(texture, 48f, 47f, 46f, 45f, 24f, outline, outline);
            PaintBox(texture, 48f, 47f, 43f, 42f, 21f, new Color(0.62f, 0.62f, 0.62f, 1f), new Color(0.55f, 0.55f, 0.55f, 1f));
            PaintBox(texture, 48f, 51.5f, 43f, 37.5f, 20f, Color.white, new Color(0.86f, 0.86f, 0.86f, 1f));
            PaintBox(texture, 48f, 77f, 34f, 7f, 7f, new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 0.2f));
            texture.Apply(false, false);
            return texture;
        }

        // Light raised card for items. Not tintable (but white-ish, so tinting gives a wash).
        private static Texture2D BuildUiCard()
        {
            var texture = NewTexture(96, 96);
            PaintBox(texture, 48f, 48f, 46f, 46f, 20f, Rgb(176, 132, 82), Rgb(176, 132, 82));
            PaintBox(texture, 48f, 48f, 43f, 43f, 17f, Rgb(226, 204, 166), Rgb(226, 204, 166));
            PaintBox(texture, 48f, 50.5f, 43f, 40.5f, 17f, Rgb(255, 250, 236), Rgb(250, 238, 212));
            texture.Apply(false, false);
            return texture;
        }

        // Recessed well for icons and content areas. Not tintable.
        private static Texture2D BuildUiSlot()
        {
            var texture = NewTexture(96, 96);
            PaintBox(texture, 48f, 48f, 46f, 46f, 18f, Rgb(190, 154, 104), Rgb(190, 154, 104));
            PaintBox(texture, 48f, 48f, 43f, 43f, 15f, Rgb(214, 186, 138), Rgb(234, 214, 176));
            PaintBox(texture, 48f, 86f, 38f, 3f, 3f, new Color(0.55f, 0.4f, 0.22f, 0.35f), new Color(0.55f, 0.4f, 0.22f, 0.35f));
            texture.Apply(false, false);
            return texture;
        }

        // Soft drop shadow behind panels.
        private static Texture2D BuildUiShadow()
        {
            var texture = NewTexture(128, 128);
            for (var y = 0; y < 128; y++)
            {
                for (var x = 0; x < 128; x++)
                {
                    var distance = RoundedBoxSdf(x + 0.5f - 64f, y + 0.5f - 64f, 40f, 40f, 20f);
                    var alpha = 0.5f * (1f - Mathf.SmoothStep(0f, 1f, (distance + 22f) / 44f));
                    if (alpha > 0.001f)
                    {
                        BlendPixel(texture, x, y, new Color(0.1f, 0.05f, 0f, alpha), 1f);
                    }
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        // Round red close button with a white X.
        private static Texture2D BuildUiClose()
        {
            var texture = NewTexture(96, 96);
            PaintBox(texture, 48f, 47f, 45f, 45f, 45f, Rgb(110, 28, 20), Rgb(110, 28, 20));
            PaintBox(texture, 48f, 47f, 41f, 41f, 41f, Rgb(168, 44, 30), Rgb(150, 38, 26));
            PaintBox(texture, 48f, 51f, 41f, 37f, 37f, Rgb(240, 104, 72), Rgb(206, 62, 42));
            DrawEllipse(texture, 48f, 72f, 24f, 8f, new Color(1f, 1f, 1f, 0.28f), 0f);
            var shadow = new Color(0.45f, 0.1f, 0.06f, 0.6f);
            DrawLine(texture, 36f, 36f, 60f, 60f, 11f, shadow);
            DrawLine(texture, 36f, 60f, 60f, 36f, 11f, shadow);
            DrawLine(texture, 36f, 39f, 60f, 63f, 9f, Color.white);
            DrawLine(texture, 36f, 63f, 60f, 39f, 9f, Color.white);
            texture.Apply(false, false);
            return texture;
        }

        // Dark wooden capsule for counters, timers and toasts. Not tintable.
        private static Texture2D BuildUiPill()
        {
            var texture = NewTexture(128, 56);
            PaintBox(texture, 64f, 28f, 62f, 26f, 26f, Rgb(54, 32, 14), Rgb(54, 32, 14));
            PaintBox(texture, 64f, 28f, 59f, 23f, 23f, Rgb(124, 80, 42), Rgb(92, 58, 28));
            DrawLine(texture, 30f, 44f, 98f, 44f, 2f, new Color(1f, 0.86f, 0.67f, 0.3f));
            texture.Apply(false, false);
            return texture;
        }

        // Folder tab with rounded top corners only. Tintable (greys).
        private static Texture2D BuildUiTab()
        {
            var texture = NewTexture(128, 72);
            var outline = new Color(0.38f, 0.38f, 0.38f, 1f);
            PaintBox(texture, 64f, 30f, 62f, 40f, 20f, outline, outline);
            PaintBox(texture, 64f, 28f, 59f, 39f, 17f, Color.white, new Color(0.9f, 0.9f, 0.9f, 1f));
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildBuildIcon()
        {
            var texture = NewTexture(IconSize, IconSize);
            DrawEllipse(texture, 64f, 18f, 38f, 7f, Shadow, 0f);
            DrawLine(texture, 42f, 24f, 78f, 80f, 17f, WoodDark);
            DrawLine(texture, 42f, 24f, 78f, 80f, 11f, Rgb(196, 140, 76));
            DrawLine(texture, 45f, 30f, 75f, 76f, 3f, Rgb(240, 196, 130));
            DrawLine(texture, 56f, 98f, 102f, 68f, 31f, Rgb(52, 58, 64));
            DrawLine(texture, 56f, 98f, 102f, 68f, 25f, Metal);
            DrawLine(texture, 58f, 103f, 99f, 76f, 5f, Rgb(196, 206, 210));
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildInventoryIcon()
        {
            var texture = NewTexture(IconSize, IconSize);
            DrawEllipse(texture, 64f, 18f, 46f, 8f, Shadow, 0f);

            DrawEllipse(texture, 46f, 96f, 9f, 16f, Rgb(231, 124, 39), -28f);
            DrawLine(texture, 52f, 108f, 58f, 118f, 4f, Rgb(75, 155, 55));
            DrawLine(texture, 52f, 108f, 48f, 120f, 4f, Rgb(75, 155, 55));
            DrawEllipse(texture, 66f, 98f, 12f, 12f, BarnRed, 0f);
            DrawEllipse(texture, 62f, 102f, 4f, 3f, new Color(1f, 1f, 1f, 0.4f), 0f);
            DrawLine(texture, 82f, 90f, 86f, 118f, 4f, Hay);
            DrawEllipse(texture, 86f, 114f, 5f, 8f, Hay, -10f);
            DrawEllipse(texture, 80f, 108f, 4f, 7f, Hay, 20f);

            FillRect(texture, 22, 22, 106, 94, WoodDark);
            FillRect(texture, 26, 26, 102, 90, Rgb(190, 132, 70));
            FillRect(texture, 26, 48, 102, 51, WoodDark);
            FillRect(texture, 26, 68, 102, 71, WoodDark);
            FillRect(texture, 26, 26, 35, 90, Rgb(150, 100, 52));
            FillRect(texture, 93, 26, 102, 90, Rgb(150, 100, 52));
            DrawLine(texture, 36f, 30f, 92f, 86f, 5f, Rgb(150, 100, 52));
            FillRect(texture, 26, 86, 102, 90, Rgb(214, 160, 98));
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildClockIcon()
        {
            var texture = NewTexture(IconSize, IconSize);
            DrawEllipse(texture, 64f, 64f, 54f, 54f, WoodDark, 0f);
            DrawEllipse(texture, 64f, 64f, 46f, 46f, UiParchmentTop, 0f);
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI / 6f;
                var x = 64f + Mathf.Cos(angle) * 38f;
                var y = 64f + Mathf.Sin(angle) * 38f;
                DrawEllipse(texture, x, y, 3f, 3f, UiGroove, 0f);
            }

            DrawLine(texture, 64f, 64f, 64f, 96f, 8f, UiInk);
            DrawLine(texture, 64f, 64f, 88f, 56f, 8f, UiInk);
            DrawEllipse(texture, 64f, 64f, 7f, 7f, BarnRed, 0f);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>Anti-aliased rounded box with a vertical gradient (top to bottom colour).</summary>
        private static void PaintBox(
            Texture2D texture,
            float centerX,
            float centerY,
            float halfWidth,
            float halfHeight,
            float radius,
            Color top,
            Color bottom)
        {
            radius = Mathf.Min(radius, Mathf.Min(halfWidth, halfHeight));
            var minX = Mathf.FloorToInt(centerX - halfWidth - 2f);
            var maxX = Mathf.CeilToInt(centerX + halfWidth + 2f);
            var minY = Mathf.FloorToInt(centerY - halfHeight - 2f);
            var maxY = Mathf.CeilToInt(centerY + halfHeight + 2f);

            for (var y = minY; y <= maxY; y++)
            {
                var t = Mathf.Clamp01((y + 0.5f - (centerY - halfHeight)) / (2f * halfHeight));
                var color = Color.Lerp(bottom, top, t);
                for (var x = minX; x <= maxX; x++)
                {
                    var distance = RoundedBoxSdf(x + 0.5f - centerX, y + 0.5f - centerY, halfWidth, halfHeight, radius);
                    var coverage = Mathf.Clamp01(0.5f - distance);
                    if (coverage > 0f)
                    {
                        BlendPixel(texture, x, y, color, coverage);
                    }
                }
            }
        }
    }
}
