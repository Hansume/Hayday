using System;
using System.Collections.Generic;
using FarmGame.Content;
using FarmGame.Economy;
using UnityEngine;

namespace FarmGame.Buildings
{
    public readonly struct ProductionJob
    {
        public readonly string ItemId;
        public readonly long ReadyAtUtcTicks;

        public ProductionJob(string itemId, long readyAtUtcTicks)
        {
            ItemId = itemId;
            ReadyAtUtcTicks = readyAtUtcTicks;
        }
    }

    public sealed class PlacedBuilding
    {
        private readonly List<ProductionJob> _production = new List<ProductionJob>();

        // One entry per animal: 0 while it is hungry, otherwise when its product is ready (or,
        // for a fish, when it is grown).
        private readonly List<long> _animalReadyAt = new List<long>();

        // One entry per animal: which kind of fish it is. Empty for other animals.
        private readonly List<string> _animalKinds = new List<string>();

        public BuildingDefinition Definition { get; }
        public Vector2Int Origin { get; internal set; }
        public int AnimalCount => _animalReadyAt.Count;

        /// <summary>Fruit trees: the UTC ticks when the fruit can be picked.</summary>
        public long FruitReadyAtUtcTicks { get; internal set; }

        /// <summary>The fish kind id of the animal at <paramref name="index"/>; empty for other animals.</summary>
        public string GetAnimalKindId(int index)
        {
            return index >= 0 && index < _animalKinds.Count ? _animalKinds[index] ?? string.Empty : string.Empty;
        }

        /// <summary>0 if the animal is hungry, otherwise the UTC ticks when its product is ready.</summary>
        public long GetAnimalReadyAt(int index)
        {
            return index >= 0 && index < _animalReadyAt.Count ? _animalReadyAt[index] : 0L;
        }

        internal void SetAnimalReadyAt(int index, long readyAtUtcTicks)
        {
            if (index >= 0 && index < _animalReadyAt.Count)
            {
                _animalReadyAt[index] = Math.Max(0L, readyAtUtcTicks);
            }
        }

        /// <summary>Jobs in the order they finish. Each starts when the one before it is done.</summary>
        public IReadOnlyList<ProductionJob> Production => _production;

        internal void EnqueueProduction(string itemId, long readyAtUtcTicks)
        {
            _production.Add(new ProductionJob(itemId, readyAtUtcTicks));
            _production.Sort((a, b) => a.ReadyAtUtcTicks.CompareTo(b.ReadyAtUtcTicks));
        }

        internal void RemoveProductionAt(int index)
        {
            _production.RemoveAt(index);
        }

        internal void AdvanceProduction(long ticks)
        {
            for (var i = 0; i < _production.Count; i++)
            {
                var job = _production[i];
                _production[i] = new ProductionJob(job.ItemId, job.ReadyAtUtcTicks - ticks);
            }

            for (var i = 0; i < _animalReadyAt.Count; i++)
            {
                if (_animalReadyAt[i] > 0L)
                {
                    // Stay above 0 so a fed animal is never mistaken for a hungry one.
                    _animalReadyAt[i] = Math.Max(1L, _animalReadyAt[i] - ticks);
                }
            }

            if (FruitReadyAtUtcTicks > 0L)
            {
                FruitReadyAtUtcTicks = Math.Max(1L, FruitReadyAtUtcTicks - ticks);
            }
        }

        public bool HasAnimalSpace =>
            Definition != null && AnimalCount < Definition.AnimalCapacity;

        public PlacedBuilding(BuildingDefinition definition, Vector2Int origin)
        {
            Definition = definition;
            Origin = origin;
        }

        internal void AddAnimal(string kindId = null)
        {
            if (HasAnimalSpace)
            {
                _animalReadyAt.Add(0L);
                _animalKinds.Add(kindId ?? string.Empty);
            }
        }

        /// <summary>Takes an animal out, e.g. a grown fish that was sold. Later animals move up one.</summary>
        internal void RemoveAnimalAt(int index)
        {
            if (index >= 0 && index < _animalReadyAt.Count)
            {
                _animalReadyAt.RemoveAt(index);
                _animalKinds.RemoveAt(index);
            }
        }

        internal void RestoreAnimalCount(int count)
        {
            RestoreAnimals(count, null);
        }

        /// <summary>Restores <paramref name="count"/> hungry animals; <paramref name="kindIds"/> gives each one's kind.</summary>
        internal void RestoreAnimals(int count, IReadOnlyList<string> kindIds)
        {
            _animalReadyAt.Clear();
            _animalKinds.Clear();
            var clamped = Definition == null ? 0 : Mathf.Clamp(count, 0, Definition.AnimalCapacity);
            for (var i = 0; i < clamped; i++)
            {
                _animalReadyAt.Add(0L);
                _animalKinds.Add(kindIds != null && i < kindIds.Count ? kindIds[i] ?? string.Empty : string.Empty);
            }
        }

        internal void SetAnimalKindId(int index, string kindId)
        {
            if (index >= 0 && index < _animalKinds.Count)
            {
                _animalKinds[index] = kindId ?? string.Empty;
            }
        }
    }

    public sealed class BuildingService
    {
        private readonly ContentDatabase _content;
        private readonly FarmLayout _layout;
        private readonly FarmGame.Land.FarmLand _land;
        private readonly HashSet<Vector2Int> _plotCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _occupiedCells = new HashSet<Vector2Int>();
        private readonly List<PlacedBuilding> _placed = new List<PlacedBuilding>();

        public IReadOnlyList<PlacedBuilding> Placed => _placed;
        public event Action Changed;

