using System.Collections.Generic;

namespace FarmGame.Save
{
    /// <summary>
    /// Brings older saves up to date with renamed content, so nothing the player owns is dropped
    /// as "unknown" on load. Runs on every loaded save before the game reads it.
    /// </summary>
    public static class SaveMigrations
    {
        /// <summary>
        /// Version 14: cabbage and sunflower became soybean and chili; the Country Kitchen became
        /// the Bakery; the Creamery and Goat Dairy merged into the Dairy (a farm that had both
        /// keeps two Dairies, each with its own queue).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> RenamedIdsV14 = new Dictionary<string, string>
        {
            { "cabbage", "soybean" },
            { "sunflower", "chili" },
            { "country_kitchen", "bakery" },
            { "creamery", "dairy" },
            { "goat_dairy", "dairy" }
        };

        public static void Apply(SaveDataV1 data)
        {
            if (data == null)
            {
                return;
            }

            if (data.version < 14)
            {
                RenameIds(data, RenamedIdsV14);
            }

            // Version 15: orders take items instead of counting progress, and can't ask for feed
            // or coins. Old notices don't fit, so the board starts over with new orders.
            if (data.version < 15)
            {
                data.notices = new List<NoticeSlotSaveDto>();
            }

            // Version 19: lines are longer and larger, and no longer wait for a juice. An old line
            // or wait would hide that for hours, so the bar starts over with a new line.
            if (data.version < 19)
            {
                data.barLine = new List<BarCustomerDto>();
                data.barLineEndsAtUtcTicks = 0L;
                data.barNextLineAtUtcTicks = 0L;
            }

            data.version = SaveDataV1.CurrentVersion;
        }

        private static void RenameIds(SaveDataV1 data, IReadOnlyDictionary<string, string> renames)
        {
            string Map(string id)
            {
                return id != null && renames.TryGetValue(id, out var renamed) ? renamed : id;
            }

            if (data.inventory != null)
            {
                for (var i = 0; i < data.inventory.Count; i++)
                {
                    var stack = data.inventory[i];
                    stack.itemId = Map(stack.itemId);
                    data.inventory[i] = stack;
                }
            }

            if (data.plots != null)
            {
                for (var i = 0; i < data.plots.Count; i++)
                {
                    var plot = data.plots[i];
                    plot.cropId = Map(plot.cropId);
                    data.plots[i] = plot;
                }
            }

            if (data.buildings != null)
            {
                for (var i = 0; i < data.buildings.Count; i++)
                {
                    var building = data.buildings[i];
                    building.buildingId = Map(building.buildingId);
                    if (building.production != null)
                    {
                        for (var j = 0; j < building.production.Count; j++)
                        {
                            var job = building.production[j];
                            job.itemId = Map(job.itemId);
                            building.production[j] = job;
                        }
                    }

                    data.buildings[i] = building;
                }
            }

            if (data.visitors != null)
            {
                for (var i = 0; i < data.visitors.Count; i++)
                {
                    var visitor = data.visitors[i];
                    if (visitor.requests != null)
                    {
                        for (var j = 0; j < visitor.requests.Count; j++)
                        {
                            var request = visitor.requests[j];
                            request.itemId = Map(request.itemId);
                            visitor.requests[j] = request;
                        }
                    }

                    if (visitor.offers != null)
                    {
                        for (var j = 0; j < visitor.offers.Count; j++)
                        {
                            var offer = visitor.offers[j];
                            offer.itemId = Map(offer.itemId);
                            visitor.offers[j] = offer;
                        }
                    }
                }
            }
        }
    }
}
