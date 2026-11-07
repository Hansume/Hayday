using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>
    /// Fruit trees and their fruit. Each tree has two pictures: building_&lt;id&gt; with fruit on it
    /// and building_&lt;id&gt;_growing without.
    /// </summary>
    public static partial class PlaceholderArtGenerator
    {
        private const int TreeWidth = 320;
        private const int TreeHeight = 400;
        private const float TreeBase = 56f;

        private sealed class FruitTreeArt
        {
            public string TreeId;
            public string FruitId;
            public Color Leaf;
            public bool IsPalm;
            public System.Action<Texture2D, float, float, float> DrawFruit;
        }

        private static readonly FruitTreeArt[] FruitTrees =
        {
            new FruitTreeArt { TreeId = "apple_tree", FruitId = "apple", Leaf = Rgb(84, 158, 62), DrawFruit = DrawApple },
            new FruitTreeArt { TreeId = "lemon_tree", FruitId = "lemon", Leaf = Rgb(62, 140, 74), DrawFruit = DrawLemon },
            new FruitTreeArt { TreeId = "peach_tree", FruitId = "peach", Leaf = Rgb(112, 164, 66), DrawFruit = DrawPeach },
            new FruitTreeArt { TreeId = "orange_tree", FruitId = "orange", Leaf = Rgb(48, 124, 66), DrawFruit = DrawOrange },
            new FruitTreeArt { TreeId = "mango_tree", FruitId = "mango", Leaf = Rgb(70, 132, 50), DrawFruit = DrawMango },
            new FruitTreeArt { TreeId = "coconut_palm", FruitId = "coconut", Leaf = Rgb(92, 166, 70), IsPalm = true, DrawFruit = DrawCoconut }
        };

        private static void WriteFruitTreeArt()
        {
            foreach (var art in FruitTrees)
            {
                WritePng($"icon_{art.FruitId}", BuildGoodsIcon(layer => art.DrawFruit(layer, 64f, 62f, 3f)));

                var withFruit = BuildFruitTree(art, true);
                WritePng($"icon_{art.TreeId}", FitToIcon(withFruit));
                WritePng($"building_{art.TreeId}", withFruit);
                WritePng($"building_{art.TreeId}_growing", BuildFruitTree(art, false));
            }
        }

        private static void ConfigureFruitTreeSprites()
        {
            var pivot = new Vector2(0.5f, TreeBase / TreeHeight);
            foreach (var art in FruitTrees)
            {
                ConfigureSprite($"icon_{art.FruitId}", 100f, SpriteAlignment.Center, Vector4.zero);
                ConfigureSprite($"icon_{art.TreeId}", 100f, SpriteAlignment.Center, Vector4.zero);
                ConfigureSprite($"building_{art.TreeId}", 128f, SpriteAlignment.Custom, Vector4.zero, pivot);
                ConfigureSprite($"building_{art.TreeId}_growing", 128f, SpriteAlignment.Custom, Vector4.zero, pivot);
            }
        }

        private static Texture2D BuildFruitTree(FruitTreeArt art, bool withFruit)
        {
            var texture = NewTexture(TreeWidth, TreeHeight);
            DrawEllipse(texture, 160f, TreeBase, 96f, 22f, Shadow, 0f);
            var layer = NewTexture(TreeWidth, TreeHeight);
            if (art.IsPalm)
            {
                DrawPalm(layer, art, withFruit);
            }
            else
            {
                DrawRoundTree(layer, art, withFruit);
            }

            FinishLayer(texture, layer, IconOutline);
            texture.Apply(false, false);
            return texture;
        }

        private static void DrawRoundTree(Texture2D layer, FruitTreeArt art, bool withFruit)
        {
            var bark = Rgb(132, 88, 50);
            DrawStroke(layer, new Vector2(160f, TreeBase), new Vector2(158f, 120f), new Vector2(160f, 200f), t => Mathf.Lerp(24f, 14f, t), bark, 0f, 0.2f);
            DrawStroke(layer, new Vector2(160f, 150f), new Vector2(130f, 180f), new Vector2(110f, 220f), t => Mathf.Lerp(10f, 5f, t), bark, 0f, 0.2f);
            DrawStroke(layer, new Vector2(160f, 150f), new Vector2(190f, 180f), new Vector2(210f, 220f), t => Mathf.Lerp(10f, 5f, t), bark, 0f, 0.2f);

            // Darker clumps behind, lighter ones in front, so the canopy looks round.
            DrawShadedEllipse(layer, 160f, 244f, 138f, 96f, 0f, Scale(art.Leaf, 0.72f), 0.1f);
            DrawShadedEllipse(layer, 82f, 232f, 68f, 58f, 0f, Scale(art.Leaf, 0.86f), 0.25f);
            DrawShadedEllipse(layer, 238f, 232f, 68f, 58f, 0f, Scale(art.Leaf, 0.82f), 0.25f);
            DrawShadedEllipse(layer, 112f, 292f, 72f, 60f, 0f, art.Leaf, 0.35f);
            DrawShadedEllipse(layer, 208f, 292f, 72f, 60f, 0f, Scale(art.Leaf, 0.94f), 0.35f);
            DrawShadedEllipse(layer, 160f, 250f, 92f, 70f, 0f, Scale(art.Leaf, 1.08f), 0.35f);

            if (withFruit)
            {
                var spots = new[]
                {
                    new Vector2(84f, 232f), new Vector2(236f, 228f), new Vector2(160f, 206f),
                    new Vector2(116f, 276f), new Vector2(206f, 280f), new Vector2(160f, 316f),
                    new Vector2(122f, 200f), new Vector2(200f, 196f), new Vector2(160f, 262f)
                };

                for (var i = 0; i < spots.Length; i++)
                {
                    art.DrawFruit(layer, spots[i].x, spots[i].y, 1.5f);
                }
            }
            else
            {
                // Pale blossoms: the fruit is still on its way.
                for (var i = 0; i < 14; i++)
                {
                    var x = 70f + Noise01(i, 11, 4) * 180f;
                    var y = 190f + Noise01(i, 5, 9) * 130f;
                    DrawEllipse(layer, x, y, 5f, 5f, Rgb(255, 244, 240), 0f);
                    DrawEllipse(layer, x, y, 2f, 2f, Rgb(250, 214, 110), 0f);
                }
            }
        }

        private static void DrawPalm(Texture2D layer, FruitTreeArt art, bool withFruit)
        {
            var bark = Rgb(158, 118, 76);
            var top = new Vector2(180f, 300f);
            DrawStroke(layer, new Vector2(160f, TreeBase), new Vector2(128f, 190f), top, t => Mathf.Lerp(24f, 13f, t), bark, 0f, 0.2f);
            for (var i = 1; i < 9; i++)
            {
                var t = i / 9f;
                var a = Vector2.Lerp(Vector2.Lerp(new Vector2(160f, TreeBase), new Vector2(128f, 190f), t), Vector2.Lerp(new Vector2(128f, 190f), top, t), t);
                var halfWidth = Mathf.Lerp(24f, 13f, t);
                DrawLine(layer, a.x - halfWidth, a.y, a.x + halfWidth, a.y + 2f, 2.5f, Scale(bark, 0.72f));
            }

            var ends = new[]
            {
                new Vector2(40f, 262f), new Vector2(66f, 340f), new Vector2(136f, 386f),
                new Vector2(226f, 382f), new Vector2(296f, 330f), new Vector2(308f, 256f)
            };

            for (var i = 0; i < ends.Length; i++)
            {
                var control = Vector2.Lerp(top, ends[i], 0.55f) + new Vector2(0f, 32f);
                var shade = i % 2 == 0 ? art.Leaf : Scale(art.Leaf, 0.84f);
                DrawLeaf(layer, top, control, ends[i], 22f, shade);
            }

            if (withFruit)
            {
                art.DrawFruit(layer, 156f, 280f, 1.5f);
                art.DrawFruit(layer, 204f, 278f, 1.5f);
                art.DrawFruit(layer, 180f, 250f, 1.5f);
            }
        }

        // ------------------------------------------------------------------ Fruit
        //
        // Each draws at (x, y) with a body about 28 pixels across times the scale: 1.5 for fruit
        // on a tree, 3 for an item icon.

        private static void DrawFruitLeaf(Texture2D layer, float x, float y, float scale)
        {
            DrawLeaf(layer, new Vector2(x, y), new Vector2(x + 5f * scale, y + 5f * scale), new Vector2(x + 11f * scale, y + 3f * scale), 3f * scale, Rgb(92, 170, 80));
        }

        private static void DrawApple(Texture2D layer, float x, float y, float scale)
        {
            var red = Rgb(216, 44, 44);
            DrawShadedEllipse(layer, x - 4f * scale, y, 10f * scale, 13f * scale, 0f, red, 0.6f);
            DrawShadedEllipse(layer, x + 4f * scale, y, 10f * scale, 13f * scale, 0f, red, 0.4f);
            DrawLine(layer, x, y + 10f * scale, x + 1f * scale, y + 16f * scale, 1.4f * scale, Rgb(96, 62, 34));
            DrawFruitLeaf(layer, x + 1f * scale, y + 13f * scale, scale);
        }

        private static void DrawLemon(Texture2D layer, float x, float y, float scale)
        {
            var yellow = Rgb(250, 222, 70);
            DrawShadedEllipse(layer, x + 12f * scale, y + 2f * scale, 3.5f * scale, 3f * scale, 0f, yellow, 0.2f);
            DrawShadedEllipse(layer, x, y, 14f * scale, 11f * scale, 12f, yellow, 0.6f);
            DrawFruitLeaf(layer, x - 11f * scale, y + 6f * scale, scale);
        }

        private static void DrawPeach(Texture2D layer, float x, float y, float scale)
        {
            DrawShadedEllipse(layer, x, y, 13f * scale, 13f * scale, 0f, Rgb(250, 176, 110), 0.5f);
            DrawEllipse(layer, x + 5f * scale, y - 3f * scale, 7f * scale, 8f * scale, new Color(0.92f, 0.32f, 0.3f, 0.55f), 0f);
            DrawStroke(layer, new Vector2(x, y + 12f * scale), new Vector2(x - 5f * scale, y), new Vector2(x, y - 11f * scale), t => 0.8f * scale, Rgb(204, 112, 76), 0f, 0f);
            DrawFruitLeaf(layer, x, y + 12f * scale, scale);
        }

        private static void DrawOrange(Texture2D layer, float x, float y, float scale)
        {
            DrawShadedEllipse(layer, x, y, 13f * scale, 13f * scale, 0f, Rgb(246, 146, 34), 0.6f);
            DrawEllipse(layer, x, y + 10f * scale, 2f * scale, 1.5f * scale, Rgb(150, 100, 40), 0f);
            DrawFruitLeaf(layer, x, y + 11f * scale, scale);
        }

        private static void DrawMango(Texture2D layer, float x, float y, float scale)
        {
            DrawShadedEllipse(layer, x, y, 11f * scale, 15f * scale, -22f, Rgb(250, 190, 56), 0.5f);
            DrawEllipse(layer, x - 4f * scale, y + 4f * scale, 6f * scale, 8f * scale, new Color(0.9f, 0.28f, 0.2f, 0.6f), -22f);
            DrawFruitLeaf(layer, x + 5f * scale, y + 12f * scale, scale);
        }

        private static void DrawCoconut(Texture2D layer, float x, float y, float scale)
        {
            DrawShadedEllipse(layer, x, y, 14f * scale, 13f * scale, 0f, Rgb(116, 76, 44), 0.25f);
            if (scale > 2f)
            {
                // The item icon is cut open to show the white inside.
                DrawShadedEllipse(layer, x, y + 3f * scale, 11f * scale, 8.5f * scale, 0f, Rgb(250, 248, 240), 0.2f);
                DrawEllipse(layer, x, y + 3f * scale, 6f * scale, 4f * scale, Rgb(222, 232, 234), 0f);
            }
            else
            {
                DrawEllipse(layer, x - 3f * scale, y + 2f * scale, 1.6f * scale, 1.6f * scale, Rgb(58, 36, 20), 0f);
                DrawEllipse(layer, x + 3f * scale, y + 2f * scale, 1.6f * scale, 1.6f * scale, Rgb(58, 36, 20), 0f);
            }
        }
    }
}
