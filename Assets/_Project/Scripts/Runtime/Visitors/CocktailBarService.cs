using System;
using System.Collections.Generic;
using FarmGame.Content;

namespace FarmGame.Visitors
{
    /// <summary>One customer in the Cocktail Bar's line, and what serving them pays.</summary>
    public sealed class BarCustomer
    {
        public string Name { get; }
        public int Look { get; }
        public string JuiceId { get; }
        public int RewardCoins { get; }
        public int RewardXp { get; }

        public BarCustomer(string name, int look, string juiceId, int rewardCoins, int rewardXp)
        {
            Name = string.IsNullOrEmpty(name) ? "Visitor" : name;
            Look = Math.Max(0, look);
            JuiceId = juiceId;
            RewardCoins = Math.Max(0, rewardCoins);
            RewardXp = Math.Max(0, rewardXp);
        }
    }

    /// <summary>
    /// The Cocktail Bar on the beach. Now and then a line of customers forms, each wanting one
    /// juice. They're served strictly one at a time, front first, and a customer can't be passed
    /// over. The line lasts <see cref="CocktailBarBalance.Line"/>, however many are left; the next
    /// one comes <see cref="CocktailBarBalance.Cooldown"/> after it ends, even if it was cleared early.
    ///
    /// A line forms whether or not the player has any juice or a way to make it. Customers ask for
    /// juices unlocked at the player's level, or the first juices to unlock if none are yet. A line
    /// that's due while the game is closed forms when it next opens, so it always gets its full time.
    /// </summary>
    public sealed class CocktailBarService
    {
        private readonly ContentDatabase _content;
        private readonly Func<int> _level;
        private readonly Func<long> _now;
        private readonly CocktailBarBalance _settings;
        private readonly List<BarCustomer> _line = new List<BarCustomer>();

        /// <summary>Raised whenever a line forms or ends, or a customer leaves it.</summary>
        public event Action Changed;

        /// <summary>The customers waiting, front first.</summary>
        public IReadOnlyList<BarCustomer> Line => _line;

        public BarCustomer Front => _line.Count > 0 ? _line[0] : null;

        public CocktailBarBalance Settings => _settings;

        /// <summary>When the current line goes home (UTC ticks), or 0 if there's no line time running.</summary>
        public long LineEndsAtUtcTicks { get; private set; }

        /// <summary>When the next line may form (UTC ticks).</summary>
        public long NextLineAtUtcTicks { get; private set; }

        /// <summary>Customers ever served.</summary>
        public int ServedCount { get; private set; }

        /// <summary>Customers ever generated; seeds the next one, so a reload gives the same sequence.</summary>
        public int GeneratedCount { get; private set; }

        public CocktailBarService(ContentDatabase content, Func<int> level, Func<long> nowTicks)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _level = level ?? (() => 1);
            _now = nowTicks ?? throw new ArgumentNullException(nameof(nowTicks));
            _settings = _content.CocktailBar;
        }

        /// <summary>
        /// Ends a line whose time is up and forms a new one when it's due. Call regularly; it also
        /// catches up on time that passed while the game was closed. Returns true when anything changed.
        /// </summary>
        public bool Update()
        {
            var now = _now();
            var changed = false;
            if (LineEndsAtUtcTicks > 0L && now >= LineEndsAtUtcTicks)
            {
                changed = _line.Count > 0;
                _line.Clear();
                LineEndsAtUtcTicks = 0L;
            }

            if (LineEndsAtUtcTicks == 0L && now >= NextLineAtUtcTicks && FormLine(now))
            {
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }

            return changed;
        }

        /// <summary>Takes the served front customer out of the line; the next one steps up. Rewards are paid by the caller.</summary>
        public void RemoveServedFront()
        {
            if (_line.Count == 0)
            {
                return;
            }

            _line.RemoveAt(0);
            ServedCount++;
            Changed?.Invoke();
        }

        /// <summary>Puts a saved line back. Customers asking for a juice that no longer exists are dropped.</summary>
        public void Restore(long lineEndsAtUtcTicks, long nextLineAtUtcTicks, int servedCount, int generatedCount, IEnumerable<BarCustomer> line)
        {
            LineEndsAtUtcTicks = Math.Max(0L, lineEndsAtUtcTicks);
            NextLineAtUtcTicks = Math.Max(0L, nextLineAtUtcTicks);
            ServedCount = Math.Max(0, servedCount);
            GeneratedCount = Math.Max(0, generatedCount);
            _line.Clear();
            if (line == null)
            {
                return;
            }

            foreach (var customer in line)
            {
                if (customer != null && _content.GetProduct(customer.JuiceId) != null)
                {
                    _line.Add(customer);
                }
            }
        }

        /// <summary>Moves the line's end and the next line earlier, as if time had passed (debug).</summary>
        public void DebugAdvance(long ticks)
        {
            if (LineEndsAtUtcTicks > 0L)
            {
                LineEndsAtUtcTicks -= ticks;
            }

            NextLineAtUtcTicks -= ticks;
        }

        private bool FormLine(long now)
        {
            var juices = RequestableJuices();
            if (juices.Count == 0)
            {
                return false;
            }

            // One generator for the whole line: a seed per customer gave rows of identical looks.
            var random = new Random(unchecked(GeneratedCount * 6983 + 11));
            _line.Clear();
            for (var i = 0; i < _settings.LineSize; i++)
            {
                GeneratedCount++;
                var juice = juices[random.Next(juices.Count)];
                _line.Add(new BarCustomer(
                    VisitorService.Names[random.Next(VisitorService.Names.Length)],
                    random.Next(VisitorService.LookCount),
                    juice.Id,
                    (int)Math.Round(juice.SellPrice * Math.Max(0f, _settings.coinMultiplier)),
                    (int)Math.Round(juice.CollectXp * Math.Max(0f, _settings.xpMultiplier))));
            }

            LineEndsAtUtcTicks = now + _settings.Line.Ticks;
            NextLineAtUtcTicks = LineEndsAtUtcTicks + _settings.Cooldown.Ticks;
            return true;
        }

        /// <summary>Juices unlocked at the player's level, or the first to unlock if none are yet.</summary>
        private List<ProductDefinition> RequestableJuices()
        {
            var firstUnlock = int.MaxValue;
            for (var i = 0; i < _content.GoodsRecipes.Count; i++)
            {
                var recipe = _content.GoodsRecipes[i];
                if (IsJuice(recipe))
                {
                    firstUnlock = Math.Min(firstUnlock, recipe.UnlockLevel);
                }
            }

            var level = Math.Max(_level(), firstUnlock);
            var juices = new List<ProductDefinition>();
            for (var i = 0; i < _content.GoodsRecipes.Count; i++)
            {
                var recipe = _content.GoodsRecipes[i];
                if (IsJuice(recipe) && recipe.UnlockLevel <= level)
                {
                    juices.Add(_content.GetProduct(recipe.OutputId));
                }
            }

            return juices;
        }

        private bool IsJuice(GoodsRecipeDefinition recipe)
        {
            return recipe != null && _content.GetFruitInput(recipe) != null && _content.GetProduct(recipe.OutputId) != null;
        }
    }
}
