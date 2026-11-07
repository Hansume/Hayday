using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>
    /// Tilled soil, crop growth stages, crop icons and feed sacks in a soft-shaded cartoon
    /// style: rounded shapes lit from the upper left with a small highlight, drawn on their
    /// own layer and then given a dark brown outline.
    /// </summary>
    public static partial class PlaceholderArtGenerator
    {
        private static readonly Color ArtOutline = Rgb(66, 40, 18);
        private static readonly Vector3 LightDirection = new Vector3(-0.45f, 0.62f, 0.64f).normalized;
        private static readonly Color Burlap = Rgb(206, 170, 112);
        private static readonly Color BurlapLight = Rgb(226, 194, 138);
        private static readonly Color Twine = Rgb(140, 96, 48);
        private static readonly Color Ribbon = Rgb(204, 66, 52);

        private const float CropOutline = 1.6f;
        private const float IconOutline = 2.4f;

        // ------------------------------------------------------------------ Soil

        /// <summary>A tilled field tile: ridged rows lit on one side, with a raised rim.</summary>
        private static Texture2D BuildSoil()
        {
            var texture = NewTexture(GroundWidth, GroundHeight);
            const float rows = 4f;
            var ridge = Rgb(150, 102, 60);
            var furrow = Rgb(88, 56, 31);

            for (var y = 0; y < GroundHeight; y++)
            {
                for (var x = 0; x < GroundWidth; x++)
                {
                    var nx = (x + 0.5f - GroundWidth * 0.5f) / (GroundWidth * 0.5f);
                    var ny = (y + 0.5f - GroundHeight * 0.5f) / (GroundHeight * 0.5f);
                    var diamond = Mathf.Abs(nx) + Mathf.Abs(ny);
                    if (diamond > 1.02f)
                    {
                        continue;
                    }

                    var coverage = Mathf.Clamp01((1.01f - diamond) * 70f + 0.5f);

                    // Rows run parallel to the tile's upper-left edge; b measures across them.
                    var b = nx - ny;
                    var phase = (b + 1f) * 0.5f * rows;
                    var f = phase - Mathf.Floor(phase);
                    var height = Mathf.Sin(f * Mathf.PI);
                    var lit = -Mathf.Cos(f * Mathf.PI); // the side facing the light
                    var color = Color.Lerp(furrow, ridge, Mathf.Pow(height, 0.8f));
                    color = Scale(color, 1f + 0.16f * lit * height);

                    var noise = (Noise01(x / 3, y / 3, 23) - 0.5f) * 0.1f;
                    color += new Color(noise, noise * 0.75f, noise * 0.45f, 0f);

                    // Raised rim: lit along the top edges, shaded along the bottom ones.
                    if (diamond > 0.86f)
                    {
                        var rim = Mathf.InverseLerp(0.86f, 0.97f, diamond);
                        var rimColor = ny > 0f ? Rgb(160, 112, 66) : Rgb(104, 66, 36);
                        color = Color.Lerp(color, rimColor, 0.75f);
                        if (diamond > 0.95f)
                        {
                            color = Color.Lerp(color, SoilEdge, Mathf.Clamp01(rim));
                        }
                    }

                    BlendPixel(texture, x, y, color, coverage);
                }
            }

            // A few clods catching the light.
            for (var i = 0; i < 18; i++)
            {
                var px = 30f + Noise01(i, 5, 43) * (GroundWidth - 60f);
                var py = 20f + Noise01(i, 8, 29) * (GroundHeight - 40f);
                var nx = (px - GroundWidth * 0.5f) / (GroundWidth * 0.5f);
                var ny = (py - GroundHeight * 0.5f) / (GroundHeight * 0.5f);
                if (Mathf.Abs(nx) + Mathf.Abs(ny) > 0.78f)
                {
                    continue;
                }

                var size = 1.6f + Noise01(i, 3, 11) * 1.6f;
                DrawShadedEllipse(texture, px, py, size * 1.3f, size, 0f, Rgb(132, 90, 52), 0.15f);
            }

            texture.Apply(false, false);
            return texture;
        }

        // ------------------------------------------------------------------ Crop stages

        private static Texture2D BuildCropStage(string cropId, int stage)
        {
            var texture = NewTexture(CropWidth, CropHeight);
            var palette = GetPalette(cropId);
            DrawEllipse(texture, 64f, 25f, 36f, 9f, Shadow, 0f);

            var layer = NewTexture(CropWidth, CropHeight);
            DrawSoilMound(layer, 64f, 31f);
            var awns = new List<WheatHead>();

            if (stage > 0 && IsExtraCrop(cropId))
            {
                DrawExtraCropStage(layer, cropId, stage, palette);
                FinishLayer(texture, layer, CropOutline);
                texture.Apply(false, false);
                return texture;
            }

            switch (stage)
            {
                case 0:
                    DrawPlantedSeeds(layer, cropId, palette);
                    break;
                case 1:
                    DrawCropSprout(layer, cropId, palette);
                    break;
                case 2:
                    DrawYoungCrop(layer, cropId, palette, awns);
                    break;
                default:
                    switch (cropId)
                    {
                        case "corn":
                            DrawRipeCorn(layer, palette);
                            break;
                        case "carrot":
                            DrawRipeCarrots(layer, palette);
                            break;
                        default:
                            DrawRipeWheat(layer, palette, awns);
                            break;
                    }

                    break;
            }

            FinishLayer(texture, layer, CropOutline);
            DrawWheatAwns(texture, awns, palette.ProduceDark);
            texture.Apply(false, false);
            return texture;
        }

        private static void DrawSoilMound(Texture2D texture, float centerX, float baseY)
        {
            DrawShadedEllipse(texture, centerX, baseY, 32f, 11f, 0f, Rgb(140, 94, 54), 0.12f);
            DrawShadedEllipse(texture, centerX - 14f, baseY + 2f, 4f, 2.6f, 0f, Rgb(122, 80, 44), 0.1f);
            DrawShadedEllipse(texture, centerX + 12f, baseY + 3f, 3.4f, 2.2f, 0f, Rgb(122, 80, 44), 0.1f);
        }

        private static void DrawPlantedSeeds(Texture2D texture, string cropId, CropPalette palette)
        {
            DrawEllipse(texture, 64f, 33f, 15f, 4.5f, Rgb(92, 58, 30), 0f);
            var seed = cropId == "carrot" ? Rgb(150, 104, 60) : palette.Produce;
            DrawShadedEllipse(texture, 56f, 34f, 3.2f, 4.2f, 25f, seed, 0.45f);
            DrawShadedEllipse(texture, 64f, 33f, 3.2f, 4.2f, -10f, seed, 0.45f);
            DrawShadedEllipse(texture, 72f, 34.5f, 3.2f, 4.2f, -30f, seed, 0.45f);
        }

        private static void DrawCropSprout(Texture2D texture, string cropId, CropPalette palette)
        {
            var root = new Vector2(64f, 33f);
            switch (cropId)
            {
                case "corn":
                    DrawLeaf(texture, root, new Vector2(50f, 48f), new Vector2(40f, 58f), 6.5f, palette.Leaf);
                    DrawLeaf(texture, root, new Vector2(80f, 50f), new Vector2(90f, 60f), 6.5f, palette.LeafLight);
                    DrawLeaf(texture, root, new Vector2(62f, 52f), new Vector2(63f, 68f), 5f, palette.LeafLight);
                    break;
                case "carrot":
                    DrawFrond(texture, root, new Vector2(46f, 62f), 0.7f, palette.Stem, palette.Leaf);
                    DrawFrond(texture, root, new Vector2(65f, 70f), 0.75f, palette.Stem, palette.LeafLight);
                    DrawFrond(texture, root, new Vector2(82f, 60f), 0.7f, palette.Stem, palette.Leaf);
                    break;
                default:
                    DrawLeaf(texture, root, new Vector2(56f, 50f), new Vector2(48f, 64f), 3.6f, palette.Leaf);
                    DrawLeaf(texture, root, new Vector2(65f, 54f), new Vector2(67f, 74f), 3.8f, palette.LeafLight);
                    DrawLeaf(texture, root, new Vector2(72f, 50f), new Vector2(82f, 62f), 3.6f, palette.Leaf);
                    break;
            }
        }

        private static void DrawYoungCrop(Texture2D texture, string cropId, CropPalette palette, List<WheatHead> awns)
        {
            var root = new Vector2(64f, 33f);
            switch (cropId)
            {
                case "corn":
                    DrawLeaf(texture, new Vector2(64f, 44f), new Vector2(42f, 64f), new Vector2(28f, 58f), 7f, palette.Leaf);
                    DrawLeaf(texture, new Vector2(64f, 52f), new Vector2(88f, 72f), new Vector2(100f, 64f), 7f, palette.Leaf);
                    DrawCurve(texture, root, new Vector2(63f, 66f), new Vector2(64f, 100f), 6f, palette.Stem);
                    DrawLeaf(texture, new Vector2(64f, 70f), new Vector2(44f, 90f), new Vector2(34f, 86f), 6.5f, palette.LeafLight);
                    DrawLeaf(texture, new Vector2(64f, 80f), new Vector2(84f, 98f), new Vector2(94f, 94f), 6.5f, palette.LeafLight);
                    DrawLeaf(texture, new Vector2(64f, 96f), new Vector2(60f, 110f), new Vector2(66f, 120f), 5.5f, palette.Leaf);
                    break;
                case "carrot":
                    DrawFrond(texture, root, new Vector2(34f, 78f), 0.9f, palette.Stem, palette.Leaf);
                    DrawFrond(texture, root, new Vector2(94f, 80f), 0.9f, palette.Stem, palette.Leaf);
                    DrawFrond(texture, root, new Vector2(48f, 98f), 1f, palette.Stem, palette.LeafLight);
                    DrawFrond(texture, root, new Vector2(80f, 100f), 1f, palette.Stem, palette.LeafLight);
                    DrawFrond(texture, root, new Vector2(64f, 108f), 1f, palette.Stem, palette.Leaf);
                    DrawShadedEllipse(texture, 64f, 36f, 7f, 5f, 0f, palette.Produce, 0.4f);
                    break;
                default:
                    for (var i = 0; i < 7; i++)
                    {
                        var s = (i - 3f) / 3f;
                        var tip = new Vector2(64f + s * 30f, 92f - Mathf.Abs(s) * 18f + Noise01(i, 1, 5) * 6f);
                        DrawLeaf(
                            texture,
                            new Vector2(64f + s * 6f, 33f),
                            new Vector2(64f + s * 16f, 62f),
                            tip,
                            3.6f,
                            i % 2 == 0 ? palette.Leaf : palette.LeafLight);
                    }

                    // Green heads just forming on the three tallest stems.
                    for (var i = -1; i <= 1; i++)
                    {
                        var basePoint = new Vector2(64f + i * 9f, 82f);
                        var direction = new Vector2(i * 0.25f, 1f);
                        DrawCurve(texture, new Vector2(64f + i * 3f, 34f), new Vector2(64f + i * 6f, 60f), basePoint, 2.4f, palette.Stem);
                        DrawWheatHead(texture, basePoint, direction, 20f, 0.8f, palette.LeafLight);
                    }

                    break;
            }
        }

        private static void DrawRipeWheat(Texture2D texture, CropPalette palette, List<WheatHead> awns)
        {
            var straw = Rgb(186, 162, 72);
            DrawLeaf(texture, new Vector2(60f, 33f), new Vector2(44f, 46f), new Vector2(30f, 50f), 5f, palette.Leaf);
            DrawLeaf(texture, new Vector2(68f, 33f), new Vector2(84f, 46f), new Vector2(98f, 50f), 5f, palette.LeafLight);

            // Outer stalks first so the middle ones sit in front.
            var order = new[] { 0, 8, 1, 7, 2, 6, 3, 5, 4 };
            for (var k = 0; k < order.Length; k++)
            {
                var i = order[k];
                var s = (i - 4f) / 4f;
                var basePoint = new Vector2(64f + s * 13f, 34f);
                var top = new Vector2(
                    64f + s * 38f + (Noise01(i, 2, 7) - 0.5f) * 5f,
                    122f - Mathf.Abs(s) * 18f + Noise01(i, 4, 9) * 6f);
                var control = new Vector2(64f + s * 20f, 80f);
                DrawCurve(texture, basePoint, control, top, 2.6f, straw);

                var direction = (top - control).normalized;
                var head = new WheatHead(top - direction * 2f, direction, 26f, 1f);
                DrawWheatHead(texture, head.Base, head.Direction, head.Length, head.Scale, palette.Produce);
                awns.Add(head);
            }

            DrawLeaf(texture, new Vector2(62f, 33f), new Vector2(50f, 52f), new Vector2(40f, 62f), 4.5f, palette.LeafLight);
            DrawLeaf(texture, new Vector2(66f, 33f), new Vector2(78f, 52f), new Vector2(88f, 62f), 4.5f, palette.Leaf);
        }

        private static void DrawRipeCorn(Texture2D texture, CropPalette palette)
        {
            // Leaves behind the stalk.
            DrawLeaf(texture, new Vector2(64f, 60f), new Vector2(36f, 86f), new Vector2(16f, 66f), 7.5f, palette.Leaf);
            DrawLeaf(texture, new Vector2(64f, 96f), new Vector2(92f, 124f), new Vector2(110f, 112f), 7f, palette.Leaf);

            DrawCurve(texture, new Vector2(64f, 33f), new Vector2(62f, 90f), new Vector2(64f, 142f), 6.5f, palette.Stem);

            // Tassel.
            var tassel = Rgb(214, 182, 104);
            DrawCurve(texture, new Vector2(64f, 140f), new Vector2(56f, 150f), new Vector2(48f, 150f), 2.2f, tassel);
            DrawCurve(texture, new Vector2(64f, 140f), new Vector2(65f, 150f), new Vector2(63f, 158f), 2.2f, tassel);
            DrawCurve(texture, new Vector2(64f, 140f), new Vector2(72f, 150f), new Vector2(80f, 150f), 2.2f, tassel);

            // Leaves in front.
            DrawLeaf(texture, new Vector2(64f, 44f), new Vector2(40f, 58f), new Vector2(26f, 44f), 7f, palette.LeafLight);
            DrawLeaf(texture, new Vector2(64f, 112f), new Vector2(40f, 136f), new Vector2(28f, 128f), 6.5f, palette.LeafLight);
            DrawLeaf(texture, new Vector2(64f, 126f), new Vector2(84f, 144f), new Vector2(98f, 140f), 6f, palette.Leaf);

            // Ear with its husk peeled open.
            DrawLeaf(texture, new Vector2(70f, 58f), new Vector2(92f, 68f), new Vector2(96f, 96f), 6.5f, palette.Leaf);
            DrawCornEar(texture, new Vector2(80f, 84f), 8.5f, 19f, -22f, palette);
            DrawLeaf(texture, new Vector2(70f, 58f), new Vector2(70f, 76f), new Vector2(78f, 94f), 6f, palette.LeafLight);
            var silk = Rgb(170, 110, 60);
            DrawCurve(texture, new Vector2(87f, 101f), new Vector2(92f, 108f), new Vector2(90f, 114f), 1.6f, silk);
            DrawCurve(texture, new Vector2(88f, 101f), new Vector2(96f, 104f), new Vector2(98f, 110f), 1.6f, silk);
        }

        private static void DrawRipeCarrots(Texture2D texture, CropPalette palette)
        {
            var carrots = new[]
            {
                new Vector3(48f, 41f, 8f),
                new Vector3(80f, 40f, -8f),
                new Vector3(64f, 44f, 0f)
            };

            for (var i = 0; i < carrots.Length; i++)
            {
                var c = carrots[i];
                DrawShadedEllipse(texture, c.x, c.y, 10f, 12f, c.z, palette.Produce, 0.45f);
                DrawCurve(texture, new Vector2(c.x - 7f, c.y - 2f), new Vector2(c.x - 3f, c.y - 4f), new Vector2(c.x + 1f, c.y - 3f), 1.4f, palette.ProduceDark);
                DrawCurve(texture, new Vector2(c.x + 2f, c.y + 4f), new Vector2(c.x + 5f, c.y + 3f), new Vector2(c.x + 8f, c.y + 4f), 1.3f, palette.ProduceDark);
            }

            // Front lip of soil hides the lower half of the carrots.
            DrawShadedEllipse(texture, 64f, 27f, 34f, 8.5f, 0f, Rgb(140, 94, 54), 0.12f);

            for (var i = 0; i < carrots.Length; i++)
            {
                var c = carrots[i];
                var top = new Vector2(c.x, c.y + 9f);
                var spread = (c.x - 64f) * 0.8f;
                DrawFrond(texture, top, new Vector2(c.x - 17f + spread, 104f + i * 6f), 0.95f, palette.Stem, palette.Leaf);
                DrawFrond(texture, top, new Vector2(c.x + 16f + spread, 108f + i * 4f), 0.95f, palette.Stem, palette.LeafLight);
                DrawFrond(texture, top, new Vector2(c.x + spread * 0.5f, 124f + i * 4f), 1.05f, palette.Stem, i == 2 ? palette.LeafLight : palette.Leaf);
            }
        }

        // ------------------------------------------------------------------ Crop icons

        private static Texture2D BuildWheatIcon()
        {
            var texture = NewTexture(IconSize, IconSize);
            var palette = GetPalette("wheat");
            DrawEllipse(texture, 64f, 16f, 34f, 7f, Shadow, 0f);

            var layer = NewTexture(IconSize, IconSize);
            var awns = new List<WheatHead>();
            var straw = Rgb(196, 170, 80);
            var tie = new Vector2(64f, 50f);
            var order = new[] { 0, 10, 1, 9, 2, 8, 3, 7, 4, 6, 5 };

            for (var k = 0; k < order.Length; k++)
            {
                var i = order[k];
                var s = (i - 5f) / 5f;
                var bottom = new Vector2(64f + s * 20f, 18f + Mathf.Abs(s) * 4f);
                var knot = tie + new Vector2(s * 4f, 0f);
                var top = new Vector2(64f + s * 32f, 84f + (1f - Mathf.Abs(s)) * 8f + Noise01(i, 6, 3) * 3f);
                DrawCurve(layer, bottom, Vector2.Lerp(bottom, knot, 0.5f), knot, 2.6f, straw);
                DrawCurve(layer, knot, Vector2.Lerp(knot, top, 0.5f), top, 2.6f, straw);

                var direction = (top - knot).normalized;
                var head = new WheatHead(top - direction * 2f, direction, 22f, 1f);
                DrawWheatHead(layer, head.Base, head.Direction, head.Length, head.Scale, palette.Produce);
                awns.Add(head);
            }

            // Red ribbon with a bow.
            DrawShadedEllipse(layer, 64f, 50f, 16f, 5.5f, 0f, Ribbon, 0.4f);
            DrawShadedEllipse(layer, 54f, 53f, 9f, 5.5f, 25f, Ribbon, 0.4f);
            DrawShadedEllipse(layer, 74f, 53f, 9f, 5.5f, -25f, Ribbon, 0.4f);
            DrawLeaf(layer, new Vector2(62f, 48f), new Vector2(58f, 40f), new Vector2(52f, 32f), 3.5f, Ribbon);
            DrawLeaf(layer, new Vector2(66f, 48f), new Vector2(70f, 40f), new Vector2(76f, 32f), 3.5f, Ribbon);
            DrawShadedEllipse(layer, 64f, 51f, 5f, 5f, 0f, Ribbon, 0.5f);

            FinishLayer(texture, layer, IconOutline);
            DrawWheatAwns(texture, awns, palette.ProduceDark);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildCornIcon()
        {
            var texture = NewTexture(IconSize, IconSize);
            var palette = GetPalette("corn");
            DrawEllipse(texture, 64f, 16f, 34f, 7f, Shadow, 0f);

            var layer = NewTexture(IconSize, IconSize);
            var husk = Rgb(118, 176, 72);
            var stem = new Vector2(44f, 30f);
            DrawLeaf(layer, stem, new Vector2(24f, 50f), new Vector2(28f, 84f), 10f, palette.Leaf);
            DrawLeaf(layer, stem, new Vector2(78f, 30f), new Vector2(104f, 46f), 10f, palette.Leaf);
            DrawCurve(layer, new Vector2(46f, 32f), new Vector2(40f, 24f), new Vector2(32f, 18f), 7f, husk);

            DrawCornEar(layer, new Vector2(70f, 72f), 15f, 36f, -30f, palette);

            DrawLeaf(layer, stem, new Vector2(52f, 56f), new Vector2(46f, 80f), 9f, husk);
            DrawLeaf(layer, stem, new Vector2(66f, 42f), new Vector2(84f, 54f), 8f, palette.LeafLight);

            FinishLayer(texture, layer, IconOutline);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildCarrotIcon()
        {
            var texture = NewTexture(IconSize, IconSize);
            var palette = GetPalette("carrot");
            DrawEllipse(texture, 64f, 14f, 30f, 6f, Shadow, 0f);

            var layer = NewTexture(IconSize, IconSize);
            var top = new Vector2(70f, 80f);
            DrawFrond(layer, top, new Vector2(46f, 104f), 0.95f, palette.Stem, palette.Leaf);
            DrawFrond(layer, top, new Vector2(94f, 102f), 0.95f, palette.Stem, palette.Leaf);
            DrawFrond(layer, top, new Vector2(70f, 110f), 1f, palette.Stem, palette.LeafLight);

            DrawStroke(
                layer,
                top,
                new Vector2(62f, 52f),
                new Vector2(46f, 18f),
                t => Mathf.Lerp(17f, 2.5f, Mathf.Pow(t, 0.85f)),
                palette.Produce,
                0f,
                0.4f);

            // Rings around the root.
            for (var i = 0; i < 5; i++)
            {
                var t = 0.18f + i * 0.15f;
                var p = Bezier(top, new Vector2(62f, 52f), new Vector2(46f, 18f), t);
                var half = Mathf.Lerp(17f, 2.5f, Mathf.Pow(t, 0.85f));
                var side = i % 2 == 0 ? -1f : 1f;
                var start = p + new Vector2(side * half * 0.85f, 0f);
                var end = p + new Vector2(side * half * 0.25f, 1.5f);
                DrawCurve(layer, start, Vector2.Lerp(start, end, 0.5f) + new Vector2(0f, -1.5f), end, 1.5f, palette.ProduceDark);
            }

            FinishLayer(texture, layer, IconOutline);
            texture.Apply(false, false);
            return texture;
        }

        // ------------------------------------------------------------------ Feed sacks

        private enum FeedEmblem
        {
            None,
            Fish,
            Egg,
            Spots,
            Horns
        }

        private static Texture2D BuildFeedIcon()
        {
            return BuildFeedSack(Hay, FeedEmblem.None, Rgb(214, 180, 90));
        }

        /// <summary>A tied burlap sack, heaped with feed, with a round badge for the animal.</summary>
        private static Texture2D BuildFeedVariantIcon(Color badgeColor, FeedEmblem emblem)
        {
            var feedColor = Color.Lerp(Hay, badgeColor, 0.35f);
            return BuildFeedSack(badgeColor, emblem, feedColor);
        }

        private static Texture2D BuildFeedSack(Color badgeColor, FeedEmblem emblem, Color feedColor)
        {
            var texture = NewTexture(IconSize, IconSize);
            DrawEllipse(texture, 64f, 16f, 40f, 8f, Shadow, 0f);

            var layer = NewTexture(IconSize, IconSize);
            DrawShadedEllipse(layer, 64f, 52f, 42f, 37f, 0f, Burlap, 0.12f);
            DrawShadedEllipse(layer, 64f, 86f, 17f, 9f, 0f, Burlap, 0.08f);
            AddWeave(layer, 0, 92);

            // Gathered top and the opening heaped with feed.
            DrawShadedEllipse(layer, 64f, 99f, 25f, 11f, 0f, BurlapLight, 0.15f);
            DrawEllipse(layer, 64f, 102f, 18f, 5.5f, Rgb(96, 64, 34), 0f);
            var pellets = new[]
            {
                new Vector2(52f, 103f), new Vector2(60f, 104f), new Vector2(68f, 104f), new Vector2(76f, 103f),
                new Vector2(56f, 107f), new Vector2(64f, 108f), new Vector2(72f, 107f), new Vector2(60f, 111f),
                new Vector2(68f, 111f), new Vector2(64f, 114f)
            };
            for (var i = 0; i < pellets.Length; i++)
            {
                var tint = Scale(feedColor, 0.9f + Noise01(i, 2, 61) * 0.25f);
                DrawShadedEllipse(layer, pellets[i].x, pellets[i].y, 4f, 3.2f, Noise01(i, 4, 7) * 60f - 30f, tint, 0.4f);
            }

            // Twine round the neck with a knot and loose ends.
            DrawCurve(layer, new Vector2(46f, 92f), new Vector2(64f, 82f), new Vector2(82f, 92f), 4f, Twine);
            DrawShadedEllipse(layer, 72f, 87f, 4f, 3.5f, 0f, Twine, 0.3f);
            DrawCurve(layer, new Vector2(72f, 85f), new Vector2(76f, 78f), new Vector2(74f, 70f), 2.4f, Twine);
            DrawCurve(layer, new Vector2(73f, 85f), new Vector2(82f, 80f), new Vector2(84f, 72f), 2.4f, Twine);

            if (emblem != FeedEmblem.None)
            {
                DrawShadedEllipse(layer, 64f, 50f, 22f, 22f, 0f, Rgb(250, 242, 220), 0.2f);
                DrawShadedEllipse(layer, 64f, 50f, 17.5f, 17.5f, 0f, badgeColor, 0.3f);
                DrawFeedEmblem(layer, emblem, badgeColor);
            }

            FinishLayer(texture, layer, IconOutline);
            texture.Apply(false, false);
            return texture;
        }

        private static void DrawFeedEmblem(Texture2D texture, FeedEmblem emblem, Color badgeColor)
        {
            var cream = Rgb(255, 250, 238);
            switch (emblem)
            {
                case FeedEmblem.Fish:
                    DrawTriangle(texture, new Vector2(68f, 50f), new Vector2(78f, 58f), new Vector2(78f, 42f), cream);
                    DrawShadedEllipse(texture, 60f, 50f, 10f, 6.5f, 0f, cream, 0.3f);
                    DrawEllipse(texture, 55f, 51.5f, 1.6f, 1.6f, Scale(badgeColor, 0.45f), 0f);
                    break;
                case FeedEmblem.Egg:
                    DrawShadedEllipse(texture, 64f, 49f, 8f, 10.5f, 0f, cream, 0.6f);
                    break;
                case FeedEmblem.Horns:
                    DrawStroke(texture, new Vector2(58f, 40f), new Vector2(50f, 56f), new Vector2(60f, 64f), t => Mathf.Lerp(4f, 1.2f, t), cream, 0f, 0.3f);
                    DrawStroke(texture, new Vector2(70f, 40f), new Vector2(78f, 56f), new Vector2(68f, 64f), t => Mathf.Lerp(4f, 1.2f, t), cream, 0f, 0.3f);
                    break;
                case FeedEmblem.Spots:
                    DrawShadedEllipse(texture, 57f, 54f, 5f, 4f, 15f, CowBlack, 0.2f);
                    DrawShadedEllipse(texture, 70f, 46f, 4.5f, 3.5f, -20f, CowBlack, 0.2f);
                    DrawShadedEllipse(texture, 71f, 56f, 2.8f, 2.2f, 0f, CowBlack, 0.2f);
                    DrawShadedEllipse(texture, 58f, 43f, 2.4f, 2f, 0f, CowBlack, 0.2f);
                    break;
            }
        }

        /// <summary>Burlap texture: a faint diagonal weave over the opaque pixels below a height.</summary>
        private static void AddWeave(Texture2D texture, int minY, int maxY)
        {
            for (var y = minY; y < Mathf.Min(maxY, texture.height); y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var c = texture.GetPixel(x, y);
                    if (c.a < 0.5f)
                    {
                        continue;
                    }

                    var weave = ((x + y) % 4 == 0 ? 0.93f : 1f) * ((x - y + 400) % 4 == 0 ? 0.95f : 1f);
                    var noise = 0.97f + Noise01(x, y, 91) * 0.06f;
                    texture.SetPixel(x, y, Scale(c, weave * noise));
                }
            }
        }

        // ------------------------------------------------------------------ Shared crop parts

        private readonly struct WheatHead
        {
            public readonly Vector2 Base;
            public readonly Vector2 Direction;
            public readonly float Length;
            public readonly float Scale;

            public WheatHead(Vector2 basePoint, Vector2 direction, float length, float scale)
            {
                Base = basePoint;
                Direction = direction.normalized;
                Length = length;
                Scale = scale;
            }
        }

        /// <summary>Two staggered rows of plump grains along the stalk direction.</summary>
        private static void DrawWheatHead(Texture2D texture, Vector2 basePoint, Vector2 direction, float length, float scale, Color grain)
        {
            direction.Normalize();
            var perpendicular = new Vector2(-direction.y, direction.x);
            var angle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
            const int grains = 7;
            for (var i = 0; i < grains; i++)
            {
                var along = (i + 0.5f) / grains;
                var side = i % 2 == 0 ? 1f : -1f;
                var size = Mathf.Lerp(1f, 0.7f, along) * scale;
                var position = basePoint + direction * (length * along) + perpendicular * (side * 2.4f * scale);
                DrawShadedEllipse(texture, position.x, position.y, 3.3f * size, 5.2f * size, angle - side * 16f, grain, 0.35f);
            }
        }

        /// <summary>Fine whiskers on the grains. Drawn after the outline so they stay thin.</summary>
        private static void DrawWheatAwns(Texture2D texture, List<WheatHead> heads, Color color)
        {
            var awn = new Color(color.r, color.g, color.b, 0.85f);
            for (var h = 0; h < heads.Count; h++)
            {
                var head = heads[h];
                var perpendicular = new Vector2(-head.Direction.y, head.Direction.x);
                for (var i = 2; i < 7; i++)
                {
                    var along = (i + 0.5f) / 7f;
                    var side = i % 2 == 0 ? 1f : -1f;
                    var start = head.Base + head.Direction * (head.Length * along) + perpendicular * (side * 4f * head.Scale);
                    var end = start + head.Direction * (10f * head.Scale) + perpendicular * (side * 5f * head.Scale);
                    DrawLine(texture, start.x, start.y, end.x, end.y, 1f, awn);
                }

                var tip = head.Base + head.Direction * (head.Length + 2f);
                var tipEnd = tip + head.Direction * (10f * head.Scale);
                DrawLine(texture, tip.x, tip.y, tipEnd.x, tipEnd.y, 1f, awn);
            }
        }

        /// <summary>A corn cob: shaded base with rows of glossy kernels.</summary>
        private static void DrawCornEar(Texture2D texture, Vector2 center, float radiusX, float radiusY, float rotation, CropPalette palette)
        {
            DrawShadedEllipse(texture, center.x, center.y, radiusX, radiusY, rotation, palette.ProduceDark, 0.1f);
            var radians = rotation * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            var kernel = radiusX * 0.24f;
            var columns = Mathf.Max(2, Mathf.RoundToInt(radiusX / (kernel * 2f)));

            for (var ly = -radiusY; ly <= radiusY; ly += kernel * 2f)
            {
                for (var column = -columns; column <= columns; column++)
                {
                    var lx = column * kernel * 2f;
                    var oy = ly + (column % 2 == 0 ? 0f : kernel);
                    var nx = lx / radiusX;
                    var ny = oy / radiusY;
                    if (nx * nx + ny * ny > 0.82f)
                    {
                        continue;
                    }

                    var wx = center.x + lx * cos - oy * sin;
                    var wy = center.y + lx * sin + oy * cos;
                    var size = kernel * (1f - Mathf.Abs(nx) * 0.3f);
                    DrawShadedEllipse(texture, wx, wy, size, size * 1.1f, rotation, palette.Produce, 0.55f);
                }
            }
        }

        /// <summary>A carrot top: a curved stem with pairs of small leaflets and a leaf at the tip.</summary>
        private static void DrawFrond(Texture2D texture, Vector2 basePoint, Vector2 tip, float scale, Color stem, Color leaf)
        {
            var mid = Vector2.Lerp(basePoint, tip, 0.5f);
            var along = tip - basePoint;
            var bend = new Vector2(-along.y, along.x).normalized * (along.magnitude * 0.12f * Mathf.Sign(tip.x - basePoint.x + 0.01f));
            var control = mid + bend;
            DrawCurve(texture, basePoint, control, tip, 2f * scale, stem);

            for (var k = 0; k < 4; k++)
            {
                var t = 0.4f + k * 0.15f;
                var point = Bezier(basePoint, control, tip, t);
                var tangent = (Bezier(basePoint, control, tip, Mathf.Min(1f, t + 0.02f)) - point).normalized;
                var normal = new Vector2(-tangent.y, tangent.x);
                var length = 10f * scale * (1.15f - t * 0.45f);
                for (var side = -1; side <= 1; side += 2)
                {
                    var outward = normal * side;
                    DrawLeaf(
                        texture,
                        point,
                        point + outward * (length * 0.5f) + tangent * (length * 0.2f),
                        point + outward * length + tangent * (length * 0.55f),
                        3f * scale,
                        leaf);
                }
            }

            var tipDirection = (tip - control).normalized;
            DrawLeaf(texture, tip - tipDirection * 2f, tip + tipDirection * 5f, tip + tipDirection * 11f * scale, 3.4f * scale, leaf);
        }

        // ------------------------------------------------------------------ Drawing primitives

        private static void DrawLeaf(Texture2D texture, Vector2 start, Vector2 control, Vector2 end, float width, Color color)
        {
            DrawStroke(
                texture,
                start,
                control,
                end,
                // Clamp: sin(pi) is a hair below zero in floats, and Pow of a negative is NaN.
                t => width * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0.08f, 1f, t))), 0.7f),
                color,
                0.45f,
                0.15f);
        }

        private static void DrawCurve(Texture2D texture, Vector2 start, Vector2 control, Vector2 end, float thickness, Color color)
        {
            var half = thickness * 0.5f;
            DrawStroke(texture, start, control, end, _ => half, color, 0f, 0.1f);
        }

        /// <summary>
        /// A shape swept along a quadratic curve. Its half-width varies along the curve and it is
        /// shaded like a tube, so it reads as rounded. A rib darkens the middle of leaves.
        /// </summary>
        private static void DrawStroke(
            Texture2D texture,
            Vector2 start,
            Vector2 control,
            Vector2 end,
            Func<float, float> halfWidthAt,
            Color color,
            float ribStrength,
            float shine)
        {
            const int segments = 32;
            var points = new Vector2[segments + 1];
            var maxHalfWidth = 0f;
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                points[i] = Bezier(start, control, end, t);
                var halfWidth = halfWidthAt(t);
                if (!float.IsNaN(halfWidth))
                {
                    maxHalfWidth = Mathf.Max(maxHalfWidth, halfWidth);
                }
            }

            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;
            for (var i = 0; i <= segments; i++)
            {
                minX = Mathf.Min(minX, points[i].x);
                minY = Mathf.Min(minY, points[i].y);
                maxX = Mathf.Max(maxX, points[i].x);
                maxY = Mathf.Max(maxY, points[i].y);
            }

            var pad = maxHalfWidth + 2f;
            var x0 = Mathf.Max(0, Mathf.FloorToInt(minX - pad));
            var y0 = Mathf.Max(0, Mathf.FloorToInt(minY - pad));
            var x1 = Mathf.Min(texture.width - 1, Mathf.CeilToInt(maxX + pad));
            var y1 = Mathf.Min(texture.height - 1, Mathf.CeilToInt(maxY + pad));

            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var best = float.MaxValue;
                    var bestT = 0f;
                    var closest = Vector2.zero;
                    for (var i = 0; i < segments; i++)
                    {
                        var a = points[i];
                        var ab = points[i + 1] - a;
                        var lengthSquared = ab.sqrMagnitude;
                        var tt = lengthSquared > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared) : 0f;
                        var q = a + ab * tt;
                        var distance = (p - q).magnitude;
                        if (distance < best)
                        {
                            best = distance;
                            bestT = (i + tt) / segments;
                            closest = q;
                        }
                    }

                    var halfWidth = Mathf.Max(0f, halfWidthAt(bestT));
                    var coverage = Mathf.Clamp01(halfWidth - best + 0.5f);
                    if (coverage <= 0f)
                    {
                        continue;
                    }

                    var across = halfWidth > 0.001f ? Mathf.Clamp01(best / halfWidth) : 0f;
                    var outward = best > 0.001f ? (p - closest) / best : Vector2.zero;
                    var normal = new Vector3(outward.x * across, outward.y * across, Mathf.Sqrt(1f - across * across));
                    var diffuse = Mathf.Clamp01(Vector3.Dot(normal, LightDirection));
                    var shaded = ShadeColor(color, diffuse, shine);

                    if (ribStrength > 0f)
                    {
                        var rib = Mathf.Clamp01(1f - best / 1.1f) * ribStrength * (1f - bestT * 0.7f);
                        shaded = Color.Lerp(shaded, Scale(color, 0.7f), rib);
                    }

                    BlendPixel(texture, x, y, shaded, coverage);
                }
            }
        }

        /// <summary>An ellipse shaded like a lit dome, with a soft highlight on the upper left.</summary>
        private static void DrawShadedEllipse(
            Texture2D texture,
            float centerX,
            float centerY,
            float radiusX,
            float radiusY,
            float rotationDegrees,
            Color color,
            float shine)
        {
            var rotation = rotationDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rotation);
            var sin = Mathf.Sin(rotation);
            var extent = Mathf.CeilToInt(Mathf.Max(radiusX, radiusY) + 2f);
            var minX = Mathf.FloorToInt(centerX) - extent;
            var maxX = Mathf.CeilToInt(centerX) + extent;
            var minY = Mathf.FloorToInt(centerY) - extent;
            var maxY = Mathf.CeilToInt(centerY) + extent;

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var dx = x + 0.5f - centerX;
                    var dy = y + 0.5f - centerY;
                    var nx = (dx * cos + dy * sin) / radiusX;
                    var ny = (-dx * sin + dy * cos) / radiusY;
                    var lengthSquared = nx * nx + ny * ny;
                    var distance = (Mathf.Sqrt(lengthSquared) - 1f) * Mathf.Min(radiusX, radiusY);
                    var coverage = Mathf.Clamp01(0.5f - distance);
                    if (coverage <= 0f)
                    {
                        continue;
                    }

                    var wx = nx * cos - ny * sin;
                    var wy = nx * sin + ny * cos;
                    var wz = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Min(1f, lengthSquared)));
                    var diffuse = Mathf.Clamp01(wx * LightDirection.x + wy * LightDirection.y + wz * LightDirection.z);
                    BlendPixel(texture, x, y, ShadeColor(color, diffuse, shine), coverage);
                }
            }
        }

        private static Color ShadeColor(Color color, float diffuse, float shine)
        {
            var shaded = Color.Lerp(Scale(color, 0.62f), Scale(color, 1.1f), diffuse);
            var highlight = Mathf.SmoothStep(0f, 1f, (diffuse - 0.9f) / 0.1f) * shine;
            shaded = Color.Lerp(shaded, new Color(1f, 0.98f, 0.9f, 1f), highlight);
            shaded.a = color.a;
            return shaded;
        }

        private static Color Scale(Color color, float factor)
        {
            return new Color(
                Mathf.Clamp01(color.r * factor),
                Mathf.Clamp01(color.g * factor),
                Mathf.Clamp01(color.b * factor),
                color.a);
        }

        private static Vector2 Bezier(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            var u = 1f - t;
            return u * u * start + 2f * u * t * control + t * t * end;
        }

        /// <summary>Outlines the layer, composites it onto the texture and frees the layer.</summary>
        private static void FinishLayer(Texture2D texture, Texture2D layer, float outlineRadius)
        {
            ApplyOutline(layer, ArtOutline, outlineRadius);
            var pixels = layer.GetPixels();
            for (var y = 0; y < layer.height; y++)
            {
                for (var x = 0; x < layer.width; x++)
                {
                    var c = pixels[y * layer.width + x];
                    if (c.a > 0.001f)
                    {
                        BlendPixel(texture, x, y, c, 1f);
                    }
                }
            }

            UnityEngine.Object.DestroyImmediate(layer);
        }

        /// <summary>Puts a soft outline of the given radius behind everything drawn on the texture.</summary>
        private static void ApplyOutline(Texture2D texture, Color color, float radius)
        {
            var width = texture.width;
            var height = texture.height;
            var source = texture.GetPixels();
            var result = new Color[source.Length];
            var reach = Mathf.CeilToInt(radius);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var s = source[y * width + x];
                    var outline = 0f;
                    if (s.a < 0.999f)
                    {
                        for (var oy = -reach; oy <= reach; oy++)
                        {
                            var ny = y + oy;
                            if (ny < 0 || ny >= height)
                            {
                                continue;
                            }

                            for (var ox = -reach; ox <= reach; ox++)
                            {
                                var nx = x + ox;
                                if (nx < 0 || nx >= width)
                                {
                                    continue;
                                }

                                var weight = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(ox * ox + oy * oy));
                                if (weight > 0f)
                                {
                                    outline = Mathf.Max(outline, source[ny * width + nx].a * weight);
                                }
                            }
                        }
                    }

                    outline *= color.a;
                    var alpha = s.a + outline * (1f - s.a);
                    if (alpha <= 0.0001f)
                    {
                        result[y * width + x] = Transparent;
                        continue;
                    }

                    var behind = outline * (1f - s.a);
                    result[y * width + x] = new Color(
                        (s.r * s.a + color.r * behind) / alpha,
                        (s.g * s.a + color.g * behind) / alpha,
                        (s.b * s.a + color.b * behind) / alpha,
                        alpha);
                }
            }

            texture.SetPixels(result);
        }
    }
}
