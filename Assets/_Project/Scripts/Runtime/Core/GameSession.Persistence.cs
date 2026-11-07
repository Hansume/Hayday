using System;
using System.Collections.Generic;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Missions;
using FarmGame.Save;
using FarmGame.Visitors;
using UnityEngine;

namespace FarmGame.Core
{
    /// <summary>Turning the session into save data and back.</summary>
    public sealed partial class GameSession
    {
        public SaveDataV1 BuildSaveData()
        {
            var data = new SaveDataV1
            {
                version = SaveDataV1.CurrentVersion,
                coins = Wallet.Coins,
                reputation = Reputation.Points,
                nextVisitorAtUtcTicks = Visitors.NextArrivalAtUtcTicks,
                visitorsServed = Visitors.ServedCount,
                visitorsGenerated = Visitors.GeneratedCount,
                storageUpgrades = Storage.Upgrades,
                level = Level.Level,
                xp = Level.Xp,
                missionsCompleted = Missions.CompletedCount,
                missionsGenerated = Missions.GeneratedCount,
                savedAtUtcTicks = _clock.UtcNow.Ticks,
                startCellX = _content.Layout.StartArea.x,
                startCellY = _content.Layout.StartArea.y,
                landDeeds = Land.Deeds
            };

            var boughtSections = Land.GetBoughtSections();
            for (var i = 0; i < boughtSections.Count; i++)
            {
                data.landSections.Add(new LandSectionDto { x = boughtSections[i].x, y = boughtSections[i].y });
            }

            WriteNotices(data);
            WriteVisitors(data);
            WriteCocktailBar(data);
            WriteInventory(data);

            for (var i = 0; i < Farm.AddedPlotCells.Count; i++)
            {
                var cell = Farm.AddedPlotCells[i];
                data.slots.Add(new SlotSaveDto { cellX = cell.x, cellY = cell.y });
            }

            for (var plotId = 0; plotId < Farm.PlotCount; plotId++)
            {
                if (!Farm.TryGetCropId(plotId, out var cropId, out var plantedAt, out var growSeconds))
                {
                    continue;
                }

                var cell = Farm.PlotCells[plotId];
                data.plots.Add(new PlotSaveDto
                {
                    plotId = plotId,
                    hasCell = true,
                    cellX = cell.x,
                    cellY = cell.y,
                    cropId = cropId,
                    plantedAtUtcTicks = plantedAt,
                    growSeconds = growSeconds
                });
            }

            for (var i = 0; i < Buildings.Placed.Count; i++)
            {
                var building = Buildings.Placed[i];
                var production = new List<ProductionJobDto>();
                for (var j = 0; j < building.Production.Count; j++)
                {
                    production.Add(new ProductionJobDto
                    {
                        itemId = building.Production[j].ItemId,
                        readyAtUtcTicks = building.Production[j].ReadyAtUtcTicks
                    });
                }

                var animalReadyAt = new List<long>();
                var animalKinds = new List<string>();
                for (var j = 0; j < building.AnimalCount; j++)
                {
                    animalReadyAt.Add(building.GetAnimalReadyAt(j));
                    animalKinds.Add(building.GetAnimalKindId(j));
                }

                data.buildings.Add(new BuildingSaveDto
                {
                    buildingId = building.Definition.Id,
                    cellX = building.Origin.x,
                    cellY = building.Origin.y,
                    animalCount = building.AnimalCount,
                    animalReadyAtUtcTicks = animalReadyAt,
                    animalKinds = animalKinds,
                    production = production,
                    fruitReadyAtUtcTicks = building.FruitReadyAtUtcTicks
                });
            }

            return data;
        }

