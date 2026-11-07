using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>
    /// Goods buildings (Dairy, Bakery) and the goods they make, in
    /// the same soft-shaded, outlined style as the crops.
    /// </summary>
    public static partial class PlaceholderArtGenerator
    {
        private static readonly string[] GoodsIds =
        {
            "butter", "cheese", "egg_tart", "custard",
            "corn_bread", "carrot_cake", "potato_pie", "strawberry_tart", "tomato_quiche", "pumpkin_pie",
            "ice_cream", "chili_cheese", "fudge"
        };

        private static readonly string[] GoodsBuildingIds = { "dairy", "bakery" };

        private enum CottageStyle
        {
            Dairy,
            Bakery,
            Smokehouse,
            GoatStable,
            JuiceMaker
        }

        private static void WriteGoodsArt()
        {
            WritePng("icon_butter", BuildButterIcon());
            WritePng("icon_cheese", BuildCheeseIcon());
            WritePng("icon_egg_tart", BuildEggTartIcon());
            WritePng("icon_custard", BuildCustardIcon());
            WriteLaterGoodsArt();

            var board = BuildNoticeBoardWorld();
            WritePng("icon_notice_board", FitToIcon(board));
            WritePng("building_notice_board", board);

            var styles = new[] { CottageStyle.Dairy, CottageStyle.Bakery };
            for (var i = 0; i < GoodsBuildingIds.Length; i++)
            {
                var world = BuildCottage(styles[i]);
                WritePng($"icon_{GoodsBuildingIds[i]}", FitToIcon(world));
                WritePng($"building_{GoodsBuildingIds[i]}", world);
            }
        }

        private static void ConfigureGoodsSprites()
        {
            for (var i = 0; i < GoodsIds.Length; i++)
            {
                ConfigureSprite($"icon_{GoodsIds[i]}", 100f, SpriteAlignment.Center, Vector4.zero);
            }

            ConfigureSprite("icon_notice_board", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite(
                "building_notice_board",
                128f,
                SpriteAlignment.Custom,
                Vector4.zero,
                new Vector2(0.5f, NoticeBoardBase / NoticeBoardHeight));

            for (var i = 0; i < GoodsBuildingIds.Length; i++)
            {
                ConfigureSprite($"icon_{GoodsBuildingIds[i]}", 100f, SpriteAlignment.Center, Vector4.zero);
                ConfigureSprite(
                    $"building_{GoodsBuildingIds[i]}",
                    128f,
                    SpriteAlignment.Custom,
                    Vector4.zero,
                    new Vector2(0.5f, 26f / SmallBuildingHeight));
            }
        }

        // ------------------------------------------------------------------ Notice board

        private const int NoticeBoardWidth = 200;
        private const int NoticeBoardHeight = 240;
        private const float NoticeBoardBase = 18f;

        /// <summary>A wooden board on two posts under a little roof, with notes pinned to it.</summary>
        private static Texture2D BuildNoticeBoardWorld()
        {
            var texture = NewTexture(NoticeBoardWidth, NoticeBoardHeight);
            DrawEllipse(texture, 100f, 22f, 70f, 12f, Shadow, 0f);
            var layer = NewTexture(NoticeBoardWidth, NoticeBoardHeight);

            var post = Rgb(130, 86, 46);
            PaintBox(layer, 46f, 92f, 7f, 74f, 3f, Scale(post, 1.1f), Scale(post, 0.8f));
            PaintBox(layer, 154f, 92f, 7f, 74f, 3f, Scale(post, 1.1f), Scale(post, 0.8f));

            // Board with a frame and a cork face.
            PaintBox(layer, 100f, 120f, 78f, 54f, 6f, Rgb(170, 112, 60), Rgb(124, 78, 40));
            PaintBox(layer, 100f, 120f, 68f, 45f, 4f, Rgb(214, 162, 104), Rgb(190, 136, 82));
            for (var i = 0; i < 40; i++)
            {
                var x = 38f + Noise01(i, 3, 5) * 124f;
                var y = 80f + Noise01(i, 7, 2) * 80f;
                DrawEllipse(layer, x, y, 1.4f, 1.4f, Rgb(170, 120, 70), 0f);
            }

            // Little roof over the board.
            DrawTriangle(layer, new Vector2(14f, 172f), new Vector2(100f, 214f), new Vector2(100f, 172f), Rgb(184, 74, 54));
            DrawTriangle(layer, new Vector2(100f, 172f), new Vector2(100f, 214f), new Vector2(186f, 172f), Rgb(146, 56, 40));
            DrawLine(layer, 12f, 171f, 188f, 171f, 5f, Rgb(250, 238, 214));

            // Notes: two pale papers and a yellow one, each with a coloured pin and lines.
            DrawNote(layer, 64f, 128f, 24f, 28f, -6f, Rgb(255, 250, 236), UIPinRed);
            DrawNote(layer, 116f, 132f, 22f, 26f, 4f, Rgb(255, 232, 140), UIPinBlue);
            DrawNote(layer, 142f, 100f, 16f, 18f, -3f, Rgb(250, 244, 230), UIPinGreen);

            FinishLayer(texture, layer, IconOutline);
            texture.Apply(false, false);
            return texture;
        }

        private static readonly Color UIPinRed = Rgb(214, 66, 52);
        private static readonly Color UIPinBlue = Rgb(64, 128, 204);
        private static readonly Color UIPinGreen = Rgb(76, 160, 60);

        private static void DrawNote(Texture2D texture, float x, float y, float halfWidth, float halfHeight, float tilt, Color paper, Color pin)
        {
            // Tilt by shearing the lines; the paper itself stays a rounded box.
            PaintBox(texture, x, y, halfWidth, halfHeight, 2f, paper, Scale(paper, 0.9f));
            var ink = new Color(0.45f, 0.32f, 0.2f, 0.7f);
            var slope = Mathf.Tan(tilt * Mathf.Deg2Rad);
            for (var i = 0; i < 3; i++)
            {
                var ly = y + halfHeight * 0.35f - i * halfHeight * 0.45f;
                var left = x - halfWidth * 0.7f;
                var right = x + halfWidth * (i == 2 ? 0.2f : 0.7f);
                DrawLine(texture, left, ly + (left - x) * slope, right, ly + (right - x) * slope, 1.6f, ink);
            }

            DrawShadedEllipse(texture, x, y + halfHeight - 3f, 4.5f, 4.5f, 0f, pin, 0.6f);
        }

        // ------------------------------------------------------------------ Buildings

        /// <summary>A small gabled workshop with an emblem in the gable showing what it makes.</summary>
        private static Texture2D BuildCottage(CottageStyle style)
        {
            var texture = NewTexture(SmallBuildingWidth, SmallBuildingHeight);
            DrawEllipse(texture, 160f, 32f, 112f, 20f, Shadow, 0f);
            var layer = NewTexture(SmallBuildingWidth, SmallBuildingHeight);

            Color wallTop, wallBottom, roof, trim;
            switch (style)
            {
                case CottageStyle.Dairy:
                    wallTop = Rgb(250, 246, 236);
                    wallBottom = Rgb(214, 206, 190);
                    roof = Rgb(74, 128, 178);
                    trim = Rgb(250, 246, 236);
                    break;
                case CottageStyle.Bakery:
                    wallTop = Rgb(246, 220, 168);
                    wallBottom = Rgb(206, 170, 112);
                    roof = Rgb(190, 72, 52);
                    trim = Rgb(255, 244, 222);
                    break;
                case CottageStyle.GoatStable:
                    wallTop = Rgb(214, 176, 122);
                    wallBottom = Rgb(170, 128, 80);
                    roof = Rgb(98, 142, 72);
                    trim = Rgb(250, 240, 214);
                    break;
                case CottageStyle.JuiceMaker:
                    wallTop = Rgb(252, 240, 190);
                    wallBottom = Rgb(226, 202, 132);
                    roof = Rgb(236, 138, 44);
                    trim = Rgb(255, 250, 232);
                    break;
                default:
                    wallTop = Rgb(150, 104, 62);
                    wallBottom = Rgb(104, 68, 38);
                    roof = Rgb(96, 100, 110);
                    trim = Rgb(214, 190, 150);
                    break;
            }

            // Chimney sits behind the roof (workshops that cook or smoke only).
            var hasChimney = style == CottageStyle.Bakery || style == CottageStyle.Smokehouse;
            if (hasChimney)
            {
                PaintBox(layer, 206f, 196f, 11f, 24f, 2f, Rgb(170, 90, 70), Rgb(130, 64, 48));
                PaintBox(layer, 206f, 220f, 14f, 4f, 2f, Rgb(120, 60, 44), Rgb(120, 60, 44));
            }

            // Walls with plank lines for the smokehouse.
            PaintBox(layer, 160f, 86f, 62f, 58f, 4f, wallTop, wallBottom);
            if (style == CottageStyle.Smokehouse || style == CottageStyle.GoatStable)
            {
                var plank = style == CottageStyle.Smokehouse ? Rgb(84, 54, 30) : Rgb(140, 100, 60);
                for (var y = 44f; y < 140f; y += 16f)
                {
                    DrawLine(layer, 102f, y, 218f, y, 1.6f, plank);
                }
            }

            // Roof: lit left slope, shaded right slope, trim along the edges.
            DrawTriangle(layer, new Vector2(76f, 136f), new Vector2(160f, 226f), new Vector2(160f, 136f), Scale(roof, 1.12f));
            DrawTriangle(layer, new Vector2(160f, 136f), new Vector2(160f, 226f), new Vector2(244f, 136f), Scale(roof, 0.82f));
            for (var i = 1; i < 4; i++)
            {
                var t = i / 4f;
                var leftX = Mathf.Lerp(76f, 160f, t);
                var rightX = Mathf.Lerp(244f, 160f, t);
                var y = Mathf.Lerp(136f, 226f, t);
                DrawLine(layer, leftX + 6f, y - 2f, rightX - 6f, y - 2f, 1.4f, Scale(roof, 0.7f));
            }

            DrawLine(layer, 74f, 135f, 160f, 228f, 5f, trim);
            DrawLine(layer, 160f, 228f, 246f, 135f, 5f, trim);
            DrawLine(layer, 72f, 135f, 248f, 135f, 5f, trim);

            // Gable emblem.
            DrawShadedEllipse(layer, 160f, 170f, 22f, 22f, 0f, Rgb(255, 250, 236), 0.2f);
            DrawCottageEmblem(layer, style);

            // Door and windows.
            PaintBox(layer, 160f, 58f, 20f, 31f, 8f, Rgb(118, 74, 40), Rgb(86, 52, 26));
            DrawLine(layer, 160f, 30f, 160f, 86f, 1.5f, Rgb(70, 42, 20));
            DrawEllipse(layer, 170f, 58f, 2.6f, 2.6f, Hay, 0f);
            DrawWindow(layer, 122f, 100f, trim);
            DrawWindow(layer, 198f, 100f, trim);

            // Something by the door that says what goes on inside.
            switch (style)
            {
                case CottageStyle.Dairy:
                    // A milk churn and a goat-cheese round: it handles cow and goat milk.
                    DrawMilkChurn(layer, 252f, 30f);
                    PaintBox(layer, 66f, 38f, 20f, 12f, 3f, Rgb(170, 120, 70), Rgb(130, 88, 48));
                    DrawShadedEllipse(layer, 66f, 58f, 17f, 8f, 0f, Rgb(250, 244, 230), 0.3f);
                    break;
                case CottageStyle.Bakery:
                    DrawShadedEllipse(layer, 66f, 38f, 18f, 12f, 0f, FeedSack, 0.15f);
                    DrawShadedEllipse(layer, 66f, 50f, 9f, 5f, 0f, Rgb(250, 244, 230), 0.3f);
                    DrawShadedEllipse(layer, 246f, 36f, 13f, 9f, 0f, Rgb(236, 226, 206), 0.4f);
                    DrawShadedEllipse(layer, 241f, 42f, 5f, 6.5f, 0f, Rgb(255, 250, 240), 0.5f);
                    DrawShadedEllipse(layer, 251f, 41f, 5f, 6.5f, 0f, Rgb(250, 236, 214), 0.5f);
                    break;
                case CottageStyle.GoatStable:
                    // A hay bale and a rock to climb on.
                    PaintBox(layer, 254f, 46f, 22f, 16f, 5f, Rgb(236, 204, 108), Rgb(196, 160, 70));
                    DrawLine(layer, 240f, 30f, 240f, 62f, 2f, Rgb(170, 120, 50));
                    DrawLine(layer, 268f, 30f, 268f, 62f, 2f, Rgb(170, 120, 50));
                    DrawShadedEllipse(layer, 66f, 40f, 22f, 14f, 0f, Rgb(166, 162, 152), 0.25f);
                    break;
                case CottageStyle.JuiceMaker:
                    // A crate of fruit and a jug of juice.
                    PaintBox(layer, 66f, 40f, 24f, 14f, 3f, Rgb(182, 130, 76), Rgb(136, 92, 50));
                    DrawShadedEllipse(layer, 52f, 58f, 10f, 10f, 0f, Rgb(224, 60, 50), 0.5f);
                    DrawShadedEllipse(layer, 70f, 60f, 10f, 10f, 0f, Rgb(250, 150, 40), 0.5f);
                    DrawShadedEllipse(layer, 84f, 56f, 9f, 9f, 0f, Rgb(250, 220, 70), 0.5f);
                    PaintBox(layer, 250f, 46f, 14f, 22f, 6f, Rgb(252, 176, 70), Rgb(226, 130, 40));
                    PaintBox(layer, 250f, 72f, 8f, 5f, 2f, Rgb(226, 240, 246), Rgb(190, 212, 222));
                    break;
                default:
                    for (var i = 0; i < 3; i++)
                    {
                        var x = 236f + i * 20f;
                        var y = i == 1 ? 50f : 34f;
                        DrawShadedEllipse(layer, x, y, 11f, 9f, 0f, Rgb(140, 92, 52), 0.1f);
                        DrawEllipse(layer, x, y, 6f, 5f, Rgb(196, 152, 98), 0f);
                    }

                    break;
            }

            FinishLayer(texture, layer, IconOutline);

            // Smoke drifts from the chimney, soft and without an outline.
            if (hasChimney)
            {
                var smoke = new Color(0.94f, 0.94f, 0.94f, 0.55f);
                DrawEllipse(texture, 210f, 234f, 9f, 7f, smoke, 0f);
                DrawEllipse(texture, 222f, 243f, 7f, 5.5f, new Color(smoke.r, smoke.g, smoke.b, 0.4f), 0f);
                DrawEllipse(texture, 233f, 250f, 5f, 4f, new Color(smoke.r, smoke.g, smoke.b, 0.28f), 0f);
            }

            texture.Apply(false, false);
            return texture;
        }

        private static void DrawWindow(Texture2D texture, float x, float y, Color frame)
        {
            PaintBox(texture, x, y, 15f, 15f, 3f, frame, frame);
            PaintBox(texture, x, y, 11f, 11f, 2f, Rgb(170, 214, 236), Rgb(108, 160, 200));
            DrawLine(texture, x, y - 11f, x, y + 11f, 2f, frame);
            DrawLine(texture, x - 11f, y, x + 11f, y, 2f, frame);
        }

        private static void DrawCottageEmblem(Texture2D texture, CottageStyle style)
        {
            switch (style)
            {
                case CottageStyle.Dairy:
                    DrawMilkChurn(texture, 160f, 156f, 0.55f);
                    break;
                case CottageStyle.Bakery:
                    // A loaf with three slashes.
                    DrawShadedEllipse(texture, 160f, 170f, 15f, 9f, 0f, Rgb(222, 160, 84), 0.5f);
                    for (var i = -1; i <= 1; i++)
                    {
                        DrawLine(texture, 160f + i * 7f - 3f, 166f, 160f + i * 7f + 3f, 175f, 1.6f, Rgb(250, 226, 170));
                    }

                    break;
                case CottageStyle.GoatStable:
                {
                    // A goat's face with curved horns.
                    var horn = Rgb(176, 150, 110);
                    DrawStroke(texture, new Vector2(154f, 176f), new Vector2(146f, 188f), new Vector2(140f, 184f), t => Mathf.Lerp(3.5f, 1f, t), horn, 0f, 0.3f);
                    DrawStroke(texture, new Vector2(166f, 176f), new Vector2(174f, 188f), new Vector2(180f, 184f), t => Mathf.Lerp(3.5f, 1f, t), horn, 0f, 0.3f);
                    DrawShadedEllipse(texture, 160f, 166f, 10f, 13f, 0f, Rgb(250, 246, 236), 0.3f);
                    DrawLeaf(texture, new Vector2(152f, 172f), new Vector2(144f, 172f), new Vector2(138f, 168f), 3f, Rgb(236, 228, 214));
                    DrawLeaf(texture, new Vector2(168f, 172f), new Vector2(176f, 172f), new Vector2(182f, 168f), 3f, Rgb(236, 228, 214));
                    DrawEllipse(texture, 156f, 170f, 1.6f, 1.6f, Rgb(50, 36, 24), 0f);
                    DrawEllipse(texture, 164f, 170f, 1.6f, 1.6f, Rgb(50, 36, 24), 0f);
                    break;
                }
                case CottageStyle.JuiceMaker:
                    // An orange slice.
                    DrawShadedEllipse(texture, 160f, 170f, 15f, 15f, 0f, Rgb(244, 130, 30), 0.3f);
                    DrawEllipse(texture, 160f, 170f, 11f, 11f, Rgb(255, 196, 90), 0f);
                    for (var i = 0; i < 3; i++)
                    {
                        var angle = i * Mathf.PI / 3f;
                        var dx = Mathf.Cos(angle) * 11f;
                        var dy = Mathf.Sin(angle) * 11f;
                        DrawLine(texture, 160f - dx, 170f - dy, 160f + dx, 170f + dy, 1.6f, Rgb(255, 240, 200));
                    }

                    break;
                default:
                    DrawTriangle(texture, new Vector2(169f, 170f), new Vector2(180f, 178f), new Vector2(180f, 162f), Rgb(196, 120, 52));
                    DrawShadedEllipse(texture, 158f, 170f, 13f, 8f, 0f, Rgb(214, 136, 60), 0.35f);
                    DrawEllipse(texture, 151f, 172f, 1.8f, 1.8f, Rgb(50, 30, 16), 0f);
                    break;
            }
        }

        private static void DrawMilkChurn(Texture2D texture, float x, float baseY, float scale = 1f)
        {
            var steel = Rgb(196, 204, 210);
            PaintBox(texture, x, baseY + 22f * scale, 16f * scale, 22f * scale, 6f * scale, Scale(steel, 1.08f), Scale(steel, 0.78f));
            PaintBox(texture, x, baseY + 48f * scale, 9f * scale, 6f * scale, 3f * scale, steel, Scale(steel, 0.8f));
            DrawShadedEllipse(texture, x, baseY + 56f * scale, 11f * scale, 4f * scale, 0f, steel, 0.4f);
            DrawLine(texture, x - 16f * scale, baseY + 30f * scale, x + 16f * scale, baseY + 30f * scale, 2f * scale, Scale(steel, 0.7f));
        }

        /// <summary>Shrinks a building sprite into a square icon, keeping its proportions.</summary>
        private static Texture2D FitToIcon(Texture2D source)
        {
            var icon = NewTexture(IconSize, IconSize);
            var scale = Mathf.Min((IconSize - 8f) / source.width, (IconSize - 8f) / source.height);
            var width = source.width * scale;
            var height = source.height * scale;
            var offsetX = (IconSize - width) * 0.5f;
            var offsetY = (IconSize - height) * 0.5f;

            for (var y = 0; y < IconSize; y++)
            {
                for (var x = 0; x < IconSize; x++)
                {
                    var u = (x + 0.5f - offsetX) / width;
                    var v = (y + 0.5f - offsetY) / height;
                    if (u < 0f || u > 1f || v < 0f || v > 1f)
                    {
                        continue;
                    }

                    // Average a 3x3 grid of samples so the shrunk image stays smooth.
                    var sum = Color.clear;
                    for (var sy = -1; sy <= 1; sy++)
                    {
                        for (var sx = -1; sx <= 1; sx++)
                        {
                            var c = source.GetPixelBilinear(u + sx / (3f * width), v + sy / (3f * height));
                            sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                        }
                    }

                    sum /= 9f;
                    if (sum.a > 0.001f)
                    {
                        icon.SetPixel(x, y, new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a));
                    }
                }
            }

            icon.Apply(false, false);
            return icon;
        }

        // ------------------------------------------------------------------ Goods icons

        private static Texture2D BuildButterIcon()
        {
            return BuildGoodsIcon(layer =>
            {
                DrawShadedEllipse(layer, 64f, 36f, 50f, 16f, 0f, Rgb(232, 236, 240), 0.3f);
                DrawShadedEllipse(layer, 64f, 38f, 40f, 11f, 0f, Rgb(208, 216, 224), 0.1f);

                // A block of butter: front face and a lighter top.
                var front = Rgb(246, 210, 92);
                PaintBox(layer, 60f, 54f, 30f, 16f, 3f, front, Scale(front, 0.85f));
                DrawTriangle(layer, new Vector2(30f, 70f), new Vector2(90f, 70f), new Vector2(104f, 86f), Rgb(255, 232, 140));
                DrawTriangle(layer, new Vector2(30f, 70f), new Vector2(104f, 86f), new Vector2(44f, 86f), Rgb(255, 232, 140));
                DrawTriangle(layer, new Vector2(90f, 38f), new Vector2(104f, 54f), new Vector2(104f, 86f), Scale(front, 0.78f));
                DrawTriangle(layer, new Vector2(90f, 38f), new Vector2(90f, 70f), new Vector2(104f, 86f), Scale(front, 0.78f));

                // A curl sliced from the top.
                DrawLeaf(layer, new Vector2(52f, 84f), new Vector2(62f, 100f), new Vector2(78f, 92f), 5f, Rgb(255, 226, 126));
            });
        }

        private static Texture2D BuildCheeseIcon()
        {
            return BuildGoodsIcon(layer =>
            {
                var rind = Rgb(240, 190, 70);
                var face = Rgb(255, 216, 104);

                // A wedge: tall back edge, sloping top, flat front face with holes.
                DrawTriangle(layer, new Vector2(18f, 70f), new Vector2(110f, 92f), new Vector2(110f, 58f), Scale(face, 1.06f));
                PaintBox(layer, 64f, 44f, 46f, 26f, 4f, face, Scale(face, 0.86f));
                DrawTriangle(layer, new Vector2(18f, 70f), new Vector2(18f, 18f), new Vector2(110f, 58f), face);
                DrawTriangle(layer, new Vector2(110f, 92f), new Vector2(118f, 84f), new Vector2(118f, 22f), Scale(rind, 0.8f));
                DrawTriangle(layer, new Vector2(110f, 92f), new Vector2(110f, 18f), new Vector2(118f, 22f), Scale(rind, 0.8f));

                var hole = Scale(face, 0.72f);
                DrawEllipse(layer, 44f, 42f, 7f, 6f, hole, 0f);
                DrawEllipse(layer, 78f, 34f, 5f, 4.5f, hole, 0f);
                DrawEllipse(layer, 88f, 56f, 6f, 5f, hole, 0f);
                DrawEllipse(layer, 62f, 62f, 3.5f, 3f, hole, 0f);
                DrawEllipse(layer, 30f, 28f, 3f, 3f, hole, 0f);
            });
        }

        private static Texture2D BuildEggTartIcon()
        {
            return BuildGoodsIcon(layer =>
            {
                var pastry = Rgb(214, 150, 72);
                DrawShadedEllipse(layer, 64f, 52f, 50f, 28f, 0f, Scale(pastry, 0.9f), 0.1f);

                // Crimped rim.
                for (var i = 0; i < 18; i++)
                {
                    var angle = i * Mathf.PI * 2f / 18f;
                    DrawShadedEllipse(layer, 64f + Mathf.Cos(angle) * 44f, 58f + Mathf.Sin(angle) * 22f, 8f, 7f, 0f, pastry, 0.3f);
                }

                // Glossy filling with a browned spot.
                DrawShadedEllipse(layer, 64f, 60f, 37f, 17f, 0f, Rgb(255, 210, 74), 0.7f);
                DrawEllipse(layer, 74f, 56f, 9f, 4f, new Color(0.78f, 0.5f, 0.18f, 0.55f), 0f);
            });
        }

        private static Texture2D BuildCustardIcon()
        {
            return BuildGoodsIcon(layer =>
            {
                // A ribbed white ramekin.
                var dish = Rgb(244, 240, 232);
                PaintBox(layer, 64f, 48f, 40f, 26f, 8f, dish, Scale(dish, 0.82f));
                for (var x = 34f; x <= 94f; x += 10f)
                {
                    DrawLine(layer, x, 26f, x, 70f, 1.5f, Scale(dish, 0.78f));
                }

                DrawShadedEllipse(layer, 64f, 74f, 42f, 12f, 0f, dish, 0.2f);

                // Custard with a caramel top and a mint leaf.
                DrawShadedEllipse(layer, 64f, 76f, 36f, 9f, 0f, Rgb(250, 210, 96), 0.4f);
                DrawShadedEllipse(layer, 64f, 78f, 28f, 6f, 0f, Rgb(196, 112, 40), 0.6f);
                DrawLeaf(layer, new Vector2(64f, 82f), new Vector2(72f, 94f), new Vector2(82f, 98f), 5f, Rgb(92, 170, 80));
                DrawLeaf(layer, new Vector2(64f, 82f), new Vector2(56f, 94f), new Vector2(48f, 98f), 5f, Rgb(120, 196, 96));
            });
        }

        private static Texture2D BuildGoodsIcon(System.Action<Texture2D> draw)
        {
            var texture = NewTexture(IconSize, IconSize);
            DrawEllipse(texture, 64f, 16f, 44f, 8f, Shadow, 0f);
            var layer = NewTexture(IconSize, IconSize);
            draw(layer);
            FinishLayer(texture, layer, IconOutline);
            texture.Apply(false, false);
            return texture;
        }
    }
}
