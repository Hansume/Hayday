using System.IO;
using FarmGame.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmGame.Tests
{
    /// <summary>Art must stay inside its grid footprint. It may rise above the back edges, since things have height.</summary>
    public sealed class ArtFitTests
    {
        /// <summary>Slightly over what the importer allows, for each edge, to cover rounding from resizing.</summary>
        private const float AllowedOutsideShare = 0.03f;

        /// <summary>The same allowance when all four slanted edges count, rather than the two front ones.</summary>
        private const float AllowedOutsideShareAllEdges = 0.05f;

        [Test]
        public void EveryBuilding_StaysInsideItsFootprint()
        {
            var content = Resources.Load<ContentDatabase>("Data/ContentDatabase");
            Assert.IsNotNull(content);

            foreach (var building in content.Buildings)
            {
                if (building == null)
                {
                    continue;
                }

                var worldWidth = building.Footprint.x + building.Footprint.y;
                var scale = worldWidth / building.WorldSprite.bounds.size.x;
                AssertInside(building.Id, building.WorldSprite, worldWidth, scale);
            }
        }

        [TestCase("fish_pond")]
        [TestCase("cow_stable")]
        public void FlatBuilding_LiesInsideItsWholeFootprint(string id)
        {
            var building = Resources.Load<ContentDatabase>("Data/ContentDatabase").GetBuilding(id);
            var worldWidth = building.Footprint.x + building.Footprint.y;
            var scale = worldWidth / building.WorldSprite.bounds.size.x;

            AssertInside(building.Id, building.WorldSprite, worldWidth, scale, true);
        }

        [Test]
        public void CowStable_SitsClearOfItsEdges()
        {
            var stable = Resources.Load<ContentDatabase>("Data/ContentDatabase").GetBuilding("cow_stable");
            var scale = 6f / stable.WorldSprite.bounds.size.x;

            // Inside a diamond 95% as big as its 3 x 3 footprint, not just the footprint.
            AssertInside(stable.Id, stable.WorldSprite, 6f * 0.95f, scale, true);
        }

        [Test]
        public void EmptyField_StaysInsideItsCell()
        {
            var soil = Resources.Load<Sprite>("Art/Field/field_empty");
            Assert.IsNotNull(soil);

            AssertInside("field_empty", soil, 2f, 1f);
        }

        private static void AssertInside(string name, Sprite sprite, float worldWidth, float scale, bool includeBackEdges = false)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))), name);
                var pixels = texture.GetPixels32();
                var half = worldWidth * 0.5f;
                var visible = 0;
                var outside = 0;
                for (var y = 0; y < texture.height; y++)
                {
                    for (var x = 0; x < texture.width; x++)
                    {
                        if (pixels[y * texture.width + x].a <= 128)
                        {
                            continue;
                        }

                        visible++;
                        var worldX = (x + 0.5f - sprite.pivot.x) / sprite.pixelsPerUnit * scale;
                        var worldY = (y + 0.5f - sprite.pivot.y) / sprite.pixelsPerUnit * scale;
                        var frontEdge = -half * 0.5f * (1f - Mathf.Min(1f, Mathf.Abs(worldX) / half));
                        if (Mathf.Abs(worldX) > half || worldY < frontEdge || (includeBackEdges && worldY > -frontEdge))
                        {
                            outside++;
                        }
                    }
                }

                Assert.Greater(visible, 0, name);
                Assert.LessOrEqual(
                    outside / (float)visible,
                    includeBackEdges ? AllowedOutsideShareAllEdges : AllowedOutsideShare,
                    $"{name}: too much of it is outside its grid cells.");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
