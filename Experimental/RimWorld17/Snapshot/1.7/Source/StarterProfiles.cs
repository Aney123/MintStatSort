using System.Collections.Generic;
using System.Linq;
using Verse;
namespace MintStatSort
{
    internal static class StarterProfiles
    {
        internal static readonly string[] Names = { "Protection", "Cold", "Light", "Range" };
        internal static ComparisonPreferences Create(string name, SortSettings current)
        {
            var result = ComparisonPreferences.Capture(current);
            result.Criteria = new List<SortCriterion>(); result.Columns = new List<ComparisonColumn>();
            result.Metric = "original"; result.OrderMode = "criteria"; result.Descending = true;
            result.Category = "all"; result.FavoritesOnly = result.OnlyStockReady = false;
            void Add(string id, bool higher, int weight)
            { if (Metrics.Resolve(id) != null) result.Criteria.Add(new SortCriterion { Id = id, HigherIsBetter = higher, Weight = weight }); }
            switch (name)
            {
                case "Protection": result.Category = "kind:apparel"; Add("stat:ArmorRating_Sharp", true, 2); Add("stat:ArmorRating_Blunt", true, 1); break;
                case "Cold": result.Category = "kind:apparel"; Add("stat:Insulation_Cold", true, 2); Add("stat:Mass", false, 1); break;
                case "Light": Add("stat:Mass", false, 1); break;
                case "Range": result.Category = "kind:ranged"; Add("weapon.range", true, 2); Add("weapon.damage", true, 1); break;
            }
            return result;
        }
    }
}
