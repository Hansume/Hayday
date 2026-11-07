using System;

namespace FarmGame.Economy
{
    public sealed class Wallet
    {
        public int Coins { get; private set; }
        public event Action<int> Changed;

        public Wallet(int startingCoins)
        {
            Coins = Math.Max(0, startingCoins);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Coins < amount)
            {
                return false;
            }

            Coins -= amount;
            Changed?.Invoke(Coins);
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            // Cap at the largest int instead of wrapping around to a negative balance.
            Coins = (int)Math.Min((long)Coins + amount, int.MaxValue);
            Changed?.Invoke(Coins);
        }
    }
}