        /// <param name="land">Which land is owned. Without it, the whole map can be built on.</param>
        public BuildingService(ContentDatabase content, FarmGame.Land.FarmLand land = null)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _layout = content.Layout ?? throw new InvalidOperationException("Content database has no farm layout.");
            _land = land;

            for (var i = 0; i < _layout.PlotCells.Count; i++)
            {
                _plotCells.Add(_layout.PlotCells[i]);
            }
        }

        public bool IsOccupied(Vector2Int cell)
        {
            return _occupiedCells.Contains(cell);
        }

        public void AddPlotCell(Vector2Int cell)
        {
            _plotCells.Add(cell);
        }

        public bool CanPlace(BuildingDefinition building, Vector2Int origin, out string reason)
        {
            if (building == null)
            {
                reason = "Unknown building.";
                return false;
            }

            var footprint = building.Footprint;
            for (var y = 0; y < footprint.y; y++)
            {
                for (var x = 0; x < footprint.x; x++)
                {
                    var cell = new Vector2Int(origin.x + x, origin.y + y);
                    if (cell.x < 0 || cell.x >= _layout.Width || cell.y < 0 || cell.y >= _layout.Height)
                    {
                        reason = "That building does not fit on the farm.";
                        return false;
                    }

                    if (_land != null && !_land.IsCellOwned(cell))
                    {
                        reason = "Land locked";
                        return false;
                    }

                    if (_plotCells.Contains(cell))
                    {
                        reason = "That spot overlaps a field plot.";
                        return false;
                    }

                    if (_occupiedCells.Contains(cell))
                    {
                        reason = "Another building is already there.";
                        return false;
                    }
                }
            }

            reason = null;
            return true;
        }

        public bool TryPlace(
            BuildingDefinition building,
            Vector2Int origin,
            Wallet wallet,
            out PlacedBuilding placed,
            out string reason)
        {
            placed = null;

            if (!CanPlace(building, origin, out reason))
            {
                return false;
            }

            if (wallet == null)
            {
                reason = "Wallet is unavailable.";
                return false;
            }

            if (!wallet.TrySpend(building.Cost))
            {
                reason = $"Need {building.Cost} coins to build {building.DisplayName}.";
                return false;
            }

            placed = Add(building, origin);
            Changed?.Invoke();
            return true;
        }

        public bool RestorePlacement(string buildingId, Vector2Int origin)
        {
            return RestorePlacement(buildingId, origin, 0, out _);
        }

        public bool RestorePlacement(
            string buildingId,
            Vector2Int origin,
            int animalCount,
            out PlacedBuilding placed,
            IReadOnlyList<string> animalKindIds = null)
        {
            placed = null;

            var building = _content.GetBuilding(buildingId);
            if (building == null || !CanPlace(building, origin, out _))
            {
                return false;
            }

            placed = Add(building, origin);
            placed.RestoreAnimals(animalCount, animalKindIds);
            return true;
        }

        /// <summary>
        /// Whether <paramref name="building"/> could move so its origin is <paramref name="origin"/>.
        /// The cells it stands on now count as free, so it can shift onto part of its own spot.
        /// </summary>
        public bool CanMove(PlacedBuilding building, Vector2Int origin, out string reason)
        {
            if (building == null || building.Definition == null || !_placed.Contains(building))
            {
                reason = "That building isn't on the farm.";
                return false;
            }

            SetCells(building, false);
            var canPlace = CanPlace(building.Definition, origin, out reason);
            SetCells(building, true);
            return canPlace;
        }

        /// <summary>Moves a placed building, keeping its animals and production.</summary>
        public bool TryMove(PlacedBuilding building, Vector2Int origin, out string reason)
        {
            if (!CanMove(building, origin, out reason))
            {
                return false;
            }

            if (building.Origin == origin)
            {
                return true;
            }

            SetCells(building, false);
            building.Origin = origin;
            SetCells(building, true);
            Changed?.Invoke();
            return true;
        }

        public bool TryAddAnimal(PlacedBuilding building, string kindId = null)
        {
            if (building == null || !building.HasAnimalSpace)
            {
                return false;
            }

            building.AddAnimal(kindId);
            return true;
        }

        /// <summary>Whether a tree that grows this fruit is on the farm.</summary>
        public bool OwnsFruitTree(string fruitId)
        {
            for (var i = 0; i < _placed.Count; i++)
            {
                var definition = _placed[i].Definition;
                if (definition != null && definition.IsFruitTree && definition.ProductId == fruitId)
                {
                    return true;
                }
            }

            return false;
        }

        public int GetCount(string buildingId)
        {
            var count = 0;
            for (var i = 0; i < _placed.Count; i++)
            {
                if (_placed[i].Definition.Id == buildingId)
                {
                    count++;
                }
            }

            return count;
        }

        private PlacedBuilding Add(BuildingDefinition building, Vector2Int origin)
        {
            var placed = new PlacedBuilding(building, origin);
            _placed.Add(placed);
            SetCells(placed, true);
            return placed;
        }

        /// <summary>Marks the cells under a building as occupied or free.</summary>
        private void SetCells(PlacedBuilding placed, bool occupied)
        {
            var footprint = placed.Definition.Footprint;
            for (var y = 0; y < footprint.y; y++)
            {
                for (var x = 0; x < footprint.x; x++)
                {
                    var cell = new Vector2Int(placed.Origin.x + x, placed.Origin.y + y);
                    if (occupied)
                    {
                        _occupiedCells.Add(cell);
                    }
                    else
                    {
                        _occupiedCells.Remove(cell);
                    }
                }
            }
        }
    }
}
