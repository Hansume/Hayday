using System;
using FarmGame.Buildings;
using FarmGame.Farming;
using FarmGame.Inventory;

namespace FarmGame.Core
{
    /// <summary>
    /// Fruit trees (category Fruit). A tree grows fruit by itself, starting when it's placed.
    /// Picking it puts the harvest in storage and starts the next crop of fruit.
    /// </summary>
    public sealed partial class GameSession
    {
        public bool IsFruitTree(PlacedBuilding building)
        {
            return building != null && building.Definition != null && building.Definition.IsFruitTree;
        }

        public bool IsFruitReady(PlacedBuilding tree)
        {
            return IsFruitTree(tree) && tree.FruitReadyAtUtcTicks <= _clock.UtcNow.Ticks;
        }

        public double GetFruitRemainingSeconds(PlacedBuilding tree)
        {
            return IsFruitTree(tree)
                ? Math.Max(0d, TicksToSeconds(tree.FruitReadyAtUtcTicks - _clock.UtcNow.Ticks))
                : 0d;
        }

        /// <summary>Picks a ready tree. Nothing changes if the fruit isn't ready or doesn't fit in storage.</summary>
        public bool TryHarvestFruit(PlacedBuilding tree, out string message)
        {
            if (!IsFruitTree(tree))
            {
                message = "That is not a fruit plant.";
                return false;
            }

            var fruit = _content.GetProduct(tree.Definition.ProductId);
            if (fruit == null)
            {
                message = "Unknown fruit.";
                return false;
            }

            if (!IsFruitReady(tree))
            {
                message = $"{fruit.DisplayName} ready in {FarmSimulation.FormatTime(GetFruitRemainingSeconds(tree))}";
                return false;
            }

            var amount = tree.Definition.HarvestAmount;
            if (!Storage.CanFit(amount))
            {
                message = Storage.FullMessage;
                return false;
            }

            Inventory.Add(fruit.Id, amount);
            var xp = (int)Math.Min((long)fruit.CollectXp * amount, int.MaxValue);
            Level.AddXp(xp);
            StartGrowingFruit(tree);
            message = $"+{amount} {fruit.DisplayName}{FormatXpSuffix(xp)}";
            SaveSoon();
            return true;
        }

        private void StartGrowingFruit(PlacedBuilding tree)
        {
            tree.FruitReadyAtUtcTicks = _clock.UtcNow.Ticks + SecondsToTicks(tree.Definition.ProductionSeconds);
        }

        /// <summary>
        /// Fruit can't be further away than one full growing time, whatever the save says
        /// (guards against a clock set far ahead when it was written).
        /// </summary>
        private void RestoreFruit(PlacedBuilding tree, long readyAtUtcTicks)
        {
            var latest = _clock.UtcNow.Ticks + SecondsToTicks(tree.Definition.ProductionSeconds);
            tree.FruitReadyAtUtcTicks = readyAtUtcTicks > 0L ? Math.Min(readyAtUtcTicks, latest) : latest;
        }
    }
}
