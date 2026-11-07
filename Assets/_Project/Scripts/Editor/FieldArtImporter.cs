using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>
    /// Turns the painted art in <see cref="SourceDirectory"/> (pre-cut PNGs on a transparent
    /// background) into the sprites the game uses. Files are matched by folder and name:
    /// <list type="bullet">
    /// <item>Environment/Empty_Field.png -> Art/Field/field_empty, the empty plot.</item>
    /// <item>Crops/GrowthStages/&lt;Crop&gt;/Crop_&lt;Crop&gt;_1..4.png -> Art/Field/crop_&lt;crop&gt;_0..3,
    /// the growth stages (each painted with its own soil, so it covers the empty field).</item>
    /// <item>Crops/Harvested/&lt;Crop&gt;.png -> Art/Icons/icon_&lt;crop&gt;, the crop's item icon.</item>
    /// <item>AnimalFeed/&lt;Name&gt;.png -> Art/Icons/icon_&lt;name&gt; (Cow_Feed -> icon_cow_feed).</item>
    /// <item>Products/&lt;Name&gt;.png -> Art/Icons/icon_&lt;name&gt; (Goat_Cheese -> icon_goat_cheese).</item>
    /// <item>Buildings/&lt;Name&gt;.png -> Art/Icons/building_&lt;name&gt; and icon_&lt;name&gt;, the building on
    /// the farm and its shop icon. The art is fitted so its ground stays inside the footprint.</item>
    /// <item>Animals/&lt;Name&gt;.png -> Art/Icons/animal_&lt;name&gt;, the animal in its pen (Cow -> animal_cow).</item>
    /// <item>Visitors/&lt;Name&gt;.png -> Art/Icons/visitor_&lt;look&gt;, a visitor look on the farm and in its
    /// portrait. The name picks the look (<see cref="VisitorLooks"/>); the art is cropped, stood on its
    /// feet and scaled to <see cref="VisitorWorldHeight"/>.</item>
    /// </list>
    /// Field sprites are scaled and pinned so the whole soil tile sits inside its cell. Icons are shrunk to at most <see cref="IconMaxSize"/> px.
    /// The game prefers these outputs over the generated placeholders, and the placeholder generator
    /// never writes to them. Adding or changing a source rebuilds automatically; Tools > Farm Game >
    /// Rebuild Painted Art does it by hand. Run Apply Balance afterwards to wire new art into content.
    /// </summary>
    [InitializeOnLoad]
    public sealed class FieldArtImporter : AssetPostprocessor
    {
        /// <summary>Source art: kept outside Resources so it isn't packed into builds.</summary>
        public const string SourceDirectory = "Assets/_Project/NewAssets";

        /// <summary>Art waiting to be used: nothing here is imported, so adding to it rebuilds nothing.</summary>
        public const string UnusedDirectory = SourceDirectory + "/Unused";

        public const string OutputDirectory = "Assets/_Project/Resources/Art/Field";
        public const string IconOutputDirectory = "Assets/_Project/Resources/Art/Icons";
        public const string EmptyFieldName = "field_empty";

        /// <summary>A farm cell is 2 world units wide.</summary>
        private const float CellWorldWidth = 2f;
        private const int IconMaxSize = 256;
        private const float IconPixelsPerUnit = 100f;
        private const int BuildingMaxSize = 1024;

        /// <summary>FarmView rescales buildings to their footprint, so this only sets the import size.</summary>
        private const float BuildingPixelsPerUnit = 128f;

        /// <summary>A footprint diamond is half as tall as it is wide, so its centre is a quarter of its width above its front corner.</summary>
        private const float FootprintHalfHeightPerWidth = 0.25f;

        /// <summary>
        /// Buildings that lie on the ground rather than stand up from it: their art has to fit inside
        /// the back edges of their footprint as well as the front ones.
        /// </summary>
        private static readonly HashSet<string> FlatBuildingOutputs = new HashSet<string>
        {
            "building_fish_pond",
            "building_cow_stable"
        };

        /// <summary>
        /// How much of its footprint a building's art fills, for ones that should sit clear of the
        /// edges (1 or unlisted: right up to them). Lower is smaller.
        /// </summary>
        private static readonly Dictionary<string, float> FootprintFill = new Dictionary<string, float>
        {
            { "building_cow_stable", 0.918f }
        };

        /// <summary>
        /// Which visitor look (VisitorService.LookCount of them) each painted visitor replaces. Looks
        /// without one keep their placeholder.
        /// </summary>
        private static readonly Dictionary<string, int> VisitorLooks = new Dictionary<string, int>
        {
            { "victor", 0 },
            { "lilly", 1 }
        };

        /// <summary>A visitor's height from feet to the top of the head, in world units (as the placeholders).</summary>
        private const float VisitorWorldHeight = 1.31f;

        private const int VisitorMaxSize = 512;
        private const int VisitorPadding = 2;

        /// <summary>The share of a building's pixels allowed past each slanted edge of its footprint.</summary>
        private const float FootprintStrayShare = 0.01f;

        /// <summary>
        /// The painted soil is a little wider than its top face, off-centre from it, and has thickness
        /// below it. This scale, lift and shift (world units, measured from Empty_Field.png) bring it
        /// all inside the cell. Every field sprite gets the same values, so a plot doesn't change size
        /// or jump as its crop grows.
        /// </summary>
        private const float FieldFitScale = 0.945f;
        private const float FieldFitLift = 0.078f;
        private const float FieldFitShift = 0.035f;

        static FieldArtImporter()
        {
            EditorApplication.delayCall += () =>
            {
                if (IsStale())
                {
                    Rebuild();
                }
            };
        }

        [MenuItem("Tools/Farm Game/Rebuild Painted Art")]
        public static void RebuildFromMenu()
        {
            if (Rebuild())
            {
                Debug.Log($"Painted art rebuilt into {OutputDirectory} and {IconOutputDirectory}. Run Apply Balance to use new icons.");
            }
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (path.StartsWith(SourceDirectory + "/", StringComparison.Ordinal) &&
                    !path.StartsWith(UnusedDirectory + "/", StringComparison.Ordinal) &&
                    path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    // Asset work isn't allowed mid-import; do it right after.
                    EditorApplication.delayCall += () => Rebuild();
                    return;
                }
            }
        }

        public static string OutputPath(string name)
        {
            return $"{OutputDirectory}/{name}.png";
        }

        public static string IconOutputPath(string name)
        {
            return $"{IconOutputDirectory}/{name}.png";
        }

        // ------------------------------------------------------------------ Source -> output mapping

        private enum ArtKind
        {
            Field,
            Icon,
            Building,
            Visitor
        }

        private readonly struct ArtJob
        {
            public readonly string Source;
            public readonly string Output;
            public readonly ArtKind Kind;

            public ArtJob(string source, string output, ArtKind kind)
            {
                Source = source;
                Output = output;
                Kind = kind;
            }
        }

        private static List<ArtJob> FindJobs()
        {
            var jobs = new List<ArtJob>();

            var emptyField = $"{SourceDirectory}/Environment/Empty_Field.png";
            if (File.Exists(emptyField))
            {
                jobs.Add(new ArtJob(emptyField, OutputPath(EmptyFieldName), ArtKind.Field));
            }

            var stagesRoot = $"{SourceDirectory}/Crops/GrowthStages";
            if (Directory.Exists(stagesRoot))
            {
                foreach (var folder in Directory.GetDirectories(stagesRoot))
                {
                    var cropName = Path.GetFileName(folder);
                    var cropId = ToId(cropName);
                    foreach (var file in Directory.GetFiles(folder, "*.png"))
                    {
                        // Crop_Wheat_1 .. _4 -> crop_wheat_0 .. _3
                        var name = Path.GetFileNameWithoutExtension(file);
                        var underscore = name.LastIndexOf('_');
                        if (underscore < 0 || !int.TryParse(name.Substring(underscore + 1), out var number) || number < 1)
                        {
                            Debug.LogWarning($"{file}: expected a name ending in _1, _2, ... for its growth stage; skipped.");
                            continue;
                        }

                        jobs.Add(new ArtJob(Normalize(file), OutputPath($"crop_{cropId}_{number - 1}"), ArtKind.Field));
                    }
                }
            }

            AddIcons(jobs, $"{SourceDirectory}/Crops/Harvested");
            AddIcons(jobs, $"{SourceDirectory}/AnimalFeed");
            AddIcons(jobs, $"{SourceDirectory}/Products");
            AddIcons(jobs, $"{SourceDirectory}/Buildings");
            AddIcons(jobs, $"{SourceDirectory}/Animals", "animal");

            var buildingsRoot = $"{SourceDirectory}/Buildings";
            if (Directory.Exists(buildingsRoot))
            {
                foreach (var file in Directory.GetFiles(buildingsRoot, "*.png"))
                {
                    var id = ToId(Path.GetFileNameWithoutExtension(file));
                    jobs.Add(new ArtJob(Normalize(file), IconOutputPath($"building_{id}"), ArtKind.Building));
                }
            }

            var visitorsRoot = $"{SourceDirectory}/Visitors";
            if (Directory.Exists(visitorsRoot))
            {
                foreach (var file in Directory.GetFiles(visitorsRoot, "*.png"))
                {
                    var name = ToId(Path.GetFileNameWithoutExtension(file));
                    if (!VisitorLooks.TryGetValue(name, out var look))
                    {
                        Debug.LogWarning($"{file}: no visitor look is assigned to '{name}' (FieldArtImporter.VisitorLooks); skipped.");
                        continue;
                    }

                    jobs.Add(new ArtJob(Normalize(file), IconOutputPath($"visitor_{look}"), ArtKind.Visitor));
                }
            }

            return jobs;
        }

        private static void AddIcons(List<ArtJob> jobs, string folder, string prefix = "icon")
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(folder, "*.png"))
            {
                var id = ToId(Path.GetFileNameWithoutExtension(file));
                jobs.Add(new ArtJob(Normalize(file), IconOutputPath($"{prefix}_{id}"), ArtKind.Icon));
            }
        }

        /// <summary>"Chicken_Feed" -> "chicken_feed", "Sweet Corn" -> "sweet_corn".</summary>
        private static string ToId(string name)
        {
            return name.Trim().Replace(' ', '_').Replace('-', '_').ToLowerInvariant();
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }

        private static bool IsStale()
        {
            foreach (var job in FindJobs())
            {
                if (!File.Exists(job.Output) || File.GetLastWriteTimeUtc(job.Output) < File.GetLastWriteTimeUtc(job.Source))
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ Rebuild

        /// <summary>Rebuilds every painted sprite. False if a source couldn't be read.</summary>
        public static bool Rebuild()
        {
            Directory.CreateDirectory(OutputDirectory);
            Directory.CreateDirectory(IconOutputDirectory);
            var written = new List<KeyValuePair<ArtJob, Vector2>>();
            var pixelsPerUnit = new Dictionary<string, float>();
            var ok = true;

            foreach (var job in FindJobs())
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    // Read the file directly so the source's own import settings don't matter.
                    if (!texture.LoadImage(File.ReadAllBytes(job.Source)))
                    {
                        Debug.LogError($"Could not read {job.Source}.");
                        ok = false;
                        continue;
                    }

                    if (job.Kind == ArtKind.Field)
                    {
                        if (!TryFindTopFace(texture, out var pivot, out var faceWidth))
                        {
                            Debug.LogError($"{job.Source}: couldn't find the field tile.");
                            ok = false;
                            continue;
                        }

                        File.Copy(job.Source, job.Output, true);
                        var fieldPpu = faceWidth / CellWorldWidth / FieldFitScale;
                        pixelsPerUnit[job.Output] = fieldPpu;
                        pivot.x += FieldFitShift * fieldPpu / texture.width;
                        pivot.y -= FieldFitLift * fieldPpu / texture.height;
                        written.Add(new KeyValuePair<ArtJob, Vector2>(job, pivot));
                    }
                    else if (job.Kind == ArtKind.Building)
                    {
                        var outputName = Path.GetFileNameWithoutExtension(job.Output);
                        var fill = FootprintFill.TryGetValue(outputName, out var listedFill) ? listedFill : 1f;
                        var fitted = FitToFootprint(texture, FlatBuildingOutputs.Contains(outputName), fill, out var buildingPivot);
                        if (fitted == null)
                        {
                            Debug.LogError($"{job.Source}: the image is empty.");
                            ok = false;
                            continue;
                        }

                        var building = FitWithin(fitted, BuildingMaxSize);
                        File.WriteAllBytes(job.Output, building.EncodeToPNG());
                        if (building != fitted)
                        {
                            UnityEngine.Object.DestroyImmediate(building);
                        }

                        UnityEngine.Object.DestroyImmediate(fitted);
                        written.Add(new KeyValuePair<ArtJob, Vector2>(job, buildingPivot));
                    }
                    else if (job.Kind == ArtKind.Visitor)
                    {
                        var cropped = CropToArt(texture, VisitorPadding);
                        if (cropped == null)
                        {
                            Debug.LogError($"{job.Source}: the image is empty.");
                            ok = false;
                            continue;
                        }

                        var visitor = FitWithin(cropped, VisitorMaxSize);
                        File.WriteAllBytes(job.Output, visitor.EncodeToPNG());
                        var visitorPadding = VisitorPadding * (float)visitor.height / cropped.height;
                        pixelsPerUnit[job.Output] = (visitor.height - 2f * visitorPadding) / VisitorWorldHeight;
                        written.Add(new KeyValuePair<ArtJob, Vector2>(job, new Vector2(0.5f, visitorPadding / visitor.height)));
                        if (visitor != cropped)
                        {
                            UnityEngine.Object.DestroyImmediate(visitor);
                        }

                        UnityEngine.Object.DestroyImmediate(cropped);
                    }
                    else
                    {
                        var icon = FitWithin(texture, IconMaxSize);
                        File.WriteAllBytes(job.Output, icon.EncodeToPNG());
                        if (icon != texture)
                        {
                            UnityEngine.Object.DestroyImmediate(icon);
                        }

                        written.Add(new KeyValuePair<ArtJob, Vector2>(job, new Vector2(0.5f, 0.5f)));
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var pair in written)
            {
                var kind = pair.Key.Kind;
                var ppu = kind == ArtKind.Field || kind == ArtKind.Visitor
                    ? pixelsPerUnit[pair.Key.Output]
                    : kind == ArtKind.Building ? BuildingPixelsPerUnit : IconPixelsPerUnit;
                ConfigureSprite(pair.Key.Output, pair.Value, ppu, kind == ArtKind.Building);
            }

            if (written.Count > 0)
            {
                SpriteAtlasBuilder.Rebuild();
            }

            return ok;
        }

        /// <summary>
        /// Finds the soil tile's top face. Its left and right corners are the widest part of the
        /// soil; the tile's sides drop straight down from them, so the face's middle is at the
        /// highest row that is (nearly) that wide. Soil is told apart from the crops by colour
        /// (orange-brown); if none is found the whole silhouette is used.
        /// </summary>
        private static bool TryFindTopFace(Texture2D texture, out Vector2 pivot, out float faceWidth)
        {
            pivot = default;
            faceWidth = 0f;
            var pixels = texture.GetPixels32();
            var width = texture.width;
            var height = texture.height;

            if (TryFindWidestRow(pixels, width, height, IsSoil, out var tipY, out var left, out var right) ||
                TryFindWidestRow(pixels, width, height, c => c.a > 128, out tipY, out left, out right))
            {
                faceWidth = right - left + 1f;
                pivot = new Vector2((left + right + 1) * 0.5f / width, (tipY + 0.5f) / height);
                return faceWidth >= width / 4f;
            }

            return false;
        }

        private static bool TryFindWidestRow(
            Color32[] pixels,
            int width,
            int height,
            Func<Color32, bool> solid,
            out int tipY,
            out int tipLeft,
            out int tipRight)
        {
            var rowLeft = new int[height];
            var rowRight = new int[height];
            var maxWidth = 0;
            for (var y = 0; y < height; y++)
            {
                rowLeft[y] = -1;
                for (var x = 0; x < width; x++)
                {
                    if (solid(pixels[y * width + x]))
                    {
                        if (rowLeft[y] < 0)
                        {
                            rowLeft[y] = x;
                        }

                        rowRight[y] = x;
                    }
                }

                if (rowLeft[y] >= 0)
                {
                    maxWidth = Mathf.Max(maxWidth, rowRight[y] - rowLeft[y] + 1);
                }
            }

            // Rows run bottom to top (Texture2D), so search from the top down.
            for (var y = height - 1; y >= 0; y--)
            {
                if (maxWidth > 0 && rowLeft[y] >= 0 && rowRight[y] - rowLeft[y] + 1 >= maxWidth * 0.97f)
                {
                    tipY = y;
                    tipLeft = rowLeft[y];
                    tipRight = rowRight[y];
                    return true;
                }
            }

            tipY = tipLeft = tipRight = -1;
            return false;
        }

        /// <summary>
        /// Makes a copy of a building's art that fits inside its footprint: the smallest diamond
        /// whose two front edges and side corners contain the art (it may rise above the back edges,
        /// since buildings have height). FarmView scales a sprite's full width to the footprint's, so
        /// the copy is exactly that diamond wide, with its middle at the returned pivot. A few stray
        /// pixels may stay past the slanted edges, so a speck there can't shrink the whole building.
        /// A flat building must also fit inside the back edges. A fill below 1 then shrinks the art
        /// around the footprint's middle, by making the transparent canvas wider. Null if it's empty.
        /// </summary>
        private static Texture2D FitToFootprint(Texture2D source, bool flat, float fill, out Vector2 pivot)
        {
            pivot = default;
            var pixels = source.GetPixels32();
            var width = source.width;
            var height = source.height;

            // In pixels, a footprint's edges fall half a row per column. For a diamond whose middle
            // is (cx, cy) and width is W, art stays above its front edges when, for every pixel,
            // y - x/2 and y + x/2 are at least cy - cx/2 - W/4 and cy + cx/2 - W/4.
            var falling = new List<float>();
            var rising = new List<float>();
            var columns = new List<float>();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a > 128)
                    {
                        falling.Add(y - x * 0.5f);
                        rising.Add(y + x * 0.5f);
                        columns.Add(x);
                    }
                }
            }

            if (columns.Count == 0)
            {
                return null;
            }

            falling.Sort();
            rising.Sort();
            columns.Sort();
            var strays = (int)(columns.Count * FootprintStrayShare);
            var lowestFalling = falling[strays];
            var lowestRising = rising[strays];

            // Every column stays: the width covers the whole picture, so nothing is cropped.
            var left = columns[0];
            var right = columns[columns.Count - 1] + 1f;

            float centerX;
            float centerY;
            int fittedWidth;
            if (flat)
            {
                // In these slanted measures the diamond is a square, half as wide as the footprint:
                // centre it on the art's range, and make it as big as the longer range.
                var highestFalling = falling[falling.Count - 1 - strays];
                var highestRising = rising[rising.Count - 1 - strays];
                var middleFalling = (lowestFalling + highestFalling) * 0.5f;
                var middleRising = (lowestRising + highestRising) * 0.5f;
                centerX = middleRising - middleFalling;
                centerY = (middleFalling + middleRising) * 0.5f;
                var slantedWidth = 2f * Mathf.Max(highestFalling - lowestFalling, highestRising - lowestRising);
                fittedWidth = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(slantedWidth, 2f * Mathf.Max(centerX - left, right - centerX))));
            }
            else
            {
                // The front corner is where the two tightest front edges meet.
                centerX = lowestRising - lowestFalling;
                fittedWidth = Mathf.Max(1, Mathf.CeilToInt(2f * Mathf.Max(centerX - left, right - centerX)));
                centerY = (lowestFalling + lowestRising) * 0.5f + fittedWidth * FootprintHalfHeightPerWidth;
            }

            fittedWidth = Mathf.CeilToInt(fittedWidth / fill);
            var shift = Mathf.RoundToInt(fittedWidth * 0.5f - centerX);

            var output = new Color32[fittedWidth * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = Mathf.Max(0, -shift); x < width && x + shift < fittedWidth; x++)
                {
                    output[y * fittedWidth + x + shift] = pixels[y * width + x];
                }
            }

            var fitted = new Texture2D(fittedWidth, height, TextureFormat.RGBA32, false);
            fitted.SetPixels32(output);
            fitted.Apply(false);
            pivot = new Vector2(0.5f, centerY / height);
            return fitted;
        }

        /// <summary>
        /// Copies the art's visible part (alpha over half) with <paramref name="padding"/> clear pixels
        /// round it, so its feet sit at the bottom. Null if nothing is visible.
        /// </summary>
        private static Texture2D CropToArt(Texture2D source, int padding)
        {
            var pixels = source.GetPixels32();
            var width = source.width;
            var height = source.height;
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a > 128)
                    {
                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y);
                    }
                }
            }

            if (maxX < 0)
            {
                return null;
            }

            var outWidth = maxX - minX + 1 + 2 * padding;
            var outHeight = maxY - minY + 1 + 2 * padding;
            var output = new Color32[outWidth * outHeight];
            for (var y = 0; y < outHeight; y++)
            {
                var sy = y + minY - padding;
                for (var x = 0; x < outWidth; x++)
                {
                    var sx = x + minX - padding;
                    if (sx >= 0 && sy >= 0 && sx < width && sy < height)
                    {
                        output[y * outWidth + x] = pixels[sy * width + sx];
                    }
                }
            }

            var cropped = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false);
            cropped.SetPixels32(output);
            cropped.Apply(false);
            return cropped;
        }

        /// <summary>Opaque orange-brown: tilled soil, not green leaves or golden wheat.</summary>
        private static bool IsSoil(Color32 c)
        {
            return c.a > 128 && c.r > 80 && c.r > 1.45f * c.g && c.g >= c.b;
        }

        /// <summary>
        /// Shrinks the image so its longer side is at most <paramref name="maxSize"/>, averaging the
        /// source pixels under each output pixel (with alpha weighting, so edges don't go dark).
        /// Returns the source itself if it is small enough already.
        /// </summary>
        private static Texture2D FitWithin(Texture2D source, int maxSize)
        {
            var longest = Mathf.Max(source.width, source.height);
            if (longest <= maxSize)
            {
                return source;
            }

            var scale = (float)maxSize / longest;
            var outWidth = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            var outHeight = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            var src = source.GetPixels32();
            var output = new Color32[outWidth * outHeight];

            for (var y = 0; y < outHeight; y++)
            {
                var y0 = y * source.height / outHeight;
                var y1 = Mathf.Max(y0 + 1, (y + 1) * source.height / outHeight);
                for (var x = 0; x < outWidth; x++)
                {
                    var x0 = x * source.width / outWidth;
                    var x1 = Mathf.Max(x0 + 1, (x + 1) * source.width / outWidth);
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    var count = 0;
                    for (var sy = y0; sy < y1; sy++)
                    {
                        for (var sx = x0; sx < x1; sx++)
                        {
                            var c = src[sy * source.width + sx];
                            var alpha = c.a / 255f;
                            r += c.r * alpha;
                            g += c.g * alpha;
                            b += c.b * alpha;
                            a += alpha;
                            count++;
                        }
                    }

                    output[y * outWidth + x] = a <= 0f
                        ? new Color32(0, 0, 0, 0)
                        : new Color32(
                            (byte)Mathf.Clamp(Mathf.RoundToInt(r / a), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(g / a), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(b / a), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(a / count * 255f), 0, 255));
                }
            }

            var result = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false);
            result.SetPixels32(output);
            result.Apply(false);
            return result;
        }

        private static void ConfigureSprite(string path, Vector2 pivot, float pixelsPerUnit, bool isBuilding)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"{path} wasn't imported as a texture.");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            // Packed into the sprite atlas, which compresses it; keep the source lossless.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Buildings stay readable so a tap can check it landed on a visible pixel (FarmView.IsOpaqueAt).
            importer.isReadable = isBuilding;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = isBuilding ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spritePixelsPerUnit = pixelsPerUnit;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
