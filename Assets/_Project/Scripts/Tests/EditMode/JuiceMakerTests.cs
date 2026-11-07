using System;
using System.Collections.Generic;
using FarmGame.Content;
using NUnit.Framework;

namespace FarmGame.Tests
{
    /// <summary>The Juice Maker's rules, checked against the balance files the game ships with.</summary>
    public sealed class JuiceMakerTests
    {
        private const string JuiceMakerId = "juice_maker";

        private BalanceData _data;
        private Dictionary<string, BuildingBalance> _treesByFruit;
        private List<GoodsRecipeBalance> _juices;

        [SetUp]
        public void SetUp()
        {
            Assert.IsTrue(
                BalanceLoader.TryLoad("Assets/_Project/Config/Balance", out _data, out var errors),
                string.Join("\n", errors));

            _treesByFruit = new Dictionary<string, BuildingBalance>();
            foreach (var building in _data.buildings)
            {
                if (building.category == "Fruit")
                {
                    _treesByFruit[building.productId] = building;
                }
            }

            _juices = new List<GoodsRecipeBalance>();
            foreach (var recipe in _data.goodsRecipes)
            {
                if (recipe.buildingId == JuiceMakerId)
                {
                    _juices.Add(recipe);
                }
            }
        }

        [Test]
        public void EveryFruit_HasOneJuice()
        {
            var made = new HashSet<string>();
            foreach (var juice in _juices)
            {
                foreach (var input in juice.inputs)
                {
                    if (_treesByFruit.ContainsKey(input.itemId))
                    {
                        Assert.IsTrue(made.Add(input.itemId), $"{input.itemId} is in more than one juice.");
                    }
                }
            }

            CollectionAssert.AreEquivalent(_treesByFruit.Keys, made);
        }

        [Test]
        public void EveryJuice_TakesOneFruitAndOneOrTwoOtherIngredients()
        {
            foreach (var juice in _juices)
            {
                var fruits = 0;
                var others = 0;
                foreach (var input in juice.inputs)
                {
                    if (_treesByFruit.ContainsKey(input.itemId))
                    {
                        fruits++;
                        Assert.AreEqual(1, input.amount, $"{juice.outputId} should take a single {input.itemId}.");
                    }
                    else
                    {
                        others++;
                    }
                }

                Assert.AreEqual(1, fruits, $"{juice.outputId} fruit count");
                Assert.That(others, Is.InRange(1, 2), $"{juice.outputId} other ingredients");
            }
        }

        [Test]
        public void EveryJuice_UnlocksWithItsFruit()
        {
            foreach (var juice in _juices)
            {
                var fruit = Array.Find(juice.inputs, input => _treesByFruit.ContainsKey(input.itemId));
                Assert.AreEqual(_treesByFruit[fruit.itemId].unlockLevel, juice.unlockLevel, juice.outputId);
            }
        }

        [Test]
        public void JuiceMaker_IsAvailableForTheFirstJuice()
        {
            var maker = Array.Find(_data.buildings, building => building.id == JuiceMakerId);
            Assert.IsNotNull(maker);
            Assert.AreEqual("Goods", maker.category);

            var firstFruit = int.MaxValue;
            foreach (var tree in _treesByFruit.Values)
            {
                firstFruit = Math.Min(firstFruit, tree.unlockLevel);
            }

            Assert.AreEqual(firstFruit, maker.unlockLevel);
        }
    }
}
