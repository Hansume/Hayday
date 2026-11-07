using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    public static partial class PlaceholderArtGenerator
    {
        public const string ArtDirectory = "Assets/_Project/Resources/Art/Placeholders";

        private const int GroundWidth = 256;
        private const int GroundHeight = 128;
        private const int CropWidth = 128;
        private const int CropHeight = 160;
        private const int IconSize = 128;
        private const int SmallBuildingWidth = 320;
        private const int SmallBuildingHeight = 256;
        private const int LargeBuildingWidth = 512;
        private const int LargeBuildingHeight = 384;
        private const int AnimalSize = 256;
        private const int PenWidth = 1024;
        private const int PenHeight = 512;

        private static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);
        private static readonly Color GrassBottom = Rgb(82, 150, 62);
        private static readonly Color GrassTop = Rgb(126, 197, 88);
        private static readonly Color GrassEdge = Rgb(55, 110, 42);
        private static readonly Color GrassBlade = Rgb(148, 207, 108);
        private static readonly Color SoilBottom = Rgb(99, 64, 37);
        private static readonly Color SoilTop = Rgb(141, 95, 55);
        private static readonly Color SoilEdge = Rgb(72, 45, 26);
        private static readonly Color SoilFurrow = Rgb(86, 55, 31);
        private static readonly Color SoilPebble = Rgb(160, 118, 79);
        private static readonly Color Shadow = new Color(0.12f, 0.08f, 0.02f, 0.22f);
        private static readonly Color MoundDark = Rgb(95, 59, 32);
        private static readonly Color MoundLight = Rgb(134, 88, 49);
        private static readonly Color WaterRim = Rgb(118, 106, 75);
        private static readonly Color WaterDeep = Rgb(49, 112, 151);
        private static readonly Color Water = Rgb(76, 162, 205);
        private static readonly Color WaterLight = Rgb(126, 207, 232);
        private static readonly Color BarnRed = Rgb(174, 66, 48);
        private static readonly Color BarnRedDark = Rgb(116, 40, 31);
        private static readonly Color BarnRoof = Rgb(87, 55, 38);
        private static readonly Color BarnRoofLight = Rgb(125, 82, 48);
        private static readonly Color BarnTrim = Rgb(238, 226, 198);
        private static readonly Color BarnDoor = Rgb(72, 40, 24);
        private static readonly Color Hay = Rgb(226, 190, 82);
        private static readonly Color Wood = Rgb(134, 92, 48);
        private static readonly Color WoodDark = Rgb(87, 56, 28);
        private static readonly Color CoopYellow = Rgb(214, 154, 62);
        private static readonly Color CoopDark = Rgb(150, 92, 36);
        private static readonly Color Stone = Rgb(146, 146, 136);
        private static readonly Color StoneLight = Rgb(181, 178, 162);
        private static readonly Color StoneDark = Rgb(93, 96, 92);
        private static readonly Color Metal = Rgb(116, 127, 131);
        private static readonly Color FeedGreen = Rgb(118, 164, 70);
        private static readonly Color FeedSack = Rgb(196, 155, 92);
        private static readonly Color FishBody = Rgb(236, 142, 48);
        private static readonly Color FishBodyDark = Rgb(184, 90, 24);
        private static readonly Color FishBelly = Rgb(252, 210, 140);
        private static readonly Color FishFin = Rgb(214, 108, 32);
        private static readonly Color FishFinDark = Rgb(170, 76, 22);
        private static readonly Color FishEye = Rgb(52, 40, 30);
        private static readonly Color CowWhite = Rgb(244, 239, 228);
        private static readonly Color CowOutline = Rgb(96, 78, 64);
        private static readonly Color CowBlack = Rgb(62, 54, 48);
        private static readonly Color CowPink = Rgb(226, 158, 150);
        private static readonly Color CowHorn = Rgb(222, 200, 150);
        private static readonly Color PenWaterTop = Rgb(70, 160, 196);
        private static readonly Color PenWaterBottom = Rgb(22, 82, 132);
        private static readonly Color PenSand = Rgb(194, 170, 116);
        private static readonly Color PenSandLight = Rgb(220, 196, 142);
        private static readonly Color PenSeaweed = Rgb(54, 128, 74);
        private static readonly Color PenSeaweedLight = Rgb(90, 162, 96);
        private static readonly Color PenBubble = new Color(0.82f, 0.96f, 1f, 0.3f);
        private static readonly Color CoopWall = Rgb(224, 174, 94);
        private static readonly Color CoopWallDark = Rgb(182, 128, 58);
        private static readonly Color StableWall = Rgb(150, 54, 42);
        private static readonly Color StableWallDark = Rgb(108, 36, 28);
        private static readonly Color StableFloor = Rgb(120, 86, 50);
        private static readonly Color TroughFeed = Rgb(146, 168, 74);

        public static void GenerateAll()
        {
            EnsureFolder(ArtDirectory);

            WritePng("grass", BuildGrass());
            WritePng("soil", BuildSoil());

            var cropIds = new[]
            {
                "wheat", "corn", "carrot", "tomato", "potato", "strawberry", "soybean", "chili", "pumpkin"
            };
            for (var cropIndex = 0; cropIndex < cropIds.Length; cropIndex++)
            {
                for (var stage = 0; stage < 4; stage++)
                {
                    WritePng($"crop_{cropIds[cropIndex]}_{stage}", BuildCropStage(cropIds[cropIndex], stage));
                }
            }

            WritePng("icon_wheat", BuildWheatIcon());
            WritePng("icon_corn", BuildCornIcon());
            WritePng("icon_carrot", BuildCarrotIcon());
            WritePng("icon_coin", BuildCoinIcon());
            WritePng("icon_scythe", BuildScytheIcon());
            WritePng("building_fish_pond", BuildFishPondWorld());
            WritePng("building_cow_stable", BuildCowStableWorld());
            WritePng("building_coop", BuildCoopWorld());
            WritePng("building_feed_grinder", BuildFeedGrinderWorld());
            WritePng("icon_fish_pond", BuildFishPondIcon());
            WritePng("icon_cow_stable", BuildCowStableIcon());
            WritePng("icon_coop", BuildCoopIcon());
            WritePng("icon_feed_grinder", BuildFeedGrinderIcon());
            WritePng("icon_feed", BuildFeedIcon());
            WritePng("animal_fish", BuildFishAnimal());
            WritePng("animal_chicken", BuildChickenAnimal());
            WritePng("animal_cow", BuildCowAnimal());
            WritePng("pen_underwater", BuildFishPen());
            WritePng("pen_coop", BuildCoopPen());
            WritePng("pen_stable", BuildStablePen());
            WritePng("placement_tile", BuildPlacementTile());
            WritePng("ui_rounded", BuildRoundedRect(64, 14));
            WritePng("ui_circle", BuildCircle(64, 30));
            WritePng("ui_square", BuildSolid(8, 8));
            WriteUiKitArt();
            WriteProductArt();
            WriteGoodsArt();
            WriteFruitTreeArt();
            WriteExtraCropAndGoatArt();
            WriteVisitorArt();
            WriteFishSpeciesArt();
            WriteBeachArt();
            WriteLandArt();
            WriteJuiceArt();
            WriteCocktailBarArt();

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ConfigureSprite("grass", 128f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("soil", 128f, SpriteAlignment.Center, Vector4.zero);

            for (var cropIndex = 0; cropIndex < cropIds.Length; cropIndex++)
            {
                for (var stage = 0; stage < 4; stage++)
                {
                    ConfigureSprite(
                        $"crop_{cropIds[cropIndex]}_{stage}",
                        100f,
                        SpriteAlignment.Custom,
                        Vector4.zero,
                        new Vector2(0.5f, 29f / CropHeight));
                }
            }

            ConfigureSprite("icon_wheat", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_corn", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_carrot", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_coin", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_scythe", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite(
                "building_fish_pond",
                128f,
                SpriteAlignment.Custom,
                Vector4.zero,
                new Vector2(0.5f, 68f / SmallBuildingHeight));
            ConfigureSprite(
                "building_cow_stable",
                128f,
                SpriteAlignment.Custom,
                Vector4.zero,
                new Vector2(0.5f, 28f / LargeBuildingHeight));
            ConfigureSprite(
                "building_coop",
                128f,
                SpriteAlignment.Custom,
                Vector4.zero,
                new Vector2(0.5f, 26f / SmallBuildingHeight));
            ConfigureSprite(
                "building_feed_grinder",
                128f,
                SpriteAlignment.Custom,
                Vector4.zero,
                new Vector2(0.5f, 28f / SmallBuildingHeight));
            ConfigureSprite("icon_fish_pond", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_cow_stable", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_coop", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_feed_grinder", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("icon_feed", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("animal_fish", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("animal_chicken", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("animal_cow", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("pen_underwater", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("pen_coop", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("pen_stable", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("placement_tile", 128f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("ui_rounded", 100f, SpriteAlignment.Center, new Vector4(20f, 20f, 20f, 20f));
            ConfigureSprite("ui_circle", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureSprite("ui_square", 100f, SpriteAlignment.Center, Vector4.zero);
            ConfigureUiKitSprites();
            ConfigureProductSprites();
            ConfigureGoodsSprites();
            ConfigureFruitTreeSprites();
            ConfigureExtraCropAndGoatSprites();
            ConfigureVisitorSprites();
            ConfigureFishSpeciesSprites();
            ConfigureBeachSprites();
            ConfigureLandSprites();
            ConfigureJuiceSprites();
            ConfigureCocktailBarSprites();

            AssetDatabase.Refresh();
            Debug.Log("Farm Game placeholder art generated.");
        }

        private static Texture2D BuildGrass()
        {
            var texture = NewTexture(GroundWidth, GroundHeight);
            var centerX = GroundWidth * 0.5f;
            var centerY = GroundHeight * 0.5f;
            var halfWidth = GroundWidth * 0.5f;
            var halfHeight = GroundHeight * 0.5f;

            for (var y = 0; y < GroundHeight; y++)
            {
                for (var x = 0; x < GroundWidth; x++)
                {
                    var px = x + 0.5f;
                    var py = y + 0.5f;
                    var diamond = Mathf.Abs(px - centerX) / halfWidth +
                                  Mathf.Abs(py - centerY) / halfHeight;
                    if (diamond > 1.02f)
                    {
                        continue;
                    }

                    var edge = Mathf.Clamp01((1.01f - diamond) * 70f + 0.5f);
                    var t = Mathf.Clamp01(py / GroundHeight);
                    var color = Color.Lerp(GrassBottom, GrassTop, 0.25f + t * 0.7f);
                    var noise = (Noise01(x / 2, y / 2, 17) - 0.5f) * 0.07f;
                    color += new Color(noise, noise * 0.8f, noise * 0.4f, 0f);

                    if (diamond > 0.955f)
                    {
                        color = Color.Lerp(color, GrassEdge, 0.65f);
                    }

                    BlendPixel(texture, x, y, color, edge);
                }
            }

            for (var i = 0; i < 90; i++)
            {
                var px = 20 + Mathf.FloorToInt(Noise01(i, 4, 33) * (GroundWidth - 40));
                var py = 14 + Mathf.FloorToInt(Noise01(i, 9, 71) * (GroundHeight - 28));
                var diamond = Mathf.Abs(px - centerX) / halfWidth +
                              Mathf.Abs(py - centerY) / halfHeight;
                if (diamond > 0.8f)
                {
                    continue;
                }

                var bladeColor = Noise01(i, 2, 51) > 0.5f ? GrassBlade : GrassEdge;
                var height = 2 + Mathf.FloorToInt(Noise01(i, 6, 19) * 4f);
                BlendPixel(texture, px, py, bladeColor, 0.55f);
                BlendPixel(texture, px, py + 1, bladeColor, 0.4f);
                if (height > 2)
                {
                    BlendPixel(texture, px + 1, py + 2, bladeColor, 0.3f);
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildCoinIcon()
        {
            var texture = NewTexture(IconSize, IconSize);

            var rim = Rgb(178, 120, 21);
            var gold = Rgb(238, 186, 46);
            var light = Rgb(255, 222, 105);

            DrawEllipse(texture, 64f, 64f, 48f, 48f, rim, 0f);
            DrawEllipse(texture, 64f, 64f, 41f, 41f, gold, 0f);
            DrawEllipse(texture, 64f, 64f, 32f, 32f, Rgb(214, 155, 31), 0f);
            DrawEllipse(texture, 64f, 64f, 28f, 28f, gold, 0f);
            DrawEllipse(texture, 51f, 82f, 13f, 8f, new Color(1f, 0.95f, 0.7f, 0.6f), -30f);
            DrawEllipse(texture, 48f, 84f, 6f, 3.5f, new Color(1f, 1f, 0.9f, 0.75f), -30f);
            DrawEllipse(texture, 64f, 64f, 4f, 4f, light, 0f);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildScytheIcon()
        {
            var texture = NewTexture(IconSize, IconSize);

            var handleDark = Rgb(71, 44, 20);
            var handle = Rgb(128, 82, 38);
            var handleLight = Rgb(178, 124, 64);
            var metalDark = Rgb(84, 91, 94);
            var metal = Rgb(166, 177, 181);
            var metalLight = Rgb(224, 232, 235);
            var rivet = Rgb(238, 186, 46);

            DrawEllipse(texture, 62f, 18f, 32f, 7f, Shadow, 0f);

            DrawLine(texture, 31f, 20f, 80f, 104f, 11f, handleDark);
            DrawLine(texture, 32f, 22f, 79f, 102f, 7.5f, handle);
            DrawLine(texture, 33f, 26f, 70f, 91f, 2.2f, handleLight);

            DrawLine(texture, 27f, 35f, 40f, 30f, 3.4f, handleDark);
            DrawLine(texture, 36f, 49f, 49f, 44f, 3.2f, handleDark);

            const int bladeSteps = 24;
            for (var pass = 0; pass < 3; pass++)
            {
                var radius = pass == 0 ? 8f : pass == 1 ? 5.8f : 1.9f;
                var color = pass == 0 ? metalDark : pass == 1 ? metal : metalLight;
                for (var i = 0; i <= bladeSteps; i++)
                {
                    var t = i / (float)bladeSteps;
                    var oneMinusT = 1f - t;
                    var x = oneMinusT * oneMinusT * 82f +
                            2f * oneMinusT * t * 44f +
                            t * t * 13f;
                    var y = oneMinusT * oneMinusT * 103f +
                            2f * oneMinusT * t * 124f +
                            t * t * 89f;
                    if (pass == 2)
                    {
                        y += 3.6f;
                    }

                    DrawEllipse(texture, x, y, radius, radius, color, 0f);
                }
            }

            DrawEllipse(texture, 78f, 100f, 6f, 6f, metalDark, 0f);
            DrawEllipse(texture, 78f, 100f, 3.4f, 3.4f, rivet, 0f);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildFishPondWorld()
        {
            var texture = NewTexture(SmallBuildingWidth, SmallBuildingHeight);

            DrawEllipse(texture, 160f, 38f, 136f, 26f, Shadow, 0f);
            DrawEllipse(texture, 160f, 68f, 132f, 56f, WaterRim, 0f);
            DrawEllipse(texture, 160f, 74f, 122f, 48f, WaterDeep, 0f);
            DrawEllipse(texture, 160f, 78f, 112f, 41f, Water, 0f);
            DrawEllipse(
                texture,
                146f,
                84f,
                70f,
                22f,
                new Color(WaterLight.r, WaterLight.g, WaterLight.b, 0.35f),
                0f);

            var ripple = new Color(WaterLight.r, WaterLight.g, WaterLight.b, 0.65f);
            DrawEllipse(texture, 126f, 88f, 43f, 8f, ripple, 0f);
            DrawEllipse(texture, 126f, 88f, 29f, 4.5f, ripple, 0f);
            DrawEllipse(texture, 196f, 61f, 35f, 6f, ripple, 0f);
            DrawEllipse(texture, 190f, 58f, 21f, 3.4f, ripple, 0f);
            DrawEllipse(texture, 82f, 55f, 24f, 5f, ripple, 0f);

            FillRect(texture, 224, 62, 306, 88, WoodDark);
            FillRect(texture, 226, 64, 304, 84, Wood);
            for (var x = 236; x <= 300; x += 16)
            {
                DrawLine(texture, x, 65f, x - 5f, 83f, 1.3f, WoodDark);
            }

            DrawLine(texture, 224f, 86f, 306f, 86f, 2.2f, WoodDark);
            FillRect(texture, 230, 56, 238, 102, WoodDark);
            FillRect(texture, 292, 56, 300, 102, WoodDark);

            DrawLine(texture, 63f, 82f, 50f, 142f, 2.6f, GrassEdge);
            DrawLine(texture, 72f, 84f, 78f, 137f, 2.4f, GrassEdge);
            DrawLine(texture, 87f, 80f, 102f, 130f, 2.3f, GrassEdge);
            DrawEllipse(texture, 49f, 145f, 4f, 10f, GrassBlade, -12f);
            DrawEllipse(texture, 79f, 140f, 4f, 9f, GrassBlade, 12f);
            DrawEllipse(texture, 103f, 133f, 3.5f, 8f, GrassBlade, 18f);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildCowStableWorld()
        {
            var texture = NewTexture(LargeBuildingWidth, LargeBuildingHeight);

            DrawEllipse(texture, 256f, 34f, 198f, 28f, Shadow, 0f);
            FillRect(texture, 132, 28, 380, 190, BarnRed);
            FillRect(texture, 132, 28, 158, 190, BarnRedDark);
            FillRect(texture, 356, 28, 380, 190, BarnRedDark);
            FillRect(texture, 158, 28, 356, 36, BarnRedDark);

            DrawTriangle(
                texture,
                new Vector2(108f, 180f),
                new Vector2(256f, 324f),
                new Vector2(404f, 180f),
                BarnRoof);
            DrawTriangle(
                texture,
                new Vector2(138f, 190f),
                new Vector2(256f, 300f),
                new Vector2(374f, 190f),
                BarnRoofLight);

            DrawLine(texture, 108f, 180f, 256f, 324f, 4.5f, BarnTrim);
            DrawLine(texture, 256f, 324f, 404f, 180f, 4.5f, BarnTrim);
            DrawLine(texture, 102f, 180f, 410f, 180f, 4.5f, BarnTrim);
            DrawLine(texture, 132f, 188f, 380f, 188f, 5f, BarnTrim);

            FillRect(texture, 232, 28, 280, 134, BarnDoor);
            DrawLine(texture, 232f, 134f, 280f, 134f, 3f, BarnTrim);
            DrawLine(texture, 232f, 28f, 280f, 134f, 3f, BarnTrim);
            DrawLine(texture, 280f, 28f, 232f, 134f, 3f, BarnTrim);
            DrawLine(texture, 256f, 28f, 256f, 134f, 1.6f, BarnTrim);

            FillRect(texture, 172, 124, 214, 166, BarnTrim);
            FillRect(texture, 178, 130, 208, 160, BarnDoor);
            DrawLine(texture, 193f, 130f, 193f, 160f, 2f, BarnTrim);
            DrawLine(texture, 178f, 145f, 208f, 145f, 2f, BarnTrim);

            FillRect(texture, 298, 124, 340, 166, BarnTrim);
            FillRect(texture, 304, 130, 334, 160, BarnDoor);
            DrawLine(texture, 319f, 130f, 319f, 160f, 2f, BarnTrim);
            DrawLine(texture, 304f, 145f, 334f, 145f, 2f, BarnTrim);

            FillRect(texture, 150, 28, 196, 68, Hay);
            FillRect(texture, 316, 28, 362, 68, Hay);
            for (var y = 36; y <= 60; y += 8)
            {
                DrawLine(texture, 152f, y, 194f, y + 2f, 1.2f, WoodDark);
                DrawLine(texture, 318f, y, 360f, y + 2f, 1.2f, WoodDark);
            }

            DrawLine(texture, 150f, 68f, 196f, 68f, 3f, BarnTrim);
            DrawLine(texture, 316f, 68f, 362f, 68f, 3f, BarnTrim);
            DrawLine(texture, 158f, 38f, 158f, 190f, 2f, BarnRedDark);
            DrawLine(texture, 356f, 38f, 356f, 190f, 2f, BarnRedDark);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildCoopWorld()
        {
            var texture = NewTexture(SmallBuildingWidth, SmallBuildingHeight);

            DrawEllipse(texture, 160f, 32f, 104f, 20f, Shadow, 0f);
            FillRect(texture, 104, 26, 216, 132, CoopYellow);
            FillRect(texture, 104, 26, 124, 132, CoopDark);
            FillRect(texture, 196, 26, 216, 132, CoopDark);
            FillRect(texture, 124, 26, 196, 34, CoopDark);

            DrawTriangle(
                texture,
                new Vector2(82f, 126f),
                new Vector2(160f, 216f),
                new Vector2(238f, 126f),
                BarnRoof);
            DrawTriangle(
                texture,
                new Vector2(108f, 132f),
                new Vector2(160f, 198f),
                new Vector2(212f, 132f),
                BarnRoofLight);
            DrawLine(texture, 82f, 126f, 160f, 216f, 4f, BarnTrim);
            DrawLine(texture, 160f, 216f, 238f, 126f, 4f, BarnTrim);
            DrawLine(texture, 78f, 126f, 242f, 126f, 4f, BarnTrim);

            FillRect(texture, 140, 26, 180, 94, WoodDark);
            DrawLine(texture, 140f, 94f, 180f, 94f, 2.6f, BarnTrim);
            DrawLine(texture, 140f, 26f, 180f, 94f, 2.2f, BarnTrim);
            DrawLine(texture, 180f, 26f, 140f, 94f, 2.2f, BarnTrim);

            DrawLine(texture, 128f, 68f, 202f, 68f, 4f, Wood);
            DrawLine(texture, 130f, 68f, 124f, 48f, 2.5f, WoodDark);
            DrawLine(texture, 200f, 68f, 206f, 48f, 2.5f, WoodDark);

            DrawChicken(texture, 236f, 52f, 1.15f);
            DrawEllipse(texture, 244f, 24f, 4f, 2f, Hay, 0f);
            DrawEllipse(texture, 258f, 24f, 3f, 1.5f, Hay, 0f);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildFeedGrinderWorld()
        {
            var texture = NewTexture(SmallBuildingWidth, SmallBuildingHeight);

            DrawEllipse(texture, 160f, 36f, 112f, 24f, Shadow, 0f);

            FillRect(texture, 82, 30, 238, 46, WoodDark);
            FillRect(texture, 88, 34, 232, 46, Wood);

            FillRect(texture, 104, 44, 216, 154, StoneDark);
            FillRect(texture, 111, 47, 209, 147, Stone);
            FillRect(texture, 124, 58, 196, 142, StoneLight);

            for (var y = 66; y <= 130; y += 16)
            {
                DrawLine(texture, 116f, y, 204f, y + 2f, 1.3f, StoneDark);
            }

            DrawTriangle(
                texture,
                new Vector2(114f, 146f),
                new Vector2(160f, 222f),
                new Vector2(206f, 146f),
                WoodDark);
            DrawTriangle(
                texture,
                new Vector2(128f, 150f),
                new Vector2(160f, 202f),
                new Vector2(192f, 150f),
                Wood);
            DrawLine(texture, 114f, 146f, 160f, 222f, 3.6f, BarnTrim);
            DrawLine(texture, 160f, 222f, 206f, 146f, 3.6f, BarnTrim);
            DrawLine(texture, 110f, 146f, 210f, 146f, 3.6f, BarnTrim);

            DrawEllipse(texture, 230f, 84f, 38f, 38f, StoneDark, 0f);
            DrawEllipse(texture, 230f, 84f, 31f, 31f, Metal, 0f);
            DrawEllipse(texture, 230f, 84f, 10f, 10f, WoodDark, 0f);
            for (var i = 0; i < 6; i++)
            {
                var angle = i * Mathf.PI / 3f;
                DrawLine(
                    texture,
                    230f + Mathf.Cos(angle) * 10f,
                    84f + Mathf.Sin(angle) * 10f,
                    230f + Mathf.Cos(angle) * 28f,
                    84f + Mathf.Sin(angle) * 28f,
                    3f,
                    WoodDark);
            }

            DrawLine(texture, 230f, 116f, 230f, 140f, 4f, WoodDark);
            DrawEllipse(texture, 230f, 143f, 6f, 6f, Wood, 0f);

            DrawTriangle(
                texture,
                new Vector2(104f, 52f),
                new Vector2(58f, 34f),
                new Vector2(104f, 28f),
                WoodDark);
            DrawTriangle(
                texture,
                new Vector2(102f, 49f),
                new Vector2(64f, 35f),
                new Vector2(102f, 31f),
                Wood);

            DrawEllipse(texture, 52f, 34f, 20f, 13f, FeedSack, 0f);
            FillRect(texture, 42, 32, 62, 44, FeedSack);
            DrawLine(texture, 43f, 44f, 61f, 44f, 2f, WoodDark);
            DrawLine(texture, 48f, 46f, 48f, 54f, 2.4f, WoodDark);
            DrawLine(texture, 56f, 46f, 56f, 54f, 2.4f, WoodDark);

            DrawEllipse(texture, 74f, 26f, 4.5f, 2.6f, Hay, 0f);
            DrawEllipse(texture, 84f, 25f, 4f, 2.4f, FeedGreen, 0f);
            DrawEllipse(texture, 93f, 26f, 3.6f, 2.2f, Hay, 0f);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildFishPondIcon()
        {
            var texture = NewTexture(IconSize, IconSize);

            DrawEllipse(texture, 64f, 24f, 45f, 8f, Shadow, 0f);
            DrawEllipse(texture, 64f, 58f, 48f, 38f, WaterRim, 0f);
            DrawEllipse(texture, 64f, 62f, 42f, 32f, WaterDeep, 0f);
            DrawEllipse(texture, 64f, 66f, 35f, 25f, Water, 0f);
            DrawEllipse(texture, 58f, 70f, 22f, 12f, WaterLight, 0f);

            var ripple = new Color(WaterLight.r, WaterLight.g, WaterLight.b, 0.75f);
            DrawEllipse(texture, 50f, 72f, 16f, 4f, ripple, 0f);
            DrawEllipse(texture, 50f, 72f, 10f, 2f, ripple, 0f);
            DrawEllipse(texture, 77f, 52f, 12f, 3f, ripple, 0f);

            FillRect(texture, 82, 58, 116, 72, WoodDark);
            FillRect(texture, 84, 60, 114, 69, Wood);
            DrawLine(texture, 84f, 69f, 114f, 69f, 2f, WoodDark);
            FillRect(texture, 86, 54, 90, 78, WoodDark);
            FillRect(texture, 108, 54, 112, 78, WoodDark);

            DrawLine(texture, 28f, 68f, 20f, 96f, 2f, GrassEdge);
            DrawLine(texture, 36f, 68f, 42f, 92f, 1.8f, GrassEdge);
            DrawEllipse(texture, 19f, 98f, 3f, 7f, GrassBlade, -10f);
            DrawEllipse(texture, 43f, 94f, 3f, 6f, GrassBlade, 10f);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildCowStableIcon()
        {
            var texture = NewTexture(IconSize, IconSize);

            DrawEllipse(texture, 64f, 20f, 48f, 8f, Shadow, 0f);
            FillRect(texture, 36, 22, 92, 72, BarnRed);
            FillRect(texture, 36, 22, 44, 72, BarnRedDark);
            FillRect(texture, 84, 22, 92, 72, BarnRedDark);

            DrawTriangle(
                texture,
                new Vector2(26f, 68f),
                new Vector2(64f, 108f),
                new Vector2(102f, 68f),
                BarnRoof);
            DrawTriangle(
                texture,
                new Vector2(38f, 72f),
                new Vector2(64f, 98f),
                new Vector2(90f, 72f),
                BarnRoofLight);
            DrawLine(texture, 26f, 68f, 64f, 108f, 2.6f, BarnTrim);
            DrawLine(texture, 64f, 108f, 102f, 68f, 2.6f, BarnTrim);
            DrawLine(texture, 23f, 68f, 105f, 68f, 2.6f, BarnTrim);

            FillRect(texture, 56, 22, 72, 52, BarnDoor);
            DrawLine(texture, 56f, 52f, 72f, 52f, 1.6f, BarnTrim);
            DrawLine(texture, 56f, 22f, 72f, 52f, 1.6f, BarnTrim);
            DrawLine(texture, 72f, 22f, 56f, 52f, 1.6f, BarnTrim);

            FillRect(texture, 42, 48, 52, 60, BarnTrim);
            FillRect(texture, 44, 50, 50, 58, BarnDoor);
            FillRect(texture, 76, 48, 86, 60, BarnTrim);
            FillRect(texture, 78, 50, 84, 58, BarnDoor);

            FillRect(texture, 36, 22, 46, 34, Hay);
            FillRect(texture, 82, 22, 92, 34, Hay);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildCoopIcon()
        {
            var texture = NewTexture(IconSize, IconSize);

            DrawEllipse(texture, 60f, 20f, 38f, 7f, Shadow, 0f);
            FillRect(texture, 36, 22, 84, 66, CoopYellow);
            FillRect(texture, 36, 22, 43, 66, CoopDark);
            FillRect(texture, 77, 22, 84, 66, CoopDark);

            DrawTriangle(
                texture,
                new Vector2(26f, 62f),
                new Vector2(60f, 106f),
                new Vector2(94f, 62f),
                BarnRoof);
            DrawTriangle(
                texture,
                new Vector2(36f, 66f),
                new Vector2(60f, 96f),
                new Vector2(84f, 66f),
                BarnRoofLight);
            DrawLine(texture, 26f, 62f, 60f, 106f, 2.6f, BarnTrim);
            DrawLine(texture, 60f, 106f, 94f, 62f, 2.6f, BarnTrim);
            DrawLine(texture, 23f, 62f, 97f, 62f, 2.6f, BarnTrim);

            FillRect(texture, 50, 22, 70, 50, WoodDark);
            DrawLine(texture, 50f, 50f, 70f, 50f, 1.8f, BarnTrim);
            DrawLine(texture, 50f, 22f, 70f, 50f, 1.6f, BarnTrim);
            DrawLine(texture, 70f, 22f, 50f, 50f, 1.6f, BarnTrim);
            DrawLine(texture, 42f, 42f, 78f, 42f, 2.4f, Wood);
            DrawLine(texture, 43f, 42f, 40f, 30f, 1.8f, WoodDark);
            DrawLine(texture, 77f, 42f, 80f, 30f, 1.8f, WoodDark);

            DrawChicken(texture, 88f, 42f, 0.65f);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildFeedGrinderIcon()
        {
            var texture = NewTexture(IconSize, IconSize);

            DrawEllipse(texture, 62f, 20f, 42f, 7f, Shadow, 0f);
            FillRect(texture, 34, 24, 90, 78, StoneDark);
            FillRect(texture, 39, 27, 85, 74, Stone);
            FillRect(texture, 47, 34, 77, 70, StoneLight);

            DrawTriangle(
                texture,
                new Vector2(34f, 74f),
                new Vector2(62f, 116f),
                new Vector2(90f, 74f),
                WoodDark);
            DrawTriangle(
                texture,
                new Vector2(43f, 77f),
                new Vector2(62f, 104f),
                new Vector2(81f, 77f),
                Wood);
            DrawLine(texture, 34f, 74f, 62f, 116f, 2.4f, BarnTrim);
            DrawLine(texture, 62f, 116f, 90f, 74f, 2.4f, BarnTrim);
            DrawLine(texture, 30f, 74f, 94f, 74f, 2.4f, BarnTrim);

            DrawEllipse(texture, 90f, 46f, 20f, 20f, StoneDark, 0f);
            DrawEllipse(texture, 90f, 46f, 15f, 15f, Metal, 0f);
            DrawEllipse(texture, 90f, 46f, 5f, 5f, WoodDark, 0f);
            DrawLine(texture, 90f, 46f, 90f, 63f, 2f, WoodDark);
            DrawLine(texture, 84f, 46f, 96f, 46f, 2f, WoodDark);

            DrawEllipse(texture, 39f, 27f, 10f, 6f, FeedSack, 0f);
            FillRect(texture, 32, 24, 46, 35, FeedSack);
            DrawLine(texture, 33f, 34f, 45f, 34f, 1.6f, WoodDark);
            DrawEllipse(texture, 52f, 23f, 2.6f, 1.4f, Hay, 0f);
            DrawEllipse(texture, 58f, 22f, 2.2f, 1.2f, FeedGreen, 0f);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildFishAnimal()
        {
            var texture = NewTexture(AnimalSize, AnimalSize);

            DrawTriangle(
                texture,
                new Vector2(62f, 128f),
                new Vector2(14f, 86f),
                new Vector2(14f, 170f),
                FishFinDark);
            DrawTriangle(
                texture,
                new Vector2(72f, 128f),
                new Vector2(28f, 104f),
                new Vector2(28f, 152f),
                FishFin);

            DrawEllipse(texture, 126f, 128f, 78f, 46f, FishBodyDark, 0f);
            DrawEllipse(texture, 130f, 130f, 70f, 38f, FishBody, 0f);
            DrawEllipse(texture, 136f, 116f, 54f, 19f, FishBelly, 0f);

            DrawTriangle(
                texture,
                new Vector2(104f, 158f),
                new Vector2(136f, 202f),
                new Vector2(160f, 156f),
                FishFin);
            DrawTriangle(
                texture,
                new Vector2(108f, 104f),
                new Vector2(138f, 72f),
                new Vector2(158f, 102f),
                FishFinDark);

            DrawLine(texture, 112f, 150f, 104f, 112f, 5f, FishBodyDark);
            DrawLine(texture, 144f, 154f, 138f, 110f, 4f, FishBodyDark);
            DrawLine(texture, 168f, 146f, 158f, 108f, 4f, FishBodyDark);

            DrawEllipse(texture, 178f, 142f, 11f, 11f, Color.white, 0f);
            DrawEllipse(texture, 181f, 142f, 5.5f, 5.5f, FishEye, 0f);
            DrawLine(texture, 196f, 118f, 204f, 118f, 2.4f, FishBodyDark);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildChickenAnimal()
        {
            var texture = NewTexture(AnimalSize, AnimalSize);

            DrawEllipse(texture, 128f, 48f, 46f, 9f, Shadow, 0f);

            DrawTriangle(
                texture,
                new Vector2(72f, 126f),
                new Vector2(34f, 100f),
                new Vector2(66f, 146f),
                Color.white);
            DrawTriangle(
                texture,
                new Vector2(76f, 120f),
                new Vector2(38f, 132f),
                new Vector2(70f, 150f),
                Rgb(236, 230, 214));

            DrawChicken(texture, 118f, 118f, 4.2f);

            DrawEllipse(texture, 112f, 118f, 26f, 18f, Rgb(238, 232, 216), -12f);
            DrawLine(texture, 94f, 116f, 130f, 112f, 2f, Rgb(208, 198, 178));
            DrawEllipse(texture, 164f, 166f, 4.2f, 4.2f, FishEye, 0f);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildCowAnimal()
        {
            var texture = NewTexture(AnimalSize, AnimalSize);

            DrawEllipse(texture, 126f, 36f, 96f, 10f, Shadow, 0f);

            DrawLine(texture, 62f, 150f, 44f, 96f, 5f, CowOutline);
            DrawEllipse(texture, 43f, 90f, 8f, 12f, CowBlack, 20f);

            FillRect(texture, 72, 46, 94, 120, CowOutline);
            FillRect(texture, 76, 48, 90, 118, CowWhite);
            FillRect(texture, 100, 46, 122, 120, CowOutline);
            FillRect(texture, 104, 48, 118, 118, CowWhite);
            FillRect(texture, 152, 46, 174, 120, CowOutline);
            FillRect(texture, 156, 48, 170, 118, CowWhite);
            FillRect(texture, 180, 46, 202, 120, CowOutline);
            FillRect(texture, 184, 48, 198, 118, CowWhite);

            FillRect(texture, 72, 46, 94, 58, CowBlack);
            FillRect(texture, 100, 46, 122, 58, CowBlack);
            FillRect(texture, 152, 46, 174, 58, CowBlack);
            FillRect(texture, 180, 46, 202, 58, CowBlack);

            DrawEllipse(texture, 126f, 142f, 84f, 52f, CowOutline, 0f);
            DrawEllipse(texture, 126f, 145f, 78f, 46f, CowWhite, 0f);

            DrawEllipse(texture, 96f, 154f, 28f, 18f, CowBlack, -15f);
            DrawEllipse(texture, 158f, 132f, 24f, 16f, CowBlack, 20f);
            DrawEllipse(texture, 176f, 158f, 14f, 10f, CowBlack, 0f);

            DrawEllipse(texture, 204f, 168f, 40f, 34f, CowOutline, 0f);
            DrawEllipse(texture, 203f, 170f, 36f, 30f, CowWhite, 0f);
            DrawEllipse(texture, 216f, 184f, 14f, 11f, CowBlack, -20f);

            DrawEllipse(texture, 230f, 154f, 22f, 15f, CowPink, 0f);
            DrawEllipse(texture, 236f, 157f, 2.6f, 2f, CowOutline, 0f);
            DrawEllipse(texture, 243f, 155f, 2.4f, 1.8f, CowOutline, 0f);

            DrawEllipse(texture, 184f, 194f, 11f, 6f, CowWhite, 30f);
            DrawEllipse(texture, 184f, 196f, 7f, 4f, CowPink, 30f);
            DrawTriangle(
                texture,
                new Vector2(192f, 198f),
                new Vector2(198f, 216f),
                new Vector2(204f, 198f),
                CowHorn);

            DrawEllipse(texture, 201f, 176f, 7f, 7f, Color.white, 0f);
            DrawEllipse(texture, 203f, 176f, 4f, 4f, FishEye, 0f);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildFishPen()
        {
            var texture = NewTexture(PenWidth, PenHeight);
            var pixels = new Color[PenWidth * PenHeight];
            for (var y = 0; y < PenHeight; y++)
            {
                var t = y / (float)(PenHeight - 1);
                var color = Color.Lerp(PenWaterBottom, PenWaterTop, t);
                for (var x = 0; x < PenWidth; x++)
                {
                    var shimmer = Mathf.Sin((x + y * 2.4f) * 0.011f) * 0.012f;
                    pixels[y * PenWidth + x] = new Color(
                        Mathf.Clamp01(color.r + shimmer),
                        Mathf.Clamp01(color.g + shimmer),
                        Mathf.Clamp01(color.b + shimmer),
                        1f);
                }
            }

            texture.SetPixels(pixels);

            for (var i = 0; i < 5; i++)
            {
                var x = 80f + i * 210f;
                DrawTriangle(
                    texture,
                    new Vector2(x, PenHeight),
                    new Vector2(x + 120f, PenHeight),
                    new Vector2(x + 330f, 90f),
                    new Color(0.8f, 0.96f, 1f, 0.07f));
            }

            DrawEllipse(texture, 512f, 22f, 620f, 82f, PenSand, 0f);
            DrawEllipse(texture, 512f, 44f, 570f, 50f, PenSandLight, 0f);

            DrawEllipse(texture, 150f, 64f, 54f, 26f, Rgb(92, 112, 108), 0f);
            DrawEllipse(texture, 884f, 74f, 66f, 30f, Rgb(88, 108, 104), 0f);
            DrawEllipse(texture, 920f, 72f, 36f, 18f, Rgb(112, 132, 124), 0f);

            DrawSeaweed(texture, 82f, 74f, 220f, 14f);
            DrawSeaweed(texture, 944f, 66f, 190f, 12f);
            DrawSeaweed(texture, 826f, 88f, 150f, 10f);

            for (var i = 0; i < 14; i++)
            {
                var x = 90f + (i * 137) % 850;
                var y = 110f + (i * 89) % 320;
                var radius = 4f + (i % 3) * 2.2f;
                DrawEllipse(texture, x, y, radius, radius, PenBubble, 0f);
                DrawEllipse(
                    texture,
                    x - radius * 0.3f,
                    y + radius * 0.3f,
                    radius * 0.32f,
                    radius * 0.32f,
                    new Color(1f, 1f, 1f, 0.5f),
                    0f);
            }

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildCoopPen()
        {
            var texture = NewTexture(PenWidth, PenHeight);

            FillTexture(texture, CoopWall);

            for (var y = 56; y < PenHeight; y += 74)
            {
                DrawLine(texture, 0f, y, PenWidth, y, 2.4f, CoopWallDark);
            }

            for (var x = 120; x < PenWidth; x += 210)
            {
                DrawLine(
                    texture,
                    x,
                    56f,
                    x + 18f,
                    PenHeight,
                    1.4f,
                    new Color(0.55f, 0.35f, 0.14f, 0.25f));
            }

            FillRect(texture, 86, 328, 286, 472, WoodDark);
            FillRect(texture, 100, 342, 272, 458, Rgb(128, 196, 218));
            DrawLine(texture, 186f, 342f, 186f, 458f, 5f, Wood);
            DrawLine(texture, 100f, 400f, 272f, 400f, 5f, Wood);
            DrawTriangle(
                texture,
                new Vector2(100f, 458f),
                new Vector2(186f, 458f),
                new Vector2(100f, 420f),
                new Color(1f, 1f, 1f, 0.18f));

            for (var i = 0; i < 3; i++)
            {
                var x = 380 + i * 160;
                FillRect(texture, x, 230, x + 128, 330, CoopDark);
                FillRect(texture, x + 10, 242, x + 118, 318, WoodDark);
                DrawEllipse(texture, x + 64f, 250f, 50f, 12f, Hay, 0f);
                DrawEllipse(texture, x + 50f, 254f, 8f, 4f, Rgb(196, 162, 72), 15f);
            }

            FillRect(texture, 50, 188, 974, 206, WoodDark);
            FillRect(texture, 50, 198, 974, 214, Wood);
            FillRect(texture, 50, 210, 974, 218, Rgb(172, 126, 68));

            FillRect(texture, 80, 58, 102, 206, WoodDark);
            FillRect(texture, 922, 58, 944, 206, WoodDark);

            FillRect(texture, 0, 0, PenWidth, 62, Rgb(164, 132, 68));
            for (var i = 0; i < 34; i++)
            {
                var x = 12f + (i * 83) % 990;
                var y = 8f + (i * 29) % 44;
                DrawLine(texture, x, y, x + 18f, y + 4f, 2f, Hay);
            }

            DrawLine(texture, 512f, PenHeight, 512f, 430f, 4f, WoodDark);
            DrawEllipse(texture, 512f, 420f, 18f, 12f, Rgb(214, 170, 80), 0f);
            DrawEllipse(texture, 512f, 422f, 12f, 8f, Rgb(255, 226, 130), 0f);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildStablePen()
        {
            var texture = NewTexture(PenWidth, PenHeight);

            FillTexture(texture, StableWall);
            for (var y = 62; y < PenHeight; y += 82)
            {
                DrawLine(texture, 0f, y, PenWidth, y, 2.6f, StableWallDark);
            }

            for (var x = 0; x < PenWidth; x += 256)
            {
                DrawLine(texture, x, 70f, x, PenHeight, 8f, StableWallDark);
            }

            FillRect(texture, 0, 0, PenWidth, 92, StableFloor);
            DrawLine(texture, 0f, 94f, PenWidth, 94f, 6f, WoodDark);
            for (var i = 0; i < 46; i++)
            {
                var x = 10f + (i * 71) % 1000;
                var y = 6f + (i * 31) % 78;
                DrawLine(texture, x, y, x + 20f, y + 5f, 2f, Hay);
            }

            FillRect(texture, 58, 74, 190, 160, Rgb(206, 166, 66));
            for (var y = 84; y <= 150; y += 14)
            {
                DrawLine(texture, 60f, y, 188f, y + 2f, 2.2f, WoodDark);
            }

            DrawLine(texture, 60f, 160f, 190f, 160f, 3f, Rgb(168, 130, 52));
            DrawLine(texture, 336f, 96f, 336f, 430f, 9f, WoodDark);
            DrawLine(texture, 348f, 96f, 348f, 430f, 4f, Wood);

            FillRect(texture, 748, 122, 980, 202, WoodDark);
            FillRect(texture, 760, 132, 968, 190, Wood);
            FillRect(texture, 774, 142, 954, 190, WoodDark);
            FillRect(texture, 776, 174, 952, 194, TroughFeed);
            FillRect(texture, 764, 92, 784, 132, WoodDark);
            FillRect(texture, 944, 92, 964, 132, WoodDark);

            DrawLine(texture, 512f, PenHeight, 512f, 428f, 4f, WoodDark);
            DrawEllipse(texture, 512f, 414f, 20f, 14f, Rgb(208, 164, 74), 0f);
            DrawEllipse(texture, 512f, 416f, 13f, 9f, Rgb(255, 226, 130), 0f);

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildPlacementTile()
        {
            var texture = NewTexture(GroundWidth, GroundHeight);
            var centerX = GroundWidth * 0.5f;
            var centerY = GroundHeight * 0.5f;
            var halfWidth = GroundWidth * 0.5f;
            var halfHeight = GroundHeight * 0.5f;

            for (var y = 0; y < GroundHeight; y++)
            {
                for (var x = 0; x < GroundWidth; x++)
                {
                    var diamond = Mathf.Abs(x + 0.5f - centerX) / halfWidth +
                                  Mathf.Abs(y + 0.5f - centerY) / halfHeight;
                    if (diamond > 1.01f)
                    {
                        continue;
                    }

                    var coverage = Mathf.Clamp01((1.01f - diamond) * 50f + 0.5f);
                    var alpha = diamond > 0.9f ? 0.85f : 0.32f;
                    BlendPixel(texture, x, y, new Color(1f, 1f, 1f, alpha), coverage);
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static void FillTexture(Texture2D texture, Color color)
        {
            var pixels = new Color[texture.width * texture.height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
        }

        private static void DrawSeaweed(
            Texture2D texture,
            float x,
            float baseY,
            float height,
            float width)
        {
            const int segments = 6;
            for (var i = 0; i < segments; i++)
            {
                var y0 = baseY + height * i / segments;
                var y1 = baseY + height * (i + 1) / segments;
                var x0 = x + Mathf.Sin(i * 1.25f) * width;
                var x1 = x + Mathf.Sin((i + 1) * 1.25f) * width;
                DrawLine(texture, x0, y0, x1, y1, width * 0.42f, PenSeaweed);
                DrawLine(texture, x0 - 2f, y0, x1 - 2f, y1, width * 0.16f, PenSeaweedLight);
                DrawEllipse(
                    texture,
                    x1,
                    y1,
                    width * 0.65f,
                    width * 0.28f,
                    PenSeaweedLight,
                    40f);
            }
        }

        private static void DrawChicken(Texture2D texture, float x, float y, float scale)
        {
            var comb = Rgb(190, 54, 43);
            DrawEllipse(texture, x, y, 14f * scale, 10f * scale, Color.white, 0f);
            DrawEllipse(texture, x + 9f * scale, y + 10f * scale, 6f * scale, 7f * scale, Color.white, 0f);
            DrawTriangle(
                texture,
                new Vector2(x + 14f * scale, y + 11f * scale),
                new Vector2(x + 23f * scale, y + 8f * scale),
                new Vector2(x + 14f * scale, y + 5f * scale),
                Rgb(232, 166, 44));
            DrawEllipse(texture, x + 8f * scale, y + 17f * scale, 3f * scale, 3f * scale, comb, 0f);
            DrawEllipse(texture, x + 12f * scale, y + 16f * scale, 2.6f * scale, 2.6f * scale, comb, 0f);
            DrawLine(
                texture,
                x - 5f * scale,
                y - 8f * scale,
                x - 5f * scale,
                y - 13f * scale,
                1.2f * scale,
                Rgb(222, 150, 34));
            DrawLine(
                texture,
                x + 3f * scale,
                y - 8f * scale,
                x + 3f * scale,
                y - 13f * scale,
                1.2f * scale,
                Rgb(222, 150, 34));
        }

        private static void FillRect(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            for (var y = y0; y < y1; y++)
            {
                for (var x = x0; x < x1; x++)
                {
                    BlendPixel(texture, x, y, color, 1f);
                }
            }
        }

        private static Texture2D BuildRoundedRect(int size, int radius)
        {
            var texture = NewTexture(size, size);
            var half = size * 0.5f - 2f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = x + 0.5f - size * 0.5f;
                    var py = y + 0.5f - size * 0.5f;
                    var distance = RoundedBoxSdf(px, py, half, half, radius);
                    var coverage = Mathf.Clamp01(0.5f - distance);
                    if (coverage > 0f)
                    {
                        BlendPixel(texture, x, y, Color.white, coverage);
                    }
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildCircle(int size, int radius)
        {
            var texture = NewTexture(size, size);
            var center = size * 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - center;
                    var dy = y + 0.5f - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    var coverage = Mathf.Clamp01(0.5f - distance);
                    if (coverage > 0f)
                    {
                        BlendPixel(texture, x, y, Color.white, coverage);
                    }
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D BuildSolid(int width, int height)
        {
            var texture = NewTexture(width, height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static void DrawTriangle(Texture2D texture, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            var minX = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - 2f);
            var maxX = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 2f);
            var minY = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - 2f);
            var maxY = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 2f);

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var hits = 0;
                    for (var sy = 0; sy < 3; sy++)
                    {
                        for (var sx = 0; sx < 3; sx++)
                        {
                            var px = x + (sx + 0.5f) / 3f;
                            var py = y + (sy + 0.5f) / 3f;
                            if (PointInTriangle(px, py, a, b, c))
                            {
                                hits++;
                            }
                        }
                    }

                    if (hits > 0)
                    {
                        BlendPixel(texture, x, y, color, hits / 9f);
                    }
                }
            }
        }

        private static bool PointInTriangle(float px, float py, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = Sign(px, py, a, b);
            var d2 = Sign(px, py, b, c);
            var d3 = Sign(px, py, c, a);
            var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Sign(float px, float py, Vector2 a, Vector2 b)
        {
            return (px - b.x) * (a.y - b.y) - (a.x - b.x) * (py - b.y);
        }

        private static void DrawEllipse(
            Texture2D texture,
            float centerX,
            float centerY,
            float radiusX,
            float radiusY,
            Color color,
            float rotationDegrees)
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
                    var rx = dx * cos + dy * sin;
                    var ry = -dx * sin + dy * cos;
                    var normalized = Mathf.Sqrt((rx / radiusX) * (rx / radiusX) + (ry / radiusY) * (ry / radiusY));
                    var distance = (normalized - 1f) * Mathf.Min(radiusX, radiusY);
                    var coverage = Mathf.Clamp01(0.5f - distance);
                    if (coverage > 0f)
                    {
                        BlendPixel(texture, x, y, color, coverage);
                    }
                }
            }
        }

        private static void DrawLine(
            Texture2D texture,
            float x0,
            float y0,
            float x1,
            float y1,
            float thickness,
            Color color)
        {
            var extent = Mathf.CeilToInt(thickness + 2f);
            var minX = Mathf.FloorToInt(Mathf.Min(x0, x1)) - extent;
            var maxX = Mathf.CeilToInt(Mathf.Max(x0, x1)) + extent;
            var minY = Mathf.FloorToInt(Mathf.Min(y0, y1)) - extent;
            var maxY = Mathf.CeilToInt(Mathf.Max(y0, y1)) + extent;

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var distance = DistanceToSegment(x + 0.5f, y + 0.5f, x0, y0, x1, y1);
                    var coverage = Mathf.Clamp01(thickness * 0.5f - distance + 0.5f);
                    if (coverage > 0f)
                    {
                        BlendPixel(texture, x, y, color, coverage);
                    }
                }
            }
        }

        private static float DistanceToSegment(float px, float py, float x0, float y0, float x1, float y1)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= 0.0001f)
            {
                return Mathf.Sqrt((px - x0) * (px - x0) + (py - y0) * (py - y0));
            }

            var t = Mathf.Clamp01(((px - x0) * dx + (py - y0) * dy) / lengthSquared);
            var closestX = x0 + t * dx;
            var closestY = y0 + t * dy;
            return Mathf.Sqrt((px - closestX) * (px - closestX) + (py - closestY) * (py - closestY));
        }

        private static void BlendPixel(Texture2D texture, int x, int y, Color color, float coverage)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            {
                return;
            }

            var alpha = Mathf.Clamp01(color.a * coverage);
            if (alpha <= 0.0001f)
            {
                return;
            }

            var destination = texture.GetPixel(x, y);
            var outAlpha = alpha + destination.a * (1f - alpha);
            if (outAlpha <= 0.0001f)
            {
                texture.SetPixel(x, y, Transparent);
                return;
            }

            var red = (color.r * alpha + destination.r * destination.a * (1f - alpha)) / outAlpha;
            var green = (color.g * alpha + destination.g * destination.a * (1f - alpha)) / outAlpha;
            var blue = (color.b * alpha + destination.b * destination.a * (1f - alpha)) / outAlpha;
            texture.SetPixel(x, y, new Color(red, green, blue, outAlpha));
        }

        private static float RoundedBoxSdf(float px, float py, float halfWidth, float halfHeight, float radius)
        {
            var qx = Mathf.Abs(px) - (halfWidth - radius);
            var qy = Mathf.Abs(py) - (halfHeight - radius);
            var outsideX = Mathf.Max(qx, 0f);
            var outsideY = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) +
                   Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Transparent;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void WritePng(string name, Texture2D texture)
        {
            if (!HasPaintedArt(name))
            {
                var path = $"{ArtDirectory}/{name}.png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }

            UnityEngine.Object.DestroyImmediate(texture);
        }

        /// <summary>Painted art replaces a placeholder, so a deleted placeholder must not come back.</summary>
        private static bool HasPaintedArt(string name)
        {
            return File.Exists(FieldArtImporter.IconOutputPath(name));
        }

        private static void ConfigureSprite(
            string name,
            float pixelsPerUnit,
            SpriteAlignment alignment,
            Vector4 border,
            Vector2? customPivot = null)
        {
            if (HasPaintedArt(name))
            {
                return;
            }

            var path = $"{ArtDirectory}/{name}.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"Could not import placeholder sprite: {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            // Most sprites are packed into the sprite atlas (SpriteAtlasBuilder), which compresses
            // them once; their sources stay uncompressed so they aren't compressed twice. Buildings
            // stay uncompressed for the tap pixel check. Only the large pen backgrounds, which
            // aren't packed, and the Beach's large tiles, are compressed here.
            importer.textureCompression = name.StartsWith("pen_", StringComparison.Ordinal) ||
                                          name.StartsWith("beach_", StringComparison.Ordinal)
                ? TextureImporterCompression.CompressedHQ
                : TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = name.StartsWith("beach_", StringComparison.Ordinal) ? 2048 : 1024;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            // Buildings stay readable so a tap can check it landed on a visible pixel.
            importer.isReadable = name.StartsWith("building_", StringComparison.Ordinal);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)alignment;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spritePixelsPerUnit = pixelsPerUnit;
            if (customPivot.HasValue)
            {
                settings.spritePivot = customPivot.Value;
            }

            importer.SetTextureSettings(settings);
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        private static CropPalette GetPalette(string cropId)
        {
            switch (cropId)
            {
                case "tomato":
                    return Palette(Rgb(70, 132, 50), Rgb(82, 150, 58), Rgb(132, 194, 92), Rgb(220, 52, 40), Rgb(255, 120, 96), Rgb(160, 30, 24));
                case "potato":
                    return Palette(Rgb(72, 128, 52), Rgb(86, 150, 60), Rgb(140, 196, 98), Rgb(196, 150, 96), Rgb(226, 190, 136), Rgb(140, 98, 56));
                case "strawberry":
                    return Palette(Rgb(66, 130, 50), Rgb(78, 148, 56), Rgb(130, 196, 90), Rgb(226, 44, 60), Rgb(255, 118, 128), Rgb(160, 24, 40));
                case "soybean":
                    return Palette(Rgb(96, 128, 48), Rgb(84, 146, 58), Rgb(136, 192, 90), Rgb(206, 178, 92), Rgb(232, 210, 132), Rgb(150, 120, 54));
                case "chili":
                    return Palette(Rgb(60, 120, 44), Rgb(70, 140, 52), Rgb(126, 190, 86), Rgb(214, 40, 32), Rgb(250, 96, 72), Rgb(150, 20, 16));
                case "pumpkin":
                    return Palette(Rgb(84, 130, 46), Rgb(92, 150, 56), Rgb(146, 198, 96), Rgb(236, 128, 36), Rgb(255, 176, 96), Rgb(176, 84, 20));
                case "corn":
                    return new CropPalette
                    {
                        Stem = Rgb(58, 124, 43),
                        Leaf = Rgb(73, 148, 52),
                        LeafLight = Rgb(131, 196, 94),
                        Produce = Rgb(242, 197, 70),
                        ProduceLight = Rgb(255, 229, 137),
                        ProduceDark = Rgb(191, 144, 35)
                    };
                case "carrot":
                    return new CropPalette
                    {
                        Stem = Rgb(58, 130, 45),
                        Leaf = Rgb(75, 155, 55),
                        LeafLight = Rgb(131, 199, 94),
                        Produce = Rgb(231, 124, 39),
                        ProduceLight = Rgb(249, 167, 91),
                        ProduceDark = Rgb(180, 84, 20)
                    };
                default:
                    return new CropPalette
                    {
                        Stem = Rgb(75, 135, 42),
                        Leaf = Rgb(91, 154, 51),
                        LeafLight = Rgb(140, 195, 86),
                        Produce = Rgb(226, 183, 64),
                        ProduceLight = Rgb(247, 216, 112),
                        ProduceDark = Rgb(174, 130, 31)
                    };
            }
        }

        private static CropPalette Palette(Color stem, Color leaf, Color leafLight, Color produce, Color produceLight, Color produceDark)
        {
            return new CropPalette
            {
                Stem = stem,
                Leaf = leaf,
                LeafLight = leafLight,
                Produce = produce,
                ProduceLight = produceLight,
                ProduceDark = produceDark
            };
        }

        private static Color Rgb(int red, int green, int blue)
        {
            return new Color(red / 255f, green / 255f, blue / 255f, 1f);
        }

        private static float Noise01(int x, int y, int seed)
        {
            unchecked
            {
                var value = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791);
                value ^= value >> 13;
                value *= 1274126177u;
                value ^= value >> 16;
                return (value & 0xFFFFFF) / 16777215f;
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private struct CropPalette
        {
            public Color Stem;
            public Color Leaf;
            public Color LeafLight;
            public Color Produce;
            public Color ProduceLight;
            public Color ProduceDark;
        }
    }
}
