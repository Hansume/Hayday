using FarmGame.Content;

namespace FarmGame.Farming
{
    public enum PlotPhase
    {
        Empty = 0,
        Growing = 1,
        Ready = 2
    }

    public readonly struct PlotSnapshot
    {
        public readonly int PlotId;
        public readonly PlotPhase Phase;
        public readonly CropDefinition Crop;
        public readonly int StageIndex;
        public readonly int StageCount;
        public readonly float Progress;
        public readonly double RemainingSeconds;

        public PlotSnapshot(
            int plotId,
            PlotPhase phase,
            CropDefinition crop,
            int stageIndex,
            int stageCount,
            float progress,
            double remainingSeconds)
        {
            PlotId = plotId;
            Phase = phase;
            Crop = crop;
            StageIndex = stageIndex;
            StageCount = stageCount;
            Progress = progress;
            RemainingSeconds = remainingSeconds;
        }

        public bool IsEmpty => Phase == PlotPhase.Empty;
        public bool IsReady => Phase == PlotPhase.Ready;
    }

    public readonly struct HarvestOutcome
    {
        public readonly bool Success;
        public readonly string CropId;
        public readonly string CropName;
        public readonly int Amount;
        public readonly string Message;

        public HarvestOutcome(bool success, string cropId, string cropName, int amount, string message)
        {
            Success = success;
            CropId = cropId;
            CropName = cropName;
            Amount = amount;
            Message = message;
        }
    }
}
