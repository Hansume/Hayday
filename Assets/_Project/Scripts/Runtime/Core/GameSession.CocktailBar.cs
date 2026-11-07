using System;
using System.Collections.Generic;
using FarmGame.Save;
using FarmGame.Visitors;

namespace FarmGame.Core
{
    /// <summary>The Cocktail Bar on the beach: serving its line of customers, one at a time.</summary>
    public sealed partial class GameSession
    {
        /// <summary>Lets lines form and end as time passes. Call regularly (the HUD does).</summary>
        public void UpdateCocktailBar()
        {
            if (CocktailBar.Update())
            {
                SaveSoon();
            }
        }

        public bool CanServeBarCustomer => CocktailBar.Front != null && Inventory.GetCount(CocktailBar.Front.JuiceId) > 0;

        /// <summary>
        /// Gives the front customer their juice and pays them. Only the front customer can be
        /// served, and nobody can be passed over: without the juice, nothing changes.
        /// </summary>
        public bool TryServeBarCustomer(out string message)
        {
            var customer = CocktailBar.Front;
            if (customer == null)
            {
                message = "No one in line";
                return false;
            }

            if (!Inventory.TryRemove(customer.JuiceId, 1))
            {
                message = $"Need 1 {_content.GetItemDisplayName(customer.JuiceId)}";
                return false;
            }

            CocktailBar.RemoveServedFront();
            Wallet.Add(customer.RewardCoins);
            Level.AddXp(customer.RewardXp);
            message = $"+{FormatCoins(customer.RewardCoins)} +{customer.RewardXp} XP{RollForDeed(_content.Land.visitorDeedChance)}";
            SaveSoon();
            return true;
        }

        private void WriteCocktailBar(SaveDataV1 data)
        {
            data.barLineEndsAtUtcTicks = CocktailBar.LineEndsAtUtcTicks;
            data.barNextLineAtUtcTicks = CocktailBar.NextLineAtUtcTicks;
            data.barServed = CocktailBar.ServedCount;
            data.barGenerated = CocktailBar.GeneratedCount;
            for (var i = 0; i < CocktailBar.Line.Count; i++)
            {
                var customer = CocktailBar.Line[i];
                data.barLine.Add(new BarCustomerDto
                {
                    name = customer.Name,
                    look = customer.Look,
                    juiceId = customer.JuiceId,
                    rewardCoins = customer.RewardCoins,
                    rewardXp = customer.RewardXp
                });
            }
        }

        private void RestoreCocktailBar(SaveDataV1 saveData)
        {
            // Same guard as visitors: a save written with the clock far ahead can't make a line
            // last, or the next one stay away, longer than the settings allow.
            var now = _clock.UtcNow.Ticks;
            var settings = CocktailBar.Settings;
            var line = new List<BarCustomer>();
            if (saveData.barLine != null)
            {
                for (var i = 0; i < saveData.barLine.Count; i++)
                {
                    var dto = saveData.barLine[i];
                    line.Add(new BarCustomer(dto.name, dto.look, dto.juiceId, dto.rewardCoins, dto.rewardXp));
                }
            }

            CocktailBar.Restore(
                Math.Min(saveData.barLineEndsAtUtcTicks, now + settings.Line.Ticks),
                Math.Min(saveData.barNextLineAtUtcTicks, now + settings.Line.Ticks + settings.Cooldown.Ticks),
                saveData.barServed,
                saveData.barGenerated,
                line);
        }
    }
}
