using FarmGame.Grid;
using FarmGame.UI;
using UnityEngine;

namespace FarmGame.View
{
    /// <summary>
    /// The Beach, in the farm's isometric view: sand with the farm's framing, and the sea on one
    /// side only, past a straight coast running along an isometric axis. A dock leads out from the
    /// coast, and tapping it goes to the open sea, a stretch of water with only a boat on it. Both
    /// are built well away from the farm so they never share the screen, and outside the farm grid
    /// so nothing can be planted or placed there. The camera goes there when the player taps the
    /// Beach button or the dock (see GameUI.Beach).
    /// </summary>
    public sealed partial class FarmView
    {
        private const string BeachArtPrefix = "Art/Placeholders/beach_";

        /// <summary>The camera frames as many cells across as the farm's first area.</summary>
        private const int BeachCells = 12;

        /// <summary>The waterline: cells with a larger first coordinate than this are out at sea.</summary>
        private const float CoastCell = 6f;

        /// <summary>Along the coast, and straight inland from it. The art must match (PlaceholderArtGenerator.Beach).</summary>
        private static readonly Vector2 CoastAlong = new Vector2(2f, 1f).normalized;
        private static readonly Vector2 CoastInland = new Vector2(-1f, 2f).normalized;

        /// <summary>World units straight inland for each cell walked along the first axis (2 / sqrt 5).</summary>
        private const float CoastDepthPerCell = 0.894f;

        /// <summary>Long enough to run past both sides of any screen, zoomed all the way out.</summary>
        private const float CoastLength = 124f;

        /// <summary>How far inland, past the waterline's cell, the plain sand starts under the shore picture.</summary>
        private const float SandStartsInland = 1f;

        /// <summary>How far inland of the beach's middle the sand reaches.</summary>
        private const float SandReach = 42f;

        /// <summary>Where the dock lies, in cells: its middle, half its length out to sea, half its width.</summary>
        private static readonly Vector2 DockCell = new Vector2(7f, 0.6f);
        private const float DockHalfLength = 2f;
        private const float DockHalfWidth = 0.7f;

        /// <summary>A tap this many cells off the dock still counts, since it's a narrow thing to hit.</summary>
        private const float DockTapMargin = 0.3f;

        /// <summary>The boat tied up on the dock's near side, clear of the button in the corner.</summary>
        private static readonly Vector2 MooredBoatCell = new Vector2(8.5f, 2.1f);

        /// <summary>Sea tile size in world units: two cells' worth of the 2:1 grid, four times over.</summary>
        private static readonly Vector2 SeaTile = new Vector2(8f, 4f);

        /// <summary>Wide and tall enough to fill any screen shape, zoomed all the way out.</summary>
        private static readonly Vector2 SeaSize = new Vector2(120f, 60f);

        public static readonly Vector3 BeachCenter = new Vector3(0f, -120f, 0f);

        /// <summary>The middle of the open sea, far enough off that no shore shows, zoomed all the way out.</summary>
        public static readonly Vector3 OpenSeaCenter = new Vector3(0f, -200f, 0f);

        /// <summary>Where the Cocktail Bar stands: towards the back of the sand, in the middle.</summary>
        private static readonly Vector2 BarCell = new Vector2(-1.5f, -1.5f);

        /// <summary>The Cocktail Bar's width in world units, about three cells across.</summary>
        private const float BarWorldWidth = 5.5f;

        private SpriteRenderer _cocktailBarRenderer;
        private SpriteRenderer _mooredBoatRenderer;

        /// <summary>Where the camera may go at the beach: the same room to pan as on the farm.</summary>
        public static Bounds BeachBounds =>
            new Bounds(BeachCenter, new Vector3(BeachCells * IsoMath.TileWidth, BeachCells * IsoMath.TileHeight, 1f));

        /// <summary>Where the camera may go on the open sea.</summary>
        public static Bounds OpenSeaBounds => new Bounds(OpenSeaCenter, BeachBounds.size);

