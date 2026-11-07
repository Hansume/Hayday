using System;
using System.Collections.Generic;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Economy;
using FarmGame.Farming;
using FarmGame.Inventory;
using FarmGame.Land;
using FarmGame.Missions;
using FarmGame.Progression;
using FarmGame.Save;
using FarmGame.Visitors;
using UnityEngine;

namespace FarmGame.Core
{
    /// <summary>
    /// The running game: every player action goes through here, and every action that changes
    /// the farm is saved. Split by area:
    /// <list type="bullet">
    /// <item>GameSession.cs: setup, notices, unlocks, fields, selling, storage, placing and moving.</item>
    /// <item>GameSession.Production.cs: the Feed Grinder and goods buildings.</item>
    /// <item>GameSession.Animals.cs: adding, feeding and collecting from animals.</item>
    /// <item>GameSession.Fruit.cs: picking fruit from trees.</item>
    /// <item>GameSession.Persistence.cs: saving and restoring.</item>
    /// </list>
    /// </summary>
    public sealed partial class GameSession
    {
        public const int NewSlotCost = 100;

        public DateTime UtcNow => _clock.UtcNow;

        private readonly ContentDatabase _content;
        private readonly IClock _clock;
        private readonly SaveService _saveService;

        public Wallet Wallet { get; }
        public PlayerLevel Level { get; }
        public InventoryService Inventory { get; }
        public Storage Storage { get; }
        public FarmSimulation Farm { get; }
        public BuildingService Buildings { get; }
        public MissionService Missions { get; }
        public VisitorService Visitors { get; }

        /// <summary>The Cocktail Bar on the beach and its line of customers.</summary>
        public CocktailBarService CocktailBar { get; }

        /// <summary>Which land the player owns, and their Land Deeds.</summary>
        public FarmLand Land { get; }

        /// <summary>The second currency, earned only by helping visitors.</summary>
        public Reputation Reputation { get; }

        public ContentDatabase Content => _content;

        public GameSession(
            ContentDatabase content,
            IClock clock,
            SaveService saveService,
            SaveDataV1 saveData)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _saveService = saveService;

            if (_content.Layout == null)
            {
                throw new InvalidOperationException("Content database has no farm layout.");
            }

            Farm = new FarmSimulation(_clock, _content, _content.Layout);
            Wallet = new Wallet(saveData != null ? saveData.coins : _content.StartingCoins);
            Level = saveData != null ? new PlayerLevel(saveData.level, saveData.xp) : new PlayerLevel();
            Inventory = new InventoryService();
            Storage = new Storage(_content.Storage, Inventory, saveData != null ? saveData.storageUpgrades : 0);
            Land = CreateLand(saveData);
            Buildings = new BuildingService(_content, Land);
            Missions = new MissionService(_content, Buildings, () => Level.Level, () => _clock.UtcNow.Ticks);
            Visitors = new VisitorService(_content, Buildings, () => Level.Level, () => _clock.UtcNow.Ticks);
            CocktailBar = new CocktailBarService(_content, () => Level.Level,() => _clock.UtcNow.Ticks);
            Reputation = new Reputation(saveData != null ? saveData.reputation : 0);

            if (saveData != null)
            {
                Restore(saveData);
            }
            else
            {
                // A brand-new farm: the first visitor comes after a short wait.
                Visitors.RestoreState(_clock.UtcNow.Ticks + Visitors.Settings.ArrivalMin.Ticks, 0, 0);
            }

            EnsureNoticeBoard();

            // New notices and visitors look at what the player owns, so post them after
            // restoring. This also clears out visitors whose time ran out while the game was
            // closed. Notices stay until the player fills or discards them.
            Missions.Update();
            Visitors.Update();
            CocktailBar.Update();
        }

        // ------------------------------------------------------------------ Notice board

        public const string NoticeBoardBuildingId = "notice_board";

        /// <summary>Where the board goes on a new farm, if that spot is free: measured from the corner of the start area.</summary>
        public static readonly Vector2Int NoticeBoardCell = new Vector2Int(10, 2);

