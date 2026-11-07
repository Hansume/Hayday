using System.Collections.Generic;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Farming;
using FarmGame.Grid;
using UnityEngine;
using UnityEngine.Rendering;

namespace FarmGame.View
{
    public sealed partial class FarmView : MonoBehaviour
    {
        private const string GrassPath = "Art/Placeholders/grass";
        private const string GrassTilePrefix = "Art/Ground/grass_";
        private const int GrassTileCount = 8;

        /// <summary>The first tiles on the grass sheet are plain grass; the rest have flowers.</summary>
        private const int PlainGrassTileCount = 4;

        /// <summary>Percent of cells that get a flower tile instead of plain grass.</summary>
        private const uint DecoratedGrassChance = 20u;
        private const string SoilPath = "Art/Placeholders/soil";

        /// <summary>The painted empty field (Tools > Farm Game > Rebuild Painted Art), used over the placeholder.</summary>
        private const string PaintedSoilPath = "Art/Field/field_empty";

        /// <summary>The empty field plot: the painted one if it has been made, otherwise the placeholder.</summary>
        public static Sprite LoadSoilSprite()
        {
            var painted = Resources.Load<Sprite>(PaintedSoilPath);
            return painted != null ? painted : Resources.Load<Sprite>(SoilPath);
        }
        private const string GroundSortingLayer = "Ground";
        private const string EntitySortingLayer = "Entities";

        private readonly List<PlotView> _plotViews = new List<PlotView>();
        private readonly List<BuildingPicture> _buildingPictures = new List<BuildingPicture>();

        /// <summary>A building's on-screen sprite, for tapping the picture rather than its cell.</summary>
        private readonly struct BuildingPicture
        {
            public readonly PlacedBuilding Building;
            public readonly SpriteRenderer Renderer;
            public readonly int SortingOrder;

            public BuildingPicture(PlacedBuilding building, SpriteRenderer renderer, int sortingOrder)
            {
                Building = building;
                Renderer = renderer;
                SortingOrder = sortingOrder;
            }
        }
        private readonly Dictionary<Vector2Int, int> _plotIdsByCell = new Dictionary<Vector2Int, int>();

        private GameSession _session;
        private ContentDatabase _content;
        private FarmSimulation _farm;
        private BuildingService _buildings;
        private FarmLayout _layout;
        private GameObject _buildingsRoot;
        private GameObject _plotsRoot;
        private Sprite _soilSprite;

        public IsoGrid Grid { get; private set; }

        public void Initialize(GameSession session)
        {
            _session = session;
            _content = session.Content;
            _farm = session.Farm;
            _buildings = session.Buildings;
            _layout = _content.Layout;
            Grid = new IsoGrid(_layout.Width, _layout.Height);
            _buildings.Changed += RebuildBuildings;

            var grass = Resources.Load<Sprite>(GrassPath);
            var soil = LoadSoilSprite();
            if (grass == null || soil == null)
            {
                Debug.LogError("Placeholder farm art is missing. Run Tools > Farm Game > Rebuild Everything.");
            }

            BuildGround(grass);
            InitializeLand();
            BuildBeach();
            BuildPlots(soil);
            _farm.PlotAdded += OnPlotAdded;
            RebuildBuildings();
            InitializeVisitors(session.Visitors);
        }

        private void OnDestroy()
        {
            if (_buildings != null)
            {
                _buildings.Changed -= RebuildBuildings;
            }

            if (_farm != null)
            {
                _farm.PlotAdded -= OnPlotAdded;
            }

            if (_session != null)
            {
                _session.Land.Expanded -= OnLandExpanded;
            }
        }

        public Vector3 GetPlotWorldPosition(int plotId)
        {
            if (_farm == null || plotId < 0 || plotId >= _farm.PlotCells.Count)
            {
                return Vector3.zero;
            }

            return Grid.CellToWorld(_farm.PlotCells[plotId]);
        }

        public bool TryGetPlotAtWorld(Vector3 worldPosition, out int plotId)
        {
            plotId = -1;
            if (Grid == null)
            {
                return false;
            }

            var cell = Grid.WorldToCell(worldPosition);
            if (!Grid.Contains(cell))
            {
                return false;
            }

            return _plotIdsByCell.TryGetValue(cell, out plotId);
        }

