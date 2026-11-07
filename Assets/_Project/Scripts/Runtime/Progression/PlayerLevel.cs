using System;

namespace FarmGame.Progression
{
    /// <summary>
    /// The player's level and experience. XP is earned by harvesting crops and collecting
    /// animal products. <see cref="Xp"/> counts progress inside the current level and resets
    /// to the remainder when the player levels up.
    /// </summary>
    public sealed class PlayerLevel
    {
        public const int MaxLevel = 99;

        public int Level { get; private set; }
        public int Xp { get; private set; }

        /// <summary>XP needed to go from the current level to the next one (0 at max level).</summary>
        public int XpToNextLevel => Level >= MaxLevel ? 0 : XpRequiredFor(Level);

        /// <summary>Progress through the current level, 0..1.</summary>
        public float Progress => XpToNextLevel <= 0 ? 1f : Xp / (float)XpToNextLevel;

        /// <summary>Raised whenever level or XP changes: (level, xp).</summary>
        public event Action<int, int> Changed;

        /// <summary>Raised once per level gained, with the new level.</summary>
        public event Action<int> LeveledUp;

        public PlayerLevel(int level = 1, int xp = 0)
        {
            Level = Math.Max(1, Math.Min(MaxLevel, level));
            Xp = 0;
            AddXpSilently(Math.Max(0, xp));
        }

        /// <summary>XP needed to advance from <paramref name="level"/> to the next level.</summary>
        public static int XpRequiredFor(int level)
        {
            // 10, 20, 35, 55, 80, ... : each step grows by 5 more than the step before.
            level = Math.Max(1, level);
            return 10 + 5 * (level - 1) * (level + 2) / 2;
        }

        /// <summary>Adds XP and returns how many levels were gained.</summary>
        public int AddXp(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            var before = Level;
            AddXpSilently(amount);
            var gained = Level - before;

            Changed?.Invoke(Level, Xp);
            for (var level = before + 1; level <= Level; level++)
            {
                LeveledUp?.Invoke(level);
            }

            return gained;
        }

        private void AddXpSilently(int amount)
        {
            var total = (long)Xp + amount;
            while (Level < MaxLevel && total >= XpRequiredFor(Level))
            {
                total -= XpRequiredFor(Level);
                Level++;
            }

            Xp = Level >= MaxLevel ? 0 : (int)Math.Min(total, int.MaxValue);
        }
    }
}
