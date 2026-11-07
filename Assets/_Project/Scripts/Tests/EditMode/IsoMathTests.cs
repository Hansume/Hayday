using FarmGame.Grid;
using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class IsoMathTests
    {
        [Test]
        public void CellToWorld_WorldToCell_RoundTrips()
        {
            for (var y = 0; y < 12; y++)
            {
                for (var x = 0; x < 12; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var world = IsoMath.CellToWorld(cell);
                    Assert.AreEqual(cell, IsoMath.WorldToCell(world), $"Failed for {cell}");
                }
            }
        }

        [Test]
        public void IsoGrid_CentersWorldBounds()
        {
            var grid = new IsoGrid(12, 12);

            Assert.AreEqual(0f, grid.WorldBounds.center.x, 0.001f);
            Assert.AreEqual(0f, grid.WorldBounds.center.y, 0.001f);
            Assert.AreEqual(24f, grid.WorldBounds.size.x, 0.001f);
            Assert.AreEqual(12f, grid.WorldBounds.size.y, 0.001f);
        }

        [Test]
        public void IsoGrid_AsymmetricLayout_IsCentered()
        {
            var grid = new IsoGrid(10, 8);
            var cell = new Vector2Int(7, 2);

            Assert.AreEqual(0f, grid.WorldBounds.center.x, 0.001f);
            Assert.AreEqual(0f, grid.WorldBounds.center.y, 0.001f);
            Assert.AreEqual(18f, grid.WorldBounds.size.x, 0.001f);
            Assert.AreEqual(9f, grid.WorldBounds.size.y, 0.001f);
            Assert.AreEqual(cell, grid.WorldToCell(grid.CellToWorld(cell)));
        }

        [Test]
        public void IsoGrid_Contains_RejectsOutsideCells()
        {
            var grid = new IsoGrid(12, 12);

            Assert.IsTrue(grid.Contains(new Vector2Int(0, 0)));
            Assert.IsTrue(grid.Contains(new Vector2Int(11, 11)));
            Assert.IsFalse(grid.Contains(new Vector2Int(-1, 0)));
            Assert.IsFalse(grid.Contains(new Vector2Int(12, 0)));
        }

        [Test]
        public void FootprintWorldWidth_MatchesIsometricBounds()
        {
            Assert.AreEqual(2f, IsoMath.FootprintWorldWidth(new Vector2Int(1, 1)), 0.001f);
            Assert.AreEqual(3f, IsoMath.FootprintWorldWidth(new Vector2Int(2, 1)), 0.001f);
            Assert.AreEqual(4f, IsoMath.FootprintWorldWidth(new Vector2Int(2, 2)), 0.001f);
            Assert.AreEqual(6f, IsoMath.FootprintWorldWidth(new Vector2Int(3, 3)), 0.001f);
        }
    }
}
