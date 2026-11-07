using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace FarmGame.Editor
{
    /// <summary>
    /// Packs the game's sprites into one atlas, so the farm and the UI draw in a handful of
    /// batches instead of one draw call per sprite.
    ///
    /// Left out on purpose:
    /// - building_*: taps read their pixels to ignore clicks on transparent areas, which needs
    ///   each building's own readable texture (see FarmView.IsOpaqueAt).
    /// - pen_*: large backgrounds shown one at a time in the animal pen window; packing them
    ///   would make the atlas much bigger for no batching gain.
    /// - beach_*: large tiled ground at the Beach, for the same reason. Tiling needs whole sprites too.
    ///
    /// Anything that rewrites packed sprites must call <see cref="Rebuild"/> afterwards: until the
    /// atlas is repacked, those sprites have no texture in Play mode and draw nothing. The art
    /// importers and generators do this themselves; Tools > Farm Game > Rebuild Sprite Atlas does it
    /// by hand.
    /// </summary>
    public static class SpriteAtlasBuilder
    {
        public const string AtlasPath = "Assets/_Project/Art/Atlases/Farm.spriteatlasv2";

        private static readonly string[] SourceFolders =
        {
            "Assets/_Project/Resources/Art/Placeholders",
            "Assets/_Project/Resources/Art/Ground",
            "Assets/_Project/Resources/Art/Field",
            "Assets/_Project/Resources/Art/Icons"
        };

        private static readonly string[] ExcludedPrefixes = { "building_", "pen_", "beach_" };

        [MenuItem("Tools/Farm Game/Rebuild Sprite Atlas", false, 30)]
        public static void Rebuild()
        {
            // Pack atlases in Play mode and builds.
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;

            Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));

            var textures = new List<UnityEngine.Object>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", SourceFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (IsExcluded(name))
                {
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    textures.Add(texture);
                }
            }

            // A fresh atlas each time, so removed art drops out. Saving over the same path keeps its GUID.
            var atlas = new SpriteAtlasAsset();
            atlas.Add(textures.ToArray());
            SpriteAtlasAsset.Save(atlas, AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(AtlasPath) as SpriteAtlasImporter;
            if (importer == null)
            {
                Debug.LogError($"Could not configure the sprite atlas at {AtlasPath}.");
                return;
            }

            importer.includeInBuild = true;
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                padding = 4,
                enableRotation = false,
                // Tight packing breaks 9-sliced UI sprites and Sprite.textureRect.
                enableTightPacking = false,
                enableAlphaDilation = true
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear
            };
            importer.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = "DefaultTexturePlatform",
                maxTextureSize = 4096,
                format = TextureImporterFormat.Automatic,
                textureCompression = TextureImporterCompression.CompressedHQ
            });
            importer.SaveAndReimport();

            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);
            Debug.Log($"Sprite atlas rebuilt with {textures.Count} textures: {AtlasPath}");
        }

        private static bool IsExcluded(string name)
        {
            foreach (var prefix in ExcludedPrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
