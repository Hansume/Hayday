using UnityEngine;

namespace FarmGame.Content
{
    /// <summary>Something an animal produces after it is fed, e.g. an egg. It can be sold.</summary>
    [CreateAssetMenu(menuName = "Farm Game/Product Definition", fileName = "Product")]
    public sealed class ProductDefinition : ScriptableObject
    {
        [SerializeField] private string id = "egg";
        [SerializeField] private string displayName = "Egg";
        [SerializeField] private Sprite icon;
        [SerializeField] private int sellPrice = 10;
        [Tooltip("Experience the player earns for collecting one of these from an animal.")]
        [SerializeField] private int collectXp = 1;

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int SellPrice => Mathf.Max(0, sellPrice);
        public int CollectXp => Mathf.Max(0, collectXp);

        public void Configure(
            string productId,
            string productDisplayName,
            Sprite productIcon,
            int productSellPrice,
            int productCollectXp = 1)
        {
            id = productId;
            displayName = productDisplayName;
            icon = productIcon;
            sellPrice = Mathf.Max(0, productSellPrice);
            collectXp = Mathf.Max(0, productCollectXp);
        }
    }
}
