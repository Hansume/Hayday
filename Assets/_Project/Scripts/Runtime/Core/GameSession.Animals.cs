using System;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Farming;
using FarmGame.Inventory;

namespace FarmGame.Core
{
    /// <summary>
    /// Animals. Each animal is Hungry, Producing or Ready. Feeding a hungry animal uses 1 pack of
    /// its feed; after the building's production time it has a product to collect, and
    /// collecting makes it hungry again.
    ///
    /// Fish are different (<see cref="BuildingDefinition.SellsAnimals"/>): the player picks which
    /// kind to buy into the pond, feeds it once, and when it is grown (Ready) sells it for that
    /// kind's price and XP. Selling takes the fish out of the pond.
    /// </summary>
    public sealed partial class GameSession
    {
        public bool TryAddAnimal(PlacedBuilding building, out string message)
        {
            if (!CanAddAnimalTo(building, out message))
            {
                return false;
            }

            var definition = building.Definition;
            if (definition.SellsAnimals)
            {
                message = "Choose a fish.";
                return false;
            }

            var animalName = definition.AnimalDisplayName.ToLowerInvariant();
            if (!Wallet.TrySpend(definition.AnimalCost))
            {
                message =
                    $"Need {FormatCoins(definition.AnimalCost)} to add a {animalName}.";
                return false;
            }

            if (!Buildings.TryAddAnimal(building))
            {
                Wallet.Add(definition.AnimalCost);
                message = $"{definition.DisplayName} is full.";
                return false;
            }

            message = $"+1 {animalName}";
            SaveSoon();
            return true;
        }

        /// <summary>Buys one fish of kind <paramref name="fishId"/> into a fish pond. It arrives hungry.</summary>
        public bool TryAddFish(PlacedBuilding building, string fishId, out string message)
        {
            if (!CanAddAnimalTo(building, out message))
            {
                return false;
            }

            var definition = building.Definition;
            if (!definition.SellsAnimals)
            {
                message = $"{definition.DisplayName} doesn't keep fish.";
                return false;
            }

            var fish = _content.GetFish(fishId);
            if (fish == null)
            {
                message = "That fish isn't available.";
                return false;
            }

            if (!IsUnlocked(fish.UnlockLevel))
            {
                message = LockedMessage(fish.DisplayName, fish.UnlockLevel);
                return false;
            }

            if (!Wallet.TrySpend(fish.Cost))
            {
                message = $"Need {FormatCoins(fish.Cost)}";
                return false;
            }

            if (!Buildings.TryAddAnimal(building, fish.Id))
            {
                Wallet.Add(fish.Cost);
                message = $"{definition.DisplayName} is full.";
                return false;
            }

            message = $"+1 {fish.DisplayName}. Feed it!";
            SaveSoon();
            return true;
        }

        /// <summary>The kind of fish at <paramref name="index"/> in a pond, or null (not a fish, or unknown).</summary>
        public FishDefinition GetFishAt(PlacedBuilding building, int index)
        {
            return building != null && building.Definition != null && building.Definition.SellsAnimals
                ? _content.GetFish(building.GetAnimalKindId(index))
                : null;
        }

        /// <summary>"Salmon" for a fish, otherwise the building's animal name ("Chicken").</summary>
        public string GetAnimalName(PlacedBuilding building, int index)
        {
            var fish = GetFishAt(building, index);
            if (fish != null)
            {
                return fish.DisplayName;
            }

            return building != null && building.Definition != null ? building.Definition.AnimalDisplayName : "Animal";
        }

        public AnimalStatus GetAnimalStatus(PlacedBuilding building, int index, out double remainingSeconds)
        {
            remainingSeconds = 0d;
            var readyAt = building != null ? building.GetAnimalReadyAt(index) : 0L;
            if (readyAt <= 0L)
            {
                return AnimalStatus.Hungry;
            }

            remainingSeconds = Math.Max(0d, TicksToSeconds(readyAt - _clock.UtcNow.Ticks));
            return remainingSeconds > 0d ? AnimalStatus.Producing : AnimalStatus.Ready;
        }

        public FeedRecipeDefinition GetFeedFor(PlacedBuilding building)
        {
            return building != null && building.Definition != null
                ? _content.GetFeedRecipeFor(building.Definition.AnimalKind)
                : null;
        }

        /// <summary>What the building's animals make, or null for fish ponds (fish are sold instead).</summary>
        public ProductDefinition GetProductFor(PlacedBuilding building)
        {
            return building != null && building.Definition != null && !building.Definition.SellsAnimals
                ? _content.GetProduct(building.Definition.ProductId)
                : null;
        }

