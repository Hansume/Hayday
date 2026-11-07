using System.Collections.Generic;
using UnityEngine;

namespace FarmGame.Content
{
    /// <summary>
    /// The shape of the farm: the whole map, the field plots a new farm starts with, the land a
    /// new farm owns (<see cref="StartArea"/>) and the size of the square sections the rest of the
    /// map is bought in (see FarmGame.Land.FarmLand).
    /// </summary>
    [CreateAssetMenu(menuName = "Farm Game/Farm Layout", fileName = "FarmLayout")]
    public sealed class FarmLayout : ScriptableObject
    {
        public const int DefaultSectionSize = 2;

        [SerializeField] private int width = 12;
        [SerializeField] private int height = 12;
        [SerializeField] private List<Vector2Int> plotCells = new List<Vector2Int>();

        // Zero size means the player owns the whole map from the start.
        [SerializeField] private RectInt startArea;
        [SerializeField] private int sectionSize = DefaultSectionSize;

        public int Width => Mathf.Max(1, width);
        public int Height => Mathf.Max(1, height);
        public IReadOnlyList<Vector2Int> PlotCells => plotCells;
        public int SectionSize => Mathf.Max(1, sectionSize);

        /// <summary>The land a new farm owns. Line it up with the sections.</summary>
        public RectInt StartArea => startArea.width > 0 && startArea.height > 0
            ? startArea
            : new RectInt(0, 0, Width, Height);

        public void Configure(
            int layoutWidth,
            int layoutHeight,
            IEnumerable<Vector2Int> cells,
            RectInt? ownedAtStart = null,
            int landSectionSize = DefaultSectionSize)
        {
            width = Mathf.Max(1, layoutWidth);
            height = Mathf.Max(1, layoutHeight);
            plotCells = new List<Vector2Int>(cells);
            startArea = ownedAtStart ?? new RectInt(0, 0, width, height);
            sectionSize = Mathf.Max(1, landSectionSize);
        }
    }
}
