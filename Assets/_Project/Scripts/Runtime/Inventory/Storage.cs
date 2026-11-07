using System;
using FarmGame.Content;
using FarmGame.Economy;

namespace FarmGame.Inventory
{
    /// <summary>
    /// The farm's storage limit. Every item in the inventory (crops, feed, animal products and
    /// goods) takes one space. When it's full, harvests and collections wait until there's room:
    /// sell things, use them up, or buy an upgrade. Upgrades get steeply more expensive.
    ///
    /// Only the game's own gathering (harvest, collect) respects the limit. Things already in
    /// the inventory are never thrown away, so a save or a lowered limit can leave it over full.
    /// </summary>
    public sealed class Storage
    {
        public const string FullMessage = "Storage is full!";

        private readonly StorageBalance _settings;
        private readonly InventoryService _inventory;

        public Storage(StorageBalance settings, InventoryService inventory, int upgrades = 0)
        {
            _settings = settings ?? new StorageBalance();
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Upgrades = Math.Max(0, Math.Min(upgrades, MaxUpgrades));
        }

        /// <summary>Raised after an upgrade.</summary>
        public event Action Changed;

        /// <summary>Upgrades bought so far.</summary>
        public int Upgrades { get; private set; }

        public int MaxUpgrades => Math.Max(0, _settings.maxUpgrades);
        public bool IsMaxed => Upgrades >= MaxUpgrades;

        public int Capacity => _settings.CapacityAt(Upgrades);
        public int Used => _inventory.TotalCount;
        public int Free => Math.Max(0, Capacity - Used);
        public bool IsFull => Free <= 0;

        /// <summary>0..1 (can be more than 1 if over the limit).</summary>
        public float Fill => Capacity <= 0 ? 1f : Used / (float)Capacity;

        /// <summary>Spaces the next upgrade adds (0 when maxed).</summary>
        public int NextUpgradeSpaces => IsMaxed ? 0 : _settings.CapacityAt(Upgrades + 1) - Capacity;

        /// <summary>Coins the next upgrade costs (0 when maxed).</summary>
        public int NextUpgradeCost => IsMaxed ? 0 : _settings.UpgradeCost(Upgrades);

        public bool CanFit(int amount)
        {
            return amount <= Free;
        }

        public bool TryUpgrade(Wallet wallet, out string message)
        {
            if (IsMaxed)
            {
                message = "Storage is already as big as it gets.";
                return false;
            }

            var cost = NextUpgradeCost;
            if (wallet == null || !wallet.TrySpend(cost))
            {
                message = $"Need {cost:N0} coins";
                return false;
            }

            var spaces = NextUpgradeSpaces;
            Upgrades++;
            message = $"Storage +{spaces} spaces";
            Changed?.Invoke();
            return true;
        }
    }
}
