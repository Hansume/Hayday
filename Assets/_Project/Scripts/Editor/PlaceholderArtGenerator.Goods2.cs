using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>
    /// Icons for the later Bakery and Dairy goods, in the same style as the first ones.
    /// </summary>
    public static partial class PlaceholderArtGenerator
    {
        private static void WriteLaterGoodsArt()
        {
            WritePng("icon_corn_bread", BuildGoodsIcon(DrawCornBreadIcon));
            WritePng("icon_carrot_cake", BuildGoodsIcon(DrawCarrotCakeIcon));
            WritePng("icon_potato_pie", BuildGoodsIcon(DrawPotatoPieIcon));
            WritePng("icon_strawberry_tart", BuildGoodsIcon(DrawStrawberryTartIcon));
            WritePng("icon_tomato_quiche", BuildGoodsIcon(DrawTomatoQuicheIcon));
            WritePng("icon_pumpkin_pie", BuildGoodsIcon(DrawPumpkinPieIcon));
            WritePng("icon_ice_cream", BuildGoodsIcon(DrawIceCreamIcon));
            WritePng("icon_chili_cheese", BuildGoodsIcon(DrawChiliCheeseIcon));
            WritePng("icon_fudge", BuildGoodsIcon(DrawFudgeIcon));
        }

        /// <summary>A round pastry case seen from above, with a crimped rim around the filling.</summary>
        private static void DrawPastryCase(Texture2D layer, Color pastry, Color filling)
        {
            DrawShadedEllipse(layer, 64f, 50f, 50f, 28f, 0f, Scale(pastry, 0.86f), 0.1f);
            for (var i = 0; i < 18; i++)
            {
                var angle = i * Mathf.PI * 2f / 18f;
                DrawShadedEllipse(layer, 64f + Mathf.Cos(angle) * 44f, 58f + Mathf.Sin(angle) * 22f, 8f, 7f, 0f, pastry, 0.3f);
            }

            DrawShadedEllipse(layer, 64f, 60f, 37f, 17f, 0f, filling, 0.5f);
        }

        private static void DrawCornBreadIcon(Texture2D layer)
        {
            // A golden loaf rising out of its tin, flecked with corn.
            var tin = Rgb(150, 156, 164);
            PaintBox(layer, 64f, 38f, 46f, 18f, 4f, Scale(tin, 1.1f), Scale(tin, 0.76f));

            var crust = Rgb(236, 180, 70);
            DrawShadedEllipse(layer, 64f, 66f, 44f, 26f, 0f, crust, 0.45f);
            PaintBox(layer, 64f, 54f, 42f, 12f, 2f, crust, Scale(crust, 0.92f));
            DrawShadedEllipse(layer, 42f, 72f, 6f, 4f, 0f, Rgb(255, 224, 110), 0.5f);
            DrawShadedEllipse(layer, 66f, 80f, 6f, 4f, 0f, Rgb(255, 224, 110), 0.5f);
            DrawShadedEllipse(layer, 88f, 70f, 6f, 4f, 0f, Rgb(255, 224, 110), 0.5f);
            DrawShadedEllipse(layer, 54f, 60f, 5f, 3.5f, 0f, Rgb(255, 224, 110), 0.5f);
            DrawShadedEllipse(layer, 78f, 58f, 5f, 3.5f, 0f, Rgb(255, 224, 110), 0.5f);
        }

        private static void DrawCarrotCakeIcon(Texture2D layer)
        {
            // A round cake: brown sponge, thick white frosting, a little carrot on top.
            var sponge = Rgb(186, 122, 66);
            PaintBox(layer, 64f, 44f, 44f, 22f, 8f, sponge, Scale(sponge, 0.78f));
            DrawLine(layer, 22f, 44f, 106f, 44f, 3f, Rgb(250, 240, 220));

            var frosting = Rgb(252, 246, 234);
            DrawShadedEllipse(layer, 64f, 66f, 46f, 18f, 0f, frosting, 0.25f);
            for (var i = 0; i < 5; i++)
            {
                DrawShadedEllipse(layer, 28f + i * 18f, 56f - Mathf.Abs(i - 2) * 1.5f, 8f, 7f, 0f, frosting, 0.2f);
            }

            var carrot = Rgb(240, 130, 40);
            DrawStroke(layer, new Vector2(52f, 82f), new Vector2(66f, 74f), new Vector2(82f, 68f), t => Mathf.Lerp(6f, 1f, t), carrot, 0f, 0.3f);
            DrawLeaf(layer, new Vector2(52f, 82f), new Vector2(46f, 90f), new Vector2(42f, 100f), 4f, Rgb(92, 170, 80));
            DrawLeaf(layer, new Vector2(52f, 82f), new Vector2(54f, 92f), new Vector2(58f, 102f), 4f, Rgb(120, 196, 96));
        }

        private static void DrawPotatoPieIcon(Texture2D layer)
        {
            // A covered pie: domed golden crust with steam vents.
            var crust = Rgb(226, 170, 90);
            DrawPastryCase(layer, Rgb(206, 142, 68), crust);
            DrawShadedEllipse(layer, 64f, 64f, 36f, 20f, 0f, crust, 0.6f);
            var vent = Rgb(150, 96, 42);
            DrawLine(layer, 46f, 58f, 54f, 72f, 3f, vent);
            DrawLine(layer, 60f, 58f, 68f, 72f, 3f, vent);
            DrawLine(layer, 74f, 58f, 82f, 72f, 3f, vent);
        }

        private static void DrawStrawberryTartIcon(Texture2D layer)
        {
            // Custard filling heaped with strawberries.
            DrawPastryCase(layer, Rgb(216, 154, 78), Rgb(255, 226, 130));
            var berry = Rgb(224, 50, 56);
            var spots = new[]
            {
                new Vector2(42f, 58f), new Vector2(64f, 52f), new Vector2(86f, 58f),
                new Vector2(52f, 72f), new Vector2(76f, 72f)
            };

            for (var i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                DrawShadedEllipse(layer, spot.x, spot.y, 12f, 11f, 0f, berry, 0.6f);
                DrawLeaf(layer, new Vector2(spot.x, spot.y + 8f), new Vector2(spot.x - 3f, spot.y + 13f), new Vector2(spot.x - 8f, spot.y + 14f), 3f, Rgb(92, 170, 80));
                DrawLeaf(layer, new Vector2(spot.x, spot.y + 8f), new Vector2(spot.x + 3f, spot.y + 13f), new Vector2(spot.x + 8f, spot.y + 14f), 3f, Rgb(120, 196, 96));
                DrawEllipse(layer, spot.x - 4f, spot.y - 2f, 1.3f, 1.3f, Rgb(255, 224, 150), 0f);
                DrawEllipse(layer, spot.x + 4f, spot.y - 4f, 1.3f, 1.3f, Rgb(255, 224, 150), 0f);
            }
        }

        private static void DrawTomatoQuicheIcon(Texture2D layer)
        {
            // Pale egg filling topped with tomato slices and herbs.
            DrawPastryCase(layer, Rgb(210, 150, 76), Rgb(250, 228, 150));
            var spots = new[] { new Vector2(44f, 62f), new Vector2(84f, 62f), new Vector2(64f, 56f), new Vector2(64f, 72f) };
            for (var i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                DrawShadedEllipse(layer, spot.x, spot.y, 12f, 8f, 0f, Rgb(214, 60, 44), 0.3f);
                DrawEllipse(layer, spot.x, spot.y, 8f, 5f, Rgb(246, 120, 90), 0f);
                DrawEllipse(layer, spot.x, spot.y, 2f, 1.5f, Rgb(214, 60, 44), 0f);
            }

            DrawLeaf(layer, new Vector2(46f, 74f), new Vector2(50f, 78f), new Vector2(56f, 79f), 3f, Rgb(92, 170, 80));
            DrawLeaf(layer, new Vector2(74f, 50f), new Vector2(79f, 49f), new Vector2(84f, 51f), 3f, Rgb(120, 196, 96));
        }

        private static void DrawPumpkinPieIcon(Texture2D layer)
        {
            // Smooth orange filling and a dollop of cream.
            DrawPastryCase(layer, Rgb(204, 136, 62), Rgb(232, 124, 40));
            var cream = Rgb(255, 250, 240);
            DrawShadedEllipse(layer, 64f, 64f, 17f, 9f, 0f, cream, 0.2f);
            DrawShadedEllipse(layer, 64f, 71f, 11f, 7f, 0f, cream, 0.2f);
            DrawShadedEllipse(layer, 65f, 78f, 6f, 5f, 0f, cream, 0.2f);
        }

        private static void DrawIceCreamIcon(Texture2D layer)
        {
            // A waffle cone with a pink scoop on a white one.
            var waffle = Rgb(226, 170, 90);
            DrawTriangle(layer, new Vector2(38f, 70f), new Vector2(90f, 70f), new Vector2(64f, 12f), waffle);
            DrawTriangle(layer, new Vector2(64f, 70f), new Vector2(90f, 70f), new Vector2(64f, 12f), Scale(waffle, 0.84f));
            var line = Scale(waffle, 0.68f);
            DrawLine(layer, 46f, 70f, 72f, 30f, 2f, line);
            DrawLine(layer, 64f, 70f, 80f, 46f, 2f, line);
            DrawLine(layer, 82f, 70f, 56f, 30f, 2f, line);
            DrawLine(layer, 64f, 70f, 48f, 46f, 2f, line);

            DrawShadedEllipse(layer, 64f, 76f, 28f, 22f, 0f, Rgb(252, 248, 238), 0.3f);
            DrawShadedEllipse(layer, 64f, 100f, 21f, 18f, 0f, Rgb(248, 150, 170), 0.5f);
            DrawShadedEllipse(layer, 64f, 113f, 6f, 6f, 0f, Rgb(214, 40, 56), 0.7f);
        }

        private static void DrawChiliCheeseIcon(Texture2D layer)
        {
            // A wheel of cheese flecked with chili, one pepper leaning on it.
            var face = Rgb(255, 214, 110);
            PaintBox(layer, 58f, 38f, 42f, 18f, 8f, Scale(face, 0.96f), Scale(face, 0.74f));
            DrawShadedEllipse(layer, 58f, 56f, 42f, 19f, 0f, face, 0.25f);

            var fleck = Rgb(214, 56, 40);
            DrawLine(layer, 34f, 58f, 40f, 54f, 3f, fleck);
            DrawLine(layer, 52f, 68f, 58f, 66f, 3f, fleck);
            DrawLine(layer, 68f, 52f, 74f, 56f, 3f, fleck);
            DrawLine(layer, 50f, 48f, 56f, 50f, 3f, fleck);
            DrawLine(layer, 26f, 36f, 32f, 32f, 3f, fleck);
            DrawLine(layer, 58f, 30f, 64f, 34f, 3f, fleck);
            DrawLine(layer, 82f, 34f, 88f, 30f, 3f, fleck);

            DrawStroke(layer, new Vector2(98f, 74f), new Vector2(116f, 56f), new Vector2(102f, 22f), t => Mathf.Lerp(9f, 1f, t), Rgb(222, 44, 36), 0f, 0.5f);
            DrawLeaf(layer, new Vector2(97f, 74f), new Vector2(94f, 82f), new Vector2(98f, 90f), 3.5f, Rgb(92, 170, 80));
        }

        private static void DrawFudgeIcon(Texture2D layer)
        {
            // Three squares of fudge in a little stack.
            DrawFudgeSquare(layer, 42f, 34f);
            DrawFudgeSquare(layer, 88f, 34f);
            DrawFudgeSquare(layer, 65f, 62f);
        }

        private static void DrawFudgeSquare(Texture2D layer, float x, float y)
        {
            var brown = Rgb(138, 84, 48);
            PaintBox(layer, x, y, 24f, 14f, 3f, brown, Scale(brown, 0.76f));
            DrawTriangle(layer, new Vector2(x - 24f, y + 14f), new Vector2(x + 24f, y + 14f), new Vector2(x + 18f, y + 26f), Rgb(176, 114, 68));
            DrawTriangle(layer, new Vector2(x - 24f, y + 14f), new Vector2(x + 18f, y + 26f), new Vector2(x - 18f, y + 26f), Rgb(176, 114, 68));
            DrawLine(layer, x - 14f, y + 20f, x + 12f, y + 20f, 2f, Rgb(214, 160, 110));
        }
    }
}
