using NUnit.Framework;
using UnityEngine;

namespace FarmGame.Tests
{
    public sealed class PackedArtTests
    {
        /// <summary>A sprite rewritten without repacking the atlas has no texture in Play mode, so it draws nothing.</summary>
        [TestCase("Art/Icons")]
        [TestCase("Art/Field")]
        [TestCase("Art/Ground")]
        [TestCase("Art/Placeholders")]
        public void EverySprite_HasATexture(string folder)
        {
            var sprites = Resources.LoadAll<Sprite>(folder);

            Assert.IsNotEmpty(sprites);
            foreach (var sprite in sprites)
            {
                Assert.IsNotNull(sprite.texture, $"{folder}/{sprite.name} has no texture; rebuild the sprite atlas.");
            }
        }
    }
}
