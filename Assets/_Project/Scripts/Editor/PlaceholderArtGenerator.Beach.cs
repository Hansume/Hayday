using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>
    /// The Beach location's art, painted in code, in the farm's isometric view: seamless sand, a
    /// shoreline and surf that repeat along the one straight coast, seamless sea tiles with lines
    /// along the isometric axes, the things lying on the sand, the dock and the boat (see FarmView.Beach).
    /// </summary>
    public static partial class PlaceholderArtGenerator
    {
        // A cell is 2 x 1 world units, so a tile 512 x 256 keeps isometric lines seamless.
        private const int SeaTileWidth = 512;
        private const int SeaTileHeight = 256;
        private const float BeachPixelsPerUnit = 64f;

        /// <summary>The seamless sand tile, in pixels and world units.</summary>
        private const int SandTileSize = 1024;
        private const float SandTileWorldSize = 13f;

        /// <summary>
        /// The shore and surf pictures: one length of coast, repeating sideways, with the waterline
        /// across their middle. They lie along the coast, so "up" in the picture is inland.
        /// </summary>
        private const int CoastTileWidth = 2048;
        private const int CoastTileHeight = 640;
        private const float CoastPixelsPerUnit = 128f;

        /// <summary>World units, straight across the coast, for each cell walked towards the sea (2 / sqrt 5).</summary>
        private const float CoastDepthPerCell = 0.894f;

        /// <summary>Scale of the dock and boat pictures.</summary>
        private const float DockPixelsPerUnit = 128f;
        private const int PalmWidth = 800;
        private const int PalmHeight = 800;
        private const float PalmBaseY = 56f;

        private static readonly Color SandDark = Rgb(230, 200, 144);
        private static readonly Color SandLight = Rgb(248, 228, 180);
        private static readonly Color SandWet = Rgb(204, 170, 118);
        private static readonly Color SeaDark = Rgb(38, 158, 196);
        private static readonly Color SeaLight = Rgb(74, 196, 216);
        private static readonly Color SeaShallow = Rgb(134, 228, 222);
        private static readonly Color Foam = Rgb(250, 254, 250);

        /// <summary>Writes only the beach art, so it can be added without regenerating everything.</summary>
        [MenuItem("Tools/Farm Game/Rebuild Beach Art", false, 21)]
        public static void GenerateBeachArt()
        {
            EnsureFolder(ArtDirectory);
            WriteBeachArt();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureBeachSprites();
            AssetDatabase.Refresh();
            Debug.Log("Beach art generated.");
        }

        private static void WriteBeachArt()
        {
            WritePng("beach_land", BuildBeachLand());
            WritePng("beach_shore", BuildBeachShore());
            WritePng("beach_surf", BuildBeachSurf());
            WritePng("beach_dock", BuildBeachDock());
            WritePng("beach_boat", BuildBeachBoat());
            WritePng("beach_sea", BuildBeachSea());
            WritePng("beach_glints", BuildBeachGlints());
            WritePng("beach_palm", BuildBeachPalm());
            WritePng("beach_rock", BuildBeachRock());
            WritePng("beach_starfish", BuildBeachStarfish());
            WritePng("beach_shell", BuildBeachShell());
            WritePng("icon_beach", BuildBeachIcon());
        }

        private static void ConfigureBeachSprites()
        {
            ConfigureSprite("beach_land", SandTileSize / SandTileWorldSize, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("beach_shore", CoastPixelsPerUnit, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("beach_surf", CoastPixelsPerUnit, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("beach_dock", DockPixelsPerUnit, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("beach_boat", DockPixelsPerUnit, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("beach_sea", BeachPixelsPerUnit, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("beach_glints", BeachPixelsPerUnit, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("beach_palm", 96f, SpriteAlignment.Custom, Vector4.zero, new Vector2(0.5f, PalmBaseY / PalmHeight));
            ConfigureSprite("beach_rock", 96f, SpriteAlignment.Custom, Vector4.zero, new Vector2(0.5f, 0.2f));
            ConfigureSprite("beach_starfish", 128f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("beach_shell", 128f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_beach", 100f, SpriteAlignment.Center, Vector4.zero);
        }

        /// <summary>0 at or below <paramref name="from"/>, 1 at or above <paramref name="to"/>, smooth between.</summary>
        private static float Ramp(float from, float to, float value)
        {
            var t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ Seamless noise

        /// <summary>Smooth noise that repeats every <paramref name="period"/> cells; u and v run 0..1 over one tile.</summary>
        private static float TileNoise(float u, float v, int period, int seed)
        {
            var x = u * period;
            var y = v * period;
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var fx = Ramp(0f, 1f, x - x0);
            var fy = Ramp(0f, 1f, y - y0);
            int W(int i) => ((i % period) + period) % period;
            var a = Noise01(W(x0), W(y0), seed);
            var b = Noise01(W(x0 + 1), W(y0), seed);
            var c = Noise01(W(x0), W(y0 + 1), seed);
            var d = Noise01(W(x0 + 1), W(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float TileFractal(float u, float v, int period, int seed)
        {
            return TileNoise(u, v, period, seed) * 0.5f +
                   TileNoise(u, v, period * 2, seed + 1) * 0.3f +
                   TileNoise(u, v, period * 4, seed + 2) * 0.2f;
        }

        private static Texture2D FromPixels(int width, int height, Color[] pixels)
        {
            var texture = NewTexture(width, height);
            texture.SetPixels(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>Paints a soft-edged disc over what's there.</summary>
        private static void Stamp(Color[] pixels, int width, int height, float cx, float cy, float radius, Color color)
        {
            var minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1f));
            var maxX = Mathf.Min(width - 1, Mathf.CeilToInt(cx + radius + 1f));
            var minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1f));
            var maxY = Mathf.Min(height - 1, Mathf.CeilToInt(cy + radius + 1f));
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var distance = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    var alpha = Mathf.Clamp01(radius - distance + 0.5f) * color.a;
                    if (alpha > 0f)
                    {
                        pixels[y * width + x] = Over(pixels[y * width + x], color, alpha);
                    }
                }
            }
        }

        private static Color Over(Color below, Color above, float alpha)
        {
            var outAlpha = alpha + below.a * (1f - alpha);
            if (outAlpha <= 0f)
            {
                return new Color(above.r, above.g, above.b, 0f);
            }

            var keep = below.a * (1f - alpha);
            return new Color(
                (above.r * alpha + below.r * keep) / outAlpha,
                (above.g * alpha + below.g * keep) / outAlpha,
                (above.b * alpha + below.b * keep) / outAlpha,
                outAlpha);
        }

        private static Color[] ClearPixels(int width, int height, Color tint)
        {
            // Transparent pixels keep a matching colour so soft edges don't pick up a dark fringe.
            var pixels = new Color[width * height];
            var clear = new Color(tint.r, tint.g, tint.b, 0f);
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            return pixels;
        }

        // ------------------------------------------------------------------ Sand and sea

        /// <summary>Turns a position in world units, from the beach's middle, into cell coordinates.</summary>
        private static void BeachCell(float worldX, float worldY, out float a, out float b)
        {
            a = worldX * 0.5f - worldY;
            b = -worldY - worldX * 0.5f;
        }

        /// <summary>
        /// How far inland a point of a coast picture is, in cells (negative: out at sea), with a gentle
        /// wave that repeats with the picture. The shore and surf both use this, so they fit together.
        /// </summary>
        private static float CoastEdge(int x, int y)
        {
            var turn = (float)x / CoastTileWidth * Mathf.PI * 2f;
            var wobble = 0.1f * Mathf.Sin(turn * 2f + 1f) + 0.08f * Mathf.Sin(turn * 5f + 2f) + 0.035f * Mathf.Sin(turn * 11f);
            var inland = (y - CoastTileHeight * 0.5f) / CoastPixelsPerUnit;
            return inland / CoastDepthPerCell + wobble;
        }

        /// <summary>Dry sand. u and v run 0..1 over one tile, which repeats seamlessly.</summary>
        private static Color SandColor(float u, float v, int x, int y)
        {
            var color = Color.Lerp(SandDark, SandLight, Ramp(0.2f, 0.8f, TileFractal(u, v, 7, 11)));

            // Wind ripples: a whole number of them fits the tile.
            var ripple = Mathf.Sin(v * Mathf.PI * 2f * 7f + TileNoise(u, v, 6, 15) * 5f);
            color = Color.Lerp(color, SandDark, Mathf.Max(0f, ripple) * 0.14f);

            var grain = Noise01(x, y, 17);
            if (grain > 0.985f)
            {
                color = Color.Lerp(color, Rgb(176, 140, 92), 0.45f);
            }
            else if (grain < 0.015f)
            {
                color = Color.Lerp(color, Color.white, 0.5f);
            }

            return color;
        }

        /// <summary>The sand everywhere inland: one seamless, solid tile.</summary>
        private static Texture2D BuildBeachLand()
        {
            var pixels = new Color[SandTileSize * SandTileSize];
            for (var y = 0; y < SandTileSize; y++)
            {
                for (var x = 0; x < SandTileSize; x++)
                {
                    var color = SandColor((float)x / SandTileSize, (float)y / SandTileSize, x, y);
                    color.a = 1f;
                    pixels[y * SandTileSize + x] = color;
                }
            }

            return FromPixels(SandTileSize, SandTileSize, pixels);
        }

        /// <summary>The sand's edge along the coast: wet sand and the last wave's line, fading into the dry sand inland.</summary>
        private static Texture2D BuildBeachShore()
        {
            var width = CoastTileWidth;
            var height = CoastTileHeight;
            var pixels = ClearPixels(width, height, SandWet);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var edge = CoastEdge(x, y);
                    if (edge <= 0f)
                    {
                        continue;
                    }

                    var u = (float)x / width;
                    var v = (float)y / height;
                    var color = SandColor(u, v * height / (SandTileWorldSize * CoastPixelsPerUnit), x, y);
                    color = Color.Lerp(color, SandWet, 1f - Ramp(0.3f, 1.15f, edge));
                    var mark = Mathf.Abs(edge - 0.62f - (TileNoise(u, v, 24, 53) - 0.5f) * 0.14f);
                    color = Color.Lerp(color, Rgb(168, 136, 92), (1f - Mathf.Clamp01(mark / 0.035f)) * 0.35f);

                    color.a = Ramp(0f, 0.05f, edge) * (1f - Ramp(1.6f, 2.4f, edge));
                    pixels[y * width + x] = color;
                }
            }

            return FromPixels(width, height, pixels);
        }

        /// <summary>What lies off the coast, under the sand: shallow water, a foam line and a broken second wave.</summary>
        private static Texture2D BuildBeachSurf()
        {
            var width = CoastTileWidth;
            var height = CoastTileHeight;
            var pixels = ClearPixels(width, height, SeaShallow);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var edge = CoastEdge(x, y);
                    if (edge < -2.4f)
                    {
                        continue;
                    }

                    var color = SeaShallow;
                    color.a = 0.92f * (1f - Ramp(0f, 2.4f, -edge));

                    var u = (float)x / width;
                    var v = (float)y / height;
                    var lace = TileNoise(u, v, 40, 51);
                    var foam = 1f - Ramp(0.16f, 0.3f + lace * 0.16f, Mathf.Abs(edge + 0.08f));
                    var bend = 0.12f * Mathf.Sin(u * Mathf.PI * 2f * 7f);
                    var second = (1f - Mathf.Clamp01(Mathf.Abs(edge + 1.05f + bend) / 0.07f)) *
                                 Ramp(0.42f, 0.62f, lace) * 0.85f;
                    var white = Mathf.Max(foam, second);
                    if (white > 0f)
                    {
                        var alpha = color.a;
                        color = Color.Lerp(color, Foam, white);
                        color.a = Mathf.Lerp(alpha, 1f, white);
                    }

                    pixels[y * width + x] = color;
                }
            }

            return FromPixels(width, height, pixels);
        }

        private static Texture2D BuildBeachSea()
        {
            var width = SeaTileWidth;
            var height = SeaTileHeight;
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var u = (float)x / width;
                    var v = (float)y / height;
                    var color = Color.Lerp(SeaDark, SeaLight, Ramp(0.25f, 0.75f, TileFractal(u, v, 4, 31)));

                    // Whole numbers of u - v keep the swell seamless and slope it 2:1, like the grid.
                    var swell = Mathf.Sin(((u - v) * 6f + TileNoise(u, v, 3, 35) * 1.4f) * Mathf.PI * 2f);
                    color = Color.Lerp(color, SeaLight, Mathf.Pow(Mathf.Max(0f, swell), 6f) * 0.5f);
                    color.a = 1f;
                    pixels[y * width + x] = color;
                }
            }

            return FromPixels(width, height, pixels);
        }

        /// <summary>Little curved highlights lying along an isometric axis; two layers drift over the sea.</summary>
        private static Texture2D BuildBeachGlints()
        {
            var width = SeaTileWidth;
            var height = SeaTileHeight;
            var pixels = ClearPixels(width, height, Color.white);
            var along = new Vector2(2f, 1f).normalized;
            var across = new Vector2(-along.y, along.x);
            for (var i = 0; i < 16; i++)
            {
                var cx = Noise01(i, 1, 41) * width;
                var cy = Noise01(i, 2, 41) * height;
                var halfLength = Mathf.Lerp(14f, 36f, Noise01(i, 3, 41));
                var strength = Mathf.Lerp(0.35f, 0.8f, Noise01(i, 4, 41));
                for (var step = -60; step <= 60; step++)
                {
                    var s = step / 60f;
                    var point = along * (s * halfLength) - across * (s * s * halfLength * 0.16f);
                    var px = Mathf.Repeat(cx + point.x, width);
                    var py = Mathf.Repeat(cy + point.y, height);
                    Stamp(pixels, width, height, px, py, Mathf.Lerp(2.4f, 0.8f, Mathf.Abs(s)), new Color(1f, 1f, 1f, strength * (1f - s * s)));
                }
            }

            return FromPixels(width, height, pixels);
        }

        // ------------------------------------------------------------------ Dock and boat

        /// <summary>
        /// A plank pier lying along the isometric axis that leads out to sea, 4 cells long and 1.4
        /// across, on posts. Its size must match FarmView.Beach, which also uses it for taps.
        /// </summary>
        private static Texture2D BuildBeachDock()
        {
            const int width = 800;
            const int height = 512;
            const float halfLength = 2f;
            const float halfAcross = 0.7f;
            var wood = Rgb(170, 122, 76);
            var pixels = ClearPixels(width, height, wood);

            float Inside(float wx, float wy)
            {
                BeachCell(wx, wy, out var a, out var b);
                return Mathf.Min(halfLength - Mathf.Abs(a), halfAcross - Mathf.Abs(b));
            }

            // Its shadow on the water, under everything else.
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var wx = (x - width * 0.5f) / DockPixelsPerUnit;
                    var wy = (y - height * 0.5f) / DockPixelsPerUnit;
                    if (Inside(wx, wy + 0.42f) > 0f)
                    {
                        pixels[y * width + x] = new Color(0.04f, 0.2f, 0.3f, 0.26f);
                    }
                }
            }

            // Posts along both sides, standing in a little ring of foam.
            var post = Rgb(96, 64, 40);
            var postLight = Rgb(128, 90, 58);
            for (var a = 0f; a <= halfLength; a += 1f)
            {
                foreach (var side in new[] { -halfAcross, halfAcross })
                {
                    var top = new Vector2(a - side, -(a + side) * 0.5f);
                    var px = top.x * DockPixelsPerUnit + width * 0.5f;
                    var py = top.y * DockPixelsPerUnit + height * 0.5f;
                    Stamp(pixels, width, height, px, py - 88f, 12f, new Color(1f, 1f, 1f, 0.55f));
                    for (var drop = 0f; drop <= 88f; drop += 1f)
                    {
                        Stamp(pixels, width, height, px, py - drop, 7f, post);
                        Stamp(pixels, width, height, px - 2.5f, py - drop, 2.5f, postLight);
                    }
                }
            }

            // The deck, with the thickness of its boards showing along the near edges.
            var side2 = Rgb(112, 76, 46);
            var gap = Rgb(84, 56, 34);
            var plankLight = Rgb(198, 150, 98);
            var beam = Rgb(138, 96, 58);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var wx = (x - width * 0.5f) / DockPixelsPerUnit;
                    var wy = (y - height * 0.5f) / DockPixelsPerUnit;
                    var inside = Inside(wx, wy);
                    if (inside <= 0f)
                    {
                        if (Inside(wx, wy + 0.14f) > 0f)
                        {
                            pixels[y * width + x] = side2;
                        }

                        continue;
                    }

                    BeachCell(wx, wy, out var a, out var b);
                    var along = (a + halfLength) / 0.36f;
                    var plank = Mathf.FloorToInt(along);
                    var color = Color.Lerp(wood, plankLight, Noise01(plank, 0, 81));
                    color = Color.Lerp(color, beam, TileNoise(a / 8f + 0.5f, b / 8f + 0.5f, 48, 83) * 0.35f);
                    if (along - plank < 0.08f)
                    {
                        color = gap;
                    }

                    if (halfAcross - Mathf.Abs(b) < 0.09f)
                    {
                        color = beam;
                    }

                    color.a = 1f;
                    pixels[y * width + x] = Over(pixels[y * width + x], color, Mathf.Clamp01(inside * DockPixelsPerUnit));
                }
            }

            // Two mooring posts at the far end.
            foreach (var side in new[] { -0.52f, 0.52f })
            {
                var top = new Vector2(1.72f - side, -(1.72f + side) * 0.5f);
                var px = top.x * DockPixelsPerUnit + width * 0.5f;
                var py = top.y * DockPixelsPerUnit + height * 0.5f;
                for (var rise = 0f; rise <= 24f; rise += 1f)
                {
                    Stamp(pixels, width, height, px, py + rise, 9f, post);
                }

                Stamp(pixels, width, height, px, py + 24f, 9f, postLight);
            }

            return FromPixels(width, height, pixels);
        }

        /// <summary>A little rowboat lying along the same axis as the dock.</summary>
        private static Texture2D BuildBeachBoat()
        {
            const int width = 440;
            const int height = 320;
            const float halfLength = 1.4f;
            const float halfBeam = 0.55f;
            var pixels = ClearPixels(width, height, Foam);
            var hull = Rgb(250, 248, 240);
            var stripe = Rgb(214, 74, 62);
            var rim = Rgb(214, 170, 112);
            var inner = Rgb(136, 94, 58);
            var seat = Rgb(196, 150, 98);

            float Inside(float wx, float wy, out float a, out float across)
            {
                BeachCell(wx, wy, out a, out var b);
                across = Mathf.Abs(b);
                var shape = 1f - (a * a) / (halfLength * halfLength);
                return shape <= 0f ? -1f : halfBeam * Mathf.Sqrt(shape) - across;
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var wx = (x - width * 0.5f) / DockPixelsPerUnit;
                    var wy = (y - height * 0.5f) / DockPixelsPerUnit;
                    var inside = Inside(wx, wy, out var a, out var across);
                    if (inside > 0f)
                    {
                        var isRim = inside < 0.14f;
                        var isSeat = Mathf.Abs(Mathf.Abs(a) - 0.4f) < 0.1f;
                        var color = isRim ? rim : isSeat ? seat : inner;
                        color.a = 1f;
                        pixels[y * width + x] = Over(pixels[y * width + x], color, Mathf.Clamp01(inside * DockPixelsPerUnit));
                    }
                    else if (Inside(wx, wy + 0.08f, out _, out _) > 0f)
                    {
                        pixels[y * width + x] = stripe;
                    }
                    else if (Inside(wx, wy + 0.24f, out _, out _) > 0f)
                    {
                        pixels[y * width + x] = hull;
                    }
                    else if (Inside(wx, wy + 0.34f, out _, out _) > 0f)
                    {
                        pixels[y * width + x] = new Color(0.04f, 0.2f, 0.3f, 0.26f);
                    }
                }
            }

            return FromPixels(width, height, pixels);
        }

        // ------------------------------------------------------------------ Things on the sand

        private static Texture2D BuildBeachPalm()
        {
            var width = PalmWidth;
            var height = PalmHeight;
            var pixels = ClearPixels(width, height, Rgb(90, 140, 60));

            // Soft shadow on the sand.
            for (var y = 0; y < 90; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var dx = (x - width * 0.5f - 70f) / 170f;
                    var dy = (y - PalmBaseY) / 26f;
                    var d = dx * dx + dy * dy;
                    if (d < 1f)
                    {
                        pixels[y * width + x] = new Color(0.32f, 0.22f, 0.1f, (1f - d) * 0.32f);
                    }
                }
            }

            var trunkBase = new Vector2(width * 0.5f, PalmBaseY);
            var trunkBend = new Vector2(width * 0.5f - 16f, 300f);
            var trunkTop = new Vector2(width * 0.5f + 46f, 520f);

            // Fronds behind the trunk.
            DrawFrond(pixels, trunkTop, new Vector2(-70f, 170f), new Vector2(-150f, 120f), 0.72f);
            DrawFrond(pixels, trunkTop, new Vector2(80f, 175f), new Vector2(165f, 110f), 0.72f);
            DrawFrond(pixels, trunkTop, new Vector2(5f, 210f), new Vector2(30f, 225f), 0.78f);

            var bark = Rgb(142, 98, 58);
            var barkDark = Rgb(100, 66, 38);
            var barkLight = Rgb(176, 130, 82);
            for (var i = 0; i <= 500; i++)
            {
                var t = i / 500f;
                var p = Bezier(trunkBase, trunkBend, trunkTop, t);
                var radius = Mathf.Lerp(30f, 15f, t);
                var ring = Mathf.Repeat(t * 13f, 1f);
                var color = ring < 0.16f ? barkDark : bark;
                Stamp(pixels, width, height, p.x, p.y, radius, color);
                Stamp(pixels, width, height, p.x - radius * 0.32f, p.y, radius * 0.4f, ring < 0.16f ? bark : barkLight);
            }

            // Coconuts, then the fronds in front.
            var coconut = Rgb(92, 60, 34);
            Stamp(pixels, width, height, trunkTop.x - 22f, trunkTop.y - 14f, 21f, coconut);
            Stamp(pixels, width, height, trunkTop.x + 24f, trunkTop.y - 12f, 20f, Rgb(108, 72, 40));
            Stamp(pixels, width, height, trunkTop.x + 2f, trunkTop.y - 30f, 20f, coconut);

            DrawFrond(pixels, trunkTop, new Vector2(-125f, 95f), new Vector2(-235f, -90f), 1f);
            DrawFrond(pixels, trunkTop, new Vector2(130f, 95f), new Vector2(245f, -95f), 1f);
            DrawFrond(pixels, trunkTop, new Vector2(-90f, 135f), new Vector2(-230f, 55f), 0.9f);
            DrawFrond(pixels, trunkTop, new Vector2(95f, 135f), new Vector2(238f, 50f), 0.9f);
            DrawFrond(pixels, trunkTop, new Vector2(-30f, 60f), new Vector2(-92f, -140f), 1f);
            DrawFrond(pixels, trunkTop, new Vector2(40f, 60f), new Vector2(100f, -145f), 1f);
            return FromPixels(width, height, pixels);
        }

        private static void DrawFrond(Color[] pixels, Vector2 start, Vector2 bend, Vector2 end, float light)
        {
            var leaf = Color.Lerp(Rgb(46, 112, 44), Rgb(102, 180, 66), light);
            var leafLight = Color.Lerp(leaf, Rgb(170, 216, 92), 0.45f);
            var from = start;
            var through = start + bend;
            var to = start + end;

            const int count = 26;
            for (var i = 2; i <= count; i++)
            {
                var t = (float)i / count;
                var p = Bezier(from, through, to, t);
                var ahead = Bezier(from, through, to, Mathf.Min(1f, t + 0.02f)) - Bezier(from, through, to, Mathf.Max(0f, t - 0.02f));
                var along = ahead.normalized;
                var side = new Vector2(-along.y, along.x);
                var length = Mathf.Lerp(62f, 16f, t * t) * Mathf.Sin(Mathf.Clamp01(t * 3f + 0.15f) * Mathf.PI * 0.5f);
                foreach (var sign in new[] { -1f, 1f })
                {
                    // Each leaflet sweeps towards the tip and droops a little.
                    var tip = p + (side * sign + along * 0.65f + Vector2.down * 0.25f).normalized * length;
                    for (var s = 0; s <= 24; s++)
                    {
                        var k = s / 24f;
                        var q = Vector2.Lerp(p, tip, k);
                        Stamp(pixels, PalmWidth, PalmHeight, q.x, q.y, Mathf.Lerp(7.5f, 1f, k), sign > 0f ? leafLight : leaf);
                    }
                }
            }

            var spine = Color.Lerp(leaf, Rgb(214, 214, 110), 0.55f);
            for (var i = 0; i <= 90; i++)
            {
                var t = i / 90f;
                var p = Bezier(from, through, to, t);
                Stamp(pixels, PalmWidth, PalmHeight, p.x, p.y, Mathf.Lerp(5f, 1.2f, t), spine);
            }
        }

        private static Texture2D BuildBeachRock()
        {
            const int width = 256;
            const int height = 180;
            var pixels = ClearPixels(width, height, Stone);
            var center = new Vector2(width * 0.5f, height * 0.47f);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var dx = (x - center.x) / 110f;
                    var dy = (y - center.y) / 70f;
                    var angle = Mathf.Atan2(dy, dx);
                    var wobble = 1f + 0.07f * Mathf.Sin(angle * 3f + 0.7f) + 0.04f * Mathf.Sin(angle * 7f);
                    var d = Mathf.Sqrt(dx * dx + dy * dy) / wobble;

                    // Shadow first, then the stone on top.
                    var sx = (x - center.x - 8f) / 118f;
                    var sy = (y - 36f) / 24f;
                    var shadow = sx * sx + sy * sy;
                    var color = pixels[y * width + x];
                    if (shadow < 1f)
                    {
                        color = new Color(0.3f, 0.22f, 0.1f, (1f - shadow) * 0.4f);
                    }

                    if (d < 1f && y > 26)
                    {
                        // Lit from the upper left, with blotchy mineral patches.
                        var light = Mathf.Clamp01(0.55f + dy * 0.5f - dx * 0.28f);
                        var patch = TileFractal((float)x / width, (float)y / width, 6, 61);
                        var stone = Color.Lerp(StoneDark, StoneLight, light);
                        stone = Color.Lerp(stone, StoneDark, Ramp(0.52f, 0.7f, patch) * 0.4f);
                        stone.a = Mathf.Clamp01((1f - d) * 60f);
                        color = Over(color, stone, stone.a);
                    }

                    pixels[y * width + x] = color;
                }
            }

            return FromPixels(width, height, pixels);
        }

        private static Texture2D BuildBeachStarfish()
        {
            const int size = 128;
            var pixels = ClearPixels(size, size, Rgb(232, 112, 80));
            var body = Rgb(236, 116, 84);
            var bodyLight = Rgb(250, 170, 120);
            var rim = Rgb(176, 72, 52);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - size * 0.5f) / (size * 0.46f);
                    var dy = (y - size * 0.48f) / (size * 0.46f);
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var angle = Mathf.Atan2(dy, dx) - Mathf.PI * 0.5f;
                    var arm = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(angle * 5f), 1.4f);
                    var reach = Mathf.Lerp(0.34f, 1f, arm);
                    if (r >= reach)
                    {
                        continue;
                    }

                    var depth = (reach - r) / reach;
                    var color = Color.Lerp(rim, body, Mathf.Clamp01(depth * 6f));
                    color = Color.Lerp(color, bodyLight, Mathf.Clamp01(depth - 0.35f));
                    if (Noise01(x / 7, y / 7, 71) > 0.8f && depth > 0.2f)
                    {
                        color = Color.Lerp(color, Color.white, 0.4f);
                    }

                    color.a = Mathf.Clamp01((reach - r) * 40f);
                    pixels[y * size + x] = color;
                }
            }

            return FromPixels(size, size, pixels);
        }

        private static Texture2D BuildBeachShell()
        {
            const int size = 128;
            var pixels = ClearPixels(size, size, Rgb(250, 226, 210));
            var hinge = new Vector2(size * 0.5f, size * 0.12f);
            var light = Rgb(255, 238, 224);
            var pink = Rgb(238, 170, 158);
            var rim = Rgb(170, 110, 92);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - hinge.x;
                    var dy = y - hinge.y;
                    var angle = Mathf.Atan2(dx, dy);
                    if (Mathf.Abs(angle) > 1.05f || dy < 0f)
                    {
                        continue;
                    }

                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var scallop = Mathf.Abs(Mathf.Sin(angle * 7.5f));
                    var reach = size * (0.68f + 0.05f * scallop) * Mathf.Sqrt(Mathf.Max(0f, Mathf.Cos(angle * 0.9f)));
                    if (r >= reach)
                    {
                        continue;
                    }

                    var rib = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 7.5f)), 4f);
                    var color = Color.Lerp(light, pink, r / reach * 0.8f);
                    color = Color.Lerp(color, rim, rib * 0.45f);
                    color = Color.Lerp(color, rim, Mathf.Clamp01(1f - (reach - r) / 3f));
                    color.a = Mathf.Clamp01(reach - r);
                    pixels[y * size + x] = color;
                }
            }

            return FromPixels(size, size, pixels);
        }

        // ------------------------------------------------------------------ Button icon

        /// <summary>A round view of the beach: sun, sea, foam and sand.</summary>
        private static Texture2D BuildBeachIcon()
        {
            const int size = 200;
            var pixels = ClearPixels(size, size, SeaLight);
            var radius = size * 0.47f;
            var center = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= radius)
                    {
                        continue;
                    }

                    var u = (float)x / size;
                    var v = (float)y / size;
                    var shore = 0.36f + 0.05f * Mathf.Sin(u * Mathf.PI * 2.4f + 0.6f);
                    var horizon = 0.66f;
                    Color color;
                    if (v < shore)
                    {
                        color = Color.Lerp(SandDark, SandLight, v / shore);
                    }
                    else if (v < shore + 0.045f)
                    {
                        color = Foam;
                    }
                    else if (v < horizon)
                    {
                        color = Color.Lerp(SeaLight, SeaDark, (v - shore) / (horizon - shore));
                    }
                    else
                    {
                        color = Color.Lerp(Rgb(172, 226, 246), Rgb(112, 196, 238), (v - horizon) / (1f - horizon));
                    }

                    var sx = x - size * 0.66f;
                    var sy = y - size * 0.77f;
                    if (sx * sx + sy * sy < 400f)
                    {
                        color = Rgb(255, 212, 72);
                    }

                    if (d > radius - 7f)
                    {
                        color = UiOutline;
                    }

                    color.a = Mathf.Clamp01(radius - d);
                    pixels[y * size + x] = color;
                }
            }

            return FromPixels(size, size, pixels);
        }
    }
}
