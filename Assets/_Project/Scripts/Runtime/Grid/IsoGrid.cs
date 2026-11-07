using UnityEngine;

namespace FarmGame.Grid
{
    public sealed class IsoGrid
    {
        public int Width { get; }
        public int Height { get; }
        public Vector3 Origin { get; }
        public Bounds WorldBounds { get; }

        public IsoGrid(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);

            var centerX = (Width - Height) * 0.5f;
            var centerY = -(Width + Height - 2) * 0.25f;
            Origin = new Vector3(-centerX, -centerY, 0f);

            var halfWidth = (Width + Height) * 0.5f;
            var halfHeight = (Width + Height) * 0.25f;
            WorldBounds = new Bounds(Vector3.zero, new Vector3(halfWidth * 2f, halfHeight * 2f, 1f));
        }

        public Vector3 CellToWorld(Vector2Int cell)
        {
            return Origin + IsoMath.CellToWorld(cell);
        }

        public Vector3 CellToWorld(Vector2 cell)
        {
            return Origin + IsoMath.CellToWorld(cell);
        }

        public Vector2Int WorldToCell(Vector3 world)
        {
            return IsoMath.WorldToCell(world - Origin);
        }

        public bool Contains(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }
    }
}