        public bool TryFeedAnimal(PlacedBuilding building, int index, out string message)
        {
            if (!TryGetFeedingInfo(building, out var feed, out message))
            {
                return false;
            }

            if (index < 0 || index >= building.AnimalCount)
            {
                message = "That animal is not here.";
                return false;
            }

            var animalName = GetAnimalName(building, index);
            var lowerName = animalName.ToLowerInvariant();
            if (GetAnimalStatus(building, index, out _) != AnimalStatus.Hungry)
            {
                message = "Already fed.";
                return false;
            }

            if (!Inventory.TryRemove(feed.Id, 1))
            {
                message = $"Need 1 {feed.DisplayName}";
                return false;
            }

            StartProducing(building, index);
            var time = FarmSimulation.FormatTime(GetAnimalWorkSeconds(building, index));
            var product = GetProductFor(building);
            if (building.Definition.SellsAnimals)
            {
                message = $"Fed! Grown in {time}";
            }
            else
            {
                message = product != null
                    ? $"Fed! {product.DisplayName} in {time}"
                    : "Fed!";
            }

            SaveSoon();
            return true;
        }

        /// <summary>Feeds every hungry animal, one pack each, for as long as the feed lasts.</summary>
        public int FeedAllAnimals(PlacedBuilding building, out string message)
        {
            if (!TryGetFeedingInfo(building, out var feed, out message))
            {
                return 0;
            }

            var fed = 0;
            var stillHungry = 0;
            for (var i = 0; i < building.AnimalCount; i++)
            {
                if (GetAnimalStatus(building, i, out _) != AnimalStatus.Hungry)
                {
                    continue;
                }

                if (Inventory.TryRemove(feed.Id, 1))
                {
                    StartProducing(building, i);
                    fed++;
                }
                else
                {
                    stillHungry++;
                }
            }

            if (fed == 0)
            {
                message = stillHungry == 0
                    ? "Nobody is hungry."
                    : $"Need {feed.DisplayName}";
                return 0;
            }

            var animals = AnimalWord(building.Definition, fed);
            message = stillHungry == 0
                ? $"Fed {fed} {animals}"
                : $"Fed {fed} {animals}. Need {stillHungry} more {feed.DisplayName}";
            SaveSoon();
            return fed;
        }

        /// <summary>Collects one animal's product; for a fish pond, sells that fish if it is grown.</summary>
        public bool TryCollectAnimalProduct(PlacedBuilding building, int index, out string message)
        {
            if (building != null && building.Definition != null && building.Definition.SellsAnimals)
            {
                return TrySellFish(building, index, out message);
            }

            var product = GetProductFor(building);
            if (product == null || index < 0 || index >= building.AnimalCount)
            {
                message = "There is nothing to collect.";
                return false;
            }

            var status = GetAnimalStatus(building, index, out var remaining);
            if (status != AnimalStatus.Ready)
            {
                message = status == AnimalStatus.Hungry
                    ? "Feed it first."
                    : $"{product.DisplayName} ready in {FarmSimulation.FormatTime(remaining)}.";
                return false;
            }

            if (!Storage.CanFit(1))
            {
                message = Storage.FullMessage;
                return false;
            }

            Inventory.Add(product.Id, 1);
            building.SetAnimalReadyAt(index, 0L);
            var xp = product.CollectXp;
            Level.AddXp(xp);
            message = $"+1 {product.DisplayName}{FormatXpSuffix(xp)}";
            SaveSoon();
            return true;
        }

        /// <summary>
        /// Collects every finished product in the building. The animals become hungry. Fish ponds
        /// have no products: grown fish wait to be sold (<see cref="SellGrownFish"/>).
        /// </summary>
        public int CollectAnimalProducts(PlacedBuilding building, out string message)
        {
            message = null;
            var product = GetProductFor(building);
            if (product == null)
            {
                return 0;
            }

            var collected = 0;
            var room = Storage.Free;
            var blocked = false;
            for (var i = 0; i < building.AnimalCount; i++)
            {
                if (GetAnimalStatus(building, i, out _) != AnimalStatus.Ready)
                {
                    continue;
                }

                if (collected >= room)
                {
                    // This animal keeps its product until there's room.
                    blocked = true;
                    continue;
                }

                building.SetAnimalReadyAt(i, 0L);
                collected++;
            }

            if (collected == 0)
            {
                message = blocked ? Storage.FullMessage : null;
                return 0;
            }

            Inventory.Add(product.Id, collected);
            var xp = (int)Math.Min((long)collected * product.CollectXp, int.MaxValue);
            Level.AddXp(xp);
            message = $"+{collected} {product.DisplayName}{FormatXpSuffix(xp)}" +
                      (blocked ? " Storage is full!" : string.Empty);
            SaveSoon();
            return collected;
        }

