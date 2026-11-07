using UnityEngine;

namespace FarmGame.Content
{
    /// <summary>
    /// A kind of fish the player buys into a fish pond. Fed once, it grows for
    /// <see cref="GrowSeconds"/>; then it is sold, leaving the pond, for <see cref="SellPrice"/>
    /// coins and <see cref="SellXp"/> XP.
    /// </summary>
    [CreateAssetMenu(menuName = "Farm Game/Fish Definition", fileName = "Fish")]
    public sealed class FishDefinition : ScriptableObject
    {
        [SerializeField] private string id = "goldfish";
        [SerializeField] private string displayName = "Goldfish";
        [SerializeField] private Sprite sprite;
        [Tooltip("Coins to buy one into a pond.")]
        [SerializeField] private int cost = 20;
        [Tooltip("Player level at which it can be bought.")]
        [SerializeField] private int unlockLevel = 1;
        [Tooltip("Seconds from feeding until it is grown and can be sold.")]
        [SerializeField] private float growSeconds = 240f;
        [SerializeField] private int sellPrice = 40;
        [SerializeField] private int sellXp = 4;

        public string Id => id;
        public string DisplayName => displayName;

        /// <summary>The fish swimming, facing right. Also its icon.</summary>
        public Sprite Sprite => sprite;

        public int Cost => Mathf.Max(0, cost);
        public int UnlockLevel => Mathf.Max(1, unlockLevel);
        public float GrowSeconds => Mathf.Max(1f, growSeconds);
        public int SellPrice => Mathf.Max(0, sellPrice);
        public int SellXp => Mathf.Max(0, sellXp);

        public void Configure(
            string fishId,
            string fishDisplayName,
            Sprite fishSprite,
            int fishCost,
            int fishUnlockLevel,
            float fishGrowSeconds,
            int fishSellPrice,
            int fishSellXp)
        {
            id = fishId;
            displayName = fishDisplayName;
            sprite = fishSprite;
            cost = Mathf.Max(0, fishCost);
            unlockLevel = Mathf.Max(1, fishUnlockLevel);
            growSeconds = Mathf.Max(1f, fishGrowSeconds);
            sellPrice = Mathf.Max(0, fishSellPrice);
            sellXp = Mathf.Max(0, fishSellXp);
        }
    }
}