        private void WriteNotices(SaveDataV1 data)
        {
            for (var i = 0; i < Missions.SlotCount; i++)
            {
                var mission = Missions.GetSlot(i);
                var requirements = new List<RequirementSaveDto>();
                if (mission != null)
                {
                    for (var r = 0; r < mission.Requirements.Count; r++)
                    {
                        var requirement = mission.Requirements[r];
                        requirements.Add(new RequirementSaveDto
                        {
                            itemId = requirement.ItemId,
                            required = requirement.Required
                        });
                    }
                }

                data.notices.Add(new NoticeSlotSaveDto
                {
                    slot = i,
                    hasNotice = mission != null,
                    nextPostAtUtcTicks = Missions.GetNextPostAt(i),
                    rewardCoins = mission != null ? mission.RewardCoins : 0,
                    rewardXp = mission != null ? mission.RewardXp : 0,
                    requirements = requirements
                });
            }
        }

        private void WriteVisitors(SaveDataV1 data)
        {
            for (var i = 0; i < Visitors.Visitors.Count; i++)
            {
                var visitor = Visitors.Visitors[i];
                var requests = new List<ItemStackDto>();
                for (var r = 0; r < visitor.Requests.Count; r++)
                {
                    requests.Add(new ItemStackDto { itemId = visitor.Requests[r].itemId, count = visitor.Requests[r].amount });
                }

                var offers = new List<VisitorOfferDto>();
                for (var o = 0; o < visitor.Offers.Count; o++)
                {
                    var offer = visitor.Offers[o];
                    offers.Add(new VisitorOfferDto
                    {
                        itemId = offer.ItemId,
                        amount = offer.Amount,
                        basePrice = offer.BasePrice,
                        minPrice = offer.MinPrice,
                        sold = offer.Sold
                    });
                }

                data.visitors.Add(new VisitorSaveDto
                {
                    id = visitor.Id,
                    name = visitor.Name,
                    look = visitor.Look,
                    spot = visitor.Spot,
                    requests = requests,
                    rewardCoins = visitor.RewardCoins,
                    rewardXp = visitor.RewardXp,
                    rewardReputation = visitor.RewardReputation,
                    arrivedAtUtcTicks = visitor.ArrivedAtUtcTicks,
                    leavesAtUtcTicks = visitor.LeavesAtUtcTicks,
                    kind = (int)visitor.Kind,
                    offers = offers
                });
            }
        }

        private void RestoreVisitors(SaveDataV1 saveData)
        {
            // Same guard as notices: a save written with the clock far ahead can't make a
            // visitor stay, or the next one stay away, longer than the settings allow.
            var now = _clock.UtcNow.Ticks;
            var settings = Visitors.Settings;

            // Saves from before visitors existed have no arrival time: the first visitor comes
            // after the usual short wait rather than the moment the game opens.
            var nextArrival = saveData.nextVisitorAtUtcTicks > 0L
                ? Math.Min(saveData.nextVisitorAtUtcTicks, now + settings.ArrivalMax.Ticks)
                : now + settings.ArrivalMin.Ticks;
            Visitors.RestoreState(nextArrival, saveData.visitorsServed, saveData.visitorsGenerated);

            if (saveData.visitors == null)
            {
                return;
            }

            var latestLeave = now + settings.Stay.Ticks;
            for (var i = 0; i < saveData.visitors.Count; i++)
            {
                var dto = saveData.visitors[i];
                var kind = Enum.IsDefined(typeof(VisitorKind), dto.kind) ? (VisitorKind)dto.kind : VisitorKind.Request;

                var requests = new List<ItemAmount>();
                if (dto.requests != null)
                {
                    for (var r = 0; r < dto.requests.Count; r++)
                    {
                        if (dto.requests[r].count > 0)
                        {
                            requests.Add(new ItemAmount(dto.requests[r].itemId, dto.requests[r].count));
                        }
                    }
                }

                var offers = new List<VisitorOffer>();
                if (dto.offers != null)
                {
                    for (var o = 0; o < dto.offers.Count; o++)
                    {
                        var offer = dto.offers[o];
                        if (offer.amount > 0)
                        {
                            offers.Add(new VisitorOffer(offer.itemId, offer.amount, offer.basePrice, offer.minPrice, offer.sold));
                        }
                    }
                }

                Visitors.Restore(new Visitor(
                    dto.id,
                    dto.name,
                    dto.look,
                    dto.spot,
                    requests,
                    dto.rewardCoins,
                    dto.rewardXp,
                    dto.rewardReputation,
                    dto.arrivedAtUtcTicks,
                    Math.Min(dto.leavesAtUtcTicks, latestLeave),
                    kind,
                    offers));
            }
        }