        private void BuildBeach()
        {
            var seaSprite = Resources.Load<Sprite>(BeachArtPrefix + "sea");
            var landSprite = Resources.Load<Sprite>(BeachArtPrefix + "land");
            var shoreSprite = Resources.Load<Sprite>(BeachArtPrefix + "shore");
            var surfSprite = Resources.Load<Sprite>(BeachArtPrefix + "surf");
            if (seaSprite == null || landSprite == null || shoreSprite == null || surfSprite == null)
            {
                Debug.LogError("Beach art is missing. Run Tools > Farm Game > Rebuild Beach Art.");
                return;
            }

            var root = new GameObject("Beach");
            root.transform.SetParent(transform, false);
            root.transform.position = BeachCenter;

            var waves = BuildWater(root, "BeachSea", "BeachGlints", seaSprite);

            // The coast runs along an isometric axis, so these lie at its slope, with their tops inland.
            var coastAngle = Mathf.Atan2(CoastAlong.y, CoastAlong.x) * Mathf.Rad2Deg;
            var waterline = CoastInland * (-CoastCell * CoastDepthPerCell);

            var surf = CreateCoastSprite(root.transform, "BeachSurf", surfSprite, 2, coastAngle, waterline, surfSprite.bounds.size.y);
            waves.SetSurf(surf.transform, CoastInland);

            var sandEdge = -(CoastCell - SandStartsInland) * CoastDepthPerCell;
            CreateCoastSprite(
                root.transform,
                "BeachLand",
                landSprite,
                3,
                coastAngle,
                CoastInland * ((sandEdge + SandReach) * 0.5f),
                SandReach - sandEdge);
            CreateCoastSprite(root.transform, "BeachShore", shoreSprite, 4, coastAngle, waterline, shoreSprite.bounds.size.y);

            DecorateBeach(root.transform);
            BuildCocktailBar(root.transform);
            BuildDock(root.transform);
            BuildOpenSea(seaSprite);
        }

        /// <summary>The sea with its drifting highlights, filling a location. Returns what animates it.</summary>
        private BeachWaves BuildWater(GameObject root, string seaName, string glintsName, Sprite seaSprite)
        {
            var sea = CreateBeachSprite(root.transform, seaName, seaSprite, 0);
            sea.drawMode = SpriteDrawMode.Tiled;
            sea.tileMode = SpriteTileMode.Continuous;
            sea.size = SeaSize;

            var waves = root.AddComponent<BeachWaves>();
            var glintsSprite = Resources.Load<Sprite>(BeachArtPrefix + "glints");
            if (glintsSprite != null)
            {
                // One layer drifts along each isometric axis, wrapping after one tile.
                waves.AddDrift(CreateGlints(root.transform, glintsName + "Near", glintsSprite, 1f).transform, SeaTile, 0.03f);
                waves.AddDrift(
                    CreateGlints(root.transform, glintsName + "Far", glintsSprite, 0.55f).transform,
                    new Vector2(SeaTile.x, -SeaTile.y),
                    0.018f);
            }

            return waves;
        }

        private static SpriteRenderer CreateCoastSprite(
            Transform root,
            string name,
            Sprite sprite,
            int order,
            float angle,
            Vector2 position,
            float height)
        {
            var renderer = CreateBeachSprite(root, name, sprite, order);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = new Vector2(CoastLength, height);
            renderer.transform.localPosition = position;
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            return renderer;
        }

