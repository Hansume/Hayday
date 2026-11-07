using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class PlaceholderArtTests
    {
        [Test]
        public void CropSprites_UseMoundCenteredPivots()
        {
            var crop = Resources.Load<Sprite>("Art/Placeholders/crop_wheat_3");

            Assert.IsNotNull(crop);
            Assert.AreEqual(0.5f, crop.pivot.x / crop.rect.width, 0.01f);
            Assert.AreEqual(29f / 160f, crop.pivot.y / crop.rect.height, 0.01f);
        }

        [Test]
        public void CoopSprite_UsesBaseCenteredPivot()
        {
            var building = Resources.Load<Sprite>("Art/Placeholders/building_coop");

            Assert.IsNotNull(building);
            Assert.AreEqual(0.5f, building.pivot.x / building.rect.width, 0.01f);
            Assert.AreEqual(26f / 256f, building.pivot.y / building.rect.height, 0.01f);
        }
    }
}
