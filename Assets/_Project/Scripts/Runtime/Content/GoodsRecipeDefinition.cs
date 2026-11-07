using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmGame.Content
{
    /// <summary>An amount of one item, e.g. 2 Milk.</summary>
    [Serializable]
    public struct ItemAmount
    {
        public string itemId;
        public int amount;

        public ItemAmount(string id, int count)
        {
            itemId = id;
            amount = count;
        }
    }

    /// <summary>
    /// A goods building recipe: a few ingredients (animal products, crops or other goods)
    /// become one good, e.g. 2 Milk into Butter. The output is a <see cref="ProductDefinition"/>,
    /// so it can be stored, sold and gives XP when collected like any other product.
    /// </summary>
    [CreateAssetMenu(menuName = "Farm Game/Goods Recipe", fileName = "Recipe")]
    public sealed class GoodsRecipeDefinition : ScriptableObject
    {
        [Tooltip("Id of the product this recipe makes. Also the recipe's id.")]
        [SerializeField] private string outputId = "butter";
        [Tooltip("Id of the building that makes it, e.g. \"dairy\".")]
        [SerializeField] private string buildingId = "dairy";
        [SerializeField] private List<ItemAmount> inputs = new List<ItemAmount>();
        [SerializeField] private int outputAmount = 1;
        [SerializeField] private float productionSeconds = 300f;
        [Tooltip("Player level at which this recipe can be made.")]
        [SerializeField] private int unlockLevel = 1;

        public string Id => outputId;
        public int UnlockLevel => Mathf.Max(1, unlockLevel);
        public string OutputId => outputId;
        public string BuildingId => buildingId;
        public IReadOnlyList<ItemAmount> Inputs => inputs ?? (inputs = new List<ItemAmount>());
        public int OutputAmount => Mathf.Max(1, outputAmount);
        public float ProductionSeconds => Mathf.Max(1f, productionSeconds);

        public void Configure(
            string recipeOutputId,
            string recipeBuildingId,
            IEnumerable<ItemAmount> recipeInputs,
            float recipeProductionSeconds,
            int recipeOutputAmount = 1,
            int recipeUnlockLevel = 1)
        {
            unlockLevel = Mathf.Max(1, recipeUnlockLevel);
            outputId = recipeOutputId;
            buildingId = recipeBuildingId;
            inputs = new List<ItemAmount>();
            foreach (var input in recipeInputs)
            {
                if (!string.IsNullOrEmpty(input.itemId) && input.amount > 0)
                {
                    inputs.Add(input);
                }
            }

            productionSeconds = Mathf.Max(1f, recipeProductionSeconds);
            outputAmount = Mathf.Max(1, recipeOutputAmount);
        }
    }
}
