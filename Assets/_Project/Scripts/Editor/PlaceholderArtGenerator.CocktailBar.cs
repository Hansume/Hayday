using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>The Cocktail Bar on the beach and its bartender, in the same style as the other art.</summary>
    public static partial class PlaceholderArtGenerator
    {
        private const int CocktailBarWidth = 460;
        private const int CocktailBarHeight = 380;
        private const float CocktailBarBaseY = 24f;

        /// <summary>Writes only the bar's art, so it can be added without regenerating everything.</summary>
        [MenuItem("Tools/Farm Game/Rebuild Cocktail Bar Art", false, 24)]
        public static void GenerateCocktailBarArt()
        {
            EnsureFolder(ArtDirectory);
            WriteCocktailBarArt();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureCocktailBarSprites();
            AssetDatabase.Refresh();
            Debug.Log("Cocktail Bar art generated.");
        }

        private static void WriteCocktailBarArt()
        {
            WritePng("building_cocktail_bar", BuildCocktailBarWorld());
            WritePng("bar_bartender", BuildBartender());
        }

        private static void ConfigureCocktailBarSprites()
        {
            ConfigureSprite(
                "building_cocktail_bar",
                128f,
                SpriteAlignment.Custom,
                Vector4.zero,
                new Vector2(0.5f, CocktailBarBaseY / CocktailBarHeight));
            ConfigureSprite(
                "bar_bartender",
                180f,
                SpriteAlignment.Custom,
                Vector4.zero,
                new Vector2(0.5f, VisitorFeetY / VisitorHeight));
        }

        /// <summary>A round beach hut: a bamboo counter with stools and bottles under a wide thatched roof.</summary>
        private static Texture2D BuildCocktailBarWorld()
        {
            var texture = NewTexture(CocktailBarWidth, CocktailBarHeight);
            DrawEllipse(texture, 230f, 30f, 200f, 24f, Shadow, 0f);
            var layer = NewTexture(CocktailBarWidth, CocktailBarHeight);

            // Back wall of shelves, with bottles.
            PaintBox(layer, 230f, 170f, 124f, 62f, 4f, Rgb(150, 100, 58), Rgb(112, 72, 38));
            DrawLine(layer, 108f, 172f, 352f, 172f, 5f, Rgb(92, 58, 30));
            var bottles = new[] { Rgb(70, 150, 96), Rgb(214, 90, 70), Rgb(240, 190, 70), Rgb(90, 140, 210), Rgb(226, 120, 170), Rgb(120, 190, 120) };
            for (var i = 0; i < bottles.Length; i++)
            {
                var x = 136f + i * 38f;
                PaintBox(layer, x, 196f, 9f, 18f, 4f, bottles[i], Scale(bottles[i], 0.75f));
                PaintBox(layer, x, 220f, 3.5f, 8f, 1f, bottles[i], Scale(bottles[i], 0.8f));
            }

            // Bamboo posts.
            var bamboo = Rgb(216, 182, 104);
            foreach (var x in new[] { 74f, 386f })
            {
                PaintBox(layer, x, 150f, 10f, 112f, 5f, Scale(bamboo, 1.08f), Scale(bamboo, 0.8f));
                for (var y = 70f; y < 250f; y += 44f)
                {
                    DrawLine(layer, x - 10f, y, x + 10f, y, 2.4f, Scale(bamboo, 0.65f));
                }
            }

            // Thatched roof with a shaggy fringe, and a little flag on top.
            DrawTriangle(layer, new Vector2(16f, 262f), new Vector2(230f, 362f), new Vector2(230f, 262f), Rgb(226, 186, 92));
            DrawTriangle(layer, new Vector2(230f, 262f), new Vector2(230f, 362f), new Vector2(444f, 262f), Rgb(190, 148, 66));
            for (var i = 0; i < 18; i++)
            {
                var x = 26f + i * 24f;
                var colour = i % 2 == 0 ? Rgb(232, 196, 104) : Rgb(204, 162, 76);
                DrawTriangle(layer, new Vector2(x - 14f, 266f), new Vector2(x + 14f, 266f), new Vector2(x, 238f), colour);
            }

            for (var i = 1; i < 4; i++)
            {
                var t = i / 4f;
                DrawLine(layer, Mathf.Lerp(16f, 230f, t) + 8f, Mathf.Lerp(262f, 362f, t), Mathf.Lerp(444f, 230f, t) - 8f, Mathf.Lerp(262f, 362f, t), 2f, Rgb(170, 128, 56));
            }

            // Counter: a planked front under a dark top, with drinks on it.
            PaintBox(layer, 230f, 72f, 150f, 44f, 6f, Rgb(196, 138, 80), Rgb(150, 100, 56));
            for (var x = 100f; x < 370f; x += 30f)
            {
                DrawLine(layer, x, 30f, x, 114f, 2f, Rgb(130, 84, 44));
            }

            PaintBox(layer, 230f, 122f, 166f, 10f, 4f, Rgb(120, 76, 40), Rgb(88, 54, 26));
            DrawCocktail(layer, 150f, 134f, Rgb(252, 150, 60));
            DrawCocktail(layer, 300f, 134f, Rgb(120, 210, 220));

            // Stools in front of the counter.
            foreach (var x in new[] { 40f, 420f })
            {
                PaintBox(layer, x, 56f, 5f, 28f, 2f, Rgb(150, 100, 56), Rgb(110, 70, 36));
                DrawShadedEllipse(layer, x, 88f, 22f, 8f, 0f, Rgb(200, 72, 60), 0.4f);
            }

            FinishLayer(texture, layer, IconOutline);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>A stemmed glass with a coloured drink and a paper umbrella.</summary>
        private static void DrawCocktail(Texture2D layer, float x, float baseY, Color drink)
        {
            var glass = Rgb(226, 240, 246);
            DrawLine(layer, x, baseY, x, baseY + 18f, 2.4f, glass);
            DrawLine(layer, x - 10f, baseY, x + 10f, baseY, 2.4f, glass);
            DrawTriangle(layer, new Vector2(x - 18f, baseY + 40f), new Vector2(x + 18f, baseY + 40f), new Vector2(x, baseY + 16f), drink);
            DrawLine(layer, x + 6f, baseY + 38f, x + 16f, baseY + 58f, 1.8f, Rgb(150, 100, 56));
            DrawTriangle(layer, new Vector2(x + 2f, baseY + 56f), new Vector2(x + 30f, baseY + 60f), new Vector2(x + 18f, baseY + 72f), Rgb(232, 84, 110));
        }

        /// <summary>A bartender: a townsperson in a waistcoat with a red bow tie.</summary>
        private static Texture2D BuildBartender()
        {
            var style = new VisitorStyle
            {
                Skin = Rgb(226, 178, 140),
                Hair = Rgb(44, 30, 24),
                Shirt = Rgb(58, 66, 92),
                Trousers = Rgb(44, 44, 56),
                Accent = Rgb(240, 196, 80)
            };

            var texture = BuildVisitor(style, 2);
            const float cx = 96f;
            var neck = VisitorFeetY + 136f;
            var bow = Rgb(214, 56, 56);
            DrawTriangle(texture, new Vector2(cx, neck), new Vector2(cx - 16f, neck + 9f), new Vector2(cx - 16f, neck - 9f), bow);
            DrawTriangle(texture, new Vector2(cx, neck), new Vector2(cx + 16f, neck + 9f), new Vector2(cx + 16f, neck - 9f), bow);
            DrawEllipse(texture, cx, neck, 4f, 4f, Scale(bow, 0.75f), 0f);
            texture.Apply(false, false);
            return texture;
        }
    }
}
