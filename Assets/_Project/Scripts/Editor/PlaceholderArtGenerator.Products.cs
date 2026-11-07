using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>Placeholder icons for animal products: egg and milk.</summary>
    public static partial class PlaceholderArtGenerator
    {
        private static void WriteProductArt()
        {
            WritePng("icon_egg", BuildEggIcon());
            WritePng("icon_milk", BuildMilkIcon());
        }

        private static void ConfigureProductSprites()
        {
            ConfigureSprite("icon_egg", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_milk", 100f, SpriteAlignment.Center, Vector4.zero);
        }

        // Two eggs in a straw nest.
        private static Texture2D BuildEggIcon()
        {
            var texture = NewTexture(IconSize, IconSize);
            DrawEllipse(texture, 64f, 26f, 46f, 10f, Shadow, 0f);
            DrawEllipse(texture, 64f, 40f, 48f, 18f, Rgb(150, 100, 46), 0f);

            DrawEllipse(texture, 50f, 66f, 20f, 26f, Rgb(214, 200, 176), -10f);
            DrawEllipse(texture, 49f, 67f, 18f, 24f, Rgb(250, 244, 230), -10f);
            DrawEllipse(texture, 43f, 76f, 5f, 8f, new Color(1f, 1f, 1f, 0.8f), -10f);
            DrawEllipse(texture, 80f, 60f, 19f, 24f, Rgb(204, 160, 112), 12f);
            DrawEllipse(texture, 79f, 61f, 17f, 22f, Rgb(232, 190, 140), 12f);
            DrawEllipse(texture, 74f, 70f, 4f, 7f, new Color(1f, 1f, 1f, 0.6f), 12f);

            DrawEllipse(texture, 64f, 38f, 50f, 13f, Rgb(196, 146, 70), 0f);
            for (var i = 0; i < 9; i++)
            {
                var x = 20f + i * 11f;
                DrawLine(texture, x, 30f, x + 12f, 46f, 2.5f, Hay);
                DrawLine(texture, x + 10f, 28f, x - 2f, 44f, 2f, Rgb(170, 120, 52));
            }

            texture.Apply(false, false);
            return texture;
        }

        // A glass milk bottle with a red cap and a blue label.
        private static Texture2D BuildMilkIcon()
        {
            var texture = NewTexture(IconSize, IconSize);
            DrawEllipse(texture, 64f, 16f, 30f, 7f, Shadow, 0f);
            PaintBox(texture, 64f, 52f, 29f, 36f, 14f, Rgb(170, 186, 196), Rgb(170, 186, 196));
            PaintBox(texture, 64f, 52f, 26f, 33f, 12f, Rgb(255, 255, 255), Rgb(236, 240, 244));
            PaintBox(texture, 64f, 94f, 15f, 12f, 5f, Rgb(170, 186, 196), Rgb(170, 186, 196));
            PaintBox(texture, 64f, 94f, 12f, 10f, 4f, Rgb(255, 255, 255), Rgb(242, 244, 246));
            PaintBox(texture, 64f, 108f, 16f, 6f, 3f, Rgb(214, 66, 50), Rgb(176, 44, 34));
            PaintBox(texture, 64f, 50f, 26f, 13f, 3f, Rgb(84, 150, 214), Rgb(60, 120, 186));
            DrawEllipse(texture, 64f, 50f, 8f, 6f, Color.white, 0f);
            DrawLine(texture, 46f, 30f, 46f, 74f, 4f, new Color(1f, 1f, 1f, 0.7f));
            texture.Apply(false, false);
            return texture;
        }
    }
}
