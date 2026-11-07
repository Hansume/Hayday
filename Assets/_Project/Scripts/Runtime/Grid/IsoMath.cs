using UnityEngine;

namespace FarmGame.Grid
{
    public static class IsoMath
    {
        public const float TileWidth = 2f;
        public const float TileHeight = 1f;

        public static Vector3 CellToWorld(Vector2Int cell)
        {
            return CellToWorld(new Vector2(cell.x, cell.y));
        }

        public static Vector3 CellToWorld(Vector2 cell)
        {
            var x = (cell.x - cell.y) * (TileWidth * 0.5f);
            var y = -(cell.x + cell.y) * (TileHeight * 0.5f);
            return new Vector3(x, y, 0f);
        }

        public static float FootprintWorldWidth(Vector2Int footprint)
        {
            var span = Mathf.Max(1, footprint.x) + Mathf.Max(1, footprint.y);
            return span * (TileWidth * 0.5f);
        }

        public static Vector2 WorldToCellFloat(Vector3 world)
        {
            var x = world.x / TileWidth - world.y / TileHeight;
            var y = -world.y / TileHeight - world.x / TileWidth;
            return new Vector2(x, y);
        }

        public static Vector2Int WorldToCell(Vector3 world)
        {
            var cell = WorldToCellFloat(world);
            return new Vector2Int(Mathf.RoundToInt(cell.x), Mathf.RoundToInt(cell.y));
        }
    }
}
