using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>The Land Deed, painted in code: a parchment with a green hill and a red seal.</summary>
    public static partial class PlaceholderArtGenerator
    {
        private const int DeedIconSize = 200;

        /// <summary>Writes only the land art, so it can be added without regenerating everything.</summary>
        [MenuItem("Tools/Farm Game/Rebuild Land Art", false, 22)]
        public static void GenerateLandArt()
        {
            EnsureFolder(ArtDirectory);
            WriteLandArt();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureLandSprites();
            AssetDatabase.Refresh();
            Debug.Log("Land art generated.");
        }

        private static void WriteLandArt()
        {
            WritePng("icon_land_deed", BuildLandDeedIcon());
        }

        private static void ConfigureLandSprites()
        {
            ConfigureSprite("icon_land_deed", 100f, SpriteAlignment.Center, Vector4.zero);
        }

        private static Texture2D BuildLandDeedIcon()
        {
            var parchment = Rgb(246, 228, 180);
            var parchmentDark = Rgb(214, 182, 122);
            var outline = Rgb(112, 74, 40);
            var sky = Rgb(186, 226, 240);
            var hill = Rgb(112, 180, 72);
            var hillDark = Rgb(78, 142, 56);
            var seal = Rgb(198, 52, 44);
            var sealLight = Rgb(232, 96, 80);

            var size = DeedIconSize;
            var pixels = ClearPixels(size, size, parchment);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size;
                    var v = (y + 0.5f) / size;

                    // The sheet, with rolled ends at the top and bottom.
                    var inSheet = u > 0.16f && u < 0.84f && v > 0.08f && v < 0.92f;
                    var inRoll = u > 0.1f && u < 0.9f && (Mathf.Abs(v - 0.1f) < 0.055f || Mathf.Abs(v - 0.9f) < 0.055f);
                    if (!inSheet && !inRoll)
                    {
                        continue;
                    }

                    var edge = Mathf.Min(
                        inRoll ? Mathf.Min(u - 0.1f, 0.9f - u) : Mathf.Min(u - 0.16f, 0.84f - u),
                        inRoll ? 0.055f - Mathf.Min(Mathf.Abs(v - 0.1f), Mathf.Abs(v - 0.9f)) : 1f);

                    Color color;
                    if (edge < 0.016f)
                    {
                        color = outline;
                    }
                    else if (inRoll)
                    {
                        color = parchmentDark;
                    }
                    else
                    {
                        color = parchment;

                        // A little picture of land on the sheet.
                        if (u > 0.25f && u < 0.75f && v > 0.42f && v < 0.8f)
                        {
                            var ground = 0.5f + 0.09f * Mathf.Sin((u - 0.25f) * Mathf.PI * 2f);
                            color = v < ground ? Color.Lerp(hillDark, hill, (v - 0.42f) / 0.16f) : sky;
                        }
                    }

                    // The wax seal.
                    var dx = u - 0.66f;
                    var dy = v - 0.26f;
                    var seal2 = dx * dx + dy * dy;
                    if (seal2 < 0.0121f)
                    {
                        color = seal2 < 0.0049f ? sealLight : seal;
                    }

                    pixels[y * size + x] = color;
                }
            }

            return FromPixels(size, size, pixels);
        }
    }
}
