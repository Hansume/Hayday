using UnityEngine;

namespace FarmGame.Content
{
    /// <summary>A Feed Grinder recipe: a number of one crop becomes feed for one kind of animal.</summary>
    [CreateAssetMenu(menuName = "Farm Game/Feed Recipe", fileName = "Feed")]
    public sealed class FeedRecipeDefinition : ScriptableObject
    {
        [SerializeField] private string id = "fish_feed";
        [SerializeField] private string displayName = "Fish Feed";
        [SerializeField] private Sprite icon;
        [SerializeField] private AnimalKind animal = AnimalKind.Fish;
        [Tooltip("Id of the crop this feed is ground from, e.g. \"wheat\".")]
        [SerializeField] private string cropId = "wheat";
        [SerializeField] private int cropAmount = 3;
        [SerializeField] private float productionSeconds = 120f;
        [SerializeField] private int outputAmount = 1;

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public AnimalKind Animal => animal;
        public string CropId => cropId;
        public int CropAmount => Mathf.Max(1, cropAmount);
        public float ProductionSeconds => Mathf.Max(1f, productionSeconds);
        public int OutputAmount => Mathf.Max(1, outputAmount);

        public void Configure(
            string recipeId,
            string recipeDisplayName,
            Sprite recipeIcon,
            AnimalKind recipeAnimal,
            string recipeCropId,
            int recipeCropAmount,
            float recipeProductionSeconds,
            int recipeOutputAmount = 1)
        {
            id = recipeId;
            displayName = recipeDisplayName;
            icon = recipeIcon;
            animal = recipeAnimal;
            cropId = recipeCropId;
            cropAmount = Mathf.Max(1, recipeCropAmount);
            productionSeconds = Mathf.Max(1f, recipeProductionSeconds);
            outputAmount = Mathf.Max(1, recipeOutputAmount);
        }
    }
}
