using UnityEngine;

namespace FarmGame.Content
{
    public enum UnlockKind
    {
        Building,
        Crop,
        Recipe,

        /// <summary>A fish that can now be bought into a fish pond.</summary>
        Fish
    }

    /// <summary>Something the player can use from a given level on, as shown when they reach it.</summary>
    public sealed class UnlockInfo
    {
        public UnlockKind Kind { get; }

        /// <summary>The building or crop id, or the output item id of a recipe.</summary>
        public string Id { get; }

        public string DisplayName { get; }
        public Sprite Icon { get; }

        /// <summary>A short caption, e.g. "New crop" or "New recipe: Dairy".</summary>
        public string Description { get; }

        /// <summary>For buildings: which shop tab it is on.</summary>
        public BuildingCategory Category { get; }

        public UnlockInfo(
            UnlockKind kind,
            string id,
            string displayName,
            Sprite icon,
            string description,
            BuildingCategory category = BuildingCategory.Animals)
        {
            Kind = kind;
            Id = id;
            DisplayName = displayName;
            Icon = icon;
            Description = description;
            Category = category;
        }
    }
}