        public PlacedBuilding NoticeBoard
        {
            get
            {
                for (var i = 0; i < Buildings.Placed.Count; i++)
                {
                    if (Buildings.Placed[i].Definition.Id == NoticeBoardBuildingId)
                    {
                        return Buildings.Placed[i];
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Every farm has one notice board. It's placed for free at its usual spot, or the
        /// nearest free cell if something is already there.
        /// </summary>
        private void EnsureNoticeBoard()
        {
            var board = _content.GetBuilding(NoticeBoardBuildingId);
            if (board == null || NoticeBoard != null)
            {
                return;
            }

            for (var radius = 0; radius < Math.Max(_content.Layout.Width, _content.Layout.Height); radius++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        {
                            continue;
                        }

                        var cell = _content.Layout.StartArea.min + NoticeBoardCell + new Vector2Int(dx, dy);
                        if (Buildings.RestorePlacement(NoticeBoardBuildingId, cell))
                        {
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>Posts new notices as time passes. Call regularly (the HUD does).</summary>
        public void UpdateMissions()
        {
            if (Missions.Update())
            {
                SaveSoon();
            }
        }

        /// <summary>Whether storage holds everything this order asks for.</summary>
        public bool CanDeliverMission(Mission mission)
        {
            if (mission == null)
            {
                return false;
            }

            for (var i = 0; i < mission.Requirements.Count; i++)
            {
                var requirement = mission.Requirements[i];
                if (Inventory.GetCount(requirement.ItemId) < requirement.Required)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True while any order on the board can be filled from storage.</summary>
        public bool HasDeliverableMission
        {
            get
            {
                for (var i = 0; i < Missions.SlotCount; i++)
                {
                    if (CanDeliverMission(Missions.GetSlot(i)))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Fills an order: takes its crops and products from storage and pays its coins and XP.
        /// Nothing is taken unless everything is there. The slot refills after a wait.
        /// </summary>
        public bool TryDeliverMission(int index, out string message)
        {
            var mission = Missions.GetSlot(index);
            if (mission == null)
            {
                message = "No order there";
                return false;
            }

            if (!CanDeliverMission(mission))
            {
                var missing = new List<string>();
                for (var i = 0; i < mission.Requirements.Count; i++)
                {
                    var requirement = mission.Requirements[i];
                    var shortBy = requirement.Required - Inventory.GetCount(requirement.ItemId);
                    if (shortBy > 0)
                    {
                        missing.Add($"{shortBy} {_content.GetItemDisplayName(requirement.ItemId)}");
                    }
                }

                message = $"Need {string.Join(", ", missing)} more";
                return false;
            }

            for (var i = 0; i < mission.Requirements.Count; i++)
            {
                Inventory.TryRemove(mission.Requirements[i].ItemId, mission.Requirements[i].Required);
            }

            Missions.TryClaim(index, out _);
            Wallet.Add(mission.RewardCoins);
            Level.AddXp(mission.RewardXp);
            message = $"+{FormatCoins(mission.RewardCoins)} +{mission.RewardXp} XP{RollForDeed(_content.Land.orderDeedChance)}";
            SaveSoon();
            return true;
        }

        /// <summary>Tears an order off the board. A new one is posted after a wait.</summary>
        public bool TryDiscardMission(int index, out string message)
        {
            if (!Missions.TryDiscard(index))
            {
                message = "No order there";
                return false;
            }

            message = "Order discarded";
            SaveSoon();
            return true;
        }

        // ------------------------------------------------------------------ Unlocks
        //
        // Crops, buildings and goods recipes each have an unlock level. Until the player reaches
        // it they are shown locked and can't be planted, bought or made. Things already owned
        // (from an older save, say) keep working.

        public bool IsUnlocked(int unlockLevel)
        {
            return Level.Level >= unlockLevel;
        }

        public bool IsUnlocked(CropDefinition crop)
        {
            return crop != null && IsUnlocked(crop.UnlockLevel);
        }

        public bool IsUnlocked(BuildingDefinition building)
        {
            return building != null && IsUnlocked(building.UnlockLevel);
        }

        public bool IsUnlocked(GoodsRecipeDefinition recipe)
        {
            return recipe != null && IsUnlocked(recipe.UnlockLevel);
        }

        public static string LockedMessage(string name, int unlockLevel)
        {
            return $"Unlocks at level {unlockLevel}";
        }

        // ------------------------------------------------------------------ Fields

        public bool TryPlant(int plotId, string cropId, out string message)
        {
            var crop = _content.GetCrop(cropId);
            if (crop == null)
            {
                message = "Select a crop first.";
                return false;
            }

            if (!IsUnlocked(crop))
            {
                message = LockedMessage(crop.DisplayName, crop.UnlockLevel);
                return false;
            }

            if (!Farm.TryPlant(plotId, crop, Wallet, out message))
            {
                return false;
            }

            message = $"Planted {crop.DisplayName} for {FormatCoins(crop.SeedCost)}.";
            SaveSoon();
            return true;
        }

        public bool TryHarvest(int plotId, out HarvestOutcome outcome, out string message)
        {
            return TryHarvest(plotId, out outcome, out _, out message);
        }

        /// <summary>Harvests a ready plot. <paramref name="xpGained"/> is the XP the harvest gave.</summary>
        public bool TryHarvest(int plotId, out HarvestOutcome outcome, out int xpGained, out string message)
        {
            xpGained = 0;
            var snapshot = Farm.GetSnapshot(plotId);
            if (snapshot.IsReady && snapshot.Crop != null && !Storage.CanFit(snapshot.Crop.YieldAmount))
            {
                outcome = default;
                message = Storage.FullMessage;
                return false;
            }

            if (!Farm.TryHarvest(plotId, Inventory, out outcome, out message))
            {
                return false;
            }

            var crop = _content.GetCrop(outcome.CropId);
            xpGained = crop != null ? crop.HarvestXp : 0;
            Level.AddXp(xpGained);
            SaveSoon();
            return true;
        }

        // ------------------------------------------------------------------ Storage and selling

        /// <summary>Buys the next storage upgrade with coins.</summary>
        public bool TryUpgradeStorage(out string message)
        {
            if (!Storage.TryUpgrade(Wallet, out message))
            {
                return false;
            }

            SaveSoon();
            return true;
        }

        /// <summary>Sells every unit of a crop or animal product.</summary>
        public bool TrySell(string itemId, out int totalCoins, out int quantity, out string message)
        {
            return TrySell(itemId, int.MaxValue, out totalCoins, out quantity, out message);
        }

        /// <summary>
        /// Sells up to <paramref name="amount"/> of a crop or animal product (fewer if the
        /// player has fewer). <paramref name="quantity"/> is how many were actually sold.
        /// </summary>
        public bool TrySell(string itemId, int amount, out int totalCoins, out int quantity, out string message)
        {
            totalCoins = 0;
            quantity = 0;

            if (!TryGetSellPrice(itemId, out var displayName, out var sellPrice))
            {
                message = "Unknown item.";
                return false;
            }

            if (amount <= 0)
            {
                message = "Choose how many to sell.";
                return false;
            }

            quantity = Math.Min(amount, Inventory.GetCount(itemId));
            if (quantity <= 0 || !Inventory.TryRemove(itemId, quantity))
            {
                quantity = 0;
                message = $"No {displayName} to sell.";
                return false;
            }

            totalCoins = (int)Math.Min((long)quantity * sellPrice, int.MaxValue);
            Wallet.Add(totalCoins);
            message = $"Sold {quantity} {displayName} for {FormatCoins(totalCoins)}.";
            SaveSoon();
            return true;
        }

        /// <summary>The name and price of one unit of something sellable: a crop or a product.</summary>
        public bool TryGetSellPrice(string itemId, out string displayName, out int sellPrice)
        {
            var crop = _content.GetCrop(itemId);
            if (crop != null)
            {
                displayName = crop.DisplayName;
                sellPrice = crop.SellPrice;
                return true;
            }

            var product = _content.GetProduct(itemId);
            if (product != null)
            {
                displayName = product.DisplayName;
                sellPrice = product.SellPrice;
                return true;
            }

            displayName = null;
            sellPrice = 0;
            return false;
        }

        // ------------------------------------------------------------------ Placing field slots

        public bool CanPlaceSlot(Vector2Int cell, out string reason)
        {
            if (Farm.IsPlotCell(cell))
            {
                reason = "That spot already has a field plot.";
                return false;
            }

            if (!Farm.CanAddPlot(cell))
            {
                reason = "That slot does not fit on the farm.";
                return false;
            }

            if (!Land.IsCellOwned(cell))
            {
                reason = "Land locked";
                return false;
            }

            if (Buildings.IsOccupied(cell))
            {
                reason = "Something is already there.";
                return false;
            }

            reason = null;
            return true;
        }

        public bool TryPlaceSlot(Vector2Int cell, out int plotId, out string message)
        {
            plotId = -1;
            if (!CanPlaceSlot(cell, out message))
            {
                return false;
            }

            if (!Wallet.TrySpend(NewSlotCost))
            {
                message = $"Need {FormatCoins(NewSlotCost)}";
                return false;
            }

            if (!Farm.TryAddPlot(cell, out plotId))
            {
                Wallet.Add(NewSlotCost);
                message = "That slot could not be added.";
                return false;
            }

            Buildings.AddPlotCell(cell);
            message = "New field added.";
            SaveSoon();
            return true;
        }

        // ------------------------------------------------------------------ Placing and moving buildings

        public bool CanPlaceBuilding(string buildingId, Vector2Int origin, out string reason)
        {
            var building = _content.GetBuilding(buildingId);
            if (building == null)
            {
                reason = "Unknown building.";
                return false;
            }

            if (!IsUnlocked(building))
            {
                reason = LockedMessage(building.DisplayName, building.UnlockLevel);
                return false;
            }

            return Buildings.CanPlace(building, origin, out reason);
        }

        public bool TryPlaceBuilding(string buildingId, Vector2Int origin, out string message)
        {
            var building = _content.GetBuilding(buildingId);
            if (building == null)
            {
                message = "Unknown building.";
                return false;
            }

            if (!IsUnlocked(building))
            {
                message = LockedMessage(building.DisplayName, building.UnlockLevel);
                return false;
            }

            if (!Buildings.TryPlace(building, origin, Wallet, out var placed, out message))
            {
                return false;
            }

            if (building.IsFruitTree)
            {
                StartGrowingFruit(placed);
            }

            message = $"{building.DisplayName} placed.";
            SaveSoon();
            return true;
        }

        /// <summary>Whether a placed building could move so its origin is <paramref name="origin"/>.</summary>
        public bool CanMoveBuilding(PlacedBuilding building, Vector2Int origin, out string reason)
        {
            return Buildings.CanMove(building, origin, out reason);
        }

        /// <summary>
        /// Moves a placed building for free. Its animals, production queue and everything else
        /// go with it.
        /// </summary>
        public bool TryMoveBuilding(PlacedBuilding building, Vector2Int origin, out string message)
        {
            if (!Buildings.TryMove(building, origin, out message))
            {
                return false;
            }

            message = $"Moved {building.Definition.DisplayName}.";
            SaveSoon();
            return true;
        }

        // ------------------------------------------------------------------ Saving

        /// <summary>
        /// When true, actions only mark the game as changed and the owner writes the save once
        /// per frame with <see cref="FlushPendingSave"/>. This stops a drag across many plots
        /// from writing the save file once per plot. When false (the default), actions save
        /// immediately.
        /// </summary>
        public bool DeferSaves { get; set; }

        public bool HasUnsavedChanges { get; private set; }

        /// <summary>Writes the save if anything changed since the last save.</summary>
        public bool FlushPendingSave()
        {
            return HasUnsavedChanges && SaveNow();
        }

        private void SaveSoon()
        {
            if (DeferSaves)
            {
                HasUnsavedChanges = true;
                return;
            }

            SaveNow();
        }

        public bool SaveNow()
        {
            HasUnsavedChanges = false;
            if (_saveService == null)
            {
                return false;
            }

            if (!_saveService.Save(BuildSaveData(), out var error))
            {
                Debug.LogWarning($"Could not save farm: {error}");
                return false;
            }

            return true;
        }

        public void DebugAdvanceAll(double seconds)
        {
            Farm.DebugAdvanceAll(seconds);

            if (seconds <= 0d)
            {
                return;
            }

            var ticks = SecondsToTicks(seconds);
            for (var i = 0; i < Buildings.Placed.Count; i++)
            {
                Buildings.Placed[i].AdvanceProduction(ticks);
            }

            Missions.DebugAdvance(ticks);
            UpdateMissions();
            Visitors.DebugAdvance(ticks);
            UpdateVisitors();
            CocktailBar.DebugAdvance(ticks);
            UpdateCocktailBar();
        }

        // ------------------------------------------------------------------ Helpers

        private static long SecondsToTicks(double seconds)
        {
            return (long)(seconds * TimeSpan.TicksPerSecond);
        }

        private static double TicksToSeconds(long ticks)
        {
            return ticks / (double)TimeSpan.TicksPerSecond;
        }

        private static string FormatCoins(int amount)
        {
            return amount == 1 ? "1 coin" : $"{amount} coins";
        }

        private static string FormatXpSuffix(int xp)
        {
            return xp > 0 ? $" +{xp} XP" : string.Empty;
        }
    }
}