        /// <summary>Sells one grown fish for its kind's price and XP. It leaves the pond.</summary>
        public bool TrySellFish(PlacedBuilding building, int index, out string message)
        {
            var fish = GetFishAt(building, index);
            if (fish == null || index < 0 || index >= building.AnimalCount)
            {
                message = "There is no fish there to sell.";
                return false;
            }

            var status = GetAnimalStatus(building, index, out var remaining);
            if (status != AnimalStatus.Ready)
            {
                message = status == AnimalStatus.Hungry
                    ? $"Feed the {fish.DisplayName} first so it can grow."
                    : $"{fish.DisplayName} grown in {FarmSimulation.FormatTime(remaining)}.";
                return false;
            }

            building.RemoveAnimalAt(index);
            Wallet.Add(fish.SellPrice);
            Level.AddXp(fish.SellXp);
            message = $"Sold a {fish.DisplayName} for {FormatCoins(fish.SellPrice)}.{FormatXpSuffix(fish.SellXp)}";
            SaveSoon();
            return true;
        }

        /// <summary>Sells every grown fish in the pond. Returns how many were sold.</summary>
        public int SellGrownFish(PlacedBuilding building, out string message)
        {
            message = null;
            if (building == null || building.Definition == null || !building.Definition.SellsAnimals)
            {
                return 0;
            }

            var sold = 0;
            var coins = 0L;
            var xp = 0L;
            // Backwards, because selling a fish moves the ones after it up a place.
            for (var i = building.AnimalCount - 1; i >= 0; i--)
            {
                var fish = GetFishAt(building, i);
                if (fish == null || GetAnimalStatus(building, i, out _) != AnimalStatus.Ready)
                {
                    continue;
                }

                building.RemoveAnimalAt(i);
                coins += fish.SellPrice;
                xp += fish.SellXp;
                sold++;
            }

            if (sold == 0)
            {
                message = "No fish are grown yet.";
                return 0;
            }

            var totalCoins = (int)Math.Min(coins, int.MaxValue);
            var totalXp = (int)Math.Min(xp, int.MaxValue);
            Wallet.Add(totalCoins);
            Level.AddXp(totalXp);
            message = $"Sold {sold} fish for {FormatCoins(totalCoins)}.{FormatXpSuffix(totalXp)}";
            SaveSoon();
            return sold;
        }

        /// <summary>Seconds from feeding this animal until it's Ready: its fish's grow time, or the building's production time.</summary>
        public float GetAnimalWorkSeconds(PlacedBuilding building, int index)
        {
            var fish = GetFishAt(building, index);
            if (fish != null)
            {
                return fish.GrowSeconds;
            }

            return building != null && building.Definition != null ? building.Definition.ProductionSeconds : 0f;
        }

        private bool CanAddAnimalTo(PlacedBuilding building, out string message)
        {
            if (building == null || building.Definition == null)
            {
                message = "That building is unavailable.";
                return false;
            }

            var definition = building.Definition;
            if (!definition.CanHouseAnimals)
            {
                message = $"{definition.DisplayName} cannot house animals.";
                return false;
            }

            if (!building.HasAnimalSpace)
            {
                message = $"{definition.DisplayName} is full.";
                return false;
            }

            message = null;
            return true;
        }

        private bool TryGetFeedingInfo(PlacedBuilding building, out FeedRecipeDefinition feed, out string message)
        {
            feed = null;
            if (building == null || building.Definition == null || !building.Definition.CanHouseAnimals)
            {
                message = "There are no animals here.";
                return false;
            }

            feed = GetFeedFor(building);
            if (feed == null)
            {
                message = $"There is no feed for {building.Definition.AnimalDisplayName.ToLowerInvariant()}s.";
                return false;
            }

            message = null;
            return true;
        }

        /// <summary>"fish", "chicken"/"chickens", "cow"/"cows".</summary>
        private static string AnimalWord(BuildingDefinition definition, int count)
        {
            var name = definition.AnimalDisplayName.ToLowerInvariant();
            return count == 1 || definition.AnimalKind == AnimalKind.Fish ? name : name + "s";
        }

        private void StartProducing(PlacedBuilding building, int index)
        {
            var readyAt = _clock.UtcNow.Ticks + SecondsToTicks(GetAnimalWorkSeconds(building, index));
            building.SetAnimalReadyAt(index, readyAt);
        }
    }
}
