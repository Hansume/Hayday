using System;
using System.Collections.Generic;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Farming;
using FarmGame.Inventory;

namespace FarmGame.Core
{
    /// <summary>
    /// Production buildings: the Feed Grinder (crops into animal feed) and goods buildings
    /// (category Goods, ingredients into products). Both work the same way: starting a job takes
    /// its ingredients now and queues it, up to <see cref="FeedQueueCapacity"/> jobs, each starting
    /// when the one before it finishes. Collecting moves finished jobs into storage, oldest first.
    /// </summary>
    public sealed partial class GameSession
    {
        public const string FeedGrinderBuildingId = "feed_grinder";

        // Ids of the standard feed recipes. The recipes themselves are FeedRecipeDefinition
        // assets in the content database.
        public const string FishFeedId = "fish_feed";
        public const string ChickenFeedId = "chicken_feed";
        public const string CowFeedId = "cow_feed";

        /// <summary>Jobs one production building can hold at once, including finished ones not yet collected.</summary>
        public const int FeedQueueCapacity = 3;

        /// <summary>What one finished job puts into storage.</summary>
        private readonly struct JobOutput
        {
            public readonly string ItemId;
            public readonly string DisplayName;
            public readonly int Amount;
            public readonly int XpPerUnit;

            public JobOutput(string itemId, string displayName, int amount, int xpPerUnit)
            {
                ItemId = itemId;
                DisplayName = displayName;
                Amount = amount;
                XpPerUnit = xpPerUnit;
            }
        }

        public bool IsFeedGrinder(PlacedBuilding building)
        {
            return building != null &&
                   building.Definition != null &&
                   building.Definition.Id == FeedGrinderBuildingId;
        }

        public bool IsGoodsBuilding(PlacedBuilding building)
        {
            return building != null &&
                   building.Definition != null &&
                   building.Definition.Category == BuildingCategory.Goods;
        }

        /// <summary>True for any building with a production queue (Feed Grinder or goods).</summary>
        public bool IsProductionBuilding(PlacedBuilding building)
        {
            return IsFeedGrinder(building) || IsGoodsBuilding(building);
        }

        // ------------------------------------------------------------------ Starting jobs

        /// <summary>
        /// Takes the crops now and queues the feed on this grinder. The feed is ready after the
        /// recipe's production time, counted from when the grinder finishes its previous job.
        /// </summary>
        public bool TryStartFeed(PlacedBuilding grinder, string feedId, out string message)
        {
            if (!IsFeedGrinder(grinder))
            {
                message = "That is not a Feed Grinder.";
                return false;
            }

            var recipe = _content.GetFeedRecipe(feedId);
            if (recipe == null)
            {
                message = "Unknown feed.";
                return false;
            }

            var crop = _content.GetCrop(recipe.CropId);
            if (crop == null)
            {
                message = $"{recipe.DisplayName} needs {recipe.CropId}, which is not available.";
                return false;
            }

            if (grinder.Production.Count >= FeedQueueCapacity)
            {
                message = "Feed Grinder is full.";
                return false;
            }

            if (!Inventory.TryRemove(recipe.CropId, recipe.CropAmount))
            {
                message = $"Need {recipe.CropAmount} {crop.DisplayName}";
                return false;
            }

            message = Enqueue(grinder, recipe.Id, recipe.ProductionSeconds, recipe.DisplayName);
            return true;
        }

        public bool TryStartGoods(PlacedBuilding building, string recipeId, out string message)
        {
            if (!IsGoodsBuilding(building))
            {
                message = "That building does not make goods.";
                return false;
            }

            var recipe = _content.GetGoodsRecipe(recipeId);
            if (recipe == null || recipe.BuildingId != building.Definition.Id)
            {
                message = $"{building.Definition.DisplayName} can't make that.";
                return false;
            }

            var name = _content.GetItemDisplayName(recipe.OutputId);
            if (!IsUnlocked(recipe))
            {
                message = LockedMessage(name, recipe.UnlockLevel);
                return false;
            }

            if (building.Production.Count >= FeedQueueCapacity)
            {
                message = $"{building.Definition.DisplayName} is full.";
                return false;
            }

            var missing = new List<string>();
            for (var i = 0; i < recipe.Inputs.Count; i++)
            {
                var input = recipe.Inputs[i];
                var shortBy = input.amount - Inventory.GetCount(input.itemId);
                if (shortBy > 0)
                {
                    missing.Add($"{shortBy} {_content.GetItemDisplayName(input.itemId)}");
                }
            }

            if (missing.Count > 0)
            {
                message = $"Need {string.Join(", ", missing)} more";
                return false;
            }

            for (var i = 0; i < recipe.Inputs.Count; i++)
            {
                Inventory.TryRemove(recipe.Inputs[i].itemId, recipe.Inputs[i].amount);
            }

            message = Enqueue(building, recipe.Id, recipe.ProductionSeconds, name);
            return true;
        }

