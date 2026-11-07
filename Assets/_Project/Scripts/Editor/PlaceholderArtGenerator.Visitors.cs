using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>
    /// Visitors (four townsfolk, standing, facing the viewer) and the reputation icon.
    /// visitor_0 ... visitor_3 match VisitorService.LookCount.
    /// </summary>
    public static partial class PlaceholderArtGenerator
    {
        private const int VisitorWidth = 192;
        private const int VisitorHeight = 256;

        /// <summary>Where a visitor's feet are, from the bottom of the picture.</summary>
        private const float VisitorFeetY = 14f;

        private struct VisitorStyle
        {
            public Color Skin;
            public Color Hair;
            public Color Shirt;
            public Color Trousers;
            public Color Accent;
            public bool Hat;
            public bool LongHair;
            public bool Basket;
        }

        private static readonly VisitorStyle[] VisitorStyles =
        {
            // A farmer in a straw hat and blue dungarees.
            new VisitorStyle { Skin = Rgb(240, 200, 166), Hair = Rgb(120, 76, 40), Shirt = Rgb(214, 70, 62), Trousers = Rgb(66, 110, 170), Accent = Rgb(232, 196, 108), Hat = true },
            // A baker with long dark hair and an apron, carrying a basket.
            new VisitorStyle { Skin = Rgb(198, 142, 102), Hair = Rgb(52, 36, 28), Shirt = Rgb(246, 236, 214), Trousers = Rgb(150, 90, 120), Accent = Rgb(186, 138, 82), LongHair = true, Basket = true },
            // A grey-haired neighbour in a green cardigan.
            new VisitorStyle { Skin = Rgb(244, 212, 182), Hair = Rgb(206, 206, 200), Shirt = Rgb(98, 150, 86), Trousers = Rgb(120, 98, 76), Accent = Rgb(236, 220, 160) },
            // A kid in a yellow raincoat and red cap.
            new VisitorStyle { Skin = Rgb(150, 100, 70), Hair = Rgb(40, 28, 22), Shirt = Rgb(244, 196, 54), Trousers = Rgb(70, 70, 92), Accent = Rgb(204, 64, 54), Hat = true }
        };

        /// <summary>Writes and imports just the visitor art (GenerateAll does this too).</summary>
        public static void GenerateVisitorArt()
        {
            EnsureFolder(ArtDirectory);
            WriteVisitorArt();
            UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            ConfigureVisitorSprites();
            UnityEditor.AssetDatabase.Refresh();
        }

        private static void WriteVisitorArt()
        {
            for (var i = 0; i < VisitorStyles.Length; i++)
            {
                WritePng($"visitor_{i}", BuildVisitor(VisitorStyles[i], i));
            }

            WritePng("icon_reputation", BuildReputationIcon());
        }

        private static void ConfigureVisitorSprites()
        {
            for (var i = 0; i < VisitorStyles.Length; i++)
            {
                ConfigureSprite(
                    $"visitor_{i}",
                    180f,
                    SpriteAlignment.Custom,
                    Vector4.zero,
                    new Vector2(0.5f, VisitorFeetY / VisitorHeight));
            }

            ConfigureSprite("icon_reputation", 100f, SpriteAlignment.Center, Vector4.zero);
        }

        private static Texture2D BuildVisitor(VisitorStyle style, int index)
        {
            var texture = NewTexture(VisitorWidth, VisitorHeight);
            DrawEllipse(texture, 96f, VisitorFeetY + 2f, 46f, 9f, Shadow, 0f);
            var layer = NewTexture(VisitorWidth, VisitorHeight);

            const float cx = 96f;
            var feet = VisitorFeetY;
            var shoe = Rgb(70, 48, 34);

            // Legs and shoes.
            PaintBox(layer, cx - 17f, feet + 34f, 12f, 30f, 6f, style.Trousers, Scale(style.Trousers, 0.82f));
            PaintBox(layer, cx + 17f, feet + 34f, 12f, 30f, 6f, style.Trousers, Scale(style.Trousers, 0.82f));
            DrawShadedEllipse(layer, cx - 19f, feet + 6f, 16f, 8f, 0f, shoe, 0.2f);
            DrawShadedEllipse(layer, cx + 19f, feet + 6f, 16f, 8f, 0f, shoe, 0.2f);

            // Body.
            PaintBox(layer, cx, feet + 98f, 40f, 42f, 18f, style.Shirt, Scale(style.Shirt, 0.84f));
            if (index == 0)
            {
                // Dungaree bib and straps.
                PaintBox(layer, cx, feet + 86f, 26f, 26f, 6f, style.Trousers, Scale(style.Trousers, 0.85f));
                DrawLine(layer, cx - 22f, feet + 110f, cx - 26f, feet + 136f, 4f, style.Trousers);
                DrawLine(layer, cx + 22f, feet + 110f, cx + 26f, feet + 136f, 4f, style.Trousers);
            }
            else if (index == 1)
            {
                // Apron.
                PaintBox(layer, cx, feet + 82f, 28f, 34f, 8f, Rgb(255, 252, 244), Rgb(232, 224, 208));
            }
            else if (index == 2)
            {
                // Cardigan buttons.
                for (var b = 0; b < 3; b++)
                {
                    DrawEllipse(layer, cx, feet + 116f - b * 18f, 3.4f, 3.4f, style.Accent, 0f);
                }
            }
            else
            {
                // Raincoat toggles.
                DrawLine(layer, cx, feet + 136f, cx, feet + 60f, 2.4f, Scale(style.Shirt, 0.7f));
            }

            // Arms (hands at the sides, or holding a basket in front).
            PaintBox(layer, cx - 46f, feet + 98f, 10f, 32f, 9f, style.Shirt, Scale(style.Shirt, 0.8f));
            PaintBox(layer, cx + 46f, feet + 98f, 10f, 32f, 9f, style.Shirt, Scale(style.Shirt, 0.8f));
            DrawShadedEllipse(layer, cx - 46f, feet + 64f, 9f, 9f, 0f, style.Skin, 0.2f);
            DrawShadedEllipse(layer, cx + 46f, feet + 64f, 9f, 9f, 0f, style.Skin, 0.2f);
            if (style.Basket)
            {
                PaintBox(layer, cx + 50f, feet + 50f, 22f, 14f, 6f, style.Accent, Scale(style.Accent, 0.75f));
                DrawStroke(layer, new Vector2(cx + 30f, feet + 60f), new Vector2(cx + 50f, feet + 88f), new Vector2(cx + 70f, feet + 60f), t => 2.4f, Scale(style.Accent, 0.7f), 0f, 0f);
                DrawEllipse(layer, cx + 42f, feet + 64f, 8f, 6f, Rgb(214, 78, 60), 0f);
                DrawEllipse(layer, cx + 58f, feet + 64f, 8f, 6f, Rgb(236, 190, 80), 0f);
            }

            // Head.
            var headY = feet + 170f;
            DrawShadedEllipse(layer, cx, headY - 30f, 10f, 8f, 0f, style.Skin, 0.1f);
            if (style.LongHair)
            {
                DrawShadedEllipse(layer, cx, headY - 12f, 40f, 44f, 0f, style.Hair, 0.15f);
            }

            DrawShadedEllipse(layer, cx, headY, 34f, 36f, 0f, style.Skin, 0.25f);
            DrawShadedEllipse(layer, cx - 34f, headY - 2f, 6f, 9f, 0f, Scale(style.Skin, 0.92f), 0.1f);
            DrawShadedEllipse(layer, cx + 34f, headY - 2f, 6f, 9f, 0f, Scale(style.Skin, 0.92f), 0.1f);

            // Hair on top.
            DrawShadedEllipse(layer, cx, headY + 24f, 34f, 16f, 0f, style.Hair, 0.2f);
            DrawShadedEllipse(layer, cx - 18f, headY + 18f, 14f, 10f, -20f, style.Hair, 0.15f);

            // Face: eyes, rosy cheeks, smile.
            var eye = Rgb(46, 32, 24);
            DrawEllipse(layer, cx - 12f, headY + 2f, 4f, 5f, eye, 0f);
            DrawEllipse(layer, cx + 12f, headY + 2f, 4f, 5f, eye, 0f);
            DrawEllipse(layer, cx - 11f, headY + 4f, 1.4f, 1.4f, Color.white, 0f);
            DrawEllipse(layer, cx + 13f, headY + 4f, 1.4f, 1.4f, Color.white, 0f);
            DrawEllipse(layer, cx - 20f, headY - 10f, 6f, 4f, new Color(0.92f, 0.45f, 0.42f, 0.45f), 0f);
            DrawEllipse(layer, cx + 20f, headY - 10f, 6f, 4f, new Color(0.92f, 0.45f, 0.42f, 0.45f), 0f);
            DrawStroke(layer, new Vector2(cx - 9f, headY - 12f), new Vector2(cx, headY - 20f), new Vector2(cx + 9f, headY - 12f), t => 1.6f, Rgb(120, 56, 44), 0f, 0f);

            // Hats.
            if (style.Hat && index == 0)
            {
                DrawShadedEllipse(layer, cx, headY + 30f, 56f, 12f, 0f, style.Accent, 0.2f);
                PaintBox(layer, cx, headY + 46f, 30f, 18f, 12f, style.Accent, Scale(style.Accent, 0.85f));
                PaintBox(layer, cx, headY + 36f, 30f, 4f, 2f, style.Shirt, Scale(style.Shirt, 0.8f));
            }
            else if (style.Hat)
            {
                DrawShadedEllipse(layer, cx, headY + 32f, 36f, 20f, 0f, style.Accent, 0.25f);
                DrawShadedEllipse(layer, cx + 26f, headY + 22f, 22f, 7f, -8f, Scale(style.Accent, 0.85f), 0.1f);
            }

            FinishLayer(texture, layer, IconOutline);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>A rosy heart on a gold ribbon: reputation.</summary>
        private static Texture2D BuildReputationIcon()
        {
            var texture = NewTexture(IconSize, IconSize);
            DrawEllipse(texture, 64f, 12f, 40f, 7f, Shadow, 0f);
            var layer = NewTexture(IconSize, IconSize);

            var gold = Rgb(244, 190, 60);
            var goldDark = Rgb(196, 134, 36);
            DrawTriangle(layer, new Vector2(40f, 50f), new Vector2(54f, 50f), new Vector2(36f, 14f), goldDark);
            DrawTriangle(layer, new Vector2(74f, 50f), new Vector2(88f, 50f), new Vector2(92f, 14f), goldDark);
            DrawTriangle(layer, new Vector2(36f, 14f), new Vector2(46f, 22f), new Vector2(44f, 12f), gold);
            DrawTriangle(layer, new Vector2(92f, 14f), new Vector2(82f, 22f), new Vector2(84f, 12f), gold);

            var heart = Rgb(232, 74, 92);
            DrawShadedEllipse(layer, 48f, 80f, 24f, 24f, 0f, heart, 0.35f);
            DrawShadedEllipse(layer, 80f, 80f, 24f, 24f, 0f, heart, 0.35f);
            DrawTriangle(layer, new Vector2(26f, 74f), new Vector2(102f, 74f), new Vector2(64f, 28f), Scale(heart, 0.92f));
            DrawEllipse(layer, 44f, 88f, 7f, 5f, new Color(1f, 1f, 1f, 0.55f), -30f);

            FinishLayer(texture, layer, IconOutline);
            texture.Apply(false, false);
            return texture;
        }
    }
}
