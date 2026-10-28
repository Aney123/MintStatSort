using System;
using System.Collections.Generic;
using System.Linq;

namespace MintStatSort
{
    // Retain the existing serialized category field and legacy IDs (including
    // defs:A|B). Escape each ID so translated labels cannot split a selection.
    public static class CategorySelection
    {
        public static List<string> Unpack(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "all") return new List<string>();
            var ids = value.StartsWith("multi:", StringComparison.Ordinal)
                ? value.Substring(6).Split(',').Select(Uri.UnescapeDataString)
                : new[] { value };
            return ids.Where(id => !string.IsNullOrWhiteSpace(id) && id != "all")
                .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        }
        public static string Pack(IEnumerable<string> ids)
        {
            var unique = (ids ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id) && id != "all")
                .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
            return unique.Count == 0 ? "all" : unique.Count == 1 ? unique[0] : "multi:" + string.Join(",", unique.Select(Uri.EscapeDataString));
        }
        public static string Toggle(string value, string id)
        {
            if (id == "all") return "all";
            var ids = Unpack(value);
            if (!ids.Remove(id)) ids.Add(id);
            return Pack(ids);
        }
        public static string Remove(string value, string id) => Pack(Unpack(value).Where(item => item != id));
        public static string Available(string value, IEnumerable<string> available)
        {
            var set = new HashSet<string>(available, StringComparer.Ordinal);
            return Pack(Unpack(value).Where(set.Contains));
        }
        public static bool Contains(string value, string id) => id == "all" ? Unpack(value).Count == 0 : Unpack(value).Contains(id);
    }
}