        public bool TryGetCellAtWorld(Vector3 worldPosition, out Vector2Int cell)
        {
            cell = default;
            if (Grid == null)
            {
                return false;
            }

            cell = Grid.WorldToCell(worldPosition);
            return Grid.Contains(cell);
        }

        /// <summary>
        /// The building under a tap. A tap on a building's own cells always picks it; a tap on a
        /// field plot never picks a building; anywhere else, a tap on the visible part of a
        /// building's picture picks it (the frontmost one if pictures overlap). The last rule is
        /// what lets a tall, narrow building like the notice board be tapped on its board
        /// rather than only at its feet.
        /// </summary>
        public bool TryGetBuildingAtWorld(Vector3 worldPosition, out PlacedBuilding building)
        {
            building = null;
            if (Grid == null || _buildings == null)
            {
                return false;
            }

            var cell = Grid.WorldToCell(worldPosition);
            if (Grid.Contains(cell))
            {
                if (TryGetBuildingAtCell(cell, out building))
                {
                    return true;
                }

                if (_plotIdsByCell.ContainsKey(cell))
                {
                    return false;
                }
            }

            return TryGetBuildingPictureAt(worldPosition, out building);
        }

        private bool TryGetBuildingPictureAt(Vector3 worldPosition, out PlacedBuilding building)
        {
            building = null;
            var bestOrder = int.MinValue;
            for (var i = 0; i < _buildingPictures.Count; i++)
            {
                var picture = _buildingPictures[i];
                if (picture.Renderer == null || picture.SortingOrder <= bestOrder)
                {
                    continue;
                }

                var bounds = picture.Renderer.bounds;
                var point = new Vector3(worldPosition.x, worldPosition.y, bounds.center.z);
                if (picture.Renderer.enabled && bounds.Contains(point) && IsOpaqueAt(picture.Renderer, worldPosition))
                {
                    building = picture.Building;
                    bestOrder = picture.SortingOrder;
                }
            }

            return building != null;
        }

        /// <summary>
        /// True when the sprite has a visible pixel at this point. Building textures are imported
        /// readable for this; if one isn't, the whole sprite rectangle counts.
        /// </summary>
        private static bool IsOpaqueAt(SpriteRenderer renderer, Vector3 worldPosition)
        {
            var sprite = renderer.sprite;
            var texture = sprite != null ? sprite.texture : null;
            if (texture == null || !texture.isReadable)
            {
                return true;
            }

            var local = renderer.transform.InverseTransformPoint(worldPosition);
            var rect = sprite.textureRect;
            var x = rect.x + sprite.pivot.x + local.x * sprite.pixelsPerUnit;
            var y = rect.y + sprite.pivot.y + local.y * sprite.pixelsPerUnit;
            if (x < rect.xMin || x >= rect.xMax || y < rect.yMin || y >= rect.yMax)
            {
                return false;
            }

            return texture.GetPixelBilinear(x / texture.width, y / texture.height).a > 0.3f;
        }