        /// <summary>The dock, reaching from the sand out over the water, with the boat tied up beside it.</summary>
        private void BuildDock(Transform root)
        {
            var dockSprite = Resources.Load<Sprite>(BeachArtPrefix + "dock");
            var boatSprite = Resources.Load<Sprite>(BeachArtPrefix + "boat");
            if (dockSprite == null || boatSprite == null)
            {
                Debug.LogError("Dock art is missing. Run Tools > Farm Game > Rebuild Beach Art.");
                return;
            }

            // Sorted from its landward end, so palms further forward on the sand stand in front of it.
            var dock = CreateBeachSprite(root, "BeachDock", dockSprite, 100 + Mathf.RoundToInt((DockCell.x - DockHalfLength + DockCell.y) * 4f));
            dock.transform.localPosition = IsoMath.CellToWorld(DockCell);

            _mooredBoatRenderer = CreateBeachSprite(root, "BeachBoat", boatSprite, dock.sortingOrder + 1);
            _mooredBoatRenderer.transform.localPosition = IsoMath.CellToWorld(MooredBoatCell);
            root.GetComponent<BeachWaves>().SetBoat(_mooredBoatRenderer.transform);
        }

        /// <summary>The middle of the sea: nothing but water and the boat that brought the player here.</summary>
        private void BuildOpenSea(Sprite seaSprite)
        {
            var root = new GameObject("OpenSea");
            root.transform.SetParent(transform, false);
            root.transform.position = OpenSeaCenter;

            var waves = BuildWater(root, "OpenSeaWater", "OpenSeaGlints", seaSprite);
            var boatSprite = Resources.Load<Sprite>(BeachArtPrefix + "boat");
            if (boatSprite != null)
            {
                waves.SetBoat(CreateBeachSprite(root.transform, "OpenSeaBoat", boatSprite, 10).transform);
            }
        }

        /// <summary>True when a tap is on the dock or the boat tied up beside it.</summary>
        public bool IsDockAtWorld(Vector3 worldPosition)
        {
            if (_mooredBoatRenderer == null)
            {
                return false;
            }

            var bounds = _mooredBoatRenderer.bounds;
            if (bounds.Contains(new Vector3(worldPosition.x, worldPosition.y, bounds.center.z)))
            {
                return true;
            }

            var cell = IsoMath.WorldToCellFloat(worldPosition - BeachCenter);
            return Mathf.Abs(cell.x - DockCell.x) <= DockHalfLength + DockTapMargin &&
                   Mathf.Abs(cell.y - DockCell.y) <= DockHalfWidth + DockTapMargin;
        }

        /// <summary>The middle of the dock, for moving the camera or tapping it.</summary>
        public static Vector3 DockWorldPosition => BeachCenter + IsoMath.CellToWorld(DockCell);

