using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>
    /// The second set of crops (tomato, potato, strawberry, soybean, chili, pumpkin) and
    /// everything goat: the goat, its pen, the Goat Stable, goat milk and its goods (made in
    /// the Dairy). Same soft-shaded, outlined style as the rest of the farm art.
    /// </summary>
    public static partial class PlaceholderArtGenerator
    {
        private static readonly string[] ExtraCropIds =
        {
            "tomato", "potato", "strawberry", "soybean", "chili", "pumpkin"
        };

        private static readonly string[] GoatGoodsIds = { "goat_milk", "goat_cheese", "goat_yogurt", "goat_milk_soap" };

        private static bool IsExtraCrop(string cropId)
        {
            return System.Array.IndexOf(ExtraCropIds, cropId) >= 0;
        }

        private static void WriteExtraCropAndGoatArt()
        {
            WritePng("icon_tomato", BuildCropIcon(DrawTomatoIcon));
            WritePng("icon_potato", BuildCropIcon(DrawPotatoIcon));
            WritePng("icon_strawberry", BuildCropIcon(DrawStrawberryIcon));
            WritePng("icon_soybean", BuildCropIcon(DrawSoybeanIcon));
            WritePng("icon_chili", BuildCropIcon(DrawChiliIcon));
            WritePng("icon_pumpkin", BuildCropIcon(DrawPumpkinIcon));

            WritePng("animal_goat", BuildGoatAnimal());
            WritePng("pen_goat", BuildGoatPen());

            var stable = BuildCottage(CottageStyle.GoatStable);
            WritePng("icon_goat_stable", FitToIcon(stable));
            WritePng("building_goat_stable", stable);

            WritePng("icon_goat_milk", BuildGoodsIcon(DrawGoatMilkIcon));
            WritePng("icon_goat_cheese", BuildGoodsIcon(DrawGoatCheeseIcon));
            WritePng("icon_goat_yogurt", BuildGoodsIcon(DrawGoatYogurtIcon));
            WritePng("icon_goat_milk_soap", BuildGoodsIcon(DrawGoatSoapIcon));
        }

        private static void ConfigureExtraCropAndGoatSprites()
        {
            for (var i = 0; i < ExtraCropIds.Length; i++)
            {
                ConfigureSprite($"icon_{ExtraCropIds[i]}", 100f, SpriteAlignment.Center, Vector4.zero);
            }

            for (var i = 0; i < GoatGoodsIds.Length; i++)
            {
                ConfigureSprite($"icon_{GoatGoodsIds[i]}", 100f, SpriteAlignment.Center, Vector4.zero);
            }

            ConfigureSprite("animal_goat", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("pen_goat", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_goat_stable", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite(
                "building_goat_stable",
                128f,
                SpriteAlignment.Custom,
                Vector4.zero,
                new Vector2(0.5f, 26f / SmallBuildingHeight));
        }

        // ------------------------------------------------------------------ Crop growth stages

        private static void DrawExtraCropStage(Texture2D texture, string cropId, int stage, CropPalette palette)
        {
            if (stage == 1)
            {
                // A sprout with two round seed leaves and a first true leaf.
                var root = new Vector2(64f, 33f);
                DrawLeaf(texture, root, new Vector2(54f, 44f), new Vector2(42f, 52f), 7f, palette.Leaf);
                DrawLeaf(texture, root, new Vector2(74f, 44f), new Vector2(86f, 52f), 7f, palette.LeafLight);
                DrawLeaf(texture, root, new Vector2(63f, 50f), new Vector2(66f, 66f), 5.5f, palette.Leaf);
                return;
            }

            var ripe = stage >= 3;
            switch (cropId)
            {
                case "tomato":
                    DrawTomatoPlant(texture, palette, ripe);
                    break;
                case "potato":
                    DrawPotatoPlant(texture, palette, ripe);
                    break;
                case "strawberry":
                    DrawStrawberryPlant(texture, palette, ripe);
                    break;
                case "soybean":
                    DrawSoybeanPlant(texture, palette, ripe);
                    break;
                case "chili":
                    DrawChiliPlant(texture, palette, ripe);
                    break;
                default:
                    DrawPumpkinPlant(texture, palette, ripe);
                    break;
            }
        }

        /// <summary>A fan of leaves around a short stem: the base of most bushy plants.</summary>
        private static void DrawLeafFan(Texture2D texture, CropPalette palette, float height, int count, float width, float leafWidth)
        {
            for (var i = 0; i < count; i++)
            {
                var s = count == 1 ? 0f : (i / (count - 1f)) * 2f - 1f;
                var tip = new Vector2(64f + s * width, 40f + height * (1f - Mathf.Abs(s) * 0.45f));
                var control = new Vector2(64f + s * width * 0.45f, 40f + height * 0.6f);
                DrawLeaf(texture, new Vector2(64f + s * 4f, 34f), control, tip, leafWidth, i % 2 == 0 ? palette.Leaf : palette.LeafLight);
            }
        }

        private static void DrawTomatoPlant(Texture2D texture, CropPalette palette, bool ripe)
        {
            var top = ripe ? 142f : 112f;
            DrawCurve(texture, new Vector2(54f, 30f), new Vector2(54f, 90f), new Vector2(54f, top), 3.5f, Rgb(150, 110, 64));
            DrawCurve(texture, new Vector2(66f, 33f), new Vector2(72f, top * 0.6f), new Vector2(62f, top - 6f), 4f, palette.Stem);

            for (var i = 0; i < 6; i++)
            {
                var y = 48f + i * (top - 54f) / 6f;
                var side = i % 2 == 0 ? -1f : 1f;
                var start = new Vector2(66f + side * 2f, y);
                DrawLeaf(texture, start, start + new Vector2(side * 14f, 10f), start + new Vector2(side * 30f, 8f), 7f, i % 2 == 0 ? palette.Leaf : palette.LeafLight);
            }

            var fruit = ripe ? palette.Produce : Rgb(142, 196, 90);
            var size = ripe ? 10f : 6.5f;
            foreach (var p in new[] { new Vector2(44f, 74f), new Vector2(84f, 92f), new Vector2(50f, 112f), new Vector2(80f, 128f) })
            {
                if (!ripe && p.y > 115f)
                {
                    continue;
                }

                DrawShadedEllipse(texture, p.x, p.y, size, size * 0.92f, 0f, fruit, 0.55f);
                DrawCalyx(texture, p + new Vector2(0f, size * 0.8f), size * 0.55f, palette.Stem);
            }
        }

        private static void DrawPotatoPlant(Texture2D texture, CropPalette palette, bool ripe)
        {
            if (ripe)
            {
                foreach (var p in new[] { new Vector3(44f, 34f, 20f), new Vector3(84f, 33f, -15f), new Vector3(64f, 36f, 5f) })
                {
                    DrawShadedEllipse(texture, p.x, p.y, 12f, 8.5f, p.z, palette.Produce, 0.3f);
                    DrawEllipse(texture, p.x - 3f, p.y + 2f, 1.3f, 1.3f, palette.ProduceDark, 0f);
                    DrawEllipse(texture, p.x + 4f, p.y - 1f, 1.1f, 1.1f, palette.ProduceDark, 0f);
                }

                DrawShadedEllipse(texture, 64f, 27f, 34f, 8.5f, 0f, Rgb(140, 94, 54), 0.12f);
            }

            DrawLeafFan(texture, palette, ripe ? 70f : 54f, 7, 40f, 8f);
            DrawLeafFan(texture, palette, ripe ? 52f : 40f, 4, 22f, 7f);
            if (ripe)
            {
                foreach (var p in new[] { new Vector2(44f, 100f), new Vector2(70f, 112f), new Vector2(88f, 92f) })
                {
                    DrawFlower(texture, p, 4.5f, Rgb(232, 220, 250), Rgb(250, 210, 60));
                }
            }
        }

        private static void DrawStrawberryPlant(Texture2D texture, CropPalette palette, bool ripe)
        {
            // Low bush of three-part leaves.
            for (var i = 0; i < 5; i++)
            {
                var s = (i / 4f) * 2f - 1f;
                var stemTop = new Vector2(64f + s * 32f, 70f + (1f - Mathf.Abs(s)) * 18f);
                DrawCurve(texture, new Vector2(64f, 34f), new Vector2(64f + s * 14f, 56f), stemTop, 2.2f, palette.Stem);
                var colour = i % 2 == 0 ? palette.Leaf : palette.LeafLight;
                DrawLeaf(texture, stemTop, stemTop + new Vector2(-8f, 6f), stemTop + new Vector2(-15f, 14f), 7f, colour);
                DrawLeaf(texture, stemTop, stemTop + new Vector2(8f, 6f), stemTop + new Vector2(15f, 14f), 7f, colour);
                DrawLeaf(texture, stemTop, stemTop + new Vector2(0f, 10f), stemTop + new Vector2(0f, 20f), 7f, colour);
            }

            if (!ripe)
            {
                DrawFlower(texture, new Vector2(46f, 62f), 5f, Rgb(255, 252, 244), Rgb(250, 210, 60));
                DrawFlower(texture, new Vector2(84f, 58f), 5f, Rgb(255, 252, 244), Rgb(250, 210, 60));
                return;
            }

            foreach (var p in new[] { new Vector2(42f, 54f), new Vector2(64f, 46f), new Vector2(88f, 52f), new Vector2(76f, 66f) })
            {
                DrawBerry(texture, p, 1f, palette);
            }
        }

        private static void DrawSoybeanPlant(Texture2D texture, CropPalette palette, bool ripe)
        {
            // A bushy plant of three-part leaves; ripe plants turn golden and hang with pods.
            var height = ripe ? 76f : 60f;
            for (var i = 0; i < 5; i++)
            {
                var s = (i / 4f) * 2f - 1f;
                var top = new Vector2(64f + s * 30f, 40f + height * (1f - Mathf.Abs(s) * 0.4f));
                DrawCurve(texture, new Vector2(64f, 34f), new Vector2(64f + s * 12f, 40f + height * 0.5f), top, 2.4f, palette.Stem);
                var colour = ripe ? (i % 2 == 0 ? palette.ProduceLight : palette.Produce) : (i % 2 == 0 ? palette.Leaf : palette.LeafLight);
                DrawLeaf(texture, top, top + new Vector2(-7f, 5f), top + new Vector2(-14f, 12f), 6.5f, colour);
                DrawLeaf(texture, top, top + new Vector2(7f, 5f), top + new Vector2(14f, 12f), 6.5f, colour);
                DrawLeaf(texture, top, top + new Vector2(0f, 9f), top + new Vector2(0f, 18f), 6.5f, colour);
            }

            if (!ripe)
            {
                foreach (var p in new[] { new Vector2(50f, 70f), new Vector2(78f, 76f) })
                {
                    DrawFlower(texture, p, 3.5f, Rgb(214, 190, 240), Rgb(250, 230, 120));
                }

                return;
            }

            foreach (var p in new[] { new Vector3(46f, 66f, -30f), new Vector3(60f, 58f, 15f), new Vector3(76f, 64f, -10f), new Vector3(88f, 56f, 30f) })
            {
                DrawSoyPod(texture, new Vector2(p.x, p.y), 1f, p.z, palette);
            }
        }

        private static void DrawChiliPlant(Texture2D texture, CropPalette palette, bool ripe)
        {
            // An upright pepper bush with chilies hanging below the leaves.
            var top = ripe ? 128f : 108f;
            DrawCurve(texture, new Vector2(64f, 33f), new Vector2(60f, top * 0.55f), new Vector2(64f, top), 4f, palette.Stem);
            for (var i = 0; i < 6; i++)
            {
                var y = 50f + i * (top - 56f) / 6f;
                var side = i % 2 == 0 ? -1f : 1f;
                var start = new Vector2(63f, y);
                DrawLeaf(texture, start, start + new Vector2(side * 14f, 8f), start + new Vector2(side * 28f, 4f), 7.5f, i % 2 == 0 ? palette.Leaf : palette.LeafLight);
            }

            var pod = ripe ? palette.Produce : Rgb(120, 180, 70);
            var count = ripe ? 5 : 3;
            var spots = new[] { new Vector2(42f, 92f), new Vector2(86f, 104f), new Vector2(50f, 118f), new Vector2(80f, 76f), new Vector2(70f, 128f) };
            for (var i = 0; i < count; i++)
            {
                DrawChiliPod(texture, spots[i], ripe ? 1f : 0.75f, i % 2 == 0 ? -12f : 12f, pod, palette.Stem);
            }
        }

        private static void DrawPumpkinPlant(Texture2D texture, CropPalette palette, bool ripe)
        {
            // Big leaves behind, a curly vine, then the pumpkin itself.
            DrawLeaf(texture, new Vector2(60f, 40f), new Vector2(34f, 70f), new Vector2(22f, 90f), 16f, palette.Leaf);
            DrawLeaf(texture, new Vector2(68f, 40f), new Vector2(96f, 66f), new Vector2(108f, 86f), 16f, palette.LeafLight);
            DrawCurve(texture, new Vector2(70f, 60f), new Vector2(92f, 92f), new Vector2(80f, 100f), 2f, palette.Stem);
            DrawCurve(texture, new Vector2(80f, 100f), new Vector2(70f, 104f), new Vector2(76f, 92f), 2f, palette.Stem);

            if (ripe)
            {
                DrawPumpkin(texture, new Vector2(64f, 50f), 1f, palette);
            }
            else
            {
                DrawLeaf(texture, new Vector2(64f, 36f), new Vector2(58f, 66f), new Vector2(64f, 84f), 12f, palette.Leaf);
                DrawShadedEllipse(texture, 64f, 42f, 11f, 9f, 0f, Rgb(150, 196, 96), 0.3f);
                DrawFlower(texture, new Vector2(46f, 74f), 6f, Rgb(250, 196, 60), Rgb(236, 140, 30));
            }
        }

        // ------------------------------------------------------------------ Shared crop parts

        private static void DrawCalyx(Texture2D texture, Vector2 centre, float size, Color colour)
        {
            for (var i = 0; i < 5; i++)
            {
                var angle = (90f + i * 72f) * Mathf.Deg2Rad;
                var tip = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f) * size * 1.6f;
                DrawLeaf(texture, centre, Vector2.Lerp(centre, tip, 0.5f), tip, size * 0.45f, colour);
            }
        }

        private static void DrawFlower(Texture2D texture, Vector2 centre, float size, Color petal, Color heart)
        {
            for (var i = 0; i < 5; i++)
            {
                var angle = (90f + i * 72f) * Mathf.Deg2Rad;
                DrawShadedEllipse(texture, centre.x + Mathf.Cos(angle) * size, centre.y + Mathf.Sin(angle) * size, size * 0.8f, size * 0.8f, 0f, petal, 0.2f);
            }

            DrawShadedEllipse(texture, centre.x, centre.y, size * 0.6f, size * 0.6f, 0f, heart, 0.4f);
        }

        private static void DrawBerry(Texture2D texture, Vector2 top, float scale, CropPalette palette)
        {
            // Round shoulders tapering to a point, glossy, dotted with seeds, leafy cap on top.
            DrawStroke(
                texture,
                top,
                top + new Vector2(0f, -10f * scale),
                top + new Vector2(0f, -22f * scale),
                t => 10f * scale * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t * 0.9f)) * Mathf.Lerp(1f, 0.25f, t * t),
                palette.Produce,
                0f,
                0.6f);
            for (var i = 0; i < 6; i++)
            {
                var x = top.x + ((i % 3) - 1) * 4.5f * scale;
                var y = top.y - (5f + (i / 3) * 7f) * scale;
                DrawEllipse(texture, x, y, 0.9f * scale, 1.3f * scale, Rgb(255, 230, 140), 0f);
            }

            DrawCalyx(texture, top + new Vector2(0f, 1f), 4.5f * scale, palette.Stem);
        }

        /// <summary>A fuzzy pod with three beans bulging through, hanging from its top end.</summary>
        private static void DrawSoyPod(Texture2D texture, Vector2 top, float scale, float tilt, CropPalette palette)
        {
            var direction = new Vector2(Mathf.Sin(tilt * Mathf.Deg2Rad), -Mathf.Cos(tilt * Mathf.Deg2Rad));
            for (var i = 0; i < 3; i++)
            {
                var p = top + direction * (5f + i * 7f) * scale;
                DrawShadedEllipse(texture, p.x, p.y, 5.2f * scale, 4.4f * scale, tilt, i == 1 ? palette.Produce : palette.ProduceLight, 0.35f);
            }

            DrawCurve(texture, top + new Vector2(0f, 3f * scale), top + new Vector2(1f, 6f * scale), top + new Vector2(2f * scale, 8f * scale), 1.4f * scale, palette.Stem);
        }

        /// <summary>A curved chili hanging from a green cap.</summary>
        private static void DrawChiliPod(Texture2D texture, Vector2 top, float scale, float bend, Color colour, Color stem)
        {
            DrawStroke(
                texture,
                top,
                top + new Vector2(bend * 0.4f * scale, -14f * scale),
                top + new Vector2(bend * scale, -26f * scale),
                t => Mathf.Lerp(5.5f, 1.2f, t * t) * scale,
                colour,
                0f,
                0.55f);
            DrawShadedEllipse(texture, top.x, top.y + 1f, 4.5f * scale, 2.6f * scale, 0f, stem, 0.3f);
            DrawCurve(texture, top + new Vector2(0f, 2f), top + new Vector2(1f, 5f * scale), top + new Vector2(3f * scale, 7f * scale), 1.5f * scale, stem);
        }

        private static void DrawPumpkin(Texture2D texture, Vector2 centre, float scale, CropPalette palette)
        {
            // Ribs: outer lobes first, the front lobe last.
            DrawShadedEllipse(texture, centre.x - 18f * scale, centre.y, 18f * scale, 20f * scale, 0f, Scale(palette.Produce, 0.9f), 0.2f);
            DrawShadedEllipse(texture, centre.x + 18f * scale, centre.y, 18f * scale, 20f * scale, 0f, Scale(palette.Produce, 0.9f), 0.2f);
            DrawShadedEllipse(texture, centre.x, centre.y, 18f * scale, 22f * scale, 0f, palette.Produce, 0.4f);
            DrawCurve(texture, centre + new Vector2(0f, 18f * scale), centre + new Vector2(2f, 26f * scale), centre + new Vector2(6f * scale, 30f * scale), 5f * scale, Rgb(110, 120, 50));
        }

        // ------------------------------------------------------------------ Crop icons

        private static Texture2D BuildCropIcon(System.Action<Texture2D> draw)
        {
            return BuildGoodsIcon(draw);
        }

        private static void DrawTomatoIcon(Texture2D layer)
        {
            var palette = GetPalette("tomato");
            DrawShadedEllipse(layer, 64f, 56f, 40f, 34f, 0f, palette.Produce, 0.6f);
            DrawCalyx(layer, new Vector2(64f, 86f), 9f, palette.Leaf);
            DrawCurve(layer, new Vector2(64f, 88f), new Vector2(66f, 98f), new Vector2(72f, 104f), 4f, palette.Stem);
        }

        private static void DrawPotatoIcon(Texture2D layer)
        {
            var palette = GetPalette("potato");
            DrawShadedEllipse(layer, 50f, 50f, 30f, 22f, 20f, Scale(palette.Produce, 0.92f), 0.3f);
            DrawShadedEllipse(layer, 78f, 62f, 32f, 23f, -15f, palette.Produce, 0.35f);
            foreach (var p in new[] { new Vector2(40f, 54f), new Vector2(56f, 44f), new Vector2(72f, 66f), new Vector2(88f, 58f), new Vector2(80f, 72f) })
            {
                DrawEllipse(layer, p.x, p.y, 2f, 1.6f, palette.ProduceDark, 0f);
            }
        }

        private static void DrawStrawberryIcon(Texture2D layer)
        {
            var palette = GetPalette("strawberry");
            DrawBerry(layer, new Vector2(64f, 100f), 3.2f, palette);
        }

        private static void DrawSoybeanIcon(Texture2D layer)
        {
            // Two golden pods and a few loose beans.
            var palette = GetPalette("soybean");
            DrawSoyPod(layer, new Vector2(34f, 64f), 3f, 65f, palette);
            DrawSoyPod(layer, new Vector2(20f, 96f), 3f, 65f, palette);
            DrawShadedEllipse(layer, 98f, 22f, 9f, 8f, 0f, Rgb(240, 214, 140), 0.5f);
            DrawShadedEllipse(layer, 114f, 32f, 9f, 8f, 0f, Rgb(232, 204, 126), 0.5f);
        }

        private static void DrawChiliIcon(Texture2D layer)
        {
            // Two red chilies crossed over each other.
            var palette = GetPalette("chili");
            DrawChiliPod(layer, new Vector2(46f, 108f), 3.4f, 18f, Scale(palette.Produce, 0.9f), palette.Stem);
            DrawChiliPod(layer, new Vector2(82f, 108f), 3.4f, -16f, palette.Produce, palette.Stem);
        }

        private static void DrawPumpkinIcon(Texture2D layer)
        {
            var palette = GetPalette("pumpkin");
            DrawLeaf(layer, new Vector2(70f, 82f), new Vector2(92f, 104f), new Vector2(110f, 100f), 12f, palette.Leaf);
            DrawPumpkin(layer, new Vector2(64f, 56f), 1.75f, palette);
        }

        // ------------------------------------------------------------------ The goat

        /// <summary>A white goat with a tan patch, curved horns and a little beard, facing right.</summary>
        private static Texture2D BuildGoatAnimal()
        {
            var texture = NewTexture(AnimalSize, AnimalSize);
            DrawEllipse(texture, 128f, 40f, 78f, 12f, Shadow, 0f);
            var layer = NewTexture(AnimalSize, AnimalSize);

            var coat = Rgb(246, 240, 228);
            var hoof = Rgb(80, 66, 56);
            var horn = Rgb(196, 176, 140);

            // Far legs, body, near legs.
            DrawLeg(layer, 92f, 102f, Scale(coat, 0.85f), hoof);
            DrawLeg(layer, 156f, 102f, Scale(coat, 0.85f), hoof);
            DrawShadedEllipse(layer, 124f, 112f, 62f, 40f, 0f, coat, 0.2f);
            DrawShadedEllipse(layer, 112f, 122f, 22f, 16f, -10f, Rgb(214, 170, 120), 0.15f);
            DrawLeg(layer, 80f, 98f, coat, hoof);
            DrawLeg(layer, 166f, 98f, coat, hoof);

            // Tail tuft.
            DrawLeaf(layer, new Vector2(66f, 122f), new Vector2(56f, 134f), new Vector2(58f, 146f), 6f, coat);

            // Neck and head.
            DrawStroke(layer, new Vector2(168f, 124f), new Vector2(182f, 146f), new Vector2(190f, 158f), t => Mathf.Lerp(20f, 15f, t), coat, 0f, 0.2f);
            DrawShadedEllipse(layer, 198f, 164f, 24f, 18f, -25f, coat, 0.25f);
            DrawShadedEllipse(layer, 214f, 154f, 11f, 9f, -25f, Rgb(236, 214, 200), 0.3f);
            DrawLeaf(layer, new Vector2(188f, 172f), new Vector2(172f, 172f), new Vector2(160f, 164f), 6f, Scale(coat, 0.92f));

            // Horns curve back over the head.
            DrawStroke(layer, new Vector2(192f, 178f), new Vector2(190f, 200f), new Vector2(170f, 204f), t => Mathf.Lerp(5f, 1.5f, t), horn, 0f, 0.4f);
            DrawStroke(layer, new Vector2(200f, 178f), new Vector2(202f, 200f), new Vector2(184f, 208f), t => Mathf.Lerp(5f, 1.5f, t), horn, 0f, 0.4f);

            // Beard, eye, nostril.
            DrawTriangle(layer, new Vector2(206f, 146f), new Vector2(214f, 144f), new Vector2(206f, 128f), Scale(coat, 0.9f));
            DrawEllipse(layer, 202f, 168f, 3.4f, 3.4f, Rgb(50, 36, 24), 0f);
            DrawEllipse(layer, 201f, 169.5f, 1.2f, 1.2f, Color.white, 0f);
            DrawEllipse(layer, 221f, 153f, 1.6f, 1.2f, Rgb(120, 90, 80), 0f);

            FinishLayer(texture, layer, IconOutline);
            texture.Apply(false, false);
            return texture;
        }

        private static void DrawLeg(Texture2D texture, float x, float topY, Color coat, Color hoof)
        {
            PaintBox(texture, x, topY - 26f, 6f, 26f, 4f, coat, Scale(coat, 0.85f));
            PaintBox(texture, x, topY - 52f, 6.5f, 5f, 2f, hoof, hoof);
        }

        /// <summary>A sunny hillside: sky, rolling hills, a rail fence, rocks to climb and a hay pile.</summary>
        private static Texture2D BuildGoatPen()
        {
            var texture = NewTexture(PenWidth, PenHeight);
            var skyTop = Rgb(150, 206, 240);
            var skyLow = Rgb(214, 238, 248);
            var hillFar = Rgb(150, 196, 104);
            var hillNear = Rgb(118, 176, 82);
            var ground = Rgb(132, 186, 92);

            for (var y = 0; y < PenHeight; y++)
            {
                for (var x = 0; x < PenWidth; x++)
                {
                    var far = 330f + Mathf.Sin(x * 0.006f + 1.2f) * 34f + Mathf.Sin(x * 0.017f) * 10f;
                    var near = 250f + Mathf.Sin(x * 0.009f + 3.1f) * 26f;
                    Color c;
                    if (y > far)
                    {
                        c = Color.Lerp(skyLow, skyTop, (y - far) / (PenHeight - far));
                    }
                    else if (y > near)
                    {
                        c = hillFar;
                    }
                    else
                    {
                        c = Color.Lerp(ground, hillNear, y / near);
                        var noise = (Noise01(x / 3, y / 3, 41) - 0.5f) * 0.06f;
                        c += new Color(noise, noise, noise * 0.5f, 0f);
                    }

                    texture.SetPixel(x, y, c);
                }
            }

            DrawEllipse(texture, 820f, 450f, 40f, 40f, Rgb(255, 240, 170), 0f);
            DrawEllipse(texture, 210f, 440f, 60f, 18f, new Color(1f, 1f, 1f, 0.85f), 0f);
            DrawEllipse(texture, 250f, 452f, 40f, 16f, new Color(1f, 1f, 1f, 0.85f), 0f);

            // Rail fence across the back.
            for (var x = 30f; x < PenWidth; x += 120f)
            {
                PaintBox(texture, x, 268f, 7f, 34f, 3f, Rgb(170, 120, 70), Rgb(130, 88, 48));
            }

            DrawLine(texture, 0f, 286f, PenWidth, 286f, 7f, Rgb(160, 112, 64));
            DrawLine(texture, 0f, 258f, PenWidth, 258f, 7f, Rgb(160, 112, 64));

            // Rocks and a hay pile.
            DrawShadedEllipse(texture, 150f, 150f, 80f, 46f, 0f, Rgb(160, 156, 146), 0.2f);
            DrawShadedEllipse(texture, 210f, 130f, 50f, 30f, 0f, Rgb(176, 170, 160), 0.2f);
            DrawShadedEllipse(texture, 880f, 140f, 70f, 40f, 0f, Rgb(160, 156, 146), 0.2f);
            DrawShadedEllipse(texture, 760f, 110f, 60f, 30f, 0f, Hay, 0.2f);
            for (var i = 0; i < 30; i++)
            {
                var x = 710f + Noise01(i, 1, 9) * 100f;
                var y = 100f + Noise01(i, 2, 9) * 30f;
                DrawLine(texture, x, y, x + 8f, y + 6f, 1.5f, Rgb(200, 160, 60));
            }

            for (var i = 0; i < 40; i++)
            {
                var x = Noise01(i, 4, 13) * PenWidth;
                var y = 20f + Noise01(i, 6, 13) * 200f;
                DrawEllipse(texture, x, y, 3f, 3f, i % 3 == 0 ? Rgb(255, 250, 240) : Rgb(250, 214, 80), 0f);
            }

            texture.Apply(false, false);
            return texture;
        }

        // ------------------------------------------------------------------ Goat milk goods

        private static void DrawGoatMilkIcon(Texture2D layer)
        {
            // A glass bottle with a green cap and a horn label.
            var glass = Rgb(244, 244, 238);
            PaintBox(layer, 64f, 52f, 26f, 34f, 10f, glass, Scale(glass, 0.85f));
            PaintBox(layer, 64f, 94f, 12f, 10f, 4f, glass, Scale(glass, 0.88f));
            PaintBox(layer, 64f, 108f, 14f, 6f, 3f, Rgb(108, 168, 80), Rgb(76, 130, 56));
            PaintBox(layer, 64f, 52f, 20f, 14f, 4f, Rgb(126, 176, 92), Rgb(100, 150, 70));
            var cream = Rgb(255, 250, 238);
            DrawStroke(layer, new Vector2(60f, 46f), new Vector2(54f, 56f), new Vector2(60f, 62f), t => Mathf.Lerp(3f, 1f, t), cream, 0f, 0.3f);
            DrawStroke(layer, new Vector2(68f, 46f), new Vector2(74f, 56f), new Vector2(68f, 62f), t => Mathf.Lerp(3f, 1f, t), cream, 0f, 0.3f);
            DrawLine(layer, 46f, 34f, 46f, 78f, 3f, new Color(1f, 1f, 1f, 0.6f));
        }

        private static void DrawGoatCheeseIcon(Texture2D layer)
        {
            // A small white round with a herb crust and a wedge cut out.
            var rind = Rgb(248, 244, 232);
            PaintBox(layer, 64f, 46f, 44f, 18f, 12f, Scale(rind, 0.95f), Scale(rind, 0.8f));
            DrawShadedEllipse(layer, 64f, 64f, 44f, 18f, 0f, rind, 0.3f);
            DrawTriangle(layer, new Vector2(64f, 64f), new Vector2(108f, 64f), new Vector2(96f, 50f), Rgb(255, 250, 240));
            DrawTriangle(layer, new Vector2(108f, 64f), new Vector2(96f, 50f), new Vector2(96f, 32f), Scale(rind, 0.9f));
            DrawTriangle(layer, new Vector2(108f, 64f), new Vector2(108f, 46f), new Vector2(96f, 32f), Scale(rind, 0.9f));
            for (var i = 0; i < 14; i++)
            {
                var angle = i * 0.45f + 2.2f;
                var x = 64f + Mathf.Cos(angle) * 30f * Noise01(i, 3, 7);
                var y = 64f + Mathf.Sin(angle) * 12f * Noise01(i, 5, 7);
                DrawEllipse(layer, x, y, 2f, 1.2f, Rgb(96, 150, 70), angle * 30f);
            }
        }

        private static void DrawGoatYogurtIcon(Texture2D layer)
        {
            // A glass jar: white yogurt below a strawberry layer, a berry on top.
            PaintBox(layer, 64f, 52f, 32f, 36f, 10f, Rgb(250, 248, 242), Rgb(226, 222, 214));
            PaintBox(layer, 64f, 78f, 30f, 8f, 4f, Rgb(236, 96, 110), Rgb(206, 64, 84));
            DrawShadedEllipse(layer, 64f, 88f, 32f, 8f, 0f, Rgb(240, 232, 220), 0.3f);
            DrawLine(layer, 40f, 26f, 40f, 72f, 3f, new Color(1f, 1f, 1f, 0.6f));
            DrawBerry(layer, new Vector2(64f, 112f), 1.4f, GetPalette("strawberry"));
        }

        private static void DrawGoatSoapIcon(Texture2D layer)
        {
            // A creamy bar tied with twine and topped with two soybeans.
            var soap = Rgb(250, 238, 210);
            PaintBox(layer, 64f, 48f, 44f, 20f, 8f, soap, Scale(soap, 0.85f));
            DrawTriangle(layer, new Vector2(20f, 66f), new Vector2(108f, 66f), new Vector2(98f, 80f), Scale(soap, 1.05f));
            DrawTriangle(layer, new Vector2(20f, 66f), new Vector2(98f, 80f), new Vector2(30f, 80f), Scale(soap, 1.05f));
            DrawLine(layer, 64f, 28f, 64f, 68f, 4f, Twine);
            DrawLine(layer, 26f, 73f, 102f, 73f, 4f, Twine);
            DrawShadedEllipse(layer, 64f, 74f, 6f, 4f, 0f, Twine, 0.3f);
            var bean = GetPalette("soybean").ProduceLight;
            DrawShadedEllipse(layer, 50f, 80f, 6f, 5f, 0f, bean, 0.4f);
            DrawShadedEllipse(layer, 78f, 80f, 6f, 5f, 0f, bean, 0.4f);
        }
    }
}
