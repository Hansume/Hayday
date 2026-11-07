using System;
using System.Collections.Generic;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Economy;
using FarmGame.Inventory;
using UnityEngine;

namespace FarmGame.Farming
{
    public sealed class FarmSimulation
    {
        private sealed class PlotState
        {
            public string CropId;
            public long PlantedAtUtcTicks;
            public float GrowSeconds;
        }

        private readonly IClock _clock;
        private readonly ContentDatabase _content;
        private readonly FarmLayout _layout;
        private readonly List<PlotState> _plots = new List<PlotState>();
        private readonly List<Vector2Int> _plotCells = new List<Vector2Int>();
        private readonly HashSet<Vector2Int> _plotCellSet = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> _addedPlotCells = new List<Vector2Int>();

        public event Action<int> PlotChanged;
        public event Action<int> PlotAdded;

        public int PlotCount => _plots.Count;
        public IReadOnlyList<Vector2Int> PlotCells => _plotCells;
        public IReadOnlyList<Vector2Int> AddedPlotCells => _addedPlotCells;

        public FarmSimulation(IClock clock, ContentDatabase content, FarmLayout layout)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));

            for (var i = 0; i < _layout.PlotCells.Count; i++)
            {
                AddPlotCell(_layout.PlotCells[i], false);
            }
        }

        public bool IsPlotCell(Vector2Int cell)
        {
            return _plotCellSet.Contains(cell);
        }

        public bool TryGetPlotId(Vector2Int cell, out int plotId)
        {
            plotId = _plotCellSet.Contains(cell) ? _plotCells.IndexOf(cell) : -1;
            return plotId >= 0;
        }

        public bool CanAddPlot(Vector2Int cell)
        {
            return cell.x >= 0 &&
                   cell.x < _layout.Width &&
                   cell.y >= 0 &&
                   cell.y < _layout.Height &&
                   !_plotCellSet.Contains(cell);
        }

        public bool TryAddPlot(Vector2Int cell, out int plotId)
        {
            return TryAddPlot(cell, true, out plotId);
        }

        public bool RestoreAddedPlot(Vector2Int cell)
        {
            return TryAddPlot(cell, true, out _);
        }

        public bool TryPlant(int plotId, CropDefinition crop, Wallet wallet, out string failureReason)
        {
            if (crop == null || _content.GetCrop(crop.Id) == null)
            {
                failureReason = "Unknown crop.";
                return false;
            }

            if (!IsValidPlot(plotId))
            {
                failureReason = "That plot does not exist.";
                return false;
            }

            var plot = _plots[plotId];
            if (!string.IsNullOrEmpty(plot.CropId))
            {
                failureReason = "This plot is already planted.";
                return false;
            }

            if (wallet == null || !wallet.TrySpend(crop.SeedCost))
            {
                failureReason = $"Not enough coins. {crop.DisplayName} seeds cost {crop.SeedCost}.";
                return false;
            }

            plot.CropId = crop.Id;
            plot.PlantedAtUtcTicks = _clock.UtcNow.Ticks;
            plot.GrowSeconds = crop.GrowSeconds;
            failureReason = null;
            PlotChanged?.Invoke(plotId);
            return true;
        }

        public bool TryHarvest(
            int plotId,
            InventoryService inventory,
            out HarvestOutcome outcome,
            out string failureReason)
        {
            if (!IsValidPlot(plotId))
            {
                outcome = default;
                failureReason = "That plot does not exist.";
                return false;
            }

            var snapshot = GetSnapshot(plotId);
            if (snapshot.IsEmpty)
            {
                outcome = default;
                failureReason = "Nothing is planted here.";
                return false;
            }

            if (!snapshot.IsReady)
            {
                outcome = default;
                failureReason =
                    $"{snapshot.Crop.DisplayName} is ready in {FormatTime(snapshot.RemainingSeconds)}.";
                return false;
            }

            if (inventory == null)
            {
                outcome = default;
                failureReason = "Inventory is unavailable.";
                return false;
            }

            inventory.Add(snapshot.Crop.Id, snapshot.Crop.YieldAmount);
            _plots[plotId] = new PlotState();
            PlotChanged?.Invoke(plotId);

            outcome = new HarvestOutcome(
                true,
                snapshot.Crop.Id,
                snapshot.Crop.DisplayName,
                snapshot.Crop.YieldAmount,
                $"Harvested {snapshot.Crop.YieldAmount} {snapshot.Crop.DisplayName}.");
            failureReason = null;
            return true;
        }

        public PlotSnapshot GetSnapshot(int plotId)
        {
            if (!IsValidPlot(plotId))
            {
                return new PlotSnapshot(plotId, PlotPhase.Empty, null, 0, 1, 0f, 0d);
            }

            var plot = _plots[plotId];
            if (string.IsNullOrEmpty(plot.CropId))
            {
                return new PlotSnapshot(plotId, PlotPhase.Empty, null, 0, 1, 0f, 0d);
            }

            var crop = _content.GetCrop(plot.CropId);
            if (crop == null)
            {
                _plots[plotId] = new PlotState();
                return new PlotSnapshot(plotId, PlotPhase.Empty, null, 0, 1, 0f, 0d);
            }

            var growSeconds = Mathf.Max(1f, plot.GrowSeconds);
            var elapsedTicks = Math.Max(0L, _clock.UtcNow.Ticks - plot.PlantedAtUtcTicks);
            var elapsedSeconds = elapsedTicks / (double)TimeSpan.TicksPerSecond;
            var progress = Mathf.Clamp01((float)(elapsedSeconds / growSeconds));
            var stageCount = crop.StageSprites != null && crop.StageSprites.Length > 0
                ? crop.StageSprites.Length
                : 1;
            var ready = progress >= 1f;
            var stageIndex = ready
                ? stageCount - 1
                : Mathf.Clamp(Mathf.FloorToInt(progress * stageCount), 0, stageCount - 1);

            return new PlotSnapshot(
                plotId,
                ready ? PlotPhase.Ready : PlotPhase.Growing,
                crop,
                stageIndex,
                stageCount,
                progress,
                Math.Max(0d, growSeconds - elapsedSeconds));
        }

        public void DebugAdvanceAll(double seconds)
        {
            if (seconds <= 0d)
            {
                return;
            }

            var ticks = (long)(seconds * TimeSpan.TicksPerSecond);
            for (var i = 0; i < _plots.Count; i++)
            {
                var plot = _plots[i];
                if (string.IsNullOrEmpty(plot.CropId))
                {
                    continue;
                }

                plot.PlantedAtUtcTicks = Math.Max(0L, plot.PlantedAtUtcTicks - ticks);
                PlotChanged?.Invoke(i);
            }
        }

        public void RestorePlot(int plotId, string cropId, long plantedAtUtcTicks, float growSeconds)
        {
            if (!IsValidPlot(plotId) || string.IsNullOrEmpty(cropId))
            {
                return;
            }

            var crop = _content.GetCrop(cropId);
            if (crop == null)
            {
                return;
            }

            var resolvedGrowSeconds = growSeconds > 0f ? growSeconds : crop.GrowSeconds;
            if (plantedAtUtcTicks > _clock.UtcNow.Ticks)
            {
                plantedAtUtcTicks = _clock.UtcNow.Ticks;
            }

            _plots[plotId] = new PlotState
            {
                CropId = cropId,
                PlantedAtUtcTicks = plantedAtUtcTicks,
                GrowSeconds = resolvedGrowSeconds
            };
        }

        public bool TryGetCropId(int plotId, out string cropId, out long plantedAtUtcTicks, out float growSeconds)
        {
            cropId = null;
            plantedAtUtcTicks = 0L;
            growSeconds = 0f;

            if (!IsValidPlot(plotId))
            {
                return false;
            }

            var plot = _plots[plotId];
            if (string.IsNullOrEmpty(plot.CropId))
            {
                return false;
            }

            cropId = plot.CropId;
            plantedAtUtcTicks = plot.PlantedAtUtcTicks;
            growSeconds = plot.GrowSeconds;
            return true;
        }

        public static string FormatTime(double seconds)
        {
            if (seconds <= 0d)
            {
                return "0:00";
            }

            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
            var minutes = totalSeconds / 60;
            var remainder = totalSeconds % 60;
            if (minutes >= 60)
            {
                var hours = minutes / 60;
                return $"{hours}:{minutes % 60:00}:{remainder:00}";
            }

            return $"{minutes}:{remainder:00}";
        }

        private bool TryAddPlot(Vector2Int cell, bool added, out int plotId)
        {
            plotId = -1;
            if (!CanAddPlot(cell))
            {
                return false;
            }

            AddPlotCell(cell, added);
            plotId = _plots.Count - 1;
            PlotAdded?.Invoke(plotId);
            return true;
        }

        private void AddPlotCell(Vector2Int cell, bool added)
        {
            _plotCells.Add(cell);
            _plotCellSet.Add(cell);
            _plots.Add(new PlotState());
            if (added)
            {
                _addedPlotCells.Add(cell);
            }
        }

        private bool IsValidPlot(int plotId)
        {
            return plotId >= 0 && plotId < _plots.Count;
        }
    }
}
