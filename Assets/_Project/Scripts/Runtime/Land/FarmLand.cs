using System;
using System.Collections.Generic;
using FarmGame.Content;
using UnityEngine;

namespace FarmGame.Land
{
    /// <summary>
    /// Which parts of the map the player owns. The map is cut into square sections. A new farm owns
    /// those in the layout's start area; each Land Deed buys one more, next to land already owned.
    /// Nothing can be built or planted on land that isn't owned.
    ///
    /// Deeds are counted here rather than in the inventory: they take no storage and can't be sold.
    /// </summary>
    public sealed class FarmLand
    {
        public const string DeedName = "Land Deed";

        private readonly int _width;
        private readonly int _height;
        private readonly RectInt _startArea;
        private readonly bool[,] _starting;
        private readonly bool[,] _owned;

        /// <summary>Raised after a section is bought.</summary>
        public event Action Expanded;

        public int SectionSize { get; }
        public int SectionsWide { get; }
        public int SectionsHigh { get; }

        /// <summary>Deeds the player holds.</summary>
        public int Deeds { get; private set; }

        public FarmLand(FarmLayout layout, int deeds = 0, IEnumerable<Vector2Int> boughtSections = null)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            _width = layout.Width;
            _height = layout.Height;
            _startArea = layout.StartArea;
            SectionSize = layout.SectionSize;
            SectionsWide = (_width + SectionSize - 1) / SectionSize;
            SectionsHigh = (_height + SectionSize - 1) / SectionSize;
            Deeds = Math.Max(0, deeds);

            _starting = new bool[SectionsWide, SectionsHigh];
            _owned = new bool[SectionsWide, SectionsHigh];
            for (var y = 0; y < SectionsHigh; y++)
            {
                for (var x = 0; x < SectionsWide; x++)
                {
                    var section = new RectInt(x * SectionSize, y * SectionSize, SectionSize, SectionSize);
                    _starting[x, y] = section.Overlaps(_startArea);
                    _owned[x, y] = _starting[x, y];
                }
            }

            if (boughtSections == null)
            {
                return;
            }

            foreach (var section in boughtSections)
            {
                if (IsSection(section))
                {
                    _owned[section.x, section.y] = true;
                }
            }
        }

        public bool IsSection(Vector2Int section)
        {
            return section.x >= 0 && section.x < SectionsWide && section.y >= 0 && section.y < SectionsHigh;
        }

        public Vector2Int SectionOf(Vector2Int cell)
        {
            return new Vector2Int(FloorDivide(cell.x), FloorDivide(cell.y));
        }

        /// <summary>The middle of a section, in cells.</summary>
        public Vector2 SectionCenter(Vector2Int section)
        {
            var half = (SectionSize - 1) * 0.5f;
            return new Vector2(section.x * SectionSize + half, section.y * SectionSize + half);
        }

        public bool IsOwned(Vector2Int section)
        {
            return IsSection(section) && _owned[section.x, section.y];
        }

        /// <summary>True for a cell on the map, on land the player owns.</summary>
        public bool IsCellOwned(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < _width && cell.y >= 0 && cell.y < _height && IsOwned(SectionOf(cell));
        }

        /// <summary>True for an unowned section touching owned land along one side.</summary>
        public bool CanBuy(Vector2Int section)
        {
            return IsSection(section) &&
                   !IsOwned(section) &&
                   (IsOwned(section + Vector2Int.left) ||
                    IsOwned(section + Vector2Int.right) ||
                    IsOwned(section + Vector2Int.up) ||
                    IsOwned(section + Vector2Int.down));
        }

        public int LockedSectionCount
        {
            get
            {
                var count = 0;
                for (var y = 0; y < SectionsHigh; y++)
                {
                    for (var x = 0; x < SectionsWide; x++)
                    {
                        if (!_owned[x, y])
                        {
                            count++;
                        }
                    }
                }

                return count;
            }
        }

        /// <summary>False once the player holds enough deeds to buy all the land that's left.</summary>
        public bool NeedsMoreDeeds => LockedSectionCount > Deeds;

        public void AddDeed()
        {
            Deeds++;
        }

        /// <summary>Spends one deed on a section. Nothing changes if it can't be bought.</summary>
        public bool TryBuy(Vector2Int section)
        {
            if (Deeds <= 0 || !CanBuy(section))
            {
                return false;
            }

            Deeds--;
            _owned[section.x, section.y] = true;
            Expanded?.Invoke();
            return true;
        }

        /// <summary>The sections bought so far, which is what a save has to remember.</summary>
        public List<Vector2Int> GetBoughtSections()
        {
            var bought = new List<Vector2Int>();
            for (var y = 0; y < SectionsHigh; y++)
            {
                for (var x = 0; x < SectionsWide; x++)
                {
                    if (_owned[x, y] && !_starting[x, y])
                    {
                        bought.Add(new Vector2Int(x, y));
                    }
                }
            }

            return bought;
        }

        /// <summary>
        /// The row just past the owned land, going forward from the start area along column
        /// <paramref name="cellX"/>. Visitors stand here, so they stay off land that can be built on.
        /// </summary>
        public int GetFrontEdge(int cellX)
        {
            var row = _startArea.yMax;
            while (row < _height && IsCellOwned(new Vector2Int(cellX, row)))
            {
                row += SectionSize;
            }

            return Math.Min(row, _height);
        }

        private int FloorDivide(int cell)
        {
            return cell >= 0 ? cell / SectionSize : (cell - SectionSize + 1) / SectionSize;
        }
    }
}
