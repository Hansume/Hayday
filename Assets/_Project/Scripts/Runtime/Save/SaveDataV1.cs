using System;
using System.Collections.Generic;

namespace FarmGame.Save
{
    [Serializable]
    public sealed class SaveDataV1
    {
        // 5: planted crops also store their grid cell.
        // 6: animals store when their product is ready (0 = hungry).
        // 7: player level and XP. Older saves read as level 0, which loads as level 1.
        // 8: missions (a simple list, replaced in 9).
        // 9: notice board slots with timed, multi-requirement notices. Older saves start
        //    with a fresh board.
        // 10: storage upgrades bought. Older saves read as 0 (the starting storage).
        // 11: visitors and reputation. Older saves start with 0 reputation and no visitors.
        // 12: visitors who sell (kind and offers). Version 11 visitors read as request visitors.
        // 13: fish ponds store each fish's kind (animalKinds). Older fish load as the starter
        //     fish; fish fillets and fish goods no longer exist and are dropped.
        // 14: cabbage/sunflower -> soybean/chili, Country Kitchen -> Bakery, Creamery and Goat
        //     Dairy -> Dairy. Older saves are renamed on load (SaveMigrations).
        // 15: the board's orders take crops and products from storage instead of counting
        //     progress. Older notices are dropped and the board fills with new orders.
        // 16: fruit trees store when their fruit is ready (fruitReadyAtUtcTicks). Older saves
        //     have no trees, so nothing needs converting.
        // 17: the map grew to 36 x 36. Saves store where the start area is (startCellX/Y), so
        //     older farms, which read as (0, 0), move onto the new start area when loaded. Also
        //     Land Deeds and the land sections bought.
        // 18: the Cocktail Bar's line and timers. Older saves read as no line and none due.
        // 19: the bar's line and timers reset, for the new line size and length.
        public const int CurrentVersion = 19;

        public int version = CurrentVersion;
        public long barLineEndsAtUtcTicks;
        public long barNextLineAtUtcTicks;
        public int barServed;
        public int barGenerated;
        public List<BarCustomerDto> barLine = new List<BarCustomerDto>();
        public int startCellX;
        public int startCellY;
        public int landDeeds;
        public List<LandSectionDto> landSections = new List<LandSectionDto>();
        public int coins;
        public int reputation;
        public List<VisitorSaveDto> visitors = new List<VisitorSaveDto>();
        public long nextVisitorAtUtcTicks;
        public int visitorsServed;
        public int visitorsGenerated;
        public int storageUpgrades;
        public int level;
        public int xp;
        public List<NoticeSlotSaveDto> notices = new List<NoticeSlotSaveDto>();
        public int missionsCompleted;
        public int missionsGenerated;
        public long savedAtUtcTicks;
        public List<ItemStackDto> inventory = new List<ItemStackDto>();
        public List<SlotSaveDto> slots = new List<SlotSaveDto>();
        public List<PlotSaveDto> plots = new List<PlotSaveDto>();
        public List<BuildingSaveDto> buildings = new List<BuildingSaveDto>();
    }

    /// <summary>A customer waiting in the Cocktail Bar's line.</summary>
    [Serializable]
    public struct BarCustomerDto
    {
        public string name;
        public int look;
        public string juiceId;
        public int rewardCoins;
        public int rewardXp;
    }

    /// <summary>A section of land the player bought, counted in sections rather than cells.</summary>
    [Serializable]
    public struct LandSectionDto
    {
        public int x;
        public int y;
    }

    [Serializable]
    public struct ItemStackDto
    {
        public string itemId;
        public int count;
    }

    [Serializable]
    public struct SlotSaveDto
    {
        public int cellX;
        public int cellY;
    }

    [Serializable]
    public struct PlotSaveDto
    {
        public int plotId;

        // The plot's grid cell. Used to find the plot on load, because plot numbers shift if
        // an earlier purchased plot can't be restored. Saves before version 5 lack it.
        public bool hasCell;
        public int cellX;
        public int cellY;

        public string cropId;
        public long plantedAtUtcTicks;
        public float growSeconds;
    }

    [Serializable]
    public struct BuildingSaveDto
    {
        public string buildingId;
        public int cellX;
        public int cellY;
        public int animalCount;

        // One entry per animal: 0 while hungry, otherwise UTC ticks when its product is ready.
        public List<long> animalReadyAtUtcTicks;

        // One entry per animal: the fish kind id for fish ponds, empty for other animals.
        public List<string> animalKinds;
        public List<ProductionJobDto> production;

        // Fruit trees: UTC ticks when the fruit can be picked. 0 for other buildings.
        public long fruitReadyAtUtcTicks;
    }

    /// <summary>One order board slot: its order, or when it gets the next one.</summary>
    [Serializable]
    public struct NoticeSlotSaveDto
    {
        public int slot;
        public bool hasNotice;
        public long nextPostAtUtcTicks;
        public int rewardCoins;
        public int rewardXp;
        public List<RequirementSaveDto> requirements;
    }

    [Serializable]
    public struct RequirementSaveDto
    {
        public string itemId;
        public int required;
    }

    /// <summary>A visitor waiting at the farm, with what they asked for and what they'll pay.</summary>
    [Serializable]
    public struct VisitorSaveDto
    {
        public int id;
        public string name;
        public int look;
        public int spot;
        public List<ItemStackDto> requests;
        public int rewardCoins;
        public int rewardXp;
        public int rewardReputation;
        public long arrivedAtUtcTicks;
        public long leavesAtUtcTicks;

        // VisitorKind as a number: 0 asks for goods, 1 sells.
        public int kind;
        public List<VisitorOfferDto> offers;
    }

    /// <summary>One bundle a selling visitor offers.</summary>
    [Serializable]
    public struct VisitorOfferDto
    {
        public string itemId;
        public int amount;
        public int basePrice;
        public int minPrice;
        public bool sold;
    }

    [Serializable]
    public struct ProductionJobDto
    {
        public string itemId;
        public long readyAtUtcTicks;
    }
}
