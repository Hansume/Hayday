using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>
    /// Turns the painted grass sheet into ground tiles the farm can use.
    ///
    /// The sheet (<see cref="SheetPath"/>) holds <see cref="Columns"/> x <see cref="Rows"/> isometric
    /// diamonds. Each one is cut out, stretched to an exact 2:1 diamond the size of a farm cell and
    /// given a clean edge, then saved as its own sprite in <see cref="OutputDirectory"/>. Replace the
    /// sheet and the tiles are rebuilt automatically; Tools > Farm Game > Rebuild Grass Tiles does
    /// it by hand. Put plain grass on the first row and decorated (flower) tiles on the second:
    /// the farm uses the plain ones most.
    /// </summary>
    [InitializeOnLoad]
    public sealed class GroundArtImporter : AssetPostprocessor
    {
        /// <summary>
        /// Source art only: kept outside Resources so it isn't packed into builds. The game uses the
        /// tiles cut from it.
        /// </summary>
        public const string SheetPath = "Assets/_Project/NewAssets/Environment/grass_tiles_sheet.png";
        public const string OutputDirectory = "Assets/_Project/Resources/Art/Ground";
        public const string TilePrefix = "grass_";

        private const int Columns = 4;
        private const int Rows = 2;

        /// <summary>A farm cell is 2 x 1 world units, so a 512 x 256 tile at 256 px per unit fills it.</summary>
        private const int TileWidth = 512;
        private const int TileHeight = 256;
        private const float TilePixelsPerUnit = 256f;

        /// <summary>Samples slightly inside the painted diamond, skipping its soft, uneven fringe.</summary>
        private const float SourceInset = 0.96f;

        /// <summary>Makes each tile a hair larger than its cell, so neighbours overlap with no seam.</summary>
        private const float EdgeOverlap = 1.012f;

        static GroundArtImporter()
        {
            EditorApplication.delayCall += () =>
            {
                if (TilesAreStale())
                {
                    RebuildTiles();
                }
            };
        }

        [MenuItem("Tools/Farm Game/Rebuild Grass Tiles")]
        public static void RebuildTilesFromMenu()
        {
            if (RebuildTiles())
            {
                Debug.Log($"Grass tiles rebuilt in {OutputDirectory}.");
            }
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, SheetPath) >= 0)
            {
                // Asset work isn't allowed mid-import; do it right after.
                EditorApplication.delayCall += () => RebuildTiles();
            }
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(OutputDirectory + "/" + TilePrefix, StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TilePixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            // Packed into the sprite atlas, which compresses it; keep the source lossless.
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spritePixelsPerUnit = TilePixelsPerUnit;
            importer.SetTextureSettings(settings);
        }

        private static string TilePath(int index)
        {
            return $"{OutputDirectory}/{TilePrefix}{index}.png";
        }

        private static bool TilesAreStale()
        {
            if (!File.Exists(SheetPath))
            {
                return false;
            }

            var sheetTime = File.GetLastWriteTimeUtc(SheetPath);
            for (var i = 0; i < Columns * Rows; i++)
            {
                var path = TilePath(i);
                if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < sheetTime)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RebuildTiles()
        {
            if (!File.Exists(SheetPath))
            {
                Debug.LogWarning($"Grass sheet not found at {SheetPath}.");
                return false;
            }

            // Read the file directly so the sheet's own import settings don't matter.
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!sheet.LoadImage(File.ReadAllBytes(SheetPath)))
                {
                    Debug.LogError($"Could not read the grass sheet at {SheetPath}.");
                    return false;
                }

                sheet.wrapMode = TextureWrapMode.Clamp;
                sheet.filterMode = FilterMode.Bilinear;
                var pixels = sheet.GetPixels32();

                Directory.CreateDirectory(OutputDirectory);
                var cellWidth = sheet.width / Columns;
                var cellHeight = sheet.height / Rows;

                for (var row = 0; row < Rows; row++)
                {
                    for (var column = 0; column < Columns; column++)
                    {
                        // Unity textures start at the bottom; the sheet's first row is at the top.
                        var cellX = column * cellWidth;
                        var cellY = sheet.height - (row + 1) * cellHeight;
                        var index = row * Columns + column;

                        if (!TryFindDiamond(pixels, sheet.width, cellX, cellY, cellWidth, cellHeight, out var diamond))
                        {
                            Debug.LogError($"No grass diamond found in sheet cell {index} (row {row}, column {column}).");
                            return false;
                        }

                        var tile = BuildTile(sheet, diamond);
                        File.WriteAllBytes(TilePath(index), tile.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(tile);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sheet);
            }

            AssetDatabase.Refresh();
            SpriteAtlasBuilder.Rebuild();
            return true;
        }

        /// <summary>Where a painted diamond's four tips are, in sheet pixels.</summary>
        private struct Diamond
        {
            public float Left;
            public float Right;
            public float TipY;
            public float Top;
            public float Bottom;
        }

        /// <summary>
        /// Finds the diamond in one sheet cell: the widest solid row near the middle gives the left
        /// and right tips, and the solid column through its middle gives the top and bottom tips.
        /// </summary>
        private static bool TryFindDiamond(
            Color32[] pixels,
            int sheetWidth,
            int cellX,
            int cellY,
            int cellWidth,
            int cellHeight,
            out Diamond diamond)
        {
            const byte Solid = 128;
            diamond = default;

            bool IsSolid(int x, int y)
            {
                return x >= cellX && x < cellX + cellWidth &&
                       y >= cellY && y < cellY + cellHeight &&
                       pixels[y * sheetWidth + x].a > Solid;
            }

            var centerX = cellX + cellWidth / 2;
            var centerY = cellY + cellHeight / 2;
            var bestWidth = 0;
            var bestY = centerY;
            var bestLeft = 0;
            var bestRight = 0;
            var search = cellHeight / 6;

            for (var y = centerY - search; y <= centerY + search; y++)
            {
                if (!IsSolid(centerX, y))
                {
                    continue;
                }

                var left = centerX;
                while (IsSolid(left - 1, y))
                {
                    left--;
                }

                var right = centerX;
                while (IsSolid(right + 1, y))
                {
                    right++;
                }

                if (right - left > bestWidth)
                {
                    bestWidth = right - left;
                    bestY = y;
                    bestLeft = left;
                    bestRight = right;
                }
            }

            if (bestWidth < cellWidth / 4)
            {
                return false;
            }

            var middleX = (bestLeft + bestRight) / 2;
            var top = bestY;
            while (IsSolid(middleX, top + 1))
            {
                top++;
            }

            var bottom = bestY;
            while (IsSolid(middleX, bottom - 1))
            {
                bottom--;
            }

            diamond = new Diamond
            {
                Left = bestLeft,
                Right = bestRight + 1f,
                TipY = bestY + 0.5f,
                Top = top + 1f,
                Bottom = bottom
            };
            return top > bestY && bottom < bestY;
        }

        /// <summary>Resamples one painted diamond into an exact 2:1 tile with a clean, slightly oversized edge.</summary>
        private static Texture2D BuildTile(Texture2D sheet, Diamond source)
        {
            var tile = new Texture2D(TileWidth, TileHeight, TextureFormat.RGBA32, false);
            var output = new Color32[TileWidth * TileHeight];

            var centerX = (source.Left + source.Right) * 0.5f;
            var halfWidth = (source.Right - source.Left) * 0.5f * SourceInset;
            var upper = (source.Top - source.TipY) * SourceInset;
            var lower = (source.TipY - source.Bottom) * SourceInset;
            var halfTileWidth = TileWidth * 0.5f;
            var halfTileHeight = TileHeight * 0.5f;

            for (var y = 0; y < TileHeight; y++)
            {
                for (var x = 0; x < TileWidth; x++)
                {
                    // -1..1 across the tile, 0 at its middle.
                    var u = (x + 0.5f - halfTileWidth) / halfTileWidth;
                    var v = (y + 0.5f - halfTileHeight) / halfTileHeight;
                    var shape = Mathf.Abs(u) + Mathf.Abs(v);

                    // One pixel of soft edge, pushed out slightly so neighbouring tiles overlap.
                    var alpha = Mathf.Clamp01((EdgeOverlap - shape) * halfTileHeight + 0.5f);
                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    var sourceX = centerX + u * halfWidth;
                    var sourceY = source.TipY + (v >= 0f ? v * upper : v * lower);
                    var color = sheet.GetPixelBilinear(sourceX / sheet.width, sourceY / sheet.height);
                    color.a = alpha;
                    output[y * TileWidth + x] = color;
                }
            }

            tile.SetPixels32(output);
            tile.Apply(false);
            return tile;
        }
    }
}