        /// <summary>Crops, then feeds, then products: only items the content still knows.</summary>
        private void WriteInventory(SaveDataV1 data)
        {
            void Write(string itemId)
            {
                var count = Inventory.GetCount(itemId);
                if (count > 0)
                {
                    data.inventory.Add(new ItemStackDto { itemId = itemId, count = count });
                }
            }

            for (var i = 0; i < _content.Crops.Count; i++)
            {
                if (_content.Crops[i] != null)
                {
                    Write(_content.Crops[i].Id);
                }
            }

            for (var i = 0; i < _content.FeedRecipes.Count; i++)
            {
                Write(_content.FeedRecipes[i].Id);
            }

            for (var i = 0; i < _content.Products.Count; i++)
            {
                Write(_content.Products[i].Id);
            }
        }

        private void Restore(SaveDataV1 saveData)
        {
            RestoreNotices(saveData);
            RestoreVisitors(saveData);
            RestoreCocktailBar(saveData);

            // Cells are saved along with where the start area was, so the farm stays on the same
            // land if the map changes shape. Saves from before the bigger map read as (0, 0).
            var cellShift = _content.Layout.StartArea.min - new Vector2Int(saveData.startCellX, saveData.startCellY);

            if (saveData.inventory != null)
            {
                for (var i = 0; i < saveData.inventory.Count; i++)
                {
                    var stack = saveData.inventory[i];
                    if (stack.count <= 0)
                    {
                        continue;
                    }

                    var known = _content.GetCrop(stack.itemId) != null ||
                                _content.GetFeedRecipe(stack.itemId) != null ||
                                _content.GetProduct(stack.itemId) != null;
                    if (known)
                    {
                        Inventory.Add(stack.itemId, stack.count);
                    }
                }
            }

            if (saveData.slots != null)
            {
                for (var i = 0; i < saveData.slots.Count; i++)
                {
                    var slot = saveData.slots[i];
                    var cell = new Vector2Int(slot.cellX, slot.cellY) + cellShift;
                    if (Farm.RestoreAddedPlot(cell))
                    {
                        Buildings.AddPlotCell(cell);
                    }
                }
            }

            if (saveData.plots != null)
            {
                for (var i = 0; i < saveData.plots.Count; i++)
                {
                    var plot = saveData.plots[i];
                    var plotId = plot.plotId;
                    if (plot.hasCell &&
                        !Farm.TryGetPlotId(new Vector2Int(plot.cellX, plot.cellY) + cellShift, out plotId))
                    {
                        // That plot no longer exists; drop the crop rather than move it.
                        continue;
                    }

                    Farm.RestorePlot(plotId, plot.cropId, plot.plantedAtUtcTicks, plot.growSeconds);
                }
            }

            if (saveData.buildings != null)
            {
                for (var i = 0; i < saveData.buildings.Count; i++)
                {
                    var building = saveData.buildings[i];
                    if (!Buildings.RestorePlacement(
                            building.buildingId,
                            new Vector2Int(building.cellX, building.cellY) + cellShift,
                            building.animalCount,
                            out var placed,
                            building.animalKinds))
                    {
                        continue;
                    }

                    if (placed.Definition.SellsAnimals)
                    {
                        RestoreFishKinds(placed);
                    }

                    RestoreProduction(placed, building.production);
                    RestoreAnimals(placed, building.animalReadyAtUtcTicks);
                    if (placed.Definition.IsFruitTree)
                    {
                        RestoreFruit(placed, building.fruitReadyAtUtcTicks);
                    }
                }
            }
        }

