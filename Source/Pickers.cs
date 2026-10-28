using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MintStatSort
{
    internal static class PickerTiles
    {
        public static bool Matches(string name, string search)
            => string.IsNullOrWhiteSpace(search) || name.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }
    internal static class MaterialInventory
    {
        internal static Dictionary<ThingDef, long> Count(Map map, IEnumerable<ThingDef> materials)
        {
            var counts = new Dictionary<ThingDef, long>();
            foreach (var material in materials)
                counts[material] = map == null ? 0 : ReferenceCore.StoredQuantity(map.listerThings.ThingsOfDef(material),
                    t => t.stackCount, t => t.Spawned, t => t.IsInAnyStorage());
            return counts;
        }
    }
}
