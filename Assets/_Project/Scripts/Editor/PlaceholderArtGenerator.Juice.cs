using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>The Juice Maker and its juices, in the same style as the other goods.</summary>
    public static partial class PlaceholderArtGenerator
    {
        private static readonly string[] JuiceIds =
        {
            "apple_juice", "lemonade", "peach_smoothie", "orange_juice", "mango_lassi", "coconut_smoothie"
        };

        /// <summary>Writes only the Juice Maker's art, so it can be added without regenerating everything.</summary>
        [MenuItem("Tools/Farm Game/Rebuild Juice Art", false, 23)]
        public static void GenerateJuiceArt()
        {
            EnsureFolder(ArtDirectory);
            WriteJuiceArt();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureJuiceSprites();
            AssetDatabase.Refresh();
            Debug.Log("Juice art generated.");
        }

        private static void WriteJuiceArt()
        {
            var world = BuildCottage(CottageStyle.JuiceMaker);
            WritePng("icon_juice_maker", FitToIcon(world));
            WritePng("building_juice_maker", world);

            WritePng("icon_apple_juice", BuildGoodsIcon(layer => DrawJuiceGlass(layer, Rgb(240, 186, 66), Rgb(214, 56, 48))));
            WritePng("icon_lemonade", BuildGoodsIcon(layer => DrawJuiceGlass(layer, Rgb(252, 238, 130), Rgb(250, 214, 60))));
            WritePng("icon_peach_smoothie", BuildGoodsIcon(layer => DrawJuiceGlass(layer, Rgb(255, 180, 134), Rgb(244, 130, 96))));
            WritePng("icon_orange_juice", BuildGoodsIcon(layer => DrawJuiceGlass(layer, Rgb(255, 152, 40), Rgb(244, 124, 26))));
            WritePng("icon_mango_lassi", BuildGoodsIcon(layer => DrawJuiceGlass(layer, Rgb(255, 204, 88), Rgb(240, 150, 44))));
            WritePng("icon_coconut_smoothie", BuildGoodsIcon(layer => DrawJuiceGlass(layer, Rgb(250, 246, 236), Rgb(130, 88, 52))));
        }

        private static void ConfigureJuiceSprites()
        {
            ConfigureSprite("icon_juice_maker", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite(
                "building_juice_maker",
                128f,
                SpriteAlignment.Custom,
                Vector4.zero,
                new Vector2(0.5f, 26f / SmallBuildingHeight));

            for (var i = 0; i < JuiceIds.Length; i++)
            {
                ConfigureSprite($"icon_{JuiceIds[i]}", 100f, SpriteAlignment.Center, Vector4.zero);
            }
        }

        /// <summary>A tall glass of juice with a straw and a slice of its fruit on the rim.</summary>
        private static void DrawJuiceGlass(Texture2D layer, Color juice, Color fruit)
        {
            var glass = Rgb(226, 240, 246);
            DrawLine(layer, 74f, 70f, 86f, 112f, 4f, Rgb(232, 84, 72));
            PaintBox(layer, 62f, 56f, 26f, 40f, 7f, Scale(glass, 1.05f), Scale(glass, 0.85f));
            PaintBox(layer, 62f, 50f, 21f, 29f, 5f, Scale(juice, 1.05f), Scale(juice, 0.82f));
            DrawLine(layer, 47f, 34f, 47f, 70f, 3f, new Color(1f, 1f, 1f, 0.55f));
            DrawShadedEllipse(layer, 90f, 90f, 15f, 15f, 0f, fruit, 0.4f);
            DrawShadedEllipse(layer, 90f, 90f, 10f, 10f, 0f, Scale(fruit, 1.25f), 0.2f);
        }
    }
}
