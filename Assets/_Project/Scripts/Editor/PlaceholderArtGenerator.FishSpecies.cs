using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>
    /// The kinds of fish that can be bought into the fish pond: a swimming sprite for each
    /// (animal_&lt;id&gt;, matching the ids in the "fish" balance section). It is also the fish's
    /// icon in the pond's fish list.
    /// </summary>
    public static partial class PlaceholderArtGenerator
    {
        private sealed class FishSpecies
        {
            public string Id;
            public Color Body;
            public Color BodyDark;
            public Color Belly;
            public Color Fin;
            public Color FinDark;

            /// <summary>Body length and height relative to the basic fish.</summary>
            public float Length = 1f;
            public float Height = 1f;

            /// <summary>Colour of bars, stripe or spots.</summary>
            public Color Pattern;
            public int Bars;
            public bool Stripe;
            public bool Spots;
            public bool Scales;
            public bool Whiskers;
            public bool Snout;
            public bool Plates;
        }

        private static readonly FishSpecies[] PondSpecies =
        {
            new FishSpecies
            {
                // The starter fish, in the colours of the plain animal_fish. (Literal colours: this
                // table may be built before the colour fields in the other partial files.)
                Id = "goldfish",
                Body = Rgb(236, 142, 48), BodyDark = Rgb(184, 90, 24), Belly = Rgb(252, 210, 140),
                Fin = Rgb(214, 108, 32), FinDark = Rgb(170, 76, 22),
                Length = 0.95f, Height = 1.05f
            },
            new FishSpecies
            {
                Id = "carp",
                Body = Rgb(204, 156, 64), BodyDark = Rgb(142, 100, 36), Belly = Rgb(244, 218, 150),
                Fin = Rgb(186, 104, 52), FinDark = Rgb(136, 74, 34),
                Height = 1.12f, Scales = true, Whiskers = true
            },
            new FishSpecies
            {
                Id = "perch",
                Body = Rgb(176, 184, 72), BodyDark = Rgb(108, 116, 40), Belly = Rgb(240, 232, 172),
                Fin = Rgb(232, 112, 48), FinDark = Rgb(184, 80, 30),
                Bars = 5, Pattern = new Color(0.26f, 0.3f, 0.12f, 0.85f)
            },
            new FishSpecies
            {
                Id = "tilapia",
                Body = Rgb(150, 162, 182), BodyDark = Rgb(92, 102, 122), Belly = Rgb(222, 226, 234),
                Fin = Rgb(198, 92, 96), FinDark = Rgb(146, 66, 72),
                Height = 1.18f, Length = 0.92f, Bars = 4, Pattern = new Color(0.3f, 0.34f, 0.42f, 0.45f)
            },
            new FishSpecies
            {
                Id = "trout",
                Body = Rgb(134, 152, 98), BodyDark = Rgb(86, 100, 60), Belly = Rgb(242, 230, 208),
                Fin = Rgb(154, 144, 102), FinDark = Rgb(106, 98, 66),
                Length = 1.1f, Height = 0.9f, Stripe = true, Spots = true,
                Pattern = Rgb(230, 118, 140)
            },
            new FishSpecies
            {
                Id = "catfish",
                Body = Rgb(112, 106, 98), BodyDark = Rgb(70, 64, 58), Belly = Rgb(216, 206, 188),
                Fin = Rgb(94, 88, 82), FinDark = Rgb(60, 56, 50),
                Length = 1.12f, Height = 0.84f, Whiskers = true
            },
            new FishSpecies
            {
                Id = "bass",
                Body = Rgb(100, 144, 72), BodyDark = Rgb(58, 92, 40), Belly = Rgb(234, 236, 192),
                Fin = Rgb(86, 124, 62), FinDark = Rgb(52, 82, 36),
                Height = 1.04f, Stripe = true, Pattern = new Color(0.2f, 0.29f, 0.14f, 0.9f)
            },
            new FishSpecies
            {
                Id = "salmon",
                Body = Rgb(184, 192, 204), BodyDark = Rgb(84, 106, 132), Belly = Rgb(250, 212, 204),
                Fin = Rgb(150, 162, 180), FinDark = Rgb(98, 112, 132),
                Length = 1.12f, Spots = true, Pattern = new Color(0.2f, 0.24f, 0.3f, 0.85f)
            },
            new FishSpecies
            {
                Id = "pike",
                Body = Rgb(94, 132, 66), BodyDark = Rgb(54, 82, 38), Belly = Rgb(228, 222, 172),
                Fin = Rgb(156, 124, 62), FinDark = Rgb(112, 88, 40),
                Length = 1.32f, Height = 0.72f, Spots = true, Snout = true,
                Pattern = new Color(0.86f, 0.86f, 0.6f, 0.9f)
            },
            new FishSpecies
            {
                Id = "sturgeon",
                Body = Rgb(122, 130, 134), BodyDark = Rgb(76, 82, 88), Belly = Rgb(216, 216, 208),
                Fin = Rgb(102, 108, 114), FinDark = Rgb(70, 74, 80),
                Length = 1.36f, Height = 0.7f, Snout = true, Plates = true, Whiskers = true
            }
        };

        private static void WriteFishSpeciesArt()
        {
            foreach (var species in PondSpecies)
            {
                WritePng($"animal_{species.Id}", BuildSpeciesAnimal(species));
            }
        }

        private static void ConfigureFishSpeciesSprites()
        {
            foreach (var species in PondSpecies)
            {
                ConfigureSprite($"animal_{species.Id}", 100f, SpriteAlignment.Center, Vector4.zero);
            }
        }

        /// <summary>The swimming fish, facing right like animal_fish, fitted to the sprite.</summary>
        private static Texture2D BuildSpeciesAnimal(FishSpecies species)
        {
            var texture = NewTexture(AnimalSize, AnimalSize);
            GetFishSpan(species, out var left, out var right);
            var scale = Mathf.Min(1f, (AnimalSize - 24f) / (right - left));
            var centerX = AnimalSize * 0.5f - (left + right) * 0.5f * scale;
            DrawFish(texture, species, new Vector2(centerX, AnimalSize * 0.5f), scale, false, 0f);
            texture.Apply(false, false);
            return texture;
        }

        // ------------------------------------------------------------------ The fish itself

        /// <summary>How far the fish reaches left (tail) and right (head) of its body centre.</summary>
        private static void GetFishSpan(FishSpecies species, out float left, out float right)
        {
            var rx = 70f * species.Length;
            left = -rx - 48f;
            right = rx + (species.Snout ? 28f : 6f) + (species.Whiskers ? 18f : 0f);
        }

        /// <summary>
        /// Draws the fish centred on its body at <paramref name="center"/>, facing right (or left
        /// when flipped), tilted by <paramref name="angle"/> degrees. Shapes follow animal_fish.
        /// </summary>
        private static void DrawFish(Texture2D texture, FishSpecies species, Vector2 center, float scale, bool flip, float angle)
        {
            var radians = angle * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);

            Vector2 P(float x, float y)
            {
                if (flip)
                {
                    x = -x;
                }

                return new Vector2(center.x + (x * cos - y * sin) * scale, center.y + (x * sin + y * cos) * scale);
            }

            void Ellipse(float x, float y, float radiusX, float radiusY, Color color)
            {
                var p = P(x, y);
                DrawEllipse(texture, p.x, p.y, radiusX * scale, radiusY * scale, color, angle);
            }

            void Line(float x0, float y0, float x1, float y1, float thickness, Color color)
            {
                var a = P(x0, y0);
                var b = P(x1, y1);
                DrawLine(texture, a.x, a.y, b.x, b.y, Mathf.Max(1f, thickness * scale), color);
            }

            void Triangle(float ax, float ay, float bx, float by, float cx, float cy, Color color)
            {
                DrawTriangle(texture, P(ax, ay), P(bx, by), P(cx, cy), color);
            }

            var rx = 70f * species.Length;
            var ry = 38f * species.Height;
            var tail = Mathf.Lerp(1f, species.Height, 0.5f);
            var back = -rx;

            // Tail.
            Triangle(back + 4f, -2f, back - 46f, -44f * tail, back - 46f, 40f * tail, species.FinDark);
            Triangle(back + 14f, -2f, back - 32f, -26f * tail, back - 32f, 22f * tail, species.Fin);

            // Body.
            Ellipse(-4f, -2f, rx + 8f, ry + 8f, species.BodyDark);
            Ellipse(0f, 0f, rx, ry, species.Body);
            Ellipse(6f, -ry * 0.37f, rx * 0.77f, ry * 0.5f, species.Belly);

            bool Inside(float x, float y, float limit)
            {
                return (x / rx) * (x / rx) + (y / ry) * (y / ry) < limit;
            }

            if (species.Scales)
            {
                var edge = new Color(species.BodyDark.r, species.BodyDark.g, species.BodyDark.b, 0.45f);
                for (var y = -ry * 0.55f; y <= ry * 0.6f; y += 12f)
                {
                    var row = Mathf.RoundToInt((y + ry) / 12f);
                    for (var x = -rx * 0.7f + (row % 2) * 7f; x <= rx * 0.45f; x += 14f)
                    {
                        if (!Inside(x, y, 0.6f))
                        {
                            continue;
                        }

                        Ellipse(x, y, 7f, 6f, edge);
                        Ellipse(x - 1.6f, y + 0.6f, 5.4f, 4.6f, y < -ry * 0.2f ? species.Belly : species.Body);
                    }
                }
            }

            if (species.Bars > 0)
            {
                for (var i = 0; i < species.Bars; i++)
                {
                    var t = species.Bars == 1 ? 0.5f : i / (float)(species.Bars - 1);
                    var x = Mathf.Lerp(-rx * 0.58f, rx * 0.42f, t);
                    var reach = ry * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / rx) * (x / rx)));
                    Line(x, reach * 0.88f, x - 5f, -reach * 0.45f, 8f, species.Pattern);
                }
            }

            if (species.Stripe)
            {
                Line(-rx * 0.82f, 0f, rx * 0.62f, 3f, 8f, species.Pattern);
            }

            if (species.Spots)
            {
                for (var i = 0; i < 16; i++)
                {
                    var x = (Noise01(i, 1, 37) * 2f - 1f) * rx * 0.75f;
                    var y = (Noise01(i, 5, 61) * 0.9f - 0.15f) * ry;
                    if (Inside(x, y, 0.62f))
                    {
                        Ellipse(x, y, 3.6f, 3.2f, species.Spots && species.Stripe ? species.BodyDark : species.Pattern);
                    }
                }
            }

            if (species.Plates)
            {
                for (var x = -rx * 0.75f; x <= rx * 0.55f; x += 17f)
                {
                    var y = ry * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / rx) * (x / rx))) * 0.7f;
                    Ellipse(x, y, 6f, 4.6f, species.BodyDark);
                    Ellipse(x - 0.6f, y + 0.8f, 4f, 3f, species.Belly);
                    Ellipse(x, -ry * 0.05f, 4.6f, 3.4f, species.BodyDark);
                    Ellipse(x - 0.5f, -ry * 0.05f + 0.6f, 3f, 2.2f, species.Belly);
                }
            }

            // Back and belly fins.
            Triangle(-26f * species.Length, ry * 0.74f, 6f * species.Length, ry + 34f * species.Height, 30f * species.Length, ry * 0.68f, species.Fin);
            Triangle(-22f * species.Length, -ry * 0.68f, 8f * species.Length, -ry - 20f * species.Height, 28f * species.Length, -ry * 0.74f, species.FinDark);

            // Head: snout, gill, eye, mouth and whiskers.
            var mouthX = rx;
            var mouthY = -ry * 0.3f;
            if (species.Snout)
            {
                Triangle(rx - 18f, ry * 0.3f, rx - 18f, -ry * 0.5f, rx + 28f, -ry * 0.18f, species.BodyDark);
                Triangle(rx - 20f, ry * 0.22f, rx - 20f, -ry * 0.42f, rx + 22f, -ry * 0.18f, species.Body);
                mouthX = rx + 6f;
                mouthY = -ry * 0.32f;
            }

            Line(rx - 32f, ry * 0.42f, rx - 42f, -ry * 0.58f, 4f, species.BodyDark);
            Ellipse(rx - 22f, ry * 0.3f, 11f, 11f, Color.white);
            Ellipse(rx - 19f, ry * 0.3f, 5.5f, 5.5f, FishEye);
            Line(mouthX - 8f, mouthY, mouthX + 2f, mouthY, 2.6f, species.BodyDark);

            if (species.Whiskers)
            {
                Line(mouthX - 2f, mouthY, mouthX + 18f, mouthY - 18f, 3f, species.BodyDark);
                Line(mouthX - 4f, mouthY - 2f, mouthX + 8f, mouthY - 24f, 2.6f, species.BodyDark);
                Line(mouthX - 4f, mouthY + 1f, mouthX + 16f, mouthY + 6f, 2.4f, species.BodyDark);
            }
        }
    }
}
