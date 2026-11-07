using UnityEngine;

namespace FarmGame.Content
{
    [CreateAssetMenu(menuName = "Farm Game/Crop Definition", fileName = "Crop")]
    public sealed class CropDefinition : ScriptableObject
    {
        [SerializeField] private string id = "wheat";
        [SerializeField] private string displayName = "Wheat";
        [SerializeField] private Sprite icon;
        [SerializeField] private Sprite[] stageSprites;
        [SerializeField] private float growSeconds = 60f;
        [SerializeField] private int seedCost = 1;
        [SerializeField] private int sellPrice = 2;
        [SerializeField] private int yieldAmount = 2;
        [Tooltip("Experience the player earns for harvesting one plot of this crop.")]
        [SerializeField] private int harvestXp = 1;
        [Tooltip("Player level at which this crop can be planted.")]
        [SerializeField] private int unlockLevel = 1;

        public string Id => id;
        public int UnlockLevel => Mathf.Max(1, unlockLevel);
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public Sprite[] StageSprites => stageSprites;
        public float GrowSeconds => Mathf.Max(1f, growSeconds);
        public int SeedCost => Mathf.Max(0, seedCost);
        public int SellPrice => Mathf.Max(0, sellPrice);
        public int YieldAmount => Mathf.Max(1, yieldAmount);
        public int HarvestXp => Mathf.Max(0, harvestXp);

        public Sprite GetStageSprite(int stageIndex, int fallbackStage)
        {
            if (stageSprites == null || stageSprites.Length == 0)
            {
                return null;
            }

            var index = Mathf.Clamp(stageIndex, 0, stageSprites.Length - 1);
            if (stageSprites[index] != null)
            {
                return stageSprites[index];
            }

            index = Mathf.Clamp(fallbackStage, 0, stageSprites.Length - 1);
            return stageSprites[index];
        }

        public void Configure(
            string cropId,
            string cropDisplayName,
            Sprite cropIcon,
            Sprite[] cropStageSprites,
            float cropGrowSeconds,
            int cropSeedCost,
            int cropSellPrice,
            int cropYieldAmount,
            int cropHarvestXp = 1,
            int cropUnlockLevel = 1)
        {
            unlockLevel = Mathf.Max(1, cropUnlockLevel);
            id = cropId;
            displayName = cropDisplayName;
            icon = cropIcon;
            stageSprites = cropStageSprites;
            growSeconds = cropGrowSeconds;
            seedCost = cropSeedCost;
            sellPrice = cropSellPrice;
            yieldAmount = cropYieldAmount;
            harvestXp = Mathf.Max(0, cropHarvestXp);
        }
    }
}
