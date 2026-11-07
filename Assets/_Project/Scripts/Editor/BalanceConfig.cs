using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FarmGame.Content;
using UnityEditor;
using UnityEngine;

namespace FarmGame.Editor
{
    /// <summary>
    /// The game's tunable numbers live in small JSON files under Assets/_Project/Config/Balance:
    /// settings.json plus any number of files, each holding whatever sections it likes (see
    /// <see cref="BalanceLoader"/>). This class reads and checks them and copies the values into
    /// the content assets. Edit the files in any text editor; when Unity sees a change it
    /// applies them automatically.
    /// </summary>
    public static class BalanceConfig
    {
        public const string BalanceDirectory = "Assets/_Project/Config/Balance";
        public const string DataDirectory = "Assets/_Project/Resources/Data";

        /// <summary>The single file used before balance was split into a folder.</summary>
        private const string LegacyFilePath = "Assets/_Project/Config/balance.json";

        private static readonly string[] SettingsNotes =
        {
            "Farm Game balance. Every .json file in this folder and its subfolders is read and merged,",
            "so organise them however you like: one file per building is a good default. Save a file and",
            "Unity applies the change automatically (or use Tools > Farm Game > Apply Balance).",
            "Mistakes are reported in the Console with the file they're in, and nothing is changed.",
            "Any file may hold any of: crops, buildings, feeds, products, goodsRecipes.",
            "Times are in seconds (60 = 1 min, 3600 = 1 h). Prices are in coins.",
            "Ids link things together across files. Don't rename ids of things players may already own;",
            "their saves refer to them. Names are free to change.",
            "Building category: Animals, Production, Goods, Fruit or Landmark. Animal: Fish, Chicken, Cow, Goat or empty.",
            "Fruit buildings grow productId by themselves in productionSeconds; tapping one gives harvestAmount and regrows it.",
            "Fish buildings (animal Fish) make no product: the player buys any kind listed in \"fish\" into the pond,",
            "feeds it once, and sells it when grown. fish: cost, unlockLevel, growSeconds, sellPrice (more than cost), sellXp.",
            "Each fish needs art animal_<id>; until it exists the plain fish picture is used.",
            "unlockLevel (crops, buildings, goodsRecipes) is the player level that makes it available (default 1).",
            "Nothing may unlock before what it needs: a recipe before its building or ingredients, an animal before its feed crop.",
            "storage: how many items the farm holds (everything counts), and upgrades. Upgrade n (from 0) costs",
            "upgradeBaseCost * upgradeCostGrowth^n coins, rounded to 100, and adds capacityPerUpgrade.",
            "notices: the notice board's slots (1 to 6), and the seconds an empty slot waits for a new notice",
            "after one is claimed (refillAfterClaimSeconds) or discarded (refillAfterDiscardSeconds). Orders never run out of time.",
            "visitors: townsfolk who ask for goods from goods buildings the player owns. Up to maxVisitors (1 to 4)",
            "at once; a new one comes every arrivalMinSeconds to arrivalMaxSeconds and leaves after staySeconds.",
            "Serving one pays coinMultiplier x the goods' sell value, xpMultiplier x the XP for making them, and",
            "reputationBase + reputationPerItem per item of reputation. Reputation only comes from visitors.",
            "Some visitors sell instead (sellerChance, 0 to 1): bundles of unlocked crops, products and goods at",
            "sellerPriceMultiplier x their sell value. Reputation discounts that, up to maxDiscount at",
            "fullDiscountReputation. sellerPriceMultiplier x (1 - maxDiscount) must stay at least 1 (no resale profit).",
            "land: the chance (0 to 1) of a Land Deed for filling an order (orderDeedChance) or serving a visitor",
            "(visitorDeedChance). 0.05 is 5%. One deed buys one section of the map, next to land already owned.",
            "cocktailBar: the beach bar's line. lineSize customers (1 to 7) each want a juice, for lineSeconds; the next line comes",
            "cooldownSeconds after that. Serving pays coinMultiplier x the juice's sell price and xpMultiplier x the XP for making it.",
            "New crops, buildings or products also need art: until it exists, a stand-in picture is used."
        };

        // ------------------------------------------------------------------ Menu

        [MenuItem("Tools/Farm Game/Apply Balance", false, 10)]
        public static void ApplyMenu()
        {
            if (TryApplyFromFiles(out var message))
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogError(message);
            }
        }

