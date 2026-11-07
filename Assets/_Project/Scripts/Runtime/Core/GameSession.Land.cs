using System.Collections.Generic;
using FarmGame.Land;
using FarmGame.Save;
using UnityEngine;

namespace FarmGame.Core
{
    /// <summary>
    /// Land: filled orders and served visitors sometimes give a Land Deed, and a deed buys one
    /// section of the map next to land the player already owns.
    /// </summary>
    public sealed partial class GameSession
    {
        private readonly System.Random _deedRandom = new System.Random();

        /// <summary>Spends a deed on a section of land.</summary>
        public bool TryBuyLand(Vector2Int section, out string message)
        {
            if (!Land.CanBuy(section))
            {
                message = Land.IsOwned(section) ? "Already yours" : "Too far";
                return false;
            }

            if (!Land.TryBuy(section))
            {
                message = $"Need 1 {FarmLand.DeedName}";
                return false;
            }

            message = "New land!";
            SaveSoon();
            return true;
        }

        /// <summary>
        /// Gives a deed with this chance, unless the player already holds enough for all the land
        /// that's left. Returns the text to add to the reward message: empty when there's no deed.
        /// </summary>
        private string RollForDeed(float chance)
        {
            if (!Land.NeedsMoreDeeds || !(_deedRandom.NextDouble() < chance))
            {
                return string.Empty;
            }

            Land.AddDeed();
            return $" +1 {FarmLand.DeedName}";
        }

        private FarmLand CreateLand(SaveDataV1 saveData)
        {
            if (saveData == null)
            {
                return new FarmLand(_content.Layout);
            }

            var sections = new List<Vector2Int>();
            if (saveData.landSections != null)
            {
                for (var i = 0; i < saveData.landSections.Count; i++)
                {
                    sections.Add(new Vector2Int(saveData.landSections[i].x, saveData.landSections[i].y));
                }
            }

            return new FarmLand(_content.Layout, saveData.landDeeds, sections);
        }
    }
}
