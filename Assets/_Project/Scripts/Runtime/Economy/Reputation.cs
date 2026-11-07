using System;

namespace FarmGame.Economy
{
    /// <summary>
    /// Reputation: how well the farm is thought of in town. A second currency, earned only by
    /// helping visitors (see <c>VisitorService</c>). It can be spent with <see cref="TrySpend"/>,
    /// ready for whatever it buys later.
    /// </summary>
    public sealed class Reputation
    {
        public int Points { get; private set; }

        /// <summary>Raised with the new total whenever points are gained or spent.</summary>
        public event Action<int> Changed;

        public Reputation(int startingPoints = 0)
        {
            Points = Math.Max(0, startingPoints);
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Points = (int)Math.Min((long)Points + amount, int.MaxValue);
            Changed?.Invoke(Points);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Points < amount)
            {
                return false;
            }

            Points -= amount;
            Changed?.Invoke(Points);
            return true;
        }
    }
}