        /// <summary>
        /// Queues a job whose ingredients have already been taken. It starts when the building's
        /// last job finishes (or now, if it's idle). Returns the message for the player.
        /// </summary>
        private string Enqueue(PlacedBuilding building, string jobId, float productionSeconds, string displayName)
        {
            var nowTicks = _clock.UtcNow.Ticks;
            var startTicks = building.Production.Count > 0
                ? Math.Max(nowTicks, building.Production[building.Production.Count - 1].ReadyAtUtcTicks)
                : nowTicks;
            var readyAt = startTicks + SecondsToTicks(productionSeconds);
            building.EnqueueProduction(jobId, readyAt);
            SaveSoon();

            return startTicks > nowTicks
                ? $"{displayName} queued. Ready in {FarmSimulation.FormatTime(TicksToSeconds(readyAt - nowTicks))}."
                : $"Making {displayName}. Ready in {FarmSimulation.FormatTime(productionSeconds)}.";
        }

        // ------------------------------------------------------------------ Collecting

        /// <summary>Moves every finished job on this grinder into the inventory.</summary>
        public int CollectReadyFeed(PlacedBuilding grinder, out string message)
        {
            message = null;
            return IsFeedGrinder(grinder) ? CollectReady(grinder, ResolveFeedJob, out message) : 0;
        }

        /// <summary>Moves every finished good in this building into the inventory, with its XP.</summary>
        public int CollectReadyGoods(PlacedBuilding building, out string message)
        {
            message = null;
            return IsGoodsBuilding(building) ? CollectReady(building, ResolveGoodsJob, out message) : 0;
        }

        private bool ResolveFeedJob(string jobId, out JobOutput output)
        {
            var recipe = _content.GetFeedRecipe(jobId);
            output = recipe != null
                ? new JobOutput(recipe.Id, recipe.DisplayName, recipe.OutputAmount, 0)
                : default;
            return recipe != null;
        }

        private bool ResolveGoodsJob(string jobId, out JobOutput output)
        {
            var recipe = _content.GetGoodsRecipe(jobId);
            var product = recipe != null ? _content.GetProduct(recipe.OutputId) : null;
            output = product != null
                ? new JobOutput(product.Id, product.DisplayName, recipe.OutputAmount, product.CollectXp)
                : default;
            return product != null;
        }

        private delegate bool JobResolver(string jobId, out JobOutput output);

        /// <summary>
        /// Moves finished jobs into storage, oldest first, so when storage runs out it's the
        /// newest that wait (finished, in the building) until there's room. Jobs whose recipe no
        /// longer exists are dropped.
        /// </summary>
        private int CollectReady(PlacedBuilding building, JobResolver resolve, out string message)
        {
            var nowTicks = _clock.UtcNow.Ticks;
            var collected = new Dictionary<string, int>();
            var order = new List<string>();
            var total = 0;
            var xp = 0L;
            var blocked = false;

            for (var i = 0; i < building.Production.Count;)
            {
                var job = building.Production[i];
                if (job.ReadyAtUtcTicks > nowTicks)
                {
                    i++;
                    continue;
                }

                if (!resolve(job.ItemId, out var output))
                {
                    building.RemoveProductionAt(i);
                    continue;
                }

                if (!Storage.CanFit(output.Amount))
                {
                    blocked = true;
                    i++;
                    continue;
                }

                building.RemoveProductionAt(i);
                Inventory.Add(output.ItemId, output.Amount);
                total += output.Amount;
                xp += (long)output.XpPerUnit * output.Amount;
                if (!collected.ContainsKey(output.DisplayName))
                {
                    collected[output.DisplayName] = 0;
                    order.Add(output.DisplayName);
                }

                collected[output.DisplayName] += output.Amount;
            }

            var gainedXp = (int)Math.Min(xp, int.MaxValue);
            message = CollectionMessage(collected, order, gainedXp, blocked);
            if (total == 0)
            {
                return 0;
            }

            Level.AddXp(gainedXp);
            SaveSoon();
            return total;
        }

        /// <summary>
        /// "Collected 2 Butter and 1 Cheese. +37 XP", with a storage warning when something had
        /// to stay behind; null when nothing happened at all.
        /// </summary>
        private static string CollectionMessage(Dictionary<string, int> collected, List<string> order, int xp, bool blocked)
        {
            if (order.Count == 0)
            {
                return blocked ? Storage.FullMessage : null;
            }

            var parts = new List<string>();
            for (var i = 0; i < order.Count; i++)
            {
                parts.Add($"+{collected[order[i]]} {order[i]}");
            }

            var message = $"{string.Join(", ", parts)}{FormatXpSuffix(xp)}";
            return blocked ? message + " Storage is full!" : message;
        }

        // ------------------------------------------------------------------ Queue timing

        /// <summary>How long a queued job takes on its own: its feed or goods recipe time.</summary>
        public float GetJobDuration(ProductionJob job)
        {
            var feed = _content.GetFeedRecipe(job.ItemId);
            if (feed != null)
            {
                return feed.ProductionSeconds;
            }

            var goods = _content.GetGoodsRecipe(job.ItemId);
            return goods != null ? goods.ProductionSeconds : 1f;
        }

        public double GetRemainingSeconds(ProductionJob job)
        {
            return Math.Max(0d, TicksToSeconds(job.ReadyAtUtcTicks - _clock.UtcNow.Ticks));
        }
    }
}
