using System;
using System.Collections.Generic;

namespace FarmGame.Inventory
{
    public sealed class InventoryService
    {
        private readonly Dictionary<string, int> _items = new Dictionary<string, int>();

        public event Action Changed;

        public int GetCount(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) && _items.TryGetValue(itemId, out var count) ? count : 0;
        }

        /// <summary>Every item held, all kinds together. This is what fills the storage.</summary>
        public int TotalCount
        {
            get
            {
                var total = 0L;
                foreach (var count in _items.Values)
                {
                    total += count;
                }

                return (int)Math.Min(total, int.MaxValue);
            }
        }

        public void Add(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return;
            }

            _items[itemId] = (int)Math.Min((long)GetCount(itemId) + amount, int.MaxValue);
            Changed?.Invoke();
        }

        public bool TryRemove(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return false;
            }

            var existing = GetCount(itemId);
            if (existing < amount)
            {
                return false;
            }

            if (existing == amount)
            {
                _items.Remove(itemId);
            }
            else
            {
                _items[itemId] = existing - amount;
            }

            Changed?.Invoke();
            return true;
        }
    }
}
