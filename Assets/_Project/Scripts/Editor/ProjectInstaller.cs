using System;
using System.Collections.Generic;
using System.IO;
using FarmGame.Bootstrap;
using FarmGame.Content;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarmGame.Editor
{
    /// <summary>
    /// Builds the project from scratch: generates the placeholder art, applies the game's
    /// numbers from the files in Assets/_Project/Config/Balance (see <see cref="BalanceConfig"/>),
    /// lays out the farm and creates the scene.
    /// </summary>
    public static class ProjectInstaller
    {
        private const string DataDirectory = BalanceConfig.DataDirectory;
        private const string ScenesDirectory = "Assets/_Project/Scenes";
        private const string FarmScenePath = ScenesDirectory + "/Farm.unity";

        [MenuItem("Tools/Farm Game/Rebuild Everything", false, 0)]
        public static void RebuildEverythingMenu()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildAll();
        }

        [MenuItem("Tools/Farm Game/Regenerate Placeholder Art", false, 20)]
        public static void RegenerateArt()
        {
            PlaceholderArtGenerator.GenerateAll();
            AssetDatabase.Refresh();
            SpriteAtlasBuilder.Rebuild();
        }

        [MenuItem("Tools/Farm Game/Build Windows", false, 40)]
        public static void BuildWindows()
        {
            var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows");
            Directory.CreateDirectory(outputDirectory);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { FarmScenePath },
                locationPathName = Path.Combine(outputDirectory, "HayDay.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows build failed: {report.summary.result} " +
                    $"({report.summary.totalErrors} errors).");
            }

            Debug.Log(
                $"Windows build succeeded: {report.summary.outputPath} " +
                $"({report.summary.totalSize} bytes).");
        }

        public static void BuildAll()
        {
            try
            {
                Debug.Log("Farm Game setup starting...");

                // Check the balance first so a typo doesn't cost a full art rebuild.
                if (!BalanceConfig.TryLoad(out var balance, out var errors))
                {
                    throw new InvalidOperationException(
                        $"The balance files in {BalanceConfig.BalanceDirectory} have problems:\n- " + string.Join("\n- ", errors));
                }

                PlaceholderArtGenerator.GenerateAll();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                SpriteAtlasBuilder.Rebuild();

                EnsureFolder(DataDirectory);
                EnsureFolder(ScenesDirectory);

                var layout = CreateOrLoadAsset<FarmLayout>($"{DataDirectory}/FarmLayout.asset");
                var plotCells = new List<Vector2Int>();
                for (var y = 16; y <= 18; y++)
                {
                    for (var x = 16; x <= 18; x++)
                    {
                        plotCells.Add(new Vector2Int(x, y));
                    }
                }

                // A 36 x 36 map; a new farm owns the 12 x 12 in the middle.
                layout.Configure(36, 36, plotCells, new RectInt(12, 12, 12, 12), FarmLayout.DefaultSectionSize);
                EditorUtility.SetDirty(layout);

                var warnings = new List<string>();
                var content = BalanceConfig.Apply(balance, layout, warnings);
                foreach (var warning in warnings)
                {
                    Debug.LogWarning(warning);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                ApplyPlayerSettings();
                BuildFarmScene();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject = content;
                Debug.Log(
                    "Farm Game setup complete. Open Assets/_Project/Scenes/Farm.unity and press Play. " +
                    "Press T in Play mode to advance crop growth by 60 seconds.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        private static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.useAnimatedAutorotation = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
        }

        private static void BuildFarmScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameObject = new GameObject("Game");
            gameObject.AddComponent<GameBootstrap>();

            if (!EditorSceneManager.SaveScene(scene, FarmScenePath))
            {
                throw new InvalidOperationException($"Could not save scene at {FarmScenePath}.");
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(FarmScenePath, true)
            };
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
    }
}
