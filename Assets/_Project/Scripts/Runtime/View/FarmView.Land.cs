using UnityEngine;
using UnityEngine.Rendering;

namespace FarmGame.View
{
    /// <summary>
    /// Land the player doesn't own yet has darker grass. Tapping a section that can be bought
    /// marks its cells and pops up a deed icon there; tapping that opens the land window. Only
    /// one is shown at a time, and it goes away on the next tap anywhere else.
    /// </summary>
    public sealed partial class FarmView
    {
        private const string DeedIconPath = "Art/Placeholders/icon_land_deed";
        private const string CellMarkPath = "Art/Placeholders/placement_tile";

        /// <summary>Above buildings and visitors, below the placement preview.</summary>
        private const int LandSignSortingOrder = 8000;

        private const float LandSignScale = 0.7f;
        private const float LandSignPopSeconds = 0.22f;

        /// <summary>Multiplies the colour of grass on land the player doesn't own.</summary>
        private static readonly Color LockedGrassTint = new Color(0.58f, 0.62f, 0.56f, 1f);

        private static readonly Color LandMarkColor = new Color(1f, 0.92f, 0.45f, 0.6f);

        private SpriteRenderer[,] _groundTiles;
        private GameObject _landSign;
        private SpriteRenderer _landSignRenderer;
        private GameObject _landMarksRoot;
        private float _landSignPop;

        /// <summary>The section the deed icon is showing on. Only meaningful while <see cref="IsLandSignVisible"/>.</summary>
        public Vector2Int LandSignSection { get; private set; }

        public bool IsLandSignVisible => _landSign != null && _landSign.activeSelf;

        private void InitializeLand()
        {
            for (var y = 0; y < _layout.Height; y++)
            {
                for (var x = 0; x < _layout.Width; x++)
                {
                    if (!_session.Land.IsCellOwned(new Vector2Int(x, y)))
                    {
                        _groundTiles[x, y].color *= LockedGrassTint;
                    }
                }
            }

            BuildLandSign();
            _session.Land.Expanded += OnLandExpanded;
        }

        private void BuildLandSign()
        {
            _landSign = new GameObject("LandSign");
            _landSign.transform.SetParent(transform, false);
            var group = _landSign.AddComponent<SortingGroup>();
            group.sortingLayerName = EntitySortingLayer;
            group.sortingOrder = LandSignSortingOrder;
            _landSignRenderer = _landSign.AddComponent<SpriteRenderer>();
            _landSignRenderer.sprite = Resources.Load<Sprite>(DeedIconPath);
            _landSignRenderer.sortingLayerName = EntitySortingLayer;
            _landSign.SetActive(false);

            _landMarksRoot = new GameObject("LandMarks");
            _landMarksRoot.transform.SetParent(transform, false);
            var mark = Resources.Load<Sprite>(CellMarkPath);
            var size = _session.Land.SectionSize;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cell = new GameObject($"LandMark_{x}_{y}");
                    cell.transform.SetParent(_landMarksRoot.transform, false);
                    cell.transform.localPosition = IsoMathOffset(x, y);
                    var renderer = cell.AddComponent<SpriteRenderer>();
                    renderer.sprite = mark;
                    renderer.color = LandMarkColor;
                    renderer.sortingLayerName = GroundSortingLayer;
                    renderer.sortingOrder = 100;
                }
            }

            _landMarksRoot.SetActive(false);
        }

        /// <summary>A cell's position relative to the first cell of its section.</summary>
        private Vector3 IsoMathOffset(int x, int y)
        {
            return Grid.CellToWorld(new Vector2Int(x, y)) - Grid.CellToWorld(Vector2Int.zero);
        }

        /// <summary>Marks a section and pops up the deed icon on it.</summary>
        public void ShowLandSign(Vector2Int section)
        {
            if (_landSign == null)
            {
                return;
            }

            LandSignSection = section;
            _landSign.transform.position = Grid.CellToWorld(_session.Land.SectionCenter(section));
            _landSign.transform.localScale = Vector3.zero;
            _landSignPop = 0f;
            _landSign.SetActive(true);

            var size = _session.Land.SectionSize;
            _landMarksRoot.transform.position = Grid.CellToWorld(new Vector2Int(section.x * size, section.y * size));
            _landMarksRoot.SetActive(true);
        }

        public void HideLandSign()
        {
            if (_landSign != null)
            {
                _landSign.SetActive(false);
                _landMarksRoot.SetActive(false);
            }
        }

        /// <summary>True when a tap is on the showing deed icon, or anywhere on the section it marks.</summary>
        public bool IsOverLandSign(Vector3 worldPosition)
        {
            if (!IsLandSignVisible)
            {
                return false;
            }

            var bounds = _landSignRenderer.bounds;
            if (bounds.Contains(new Vector3(worldPosition.x, worldPosition.y, bounds.center.z)))
            {
                return true;
            }

            return TryGetLockedSectionAtWorld(worldPosition, out var section) && section == LandSignSection;
        }

        private void UpdateLandSign(float deltaTime)
        {
            if (!IsLandSignVisible || _landSignPop >= 1f)
            {
                return;
            }

            // Grows in, overshooting a little before it settles.
            _landSignPop = Mathf.Min(1f, _landSignPop + deltaTime / LandSignPopSeconds);
            var scale = LandSignScale * _landSignPop * (1f + 0.3f * Mathf.Sin(_landSignPop * Mathf.PI));
            _landSign.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void OnLandExpanded()
        {
            RefreshBoughtGround();
            HideLandSign();
            MoveVisitorsToFrontEdge();
        }

        /// <summary>Takes the tint off newly bought land. Only darkened tiles are changed.</summary>
        private void RefreshBoughtGround()
        {
            var size = _session.Land.SectionSize;
            for (var y = 0; y < _layout.Height; y += size)
            {
                for (var x = 0; x < _layout.Width; x += size)
                {
                    if (_session.Land.IsCellOwned(new Vector2Int(x, y)) && IsTinted(_groundTiles[x, y]))
                    {
                        ClearTint(x, y, size);
                    }
                }
            }
        }

        private void ClearTint(int startX, int startY, int size)
        {
            for (var y = startY; y < Mathf.Min(startY + size, _layout.Height); y++)
            {
                for (var x = startX; x < Mathf.Min(startX + size, _layout.Width); x++)
                {
                    var tile = _groundTiles[x, y];
                    var color = tile.color;
                    tile.color = new Color(
                        color.r / LockedGrassTint.r,
                        color.g / LockedGrassTint.g,
                        color.b / LockedGrassTint.b,
                        color.a);
                }
            }
        }

        /// <summary>Owned tiles are never darker than this, whichever grass art is in use.</summary>
        private static bool IsTinted(SpriteRenderer tile)
        {
            return tile.color.r < 0.8f;
        }

        /// <summary>The section under a tap, when it's on land the player doesn't own.</summary>
        public bool TryGetLockedSectionAtWorld(Vector3 worldPosition, out Vector2Int section)
        {
            section = default;
            if (Grid == null || _session == null)
            {
                return false;
            }

            var cell = Grid.WorldToCell(worldPosition);
            if (!Grid.Contains(cell) || _session.Land.IsCellOwned(cell))
            {
                return false;
            }

            section = _session.Land.SectionOf(cell);
            return true;
        }
    }
}