        [MenuItem("Tools/Farm Game/Export Balance", false, 11)]
        public static void ExportMenu()
        {
            var content = LoadDatabase();
            if (content == null)
            {
                Debug.LogError("There is no content database to export yet. Run Tools > Farm Game > Rebuild Everything.");
                return;
            }

            if (Directory.Exists(BalanceDirectory) &&
                !Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    "Export Balance",
                    $"Replace the files in {BalanceDirectory} with the values currently in the game data?",
                    "Replace",
                    "Cancel"))
            {
                return;
            }

            var written = Export(content);
            Debug.Log($"Wrote {written} balance files to {BalanceDirectory}.");
        }

        // ------------------------------------------------------------------ Loading and applying

        /// <summary>Reads, merges and checks every balance file. On failure <paramref name="errors"/> says what's wrong.</summary>
        public static bool TryLoad(out BalanceData data, out List<string> errors)
        {
            return BalanceLoader.TryLoad(BalanceDirectory, out data, out errors);
        }

        /// <summary>Loads, checks and applies the balance. The message says what happened either way.</summary>
        public static bool TryApplyFromFiles(out string message)
        {
            if (!TryLoad(out var data, out var errors))
            {
                message = $"Balance was not applied ({errors.Count} problem{(errors.Count == 1 ? "" : "s")}):\n- " +
                          string.Join("\n- ", errors);
                return false;
            }

            var layout = AssetDatabase.LoadAssetAtPath<FarmLayout>($"{DataDirectory}/FarmLayout.asset");
            if (layout == null)
            {
                message = "Balance was not applied: the farm layout doesn't exist yet. Run Tools > Farm Game > Rebuild Everything.";
                return false;
            }

            var warnings = new List<string>();
            Apply(data, layout, warnings);
            message = warnings.Count == 0
                ? $"Applied balance: {data.crops.Length} crops, {data.buildings.Length} buildings, " +
                  $"{data.feeds.Length} feeds, {data.products.Length} products, {data.goodsRecipes.Length} goods recipes, " +
                  $"{data.fish?.Length ?? 0} fish."
                : "Applied balance with warnings:\n- " + string.Join("\n- ", warnings);
            return true;
        }

        /// <summary>Writes the balance into the content assets, creating any that are new.</summary>
        public static ContentDatabase Apply(BalanceData data, FarmLayout layout, List<string> warnings)
        {
            var crops = new List<CropDefinition>();
            foreach (var entry in data.crops)
            {
                var crop = FindOrCreate<CropDefinition>(entry.id, c => c.Id, $"Crop_{entry.id}");
                var stages = new Sprite[4];
                for (var stage = 0; stage < stages.Length; stage++)
                {
                    // Painted growth stages (Tools > Farm Game > Rebuild Painted Art) win over placeholders.
                    stages[stage] = AssetDatabase.LoadAssetAtPath<Sprite>(FieldArtImporter.OutputPath($"crop_{entry.id}_{stage}"))
                                    ?? LoadSprite($"crop_{entry.id}_{stage}", $"crop_wheat_{stage}", warnings);
                }

                crop.Configure(
                    entry.id,
                    entry.name,
                    LoadSprite($"icon_{entry.id}", "icon_wheat", warnings),
                    stages,
                    entry.growSeconds,
                    entry.seedCost,
                    entry.sellPrice,
                    entry.yield,
                    entry.harvestXp,
                    entry.unlockLevel);
                EditorUtility.SetDirty(crop);
                crops.Add(crop);
            }

            var buildings = new List<BuildingDefinition>();
            foreach (var entry in data.buildings)
            {
                BalanceValidator.TryParseCategory(entry.category, out var category);
                BalanceValidator.TryParseAnimal(entry.animal, out var animal);
                var building = FindOrCreate<BuildingDefinition>(entry.id, b => b.Id, $"Building_{entry.id}");
                building.Configure(
                    entry.id,
                    entry.name,
                    entry.description ?? string.Empty,
                    LoadSprite($"icon_{entry.id}", "icon_build", warnings),
                    LoadSprite($"building_{entry.id}", "building_coop", warnings),
                    new Vector2Int(entry.width, entry.height),
                    entry.cost,
                    category,
                    animal,
                    entry.animalCapacity,
                    entry.animalCost,
                    entry.productId ?? string.Empty,
                    entry.productionSeconds > 0f ? entry.productionSeconds : 300f,
                    entry.unlockLevel,
                    entry.harvestAmount,
                    category == BuildingCategory.Fruit
                        ? LoadSprite($"building_{entry.id}_growing", $"building_{entry.id}", warnings)
                        : null);
                EditorUtility.SetDirty(building);
                buildings.Add(building);
            }

            var feeds = new List<FeedRecipeDefinition>();
            foreach (var entry in data.feeds)
            {
                BalanceValidator.TryParseAnimal(entry.animal, out var animal);
                var feed = FindOrCreate<FeedRecipeDefinition>(entry.id, f => f.Id, $"Feed_{entry.id}");
                feed.Configure(
                    entry.id,
                    entry.name,
                    LoadSprite($"icon_{entry.id}", "icon_feed", warnings),
                    animal,
                    entry.cropId,
                    entry.cropAmount,
                    entry.productionSeconds,
                    entry.outputAmount);
                EditorUtility.SetDirty(feed);
                feeds.Add(feed);
            }

            var products = new List<ProductDefinition>();
            foreach (var entry in data.products)
            {
                var product = FindOrCreate<ProductDefinition>(entry.id, p => p.Id, $"Product_{entry.id}");
                product.Configure(entry.id, entry.name, LoadSprite($"icon_{entry.id}", "icon_inventory", warnings), entry.sellPrice, entry.collectXp);
                EditorUtility.SetDirty(product);
                products.Add(product);
            }

            var recipes = new List<GoodsRecipeDefinition>();
            foreach (var entry in data.goodsRecipes)
            {
                var recipe = FindOrCreate<GoodsRecipeDefinition>(entry.outputId, r => r.Id, $"Recipe_{entry.outputId}");
                recipe.Configure(
                    entry.outputId,
                    entry.buildingId,
                    entry.inputs,
                    entry.productionSeconds,
                    entry.outputAmount,
                    entry.unlockLevel);
                EditorUtility.SetDirty(recipe);
                recipes.Add(recipe);
            }

            var fish = new List<FishDefinition>();
            foreach (var entry in data.fish ?? Array.Empty<FishBalance>())
            {
                var kind = FindOrCreate<FishDefinition>(entry.id, f => f.Id, $"Fish_{entry.id}");
                kind.Configure(
                    entry.id,
                    entry.name,
                    LoadSprite($"animal_{entry.id}", "animal_fish", warnings),
                    entry.cost,
                    entry.unlockLevel,
                    entry.growSeconds,
                    entry.sellPrice,
                    entry.sellXp);
                EditorUtility.SetDirty(kind);
                fish.Add(kind);
            }

            var content = LoadDatabase();
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<ContentDatabase>();
                AssetDatabase.CreateAsset(content, $"{DataDirectory}/ContentDatabase.asset");
            }

            content.Configure(crops, layout, data.startingCoins, buildings, feeds, products, recipes);
            content.ConfigureFish(fish);
            content.ConfigureStorage(data.storage);
            content.ConfigureNotices(data.notices);
            content.ConfigureVisitors(data.visitors);
            content.ConfigureLand(data.land);
            content.ConfigureCocktailBar(data.cocktailBar);
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
            return content;
        }

        // ------------------------------------------------------------------ Exporting

        /// <summary>
        /// Writes the content database out as a balance folder, one file per feature:
        /// settings.json, crops.json, and a file per building under animals/, production/,
        /// goods/ and landmarks/ holding the building and everything that belongs to it.
        /// Anything not tied to a building goes to misc.json. Returns the number of files.
        /// </summary>
        public static int Export(ContentDatabase content)
        {
            var files = new Dictionary<string, BalanceFile>();
            BalanceFile FileFor(string path)
            {
                if (!files.TryGetValue(path, out var file))
                {
                    file = new BalanceFile();
                    files[path] = file;
                }

                return file;
            }

            var placedProducts = new HashSet<string>();
            var placedFeeds = new HashSet<string>();
            var fishPlaced = false;

            FileFor("crops.json").crops = content.Crops.Where(c => c != null).Select(ToBalance).ToArray();

            foreach (var building in content.Buildings.Where(b => b != null))
            {
                var file = FileFor($"{FolderFor(building.Category)}/{building.Id}.json");
                file.buildings = Append(file.buildings, ToBalance(building));

                if (building.CanHouseAnimals)
                {
                    // An animal building's file also holds what its animals eat and make.
                    var feed = content.GetFeedRecipeFor(building.AnimalKind);
                    if (feed != null && placedFeeds.Add(feed.Id))
                    {
                        file.feeds = Append(file.feeds, ToBalance(feed));
                    }

                    var product = content.GetProduct(building.ProductId);
                    if (product != null && placedProducts.Add(product.Id))
                    {
                        file.products = Append(file.products, ToBalance(product));
                    }

                    // The (first) fish pond's file also lists the fish that can be bought.
                    if (building.SellsAnimals && !fishPlaced)
                    {
                        file.fish = content.Fish.Where(f => f != null).Select(ToBalance).ToArray();
                        fishPlaced = true;
                    }
                }
                else if (building.IsFruitTree)
                {
                    // A fruit tree's file also holds its fruit.
                    var fruit = content.GetProduct(building.ProductId);
                    if (fruit != null && placedProducts.Add(fruit.Id))
                    {
                        file.products = Append(file.products, ToBalance(fruit));
                    }
                }
                else if (building.Category == BuildingCategory.Goods)
                {
                    // A goods building's file holds its recipes and the goods they make.
                    foreach (var recipe in content.GetGoodsRecipesFor(building.Id))
                    {
                        file.goodsRecipes = Append(file.goodsRecipes, ToBalance(recipe));
                        var product = content.GetProduct(recipe.OutputId);
                        if (product != null && placedProducts.Add(product.Id))
                        {
                            file.products = Append(file.products, ToBalance(product));
                        }
                    }
                }
            }

            // Anything left over (a feed for an animal with no building, a recipe for a missing
            // building, a product nothing makes) still gets written, so nothing is lost.
            var misc = new BalanceFile();
            misc.feeds = content.FeedRecipes.Where(f => f != null && !placedFeeds.Contains(f.Id)).Select(ToBalance).ToArray();
            misc.products = content.Products.Where(p => p != null && !placedProducts.Contains(p.Id)).Select(ToBalance).ToArray();
            misc.goodsRecipes = content.GoodsRecipes
                .Where(r => r != null && content.GetBuilding(r.BuildingId) == null)
                .Select(ToBalance)
                .ToArray();
            if (!fishPlaced)
            {
                misc.fish = content.Fish.Where(f => f != null).Select(ToBalance).ToArray();
            }

            if (!misc.IsEmpty)
            {
                files["misc.json"] = misc;
            }

            // Replace the folder's contents so removed items don't linger in old files.
            BalanceWatcher.Suppress = true;
            try
            {
                if (Directory.Exists(BalanceDirectory))
                {
                    foreach (var old in Directory.GetFiles(BalanceDirectory, "*.json", SearchOption.AllDirectories))
                    {
                        AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
                    }
                }

                WriteJson(BalanceLoader.SettingsFileName, new BalanceSettings
                {
                    notes = SettingsNotes,
                    startingCoins = content.StartingCoins,
                    storage = content.Storage.Clone(),
                    notices = content.Notices.Clone(),
                    visitors = content.Visitors.Clone(),
                    land = content.Land.Clone(),
                    cocktailBar = content.CocktailBar.Clone()
                });
                foreach (var pair in files)
                {
                    WriteJson(pair.Key, pair.Value);
                }

                if (File.Exists(LegacyFilePath))
                {
                    AssetDatabase.DeleteAsset(LegacyFilePath);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                BalanceWatcher.Suppress = false;
            }

            return files.Count + 1;
        }

        private static void WriteJson(string relativePath, object value)
        {
            var path = $"{BalanceDirectory}/{relativePath}";
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? BalanceDirectory);
            File.WriteAllText(path, JsonUtility.ToJson(value, true) + "\n");
        }

        private static string FolderFor(BuildingCategory category)
        {
            switch (category)
            {
                case BuildingCategory.Animals:
                    return "animals";
                case BuildingCategory.Goods:
                    return "goods";
                case BuildingCategory.Landmark:
                    return "landmarks";
                case BuildingCategory.Fruit:
                    return "fruit";
                default:
                    return "production";
            }
        }

        private static T[] Append<T>(T[] array, T item)
        {
            var list = array == null ? new List<T>() : new List<T>(array);
            list.Add(item);
            return list.ToArray();
        }

        private static CropBalance ToBalance(CropDefinition c)
        {
            return new CropBalance
            {
                id = c.Id,
                name = c.DisplayName,
                growSeconds = c.GrowSeconds,
                seedCost = c.SeedCost,
                sellPrice = c.SellPrice,
                yield = c.YieldAmount,
                harvestXp = c.HarvestXp,
                unlockLevel = c.UnlockLevel
            };
        }

        private static BuildingBalance ToBalance(BuildingDefinition b)
        {
            return new BuildingBalance
            {
                id = b.Id,
                name = b.DisplayName,
                description = b.Description,
                category = b.Category.ToString(),
                width = b.Footprint.x,
                height = b.Footprint.y,
                cost = b.Cost,
                unlockLevel = b.UnlockLevel,
                animal = b.AnimalKind == AnimalKind.None ? string.Empty : b.AnimalKind.ToString(),
                animalCapacity = b.AnimalCapacity,
                animalCost = b.AnimalCost,
                productId = b.ProductId,
                productionSeconds = (b.CanHouseAnimals && !b.SellsAnimals) || b.IsFruitTree ? b.ProductionSeconds : 0f,
                harvestAmount = b.IsFruitTree ? b.HarvestAmount : 0
            };
        }

        private static FeedBalance ToBalance(FeedRecipeDefinition f)
        {
            return new FeedBalance
            {
                id = f.Id,
                name = f.DisplayName,
                animal = f.Animal.ToString(),
                cropId = f.CropId,
                cropAmount = f.CropAmount,
                productionSeconds = f.ProductionSeconds,
                outputAmount = f.OutputAmount
            };
        }

        private static ProductBalance ToBalance(ProductDefinition p)
        {
            return new ProductBalance { id = p.Id, name = p.DisplayName, sellPrice = p.SellPrice, collectXp = p.CollectXp };
        }

        private static FishBalance ToBalance(FishDefinition f)
        {
            return new FishBalance
            {
                id = f.Id,
                name = f.DisplayName,
                cost = f.Cost,
                unlockLevel = f.UnlockLevel,
                growSeconds = f.GrowSeconds,
                sellPrice = f.SellPrice,
                sellXp = f.SellXp
            };
        }

        private static GoodsRecipeBalance ToBalance(GoodsRecipeDefinition r)
        {
            return new GoodsRecipeBalance
            {
                outputId = r.OutputId,
                buildingId = r.BuildingId,
                productionSeconds = r.ProductionSeconds,
                outputAmount = r.OutputAmount,
                unlockLevel = r.UnlockLevel,
                inputs = r.Inputs.ToArray()
            };
        }

        // ------------------------------------------------------------------ Helpers

        public static ContentDatabase LoadDatabase()
        {
            return AssetDatabase.LoadAssetAtPath<ContentDatabase>($"{DataDirectory}/ContentDatabase.asset");
        }

        /// <summary>
        /// The existing asset with this id, wherever it is in the data folder (so renaming an
        /// item updates its asset rather than making a second one), or a new one.
        /// </summary>
        private static T FindOrCreate<T>(string id, Func<T, string> getId, string fileName) where T : ScriptableObject
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataDirectory }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && getId(asset) == id)
                {
                    return asset;
                }
            }

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath($"{DataDirectory}/{fileName}.asset"));
            return created;
        }

        /// <summary>A generated sprite, or a stand-in (with a warning) when there's no art for it yet.</summary>
        private static Sprite LoadSprite(string name, string fallbackName, List<string> warnings)
        {
            // Painted icons (Tools > Farm Game > Rebuild Painted Art) win over placeholders.
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FieldArtImporter.IconOutputPath(name))
                         ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaceholderArtGenerator.ArtDirectory}/{name}.png");
            if (sprite != null)
            {
                return sprite;
            }

            warnings.Add($"No art for \"{name}\" yet; using \"{fallbackName}\" for now.");
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaceholderArtGenerator.ArtDirectory}/{fallbackName}.png");
        }
    }

    /// <summary>Applies the balance whenever Unity imports, deletes or moves a balance file.</summary>
    internal sealed class BalanceWatcher : AssetPostprocessor
    {
        /// <summary>Set while the editor itself writes the files (Export), so they aren't re-applied.</summary>
        internal static bool Suppress;

        private static bool _pending;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Suppress || Application.isBatchMode || _pending)
            {
                return;
            }

            if (!Touches(importedAssets) && !Touches(deletedAssets) && !Touches(movedAssets) && !Touches(movedFromAssetPaths))
            {
                return;
            }

            // Assets can't be changed safely during an import, so wait for the next editor tick.
            // Saving several files at once applies them together.
            _pending = true;
            EditorApplication.delayCall += ApplyWhenReady;
        }

        private static bool Touches(string[] paths)
        {
            foreach (var path in paths)
            {
                if (path.StartsWith(BalanceConfig.BalanceDirectory + "/", StringComparison.Ordinal) &&
                    path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyWhenReady()
        {
            _pending = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("Balance files changed. They will be applied when you leave Play mode.");
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                return;
            }

            BalanceConfig.ApplyMenu();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            BalanceConfig.ApplyMenu();
        }
    }
}