        private bool TryGetBuildingAtCell(Vector2Int cell, out PlacedBuilding building)
        {
            building = null;
            var placedBuildings = _buildings.Placed;
            for (var i = 0; i < placedBuildings.Count; i++)
            {
                var placed = placedBuildings[i];
                if (placed.Definition == null)
                {
                    continue;
                }

                var footprint = placed.Definition.Footprint;
                if (cell.x >= placed.Origin.x &&
                    cell.x < placed.Origin.x + footprint.x &&
                    cell.y >= placed.Origin.y &&
                    cell.y < placed.Origin.y + footprint.y)
                {
                    building = placed;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Shows or hides a building's picture, e.g. while it's being moved.</summary>
        public void SetBuildingHidden(PlacedBuilding building, bool hidden)
        {
            for (var i = 0; i < _buildingPictures.Count; i++)
            {
                var picture = _buildingPictures[i];
                if (picture.Building == building && picture.Renderer != null)
                {
                    picture.Renderer.enabled = !hidden;
                }
            }
        }

        public Vector3 GetCellWorldPosition(Vector2Int cell)
        {
            return Grid != null ? Grid.CellToWorld(cell) : Vector3.zero;
        }

        public Vector3 GetBuildingWorldPosition(BuildingDefinition building, Vector2Int origin)
        {
            if (building == null || Grid == null)
            {
                return Vector3.zero;
            }

            var footprint = building.Footprint;
            var centerCell = new Vector2(
                origin.x + (footprint.x - 1) * 0.5f,
                origin.y + (footprint.y - 1) * 0.5f);

            return Grid.CellToWorld(centerCell);
        }

        public float GetBuildingVisualScale(BuildingDefinition building)
        {
            if (building == null || building.WorldSprite == null)
            {
                return 1f;
            }

            var spriteWidth = building.WorldSprite.bounds.size.x;
            if (spriteWidth <= 0.0001f)
            {
                return 1f;
            }

            return IsoMath.FootprintWorldWidth(building.Footprint) / spriteWidth;
        }

        private void Update()
        {
            if (_farm == null)
            {
                return;
            }

            for (var i = 0; i < _plotViews.Count; i++)
            {
                _plotViews[i].Refresh(_farm.GetSnapshot(i));
            }

            UpdateFruitTrees();
            UpdateVisitors(Time.deltaTime);
            UpdateLandSign(Time.deltaTime);
        }

        /// <summary>Trees show their fruit once it can be picked.</summary>
        private void UpdateFruitTrees()
        {
            for (var i = 0; i < _buildingPictures.Count; i++)
            {
                var picture = _buildingPictures[i];
                if (picture.Renderer == null || !picture.Building.Definition.IsFruitTree)
                {
                    continue;
                }

                var sprite = _session.IsFruitReady(picture.Building)
                    ? picture.Building.Definition.WorldSprite
                    : picture.Building.Definition.GrowingSprite;
                if (picture.Renderer.sprite != sprite)
                {
                    picture.Renderer.sprite = sprite;
                }
            }
        }

        private void RebuildBuildings()
        {
            if (_buildingsRoot != null)
            {
                Destroy(_buildingsRoot);
                _buildingsRoot = null;
            }

            if (_buildings == null)
            {
                return;
            }

            _buildingsRoot = new GameObject("Buildings");
            _buildingsRoot.transform.SetParent(transform, false);
            _buildingPictures.Clear();

            for (var i = 0; i < _buildings.Placed.Count; i++)
            {
                var placed = _buildings.Placed[i];
                if (placed.Definition == null)
                {
                    continue;
                }

                var footprint = placed.Definition.Footprint;
                var frontCell = new Vector2Int(
                    placed.Origin.x + footprint.x - 1,
                    placed.Origin.y + footprint.y - 1);

                var buildingObject = new GameObject(
                    $"Building_{placed.Definition.Id}_{placed.Origin.x}_{placed.Origin.y}");
                buildingObject.transform.SetParent(_buildingsRoot.transform, false);
                buildingObject.transform.position =
                    GetBuildingWorldPosition(placed.Definition, placed.Origin);

                var group = buildingObject.AddComponent<SortingGroup>();
                group.sortingLayerName = EntitySortingLayer;
                group.sortingOrder = 50 + (frontCell.x + frontCell.y) * 10 + 5;

                if (placed.Definition.WorldSprite == null)
                {
                    continue;
                }

                var spriteObject = new GameObject("Sprite");
                spriteObject.transform.SetParent(buildingObject.transform, false);
                var scale = GetBuildingVisualScale(placed.Definition);
                spriteObject.transform.localScale = new Vector3(scale, scale, 1f);
                var renderer = spriteObject.AddComponent<SpriteRenderer>();
                renderer.sprite = placed.Definition.WorldSprite;
                renderer.sortingLayerName = EntitySortingLayer;
                renderer.sortingOrder = 0;
                _buildingPictures.Add(new BuildingPicture(placed, renderer, group.sortingOrder));
            }

            UpdateFruitTrees();
        }

        private void BuildGround(Sprite grass)
        {
            var groundRoot = new GameObject("Ground");
            groundRoot.transform.SetParent(transform, false);

            var painted = LoadGrassTiles();
            var usePainted = painted.Count > 0;
            _groundTiles = new SpriteRenderer[_layout.Width, _layout.Height];

            for (var y = 0; y < _layout.Height; y++)
            {
                for (var x = 0; x < _layout.Width; x++)
                {
                    var tile = new GameObject($"Grass_{x}_{y}");
                    tile.transform.SetParent(groundRoot.transform, false);
                    tile.transform.position = Grid.CellToWorld(new Vector2Int(x, y));

                    var renderer = tile.AddComponent<SpriteRenderer>();
                    renderer.sortingLayerName = GroundSortingLayer;

                    if (usePainted)
                    {
                        // Front tiles draw over back ones where their edges overlap.
                        renderer.sortingOrder = x + y;
                        renderer.sprite = PickGrassTile(painted, x, y, out var flip);
                        renderer.flipX = flip;
                    }
                    else
                    {
                        renderer.sortingOrder = 0;
                        renderer.sprite = grass;
                        renderer.color = ((x + y) & 1) == 0
                            ? Color.white
                            : new Color(0.93f, 0.97f, 0.9f, 1f);
                    }

                    _groundTiles[x, y] = renderer;
                }
            }
        }

        /// <summary>
        /// The painted grass tiles, in order: plain ones first, then decorated (flower) ones.
        /// Empty if they haven't been built yet (Tools > Farm Game > Rebuild Grass Tiles).
        /// </summary>
        private static List<Sprite> LoadGrassTiles()
        {
            var tiles = new List<Sprite>();
            for (var i = 0; i < GrassTileCount; i++)
            {
                var sprite = Resources.Load<Sprite>(GrassTilePrefix + i);
                if (sprite != null)
                {
                    tiles.Add(sprite);
                }
            }

            return tiles;
        }

        /// <summary>
        /// Picks a tile for a cell. The same cell always gets the same tile, so the farm looks the
        /// same every visit. Plain grass is common; flower tiles are sprinkled in.
        /// </summary>
        private static Sprite PickGrassTile(List<Sprite> tiles, int x, int y, out bool flip)
        {
            var hash = unchecked((uint)(x * 73856093) ^ (uint)(y * 19349663));
            hash ^= hash >> 13;
            hash *= 0x5bd1e995;
            hash ^= hash >> 15;

            flip = (hash & 1u) != 0;
            var roll = (hash >> 1) % 100u;
            var plainCount = Mathf.Min(PlainGrassTileCount, tiles.Count);
            var decoratedCount = tiles.Count - plainCount;

            if (decoratedCount > 0 && roll < DecoratedGrassChance)
            {
                return tiles[plainCount + (int)((hash >> 8) % (uint)decoratedCount)];
            }

            return tiles[(int)((hash >> 8) % (uint)plainCount)];
        }

        private void BuildPlots(Sprite soil)
        {
            _soilSprite = soil;
            _plotsRoot = new GameObject("Plots");
            _plotsRoot.transform.SetParent(transform, false);

            for (var plotId = 0; plotId < _farm.PlotCount; plotId++)
            {
                CreatePlotView(plotId, _farm.PlotCells[plotId]);
            }
        }

        private void OnPlotAdded(int plotId)
        {
            if (plotId < 0 || plotId >= _farm.PlotCells.Count)
            {
                return;
            }

            CreatePlotView(plotId, _farm.PlotCells[plotId]);
        }

        private void CreatePlotView(int plotId, Vector2Int cell)
        {
            _plotIdsByCell[cell] = plotId;

            var plot = new GameObject($"Plot_{plotId}");
            plot.transform.SetParent(_plotsRoot.transform, false);
            plot.transform.position = Grid.CellToWorld(cell);

            var group = plot.AddComponent<SortingGroup>();
            group.sortingLayerName = EntitySortingLayer;
            group.sortingOrder = 50 + (cell.x + cell.y) * 10;

            var soilObject = new GameObject("Soil");
            soilObject.transform.SetParent(plot.transform, false);
            var soilRenderer = soilObject.AddComponent<SpriteRenderer>();
            soilRenderer.sprite = _soilSprite;
            soilRenderer.sortingLayerName = EntitySortingLayer;
            soilRenderer.sortingOrder = 0;

            var cropObject = new GameObject("Crop");
            cropObject.transform.SetParent(plot.transform, false);
            var cropRenderer = cropObject.AddComponent<SpriteRenderer>();
            cropRenderer.sortingLayerName = EntitySortingLayer;
            cropRenderer.sortingOrder = 1;

            var view = plot.AddComponent<PlotView>();
            view.Initialize(cropRenderer);
            _plotViews.Add(view);

            view.Refresh(_farm.GetSnapshot(plotId));
        }
    }
}
