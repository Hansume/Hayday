using UnityEngine;

namespace FarmGame.Content
{
    public enum BuildingCategory
    {
        Animals,
        Production,

        /// <summary>Turns animal products into goods, e.g. milk into butter.</summary>
        Goods,

        /// <summary>Part of every farm (the notice board). Never sold in the shop.</summary>
        Landmark,

        /// <summary>
        /// A fruit tree: grows fruit on its own, for the player to pick and regrow. Stays last
        /// because assets store categories as numbers.
        /// </summary>
        Fruit
    }

    public enum AnimalKind
    {
        None,
        Fish,
        Chicken,
        Cow,
        Goat
    }

    [CreateAssetMenu(menuName = "Farm Game/Building Definition", fileName = "Building")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        [SerializeField] private string id = "fish_pond";
        [SerializeField] private string displayName = "Fish Pond";
        [SerializeField] private string description = string.Empty;
        [SerializeField] private Sprite icon;
        [SerializeField] private Sprite worldSprite;
        [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);
        [SerializeField] private int cost = 100;
        [SerializeField] private BuildingCategory category = BuildingCategory.Animals;
        [SerializeField] private AnimalKind animalKind = AnimalKind.None;
        [SerializeField] private int animalCapacity;
        [SerializeField] private int animalCost;
        [Tooltip("Id of the product a fed animal makes, e.g. \"egg\". Empty for non-animal buildings.")]
        [SerializeField] private string productId = string.Empty;
        [Tooltip("Seconds from feeding an animal until its product is ready.")]
        [SerializeField] private float productionSeconds = 300f;
        [Tooltip("Player level at which this building can be bought.")]
        [SerializeField] private int unlockLevel = 1;
        [Tooltip("Fruit trees: fruit from one harvest. productId is the fruit, productionSeconds the time it takes to grow.")]
        [SerializeField] private int harvestAmount;
        [Tooltip("Fruit trees: the tree with no fruit on it. The world sprite shows it with fruit.")]
        [SerializeField] private Sprite growingSprite;

        public bool IsFruitTree => category == BuildingCategory.Fruit;
        public int HarvestAmount => Mathf.Max(1, harvestAmount);
        public Sprite GrowingSprite => growingSprite != null ? growingSprite : worldSprite;

        public string Id => id;
        public int UnlockLevel => Mathf.Max(1, unlockLevel);
        public string ProductId => productId;
        public float ProductionSeconds => Mathf.Max(1f, productionSeconds);
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Sprite WorldSprite => worldSprite;
        public int Cost => Mathf.Max(0, cost);
        public BuildingCategory Category => category;
        public AnimalKind AnimalKind => animalKind;
        public int AnimalCapacity => Mathf.Max(0, animalCapacity);
        public int AnimalCost => Mathf.Max(0, animalCost);
        public bool CanHouseAnimals => animalKind != AnimalKind.None && AnimalCapacity > 0;
        public Vector2Int Footprint => new Vector2Int(
            Mathf.Max(1, footprint.x),
            Mathf.Max(1, footprint.y));

        /// <summary>
        /// A fish pond: the player picks which fish to buy, and grown fish are sold rather than
        /// making a product. Each fish's price and timing come from its <see cref="FishDefinition"/>.
        /// </summary>
        public bool SellsAnimals => CanHouseAnimals && animalKind == AnimalKind.Fish;

        public string AnimalDisplayName
        {
            get
            {
                switch (animalKind)
                {
                    case AnimalKind.Fish:
                        return "Fish";
                    case AnimalKind.Chicken:
                        return "Chicken";
                    case AnimalKind.Cow:
                        return "Cow";
                    case AnimalKind.Goat:
                        return "Goat";
                    default:
                        return "Animal";
                }
            }
        }

        public void Configure(
            string buildingId,
            string buildingDisplayName,
            string buildingDescription,
            Sprite buildingIcon,
            Sprite buildingWorldSprite,
            Vector2Int buildingFootprint,
            int buildingCost,
            BuildingCategory buildingCategory = BuildingCategory.Animals,
            AnimalKind buildingAnimalKind = AnimalKind.None,
            int buildingAnimalCapacity = 0,
            int buildingAnimalCost = 0,
            string buildingProductId = "",
            float buildingProductionSeconds = 300f,
            int buildingUnlockLevel = 1,
            int buildingHarvestAmount = 0,
            Sprite buildingGrowingSprite = null)
        {
            harvestAmount = Mathf.Max(0, buildingHarvestAmount);
            growingSprite = buildingGrowingSprite;
            unlockLevel = Mathf.Max(1, buildingUnlockLevel);
            productId = buildingProductId ?? string.Empty;
            productionSeconds = Mathf.Max(1f, buildingProductionSeconds);
            id = buildingId;
            displayName = buildingDisplayName;
            description = buildingDescription;
            icon = buildingIcon;
            worldSprite = buildingWorldSprite;
            footprint = new Vector2Int(
                Mathf.Max(1, buildingFootprint.x),
                Mathf.Max(1, buildingFootprint.y));
            cost = Mathf.Max(0, buildingCost);
            category = buildingCategory;
            animalKind = buildingAnimalKind;
            animalCapacity = Mathf.Max(0, buildingAnimalCapacity);
            animalCost = Mathf.Max(0, buildingAnimalCost);
        }
    }
}