        private void RestoreNotices(SaveDataV1 saveData)
        {
            Missions.RestoreCounters(saveData.missionsCompleted, saveData.missionsGenerated);
            if (saveData.notices == null)
            {
                return;
            }

            // A slot can't wait longer than a discard delay, whatever the save says (guards
            // against a clock set far ahead when it was written).
            var latestPost = _clock.UtcNow.Ticks + Missions.RefillAfterDiscard.Ticks;
            for (var i = 0; i < saveData.notices.Count; i++)
            {
                var dto = saveData.notices[i];
                Mission mission = null;
                if (dto.hasNotice && dto.requirements != null)
                {
                    var requirements = new List<MissionRequirement>();
                    for (var r = 0; r < dto.requirements.Count; r++)
                    {
                        requirements.Add(new MissionRequirement(dto.requirements[r].itemId, dto.requirements[r].required));
                    }

                    mission = new Mission(requirements, dto.rewardCoins, dto.rewardXp);
                }

                Missions.RestoreSlot(dto.slot, mission, Math.Min(dto.nextPostAtUtcTicks, latestPost));
            }
        }

        private void RestoreAnimals(PlacedBuilding building, List<long> readyAt)
        {
            if (readyAt == null)
            {
                return;
            }

            // A product (or a growing fish) can't be further away than one full production time.
            var now = _clock.UtcNow.Ticks;
            for (var i = 0; i < building.AnimalCount && i < readyAt.Count; i++)
            {
                var latest = now + SecondsToTicks(GetAnimalWorkSeconds(building, i));
                building.SetAnimalReadyAt(i, readyAt[i] > 0L ? Math.Min(readyAt[i], latest) : 0L);
            }
        }

        /// <summary>
        /// Every fish in a pond must be a kind the content knows. Fish from older saves (which had
        /// no kinds) and kinds since removed become the starter fish.
        /// </summary>
        private void RestoreFishKinds(PlacedBuilding pond)
        {
            var starter = _content.GetStarterFish();
            for (var i = 0; i < pond.AnimalCount; i++)
            {
                if (_content.GetFish(pond.GetAnimalKindId(i)) == null)
                {
                    pond.SetAnimalKindId(i, starter != null ? starter.Id : string.Empty);
                }
            }
        }

        private void RestoreProduction(PlacedBuilding building, List<ProductionJobDto> jobs)
        {
            if (jobs == null || !IsProductionBuilding(building))
            {
                return;
            }

            // Only recipes this building can make are restored.
            var feedGrinder = IsFeedGrinder(building);
            var goodsRecipes = feedGrinder ? null : _content.GetGoodsRecipesFor(building.Definition.Id);

            // Guard against a save written with a clock set far in the future: a full queue of
            // the slowest recipe is the longest any job can legitimately take.
            var longestSeconds = 0f;
            if (feedGrinder)
            {
                for (var i = 0; i < _content.FeedRecipes.Count; i++)
                {
                    longestSeconds = Math.Max(longestSeconds, _content.FeedRecipes[i].ProductionSeconds);
                }
            }
            else
            {
                for (var i = 0; i < goodsRecipes.Count; i++)
                {
                    longestSeconds = Math.Max(longestSeconds, goodsRecipes[i].ProductionSeconds);
                }
            }

            var latest = _clock.UtcNow.Ticks + SecondsToTicks(longestSeconds * FeedQueueCapacity);
            for (var i = 0; i < jobs.Count && building.Production.Count < FeedQueueCapacity; i++)
            {
                var itemId = jobs[i].itemId;
                var known = feedGrinder
                    ? _content.GetFeedRecipe(itemId) != null
                    : goodsRecipes.Exists(recipe => recipe.Id == itemId);
                if (!known)
                {
                    continue;
                }

                building.EnqueueProduction(itemId, Math.Min(jobs[i].readyAtUtcTicks, latest));
            }
        }
    }
}