        /// <summary>
        /// The Cocktail Bar: part of the beach from the start, not bought or placed. Painted art
        /// (NewAssets/Buildings/Cocktail_Bar.png) wins over the placeholder.
        /// </summary>
        private void BuildCocktailBar(Transform root)
        {
            var sprite = UIFactory.Icon("building_cocktail_bar");
            if (sprite == null)
            {
                Debug.LogError("Cocktail Bar art is missing. Run Tools > Farm Game > Rebuild Cocktail Bar Art.");
                return;
            }

            _cocktailBarRenderer = CreateBeachSprite(root, "CocktailBar", sprite, 100 + Mathf.RoundToInt((BarCell.x + BarCell.y) * 4f));
            _cocktailBarRenderer.transform.localPosition = IsoMath.CellToWorld(BarCell);
            var scale = BarWorldWidth / sprite.bounds.size.x;
            _cocktailBarRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>True when a tap is on the Cocktail Bar's picture.</summary>
        public bool IsCocktailBarAtWorld(Vector3 worldPosition)
        {
            if (_cocktailBarRenderer == null)
            {
                return false;
            }

            var bounds = _cocktailBarRenderer.bounds;
            return bounds.Contains(new Vector3(worldPosition.x, worldPosition.y, bounds.center.z));
        }

        /// <summary>The middle of the Cocktail Bar, for moving the camera or tapping it.</summary>
        public Vector3 CocktailBarWorldPosition =>
            _cocktailBarRenderer != null ? _cocktailBarRenderer.bounds.center : BeachCenter;

        private static SpriteRenderer CreateGlints(Transform root, string name, Sprite sprite, float alpha)
        {
            var glints = CreateBeachSprite(root, name, sprite, 1);
            glints.drawMode = SpriteDrawMode.Tiled;
            glints.tileMode = SpriteTileMode.Continuous;
            glints.size = SeaSize + SeaTile * 2f;
            glints.color = new Color(1f, 1f, 1f, alpha);
            return glints;
        }

        /// <summary>
        /// Palms, rocks and shells, placed by cell like things on the farm. The camera starts on the
        /// cells from -6 to 6: (-6, -6) is at the back and (6, 6) at the front. The dock is at
        /// (5 to 9, -0.1 to 1.3), so keep that clear.
        /// </summary>
        private static void DecorateBeach(Transform root)
        {
            // Further off, for when the player zooms out over the sand.
            PlaceBeachProp(root, "palm", -9f, -2f, 0.62f, false, false);
            PlaceBeachProp(root, "palm", -3f, -10f, 0.56f, true, false);
            PlaceBeachProp(root, "palm", -10f, 5f, 0.6f, true, false);
            PlaceBeachProp(root, "palm", -5f, 10f, 0.58f, false, false);
            PlaceBeachProp(root, "palm", 3f, -11f, 0.6f, false, false);
            PlaceBeachProp(root, "rock", -8f, 7f, 0.7f, false, false);
            PlaceBeachProp(root, "rock", -2f, -8f, 0.6f, true, false);

            // Palms keep to the sides and back, so the middle of the sand stays open.
            PlaceBeachProp(root, "palm", -4.4f, 0.8f, 0.58f, false, false);
            PlaceBeachProp(root, "palm", 0.5f, -4.6f, 0.56f, true, false);
            PlaceBeachProp(root, "palm", -2.6f, 4.4f, 0.6f, false, false);
            PlaceBeachProp(root, "palm", 4.4f, -1.6f, 0.6f, true, false);
            PlaceBeachProp(root, "palm", 4.8f, 2.8f, 0.5f, true, false);

            PlaceBeachProp(root, "rock", -1.5f, -4.6f, 0.6f, false, false);
            PlaceBeachProp(root, "rock", 2.8f, -3.6f, 0.7f, false, false);
            PlaceBeachProp(root, "rock", 3.4f, -3.1f, 0.4f, true, false);
            PlaceBeachProp(root, "rock", -0.8f, 4.6f, 0.6f, true, false);

            PlaceBeachProp(root, "starfish", 0.4f, 0.8f, 0.85f, false, true);
            PlaceBeachProp(root, "starfish", 2.6f, 2.2f, 0.7f, true, true);
            PlaceBeachProp(root, "starfish", -2.4f, 0.4f, 0.65f, true, true);
            PlaceBeachProp(root, "shell", -0.8f, -1.6f, 0.65f, false, true);
            PlaceBeachProp(root, "shell", 1.4f, -1.2f, 0.55f, true, true);
            PlaceBeachProp(root, "shell", 0.6f, 3.4f, 0.7f, false, true);
            PlaceBeachProp(root, "shell", -1.7f, 2.3f, 0.5f, true, true);
        }

        private static void PlaceBeachProp(Transform root, string art, float cellA, float cellB, float scale, bool flip, bool flat)
        {
            var sprite = Resources.Load<Sprite>(BeachArtPrefix + art);
            if (sprite == null)
            {
                return;
            }

            // Further forward on the grid draws in front, as on the farm.
            var renderer = CreateBeachSprite(root, $"Beach_{art}", sprite, 100 + Mathf.RoundToInt((cellA + cellB) * 4f));
            renderer.transform.localPosition = IsoMath.CellToWorld(new Vector2(cellA, cellB));

            // Things lying on the sand are foreshortened like the ground.
            renderer.transform.localScale = new Vector3(scale, flat ? scale * 0.62f : scale, 1f);
            renderer.flipX = flip;
        }

        private static SpriteRenderer CreateBeachSprite(Transform root, string name, Sprite sprite, int order)
        {
            var spriteObject = new GameObject(name);
            spriteObject.transform.SetParent(root, false);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = GroundSortingLayer;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
